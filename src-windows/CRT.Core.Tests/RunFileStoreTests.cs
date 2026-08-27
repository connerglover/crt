using CRT.Core.Files;
using CRT.Core.Models;
using Xunit;

namespace CRT.Core.Tests;

public class RunFileStoreTests
{
    [Fact]
    public void ReadsPythonFormat()
    {
        var session = RunFileStore.Deserialize(
            "{\"start_frame\": 10, \"end_frame\": 500, \"framerate\": \"29.97\", \"loads\": [[100, 200], [250, 300]]}");
        Assert.Equal(10, session.StartFrame);
        Assert.Equal(500, session.EndFrame);
        Assert.Equal(29.97m, session.Framerate);
        Assert.Equal(2, session.Loads.Count);
        Assert.Equal(100, session.Loads[0].StartFrame);
        Assert.Equal(300, session.Loads[1].EndFrame);
        Assert.Equal(TimingMode.Loads, session.Mode); // mode/segments missing → loads mode
        Assert.Empty(session.Segments);
    }

    [Fact]
    public void ReadsNumericFramerate()
    {
        var session = RunFileStore.Deserialize(
            "{\"start_frame\": 0, \"end_frame\": 1, \"framerate\": 60, \"loads\": []}");
        Assert.Equal(60m, session.Framerate);
    }

    [Fact]
    public void WritesPythonCompatiblePrefix_ExactString()
    {
        var session = new TimeSession { StartFrame = 10, EndFrame = 500, Framerate = 29.97m };
        session.AddLoad(100, 200);
        string json = RunFileStore.Serialize(session);
        // The first four keys must match Python json.dump byte-for-byte so the
        // Python app (which reads only these) stays fully compatible.
        Assert.StartsWith(
            "{\"start_frame\": 10, \"end_frame\": 500, \"framerate\": \"29.97\", \"loads\": [[100, 200]]",
            json);
        Assert.Contains("\"mode\": \"loads\"", json);
    }

    [Fact]
    public void FramerateSerializedAsString()
    {
        var session = new TimeSession { Framerate = 60m };
        Assert.Contains("\"framerate\": \"60\"", RunFileStore.Serialize(session));
    }

    [Fact]
    public void RoundTrip_LoadsMode()
    {
        var original = new TimeSession { StartFrame = 5, EndFrame = 100, Framerate = 59.94m };
        original.AddLoad(10, 20);
        original.Meta.Title = "Any% \"quoted\"";
        original.Meta.Game = "Portal";

        var restored = RunFileStore.Deserialize(RunFileStore.Serialize(original));
        Assert.Equal(original.StartFrame, restored.StartFrame);
        Assert.Equal(original.EndFrame, restored.EndFrame);
        Assert.Equal(original.Framerate, restored.Framerate);
        Assert.Single(restored.Loads);
        Assert.Equal(TimingMode.Loads, restored.Mode);
        Assert.Equal("Any% \"quoted\"", restored.Meta.Title);
        Assert.Equal("Portal", restored.Meta.Game);
    }

    [Fact]
    public void SegmentMode_WritesBoundsAndGaps_PythonDegradesGracefully()
    {
        var session = new TimeSession { Mode = TimingMode.Segments, Framerate = 60m };
        session.AddSegment(100, 200);
        session.AddSegment(300, 500);
        session.AddSegment(550, 600);

        string json = RunFileStore.Serialize(session);
        Assert.StartsWith(
            "{\"start_frame\": 100, \"end_frame\": 600, \"framerate\": \"60\", " +
            "\"loads\": [[200, 300], [500, 550]]",
            json);

        // Bounds minus gap-loads must equal the segment total (what Python computes).
        var asPython = RunFileStore.Deserialize(
            "{\"start_frame\": 100, \"end_frame\": 600, \"framerate\": \"60\", \"loads\": [[200, 300], [500, 550]]}");
        Assert.Equal(session.SegmentTotalFrames, asPython.LengthWithoutLoads);
    }

