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
    /// Line spacing as a multiple of the text size, where 1.0 is the font's own
    /// line height. Values below 1.0 pull the lines together.
    /// </summary>
    public double LineSpacing { get; init; } = 1.0;

    /// <summary>Outline drawn around the glyphs; 0 disables it.</summary>
    public int OutlineWidth { get; init; }

    public string OutlineColor { get; init; } = "#000000";

    /// <summary>
    /// Corner radius of the background, in pixels at the video's own scale.
    /// Ignored unless a measured box is supplied — see <see cref="BoxWidth"/>.
    /// </summary>
    public int CornerRadius { get; init; }

    /// <summary>
    /// Measured size of the rendered text block, supplied by the caller.
    /// </summary>
    /// <remarks>
    /// Rounded corners cannot come from drawtext, which only draws a square box,
    /// so the background is composited separately — and that needs a concrete
    /// size, where drawtext's own box sizes itself from the text at render time.
    /// Callers that cannot measure leave this zero and get square corners.
    /// </remarks>
    public int BoxWidth { get; init; }

    public int BoxHeight { get; init; }

    public int VideoWidth { get; init; }

    public int FontSize => Math.Max(1, (int)Math.Round(VideoHeight * TextSizePercent / 100.0));

    /// <summary>
    /// Extra pixels drawtext adds between lines. It advances by the font's own
    /// line height already, so this is the difference from that, which is
    /// negative whenever the lines are asked to sit closer than normal.
    /// </summary>
    public int LineSpacingPixels => (int)Math.Round(FontSize * (LineSpacing - 1.0));

    /// <summary>Padding between the text and the edge of its background.</summary>
    public int BoxPadding => Math.Max(4, (int)Math.Round(FontSize * 0.22));

    /// <summary>True when the background is drawn as a composited rounded rect.</summary>
    public bool UsesRoundedBackground =>
        Background && CornerRadius > 0 && BoxWidth > 0 && BoxHeight > 0;

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
        string format = (options.Format ?? "").Replace("\r\n", "\n");
        string[] lines = format.Split('\n');

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

        // Every line is emitted inside a single drawtext, separated by real
        // newlines. drawtext then draws one box around the whole block, which is
        // what makes tight spacing possible at all: as separate filters each
        // line carried its own box, and pulling them together composited two
        // translucent rectangles into a darker band where they met.
        string prefix = FilterPrefix(options);
        foreach (var window in Subdivide(windows, format, options.ClockStyle))
        {
            if (!IsVisible(window, trimStart))
            {
                continue;
            }
            string text = string.Join("\n", lines.Select(line =>
                RenderLine(line, window, options.ClockStyle, loadlessTotal, realTimeTotal, trimStart)));
            string enable = EnableExpression(window, trimStart);
            filters.Add($"{prefix}:enable='{enable}':text='{text}'");
        }

        return string.Join(",", filters);
    }

    /// <summary>
    /// Wraps a drawtext chain into a complete filtergraph, adding the rounded
    /// background underneath it when one is configured.
    /// </summary>
    /// <remarks>
    /// drawtext can only draw a square box, so a rounded background is generated
    /// as its own source and overlaid — which needs two inputs and therefore a
    /// labelled graph rather than a plain chain.
    /// </remarks>
    public static string ComposeGraph(
        string chain, TimerOverlayOptions options, string inputLabel = "0:v", string outputLabel = "")
    {
        string tail = outputLabel.Length > 0 ? $"[{outputLabel}]" : "";
        if (!options.UsesRoundedBackground)
        {
            return $"[{inputLabel}]{chain}{tail}";
        }

        int width = options.BoxWidth;
        int height = options.BoxHeight;
        int radius = Math.Min(options.CornerRadius, Math.Min(width, height) / 2);
        int alpha = (int)Math.Round(Math.Clamp(options.BackgroundOpacity, 0, 100) / 100.0 * 255);
        var (red, green, blue) = TimerFontCatalog.Rgb(options.BackgroundColor, "000000");
        string r = radius.ToString(CultureInfo.InvariantCulture);

        // Rounded-rectangle test: distance from the inset rectangle, which is
        // zero along the flat edges and grows only inside the corner squares.
        string inside =
            $"if(gt(hypot(max(0\\,max({r}-X\\,X-(W-1-{r})))\\,max(0\\,max({r}-Y\\,Y-(H-1-{r})))),{r}),0,{alpha})";

        // A unique-enough label so the graph cannot collide with the caller's.
        var (x, y) = BoxPosition(options);
        return
            $"color=c=black:s={width}x{height}:d=1,format=rgba," +
            $"geq=r={red}:g={green}:b={blue}:a='{inside}'[crtbox_{inputLabel.Replace(":", "_")}];" +
            $"[{inputLabel}][crtbox_{inputLabel.Replace(":", "_")}]" +
            $"overlay=x={x}:y={y}:eof_action=repeat[crtbg_{inputLabel.Replace(":", "_")}];" +
            $"[crtbg_{inputLabel.Replace(":", "_")}]{chain}{tail}";
    }

    /// <summary>Margin between the overlay and the edge of the frame.</summary>
    public const int Margin = 24;

    /// <summary>Top-left corner of the measured background box, in pixels.</summary>
    private static (string X, string Y) BoxPosition(TimerOverlayOptions options)
    {
        string normalized = (options.Corner ?? "").ToLowerInvariant();
        bool top = normalized.StartsWith("top", StringComparison.Ordinal);
        bool left = normalized.EndsWith("left", StringComparison.Ordinal);

        string margin = Margin.ToString(CultureInfo.InvariantCulture);
        return (left ? margin : $"W-w-{margin}", top ? margin : $"H-h-{margin}");
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
    private static IEnumerable<Window> Subdivide(List<Window> windows, string format, TimerClockStyle style)
    {
        if (style != TimerClockStyle.Compact)
        {
            return windows;
        }

        bool usesLoadless = UsesPlaceholder(format, WithoutLoadsPlaceholder);
        bool usesRealTime = UsesPlaceholder(format, WithLoadsPlaceholder);

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

    private static string FilterPrefix(TimerOverlayOptions options)
    {
        var sb = new StringBuilder("drawtext=fontfile='");
        sb.Append(EscapeColons(options.FontFile));
        sb.Append("':fontsize=").Append(options.FontSize.ToString(CultureInfo.InvariantCulture));
        sb.Append(":fontcolor=").Append(TimerFontCatalog.Color(options.TextColor, "FFFFFF"));

        if (options.LineSpacingPixels != 0)
        {
            sb.Append(":line_spacing=")
              .Append(options.LineSpacingPixels.ToString(CultureInfo.InvariantCulture));
        }
        if (options.OutlineWidth > 0)
        {
            sb.Append(":borderw=").Append(options.OutlineWidth.ToString(CultureInfo.InvariantCulture))
              .Append(":bordercolor=").Append(TimerFontCatalog.Color(options.OutlineColor, "000000"));
        }

        // drawtext's own box is square, so it is only used when the background
        // is not being composited as a rounded rectangle underneath.
        if (options.Background && !options.UsesRoundedBackground)
        {
            sb.Append(":box=1:boxcolor=")
              .Append(TimerFontCatalog.Color(options.BackgroundColor, "000000", options.BackgroundOpacity))
              .Append(":boxborderw=").Append(options.BoxPadding.ToString(CultureInfo.InvariantCulture));
        }

        var (x, y) = CornerPosition(options);
        sb.Append(":x=").Append(x).Append(":y=").Append(y);
        return sb.ToString();
    }

    /// <summary>
    /// Places the whole text block in the chosen corner.
    /// </summary>
    /// <remarks>
    /// One position now covers every line: drawtext lays the block out itself
    /// from the newlines and the line spacing, where previously each line was a
    /// separate filter that had to be offset by hand.
    /// </remarks>
    private static (string X, string Y) CornerPosition(TimerOverlayOptions options)
    {
        string normalized = (options.Corner ?? "").ToLowerInvariant();
        bool top = normalized.StartsWith("top", StringComparison.Ordinal);
        bool left = normalized.EndsWith("left", StringComparison.Ordinal);

        if (!options.UsesRoundedBackground)
        {
            // drawtext sizes and places its own box, so the text can position
            // itself from its own width.
            string edge = Margin.ToString(CultureInfo.InvariantCulture);
            return (left ? edge : $"w-tw-{edge}", top ? edge : $"h-th-{edge}");
        }

        // With a composited background the text has to line up inside a box of a
        // known size, so it is offset from the box's corner rather than from its
        // own extent — otherwise a clock narrower than the box drifts out of it.
        int padding = options.BoxPadding;
        string x = left
            ? Number(Margin + padding)
            : $"w-{Number(Margin + options.BoxWidth - padding)}";
        string y = top
            ? Number(Margin + padding)
            : $"h-{Number(Margin + options.BoxHeight - padding)}";
        return (x, y);
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

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
