using System.Linq;
using CRT.Core.Tools;
using Xunit;

namespace CRT.Core.Tests;

public class TimerFiltergraphBuilderTests
{
    // Verified against real ffmpeg output frames: run starts t=1, one load 2→3,
    // run ends t=4.5. VideoHeight 432 → fontsize 24.
    private static readonly TimerOverlayOptions Options = new(VideoHeight: 432);

    private const string Prefix =
        "drawtext=fontfile='C\\:/Windows/Fonts/consola.ttf':fontsize=24:fontcolor=white:" +
        "box=1:boxcolor=black@0.55:boxborderw=10:x=w-tw-24:y=h-th-24";

    private static string Running(string o) =>
        $"%{{eif\\:trunc((t-{o})/3600)\\:d\\:2}}\\:" +
        $"%{{eif\\:trunc(mod((t-{o})/60,60))\\:d\\:2}}\\:" +
        $"%{{eif\\:trunc(mod(t-{o},60))\\:d\\:2}}." +
        $"%{{eif\\:trunc(mod((t-{o})*1000,1000))\\:d\\:3}}";

    [Fact]
    public void ReferenceScenario_ExactChain()
    {
        string chain = TimerFiltergraphBuilder.Build(
            runStart: 1m,
            runEnd: 4.5m,
            pauses: new[] { new TimerFiltergraphBuilder.Pause(2m, 3m) },
            trimStart: 0m,
            options: Options);

        string expected = string.Join(",",
            $"{Prefix}:enable='lt(t,1)':text='00\\:00\\:00.000'",
            $"{Prefix}:enable='between(t,1,2)':text='{Running("1")}'",
            $"{Prefix}:enable='between(t,2,3)':text='00\\:00\\:01.000'",
            $"{Prefix}:enable='between(t,3,4.5)':text='{Running("2")}'",
            $"{Prefix}:enable='gt(t,4.5)':text='00\\:00\\:02.500'");

        Assert.Equal(expected, chain);
    }

    [Fact]
    public void NoPauses_SingleRunningWindow()
    {
        string chain = TimerFiltergraphBuilder.Build(2m, 10m, Array.Empty<TimerFiltergraphBuilder.Pause>(), 0m, Options);
        string expected = string.Join(",",
            $"{Prefix}:enable='lt(t,2)':text='00\\:00\\:00.000'",
            $"{Prefix}:enable='between(t,2,10)':text='{Running("2")}'",
            $"{Prefix}:enable='gt(t,10)':text='00\\:00\\:08.000'");
        Assert.Equal(expected, chain);
    }

    [Fact]
    public void TrimStart_ShiftsAllWindowTimes()
    {
        string chain = TimerFiltergraphBuilder.Build(
            runStart: 2m, runEnd: 5m,
            pauses: new[] { new TimerFiltergraphBuilder.Pause(3m, 4m) },
            trimStart: 0.5m,
            options: Options);

        string expected = string.Join(",",
            $"{Prefix}:enable='lt(t,1.5)':text='00\\:00\\:00.000'",
            $"{Prefix}:enable='between(t,1.5,2.5)':text='{Running("1.5")}'",
            $"{Prefix}:enable='between(t,2.5,3.5)':text='00\\:00\\:01.000'",
            $"{Prefix}:enable='between(t,3.5,4.5)':text='{Running("2.5")}'",
            $"{Prefix}:enable='gt(t,4.5)':text='00\\:00\\:02.000'");
        Assert.Equal(expected, chain);
    }

    [Fact]
    public void RunStartAtTrimStart_OmitsPreWindow()
    {
        string chain = TimerFiltergraphBuilder.Build(0m, 5m, Array.Empty<TimerFiltergraphBuilder.Pause>(), 0m, Options);
        Assert.StartsWith($"{Prefix}:enable='between(t,0,5)'", chain);
    }

    [Fact]
    public void PlainStyle_OmitsBox()
    {
        var options = new TimerOverlayOptions(VideoHeight: 1080, Style: "plain");
        string chain = TimerFiltergraphBuilder.Build(0m, 1m, Array.Empty<TimerFiltergraphBuilder.Pause>(), 0m, options);
        Assert.DoesNotContain("box=1", chain);
        Assert.Contains("fontsize=60", chain); // 1080 / 18
    }

    [Theory]
    [InlineData("top-left", ":x=24:y=24")]
    [InlineData("top-right", ":x=w-tw-24:y=24")]
    [InlineData("bottom-left", ":x=24:y=h-th-24")]
    [InlineData("bottom-right", ":x=w-tw-24:y=h-th-24")]
    public void Corners(string corner, string expected)
    {
        var options = new TimerOverlayOptions(VideoHeight: 432, Corner: corner);
        string chain = TimerFiltergraphBuilder.Build(0m, 1m, Array.Empty<TimerFiltergraphBuilder.Pause>(), 0m, options);
        Assert.Contains(expected, chain);
    }

    [Fact]
    public void ConstantClock_Formatting()
    {
        Assert.Equal("00\\:00\\:00.000", TimerFiltergraphBuilder.ConstantClock(0m));
        Assert.Equal("00\\:01\\:23.456", TimerFiltergraphBuilder.ConstantClock(83.456m));
        Assert.Equal("01\\:00\\:00.000", TimerFiltergraphBuilder.ConstantClock(3600m));
        Assert.Equal("00\\:00\\:00.000", TimerFiltergraphBuilder.ConstantClock(-1m));
    }

