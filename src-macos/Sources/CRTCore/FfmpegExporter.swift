import Foundation

/// One export request (spec §9.3).
public struct ExportJob: Sendable {
    public var input: URL
    public var output: URL
    public var trimStart: Decimal
    public var trimEnd: Decimal
    public var filtergraph: String

    public init(input: URL, output: URL, trimStart: Decimal, trimEnd: Decimal, filtergraph: String) {
        self.input = input
        self.output = output
        self.trimStart = trimStart
        self.trimEnd = trimEnd
        self.filtergraph = filtergraph
    }
}

/// Runs the ffmpeg export with progress parsed from stderr `time=` lines.
public final class FfmpegExporter: @unchecked Sendable {
    private let lock = NSLock()
    private var runner: ProcessRunner?

    public init() {}

    /// The exact command shape from spec §9.3. `-ss` before `-i` for fast
    /// seek (fine because we re-encode); the -vf chain is one argument.
    public static func arguments(for job: ExportJob) -> [String] {
        [
            "-y",
            "-ss", TimeFormatter.string(TimeFormatter.rounded(job.trimStart, scale: 3)),
            "-to", TimeFormatter.string(TimeFormatter.rounded(job.trimEnd, scale: 3)),
            "-i", job.input.path,
            "-vf", job.filtergraph,
            "-c:v", "libx264",
            "-preset", "veryfast",
            "-crf", "18",
            "-c:a", "aac",
            "-movflags", "+faststart",
            job.output.path,
        ]
    }

    /// Parses an ffmpeg stderr progress chunk for `time=HH:MM:SS.cc`.
    public static func parseTimeSeconds(fromStderrLine line: String) -> Double? {
        guard let range = line.range(of: "time=") else { return nil }
        let tail = line[range.upperBound...]
        let token = tail.prefix(while: { $0 != " " })
        if token.hasPrefix("N/A") { return nil }

        let parts = token.split(separator: ":")
        guard parts.count == 3 else { return nil }
        guard
            let hours = Double(parts[0]),
            let minutes = Double(parts[1]),
            let seconds = Double(parts[2])
        else {
            return nil
        }
        return hours * 3600 + minutes * 60 + seconds
    }

    public func cancel() {
        lock.lock()
        let active = runner
        lock.unlock()
        active?.cancel()
    }

    /// Runs the export. `progress` receives a fraction in [0, 1].
    public func export(job: ExportJob, ffmpeg: URL,
                       progress: @escaping @Sendable (Double) -> Void) async throws {
        let newRunner = ProcessRunner()
        lock.lock()
        runner = newRunner
        lock.unlock()

        let totalSeconds = NSDecimalNumber(decimal: job.trimEnd - job.trimStart).doubleValue

        let result = try await newRunner.run(
            executable: ffmpeg,
            arguments: FfmpegExporter.arguments(for: job),
            onStderrLine: { line in
                guard totalSeconds > 0 else { return }
                if let seconds = FfmpegExporter.parseTimeSeconds(fromStderrLine: line) {
                    progress(min(max(seconds / totalSeconds, 0), 1))
                }
            }
        )

        lock.lock()
        runner = nil
        lock.unlock()

        if newRunner.isCancelled {
            try? FileManager.default.removeItem(at: job.output)
            throw CRTError.message("The export was cancelled.")
        }
        guard result.status == 0 else {
            let tail = result.stderr.split(separator: "\n").suffix(5).joined(separator: "\n")
            throw CRTError.message("ffmpeg failed:\n\(tail)")
        }
        progress(1.0)
    }
}
