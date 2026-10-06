// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using FlashFix.Client;
using FlashFix_Desktop.Design;

namespace FlashFix_Desktop.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        MotionToggle.IsOn = Motion.Enabled;
        Loaded += (_, _) =>
        {
            Motion.Reveal(HeaderSection);
            Motion.Reveal(ContentSection, 80);
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            var profile = await ((App)Application.Current).ApiClient.GetProfileAsync();
            UsernameText.Text = profile.Username;
            LicenseText.Text = profile.License is null
                ? "Conta administrativa"
                : profile.License.ExpiresAt is null
                    ? "Licença vitalícia · Ativa"
                    : $"Licença ativa até {profile.License.ExpiresAt.Value.ToLocalTime():dd/MM/yyyy}";
        }
        catch (ApiException)
        {
            UsernameText.Text = "Conta indisponível";
            LicenseText.Text = "Entre novamente para continuar.";
        }
    }

    private void MotionToggle_Toggled(object sender, RoutedEventArgs e) =>
        Motion.SetEnabled(MotionToggle.IsOn);

    private async void Support_Click(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://discord.gg/flashfix"));

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        try { await ((App)Application.Current).LogoutAsync(); }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceWarning("Logout request failed: {0}", error.GetType().Name);
        }
    }
}