    [Fact]
    public void PausesOutsideRun_AreClampedOrDropped()
    {
        string chain = TimerFiltergraphBuilder.Build(
            5m, 10m,
            new[]
            {
                new TimerFiltergraphBuilder.Pause(0m, 1m),   // entirely before → dropped
                new TimerFiltergraphBuilder.Pause(6m, 7m),
                new TimerFiltergraphBuilder.Pause(11m, 12m), // entirely after → dropped
            },
            0m, Options);
        Assert.Contains("between(t,6,7)", chain);
        Assert.DoesNotContain("between(t,0,1)", chain);
        Assert.DoesNotContain("between(t,11,12)", chain);
    }
}

public class FfmpegExporterTests
{
    [Fact]
    public void ComputeTrim_LeadAndTailClamped()
    {
        Assert.Equal((0m, 12m), FfmpegExporter.ComputeTrim(1m, 10m, 100m));      // lead clamps to 0
        Assert.Equal((8m, 22m), FfmpegExporter.ComputeTrim(10m, 20m, 100m));
        Assert.Equal((8m, 21m), FfmpegExporter.ComputeTrim(10m, 20m, 21m));      // tail clamps to duration
    }

    [Fact]
    public void BuildArguments_ExactShape()
    {
        var args = FfmpegExporter.BuildArguments("in.mp4", "out.mp4", 1.5m, 10m, "CHAIN");
        Assert.Equal(new[]
        {
            "-y", "-ss", "1.5", "-to", "10", "-i", "in.mp4", "-vf", "CHAIN",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
            "-c:a", "aac", "-movflags", "+faststart", "out.mp4",
        }, args);
    }
}

public class DualTimerTests
{
    private static readonly TimerFiltergraphBuilder.Pause[] OneLoad =
        { new(2m, 3m) };

    private static TimerOverlayOptions Options(bool both, string corner = "bottom-right") =>
        new(VideoHeight: 1080, Corner: corner)
        {
            ShowBothTimers = both,
            WithoutLoadsLabel = "Without Loads",
            WithLoadsLabel = "With Loads",
        };

    [Fact]
    public void SingleTimerIsUnchangedByTheDualOption()
    {
        // The default path must produce what it did before the option existed:
        // no caption, and every line flush against the corner with no stacking
        // offset applied.
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m, Options(both: false));
        Assert.DoesNotContain("Without Loads", chain);
        Assert.DoesNotContain("With Loads", chain);
        Assert.DoesNotContain(":y=h-th-24-", chain);
        Assert.Contains(":y=h-th-24:", chain);
    }

    [Fact]
    public void BothTimersAreDrawnAndLabelled()
    {
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m, Options(both: true));
        Assert.Contains("Without Loads", chain);
        Assert.Contains("With Loads", chain);
    }

    [Fact]
    public void OnlyTheLoadlessTrackFreezes()
    {
        // The real-time clock ignores the loads entirely, so the frozen constant
        // that the loadless track emits during the load must not appear on it.
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m, Options(both: true));
        string[] tracks = chain.Split("With Loads");

        // Everything before the first "With Loads" caption belongs to the
        // loadless track, which freezes at 00:00:01.000 for the load window.
        Assert.Contains("between(t,2,3)", tracks[0]);
        Assert.Contains("Without Loads 00\\:00\\:01.000", tracks[0]);

        // The real-time track spans the run with no frozen window inside it.
        string realtime = string.Join("With Loads", tracks[1..]);
        Assert.DoesNotContain("between(t,2,3)", realtime);
    }

    [Fact]
    public void FinalTimesDifferByTheLoadLength()
    {
        // 4s run with a 1s load: 00:00:03.000 loadless, 00:00:04.000 real time.
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m, Options(both: true));
        Assert.Contains("Without Loads 00\\:00\\:03.000", chain);
        Assert.Contains("With Loads 00\\:00\\:04.000", chain);
    }

    [Fact]
    public void BottomCornerStacksUpwardsSoTheBlockStaysOnTheEdge()
    {
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m, Options(both: true));
        // Second line sits on the edge, first line one line-height (1.5 x the
        // 60px font at 1080p) above it.
        Assert.Contains(":y=h-th-24-90:", chain);
        Assert.Contains(":y=h-th-24:", chain);
    }

    [Fact]
    public void TopCornerStacksDownwards()
    {
        string chain = TimerFiltergraphBuilder.Build(
            1m, 5m, OneLoad, 0m, Options(both: true, corner: "top-left"));
        Assert.Contains(":y=24:", chain);
        Assert.Contains(":y=114:", chain);
        Assert.Contains(":x=24:", chain);
    }

    [Fact]
    public void ApostropheInALabelCannotBreakTheFilter()
    {
        // A stray quote would close text='...' and make ffmpeg reject the graph.
        var options = new TimerOverlayOptions(VideoHeight: 1080)
        {
            ShowBothTimers = true,
            WithoutLoadsLabel = "Sans l'chargement",
            WithLoadsLabel = "With Loads",
        };
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m, options);
        Assert.Contains("Sans lchargement", chain);
        // Every quote in the graph must still be part of a matched pair, or
        // ffmpeg cannot parse the filter at all.
        Assert.Equal(0, chain.Count(c => c == '\'') % 2);
    }
}