    [Fact]
    public void RoundTrip_SegmentMode()
    {
        var original = new TimeSession { Mode = TimingMode.Segments, Framerate = 30m };
        original.AddSegment(100, 200);
        original.AddSegment(300, 500);

        var restored = RunFileStore.Deserialize(RunFileStore.Serialize(original));
        Assert.Equal(TimingMode.Segments, restored.Mode);
        Assert.Equal(2, restored.Segments.Count);
        Assert.Equal(100, restored.Segments[0].StartFrame);
        Assert.Equal(500, restored.Segments[1].EndFrame);
        // Gap-loads carried for Python compat.
        Assert.Single(restored.Loads);
        Assert.Equal(200, restored.Loads[0].StartFrame);
        Assert.Equal(300, restored.Loads[0].EndFrame);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"start_frame\": 1}")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"start_frame\": 1, \"end_frame\": 2, \"framerate\": true, \"loads\": []}")]
    public void CorruptFiles_ThrowExactMessage(string json)
    {
        var ex = Assert.Throws<ValidationException>(() => RunFileStore.Deserialize(json));
        Assert.Equal("The file provided is corrupted.", ex.Message);
    }

    [Fact]
    public void SaveAndLoad_Disk()
    {
        string path = Path.Combine(Path.GetTempPath(), $"crt-test-{Guid.NewGuid():N}.json");
        try
        {
            var session = new TimeSession { StartFrame = 1, EndFrame = 2, Framerate = 60m };
            RunFileStore.Save(session, path);
            Assert.False(string.IsNullOrEmpty(session.Meta.Created));  // stamped on save
            Assert.False(string.IsNullOrEmpty(session.Meta.Modified));

            var restored = RunFileStore.Load(path);
            Assert.Equal(1, restored.StartFrame);
            Assert.Equal(2, restored.EndFrame);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class SegmentMathTests
{
    [Fact]
    public void EmptySegments_ZeroBounds()
    {
        var (start, end, gaps) = SegmentMath.ToRunBoundsAndGaps(new List<Segment>());
        Assert.Equal(0, start);
        Assert.Equal(0, end);
        Assert.Empty(gaps);
    }

    [Fact]
    public void SingleSegment_NoGaps()
    {
        var (start, end, gaps) = SegmentMath.ToRunBoundsAndGaps(new List<Segment> { new(50, 150) });
        Assert.Equal(50, start);
        Assert.Equal(150, end);
        Assert.Empty(gaps);
    }

    [Fact]
    public void UnsortedSegments_GapsBetweenSortedNeighbors()
    {
        var segments = new List<Segment> { new(550, 600), new(100, 200), new(300, 500) };
        var (start, end, gaps) = SegmentMath.ToRunBoundsAndGaps(segments);
        Assert.Equal(100, start);
        Assert.Equal(600, end);
        Assert.Equal(2, gaps.Count);
        Assert.Equal((200, 300), (gaps[0].StartFrame, gaps[0].EndFrame));
        Assert.Equal((500, 550), (gaps[1].StartFrame, gaps[1].EndFrame));
        // Invariant: span − gaps == Σ segment lengths (non-overlapping segments).
        int span = end - start;
        int gapTotal = gaps.Sum(g => g.Length);
        Assert.Equal(segments.Sum(s => s.Length), span - gapTotal);
    }

    [Fact]
    public void AdjacentSegments_NoGap()
    {
        var (_, _, gaps) = SegmentMath.ToRunBoundsAndGaps(
            new List<Segment> { new(0, 100), new(100, 200) });
        Assert.Empty(gaps);
    }
}

public class RunFileVideoBindingTests
{
    [Fact]
    public void VideoPathRoundTrips()
    {
        var session = new TimeSession { StartFrame = 0, EndFrame = 600 };
        session.Meta.VideoPath = @"C:\runs\attempt.mp4";
        var reloaded = RunFileStore.Deserialize(RunFileStore.Serialize(session));
        Assert.Equal(@"C:\runs\attempt.mp4", reloaded.Meta.VideoPath);
    }

    [Fact]
    public void MissingVideoPathIsEmptyNotNull()
    {
        var reloaded = RunFileStore.Deserialize(
            "{\"start_frame\": 0, \"end_frame\": 60, \"framerate\": \"60\", \"loads\": []}");
        Assert.Equal("", reloaded.Meta.VideoPath);
    }

    // Segment mode is now the default for new sessions. A Python file has no
    // "mode" key, and its start/end/loads only mean anything in classic mode,
    // so it must not pick up the new default.
    [Fact]
    public void LegacyPythonFileStillOpensInClassicMode()
    {
        var reloaded = RunFileStore.Deserialize(
            "{\"start_frame\": 100, \"end_frame\": 700, \"framerate\": \"60\", " +
            "\"loads\": [[200, 300]]}");
        Assert.Equal(TimingMode.Loads, reloaded.Mode);
        Assert.Equal(100, reloaded.StartFrame);
        Assert.Equal(700, reloaded.EndFrame);
        Assert.Single(reloaded.Loads);
        Assert.Equal(500, reloaded.LengthWithoutLoads);
    }
}
