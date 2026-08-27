using System.Text.RegularExpressions;

namespace CRT.Core.Hotkeys;

/// <summary>A user-customizable hotkey action.</summary>
/// <param name="Id">The dispatch key (matches the Python action ids).</param>
/// <param name="LabelKey">Key into the localization catalog for the editor row label.</param>
/// <param name="Default">Default key sequence, e.g. "Ctrl+S".</param>
public sealed record HotkeyAction(string Id, string LabelKey, string Default);

/// <summary>
/// Central registry of user-customizable hotkey actions — ported from
/// <c>src/crt/hotkeys/app.py</c> plus the new video-mode actions.
/// </summary>
public static partial class HotkeyRegistry
{
    public static readonly IReadOnlyList<HotkeyAction> Actions = new List<HotkeyAction>
    {
        new("New Time", "New Time", "Ctrl+N"),
        new("Open Time", "Open Time", "Ctrl+O"),
        new("Session History", "Session History", "Ctrl+H"),
        new("Save", "Save", "Ctrl+S"),
        new("Save As", "Save As", "Ctrl+Shift+S"),
        new("Settings", "Settings", "Ctrl+,"),
        new("Copy Mod Note", "Copy Mod Note", "Ctrl+M"),
        new("Copy Discord Message", "Copy Discord Message", "Ctrl+Shift+D"),
        new("Copy YouTube Chapters", "Copy YouTube Chapters", "Ctrl+Shift+Y"),
        new("Clear Loads", "Clear Loads", "Ctrl+Shift+L"),
        new("start_paste", "Paste Start Frame", "Ctrl+1"),
        new("end_paste", "Paste End Frame", "Ctrl+2"),
        new("start_loads_paste", "Paste Start Frame (Loads)", "Ctrl+3"),
        new("end_loads_paste", "Paste End Frame (Loads)", "Ctrl+4"),
        new("Add Loads", "Add Load", "Ctrl+L"),
        // New (native rewrite): video retimer + mode toggle.
        new("video_frame_back", "Frame Back", ","),
        new("video_frame_forward", "Frame Forward", "."),
        new("video_play_pause", "Play/Pause", "Space"),
        new("video_mark_start", "Mark Segment Start", "["),
        new("video_mark_end", "Mark Segment End", "]"),
        new("video_mark_load_start", "Mark Load Start", "L"),
        new("video_mark_load_end", "Mark Load End", "Shift+L"),
        new("Toggle Mode", "Toggle Mode", "Ctrl+T"),
    };

    /// <summary>Actions surfaced as menu entries (shortcut is shown beside the item).</summary>
    public static readonly IReadOnlySet<string> MenuActionIds = new HashSet<string>
    {
        "New Time", "Open Time", "Session History", "Save", "Save As", "Settings",
        "Copy Mod Note", "Copy Discord Message", "Copy YouTube Chapters", "Clear Loads",
    };

    public static readonly IReadOnlyDictionary<string, string> Defaults =
        Actions.ToDictionary(a => a.Id, a => a.Default);

    public static readonly IReadOnlyDictionary<string, string> OptionNames =
        Actions.ToDictionary(a => a.Id, a => OptionName(a.Id));

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();

    /// <summary>
    /// Converts an action id into a safe, stable INI option name — the same
    /// <c>[^a-z0-9]+ → _</c> slug rule as the Python app.
    /// </summary>
    public static string OptionName(string actionId) =>
        SlugRegex().Replace(actionId.ToLowerInvariant(), "_").Trim('_');

    /// <summary>
    /// Finds groups of actions sharing the same key sequence. Used for the
    /// editor's duplicate detection.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> FindDuplicates(IReadOnlyDictionary<string, string> hotkeys)
    {
        return hotkeys
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => (IReadOnlyList<string>)g.Select(kv => kv.Key).ToList())
            .ToList();
    }
}
