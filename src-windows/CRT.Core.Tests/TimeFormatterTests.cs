using CRT.Core.Formatting;
using CRT.Core.Models;
using Xunit;

namespace CRT.Core.Tests;

public class TimeFormatterTests
{
    [Theory]
    [InlineData("0", "00.000")]                 // zero state
    [InlineData("0.05", "00.050")]              // fractional left-pad
    [InlineData("5.5", "05.500")]               // sub-minute, seconds 2-digit
    [InlineData("59.999", "59.999")]            // just under a minute
    [InlineData("60", "01:00.000")]             // exact minute — leading unit IS zero-padded
    [InlineData("61.05", "01:01.050")]
    [InlineData("599.999", "09:59.999")]        // single-digit minutes still pad to 2
    [InlineData("3599.999", "59:59.999")]       // just under an hour
    [InlineData("3600", "01:00:00.000")]        // exact hour — leading hours zero-padded too
    [InlineData("3661.001", "01:01:01.001")]
    [InlineData("36000.123", "10:00:00.123")]   // two-digit hours unchanged
    [InlineData("-5", "00.000")]                // negative clamps to 0
    [InlineData("-0.001", "00.000")]
    public void FormatIso_EdgeCases(string input, string expected)
    {
        Assert.Equal(expected, TimeFormatter.FormatIso(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void FormatComponents_AlwaysTwoDigitUnits_ThreeDigitMs()
    {
        var (h, m, s, ms) = TimeFormatter.FormatComponents(3661.05m);
        Assert.Equal("01", h);
        Assert.Equal("01", m);
        Assert.Equal("01", s);
        Assert.Equal("050", ms);
    }

    [Fact]
    public void FormatComponents_ZeroIsAllZeros()
    {
        var (h, m, s, ms) = TimeFormatter.FormatComponents(0m);
        Assert.Equal(("00", "00", "00", "000"), (h, m, s, ms));
    }

    [Fact]
    public void FormatFrameTime_ZeroFramerate_IsZeroState()
    {
        Assert.Equal("00.000", TimeFormatter.FormatFrameTime(500, 0m, 3));
    }

    [Fact]
    public void FormatFrameTime_2997_RoundsToPrecision()
    {
        // 300 / 29.97 = 10.010010… → 10.010
        Assert.Equal("10.010", TimeFormatter.FormatFrameTime(300, 29.97m, 3));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(900, "0:30")]
    [InlineData(1800, "1:00")]
    [InlineData(108000, "1:00:00")]
    [InlineData(113429, "1:03:00")]  // floor to seconds: 113429/30 = 3780.966… → 3780s
    public void FormatYouTubeTimestamp_FlooredNoMilliseconds(int frame, string expected)
    {
        Assert.Equal(expected, TimeFormatter.FormatYouTubeTimestamp(frame, 30m));
    }
}

public class TimeSessionTests
{
    [Fact]
    public void WithAndWithoutLoads_Decimal()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 7200, Framerate = 60m };
        session.AddLoad(600, 1200);
        Assert.Equal(120.000m, session.WithLoads);
        Assert.Equal(110.000m, session.WithoutLoads);
        Assert.Equal("01:50.000", TimeFormatter.FormatIso(session.WithoutLoads));
        Assert.Equal("02:00.000", TimeFormatter.FormatIso(session.WithLoads));
    }

    [Fact]
    public void ZeroFramerate_NeverDivides()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 100, Framerate = 0m };
        Assert.Equal(0m, session.WithLoads);
        Assert.Equal(0m, session.WithoutLoads);
    }

    [Fact]
    public void Framerate2997_BehavesExactly()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 2997, Framerate = 29.97m };
        Assert.Equal(100.000m, session.WithLoads);
    }

    [Fact]
    public void AddLoad_ZeroZero_RequiresInput()
    {
        var session = new TimeSession();
        var ex = Assert.Throws<ValidationException>(() => session.AddLoad(0, 0));
        Assert.Equal("You must provide an input for the loads", ex.Message);
    }

    [Fact]
    public void AddLoad_ZeroDuration_Rejected()
    {
        var session = new TimeSession();
        var ex = Assert.Throws<ValidationException>(() => session.AddLoad(5, 5));
        Assert.Equal("The duration of the load is 0.000", ex.Message);
    }

    [Fact]
    public void AddLoad_EndsBeforeStart_Rejected()
    {
        var session = new TimeSession();
        var ex = Assert.Throws<ValidationException>(() => session.AddLoad(10, 5));
        Assert.Equal("The load time ends before it starts.", ex.Message);
    }

    [Fact]
    public void MutateLoad_Validates()
    {
        var session = new TimeSession();
        session.AddLoad(10, 20);
        Assert.Throws<ValidationException>(() => session.MutateLoad(0, startFrame: 30));
        session.MutateLoad(0, startFrame: 15, endFrame: 25);
        Assert.Equal(15, session.Loads[0].StartFrame);
        Assert.Equal(25, session.Loads[0].EndFrame);
    }

    [Fact]
    public void ConcerninglyLongLoad_Guard()
    {
        var session = new TimeSession();
        Assert.False(session.IsConcerninglyLongLoad(0, 100_000)); // no previous loads
        session.AddLoad(0, 100);
        Assert.False(session.IsConcerninglyLongLoad(0, 1000));    // == 10× avg: not concerning
        Assert.True(session.IsConcerninglyLongLoad(0, 1001));     // > 10× avg
    }

    [Fact]
    public void SegmentMode_TotalsAndSpan()
    {
        var session = new TimeSession { Mode = TimingMode.Segments, Framerate = 10m };
        session.AddSegment(100, 200);
        session.AddSegment(300, 500);
        Assert.Equal(300, session.SegmentTotalFrames);
        Assert.Equal(400, session.FullRunFrames);
        Assert.Equal(30.000m, session.SegmentTotal);
        Assert.Equal(40.000m, session.FullRun);
        // Copy actions map: primary = segment total, secondary = full run.
        Assert.Equal(session.SegmentTotal, session.PrimarySeconds);
        Assert.Equal(session.FullRun, session.SecondarySeconds);
        Assert.Equal(100, session.EffectiveStartFrame);
        Assert.Equal(500, session.EffectiveEndFrame);
    }

    [Fact]
    public void SegmentValidation_SharesLoadMessages()
    {
        var session = new TimeSession();
        Assert.Equal("The duration of the load is 0.000",
            Assert.Throws<ValidationException>(() => session.AddSegment(7, 7)).Message);
        Assert.Equal("The load time ends before it starts.",
            Assert.Throws<ValidationException>(() => session.AddSegment(9, 3)).Message);
        Assert.Equal("You must provide an input for the loads",
            Assert.Throws<ValidationException>(() => session.AddSegment(0, 0)).Message);
    }
}
