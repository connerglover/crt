using System.Globalization;

namespace CRT.Core.Tools;

/// <summary>
/// Renders a still preview of the timer overlay on black and white, side by
/// side.
/// </summary>
/// <remarks>
/// The overlay is burned in, so its legibility depends entirely on what is
/// behind it — a white clock with the background box off vanishes on bright
/// gameplay. Showing both extremes at once answers that without exporting a
/// whole video to find out.
/// </remarks>
public static class TimerPreviewRenderer
{
    /// <summary>Half-width of the preview; the output is twice this.</summary>
    public const int PaneWidth = 640;

    public const int PaneHeight = 360;

    /// <summary>
    /// The scenario the preview depicts: a five-minute run with a fifteen-second
    /// load, sampled at 1:35 of real time.
    /// </summary>
    /// <remarks>
    /// Chosen so the two clocks read differently (1:20 loadless against 1:35
    /// real), and so the run is long enough to show minutes — a sample from a
    /// ten-second scenario would render every clock style identically and prove
    /// nothing.
    /// </remarks>
    public const decimal SampleRunEnd = 300m;

    public const decimal SampleLoadStart = 10m;

    public const decimal SampleLoadEnd = 25m;

    public const decimal SampleAt = 95m;

    /// <summary>Builds the filter chain for the preview scenario.</summary>
    public static string BuildChain(TimerOverlayOptions options) =>
        TimerFiltergraphBuilder.Build(
            runStart: 0m,
            runEnd: SampleRunEnd,
            pauses: new[] { new TimerFiltergraphBuilder.Pause(SampleLoadStart, SampleLoadEnd) },
            trimStart: 0m,
            options: options with { VideoHeight = PaneHeight });

    /// <summary>
    /// ffmpeg arguments producing the side-by-side PNG.
    /// </summary>
    /// <remarks>
    /// Two synthetic sources are generated at one frame per second and every
    /// frame is written to the same file, so the last one — the sample time —
    /// is what remains. That avoids seeking a filtered graph, which is awkward
    /// once the two panes are stacked together.
    /// </remarks>
    public static string[] BuildArguments(string chain, string outputPath, TimerOverlayOptions options)
    {
        string duration = (SampleAt + 1m).ToString(CultureInfo.InvariantCulture);
        string size = $"{PaneWidth}x{PaneHeight}";

        // Each pane is composed separately so a rounded background is overlaid
        // onto both, then the two are stacked.
        string black = TimerFiltergraphBuilder.ComposeGraph(chain, options, "0:v", "a");
        string white = TimerFiltergraphBuilder.ComposeGraph(chain, options, "1:v", "b");

        return new[]
        {
            "-y",
            "-f", "lavfi", "-i", $"color=c=black:s={size}:d={duration}:r=1",
            "-f", "lavfi", "-i", $"color=c=white:s={size}:d={duration}:r=1",
            "-filter_complex", $"{black};{white};[a][b]hstack=inputs=2",
            "-update", "1",
            "-frames:v", ((int)SampleAt + 1).ToString(CultureInfo.InvariantCulture),
            outputPath,
        };
    }
}
