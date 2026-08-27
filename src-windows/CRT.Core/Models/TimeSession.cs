namespace CRT.Core.Models;

/// <summary>
/// The timing model for a single run — a direct port of the Python <c>Time</c>
/// class plus the new segment mode. All time math uses <see cref="decimal"/>.
/// </summary>
public sealed class TimeSession
{
    public const int DefaultPrecision = 3;

    public int StartFrame { get; set; }

    public int EndFrame { get; set; }

    /// <summary>Frames per second. Decimal so 29.97 behaves exactly.</summary>
    public decimal Framerate { get; set; } = 60m;

    /// <summary>Decimal places for computed seconds.</summary>
    public int Precision { get; set; } = DefaultPrecision;

    public List<Load> Loads { get; set; } = new();

    public List<Segment> Segments { get; set; } = new();

    public TimingMode Mode { get; set; } = TimingMode.Loads;

    public RunMeta Meta { get; set; } = new();

    // ── Loads-mode computed values ─────────────────────────────────────────

    /// <summary>Total run length in frames, including loads.</summary>
    public int LengthWithLoads => EndFrame - StartFrame;

    /// <summary>Total run length in frames, excluding loads.</summary>
    public int LengthWithoutLoads => LengthWithLoads - Loads.Sum(l => l.Length);

    /// <summary>Average load length in frames (0 when there are no loads).</summary>
    public decimal AverageLoadLength =>
        Loads.Count == 0 ? 0m : (decimal)Loads.Sum(l => l.Length) / Loads.Count;

    /// <summary>Run time in seconds including loads. 0 when framerate is 0.</summary>
    public decimal WithLoads => FramesToSeconds(LengthWithLoads);

    /// <summary>Run time in seconds excluding loads. 0 when framerate is 0.</summary>
    public decimal WithoutLoads => FramesToSeconds(LengthWithoutLoads);

    // ── Segment-mode computed values ───────────────────────────────────────

    /// <summary>Sum of segment lengths in frames.</summary>
    public int SegmentTotalFrames => Segments.Sum(s => s.Length);

    /// <summary>
    /// Segments that actually cover time. "Add Segment" appends a blank row for
    /// the user to fill in, and an unfilled 0-0 row must not drag the run bounds
    /// down to frame zero while they are typing.
    /// </summary>
    public IEnumerable<Segment> FilledSegments => Segments.Where(s => s.Length > 0);

    /// <summary>Span from earliest segment start to latest segment end, in frames.</summary>
    public int FullRunFrames =>
        FilledSegments.Any()
            ? FilledSegments.Max(s => s.EndFrame) - FilledSegments.Min(s => s.StartFrame)
            : 0;

    /// <summary>Segment total in seconds (the primary display in segment mode).</summary>
    public decimal SegmentTotal => FramesToSeconds(SegmentTotalFrames);

    /// <summary>Full-run span in seconds (the secondary display in segment mode).</summary>
    public decimal FullRun => FramesToSeconds(FullRunFrames);

    // ── Mode-aware values used by the copy actions ─────────────────────────

    /// <summary>"Time" — without loads in loads mode, segment total in segment mode.</summary>
    public decimal PrimarySeconds => Mode == TimingMode.Segments ? SegmentTotal : WithoutLoads;

    /// <summary>"Time (with loads)" — with loads in loads mode, full-run span in segment mode.</summary>
    public decimal SecondarySeconds => Mode == TimingMode.Segments ? FullRun : WithLoads;

    /// <summary>Effective run start frame (min segment start in segment mode).</summary>
    public int EffectiveStartFrame =>
        Mode == TimingMode.Segments
            ? (Segments.Count == 0 ? 0 : Segments.Min(s => s.StartFrame))
            : StartFrame;

    /// <summary>Effective run end frame (max segment end in segment mode).</summary>
    public int EffectiveEndFrame =>
        Mode == TimingMode.Segments
            ? (Segments.Count == 0 ? 0 : Segments.Max(s => s.EndFrame))
            : EndFrame;

