using CRT.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace CRT;

public partial class App : Application
{
    private MainWindow? _window;
    private DispatcherQueueTimer? _autosaveTimer;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppServices.Initialize();

        // Accent must be applied before the first window exists (spec §12).
        ThemeService.ApplyAccentColor(AppServices.Settings.AccentColor);

        AppServices.Session.ApplyDefaultMode(AppServices.Settings.DefaultMode);

        _window = new MainWindow();
        AppServices.MainWindow = _window;
        _window.Activate();

        StartAutosaveTimer();
        _ = _window.RunStartupTasksAsync();
    }

    private void StartAutosaveTimer()
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        _autosaveTimer = queue.CreateTimer();
        _autosaveTimer.Interval = TimeSpan.FromSeconds(Core.Files.AutosaveService.IntervalSeconds);
        _autosaveTimer.Tick += (_, _) => AppServices.Session.WriteAutosaveIfDirty();
        _autosaveTimer.Start();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Surface instead of crashing; the autosave file stays for restore if
        // the process still dies.
        e.Handled = true;
        _ = AppServices.Dialogs.ShowErrorAsync(e.Message);
    }
}
