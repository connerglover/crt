namespace CRT.Core.Models;

/// <summary>A timed segment of a run, expressed as an absolute frame range.</summary>
public sealed class Segment
{
    public Segment() { }

    public Segment(int startFrame, int endFrame)
    {
        StartFrame = startFrame;
        EndFrame = endFrame;
    }

    public int StartFrame { get; set; }

    public int EndFrame { get; set; }

    /// <summary>The length of the segment in frames.</summary>
    public int Length => EndFrame - StartFrame;

    public Segment Clone() => new(StartFrame, EndFrame);
}
