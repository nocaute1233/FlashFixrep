using FlashFix.Core.Navigation;
using FlashFix_Desktop.Design;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FlashFix_Desktop.Pages;

public sealed partial class FeaturePage : Page
{
    public FeaturePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Motion.Reveal(HeaderSection);
            Motion.Reveal(ContentSection, 75);
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is not string id)
            return;

        var section = SectionCatalog.Get(id);
        PhaseLabel.Text = $"EM DESENVOLVIMENTO / {section.Phase.ToUpperInvariant()}";
        PageTitle.Text = section.Title;
        PageSubtitle.Text = section.Subtitle;
        PageDescription.Text = section.Description;
        FeatureIcon.Glyph = section.Glyph;
    }
}
