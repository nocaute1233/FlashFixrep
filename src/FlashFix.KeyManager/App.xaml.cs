using System.Windows;
using System.Globalization;

namespace FlashFix.KeyManager;

public partial class App : Application
{
    public App()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
    }
}

