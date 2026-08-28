namespace CRT.Core.Tools;

/// <summary>A named starting point for the burned-in timer.</summary>
/// <param name="Name">Display name in the preset picker.</param>
/// <param name="Format">Template written into the format box.</param>
/// <param name="ClockStyle">Clock width the preset pairs with.</param>
public sealed record TimerPreset(string Name, string Format, TimerClockStyle ClockStyle);

/// <summary>
/// Ready-made timer layouts.
/// </summary>
/// <remarks>
/// Presets deliberately set only the template and the clock width. Font, size,
/// colors, background and position stay under the user's control, so picking a
/// preset never silently undoes the look they set up.
/// </remarks>
public static class TimerPresets
{
    public const string Custom = "Custom";

    public static IReadOnlyList<TimerPreset> All { get; } = new List<TimerPreset>
    {
        new("Minimal", "{time_without_loads}", TimerClockStyle.Compact),
        new("Minimal Dual", "{time_without_loads}\n{time_with_loads}", TimerClockStyle.Compact),
        new("Standard", "{time_without_loads}", TimerClockStyle.Fitted),
        new("Standard Dual", "{time_without_loads}\n{time_with_loads}", TimerClockStyle.Fitted),
        new("Labelled Dual", "No Loads {time_without_loads}\nWith Loads {time_with_loads}", TimerClockStyle.Fitted),
        new("Real Time Only", "{time_with_loads}", TimerClockStyle.Fitted),
        new("Full", "{time_without_loads}", TimerClockStyle.Full),
        new("Full Dual", "{time_without_loads}\n{time_with_loads}", TimerClockStyle.Full),
    };

    /// <summary>
    /// Names for the picker, with "Custom" first — an edited format matches no
    /// preset, and the picker has to be able to say so rather than keep
    /// displaying whichever one was chosen last.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } =
        new[] { Custom }.Concat(All.Select(p => p.Name)).ToList();

    /// <summary>Finds the preset matching a format and style, or null for none.</summary>
    public static TimerPreset? Match(string format, TimerClockStyle style)
    {
        string normalized = (format ?? "").Replace("\r\n", "\n");
        return All.FirstOrDefault(p => p.Format == normalized && p.ClockStyle == style);
    }
}
