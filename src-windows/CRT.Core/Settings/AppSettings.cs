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

    /// <summary>
    /// Template for the burned-in timer, same placeholder vocabulary as the mod
    /// note: <c>{time_without_loads}</c> / <c>{time_with_loads}</c>. A newline
    /// stacks another line.
    /// </summary>
    public string TimerFormat { get; set; } = Tools.TimerFiltergraphBuilder.DefaultFormat;

    /// <summary>compact | fitted | full.</summary>
    public string TimerClockStyle { get; set; } = "fitted";

    public string TimerFontFamily { get; set; } = Tools.TimerFontCatalog.DefaultFamily;

    public bool TimerBold { get; set; }

    /// <summary>Text height as a percentage of the video height.</summary>
    public double TimerTextSize { get; set; } = 5.5;

    /// <summary>Gap between stacked timer lines, as a multiple of the text size.</summary>
    public double TimerLineSpacing { get; set; } = 1.2;

    public string TimerTextColor { get; set; } = "#ffffff";

    public bool TimerBackground { get; set; } = true;

    public string TimerBackgroundColor { get; set; } = "#000000";

    public int TimerBackgroundOpacity { get; set; } = 55;

    /// <summary>Explicit ffmpeg path; empty = auto-discover.</summary>
    public string FfmpegPath { get; set; } = "";

    /// <summary>Explicit yt-dlp path; empty = auto-discover.</summary>
    public string YtDlpPath { get; set; } = "";

    /// <summary>
    /// segments | loads. Segment mode is the default; "loads" is the classic
    /// start/end-plus-loads workflow, kept for runs timed that way.
    /// </summary>
    public string DefaultMode { get; set; } = "segments";

    /// <summary>True when <see cref="DefaultMode"/> selects the classic workflow.</summary>
    public bool ClassicMode
    {
        get => !string.Equals(DefaultMode, "segments", StringComparison.OrdinalIgnoreCase);
        set => DefaultMode = value ? "loads" : "segments";
    }

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
        TimerFormat = TimerFormat,
        TimerClockStyle = TimerClockStyle,
        TimerFontFamily = TimerFontFamily,
        TimerBold = TimerBold,
        TimerTextSize = TimerTextSize,
        TimerLineSpacing = TimerLineSpacing,
        TimerTextColor = TimerTextColor,
        TimerBackground = TimerBackground,
        TimerBackgroundColor = TimerBackgroundColor,
        TimerBackgroundOpacity = TimerBackgroundOpacity,
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
        TimerFormat == other.TimerFormat &&
        TimerClockStyle == other.TimerClockStyle &&
        TimerFontFamily == other.TimerFontFamily &&
        TimerBold == other.TimerBold &&
        TimerTextSize == other.TimerTextSize &&
        TimerLineSpacing == other.TimerLineSpacing &&
        TimerTextColor == other.TimerTextColor &&
        TimerBackground == other.TimerBackground &&
        TimerBackgroundColor == other.TimerBackgroundColor &&
        TimerBackgroundOpacity == other.TimerBackgroundOpacity &&
        FfmpegPath == other.FfmpegPath &&
        YtDlpPath == other.YtDlpPath &&
        DefaultMode == other.DefaultMode &&
        Hotkeys.Count == other.Hotkeys.Count &&
        Hotkeys.All(kv => other.Hotkeys.TryGetValue(kv.Key, out string? v) && v == kv.Value);
}