    /// <summary>Effective total frame length (full-run span in segment mode).</summary>
    public int EffectiveTotalFrames =>
        Mode == TimingMode.Segments ? FullRunFrames : LengthWithLoads;

    // ── Conversions ────────────────────────────────────────────────────────

    /// <summary>Converts a frame count to seconds at the session framerate, rounded to Precision. 0 when framerate is 0.</summary>
    public decimal FramesToSeconds(int frames)
    {
        if (Framerate == 0m)
        {
            return 0m;
        }
        return Math.Round(frames / Framerate, Precision, MidpointRounding.ToEven);
    }

    // ── Mutation (ported from Time.mutate / add_load / mutate_load / …) ────

    public void Mutate(int? startFrame = null, int? endFrame = null, decimal? framerate = null)
    {
        StartFrame = startFrame ?? StartFrame;
        EndFrame = endFrame ?? EndFrame;
        Framerate = framerate ?? Framerate;
    }

    /// <summary>Shared load/segment bounds validation — messages match the Python app.</summary>
    public static void ValidateRange(int startFrame, int endFrame)
    {
        if (startFrame == endFrame)
        {
            throw new ValidationException("The duration of the load is 0.000");
        }
        if (startFrame > endFrame)
        {
            throw new ValidationException("The load time ends before it starts.");
        }
    }

    public void AddLoad(int startFrame, int endFrame)
    {
        if (startFrame == 0 && endFrame == 0)
        {
            throw new ValidationException("You must provide an input for the loads");
        }
        ValidateRange(startFrame, endFrame);
        Loads.Add(new Load(startFrame, endFrame));
    }

    public void MutateLoad(int index, int? startFrame = null, int? endFrame = null)
    {
        var load = Loads[index];
        int newStart = startFrame ?? load.StartFrame;
        int newEnd = endFrame ?? load.EndFrame;
        ValidateRange(newStart, newEnd);
        load.StartFrame = newStart;
        load.EndFrame = newEnd;
    }

    public void DeleteLoad(int index) => Loads.RemoveAt(index);

    public void ClearLoads() => Loads.Clear();

    /// <summary>
    /// Appends an empty segment row, deliberately skipping validation: the user
    /// fills it in from the retimer page, and committing a field validates it
    /// then. Zero-length rows are excluded from the run bounds until filled.
    /// </summary>
    public void AddBlankSegment() => Segments.Add(new Segment(0, 0));

    public void AddSegment(int startFrame, int endFrame)
    {
        if (startFrame == 0 && endFrame == 0)
        {
            throw new ValidationException("You must provide an input for the loads");
        }
        ValidateRange(startFrame, endFrame);
        Segments.Add(new Segment(startFrame, endFrame));
    }

    public void MutateSegment(int index, int? startFrame = null, int? endFrame = null)
    {
        var segment = Segments[index];
        int newStart = startFrame ?? segment.StartFrame;
        int newEnd = endFrame ?? segment.EndFrame;
        ValidateRange(newStart, newEnd);
        segment.StartFrame = newStart;
        segment.EndFrame = newEnd;
    }

    public void DeleteSegment(int index) => Segments.RemoveAt(index);

    public void ClearSegments() => Segments.Clear();

    /// <summary>
    /// True when adding a load of the given length should trigger the
    /// "concerningly long load" confirmation (see <c>_add_loads</c> in the Python app).
    /// </summary>
    public bool IsConcerninglyLongLoad(int startFrame, int endFrame)
    {
        if (Loads.Count == 0)
        {
            return false;
        }
        return endFrame - startFrame > AverageLoadLength * 10;
    }

    /// <summary>Deep copy (used for undo/redo snapshots and autosave).</summary>
    public TimeSession Clone() => new()
    {
        StartFrame = StartFrame,
        EndFrame = EndFrame,
        Framerate = Framerate,
        Precision = Precision,
        Loads = Loads.Select(l => l.Clone()).ToList(),
        Segments = Segments.Select(s => s.Clone()).ToList(),
        Mode = Mode,
        Meta = Meta.Clone(),
    };
}
