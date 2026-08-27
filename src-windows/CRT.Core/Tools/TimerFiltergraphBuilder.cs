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

    /// <summary>Vertical advance between stacked timer lines.</summary>
    public int LineHeight => (int)Math.Round(FontSize * 1.5);

    /// <summary>
    /// Draw the loadless clock and the real-time clock as two stacked lines
    /// instead of one. Both are labelled, since two bare clocks side by side
    /// are indistinguishable once burned into the video.
    /// </summary>
    public bool ShowBothTimers { get; init; }

    /// <summary>Caption for the loadless clock. Supplied by the caller so it is localized.</summary>
    public string WithoutLoadsLabel { get; init; } = "";

    /// <summary>Caption for the real-time clock.</summary>
    public string WithLoadsLabel { get; init; } = "";
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
        if (!options.ShowBothTimers)
        {
            return BuildTrack(runStart, runEnd, pauses, trimStart, options, "", 0, 1);
        }

        // The real-time clock is the same run window with nothing frozen, so the
        // two tracks differ only in whether the pauses are applied.
        string withoutLoads = BuildTrack(
            runStart, runEnd, pauses, trimStart, options, options.WithoutLoadsLabel, 0, 2);
        string withLoads = BuildTrack(
            runStart, runEnd, Array.Empty<Pause>(), trimStart, options, options.WithLoadsLabel, 1, 2);
        return withoutLoads + "," + withLoads;
    }

    private static string BuildTrack(
        decimal runStart,
        decimal runEnd,
        IReadOnlyList<Pause> pauses,
        decimal trimStart,
        TimerOverlayOptions options,
        string label,
        int lineIndex,
        int lineCount)
    {
        string prefix = FilterPrefix(options, lineIndex, lineCount);
        string caption = label.Length == 0 ? "" : EscapeText(label) + " ";
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
            filters.Add($"{prefix}:enable='lt(t,{Num(runStartOut)})':text='{caption}{ConstantClock(0m)}'");
        }

        // Alternating running / frozen windows.
        decimal cursor = runStart;
        decimal accumulatedPause = 0m;
        foreach (var pause in effectivePauses)
        {
            if (pause.Start > cursor)
            {
                filters.Add(RunningWindow(prefix, caption, cursor - trimStart, pause.Start - trimStart, runStartOut + accumulatedPause));
            }
            decimal elapsedAtFreeze = pause.Start - runStart - accumulatedPause;
            filters.Add($"{prefix}:enable='between(t,{Num(pause.Start - trimStart)},{Num(pause.End - trimStart)})':text='{caption}{ConstantClock(elapsedAtFreeze)}'");
            accumulatedPause += pause.End - pause.Start;
            cursor = pause.End;
        }
        if (runEnd > cursor)
        {
            filters.Add(RunningWindow(prefix, caption, cursor - trimStart, runEndOut, runStartOut + accumulatedPause));
        }

        // After the run: the final time, held.
        decimal finalElapsed = runEnd - runStart - accumulatedPause;
        filters.Add($"{prefix}:enable='gt(t,{Num(runEndOut)})':text='{caption}{ConstantClock(finalElapsed)}'");

        return string.Join(",", filters);
    }

    private static string RunningWindow(string prefix, string caption, decimal from, decimal to, decimal offset)
    {
        string o = Num(offset);
        string text =
            $"%{{eif\\:trunc((t-{o})/3600)\\:d\\:2}}\\:" +
            $"%{{eif\\:trunc(mod((t-{o})/60,60))\\:d\\:2}}\\:" +
            $"%{{eif\\:trunc(mod(t-{o},60))\\:d\\:2}}." +
            $"%{{eif\\:trunc(mod((t-{o})*1000,1000))\\:d\\:3}}";
        return $"{prefix}:enable='between(t,{Num(from)},{Num(to)})':text='{caption}{text}'";
    }

    /// <summary>Shared drawtext style options (font, colors, box, corner position).</summary>
    private static string FilterPrefix(TimerOverlayOptions options, int lineIndex, int lineCount)
    {
        var sb = new StringBuilder("drawtext=fontfile='");
        sb.Append(EscapeColons(options.FontFile));
        sb.Append("':fontsize=").Append(options.FontSize.ToString(CultureInfo.InvariantCulture));
        sb.Append(":fontcolor=white");
        if (!string.Equals(options.Style, "plain", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(":box=1:boxcolor=black@0.55:boxborderw=10");
        }
        var (x, y) = CornerPosition(options.Corner, lineIndex, lineCount, options.LineHeight);
        sb.Append(":x=").Append(x).Append(":y=").Append(y);
        return sb.ToString();
    }

    /// <summary>
    /// Places one timer line in the chosen corner. Lines stack downwards, and
    /// bottom corners are offset upwards by the remaining lines so the block as
    /// a whole still sits against the edge.
    /// </summary>
    private static (string X, string Y) CornerPosition(string corner, int lineIndex, int lineCount, int lineHeight)
    {
        string normalized = corner.ToLowerInvariant();
        bool top = normalized.StartsWith("top", StringComparison.Ordinal);
        bool left = normalized.EndsWith("left", StringComparison.Ordinal);

        string x = left ? "24" : "w-tw-24";
        int offset = top ? lineIndex * lineHeight : (lineCount - 1 - lineIndex) * lineHeight;
        string y = top
            ? (24 + offset).ToString(CultureInfo.InvariantCulture)
            : offset == 0 ? "h-th-24" : $"h-th-24-{offset.ToString(CultureInfo.InvariantCulture)}";
        return (x, y);
    }

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

    /// <summary>
    /// Escapes caption text for a drawtext <c>text='...'</c> value. An
    /// apostrophe would terminate the quoted value and there is no way to
    /// reopen it inside a single argv entry, so it is dropped rather than
    /// escaped — losing a punctuation mark beats producing a filtergraph
    /// ffmpeg refuses to parse.
    /// </summary>
    private static string EscapeText(string text) => text
        .Replace("\\", "\\\\")
        .Replace("'", "")
        .Replace(":", "\\:")
        .Replace("%", "\\%");
}
