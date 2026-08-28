using System.Linq;
using CRT.Core.Tools;
using Xunit;

namespace CRT.Core.Tests;

public class TimerFiltergraphBuilderTests
{
    // Verified against real ffmpeg output frames: run starts t=1, one load 2→3,
    // run ends t=4.5. VideoHeight 432 at the default 5.5% → fontsize 24.
    // Pinned to Full, the style this scenario was originally captured with.
    private static readonly TimerOverlayOptions Options =
        new(VideoHeight: 432) { ClockStyle = TimerClockStyle.Full };

    private const string Prefix =
        "drawtext=fontfile='C\\:/Windows/Fonts/consola.ttf':fontsize=24:fontcolor=0xFFFFFF:" +
        "box=1:boxcolor=0x000000@0.55:boxborderw=10:x=w-tw-24:y=h-th-24";

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
    public void BackgroundOff_OmitsBox()
    {
        var options = Options with { VideoHeight = 1080, Background = false };
        string chain = TimerFiltergraphBuilder.Build(0m, 1m, Array.Empty<TimerFiltergraphBuilder.Pause>(), 0m, options);
        Assert.DoesNotContain("box=1", chain);
        Assert.Contains("fontsize=59", chain); // 1080 * 5.5%
    }

    [Theory]
    [InlineData("top-left", ":x=24:y=24")]
    [InlineData("top-right", ":x=w-tw-24:y=24")]
    [InlineData("bottom-left", ":x=24:y=h-th-24")]
    [InlineData("bottom-right", ":x=w-tw-24:y=h-th-24")]
    public void Corners(string corner, string expected)
    {
        var options = Options with { Corner = corner };
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
            runStart: 2m, runEnd: 6m,
            pauses: new[]
            {
                new TimerFiltergraphBuilder.Pause(0m, 1m),   // entirely before the run
                new TimerFiltergraphBuilder.Pause(1m, 3m),   // clamped to 2→3
                new TimerFiltergraphBuilder.Pause(7m, 8m),   // entirely after
            },
            trimStart: 0m,
            options: Options);

        Assert.Contains("between(t,2,3)", chain);
        Assert.DoesNotContain("between(t,0,1)", chain);
        Assert.DoesNotContain("between(t,7,8)", chain);
    }
}

public class TimerClockStyleTests
{
    private static readonly TimerFiltergraphBuilder.Pause[] OneLoad = { new(2m, 3m) };

    private static TimerOverlayOptions Options(TimerClockStyle style, string format = "{time_without_loads}") =>
        new(VideoHeight: 1080) { ClockStyle = style, Format = format };

    [Fact]
    public void Full_AlwaysUsesHoursMinutesSeconds()
    {
        string chain = TimerFiltergraphBuilder.Build(0m, 5m, OneLoad, 0m, Options(TimerClockStyle.Full));
        Assert.Contains("trunc((t-0)/3600)\\:d\\:2", chain);
        Assert.Contains("00\\:00\\:04.000", chain); // final, 5s run minus a 1s load
    }

    [Fact]
    public void Fitted_ShortRunDropsToSecondsOnly()
    {
        // 4s of loadless time never reaches a minute, so no minute field appears.
        string chain = TimerFiltergraphBuilder.Build(0m, 5m, OneLoad, 0m, Options(TimerClockStyle.Fitted));
        Assert.DoesNotContain("/3600", chain);
        Assert.DoesNotContain("/60", chain);
        Assert.Contains("4.000", chain);
    }

    [Fact]
    public void Fitted_FiveMinuteRunKeepsMinutesFromTheFirstSecond()
    {
        // The example from the request: in a five-minute run one second reads
        // 0:01.000, so the minute field is present even while it is zero.
        string chain = TimerFiltergraphBuilder.Build(0m, 300m, Array.Empty<TimerFiltergraphBuilder.Pause>(), 0m,
            Options(TimerClockStyle.Fitted));
        Assert.Contains("trunc((t-0)/60)\\:d\\:1", chain);
        Assert.DoesNotContain("/3600", chain);
        Assert.Contains("5\\:00.000", chain); // held final
    }

    [Fact]
    public void Compact_SplitsTheWindowWhereTheClockGrows()
    {
        // Below a minute it is S.mmm; at a minute it becomes M:SS.mmm, and
        // drawtext cannot switch format mid-expression — so the run window is
        // cut at the crossing and each side gets its own filter.
        string chain = TimerFiltergraphBuilder.Build(0m, 300m, Array.Empty<TimerFiltergraphBuilder.Pause>(), 0m,
            Options(TimerClockStyle.Compact));
        Assert.Contains("between(t,0,60)", chain);
        Assert.Contains("between(t,60,300)", chain);

        string beforeMinute = chain[..chain.IndexOf("between(t,60,300)", StringComparison.Ordinal)];
        Assert.DoesNotContain("/60", beforeMinute);
    }

    [Fact]
    public void Compact_ShortRunNeverSplits()
    {
        string chain = TimerFiltergraphBuilder.Build(0m, 5m, OneLoad, 0m, Options(TimerClockStyle.Compact));
        Assert.DoesNotContain("between(t,60", chain);
    }
}

public class TimerFormatTests
{
    private static readonly TimerFiltergraphBuilder.Pause[] OneLoad = { new(2m, 3m) };

