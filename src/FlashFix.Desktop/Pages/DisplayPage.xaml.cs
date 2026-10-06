using FlashFix.Hardware;
using FlashFix_Desktop.Design;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FlashFix_Desktop.Pages;

public sealed partial class DisplayPage : Page
{
    private readonly WindowsHardwareDetector _detector = new();
    private bool _loading;

    public DisplayPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            Motion.Reveal(HeaderSection);
            Motion.Reveal(DisplayCard, 70);
            await RefreshAsync();
        };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        RefreshButton.IsEnabled = false;
        ReadStatus.Text = "Consultando o Windows…";
        try
        {
            var snapshot = await _detector.CaptureAsync();
            DisplayValue.Text = snapshot.Display;
            GraphicsValue.Text = $"Placa de vídeo: {snapshot.Gpu}";
            ReadStatus.Text = $"Atualizado às {snapshot.CapturedAt:HH:mm}. Se houver mais de uma tela, confira cada uma nas Configurações do Windows.";
        }
        catch (Exception)
        {
            ReadStatus.Text = "Não foi possível ler os dados da tela agora. Tente novamente.";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            _loading = false;
        }
    }

    private async void OpenDisplay_Click(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:display"));

    private async void OpenAdvanced_Click(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:display-advanced"));
}
