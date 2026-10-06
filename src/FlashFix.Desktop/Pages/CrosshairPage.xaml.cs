using FlashFix.Core.Crosshair;
using FlashFix_Desktop.Design;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace FlashFix_Desktop.Pages;

public sealed partial class CrosshairPage : Page
{
    private readonly CrosshairStore _store = CrosshairStore.CreateDefault();
    private IReadOnlyList<CrosshairProfile> _profiles = [];
    private CrosshairProfile _current = CrosshairProfile.Default();
    private bool _ready;
    private bool _loadingControls;

    public CrosshairPage()
    {
        InitializeComponent();
        _ready = true;
        Loaded += (_, _) =>
        {
            Motion.Reveal(HeaderSection);
            Motion.Reveal(EditorSection, 70);
            LoadProfiles();
            Render();
            if (string.IsNullOrEmpty(FeedbackText.Text))
                FeedbackText.Text = "Ajuste a prévia e salve um perfil para encontrá-lo aqui depois.";
        };
    }

    private void PreviewTextChanged(object sender, TextChangedEventArgs e) => Render();
    private void PreviewSliderChanged(object sender, RangeBaseValueChangedEventArgs e) => Render();
    private void PreviewToggleChanged(object sender, RoutedEventArgs e) => Render();

    private CrosshairProfile FromControls() => _current with
    {
        Name = NameBox.Text.Trim(),
        Color = ColorBox.Text.Trim().ToUpperInvariant(),
        ArmLength = (int)Math.Round(LengthSlider.Value),
        Thickness = (int)Math.Round(ThicknessSlider.Value),
        Gap = (int)Math.Round(GapSlider.Value),
        Opacity = OpacitySlider.Value / 100,
        CenterDot = DotToggle.IsOn,
        Outline = OutlineToggle.IsOn
    };

    private void SetControls(CrosshairProfile profile)
    {
        _loadingControls = true;
        _current = profile;
        NameBox.Text = profile.Name;
        ColorBox.Text = profile.Color;
        LengthSlider.Value = profile.ArmLength;
        ThicknessSlider.Value = profile.Thickness;
        GapSlider.Value = profile.Gap;
        OpacitySlider.Value = profile.Opacity * 100;
        DotToggle.IsOn = profile.CenterDot;
        OutlineToggle.IsOn = profile.Outline;
        _loadingControls = false;
        Render();
    }

    private void Render()
    {
        if (!_ready || _loadingControls) return;
        LengthValue.Text = $"{LengthSlider.Value:0} px";
        ThicknessValue.Text = $"{ThicknessSlider.Value:0} px";
        GapValue.Text = $"{GapSlider.Value:0} px";
        OpacityValue.Text = $"{OpacitySlider.Value:0}%";
        PreviewCanvas.Children.Clear();
        try
        {
            var profile = FromControls();
            (profile with { Name = "Prévia" }).Validate();
            Draw(profile);
            var recoveredFromError = !SaveButton.IsEnabled;
            SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(profile.Name);
            if (recoveredFromError)
                FeedbackText.Text = "Cor válida. A prévia está pronta para salvar.";
        }
        catch (ArgumentException error)
        {
            SaveButton.IsEnabled = false;
            FeedbackText.Text = error.Message;
        }
    }

    private void Draw(CrosshairProfile profile)
    {
        const double center = 130;
        var color = Windows.UI.Color.FromArgb(255,
            Convert.ToByte(profile.Color.Substring(1, 2), 16),
            Convert.ToByte(profile.Color.Substring(3, 2), 16),
            Convert.ToByte(profile.Color.Substring(5, 2), 16));
        var brush = new SolidColorBrush(color);
        var arm = profile.ArmLength;
        var gap = profile.Gap;
        var thickness = profile.Thickness;
        AddArm(center - gap - arm, center - thickness / 2d, arm, thickness, brush, profile);
        AddArm(center + gap, center - thickness / 2d, arm, thickness, brush, profile);
        AddArm(center - thickness / 2d, center - gap - arm, thickness, arm, brush, profile);
        AddArm(center - thickness / 2d, center + gap, thickness, arm, brush, profile);
        if (!profile.CenterDot) return;
        var size = Math.Max(2, thickness);
        if (profile.Outline)
            AddEllipse(center - size / 2d - 2, center - size / 2d - 2,
                size + 4, new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)), profile.Opacity);
        AddEllipse(center - size / 2d, center - size / 2d, size, brush, profile.Opacity);
    }

    private void AddArm(double x, double y, double width, double height,
        SolidColorBrush brush, CrosshairProfile profile)
    {
        if (profile.Outline)
            AddRectangle(x - 2, y - 2, width + 4, height + 4,
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)), profile.Opacity);
        AddRectangle(x, y, width, height, brush, profile.Opacity);
    }

    private void AddRectangle(double x, double y, double width, double height,
        SolidColorBrush brush, double opacity)
    {
        var shape = new Rectangle { Width = width, Height = height, Fill = brush, Opacity = opacity };
        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
        PreviewCanvas.Children.Add(shape);
    }

    private void AddEllipse(double x, double y, double size,
        SolidColorBrush brush, double opacity)
    {
        var shape = new Ellipse { Width = size, Height = size, Fill = brush, Opacity = opacity };
        Canvas.SetLeft(shape, x);
        Canvas.SetTop(shape, y);
        PreviewCanvas.Children.Add(shape);
    }

    private void LoadProfiles(Guid? selectId = null)
    {
        try
        {
            _profiles = _store.Load();
            ProfileList.Items.Clear();
            foreach (var profile in _profiles)
            {
                var item = new ListViewItem { Content = profile.Name, Tag = profile.Id };
                ProfileList.Items.Add(item);
                if (profile.Id == selectId) ProfileList.SelectedItem = item;
            }
            DeleteButton.IsEnabled = ProfileList.SelectedItem is not null;
        }
        catch (Exception error) { FeedbackText.Text = $"Não foi possível abrir os perfis: {error.Message}"; }
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || ProfileList.SelectedItem is not ListViewItem { Tag: Guid id })
        {
            if (_ready) DeleteButton.IsEnabled = false;
            return;
        }
        var profile = _profiles.FirstOrDefault(x => x.Id == id);
        if (profile is null) return;
        SetControls(profile);
        DeleteButton.IsEnabled = true;
        FeedbackText.Text = $"Perfil “{profile.Name}” carregado.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var profile = FromControls();
            _store.Save(profile);
            _current = profile;
            LoadProfiles(profile.Id);
            FeedbackText.Text = $"Perfil “{profile.Name}” salvo.";
        }
        catch (Exception error) { FeedbackText.Text = $"Não foi possível salvar: {error.Message}"; }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        ProfileList.SelectedItem = null;
        SetControls(CrosshairProfile.Default());
        FeedbackText.Text = "Novo perfil. Ajuste a prévia e escolha Salvar perfil.";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ListViewItem { Tag: Guid id }) return;
        try
        {
            _store.Delete(id);
            SetControls(CrosshairProfile.Default());
            LoadProfiles();
            FeedbackText.Text = "Perfil excluído.";
        }
        catch (Exception error) { FeedbackText.Text = $"Não foi possível excluir: {error.Message}"; }
    }
}
