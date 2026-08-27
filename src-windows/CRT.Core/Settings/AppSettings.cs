namespace CRT.Core.Settings;

/// <summary>Typed view of <c>settings.ini</c>.</summary>
public sealed class AppSettings
{
    public bool EnableUpdates { get; set; } = true;

    /// <summary>Stored in English: Automatic / Dark / Light.</summary>
    public string Theme { get; set; } = "Automatic";

    public string AccentColor { get; set; } = SettingsService.DefaultAccentColor;

    public string Language { get; set; } = "en";

    public string ModNoteFormat { get; set; } = "Mod Note: Retimed to {time_without_loads}";

    /// <summary>top-left | top-right | bottom-left | bottom-right.</summary>
    public string TimerCorner { get; set; } = "bottom-right";

    /// <summary>pill | plain.</summary>
    public string TimerStyle { get; set; } = "pill";

    /// <summary>Explicit ffmpeg path; empty = auto-discover.</summary>
    public string FfmpegPath { get; set; } = "";

    /// <summary>Explicit yt-dlp path; empty = auto-discover.</summary>
    public string YtDlpPath { get; set; } = "";

    /// <summary>loads | segments.</summary>
    public string DefaultMode { get; set; } = "loads";

    /// <summary>Hotkey sequences keyed by action id.</summary>
    public Dictionary<string, string> Hotkeys { get; set; } = new();

    public AppSettings Clone() => new()
    {
        EnableUpdates = EnableUpdates,
        Theme = Theme,
        AccentColor = AccentColor,
        Language = Language,
        ModNoteFormat = ModNoteFormat,
        TimerCorner = TimerCorner,
        TimerStyle = TimerStyle,
        FfmpegPath = FfmpegPath,
        YtDlpPath = YtDlpPath,
        DefaultMode = DefaultMode,
        Hotkeys = new Dictionary<string, string>(Hotkeys),
    };

    public bool ContentEquals(AppSettings other) =>
        EnableUpdates == other.EnableUpdates &&
        Theme == other.Theme &&
        AccentColor == other.AccentColor &&
        Language == other.Language &&
        ModNoteFormat == other.ModNoteFormat &&
        TimerCorner == other.TimerCorner &&
        TimerStyle == other.TimerStyle &&
        FfmpegPath == other.FfmpegPath &&
        YtDlpPath == other.YtDlpPath &&
        DefaultMode == other.DefaultMode &&
        Hotkeys.Count == other.Hotkeys.Count &&
        Hotkeys.All(kv => other.Hotkeys.TryGetValue(kv.Key, out string? v) && v == kv.Value);
}
