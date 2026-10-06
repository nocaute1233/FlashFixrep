using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace FlashFix_Desktop.Design;

public static class Motion
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlashFix", "motion.txt");

    public static bool Enabled { get; private set; } = ReadPreference();

    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            File.WriteAllText(PreferencePath, enabled ? "on" : "off");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public static void Reveal(FrameworkElement element, int delayMilliseconds = 0)
    {
        if (!Enabled)
        {
            element.Opacity = 1;
            element.RenderTransform = null;
            return;
        }

        element.Opacity = 0;
        var transform = new TranslateTransform { Y = 14 };
        element.RenderTransform = transform;
        var storyboard = new Storyboard();
        var duration = new Duration(TimeSpan.FromMilliseconds(380));
        var delay = TimeSpan.FromMilliseconds(delayMilliseconds);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fade = new DoubleAnimation
        {
            From = 0, To = 1, Duration = duration, BeginTime = delay,
            EasingFunction = ease
        };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);

        var slide = new DoubleAnimation
        {
            From = 14, To = 0, Duration = duration, BeginTime = delay,
            EasingFunction = ease, EnableDependentAnimation = true
        };
        Storyboard.SetTarget(slide, transform);
        Storyboard.SetTargetProperty(slide, "Y");
        storyboard.Children.Add(slide);
        storyboard.Begin();
    }

    private static bool ReadPreference()
    {
        try { return !File.Exists(PreferencePath) || File.ReadAllText(PreferencePath).Trim() != "off"; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }
}
