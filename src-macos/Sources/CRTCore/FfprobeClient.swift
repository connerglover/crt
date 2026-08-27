import Foundation

/// Result of probing a video with ffprobe (spec §9.1).
public struct ProbeResult: Sendable, Equatable {
    /// Framerate rounded to 3 decimals (e.g. 30000/1001 → 29.97).
    public var fps: Decimal
    /// The raw rational, e.g. "30000/1001".
    public var fpsRational: String
    public var durationSeconds: Decimal
    public var width: Int
    public var height: Int

    public init(fps: Decimal, fpsRational: String, durationSeconds: Decimal, width: Int, height: Int) {
        self.fps = fps
        self.fpsRational = fpsRational
        self.durationSeconds = durationSeconds
        self.width = width
        self.height = height
    }
}

public struct FfprobeClient: Sendable {
    public init() {}

    public func probe(file: URL, ffprobe: URL) async throws -> ProbeResult {
        let result = try await Subprocess.run(
            executable: ffprobe,
            arguments: [
                "-v", "error",
                "-print_format", "json",
                "-show_streams",
                "-show_format",
                file.path,
            ]
        )
        guard result.status == 0 else {
            throw CRTError.message("ffprobe failed: \(result.stderr)")
        }
        return try FfprobeClient.parse(Data(result.stdout.utf8))
    }

    /// Parses `ffprobe -print_format json -show_streams -show_format` output.
    public static func parse(_ data: Data) throws -> ProbeResult {
        guard
            let object = try? JSONSerialization.jsonObject(with: data),
            let dict = object as? [String: Any],
            let streams = dict["streams"] as? [[String: Any]]
        else {
            throw CRTError.message("ffprobe returned an unreadable response.")
        }

        guard let video = streams.first(where: { ($0["codec_type"] as? String) == "video" }) else {
            throw CRTError.message("The file has no video stream.")
        }

        var rational = (video["avg_frame_rate"] as? String) ?? "0/0"
        var fps = fpsFromRational(rational)
        if fps == 0 {
            rational = (video["r_frame_rate"] as? String) ?? rational
            fps = fpsFromRational(rational)
        }

        var duration = Decimal(0)
        if let format = dict["format"] as? [String: Any],
           let durationText = format["duration"] as? String,
           let value = Decimal(string: durationText) {
            duration = value
        } else if let durationText = video["duration"] as? String,
                  let value = Decimal(string: durationText) {
            duration = value
        }

        let width = (video["width"] as? NSNumber)?.intValue ?? 0
        let height = (video["height"] as? NSNumber)?.intValue ?? 0

        return ProbeResult(
            fps: fps,
            fpsRational: rational,
            durationSeconds: duration,
            width: width,
            height: height
        )
    }

    /// "30000/1001" → 29.97 (rounded to 3 decimals); plain numbers accepted.
    public static func fpsFromRational(_ text: String) -> Decimal {
        let parts = text.split(separator: "/")
        if parts.count == 2 {
            guard
                let numerator = Decimal(string: String(parts[0])),
                let denominator = Decimal(string: String(parts[1])),
                denominator != 0
            else {
                return Decimal(0)
            }
            return TimeFormatter.rounded(numerator / denominator, scale: 3)
        }
        return Decimal(string: text) ?? Decimal(0)
    }
}
