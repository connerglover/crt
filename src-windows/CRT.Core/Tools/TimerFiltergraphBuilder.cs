using System.Globalization;
using System.Text;

namespace CRT.Core.Tools;

/// <summary>Style options for the exported timer overlay.</summary>
public sealed record TimerOverlayOptions(
    int VideoHeight,
    string Corner = "bottom-right",
    string Style = "pill",
    string FontFile = TimerFiltergraphBuilder.DefaultWindowsFontFile)
{
    public int FontSize => Math.Max(1, VideoHeight / 18);
}

/// <summary>
/// Builds the piecewise drawtext chain for the LiveSplit-style timer overlay
/// (spec §9.3): constant 00:00:00.000 before the run, a running clock during
/// gameplay, frozen constants during loads / between segments, and the final
/// time held after the run ends. All escaping is filter-level (the whole chain
/// is passed to ffmpeg as ONE argv entry via -vf, with no shell involved).
/// </summary>
public static class TimerFiltergraphBuilder
{
    public const string DefaultWindowsFontFile = "C:/Windows/Fonts/consola.ttf";

    /// <summary>A pause window in absolute video seconds.</summary>
    public readonly record struct Pause(decimal Start, decimal End);

    /// <summary>
    /// Builds the full -vf chain.
    /// </summary>
    /// <param name="runStart">Run start, absolute video seconds.</param>
    /// <param name="runEnd">Run end, absolute video seconds.</param>
    /// <param name="pauses">Frozen windows (loads / gaps between segments), absolute video seconds.</param>
    /// <param name="trimStart">Where the exported clip starts (subtracted from every window time).</param>
    /// <param name="options">Overlay style.</param>
    public static string Build(
        decimal runStart,
        decimal runEnd,
        IReadOnlyList<Pause> pauses,
        decimal trimStart,
        TimerOverlayOptions options)
    {
        string prefix = FilterPrefix(options);
        var filters = new List<string>();

        // Clamp pauses into the run window, drop empties, sort chronologically.
        var effectivePauses = pauses
            .Select(p => new Pause(Math.Max(p.Start, runStart), Math.Min(p.End, runEnd)))
            .Where(p => p.End > p.Start)
            .OrderBy(p => p.Start)
            .ToList();

        decimal runStartOut = runStart - trimStart;
        decimal runEndOut = runEnd - trimStart;

        // Before the run: constant zero clock.
        if (runStartOut > 0m)
        {
            filters.Add($"{prefix}:enable='lt(t,{Num(runStartOut)})':text='{ConstantClock(0m)}'");
        }

        // Alternating running / frozen windows.
        decimal cursor = runStart;
        decimal accumulatedPause = 0m;
        foreach (var pause in effectivePauses)
        {
            if (pause.Start > cursor)
            {
                filters.Add(RunningWindow(prefix, cursor - trimStart, pause.Start - trimStart, runStartOut + accumulatedPause));
            }
            decimal elapsedAtFreeze = pause.Start - runStart - accumulatedPause;
            filters.Add($"{prefix}:enable='between(t,{Num(pause.Start - trimStart)},{Num(pause.End - trimStart)})':text='{ConstantClock(elapsedAtFreeze)}'");
            accumulatedPause += pause.End - pause.Start;
            cursor = pause.End;
        }
        if (runEnd > cursor)
        {
            filters.Add(RunningWindow(prefix, cursor - trimStart, runEndOut, runStartOut + accumulatedPause));
        }

        // After the run: the final time, held.
        decimal finalElapsed = runEnd - runStart - accumulatedPause;
        filters.Add($"{prefix}:enable='gt(t,{Num(runEndOut)})':text='{ConstantClock(finalElapsed)}'");

        return string.Join(",", filters);
    }

    private static string RunningWindow(string prefix, decimal from, decimal to, decimal offset)
    {
        string o = Num(offset);
        string text =
            $"%{{eif\\:trunc((t-{o})/3600)\\:d\\:2}}\\:" +
            $"%{{eif\\:trunc(mod((t-{o})/60,60))\\:d\\:2}}\\:" +
            $"%{{eif\\:trunc(mod(t-{o},60))\\:d\\:2}}." +
            $"%{{eif\\:trunc(mod((t-{o})*1000,1000))\\:d\\:3}}";
        return $"{prefix}:enable='between(t,{Num(from)},{Num(to)})':text='{text}'";
    }

    /// <summary>Shared drawtext style options (font, colors, box, corner position).</summary>
    private static string FilterPrefix(TimerOverlayOptions options)
    {
        var sb = new StringBuilder("drawtext=fontfile='");
        sb.Append(EscapeColons(options.FontFile));
        sb.Append("':fontsize=").Append(options.FontSize.ToString(CultureInfo.InvariantCulture));
        sb.Append(":fontcolor=white");
        if (!string.Equals(options.Style, "plain", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(":box=1:boxcolor=black@0.55:boxborderw=10");
        }
        var (x, y) = CornerPosition(options.Corner);
        sb.Append(":x=").Append(x).Append(":y=").Append(y);
        return sb.ToString();
    }

    private static (string X, string Y) CornerPosition(string corner) => corner.ToLowerInvariant() switch
    {
        "top-left" => ("24", "24"),
        "top-right" => ("w-tw-24", "24"),
        "bottom-left" => ("24", "h-th-24"),
        _ => ("w-tw-24", "h-th-24"), // bottom-right (default)
    };

    /// <summary>Formats elapsed seconds as a constant HH:MM:SS.mmm clock, colons filter-escaped.</summary>
    public static string ConstantClock(decimal elapsedSeconds)
    {
        if (elapsedSeconds < 0m)
        {
            elapsedSeconds = 0m;
        }
        long totalMs = (long)Math.Floor(elapsedSeconds * 1000m);
        long hours = totalMs / 3_600_000;
        long minutes = totalMs / 60_000 % 60;
        long seconds = totalMs / 1000 % 60;
        long ms = totalMs % 1000;
        return $"{hours:00}\\:{minutes:00}\\:{seconds:00}.{ms:000}";
    }

    /// <summary>Deterministic invariant decimal formatting with trailing zeros trimmed.</summary>
    internal static string Num(decimal value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string EscapeColons(string text) => text.Replace(":", "\\:");
}
