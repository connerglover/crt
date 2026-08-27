using System.Diagnostics;
using CRT.Services;
using CRT.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CRT;

/// <summary>
/// The shell window: NavigationView (Dashboard / Frame Retimer / Video
/// Retimer / Settings), Mica backdrop, always-on-top (on by default), update
/// banner, copy toasts, and exit-time save prompting.
/// </summary>
public sealed partial class MainWindow : Window
{
    private DispatcherQueueTimer? _toastTimer;
    private bool _closeConfirmed;
    private bool _alwaysOnTop;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Conner's Retime Tool";
        SystemBackdrop = new MicaBackdrop();

        // Localized nav labels.
        NavDashboard.Content = AppServices.Loc["Dashboard"];
        NavRetimer.Content = AppServices.Loc["Frame Retimer"];
        NavVideo.Content = AppServices.Loc["Video Retimer"];

        RootGrid.RequestedTheme = ThemeService.ResolveTheme(AppServices.Settings.Theme);
        RootGrid.Loaded += (_, _) => AppServices.Dialogs.Root = RootGrid.XamlRoot;

        ConfigureAppWindow();

        // Always on top is ON by default (spec §6).
        SetAlwaysOnTop(true);

        AppWindow.Closing += OnAppWindowClosing;

        // Dashboard is the startup page (spec §11).
        Nav.SelectedItem = NavDashboard;
        NavigateTo("dashboard");
    }

    public bool AlwaysOnTop => _alwaysOnTop;

    private void ConfigureAppWindow()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 760));
    }

    // ── Navigation ─────────────────────────────────────────────────────────

    public void NavigateTo(string tag)
    {
        Type pageType = tag switch
        {
            "retimer" => typeof(RetimerPage),
            "video" => typeof(VideoRetimerPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(DashboardPage),
        };
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }

        // Keep the nav selection in sync when navigation is triggered from code.
        object? target = tag switch
        {
            "retimer" => NavRetimer,
            "video" => NavVideo,
            "settings" => Nav.SettingsItem,
            _ => NavDashboard,
        };
        if (!ReferenceEquals(Nav.SelectedItem, target))
        {
            Nav.SelectedItem = target;
        }
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            if (ContentFrame.CurrentSourcePageType != typeof(SettingsPage))
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            return;
        }
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            Type pageType = tag switch
            {
                "retimer" => typeof(RetimerPage),
                "video" => typeof(VideoRetimerPage),
                _ => typeof(DashboardPage),
            };
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }

    // ── Always on top ──────────────────────────────────────────────────────

    public void SetAlwaysOnTop(bool enabled)
    {
        _alwaysOnTop = enabled;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = enabled;
        }
    }

    // ── Update banner ──────────────────────────────────────────────────────

    public void ShowUpdateBanner(string version)
    {
        UpdateBanner.Message = AppServices.Loc.Format("Update Available", ("version", version));
        UpdateBanner.ActionButton = new Button
        {
            Content = AppServices.Loc["Download"],
        };
        ((Button)UpdateBanner.ActionButton).Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(Core.AppVersion.ReleasesUrl) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // Browser launch failure is not actionable.
            }
        };
        UpdateBanner.IsOpen = true;
    }

    // ── Toasts / busy ──────────────────────────────────────────────────────

    public void ShowToast(string message)
    {
        ToastBar.Message = message;
        ToastBar.IsOpen = true;

        _toastTimer ??= DispatcherQueue.CreateTimer();
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(2.5);
        _toastTimer.IsRepeating = false;
        _toastTimer.Tick += ToastTimerTick;
        _toastTimer.Start();
    }

    private void ToastTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= ToastTimerTick;
        ToastBar.IsOpen = false;
    }

    public void SetBusy(bool busy) =>
        BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;

    public void SetPendingRunsBadge(int count)
    {
        NavDashboard.InfoBadge = count > 0 ? new InfoBadge { Value = count } : null;
    }

    // ── Startup tasks (after activation) ───────────────────────────────────

    public async Task RunStartupTasksAsync()
    {
        // Wait until the XamlRoot exists for dialogs.
        while (AppServices.Dialogs.Root is null)
        {
            await Task.Delay(50);
        }

        // Offer crash restore before anything else touches the session.
        var snapshot = AppServices.Autosave.TryRead();
        if (snapshot is not null)
        {
            bool restore = await AppServices.Dialogs.ConfirmAsync(
                AppServices.Loc["Restore Session"],
                AppServices.Loc["Restore Session Message"],
                AppServices.Loc["Restore Session"],
                AppServices.Loc["Cancel"]);
            if (restore)
            {
                AppServices.Session.RestoreFromSnapshot(snapshot);
                NavigateTo("retimer");
            }
            else
            {
                AppServices.Autosave.Clear();
            }
        }

        // Update check on launch, unless disabled (spec §6).
        if (AppServices.Settings.EnableUpdates)
        {
            string? latest = await AppServices.UpdateChecker.CheckForUpdatesAsync();
            if (latest is not null)
            {
                ShowUpdateBanner(latest);
            }
        }
    }

    // ── Exit flow ──────────────────────────────────────────────────────────

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed)
        {
            return;
        }

        if (AppServices.Session.Dirty)
        {
            args.Cancel = true;
            _ = ConfirmExitAsync();
        }
        else
        {
            AppServices.Autosave.Clear(); // clean exit
        }
    }

    private async Task ConfirmExitAsync()
    {
        bool save = await AppServices.Dialogs.ConfirmAsync(
            AppServices.Loc["Exit"],
            AppServices.Loc["Would you like to save?"],
            AppServices.Loc["Save"],
            AppServices.Loc["Don't Save"]);
        if (save)
        {
            await AppServices.Session.SaveAsync();
            if (AppServices.Session.Dirty)
            {
                return; // save was cancelled — keep the app open
            }
        }
        AppServices.Autosave.Clear(); // clean exit
        _closeConfirmed = true;
        Close();
    }
}
