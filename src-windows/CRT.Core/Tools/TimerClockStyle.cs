namespace CRT.Core.Tools;

/// <summary>How wide a burned-in clock is rendered.</summary>
public enum TimerClockStyle
{
    /// <summary>
    /// Only the units currently needed, unpadded: <c>1.000</c>, then
    /// <c>10.000</c>, then <c>1:00.000</c>. Shortest to read, but the text
    /// changes width as the run passes a minute or an hour.
    /// </summary>
    Compact,

    /// <summary>
    /// The units the run reaches, fixed for its whole length: in a five-minute
    /// run, <c>0:01.000</c> at one second and <c>1:00.000</c> at one minute.
    /// Constant width without padding that never gets used.
    /// </summary>
    Fitted,

    /// <summary>Always <c>HH:MM:SS.mmm</c>, whatever the run length.</summary>
    Full,
}

public static class TimerClockStyleExtensions
{
    public static string ToSerialString(this TimerClockStyle style) => style switch
    {
        TimerClockStyle.Compact => "compact",
        TimerClockStyle.Full => "full",
        _ => "fitted",
    };

    public static TimerClockStyle ParseSerialString(string? value) => value?.ToLowerInvariant() switch
    {
        "compact" => TimerClockStyle.Compact,
        "full" => TimerClockStyle.Full,
        _ => TimerClockStyle.Fitted,
    };
}
