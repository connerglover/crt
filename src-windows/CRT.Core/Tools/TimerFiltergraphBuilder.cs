using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CRT.Core.Tools;

/// <summary>Look and layout of the exported timer overlay.</summary>
public sealed record TimerOverlayOptions(int VideoHeight)
{
    /// <summary>
    /// Text template, same placeholder vocabulary as the mod note:
    /// <c>{time_without_loads}</c> and <c>{time_with_loads}</c>. Literal text is
    /// kept as written, and a newline starts another stacked line.
    /// </summary>
    public string Format { get; init; } = TimerFiltergraphBuilder.DefaultFormat;

    public TimerClockStyle ClockStyle { get; init; } = TimerClockStyle.Fitted;

    /// <summary>top-left | top-right | bottom-left | bottom-right.</summary>
    public string Corner { get; init; } = "bottom-right";

    public string FontFamily { get; init; } = TimerFontCatalog.DefaultFamily;

    public bool Bold { get; init; }

    /// <summary>Text height as a percentage of the video height.</summary>
    public double TextSizePercent { get; init; } = 5.5;

    public string TextColor { get; init; } = "#ffffff";

    public bool Background { get; init; } = true;

    public string BackgroundColor { get; init; } = "#000000";

    public int BackgroundOpacity { get; init; } = 55;

    /// <summary>
    /// Vertical advance between stacked lines, as a multiple of the text size.
    /// 1.0 sets the lines flush against each other.
    /// </summary>
    public double LineSpacing { get; init; } = 1.2;

    public int FontSize => Math.Max(1, (int)Math.Round(VideoHeight * TextSizePercent / 100.0));

    /// <summary>Vertical advance between stacked lines, in pixels.</summary>
    public int LineHeight => Math.Max(1, (int)Math.Round(FontSize * LineSpacing));

    public string FontFile => TimerFontCatalog.ResolveFile(FontFamily, Bold);
}

/// <summary>
/// Builds the piecewise drawtext chain for the burned-in timer (spec §9.3).
/// </summary>
/// <remarks>
/// <para>
/// The overlay is a user template rather than a fixed clock, so a line can hold
/// either clock, both, or neither plus literal text. Everything is emitted as
/// time-windowed <c>drawtext</c> filters: a clock is a live ffmpeg expression
/// while it runs and a constant while it is frozen, so a window where the
/// loadless clock is paused but the real-time clock still runs is just one
/// window with two different clock states.
/// </para>
/// <para>
/// <see cref="TimerClockStyle.Compact"/> changes width as the run passes a
/// minute or an hour, and drawtext cannot vary its format from an expression —
/// so those windows are split at the crossings and each side gets its own
/// filter. All escaping is filter-level: the whole chain reaches ffmpeg as one
/// argv entry via <c>-vf</c>, with no shell involved.
/// </para>
/// </remarks>
public static partial class TimerFiltergraphBuilder
{
    public const string DefaultWindowsFontFile = "C:/Windows/Fonts/consola.ttf";

    public const string DefaultFormat = "{time_without_loads}";

    public const string WithoutLoadsPlaceholder = "time_without_loads";

    public const string WithLoadsPlaceholder = "time_with_loads";

