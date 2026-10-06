using FlashFix.Optimization;
using FlashFix_Desktop.Design;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FlashFix_Desktop.Pages;

public sealed partial class HistoryPage : Page
{
    private readonly OptimizationEngine _engine = OptimizationEngine.CreateDefault();
    private bool _busy;

    public HistoryPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            Motion.Reveal(HeaderSection);
            Motion.Reveal(RestoreSection, 70);
            await LoadAsync();
        };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        if (_busy) return;
        _busy = true;
        RestoreAllButton.IsEnabled = false;
        StatusText.Text = "Carregando histórico…";
        try
        {
            var statuses = await Task.WhenAll(TweakCatalog.All.Select(x => _engine.GetStatusAsync(x.Id)));
            var active = statuses.Count(x => x.CanRestore);
            ActiveCountText.Text = active == 1
                ? "1 ajuste pode ser restaurado."
                : $"{active} ajustes podem ser restaurados.";
            RestoreAllButton.IsEnabled = active > 0;
            HistoryList.Children.Clear();
            var entries = await _engine.GetHistoryAsync();
            if (entries.Count == 0)
                HistoryList.Children.Add(new TextBlock
                {
                    Text = "Nenhuma alteração registrada até agora.",
                    Foreground = Brush("#A7A7AA"), FontSize = 14,
                    Margin = new Thickness(0, 12, 0, 0)
                });
            else
                foreach (var entry in entries)
                    HistoryList.Children.Add(MakeEntry(entry));
            StatusText.Text = "Histórico atualizado.";
            Motion.Reveal(HistoryList, 100);
        }
        catch (Exception error) { StatusText.Text = $"Não foi possível carregar o histórico: {error.Message}"; }
        finally { _busy = false; }
    }

    private async void RestoreAll_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        TweakStatus[] restorable;
        try
        {
            var statuses = await Task.WhenAll(TweakCatalog.All.Select(x => _engine.GetStatusAsync(x.Id)));
            restorable = statuses.Where(x => x.CanRestore).ToArray();
        }
        catch (Exception error)
        {
            StatusText.Text = $"Não foi possível verificar os backups: {error.Message}";
            return;
        }
        if (restorable.Length == 0) { await LoadAsync(); return; }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Restaurar configurações originais",
            Content = $"O FlashFix vai restaurar {restorable.Length} ajuste(s) usando os valores salvos antes da aplicação.",
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        _busy = true;
        RestoreAllButton.IsEnabled = false;
        OperationProgress.Maximum = restorable.Length;
        OperationProgress.Value = 0;
        OperationProgress.Visibility = Visibility.Visible;
        var restored = 0;
        var failed = 0;
        foreach (var status in restorable)
        {
            StatusText.Text = $"Restaurando {status.Definition.Name}…";
            try
            {
                var result = await _engine.RestoreAsync(status.Definition.Id);
                if (result.Success) restored++;
                else failed++;
            }
            catch { failed++; }
            OperationProgress.Value++;
        }
        _busy = false;
        await LoadAsync();
        OperationProgress.Visibility = Visibility.Collapsed;
        StatusText.Text = failed == 0
            ? $"{restored} ajuste(s) restaurado(s) com sucesso."
            : $"{restored} ajuste(s) restaurado(s); {failed} falha(s). Consulte o histórico ou tente novamente.";
    }

    private static Border MakeEntry(HistoryEntry entry)
    {
        var name = TweakCatalog.All.FirstOrDefault(x => x.Id == entry.TweakId)?.Name ?? entry.TweakId;
        var action = entry.Action == "apply" ? "Aplicação" : "Restauração";
        var outcome = entry.Outcome == "success" ? "Concluída" : "Falhou";
        var body = new StackPanel { Spacing = 7 };
        body.Children.Add(new TextBlock
        {
            Text = $"{action.ToUpperInvariant()}  ·  {entry.OccurredAt.ToLocalTime():dd/MM/yyyy HH:mm}",
            Foreground = Brush("#9C9CA0"), FontSize = 11, CharacterSpacing = 60
        });
        body.Children.Add(new TextBlock
        {
            Text = $"{name}  ·  {outcome}", Foreground = Brush("#F4F4F2"),
            FontSize = 17, FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(new TextBlock
        {
            Text = entry.Detail, Foreground = Brush("#A7A7AA"), FontSize = 12,
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
