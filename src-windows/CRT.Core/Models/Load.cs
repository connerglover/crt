namespace CRT.Core.Models;

/// <summary>A load (pause) inside a run, expressed as an absolute frame range.</summary>
public sealed class Load
{
    public Load() { }

    public Load(int startFrame, int endFrame)
    {
        StartFrame = startFrame;
        EndFrame = endFrame;
    }

    public int StartFrame { get; set; }

    public int EndFrame { get; set; }

    /// <summary>The length of the load in frames.</summary>
    public int Length => EndFrame - StartFrame;

    public Load Clone() => new(StartFrame, EndFrame);
}
