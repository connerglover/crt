using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRT.Core.Localization;
using CRT.Core.Settings;
using CRT.Services;

namespace CRT.ViewModels;

/// <summary>
/// The settings page: an editable copy of the settings with Apply / Cancel /
/// Restore Defaults semantics ported from the Python app. Theme names are
/// shown localized but always stored in English.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private AppSettings _edited;

    public SettingsViewModel()
    {
        _edited = AppServices.Settings.Clone();
        LoadFrom(_edited);
    }

    public IReadOnlyList<string> ThemeOptions { get; } = new[]
    {
        AppServices.Loc["Automatic"], AppServices.Loc["Dark"], AppServices.Loc["Light"],
    };

    public IReadOnlyList<string> LanguageOptions => LanguageCatalog.LanguageNames;

    public IReadOnlyList<string> TimerCornerOptions { get; } = new[]
    {
        AppServices.Loc["Top Left"], AppServices.Loc["Top Right"],
        AppServices.Loc["Bottom Left"], AppServices.Loc["Bottom Right"],
    };

    public IReadOnlyList<string> TimerStyleOptions { get; } = new[]
    {
        AppServices.Loc["Pill"], AppServices.Loc["Plain"],
    };

    public IReadOnlyList<string> DefaultModeOptions { get; } = new[]
    {
        AppServices.Loc["Load Mode"], AppServices.Loc["Segment Mode"],
    };

    [ObservableProperty]
    private bool _enableUpdates;

    [ObservableProperty]
    private int _themeIndex;

    [ObservableProperty]
    private string _accentColor = SettingsService.DefaultAccentColor;

    [ObservableProperty]
    private int _languageIndex;

    [ObservableProperty]
    private string _modNoteFormat = "";

    [ObservableProperty]
    private int _timerCornerIndex = 3;

    [ObservableProperty]
    private int _timerStyleIndex;

    [ObservableProperty]
    private string _ffmpegPath = "";

    [ObservableProperty]
    private string _ytDlpPath = "";

    [ObservableProperty]
    private int _defaultModeIndex;

    /// <summary>Working copy of the hotkeys, edited by the hotkey dialog.</summary>
    public Dictionary<string, string> Hotkeys { get; private set; } = new();

    private void LoadFrom(AppSettings settings)
    {
        EnableUpdates = settings.EnableUpdates;
        ThemeIndex = settings.Theme switch { "Dark" => 1, "Light" => 2, _ => 0 };
        AccentColor = settings.AccentColor;
        LanguageIndex = Math.Max(0, LanguageCatalog.LanguageNames.ToList().IndexOf(
            LanguageCatalog.Languages.ContainsKey(settings.Language) ? settings.Language : "English"));
        ModNoteFormat = settings.ModNoteFormat;
        TimerCornerIndex = settings.TimerCorner switch
        {
            "top-left" => 0,
            "top-right" => 1,
            "bottom-left" => 2,
            _ => 3,
        };
        TimerStyleIndex = settings.TimerStyle == "plain" ? 1 : 0;
        FfmpegPath = settings.FfmpegPath;
        YtDlpPath = settings.YtDlpPath;
        DefaultModeIndex = settings.DefaultMode == "segments" ? 1 : 0;
        Hotkeys = new Dictionary<string, string>(settings.Hotkeys);
    }

    private AppSettings CollectInto()
    {
        var settings = _edited.Clone();
        settings.EnableUpdates = EnableUpdates;
        settings.Theme = ThemeIndex switch { 1 => "Dark", 2 => "Light", _ => "Automatic" };
        settings.AccentColor = AccentColor;
        settings.Language = LanguageCatalog.LanguageNames[Math.Clamp(LanguageIndex, 0, LanguageCatalog.LanguageNames.Count - 1)];
        settings.ModNoteFormat = ModNoteFormat;
        settings.TimerCorner = TimerCornerIndex switch
        {
            0 => "top-left",
            1 => "top-right",
            2 => "bottom-left",
            _ => "bottom-right",
        };
        settings.TimerStyle = TimerStyleIndex == 1 ? "plain" : "pill";
        settings.FfmpegPath = FfmpegPath.Trim();
        settings.YtDlpPath = YtDlpPath.Trim();
        settings.DefaultMode = DefaultModeIndex == 1 ? "segments" : "loads";
        settings.Hotkeys = new Dictionary<string, string>(Hotkeys);
        return settings;
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        var oldSettings = AppServices.Settings;
        var newSettings = CollectInto();
        AppServices.SettingsService.Apply(newSettings);
        AppServices.ReloadSettings();
        _edited = AppServices.Settings.Clone();

        // Theme is cheap to re-apply, so it takes effect immediately rather than
        // waiting for the restart the remaining settings still need.
        AppServices.MainWindow?.ApplyTheme(newSettings.Theme);

        if (!newSettings.ContentEquals(oldSettings))
        {
            await AppServices.Dialogs.ShowInfoAsync(
                AppServices.Loc["Settings"],
                AppServices.Loc["Please restart the application to apply the changes."]);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _edited = AppServices.Settings.Clone();
        LoadFrom(_edited);
    }

    [RelayCommand]
    private async Task RestoreDefaultsAsync()
    {
        bool confirmed = await AppServices.Dialogs.ConfirmAsync(
            AppServices.Loc["Restore Defaults"],
            AppServices.Loc["Restore Defaults Message"],
            AppServices.Loc["OK"], AppServices.Loc["Cancel"]);
        if (!confirmed)
        {
            return;
        }
        AppServices.SettingsService.RestoreDefaults();
        AppServices.ReloadSettings();
        _edited = AppServices.Settings.Clone();
        LoadFrom(_edited);
    }

    [RelayCommand]
    private async Task CustomizeHotkeysAsync()
    {
        var updated = await HotkeyEditor.ShowAsync(Hotkeys);
        if (updated is not null)
        {
            Hotkeys = updated;
        }
    }
}
