using FlashFix.Hardware;
using FlashFix_Desktop.Design;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FlashFix_Desktop.Pages;

public sealed partial class SystemPage : Page
{
    private readonly WindowsHardwareDetector _detector = new();
    private bool _analyzing;

    public SystemPage()
    {
        InitializeComponent();
        Loaded += (_, _) => { Motion.Reveal(HeaderSection); Motion.Reveal(HeroSection, 70); };
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        if (_analyzing) return;
        _analyzing = true;
        AnalyzeButton.IsEnabled = false;
        AnalysisProgress.Visibility = Visibility.Visible;
        StatusText.Text = "Lendo processador, vídeo, memória, armazenamento e rede…";
        try
        {
            var snapshot = await _detector.CaptureAsync();
            var analysis = SystemAnalyzer.Analyze(snapshot);
            SummaryText.Text = analysis.Summary;
            AnalysisList.Children.Clear();
            foreach (var item in analysis.Items)
                AnalysisList.Children.Add(MakeItem(item));
            StatusText.Text = $"Análise concluída às {analysis.CapturedAt:HH:mm:ss}. Nenhuma configuração foi alterada.";
            Motion.Reveal(AnalysisList, 90);
        }
        catch (Exception error) { StatusText.Text = $"Falha na análise: {error.Message}"; }
        finally
        {
            AnalysisProgress.Visibility = Visibility.Collapsed;
            AnalyzeButton.IsEnabled = true;
            _analyzing = false;
        }
    }

    private static Border MakeItem(AnalysisItem item)
    {
        var body = new StackPanel { Spacing = 7 };
        body.Children.Add(new TextBlock
        {
            Text = item.Area, Foreground = Brush("#A1A1A4"), FontSize = 11,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }, CharacterSpacing = 80
        });
        body.Children.Add(new TextBlock
        {
            Text = item.Detected, Foreground = Brush("#F4F4F2"), FontSize = 17,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }, TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(new TextBlock
        {
            Text = item.Recommendation, Foreground = Brush("#A7A7AA"), FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        });
        return new Border
        {
            Background = Brush("#171719"), BorderBrush = Brush("#343438"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20), Child = body
        };
    }

    private static SolidColorBrush Brush(string hex) =>
        new(Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16)));
}
