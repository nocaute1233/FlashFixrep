using FlashFix.Client;
using FlashFix_Desktop.Design;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Security.Cryptography;

namespace FlashFix_Desktop.Pages;

public sealed partial class AuthPage : Page
{
    private bool _register;
    public event Action<Profile>? Authenticated;

    public AuthPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Motion.Reveal(AuthRoot);
    }

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        _register = !_register;
        KeyField.Visibility = _register ? Visibility.Visible : Visibility.Collapsed;
        FormTitle.Text = _register ? "Crie sua conta" : "Acesse sua conta";
        FormSubtitle.Text = _register
            ? "Escolha suas credenciais e informe a chave recebida na compra."
            : "Use as credenciais da conta vinculada à sua licença.";
        SubmitButton.Content = _register ? "Criar conta" : "Entrar";
        ModeButton.Content = _register ? "Já tem conta? Entrar" : "Criar conta com uma chave";
        ErrorText.Visibility = Visibility.Collapsed;
        Motion.Reveal(FormPanel);
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var api = ((App)Application.Current).ApiClient;
        SubmitButton.IsEnabled = false;
        LoadingRing.IsActive = true;
        LoadingRing.Visibility = Visibility.Visible;
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            if (_register)
                await api.RegisterAsync(UsernameBox.Text.Trim(), PasswordInput.Password, KeyBox.Text.Trim());
            else
                await api.LoginAsync(UsernameBox.Text.Trim(), PasswordInput.Password);

            var profile = await api.GetProfileAsync();
            PasswordInput.Password = "";
            KeyBox.Text = "";
            Authenticated?.Invoke(profile);
        }
        catch (ApiException error)
        {
            ShowError(error.Message);
        }
        catch (HttpRequestException)
        {
            ShowError("Não foi possível acessar o servidor. Verifique sua conexão e tente novamente.");
        }
        catch (TaskCanceledException)
        {
            ShowError("A conexão demorou demais. Tente novamente.");
        }
        catch (CryptographicException)
        {
            ShowError("Não foi possível confirmar este dispositivo. Verifique a segurança do Windows e tente novamente.");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            SubmitButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private async void Support_Click(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://discord.gg/flashfix"));
}
