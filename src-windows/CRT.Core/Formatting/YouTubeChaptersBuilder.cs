using CRT.Core.Models;

namespace CRT.Core.Formatting;

/// <summary>
/// Builds a YouTube chapters list — ported from <c>_youtube_chapters</c> in
/// <c>src/crt/app/app.py</c>, plus the segment-mode variant. YouTube requires
/// the first chapter to start at 0:00.
/// </summary>
public static class YouTubeChaptersBuilder
{
    public static string Build(TimeSession session)
    {
        var lines = new List<string>();

        if (session.Mode == TimingMode.Segments)
        {
            lines.Add("0:00 Waiting");
            int index = 1;
            foreach (var segment in session.Segments.OrderBy(s => s.StartFrame))
            {
                lines.Add($"{Timestamp(session, segment.StartFrame)} Segment {index}");
                lines.Add($"{Timestamp(session, segment.EndFrame)} Waiting");
                index++;
            }
        }
        else
        {
            lines.Add("0:00 Gameplay");
            foreach (var load in session.Loads.OrderBy(l => l.StartFrame))
            {
                lines.Add($"{Timestamp(session, load.StartFrame)} Loading");
                lines.Add($"{Timestamp(session, load.EndFrame)} Gameplay");
            }
        }

        return string.Join("\n", lines);
    }

    private static string Timestamp(TimeSession session, int frame) =>
        TimeFormatter.FormatYouTubeTimestamp(frame, session.Framerate);
}
