using System.Diagnostics;
using FlashFix.Core.Trainer;
using FlashFix_Desktop.Design;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace FlashFix_Desktop.Pages;

public sealed partial class TrainerPage : Page
{
    private sealed class Target(Ellipse shape, double x, double y)
    {
        public Ellipse Shape { get; } = shape;
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double Dx { get; set; } = 1;
        public double Dy { get; set; } = 0.65;
    }

    private readonly TrainerResultStore _store = TrainerResultStore.CreateDefault();
    private readonly Random _random = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _watch = new();
    private readonly List<Target> _targets = [];
    private TrainerSession? _session;
    private TimeSpan _lastShownAt;
    private TimeSpan _nextReflexAt;
    private TimeSpan _lastTrackingSample;
    private Point _pointer;
    private bool _pointerInside;

    public TrainerPage()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        Loaded += (_, _) =>
        {
            Motion.Reveal(HeaderSection);
            Motion.Reveal(ControlSection, 70);
            LoadResults();
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _watch.Stop();
            _session = null;
        };
    }

    private TrainerMode SelectedMode => ModeCombo.SelectedIndex switch
    {
        1 => TrainerMode.Tracking,
        2 => TrainerMode.ReflexShot,
        3 => TrainerMode.Gridshot,
        _ => TrainerMode.Flick
    };

    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeDescription is null) return;
        ModeDescription.Text = SelectedMode switch
        {
            TrainerMode.Tracking => "Acompanhe o alvo em movimento. A precisão mede a proporção de amostras em que o ponteiro permaneceu sobre ele.",
            TrainerMode.ReflexShot => "Espere o alvo aparecer e clique rapidamente. O tempo médio de reação é registrado nos acertos.",
            TrainerMode.Gridshot => "Três alvos aparecem ao mesmo tempo. Cada acerto substitui um deles em outra posição.",
            _ => "Clique no alvo assim que ele aparecer. Cada acerto muda sua posição."
        };
    }

    private void DifficultyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SizeSlider is null || _session is not null) return;
        SizeSlider.Value = DifficultyCombo.SelectedIndex switch { 1 => 35, 2 => 25, _ => 50 };
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var duration = DurationCombo.SelectedIndex switch { 1 => 60, 2 => 90, _ => 30 };
        _session = new TrainerSession(SelectedMode, duration);
        HeaderSection.Visibility = Visibility.Collapsed;
        ControlSection.Visibility = Visibility.Collapsed;
        ModeDescription.Visibility = Visibility.Collapsed;
        ActiveModeText.Text = $"{ModeName(_session.Mode).ToUpperInvariant()} / TREINO EM ANDAMENTO";
        ActiveModeText.Visibility = Visibility.Visible;
        ResultsHeading.Visibility = Visibility.Collapsed;
        ResultsList.Visibility = Visibility.Collapsed;
        _targets.Clear();
        ArenaCanvas.Children.Clear();
        _pointerInside = false;
        _lastTrackingSample = TimeSpan.Zero;
        _watch.Restart();
        _nextReflexAt = TimeSpan.FromMilliseconds(400 + _random.Next(450));
        TimeProgress.Maximum = duration;
        TimeProgress.Value = 0;
        TimeText.Text = $"{duration} s";
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        ModeCombo.IsEnabled = false;
        DifficultyCombo.IsEnabled = false;
        DurationCombo.IsEnabled = false;
        SizeSlider.IsEnabled = false;
        if (_session.Mode is TrainerMode.Flick or TrainerMode.Tracking)
            SpawnTarget();
        else if (_session.Mode == TrainerMode.Gridshot)
            for (var index = 0; index < 3; index++) SpawnTarget();
        StatusText.Text = _session.Mode == TrainerMode.Tracking
            ? "Mantenha o ponteiro sobre o alvo em movimento."
            : "Treino iniciado. Clique nos alvos.";
        UpdateStats();
        _timer.Start();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => EndSession(false);

    private void Timer_Tick(object? sender, object e)
    {
        if (_session is null) return;
        var elapsed = _watch.Elapsed;
        TimeProgress.Value = Math.Min(elapsed.TotalSeconds, _session.DurationSeconds);
        TimeText.Text = $"{Math.Max(0, _session.DurationSeconds - elapsed.TotalSeconds):0.0} s";
        if (elapsed.TotalSeconds >= _session.DurationSeconds)
        {
            EndSession(true);
            return;
        }
        if (_session.Mode == TrainerMode.ReflexShot && _targets.Count == 0 && elapsed >= _nextReflexAt)
            SpawnTarget();
        if (_session.Mode != TrainerMode.Tracking || _targets.Count == 0) return;

        var target = _targets[0];
        var speed = DifficultyCombo.SelectedIndex switch { 1 => 125d, 2 => 175d, _ => 85d };
        var dt = Math.Min(_timer.Interval.TotalSeconds, 0.05);
        var radius = target.Shape.Width / 2;
        target.X += target.Dx * speed * dt;
        target.Y += target.Dy * speed * dt;
        if (target.X < radius || target.X > ArenaCanvas.Width - radius)
        {
            target.Dx *= -1;
            target.X = Math.Clamp(target.X, radius, ArenaCanvas.Width - radius);
        }
        if (target.Y < radius || target.Y > ArenaCanvas.Height - radius)
        {
            target.Dy *= -1;
            target.Y = Math.Clamp(target.Y, radius, ArenaCanvas.Height - radius);
        }
        Place(target);
        if (elapsed - _lastTrackingSample >= TimeSpan.FromMilliseconds(50))
        {
            _lastTrackingSample = elapsed;
            _session.RecordTrackingSample(_pointerInside && Distance(_pointer, target) <= radius);
            UpdateStats();
        }
    }

    private void Arena_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _pointer = e.GetCurrentPoint(ArenaCanvas).Position;
        _pointerInside = _pointer.X is >= 0 and <= 740 && _pointer.Y is >= 0 and <= 400;
    }

    private void Arena_PointerExited(object sender, PointerRoutedEventArgs e) =>
        _pointerInside = false;

    private void Arena_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_session is null || _session.Mode == TrainerMode.Tracking) return;
        var point = e.GetCurrentPoint(ArenaCanvas).Position;
        var target = _targets.FirstOrDefault(x => Distance(point, x) <= x.Shape.Width / 2);
        if (target is null)
        {
            _session.RecordClick(false);
            UpdateStats();
            return;
        }
        var reaction = _session.Mode == TrainerMode.ReflexShot
            ? (_watch.Elapsed - _lastShownAt).TotalMilliseconds : (double?)null;
        _session.RecordClick(true, reaction);
        RemoveTarget(target);
        if (_session.Mode == TrainerMode.ReflexShot)
            _nextReflexAt = _watch.Elapsed + TimeSpan.FromMilliseconds(350 + _random.Next(650));
        else SpawnTarget();
        UpdateStats();
    }

    private void SpawnTarget()
    {
        var size = SizeSlider.Value;
        var radius = size / 2;
        var x = radius + 12 + _random.NextDouble() * (ArenaCanvas.Width - size - 24);
        var y = radius + 12 + _random.NextDouble() * (ArenaCanvas.Height - size - 24);
        var shape = new Ellipse
        {
            Width = size, Height = size,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 241, 241, 239)),
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 10, 10, 12)),
            StrokeThickness = 3
        };
        AutomationProperties.SetAutomationId(shape, "TrainerTarget");
        AutomationProperties.SetName(shape, "Alvo de treino");
        var target = new Target(shape, x, y);
        _targets.Add(target);
        ArenaCanvas.Children.Add(shape);
        Place(target);
        _lastShownAt = _watch.Elapsed;
    }

    private static double Distance(Point point, Target target) =>
        Math.Sqrt(Math.Pow(point.X - target.X, 2) + Math.Pow(point.Y - target.Y, 2));

    private static void Place(Target target)
    {
        Canvas.SetLeft(target.Shape, target.X - target.Shape.Width / 2);
        Canvas.SetTop(target.Shape, target.Y - target.Shape.Height / 2);
    }

    private void RemoveTarget(Target target)
    {
        _targets.Remove(target);
        ArenaCanvas.Children.Remove(target.Shape);
    }

    private void UpdateStats()
    {
        if (_session is null) return;
        ScoreText.Text = _session.Score.ToString("N0");
        HitsText.Text = _session.Hits.ToString("N0");
        AccuracyText.Text = $"{_session.AccuracyPercent:0}%";
    }

    private void EndSession(bool completed)
    {
        if (_session is null) return;
        _timer.Stop();
        _watch.Stop();
        ArenaCanvas.Children.Clear();
        _targets.Clear();
        HeaderSection.Visibility = Visibility.Visible;
        ControlSection.Visibility = Visibility.Visible;
        ModeDescription.Visibility = Visibility.Visible;
        ActiveModeText.Visibility = Visibility.Collapsed;
        ResultsHeading.Visibility = Visibility.Visible;
        ResultsList.Visibility = Visibility.Visible;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        ModeCombo.IsEnabled = true;
        DifficultyCombo.IsEnabled = true;
        DurationCombo.IsEnabled = true;
        SizeSlider.IsEnabled = true;
        if (completed)
        {
            var result = _session.Finish();
            try
            {
                _store.Add(result);
                LoadResults();
                StatusText.Text = result.AverageReactionMs is double reaction
                    ? $"Sessão concluída. Reação média: {reaction:0} ms. Resultado salvo."
                    : "Sessão concluída. Resultado salvo.";
            }
            catch (Exception error)
            {
                StatusText.Text = $"Sessão concluída, mas o resultado não pôde ser salvo: {error.Message}";
            }
        }
        else StatusText.Text = "Sessão interrompida. O resultado não foi salvo.";
        _session = null;
    }

    private void LoadResults()
    {
        ResultsList.Children.Clear();
        try
        {
            var results = _store.Latest();
            if (results.Count == 0)
            {
                ResultsList.Children.Add(new TextBlock
                {
                    Text = "Nenhuma sessão concluída até agora.",
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 166, 166, 170)),
                    FontSize = 13
                });
                return;
            }
            foreach (var result in results)
            {
                ResultsList.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 25, 25, 27)),
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(15, 11, 15, 11),
                    Child = new TextBlock
                    {
                        Text = $"{ModeName(result.Mode)}  ·  {result.FinishedAt.ToLocalTime():dd/MM/yyyy HH:mm}  ·  {result.Score:N0} pontos  ·  {result.AccuracyPercent:0}% de precisão",
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 231, 231, 231)),
                        FontSize = 13, TextWrapping = TextWrapping.Wrap
                    }
                });
            }
        }
        catch (Exception error) { StatusText.Text = $"Não foi possível abrir o histórico de treinos: {error.Message}"; }
    }

    private static string ModeName(TrainerMode mode) => mode switch
    {
        TrainerMode.ReflexShot => "Reflex Shot",
        _ => mode.ToString()
    };
}
