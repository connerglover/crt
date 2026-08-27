import Foundation

/// Discord message and YouTube chapters builders.
/// Port of `App._discord_message` / `App._youtube_chapters` (spec §5) with
/// the segment-mode variants.
public enum CopyFormats {

    // MARK: - Discord message

    public static func discordMessage(session: TimeSession) -> String {
        let fps = session.framerate
        let precision = session.precision

        func frameTime(_ frame: Int) -> String {
            TimeFormatter.frameTime(frames: frame, framerate: fps, precision: precision)
        }

        var lines = [
            "Time: \(TimeFormatter.iso(session.displayWithoutLoads))",
            "Time (with loads): \(TimeFormatter.iso(session.displayWithLoads))",
        ]

        if session.mode == .segments {
            if !session.segments.isEmpty {
                lines.append("")
                lines.append("Segments (\(session.segments.count)):")
                for (offset, segment) in session.segments.enumerated() {
                    lines.append(
                        "\(offset + 1). \(frameTime(segment.startFrame)) - "
                        + "\(frameTime(segment.endFrame)) (\(frameTime(segment.length)))"
                    )
                }
            }
        } else {
            if !session.loads.isEmpty {
                lines.append("")
                lines.append("Loads (\(session.loads.count)):")
                for (offset, load) in session.loads.enumerated() {
                    lines.append(
                        "\(offset + 1). \(frameTime(load.startFrame)) - "
                        + "\(frameTime(load.endFrame)) (\(frameTime(load.length)))"
                    )
                }
            }
        }

        return "```\n" + lines.joined(separator: "\n") + "\n```"
    }

    // MARK: - YouTube chapters

    public static func youtubeChapters(session: TimeSession) -> String {
        let fps = session.framerate

        func timestamp(_ frame: Int) -> String {
            TimeFormatter.youtubeTimestamp(frame: frame, framerate: fps)
        }

        if session.mode == .segments {
            let sorted = session.segments.sorted { $0.startFrame < $1.startFrame }
            var lines = ["0:00 Waiting"]
            for (offset, segment) in sorted.enumerated() {
                lines.append("\(timestamp(segment.startFrame)) Segment \(offset + 1)")
                lines.append("\(timestamp(segment.endFrame)) Waiting")
            }
            return lines.joined(separator: "\n")
        }

        let sorted = session.loads.sorted { $0.startFrame < $1.startFrame }
        var lines = ["0:00 Gameplay"]
        for load in sorted {
            lines.append("\(timestamp(load.startFrame)) Loading")
            lines.append("\(timestamp(load.endFrame)) Gameplay")
        }
        return lines.joined(separator: "\n")
    }
}
