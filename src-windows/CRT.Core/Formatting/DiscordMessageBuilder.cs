using System.Text;
using CRT.Core.Models;

namespace CRT.Core.Formatting;

/// <summary>
/// Builds the Discord-friendly code block — ported from <c>_discord_message</c>
/// in <c>src/crt/app/app.py</c>, plus the segment-mode variant.
/// </summary>
public static class DiscordMessageBuilder
{
    public static string Build(TimeSession session)
    {
        var lines = new List<string>
        {
            $"Time: {TimeFormatter.FormatIso(session.PrimarySeconds)}",
            $"Time (with loads): {TimeFormatter.FormatIso(session.SecondarySeconds)}",
        };

        if (session.Mode == TimingMode.Segments)
        {
            if (session.Segments.Count > 0)
            {
                lines.Add("");
                lines.Add($"Segments ({session.Segments.Count}):");
                int index = 1;
                foreach (var segment in session.Segments)
                {
                    lines.Add(
                        $"{index}. {FrameTime(session, segment.StartFrame)} - " +
                        $"{FrameTime(session, segment.EndFrame)} ({FrameTime(session, segment.Length)})");
                    index++;
                }
            }
        }
        else if (session.Loads.Count > 0)
        {
            lines.Add("");
            lines.Add($"Loads ({session.Loads.Count}):");
            int index = 1;
            foreach (var load in session.Loads)
            {
                lines.Add(
                    $"{index}. {FrameTime(session, load.StartFrame)} - " +
                    $"{FrameTime(session, load.EndFrame)} ({FrameTime(session, load.Length)})");
                index++;
            }
        }

        return "```\n" + string.Join("\n", lines) + "\n```";
    }

    private static string FrameTime(TimeSession session, int frames) =>
        TimeFormatter.FormatFrameTime(frames, session.Framerate, session.Precision);
}