    private static TimerOverlayOptions Options(string format) =>
        new(VideoHeight: 1080) { Format = format, ClockStyle = TimerClockStyle.Full };

    [Fact]
    public void BothClocksOnOneLine_FreezeIndependently()
    {
        // During the load the loadless clock is held while the real-time clock
        // keeps running — one window, two different clock states.
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m,
            Options("{time_without_loads} / {time_with_loads}"));

        string loadWindow = chain
            .Split(",drawtext")
            .First(f => f.Contains("between(t,2,3)", StringComparison.Ordinal));

        Assert.Contains("00\\:00\\:01.000", loadWindow); // loadless, held
        Assert.Contains("%{eif", loadWindow);            // real time, still ticking
    }

    [Fact]
    public void TwoLinesStackWithOneFilterEach()
    {
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m,
            Options("{time_without_loads}\n{time_with_loads}"));
        Assert.Contains(":y=h-th-24-88:", chain); // upper line, one line-height up
        Assert.Contains(":y=h-th-24:", chain);    // lower line, on the edge
    }

    [Fact]
    public void FinalsDifferByTheLoadLength()
    {
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m,
            Options("{time_without_loads}\n{time_with_loads}"));
        Assert.Contains("00\\:00\\:03.000", chain); // 4s run minus 1s load
        Assert.Contains("00\\:00\\:04.000", chain); // real time
    }

    [Fact]
    public void LiteralTextIsKeptAndEscaped()
    {
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m,
            Options("Time: {time_without_loads}"));
        // The colon in the caption must be escaped or drawtext reads it as the
        // start of another option.
        Assert.Contains("Time\\:", chain);
    }

    [Fact]
    public void ApostropheInACaptionCannotBreakTheFilter()
    {
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m,
            Options("Conner's {time_without_loads}"));
        Assert.Contains("Conners", chain);
        Assert.Equal(0, chain.Count(c => c == '\'') % 2);
    }

    [Fact]
    public void OneClockOnly_EmitsNothingForTheOther()
    {
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m, Options("{time_with_loads}"));
        // The real-time clock never freezes, so the run is one window and the
        // loadless clock's frozen constant never appears.
        Assert.DoesNotContain("00\\:00\\:03.000", chain);
        Assert.Contains("00\\:00\\:04.000", chain);
    }

    [Fact]
    public void UnknownPlaceholderIsLeftAsText()
    {
        string chain = TimerFiltergraphBuilder.Build(1m, 5m, OneLoad, 0m, Options("{nope} {time_without_loads}"));
        Assert.Contains("{nope}", chain);
    }
}

public class TimerPresetTests
{
    [Fact]
    public void EveryPresetProducesAUsableChain()
    {
        foreach (var preset in TimerPresets.All)
        {
            var options = new TimerOverlayOptions(VideoHeight: 1080)
            {
                Format = preset.Format,
                ClockStyle = preset.ClockStyle,
            };
            string chain = TimerFiltergraphBuilder.Build(
                1m, 5m, new[] { new TimerFiltergraphBuilder.Pause(2m, 3m) }, 0m, options);

            Assert.False(string.IsNullOrWhiteSpace(chain), preset.Name);
            Assert.Equal(0, chain.Count(c => c == '\'') % 2);
            Assert.Contains("drawtext=", chain);
        }
    }

    [Fact]
    public void PresetsRoundTripThroughMatch()
    {
        foreach (var preset in TimerPresets.All)
        {
            Assert.Equal(preset, TimerPresets.Match(preset.Format, preset.ClockStyle));
        }
    }

    [Fact]
    public void AnEditedFormatMatchesNoPreset()
    {
        Assert.Null(TimerPresets.Match("{time_without_loads} custom", TimerClockStyle.Fitted));
    }

    [Fact]
    public void NamesLeadWithCustom()
    {
        Assert.Equal(TimerPresets.Custom, TimerPresets.Names[0]);
        Assert.Equal(TimerPresets.All.Count + 1, TimerPresets.Names.Count);
    }
}

public class TimerFontCatalogTests
{
    [Fact]
    public void ResolvesToAForwardSlashPath()
    {
        string path = TimerFontCatalog.ResolveFile("Consolas", bold: false);
        Assert.DoesNotContain("\\", path);
        Assert.EndsWith(".ttf", path);
    }

    [Fact]
    public void UnknownFamilyFallsBackToTheDefault()
    {
        string path = TimerFontCatalog.ResolveFile("Not A Font", bold: false);
        Assert.Contains("consola", path);
    }

    [Theory]
    [InlineData("#ff8800", 100, "0xFF8800")]
    [InlineData("ff8800", 100, "0xFF8800")]
    [InlineData("#000000", 55, "0x000000@0.55")]
    [InlineData("#000000", 0, "0x000000@0")]
    public void ColorFormatting(string hex, int opacity, string expected)
    {
        Assert.Equal(expected, TimerFontCatalog.Color(hex, "FFFFFF", opacity));
    }

    [Fact]
    public void InvalidColorFallsBackInsteadOfBreakingTheFilter()
    {
        Assert.Equal("0xFFFFFF", TimerFontCatalog.Color("not-a-color", "FFFFFF"));
        Assert.Equal("0xFFFFFF", TimerFontCatalog.Color("", "FFFFFF"));
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
