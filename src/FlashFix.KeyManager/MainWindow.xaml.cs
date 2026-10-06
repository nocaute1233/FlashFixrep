using FlashFix.Client;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FlashFix.KeyManager;

public partial class MainWindow : Window
{
    private FlashFixApiClient? _api;
    private FlashFixApiClient Api => _api ?? throw new InvalidOperationException("Entre na conta administrativa.");

    public MainWindow()
    {
        InitializeComponent();
        ServerUrl.Text = Environment.GetEnvironmentVariable("FLASHFIX_API_URL") ?? "";
        Loaded += (_, _) => Animate(LoginView);
    }

    private void Support_Click(object sender, RoutedEventArgs e) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://discord.gg/flashfix")
        { UseShellExecute = true });

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        LoginError.Text = "";
        LoginButton.IsEnabled = false;
        try
        {
            _api?.Dispose();
            _api = new FlashFixApiClient(ServerUrl.Text.Trim(),
                deviceProof: new WindowsDeviceProof("FlashFix.Admin.v1"));
            await Api.LoginAsync(LoginUsername.Text.Trim(), LoginPassword.Password);
            var profile = await Api.GetProfileAsync();
            if (!profile.IsAdmin)
            {
                await Api.LogoutAsync();
                LoginError.Text = "Esta conta não tem acesso administrativo.";
                return;
            }
            LoginPassword.Clear();
            LoginView.Visibility = Visibility.Collapsed;
            DashboardView.Visibility = Visibility.Visible;
            Animate(DashboardView);
            await LoadKeysAsync();
        }
        catch (Exception ex) { LoginError.Text = Friendly(ex); }
        finally { LoginButton.IsEnabled = true; }
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        try { await Api.LogoutAsync(); }
        catch (Exception)
        {
            LoginError.Text = "A sessão local foi encerrada. Não foi possível confirmar a saída no servidor.";
        }
        finally { _api?.Dispose(); _api = null; }
        DashboardView.Visibility = Visibility.Collapsed;
        LoginView.Visibility = Visibility.Visible;
        Animate(LoginView);
        StatusText.Text = "";
    }

    private void KeysTab_Click(object sender, RoutedEventArgs e)
    {
        KeysPanel.Visibility = Visibility.Visible;
        UsersPanel.Visibility = Visibility.Collapsed;
        PageTitle.Text = "Licenças";
        PageSubtitle.Text = "Emita, encontre e gerencie as licenças do serviço.";
        Animate(KeysPanel);
    }

    private async void UsersTab_Click(object sender, RoutedEventArgs e)
    {
        KeysPanel.Visibility = Visibility.Collapsed;
        UsersPanel.Visibility = Visibility.Visible;
        PageTitle.Text = "Usuários";
        PageSubtitle.Text = "Consulte e gerencie as contas cadastradas.";
        Animate(UsersPanel);
        await LoadUsersAsync();
    }

    private async void CreateKey_Click(object sender, RoutedEventArgs e)
    {
        CreateKeyButton.IsEnabled = false;
        try
        {
            var tag = (DurationPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            int? days = int.TryParse(tag, out var parsed) ? parsed : null;
            var key = await Api.CreateKeyAsync(days);
            ShowValue("Chave gerada", key.Key,
                "Guarde esta chave agora. O valor completo não será exibido novamente.");
            await LoadKeysAsync();
        }
        catch (Exception ex) { StatusText.Text = Friendly(ex); }
        finally { CreateKeyButton.IsEnabled = true; }
    }

    private async void RefreshKeys_Click(object sender, RoutedEventArgs e) => await LoadKeysAsync();
    private async void RefreshUsers_Click(object sender, RoutedEventArgs e) => await LoadUsersAsync();

    private async Task LoadKeysAsync()
    {
        try
        {
            var keys = await Api.SearchKeysAsync(KeySearch.Text.Trim(), KeyUserSearch.Text.Trim());
            KeysGrid.ItemsSource = keys.Select(x => new KeyRow(x)).ToList();
            StatusText.Text = $"{keys.Count} licença(s) encontrada(s).";
        }
        catch (Exception ex) { StatusText.Text = Friendly(ex); }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await Api.SearchUsersAsync(UserSearch.Text.Trim());
            UsersGrid.ItemsSource = users.Select(x => new UserRow(x)).ToList();
            StatusText.Text = $"{users.Count} usuário(s) encontrado(s).";
        }
        catch (Exception ex) { StatusText.Text = Friendly(ex); }
    }

    private void KeysGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SelectedKeyText.Text = KeysGrid.SelectedItem is KeyRow row
            ? $"{row.Value.Prefix}  ·  {row.Status}  ·  {(row.Value.DeviceBound ? "dispositivo vinculado" : "sem dispositivo")}"
            : "Selecione uma licença para gerenciar.";

    private async void History_Click(object sender, RoutedEventArgs e)
    {
        if (KeysGrid.SelectedItem is not KeyRow row) { StatusText.Text = "Selecione uma licença."; return; }
        try
        {
            var events = await Api.GetKeyHistoryAsync(row.Value.Id);
            ShowValue("Histórico da licença", string.Join(Environment.NewLine,
                events.Select(x => $"{x.OccurredAt.ToLocalTime():dd/MM/yyyy HH:mm}  {x.Action}  {x.Outcome}  {x.Detail}")),
                events.Count == 0 ? "Sem eventos." : $"{events.Count} evento(s) mais recentes.");
        }
        catch (Exception ex) { StatusText.Text = Friendly(ex); }
    }

    private async void BlockKey_Click(object sender, RoutedEventArgs e) => await KeyActionAsync("block", "bloquear");
    private async void UnblockKey_Click(object sender, RoutedEventArgs e) => await KeyActionAsync("unblock", "desbloquear");
    private async void ResetDevice_Click(object sender, RoutedEventArgs e) => await KeyActionAsync("reset-device", "redefinir dispositivo");
    private async void ReleaseKey_Click(object sender, RoutedEventArgs e) => await KeyActionAsync("release", "liberar");
    private async void RevokeKey_Click(object sender, RoutedEventArgs e) => await KeyActionAsync("revoke", "revogar");
    private async void BlockUser_Click(object sender, RoutedEventArgs e) => await UserActionAsync("block", "bloquear");
    private async void UnblockUser_Click(object sender, RoutedEventArgs e) => await UserActionAsync("unblock", "desbloquear");

    private async Task KeyActionAsync(string action, string label)
    {
        if (KeysGrid.SelectedItem is not KeyRow row) { StatusText.Text = "Selecione uma licença."; return; }
        var reason = AskReason($"Motivo da ação ({label}) na licença {row.Value.Prefix}");
        if (reason is null) return;
        try
        {
            await Api.ApplyKeyActionAsync(row.Value.Id, action, reason);
            await LoadKeysAsync();
            StatusText.Text = "Ação concluída e registrada no histórico.";
        }
        catch (Exception ex) { StatusText.Text = Friendly(ex); }
    }

    private async Task UserActionAsync(string action, string label)
    {
        if (UsersGrid.SelectedItem is not UserRow row) { StatusText.Text = "Selecione um usuário."; return; }
        var reason = AskReason($"Motivo da ação ({label}) na conta {row.Value.Username}");
        if (reason is null) return;
        try
        {
            await Api.ApplyUserActionAsync(row.Value.Id, action, reason);
            await LoadUsersAsync();
            StatusText.Text = "Ação concluída e registrada para a conta.";
        }
        catch (Exception ex) { StatusText.Text = Friendly(ex); }
    }

    private string? AskReason(string prompt)
    {
        var dialog = new Window { Title = "Confirmar ação", Owner = this, Width = 450, Height = 210,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.White, ResizeMode = ResizeMode.NoResize };
        var stack = new StackPanel { Margin = new Thickness(22) };
        stack.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        var input = new TextBox { MaxLength = 200, Height = 35 };
        stack.Children.Add(input);
        stack.Children.Add(new TextBlock { Text = "Informe um motivo de 3 a 200 caracteres.", Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 10) });
        var confirm = new Button { Content = "Confirmar", HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Black, Foreground = Brushes.White };
        confirm.Click += (_, _) =>
        {
            if (input.Text.Trim().Length is >= 3 and <= 200) dialog.DialogResult = true;
            else MessageBox.Show(dialog, "Informe um motivo de 3 a 200 caracteres.");
        };
        stack.Children.Add(confirm);
        dialog.Content = stack;
        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    private void ShowValue(string title, string value, string detail)
    {
        var dialog = new Window { Title = title, Owner = this, Width = 640, Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.White };
        var stack = new StackPanel { Margin = new Thickness(22) };
        stack.Children.Add(new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) });
        var box = new TextBox { Text = value, IsReadOnly = true, Height = 95,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(box);
        var copy = new Button { Content = "Copiar", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        copy.Click += (_, _) => { Clipboard.SetText(value); dialog.Close(); };
        stack.Children.Add(copy);
        dialog.Content = stack;
        dialog.ShowDialog();
    }

    protected override void OnClosed(EventArgs e) { _api?.Dispose(); base.OnClosed(e); }
    private static string Friendly(Exception ex) => ex switch
    {
        ApiException api => api.Message,
        ArgumentException => "Informe o endereço HTTPS da API. HTTP local é aceito apenas para desenvolvimento.",
        HttpRequestException or TaskCanceledException => "Não foi possível conectar ao servidor. Confira sua conexão e tente novamente.",
        _ => "Não foi possível concluir a operação. Tente novamente."
    };

    private static void Animate(UIElement element)
    {
        var preferencePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlashFix", "motion.txt");
        try
        {
            if (File.Exists(preferencePath) && File.ReadAllText(preferencePath).Trim() == "off")
            {
                element.Opacity = 1;
                element.RenderTransform = null;
                return;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        element.Opacity = 0;
        var translate = new TranslateTransform(0, 14);
        element.RenderTransform = translate;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1,
            TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0,
            TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private sealed record KeyRow(KeySummary Value)
    {
        public string Prefix => Value.Prefix;
        public string Status => Value.Status switch
        {
            "active" => "Ativa",
            "available" => "Disponível",
            "expired" => "Expirada",
            "blocked" => "Bloqueada",
            "revoked" => "Revogada",
            _ => Value.Status
        };
        public string? Username => Value.Username;
        public string DurationLabel => Value.DurationDays is int days ? $"{days} dia(s)" : "Vitalícia";
        public string CreatedLabel => Value.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
        public string ExpiresLabel => Value.ExpiresAt?.ToLocalTime().ToString("dd/MM/yyyy") ?? "—";
    }
    private sealed record UserRow(UserSummary Value)
    {
        public string Username => Value.Username;
        public string Status => Value.IsBlocked ? "Bloqueado" : "Ativo";
        public string CreatedLabel => Value.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
        public string LastLoginLabel => Value.LastLoginAt?.ToLocalTime().ToString("dd/MM/yyyy") ?? "—";
    }
}
