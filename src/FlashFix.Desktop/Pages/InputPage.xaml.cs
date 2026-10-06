using FlashFix.Optimization;
using FlashFix_Desktop.Design;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace FlashFix_Desktop.Pages;

public sealed partial class InputPage : Page
{
    private readonly OptimizationEngine _engine = OptimizationEngine.CreateDefault();
    private string _category = "mouse";
    private bool _busy;

    public InputPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            Motion.Reveal(HeaderSection);
            Motion.Reveal(NoticeSection, 60);
            await LoadAsync();
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _category = e.Parameter as string == "keyboard" ? "keyboard" : "mouse";
        TitleText.Text = _category == "mouse" ? "Mouse" : "Teclado";
        SubtitleText.Text = _category == "mouse"
            ? "Controle o ponteiro do Windows com ajustes reversíveis."
            : "Ajuste a repetição de teclas com segurança.";
        EyebrowText.Text = _category == "mouse" ? "AJUSTES / MOUSE" : "AJUSTES / TECLADO";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        if (_busy) return;
        _busy = true;
        FeedbackText.Text = "Lendo configurações do Windows…";
        TweakList.Children.Clear();
        try
        {
            foreach (var definition in TweakCatalog.All.Where(x => x.Category == _category))
            {
                var status = await _engine.GetStatusAsync(definition.Id);
                TweakList.Children.Add(MakeCard(status));
            }
            FeedbackText.Text = "Os ajustes só são aplicados quando você escolhe uma ação.";
            Motion.Reveal(TweakList, 70);
        }
        catch (Exception error) { FeedbackText.Text = $"Não foi possível ler as configurações: {error.Message}"; }
        finally { _busy = false; }
    }

    private Border MakeCard(TweakStatus status)
    {
        var border = new Border
        {
            Background = Brush("#171719"),
            BorderBrush = Brush("#343438"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(22)
        };
        AutomationProperties.SetAutomationId(border, $"TweakCard_{status.Definition.Id}");
        var body = new StackPanel { Spacing = 13 };
        body.Children.Add(new TextBlock
        {
            Text = status.Definition.Category == "mouse" ? "MOUSE / WINDOWS" : "TECLADO / WINDOWS",
            FontSize = 11, FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
            Foreground = Brush("#A1A1A4"), CharacterSpacing = 80
        });
        body.Children.Add(new TextBlock
        {
            Text = status.Definition.Name, FontSize = 21,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
            Foreground = Brush("#F5F5F4")
        });
        body.Children.Add(new TextBlock
        {
            Text = status.Definition.Description, FontSize = 13,
            Foreground = Brush("#A7A7AA"), TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(new Border { Height = 1, Background = Brush("#343438"), Margin = new Thickness(0, 4, 0, 4) });
        body.Children.Add(new TextBlock
        {
            Text = $"ESTADO ATUAL  ·  {FormatCurrent(status)}",
            Foreground = Brush("#E4E4E4"), FontSize = 12,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }
        });
        body.Children.Add(new TextBlock
        {
            Text = $"Impacto esperado: {status.Definition.ExpectedImpact}  ·  Risco: {status.Definition.Risk}",
            Foreground = Brush("#9C9CA0"), FontSize = 12, TextWrapping = TextWrapping.Wrap
        });
        var actions = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 7, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var apply = new Button
        {
            Content = "Aplicar ajuste", Tag = status.Definition.Id,
            Background = Brush("#ECECEA"), Foreground = Brush("#111113"),
            BorderThickness = new Thickness(0), Padding = new Thickness(16, 9, 16, 9),
            IsEnabled = !status.MatchesTarget && !status.CanRestore &&
                status.Label != "Alterado fora do FlashFix"
        };
        AutomationProperties.SetAutomationId(apply, $"Apply_{status.Definition.Id}");
        apply.Click += Apply_Click;
        actions.Children.Add(apply);
        var restore = new Button
        {
            Content = status.Label == "Alterado fora do FlashFix"
                ? "Restaurar mesmo assim" : "Restaurar original",
            Tag = status.Definition.Id,
            Background = Brush("#2A2A2D"), Foreground = Brush("#F5F5F4"),
            BorderThickness = new Thickness(0), Padding = new Thickness(16, 9, 16, 9),
            IsEnabled = status.CanRestore || status.Label == "Alterado fora do FlashFix"
        };
        Grid.SetColumn(restore, 1);
        AutomationProperties.SetAutomationId(restore, $"Restore_{status.Definition.Id}");
        restore.Click += Restore_Click;
        actions.Children.Add(restore);
        var state = new TextBlock
        {
            Text = status.Label, Foreground = Brush("#A1A1A4"),
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(state, 2);
        actions.Children.Add(state);
        body.Children.Add(actions);
        border.Child = body;
        return border;
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } || _busy) return;
        var definition = TweakCatalog.Get(id);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = definition.Name,
            Content = $"{definition.Description}\n\nO valor original será salvo antes da alteração. Risco: {definition.Risk}.",
            PrimaryButtonText = "Aplicar ajuste",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        _busy = true;
        try
        {
            var result = await _engine.ApplyAsync(id);
            _busy = false;
            await LoadAsync();
            FeedbackText.Text = result.Message;
        }
        catch (Exception error) { _busy = false; FeedbackText.Text = error.Message; }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } || _busy) return;
        TweakStatus status;
        try { status = await _engine.GetStatusAsync(id); }
        catch (Exception error) { FeedbackText.Text = error.Message; return; }
        var externalChange = status.Label == "Alterado fora do FlashFix";
        if (externalChange)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Mudança feita fora do FlashFix",
                Content = "O valor atual foi alterado por outro programa ou pelo Windows. Restaurar agora substituirá essa mudança pelo valor original salvo pelo FlashFix.",
                PrimaryButtonText = "Restaurar valor salvo",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        }
        _busy = true;
        try
        {
            var result = await _engine.RestoreAsync(id, externalChange);
            _busy = false;
            await LoadAsync();
            FeedbackText.Text = result.Message;
        }
        catch (Exception error) { _busy = false; FeedbackText.Text = error.Message; }
    }

    private static string FormatCurrent(TweakStatus status) => status.Definition.Kind switch
    {
        TweakKind.MouseAcceleration => status.Current.Length == 3
            ? $"Aceleração {(status.Current[2] == 0 ? "desativada" : "ativada")} · limites {status.Current[0]}/{status.Current[1]}"
            : "Indisponível",
        TweakKind.MouseSpeed => $"{status.Current[0]}/20",
        TweakKind.MouseWheelLines => status.Current[0] == -1
            ? "Uma página por etapa" : $"{status.Current[0]} linha(s) por etapa",
        TweakKind.MouseWheelChars => status.Current[0] == -1
            ? "Uma página por etapa" : $"{status.Current[0]} caractere(s) por etapa",
        TweakKind.KeyboardDelay => $"{status.Current[0]}/3 (0 = menor atraso)",
        TweakKind.KeyboardSpeed => $"{status.Current[0]}/31 (31 = mais rápida)",
        _ => "Indisponível"
    };

    private static SolidColorBrush Brush(string hex) =>
        new(Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16)));
}
