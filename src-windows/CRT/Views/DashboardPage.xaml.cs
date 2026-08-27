using System.Diagnostics;
using CRT.Services;
using CRT.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CRT.Views;

/// <summary>
/// The startup page: run library, quick actions, and the Speedrun.com
/// moderation panel.
/// </summary>
public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        ApplyLocalization();

        Loaded += (_, _) => _ = VM.ActivateAsync();
        Unloaded += (_, _) => VM.Deactivate();
    }

    public DashboardViewModel VM => AppServices.Dashboard;

    public Visibility Invert(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public string SignedInText(string user) =>
        AppServices.Loc.Format("Signed in as {user}", ("user", user));

    public ImageSource? AvatarSource(string? uri)
    {
        if (string.IsNullOrEmpty(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
        {
            return null;
        }
        return new BitmapImage(parsed);
    }

    private void ApplyLocalization()
    {
        var loc = AppServices.Loc;
        NewRetimeButton.Content = loc["New Retime"];
        OpenFileButton.Content = loc["Open File"];
        ImportVideoButton.Content = loc["Import Video"];
        LibraryHeader.Text = loc["Run Library"];
        LibraryEmptyText.Text = loc["Empty Library"];
        SrcHeader.Text = loc["Speedrun.com"];
        RefreshButton.Content = loc["Refresh"];
        SignInExplainer.Text = loc["Sign in explainer"];
        ApiKeyBox.PlaceholderText = loc["API Key"];
        SignInButton.Content = loc["Sign In"];
        GetKeyLink.Content = loc["Get your key"];
        SignOutButton.Content = loc["Sign Out"];
        NoPendingText.Text = loc["No runs to verify"];
        RecentRunsHeader.Text = loc["My Recent Runs"];
    }

    private void OnApiKeyChanged(object sender, RoutedEventArgs e) =>
        VM.ApiKeyInput = ApiKeyBox.Password;

    private void OnGetKeyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Core.Net.SpeedrunClient.ApiKeySettingsUrl)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            // Browser launch failure is not actionable.
        }
    }
}