    [GeneratedRegex(@"\{([a-zA-Z_][a-zA-Z0-9_]*)\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>A pause window in absolute video seconds.</summary>
    public readonly record struct Pause(decimal Start, decimal End);

    /// <summary>
    /// A clock inside one window: either ticking from an offset, or held at a
    /// fixed elapsed value.
    /// </summary>
    private readonly record struct ClockState(bool Running, decimal Offset, decimal Frozen)
    {
        public static ClockState Ticking(decimal offset) => new(true, offset, 0m);

        public static ClockState Held(decimal value) => new(false, 0m, value);

        /// <summary>Elapsed value at video time <paramref name="t"/>.</summary>
        public decimal ElapsedAt(decimal t) => Running ? t - Offset : Frozen;
    }

    private readonly record struct Window(decimal From, decimal To, ClockState Loadless, ClockState RealTime);

    /// <summary>Builds the full -vf chain.</summary>
    /// <param name="runStart">Run start, absolute video seconds.</param>
    /// <param name="runEnd">Run end, absolute video seconds.</param>
    /// <param name="pauses">Frozen windows (loads / gaps between segments).</param>
    /// <param name="trimStart">Where the exported clip starts.</param>
    /// <param name="options">Template and appearance.</param>
    public static string Build(
        decimal runStart,
        decimal runEnd,
        IReadOnlyList<Pause> pauses,
        decimal trimStart,
        TimerOverlayOptions options)
    {
        string[] lines = (options.Format ?? "")
            .Replace("\r\n", "\n")
            .Split('\n');

        var windows = BuildWindows(runStart, runEnd, pauses);

        // Units are chosen from the totals the run reaches, so a Fitted clock
        // keeps one width for the whole video.
        //
        // Both clocks are measured against the larger of the two totals rather
        // than their own. A run whose loadless time stays under a minute while
        // its real time passes one would otherwise render "50.000" stacked above
        // "1:10.000" — two clocks in visibly different formats, which reads as
        // the style being broken rather than as a deliberate fit.
        decimal loadlessTotal = windows[^1].Loadless.Frozen;
        decimal realTimeTotal = windows[^1].RealTime.Frozen;
        if (options.ClockStyle == TimerClockStyle.Fitted)
        {
            decimal shared = Math.Max(loadlessTotal, realTimeTotal);
            loadlessTotal = shared;
            realTimeTotal = shared;
        }

        var filters = new List<string>();
        int lineIndex = 0;
        foreach (string line in lines)
        {
            if (line.Trim().Length == 0)
            {
                lineIndex++; // blank lines still occupy a row
                continue;
            }

            string prefix = FilterPrefix(options, lineIndex, lines.Length);
            foreach (var window in Subdivide(windows, line, options.ClockStyle))
            {
                if (!IsVisible(window, trimStart))
                {
                    continue;
                }
                string text = RenderLine(line, window, options.ClockStyle, loadlessTotal, realTimeTotal, trimStart);
                string enable = EnableExpression(window, trimStart);
                filters.Add($"{prefix}:enable='{enable}':text='{text}'");
            }
            lineIndex++;
        }

        return string.Join(",", filters);
    }

    // ── Windows ────────────────────────────────────────────────────────────

    private static List<Window> BuildWindows(decimal runStart, decimal runEnd, IReadOnlyList<Pause> pauses)
    {
        var effective = pauses
            .Select(p => new Pause(Math.Max(p.Start, runStart), Math.Min(p.End, runEnd)))
            .Where(p => p.End > p.Start)
            .OrderBy(p => p.Start)
            .ToList();

        var windows = new List<Window>();

        // Before the run both clocks read zero.
        windows.Add(new Window(decimal.MinValue, runStart, ClockState.Held(0m), ClockState.Held(0m)));

        // The real-time clock ignores the pauses entirely, so it ticks from the
        // run start for the whole run.
        ClockState realTime = ClockState.Ticking(runStart);

        decimal cursor = runStart;
        decimal accumulated = 0m;
        foreach (var pause in effective)
        {
            if (pause.Start > cursor)
            {
                windows.Add(new Window(cursor, pause.Start, ClockState.Ticking(runStart + accumulated), realTime));
            }
            decimal frozenAt = pause.Start - runStart - accumulated;
            windows.Add(new Window(pause.Start, pause.End, ClockState.Held(frozenAt), realTime));
            accumulated += pause.End - pause.Start;
            cursor = pause.End;
        }
        if (runEnd > cursor)
        {
            windows.Add(new Window(cursor, runEnd, ClockState.Ticking(runStart + accumulated), realTime));
        }

        // After the run both hold their finals.
        decimal loadlessFinal = runEnd - runStart - accumulated;
        decimal realTimeFinal = runEnd - runStart;
        windows.Add(new Window(runEnd, decimal.MaxValue, ClockState.Held(loadlessFinal), ClockState.Held(realTimeFinal)));

        return windows;
    }

    /// <summary>
    /// Splits running windows where a Compact clock would change width. Only the
    /// clocks the line actually uses are considered, so a line showing one clock
    /// is not fragmented by the other one's crossings.
    /// </summary>
    private static IEnumerable<Window> Subdivide(List<Window> windows, string line, TimerClockStyle style)
    {
        if (style != TimerClockStyle.Compact)
        {
            return windows;
        }

        bool usesLoadless = UsesPlaceholder(line, WithoutLoadsPlaceholder);
        bool usesRealTime = UsesPlaceholder(line, WithLoadsPlaceholder);

        var result = new List<Window>();
        foreach (var window in windows)
        {
            var cuts = new SortedSet<decimal>();
            if (usesLoadless)
            {
                AddCrossings(cuts, window.Loadless, window.From, window.To);
            }
            if (usesRealTime)
            {
                AddCrossings(cuts, window.RealTime, window.From, window.To);
            }

            decimal from = window.From;
            foreach (decimal cut in cuts)
            {
                result.Add(window with { From = from, To = cut });
                from = cut;
            }
            result.Add(window with { From = from, To = window.To });
        }
        return result;
    }

    private static void AddCrossings(SortedSet<decimal> cuts, ClockState clock, decimal from, decimal to)
    {
        if (!clock.Running)
        {
            return;
        }
        foreach (decimal boundary in new[] { 60m, 3600m })
        {
            decimal at = clock.Offset + boundary;
            if (at > from && at < to)
            {
                cuts.Add(at);
            }
        }
    }

    private static bool UsesPlaceholder(string line, string name) =>
        PlaceholderRegex().Matches(line).Any(m => m.Groups[1].Value == name);

    /// <summary>
    /// True when any of the window falls inside the exported clip. Trimming can
    /// cut a window away entirely, and emitting a filter that is never enabled
    /// costs decode time on every frame for nothing.
    /// </summary>
    private static bool IsVisible(Window window, decimal trimStart)
    {
        if (window.To == decimal.MaxValue)
        {
            return true;
        }
        return window.To - trimStart > 0m;
    }

    private static string EnableExpression(Window window, decimal trimStart)
    {
        // The open bounds are sentinels, so shift only the side that is real:
        // subtracting a trim from decimal.MinValue overflows, and every export
        // has a non-zero trim.
        bool openStart = window.From == decimal.MinValue;
        bool openEnd = window.To == decimal.MaxValue;

        if (openStart && openEnd)
        {
            return "1";
        }
        if (openStart)
        {
            return $"lt(t,{Num(window.To - trimStart)})";
        }
        if (openEnd)
        {
            return $"gt(t,{Num(window.From - trimStart)})";
        }
        return $"between(t,{Num(window.From - trimStart)},{Num(window.To - trimStart)})";
    }

    // ── Text ───────────────────────────────────────────────────────────────

    private static string RenderLine(
        string line,
        Window window,
        TimerClockStyle style,
        decimal loadlessTotal,
        decimal realTimeTotal,
        decimal trimStart)
    {
        // Walk the line so literal spans get escaped and generated clock
        // expressions do not: a caption may contain a colon, which drawtext
        // would otherwise read as the start of another option.
        var sb = new StringBuilder();
        int index = 0;
        foreach (Match match in PlaceholderRegex().Matches(line))
        {
            sb.Append(EscapeText(line[index..match.Index]));
            string name = match.Groups[1].Value;
            if (name == WithoutLoadsPlaceholder)
            {
                sb.Append(Clock(window.Loadless, loadlessTotal, style, trimStart, window));
            }
            else if (name == WithLoadsPlaceholder)
            {
                sb.Append(Clock(window.RealTime, realTimeTotal, style, trimStart, window));
            }
            else
            {
                sb.Append(EscapeText(match.Value)); // unknown placeholder stays literal
            }
            index = match.Index + match.Length;
        }
        sb.Append(EscapeText(line[index..]));
        return sb.ToString();
    }

    private static string Clock(
        ClockState clock,
        decimal total,
        TimerClockStyle style,
        decimal trimStart,
        Window window)
    {
        decimal atStart = clock.ElapsedAt(window.From == decimal.MinValue ? 0m : window.From);
        ClockUnit unit = UnitFor(style, total, atStart);
        return clock.Running
            ? RunningExpression(clock.Offset - trimStart, unit, style)
            : ConstantClock(clock.Frozen, unit, style);
    }

    public enum ClockUnit
    {
        Seconds,
        Minutes,
        Hours,
    }

    private static ClockUnit UnitFor(TimerClockStyle style, decimal total, decimal current) => style switch
    {
        TimerClockStyle.Full => ClockUnit.Hours,
        TimerClockStyle.Compact => UnitOf(current),
        _ => UnitOf(total),
    };

    private static ClockUnit UnitOf(decimal seconds) => seconds switch
    {
        >= 3600m => ClockUnit.Hours,
        >= 60m => ClockUnit.Minutes,
        _ => ClockUnit.Seconds,
    };

    private static string RunningExpression(decimal offset, ClockUnit unit, TimerClockStyle style)
    {
        string o = Num(offset);
        string ms = $"%{{eif\\:trunc(mod((t-{o})*1000,1000))\\:d\\:3}}";
        int leadDigits = style == TimerClockStyle.Full ? 2 : 1;

        return unit switch
        {
            ClockUnit.Seconds =>
                $"%{{eif\\:trunc(t-{o})\\:d\\:{leadDigits}}}.{ms}",
            ClockUnit.Minutes =>
                $"%{{eif\\:trunc((t-{o})/60)\\:d\\:{leadDigits}}}\\:" +
                $"%{{eif\\:trunc(mod(t-{o},60))\\:d\\:2}}.{ms}",
            _ =>
                $"%{{eif\\:trunc((t-{o})/3600)\\:d\\:{leadDigits}}}\\:" +
                $"%{{eif\\:trunc(mod((t-{o})/60,60))\\:d\\:2}}\\:" +
                $"%{{eif\\:trunc(mod(t-{o},60))\\:d\\:2}}.{ms}",
        };
    }

    /// <summary>Formats a held clock, colons filter-escaped.</summary>
    public static string ConstantClock(
        decimal elapsedSeconds,
        ClockUnit unit = ClockUnit.Hours,
        TimerClockStyle style = TimerClockStyle.Full)
    {
        if (elapsedSeconds < 0m)
        {
            elapsedSeconds = 0m;
        }
        long totalMs = (long)Math.Floor(elapsedSeconds * 1000m);
        // Full pads its leading unit to two digits; the other styles do not pad
        // the unit they lead with, only the ones below it.
        string lead = style == TimerClockStyle.Full ? "00" : "0";

        static string F(long value, string format) =>
            value.ToString(format, CultureInfo.InvariantCulture);

        return unit switch
        {
            ClockUnit.Seconds =>
                F(totalMs / 1000, lead) + "." + F(totalMs % 1000, "000"),
            ClockUnit.Minutes =>
                F(totalMs / 60_000, lead) + "\\:" +
                F(totalMs / 1000 % 60, "00") + "." + F(totalMs % 1000, "000"),
            _ =>
                F(totalMs / 3_600_000, lead) + "\\:" +
                F(totalMs / 60_000 % 60, "00") + "\\:" +
                F(totalMs / 1000 % 60, "00") + "." + F(totalMs % 1000, "000"),
        };
    }

    // ── Style ──────────────────────────────────────────────────────────────

    private static string FilterPrefix(TimerOverlayOptions options, int lineIndex, int lineCount)
    {
        var sb = new StringBuilder("drawtext=fontfile='");
        sb.Append(EscapeColons(options.FontFile));
        sb.Append("':fontsize=").Append(options.FontSize.ToString(CultureInfo.InvariantCulture));
        sb.Append(":fontcolor=").Append(TimerFontCatalog.Color(options.TextColor, "FFFFFF"));
        if (options.Background)
        {
            sb.Append(":box=1:boxcolor=")
              .Append(TimerFontCatalog.Color(options.BackgroundColor, "000000", options.BackgroundOpacity))
              .Append(":boxborderw=10");
        }
        var (x, y) = CornerPosition(options.Corner, lineIndex, lineCount, options.LineHeight);
        sb.Append(":x=").Append(x).Append(":y=").Append(y);
        return sb.ToString();
    }

    /// <summary>
    /// Places one line in the chosen corner. Lines stack downwards, and bottom
    /// corners are offset upwards by the remaining lines so the block as a whole
    /// still sits against the edge.
    /// </summary>
    private static (string X, string Y) CornerPosition(string corner, int lineIndex, int lineCount, int lineHeight)
    {
        string normalized = (corner ?? "").ToLowerInvariant();
        bool top = normalized.StartsWith("top", StringComparison.Ordinal);
        bool left = normalized.EndsWith("left", StringComparison.Ordinal);

        string x = left ? "24" : "w-tw-24";
        int offset = top ? lineIndex * lineHeight : (lineCount - 1 - lineIndex) * lineHeight;
        string y = top
            ? (24 + offset).ToString(CultureInfo.InvariantCulture)
            : offset == 0 ? "h-th-24" : $"h-th-24-{offset.ToString(CultureInfo.InvariantCulture)}";
        return (x, y);
    }

    /// <summary>Deterministic invariant decimal formatting with trailing zeros trimmed.</summary>
    internal static string Num(decimal value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string EscapeColons(string text) => text.Replace(":", "\\:");

    /// <summary>
    /// Escapes literal text for a drawtext <c>text='...'</c> value. An
    /// apostrophe would terminate the quoted value and there is no way to reopen
    /// it inside a single argv entry, so it is dropped rather than escaped —
    /// losing a punctuation mark beats producing a filtergraph ffmpeg refuses.
    /// </summary>
    private static string EscapeText(string text) => text
        .Replace("\\", "\\\\")
        .Replace("'", "")
        .Replace(":", "\\:")
        .Replace("%", "\\%");
}
