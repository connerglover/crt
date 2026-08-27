using System.Diagnostics;
using CRT.Services;
using CRT.Views;
using Microsoft.UI.Composition.SystemBackdrops;
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
    private bool _backdropActive;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Conner's Retime Tool";
        // The nav surfaces are transparent so the backdrop can show through, so
        // a window with no backdrop renders pure black. Mica needs Windows 11
        // and the system "transparency effects" setting, so where it is
        // unavailable ApplyTheme paints an opaque base instead.
        _backdropActive = MicaController.IsSupported();
        if (_backdropActive)
        {
            SystemBackdrop = new MicaBackdrop();
        }

        // The title bar has to be part of the content to be themed at all: the
        // system-drawn one follows the OS theme, not the app's, so it stayed
        // light no matter what the in-app theme was set to. Extending also lets
        // the Mica backdrop reach the caption area.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        TitleBarText.Text = Title;

        // Localized nav labels.
        NavDashboard.Content = AppServices.Loc["Dashboard"];
        NavRetimer.Content = AppServices.Loc["Frame Retimer"];
        NavVideo.Content = AppServices.Loc["Video Retimer"];

        ApplyTheme(AppServices.Settings.Theme);
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

    // Logical (DPI-independent) startup size. The Python window is a fixed
    // 780×494 with the loads panel open; this shell adds a navigation rail and
    // a menu bar, so it needs a little more, but the previous 1180×760 was
    // roughly twice the area of the original for no reason.
    private const int DefaultLogicalWidth = 940;
    private const int DefaultLogicalHeight = 600;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void ConfigureAppWindow()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
            TitleBarIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
        }

        // AppWindow.Resize takes physical pixels, so a fixed number shrinks in
        // apparent size as display scaling rises. Qt sized in logical units, so
        // scale to match what the original actually looked like.
        double scale = Scale();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)(DefaultLogicalWidth * scale),
            (int)(DefaultLogicalHeight * scale)));
    }

    private double Scale()
    {
        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        uint dpi = GetDpiForWindow(handle);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    // ── Theme ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a stored theme name to the content and the caption buttons.
    /// Public so the settings page can re-apply it without a restart.
    /// </summary>
    public void ApplyTheme(string themeName)
    {
        ElementTheme theme = ThemeService.ResolveTheme(themeName);
        RootGrid.RequestedTheme = theme;

        // Caption buttons are drawn by the system and are not part of the XAML
        // tree, so they do not inherit RootGrid's theme and have to be colored
        // by hand. Transparent backgrounds keep Mica visible behind them.
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

        bool dark = theme switch
        {
            ElementTheme.Dark => true,
            ElementTheme.Light => false,
            _ => Application.Current.RequestedTheme == ApplicationTheme.Dark,
        };
        // Without a backdrop the transparent nav surfaces have nothing behind
        // them, so supply the base color the backdrop would otherwise provide.
        RootGrid.Background = _backdropActive
            ? null
            : new SolidColorBrush(dark
                ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
                : Windows.UI.Color.FromArgb(255, 243, 243, 243));

        Windows.UI.Color foreground = dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = dark ? Microsoft.UI.Colors.Gray : Microsoft.UI.Colors.DimGray;
        titleBar.ButtonHoverBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(24, 255, 255, 255)
            : Windows.UI.Color.FromArgb(16, 0, 0, 0);
        titleBar.ButtonPressedBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(40, 255, 255, 255)
            : Windows.UI.Color.FromArgb(28, 0, 0, 0);
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
