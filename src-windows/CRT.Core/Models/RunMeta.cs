namespace CRT.Core.Models;

/// <summary>Optional metadata stored alongside a run file (ignored by the Python app).</summary>
public sealed class RunMeta
{
    public string Title { get; set; } = "";

    public string Game { get; set; } = "";

    public string Notes { get; set; } = "";

    /// <summary>ISO-8601 creation timestamp, set on first save.</summary>
    public string Created { get; set; } = "";

    /// <summary>ISO-8601 last-modified timestamp, refreshed on every save.</summary>
    public string Modified { get; set; } = "";

    public string VideoUrl { get; set; } = "";

    public RunMeta Clone() => new()
    {
        Title = Title,
        Game = Game,
        Notes = Notes,
        Created = Created,
        Modified = Modified,
        VideoUrl = VideoUrl,
    };
}
