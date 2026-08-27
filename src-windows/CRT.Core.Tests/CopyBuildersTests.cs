using CRT.Core.Formatting;
using CRT.Core.Models;
using Xunit;

namespace CRT.Core.Tests;

public class ModNoteBuilderTests
{
    private static TimeSession SampleSession()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 7200, Framerate = 60m };
        session.AddLoad(600, 1200);
        return session;
    }

    [Fact]
    public void DefaultTemplate()
    {
        Assert.Equal(
            "Mod Note: Retimed to 01:50.000",
            ModNoteBuilder.Build(SampleSession(), ModNoteBuilder.DefaultTemplate));
    }

    [Fact]
    public void AllPlaceholders()
    {
        string template =
            "{time_without_loads}|{time_with_loads}|{hours}:{minutes}:{seconds}.{milliseconds}|" +
            "{start_frame}-{end_frame}|{start_time}-{end_time}|{total_frames}|{fps}|{plug}";
        Assert.Equal(
            "01:50.000|02:00.000|00:02:00.000|0-7200|0.0-120.0|7200|60|" +
            "[Conner's Retime Tool](https://github.com/connerglover/conners-retime-tool)",
            ModNoteBuilder.Build(SampleSession(), template));
    }

    [Fact]
    public void UnknownPlaceholders_LeftLiteral()
    {
        Assert.Equal(
            "x {no_such_thing} y 01:50.000 {}",
            ModNoteBuilder.Build(SampleSession(), "x {no_such_thing} y {time_without_loads} {}"));
    }

    [Fact]
    public void ZeroFramerate_ZeroTimes()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 100, Framerate = 0m };
        Assert.Equal(
            "00.000 00.000 0 0 0",
            ModNoteBuilder.Build(session, "{time_with_loads} {time_without_loads} {start_time} {end_time} {fps}"));
    }

    [Fact]
    public void SegmentMode_UsesSegmentTotalAndFullRun()
    {
        var session = new TimeSession { Mode = TimingMode.Segments, Framerate = 10m };
        session.AddSegment(100, 200);
        session.AddSegment(300, 500);
        Assert.Equal(
            "30.000 / 40.000 / 100-500 / 400",
            ModNoteBuilder.Build(session, "{time_without_loads} / {time_with_loads} / {start_frame}-{end_frame} / {total_frames}"));
    }
}

public class DiscordMessageBuilderTests
{
    [Fact]
    public void LoadsMode_WithLoads()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 7200, Framerate = 60m };
        session.AddLoad(600, 1200);
        Assert.Equal(
            "```\n" +
            "Time: 01:50.000\n" +
            "Time (with loads): 02:00.000\n" +
            "\n" +
            "Loads (1):\n" +
            "1. 10.000 - 20.000 (10.000)\n" +
            "```",
            DiscordMessageBuilder.Build(session));
    }

    [Fact]
    public void LoadsMode_NoLoads_OmitsSection()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 600, Framerate = 60m };
        Assert.Equal(
            "```\nTime: 10.000\nTime (with loads): 10.000\n```",
            DiscordMessageBuilder.Build(session));
    }

    [Fact]
    public void SegmentMode_ListsSegments()
    {
        var session = new TimeSession { Mode = TimingMode.Segments, Framerate = 10m };
        session.AddSegment(100, 200);
        session.AddSegment(300, 500);
        Assert.Equal(
            "```\n" +
            "Time: 30.000\n" +
            "Time (with loads): 40.000\n" +
            "\n" +
            "Segments (2):\n" +
            "1. 10.000 - 20.000 (10.000)\n" +
            "2. 30.000 - 50.000 (20.000)\n" +
            "```",
            DiscordMessageBuilder.Build(session));
    }
}

public class YouTubeChaptersBuilderTests
{
    [Fact]
    public void LoadsMode_AlternatesGameplayLoading_SortedByStart()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 108000, Framerate = 30m };
        session.AddLoad(3600, 5400);  // out of order on purpose
        session.AddLoad(900, 1800);
        Assert.Equal(
            "0:00 Gameplay\n" +
            "0:30 Loading\n" +
            "1:00 Gameplay\n" +
            "2:00 Loading\n" +
            "3:00 Gameplay",
            YouTubeChaptersBuilder.Build(session));
    }

    [Fact]
    public void SegmentMode_WaitingAndSegments()
    {
        var session = new TimeSession { Mode = TimingMode.Segments, Framerate = 30m };
        session.AddSegment(900, 1800);
        session.AddSegment(3600, 5400);
        Assert.Equal(
            "0:00 Waiting\n" +
            "0:30 Segment 1\n" +
            "1:00 Waiting\n" +
            "2:00 Segment 2\n" +
            "3:00 Waiting",
            YouTubeChaptersBuilder.Build(session));
    }
}
