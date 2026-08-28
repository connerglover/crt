using CRT.Core.Files;
using CRT.Core.Localization;
using CRT.Core.Net;
using CRT.Core.Settings;
using CRT.Core.Tools;
using CRT.ViewModels;

namespace CRT.Services;

/// <summary>
/// Composition root: one instance of every service, created at startup and
/// shared by all pages/view models.
/// </summary>
public static class AppServices
{
    public static ConfigPaths Paths { get; private set; } = null!;
    public static SettingsService SettingsService { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;
    public static Localizer Loc { get; private set; } = null!;
    public static RecentFiles RecentFiles { get; private set; } = null!;
    public static RunLibrary Library { get; private set; } = null!;
    public static AutosaveService Autosave { get; private set; } = null!;
    public static ApiKeyStore ApiKeyStore { get; private set; } = null!;
    public static SpeedrunClient Speedrun { get; private set; } = null!;
    public static UpdateChecker UpdateChecker { get; private set; } = null!;
    public static InnertubeClient Innertube { get; private set; } = null!;
    public static ToolLocator Tools { get; private set; } = null!;
    public static DialogService Dialogs { get; private set; } = null!;

    public static SessionViewModel Session { get; private set; } = null!;
    public static VideoRetimerViewModel VideoRetimer { get; private set; } = null!;
    public static DashboardViewModel Dashboard { get; private set; } = null!;

    /// <summary>The main window, set once it exists (used for pickers/presenter).</summary>
    public static MainWindow? MainWindow { get; set; }

    public static void Initialize()
    {
        Paths = new ConfigPaths();
        Paths.EnsureBaseDirectory();

        SettingsService = new SettingsService(Paths.SettingsFile);
        Settings = SettingsService.Current();
        Loc = new Localizer(Settings.Language);

        RecentFiles = new RecentFiles(Paths.RecentFile);
        Library = new RunLibrary(Paths.LibraryFile);
        Autosave = new AutosaveService(Paths.AutosaveFile);
        ApiKeyStore = new ApiKeyStore(Paths.ApiKeyFile);
        Speedrun = new SpeedrunClient();
        UpdateChecker = new UpdateChecker();
        Tools = new ToolLocator(
            Paths.ToolsDirectory,
            () => Settings.FfmpegPath,
            () => Settings.YtDlpPath);
        Innertube = new InnertubeClient(ytDlpFallback: async (videoId, formatId, ct) =>
        {
            string? ytDlp = Tools.Find(ToolKind.YtDlp);
            if (ytDlp is null)
            {
                return null;
            }
            var importer = new YtDlpImporter(ytDlp, Paths.VideoCacheDirectory);
            return await importer.GetFpsByVideoIdAsync(videoId, formatId, ct).ConfigureAwait(false);
        });
        Dialogs = new DialogService();

        Session = new SessionViewModel();
        VideoRetimer = new VideoRetimerViewModel();
        Dashboard = new DashboardViewModel();
    }

    /// <summary>
    /// Raised after settings are re-read, so live pages can re-apply anything
    /// they captured at construction — their localized strings and their
    /// keyboard accelerators.
    /// </summary>
    /// <remarks>
    /// Most settings need no such handling: they are read from
    /// <see cref="Settings"/> at the point of use, and swapping the instance
    /// below is enough. Only what a page copies once has to be told.
    /// </remarks>
    public static event EventHandler? SettingsChanged;

    /// <summary>Re-reads settings from disk after the settings page applies changes.</summary>
    public static void ReloadSettings()
    {
        string previousLanguage = Settings.Language;
        Settings = SettingsService.Current();

        // The localizer is bound to one language, so a language change means a
        // new one; every page then re-reads its strings through it.
        if (!string.Equals(previousLanguage, Settings.Language, StringComparison.Ordinal))
        {
            Loc = new Localizer(Settings.Language);
        }

        SettingsChanged?.Invoke(null, EventArgs.Empty);
    }
}
