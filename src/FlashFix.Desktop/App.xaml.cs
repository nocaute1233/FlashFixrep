using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using FlashFix.Client;
using System.Globalization;


namespace FlashFix_Desktop;

public partial class App : Application
{
    private Window? _window;
    public FlashFixApiClient ApiClient { get; } = new(deviceProof: new WindowsDeviceProof("FlashFix.License.v1"));
    public event EventHandler? SessionEnded;

    public async Task LogoutAsync()
    {
        try { await ApiClient.LogoutAsync(); }
        finally { SessionEnded?.Invoke(this, EventArgs.Empty); }
    }
    
    public App()
    {
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
            InitializeComponent();
            UnhandledException += (_, e) => LogStartupError(e.Exception);
        }
        catch (Exception e)
        {
            LogStartupError(e);
            throw;
        }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception e)
        {
            LogStartupError(e);
            throw;
        }
    }

    private static void LogStartupError(Exception exception)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlashFix", "logs");
        Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "startup.log"),
            $"{DateTimeOffset.UtcNow:O} {exception}{Environment.NewLine}");
    }
}
