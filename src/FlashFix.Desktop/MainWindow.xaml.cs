using FlashFix.Core.Navigation;
using FlashFix_Desktop.Design;
using FlashFix_Desktop.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FlashFix_Desktop;

public sealed partial class MainWindow : Window
{
    private NavigationViewItem? _overviewItem;
    private readonly Dictionary<string, NavigationViewItem> _navigationItems = new();
    private int _tourStep;
    private Guid _currentUserId;
    private string TourCompletedPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlashFix", $"tour-v1-{_currentUserId:N}.complete");

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 820));

        AddGroup("INÍCIO", "overview");
        AddGroup("OTIMIZAÇÃO", "system", "mouse", "keyboard");
        AddGroup("FERRAMENTAS", "display", "crosshair", "trainer");
        AddGroup("ATIVIDADE", "history");

        var tourItem = new NavigationViewItem
        {
            Content = "Apresentação do Flash",
            Tag = "tour",
            Icon = new FontIcon { Glyph = "\uE8F2" },
            CornerRadius = new CornerRadius(10)
        };
        NavView.FooterMenuItems.Add(tourItem);
        var settingsItem = new NavigationViewItem
        {
            Content = "Configurações",
            Tag = "settings",
            Icon = new FontIcon { Glyph = "\uE713" },
            CornerRadius = new CornerRadius(10)
        };
        NavView.FooterMenuItems.Add(settingsItem);
        _navigationItems["settings"] = settingsItem;
        NavView.SelectedItem = _overviewItem;
        ShowAuthentication();
        ((App)Application.Current).SessionEnded += (_, _) => ShowAuthentication();
    }

    private void AddGroup(string title, params string[] ids)
    {
        NavView.MenuItems.Add(new NavigationViewItemHeader { Content = title });
        foreach (var id in ids)
        {
            var section = SectionCatalog.Get(id);
            var item = new NavigationViewItem
            {
                Content = section.Title,
                Tag = section.Id,
                Icon = new FontIcon { Glyph = section.Glyph },
                CornerRadius = new CornerRadius(10)
            };
            NavView.MenuItems.Add(item);
            _navigationItems[id] = item;
            if (id == "overview") _overviewItem = item;
        }
    }

    private void ShowAuthentication()
    {
        TourOverlay.Visibility = Visibility.Collapsed;
        NavView.Visibility = Visibility.Collapsed;
        _currentUserId = Guid.Empty;
        var page = new AuthPage();
        page.Authenticated += profile =>
        {
            _currentUserId = profile.Id;
            AuthFrame.Visibility = Visibility.Collapsed;
            NavView.Visibility = Visibility.Visible;
            NavView.SelectedItem = _overviewItem;
            DispatcherQueue.TryEnqueue(() => StartTour(false));
        };
        AuthFrame.Content = page;
        AuthFrame.Visibility = Visibility.Visible;
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string id)
            return;

        if (id == "tour")
        {
            StartTour(true);
            return;
        }

        if (id == "settings")
            NavFrame.Navigate(typeof(SettingsPage));
        else if (id == "overview")
            NavFrame.Navigate(typeof(HomePage));
        else if (id is "mouse" or "keyboard")
            NavFrame.Navigate(typeof(InputPage), id);
        else if (id == "history")
            NavFrame.Navigate(typeof(HistoryPage));
        else if (id == "system")
            NavFrame.Navigate(typeof(SystemPage));
        else if (id == "crosshair")
            NavFrame.Navigate(typeof(CrosshairPage));
        else if (id == "trainer")
            NavFrame.Navigate(typeof(TrainerPage));
        else
            NavFrame.Navigate(typeof(FeaturePage), id);
    }

    private void StartTour(bool replay)
    {
        if (!replay && File.Exists(TourCompletedPath)) return;
        _tourStep = 0;
        TourOverlay.Visibility = Visibility.Visible;
        ShowTourStep();
    }

    private void ShowTourStep()
    {
        var step = TourContent.Steps[_tourStep];
        NavView.SelectedItem = _navigationItems[step.SectionId];
        TourStepText.Text = $"{_tourStep + 1:00} / {TourContent.Steps.Count:00}";
        TourTitleText.Text = step.Title;
        TourBodyText.Text = step.Message;
        TourBackButton.IsEnabled = _tourStep > 0;
        TourNextButton.Content = _tourStep == TourContent.Steps.Count - 1 ? "Concluir" : "Avançar";
        Motion.Reveal(TourCard);
    }

    private void TourBack_Click(object sender, RoutedEventArgs e)
    {
        if (_tourStep > 0) { _tourStep--; ShowTourStep(); }
    }

    private void TourNext_Click(object sender, RoutedEventArgs e)
    {
        if (_tourStep < TourContent.Steps.Count - 1) { _tourStep++; ShowTourStep(); }
        else FinishTour();
    }

    private void TourSkip_Click(object sender, RoutedEventArgs e) => FinishTour();

    private void FinishTour()
    {
        TourOverlay.Visibility = Visibility.Collapsed;
        NavView.SelectedItem = _overviewItem;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TourCompletedPath)!);
            File.WriteAllText(TourCompletedPath, "completed");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
