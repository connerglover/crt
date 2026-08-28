using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRT.Core.Localization;
using CRT.Core.Models;
using CRT.Core.Settings;
using CRT.Core.Tools;
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

    // Computed, not captured: these were built once in the constructor, so a
    // language change left the pickers reading in the previous language until
    // the app was restarted.
    public IReadOnlyList<string> ThemeOptions => new[]
    {
        AppServices.Loc["Automatic"], AppServices.Loc["Dark"], AppServices.Loc["Light"],
    };

    public IReadOnlyList<string> LanguageOptions => LanguageCatalog.LanguageNames;

    public IReadOnlyList<string> TimerCornerOptions => new[]
    {
        AppServices.Loc["Top Left"], AppServices.Loc["Top Right"],
        AppServices.Loc["Bottom Left"], AppServices.Loc["Bottom Right"],
    };

    public IReadOnlyList<string> ClockStyleOptions => new[]
    {
        AppServices.Loc["Clock Compact"], AppServices.Loc["Clock Fitted"], AppServices.Loc["Clock Full"],
    };

    public IReadOnlyList<string> TimerPresetOptions => TimerPresets.Names;

    public IReadOnlyList<string> TimerFontOptions => TimerFontCatalog.Families;

    public IReadOnlyList<string> TimerWeightOptions => new[]
    {
        AppServices.Loc["Regular"], AppServices.Loc["Bold"],
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
    private string _timerFormat = "";

    [ObservableProperty]
    private int _clockStyleIndex = 1;

    [ObservableProperty]
    private int _timerFontIndex;

    [ObservableProperty]
    private int _timerWeightIndex;

    [ObservableProperty]
    private double _timerTextSize = 5.5;

    [ObservableProperty]
    private string _timerTextColor = "#ffffff";

    [ObservableProperty]
    private bool _timerBackground = true;

    [ObservableProperty]
    private string _timerBackgroundColor = "#000000";

    [ObservableProperty]
    private int _timerBackgroundOpacity = 55;

    /// <summary>
    /// Index into <see cref="TimerPresetOptions"/>. Selecting one rewrites the
    /// format and clock style; editing either afterwards drops back to Custom
    /// rather than leaving a preset name that no longer describes the settings.
    /// </summary>
    [ObservableProperty]
    private int _timerPresetIndex;

    /// <summary>Slider read-outs, so the numbers are visible while dragging.</summary>
    public string TimerTextSizeText => TimerTextSize.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";

    public string TimerBackgroundOpacityText => TimerBackgroundOpacity + "%";

    partial void OnTimerTextSizeChanged(double value) => OnPropertyChanged(nameof(TimerTextSizeText));

    partial void OnTimerBackgroundOpacityChanged(int value) => OnPropertyChanged(nameof(TimerBackgroundOpacityText));

    private bool _applyingPreset;

    partial void OnTimerPresetIndexChanged(int value)
    {
        if (_applyingPreset || value <= 0 || value > TimerPresets.All.Count)
        {
            return;
        }
        var preset = TimerPresets.All[value - 1];
        _applyingPreset = true;
        TimerFormat = preset.Format;
        ClockStyleIndex = preset.ClockStyle switch
        {
            Core.Tools.TimerClockStyle.Compact => 0,
            Core.Tools.TimerClockStyle.Full => 2,
            _ => 1,
        };
        _applyingPreset = false;
    }

    partial void OnTimerFormatChanged(string value) => SyncPresetSelection();

    partial void OnClockStyleIndexChanged(int value) => SyncPresetSelection();

    private void SyncPresetSelection()
    {
        if (_applyingPreset)
        {
            return;
        }
        var style = ClockStyleIndex switch
        {
            0 => Core.Tools.TimerClockStyle.Compact,
            2 => Core.Tools.TimerClockStyle.Full,
            _ => Core.Tools.TimerClockStyle.Fitted,
        };
        var match = TimerPresets.Match(TimerFormat, style);
        _applyingPreset = true;
        TimerPresetIndex = match is null ? 0 : TimerPresets.All.ToList().IndexOf(match) + 1;
        _applyingPreset = false;
    }

    [ObservableProperty]
    private string _ffmpegPath = "";

    [ObservableProperty]
    private string _ytDlpPath = "";

    /// <summary>
    /// The timing mode now lives here rather than on the retimer page, as a
    /// single opt-in: segment mode is the default and this reverts to the
    /// classic start/end-with-loads workflow.
    /// </summary>
    [ObservableProperty]
    private bool _classicMode;

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
        _applyingPreset = true;
        TimerFormat = settings.TimerFormat;
        ClockStyleIndex = settings.TimerClockStyle switch { "compact" => 0, "full" => 2, _ => 1 };
        _applyingPreset = false;
        SyncPresetSelection();
        TimerFontIndex = Math.Max(0, TimerFontCatalog.Families.ToList().IndexOf(settings.TimerFontFamily));
        TimerWeightIndex = settings.TimerBold ? 1 : 0;
        TimerTextSize = settings.TimerTextSize;
        TimerTextColor = settings.TimerTextColor;
        TimerBackground = settings.TimerBackground;
        TimerBackgroundColor = settings.TimerBackgroundColor;
        TimerBackgroundOpacity = settings.TimerBackgroundOpacity;
        FfmpegPath = settings.FfmpegPath;
        YtDlpPath = settings.YtDlpPath;
        ClassicMode = settings.ClassicMode;
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
        settings.TimerFormat = TimerFormat;
        settings.TimerClockStyle = ClockStyleIndex switch { 0 => "compact", 2 => "full", _ => "fitted" };
        settings.TimerFontFamily = TimerFontCatalog.Families[
            Math.Clamp(TimerFontIndex, 0, TimerFontCatalog.Families.Count - 1)];
        settings.TimerBold = TimerWeightIndex == 1;
        settings.TimerTextSize = TimerTextSize;
        settings.TimerTextColor = TimerTextColor;
        settings.TimerBackground = TimerBackground;
        settings.TimerBackgroundColor = TimerBackgroundColor;
        settings.TimerBackgroundOpacity = TimerBackgroundOpacity;
        settings.FfmpegPath = FfmpegPath.Trim();
        settings.YtDlpPath = YtDlpPath.Trim();
        settings.ClassicMode = ClassicMode;
        settings.Hotkeys = new Dictionary<string, string>(Hotkeys);
        return settings;
    }

    /// <summary>
    /// Saves the edited settings and applies them to the running app.
    /// </summary>
    /// <remarks>
    /// Synchronous now that nothing here waits on a dialog. Note the ordering:
    /// <c>ReloadSettings</c> raises the change event, and the settings page's
    /// own handler re-reads its values through <c>Cancel</c>, which clones from
    /// the freshly-loaded settings rather than the stale local copy — so the
    /// page shows what was just saved rather than reverting.
    /// </remarks>
    [RelayCommand]
    private void Apply()
    {
        var oldSettings = AppServices.Settings;
        var newSettings = CollectInto();
        AppServices.SettingsService.Apply(newSettings);
        AppServices.ReloadSettings();
        _edited = AppServices.Settings.Clone();

        // This checkbox is the only way to change timing mode, so it has to act
        // on the open session rather than only on the next one.
        AppServices.Session.SetMode(TimingModeExtensions.ParseSerialString(newSettings.DefaultMode));

        // ReloadSettings has already rebuilt the localizer and told every live
        // page to re-read its strings and rebind its hotkeys, so there is
        // nothing left that a restart would fix.
        if (!newSettings.ContentEquals(oldSettings))
        {
            AppServices.MainWindow?.ShowToast(AppServices.Loc["Settings applied"]);
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
