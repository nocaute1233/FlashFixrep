using FlashFix.Hardware;
using FlashFix_Desktop.Design;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FlashFix_Desktop.Pages;

public sealed partial class HomePage : Page
{
    private readonly WindowsHardwareDetector _detector = new();
    private bool _loading;

    public HomePage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            Motion.Reveal(HeaderSection);
            Motion.Reveal(HeroSection, 65);
            Motion.Reveal(MetricsSection, 130);
            Motion.Reveal(HardwareSection, 195);
            await RefreshAsync();
        };
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        RefreshButton.IsEnabled = false;
        UpdatedText.Text = "Lendo dados…";
        try
        {
            var snapshot = await _detector.CaptureAsync();
            DeviceTypeText.Text = snapshot.FormFactor;
            OsText.Text = snapshot.Windows;
            UpdatedText.Text = $"Atualizado às {snapshot.CapturedAt:HH:mm:ss}";
            CpuNameText.Text = snapshot.Cpu;
            GpuNameText.Text = snapshot.Gpu;
            MemoryText.Text = snapshot.Memory;
            StorageText.Text = $"{snapshot.Storage} · {snapshot.StorageType}";
            NetworkText.Text = snapshot.Network;
            DisplayText.Text = snapshot.Display;
            SetMetric(CpuPercentText, CpuBar, snapshot.CpuPercent);
            SetMetric(GpuPercentText, GpuBar, snapshot.GpuPercent);
            SetMetric(RamPercentText, RamBar, snapshot.MemoryPercent);
            SetMetric(DiskPercentText, DiskBar, snapshot.StorageUsedPercent);
            GpuNoteText.Text = snapshot.GpuPercent is null ? "Dados indisponíveis" : "Carga atual";
            SummaryText.Text = snapshot.GpuPercent is null
                ? "CPU, RAM e espaço em disco identificados. O uso da GPU depende dos dados fornecidos pelo driver."
                : "CPU, GPU, RAM e espaço em disco identificados.";

            var profile = await ((App)Application.Current).ApiClient.GetProfileAsync();
            LicenseText.Text = profile.License?.ExpiresAt is DateTime expiry
                ? $"LICENÇA ATIVA · ATÉ {expiry.ToLocalTime():dd/MM/yyyy}"
                : profile.License is not null ? "LICENÇA ATIVA · VITALÍCIA" : "CONTA ADMINISTRATIVA";
        }
        catch (Exception error)
        {
            UpdatedText.Text = "Não foi possível concluir a leitura.";
            SummaryText.Text = error.Message;
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            _loading = false;
        }
    }

    private static void SetMetric(TextBlock text, ProgressBar bar, double? value)
    {
        text.Text = value is double number ? $"{number:0}%" : "—";
        bar.Value = value ?? 0;
    }
}
