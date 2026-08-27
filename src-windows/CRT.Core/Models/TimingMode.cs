namespace CRT.Core.Models;

/// <summary>How a session measures its run time.</summary>
public enum TimingMode
{
    /// <summary>Classic: start of run + end of run + loads subtracted.</summary>
    Loads,

    /// <summary>Sum of explicit segment lengths.</summary>
    Segments,
}

public static class TimingModeExtensions
{
    public static string ToSerialString(this TimingMode mode) =>
        mode == TimingMode.Segments ? "segments" : "loads";

    public static TimingMode ParseSerialString(string? value) =>
        string.Equals(value, "segments", StringComparison.OrdinalIgnoreCase)
            ? TimingMode.Segments
            : TimingMode.Loads;
}
