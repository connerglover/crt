using CRT.Core.Hotkeys;

namespace CRT.Core.Settings;

/// <summary>
/// Loads/saves <c>settings.ini</c> — a port of <c>src/crt/app_settings/app.py</c>.
/// The file lives at <c>%LOCALAPPDATA%\CRT\CRT\settings.ini</c> and stays
/// compatible with the Python app; missing keys/sections are synced in with
/// defaults on startup (file rewritten when that happens).
/// </summary>
public sealed class SettingsService
{
    public const string DefaultAccentColor = "#5b9bd5";
    public const string SettingsSection = "Settings";
    public const string HotkeysSection = "Hotkeys";

    /// <summary>[Settings] defaults, in the order they are written.</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Defaults = new List<KeyValuePair<string, string>>
    {
        new("enable_updates", "True"),
        new("theme", "Automatic"),
        new("accent_color", DefaultAccentColor),
        new("language", "en"),
        new("mod_note_format", "Mod Note: Retimed to {time_without_loads}"),
        // Native-only keys, synced with defaults the same way.
        new("timer_corner", "bottom-right"),
        new("timer_style", "pill"),
        new("dual_timer", "False"),
        new("ffmpeg_path", ""),
        new("ytdlp_path", ""),
        new("default_mode", "segments"),
    };

    private readonly string _filePath;
    private IniFile _ini = new();

    public SettingsService(string filePath)
    {
        _filePath = filePath;

        if (!File.Exists(_filePath))
        {
            RestoreDefaults();
        }
        else
        {
            try
            {
                _ini = IniFile.Load(_filePath);
            }
            catch (IOException)
            {
                _ini = new IniFile();
            }
            SyncMissing();
        }
    }

    public string FilePath => _filePath;

    /// <summary>Rewrites the file with pure defaults.</summary>
    public void RestoreDefaults()
    {
        _ini = new IniFile();
        _ini.EnsureSection(SettingsSection);
        foreach (var (key, value) in Defaults)
        {
            _ini.Set(SettingsSection, key, value);
        }
        _ini.EnsureSection(HotkeysSection);
        foreach (var action in HotkeyRegistry.Actions)
        {
            _ini.Set(HotkeysSection, HotkeyRegistry.OptionName(action.Id), action.Default);
        }
        _ini.Save(_filePath);
    }

    /// <summary>Adds any missing settings/hotkey options with their defaults, rewriting the file if needed.</summary>
    public void SyncMissing()
    {
        bool updated = false;

        _ini.EnsureSection(SettingsSection);
        foreach (var (key, value) in Defaults)
        {
            if (!_ini.HasOption(SettingsSection, key))
            {
                _ini.Set(SettingsSection, key, value);
                updated = true;
            }
        }

        _ini.EnsureSection(HotkeysSection);
        foreach (var action in HotkeyRegistry.Actions)
        {
            string option = HotkeyRegistry.OptionName(action.Id);
            if (!_ini.HasOption(HotkeysSection, option))
            {
                _ini.Set(HotkeysSection, option, action.Default);
                updated = true;
            }
        }

        if (updated)
        {
            _ini.Save(_filePath);
        }
    }

    public AppSettings Current()
    {
        var settings = new AppSettings
        {
            EnableUpdates = _ini.GetBoolean(SettingsSection, "enable_updates", true),
            Theme = _ini.Get(SettingsSection, "theme", "Automatic"),
            AccentColor = _ini.Get(SettingsSection, "accent_color", DefaultAccentColor),
            Language = _ini.Get(SettingsSection, "language", "en"),
            ModNoteFormat = _ini.Get(SettingsSection, "mod_note_format", "Mod Note: Retimed to {time_without_loads}"),
            TimerCorner = _ini.Get(SettingsSection, "timer_corner", "bottom-right"),
            TimerStyle = _ini.Get(SettingsSection, "timer_style", "pill"),
            DualTimer = _ini.GetBoolean(SettingsSection, "dual_timer", false),
            FfmpegPath = _ini.Get(SettingsSection, "ffmpeg_path", ""),
            YtDlpPath = _ini.Get(SettingsSection, "ytdlp_path", ""),
            DefaultMode = _ini.Get(SettingsSection, "default_mode", "segments"),
        };

        foreach (var action in HotkeyRegistry.Actions)
        {
            settings.Hotkeys[action.Id] =
                _ini.Get(HotkeysSection, HotkeyRegistry.OptionName(action.Id), action.Default);
        }

        return settings;
    }

    public void Apply(AppSettings settings)
    {
        _ini.Set(SettingsSection, "enable_updates", settings.EnableUpdates ? "True" : "False");
        _ini.Set(SettingsSection, "theme", settings.Theme);
        _ini.Set(SettingsSection, "accent_color", settings.AccentColor);
        _ini.Set(SettingsSection, "language", settings.Language);
        _ini.Set(SettingsSection, "mod_note_format", settings.ModNoteFormat);
        _ini.Set(SettingsSection, "timer_corner", settings.TimerCorner);
        _ini.Set(SettingsSection, "timer_style", settings.TimerStyle);
        _ini.Set(SettingsSection, "dual_timer", settings.DualTimer ? "True" : "False");
        _ini.Set(SettingsSection, "ffmpeg_path", settings.FfmpegPath);
        _ini.Set(SettingsSection, "ytdlp_path", settings.YtDlpPath);
        _ini.Set(SettingsSection, "default_mode", settings.DefaultMode);

        _ini.EnsureSection(HotkeysSection);
        foreach (var (actionId, sequence) in settings.Hotkeys)
        {
            if (HotkeyRegistry.OptionNames.TryGetValue(actionId, out string? option))
            {
                _ini.Set(HotkeysSection, option, sequence);
            }
        }

        _ini.Save(_filePath);
    }
}
