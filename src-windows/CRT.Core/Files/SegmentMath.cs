using CRT.Core.Models;

namespace CRT.Core.Files;

/// <summary>
/// Segment ↔ loads-gap conversion used to keep segment-mode files readable by
/// the Python app (run bounds minus gaps == segment total).
/// </summary>
public static class SegmentMath
{
    /// <summary>
    /// Computes Python-compatible run bounds and "loads" (the gaps between
    /// segments) for a set of segments. Empty input → (0, 0, no gaps).
    /// </summary>
    public static (int StartFrame, int EndFrame, List<Load> Gaps) ToRunBoundsAndGaps(IReadOnlyList<Segment> segments)
    {
        // Unfilled 0-0 rows are placeholders, not coverage: including one would
        // anchor the run to frame zero and invent a gap spanning the whole run.
        var filled = segments.Where(s => s.Length > 0).ToList();
        if (filled.Count == 0)
        {
            return (0, 0, new List<Load>());
        }

        var sorted = filled.OrderBy(s => s.StartFrame).ThenBy(s => s.EndFrame).ToList();
        int startFrame = sorted[0].StartFrame;
        int endFrame = filled.Max(s => s.EndFrame);

        var gaps = new List<Load>();
        int coveredEnd = sorted[0].EndFrame;
        foreach (var segment in sorted.Skip(1))
        {
            if (segment.StartFrame > coveredEnd)
            {
                gaps.Add(new Load(coveredEnd, segment.StartFrame));
            }
            coveredEnd = Math.Max(coveredEnd, segment.EndFrame);
        }

        return (startFrame, endFrame, gaps);
    }
}
