using CRT.Core.Tools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CRT.Services;

/// <summary>
/// Measures the rendered size of the timer text, so a rounded background can be
/// drawn at the right size.
/// </summary>
/// <remarks>
/// <para>
/// drawtext sizes its own box from the text, but only draws it square. A rounded
/// background has to be generated separately at a fixed size, which means
/// knowing how wide the text will be before ffmpeg runs.
/// </para>
/// <para>
/// The measurement comes from DirectWrite via a TextBlock, while ffmpeg lays out
/// with FreeType, so the two agree closely rather than exactly. That is
/// tolerable because the box is padded: a few pixels of disagreement changes the
/// padding slightly and nothing else. It is sized to the widest text the run
/// will ever show, so the background never resizes mid-video.
/// </para>
/// </remarks>
public static class TimerTextMeasurer
{
    /// <summary>
    /// Measures the widest sample the format can produce, padded, in the video's
    /// own pixels.
    /// </summary>
    public static (int Width, int Height) Measure(TimerOverlayOptions options)
    {
        string[] lines = SampleLines(options);
        if (lines.Length == 0)
        {
            return (0, 0);
        }

        double widest = 0;
        double lineHeight = 0;
        foreach (string line in lines)
        {
            var block = new TextBlock
            {
                Text = line.Length == 0 ? " " : line,
                FontSize = options.FontSize,
                FontFamily = new FontFamily(options.FontFamily),
                FontWeight = options.Bold
                    ? Microsoft.UI.Text.FontWeights.Bold
                    : Microsoft.UI.Text.FontWeights.Normal,
            };
            block.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, block.DesiredSize.Width);
            lineHeight = Math.Max(lineHeight, block.DesiredSize.Height);
        }

        // Match how drawtext stacks: the font's line height for each line, plus
        // the configured extra spacing between them.
        double height = lineHeight * lines.Length + options.LineSpacingPixels * (lines.Length - 1);

        // The outline grows the glyphs outwards on every side.
        double outline = options.OutlineWidth * 2;
        int padding = options.BoxPadding;

        return (
            (int)Math.Ceiling(widest + outline) + padding * 2,
            (int)Math.Ceiling(height + outline) + padding * 2);
    }

    /// <summary>
    /// The literal text each format line will occupy at its widest.
    /// </summary>
    /// <remarks>
    /// Clock placeholders are live ffmpeg expressions with no literal form, so
    /// each is replaced by the longest string its clock style can render — the
    /// widest the box will ever need to be.
    /// </remarks>
    private static string[] SampleLines(TimerOverlayOptions options)
    {
        string widestClock = options.ClockStyle switch
        {
            TimerClockStyle.Full => "00:00:00.000",
            // Compact and Fitted both grow with the run; an hour-long run is the
            // widest either reaches.
            _ => "9:59:59.999",
        };

        return (options.Format ?? "")
            .Replace("\r\n", "\n")
            .Replace("{" + TimerFiltergraphBuilder.WithoutLoadsPlaceholder + "}", widestClock)
            .Replace("{" + TimerFiltergraphBuilder.WithLoadsPlaceholder + "}", widestClock)
            .Split('\n');
    }
}
