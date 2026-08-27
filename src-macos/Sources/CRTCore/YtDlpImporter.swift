import Foundation

/// YouTube import via yt-dlp (spec §9.1) plus URL id parsing and progress
/// parsing helpers.
public struct YtDlpImporter: Sendable {
    public init() {}

    /// Extracts a YouTube video id from watch/shorts/youtu.be URLs.
    public static func youtubeID(from urlString: String) -> String? {
        guard let components = URLComponents(string: urlString.trimmingCharacters(in: .whitespacesAndNewlines)),
              let host = components.host?.lowercased() else {
            return nil
        }

        func cleanID(_ raw: String) -> String? {
            let id = raw.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
            guard !id.isEmpty else { return nil }
            let allowed = CharacterSet(charactersIn: "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_")
            guard id.unicodeScalars.allSatisfy({ allowed.contains($0) }) else { return nil }
            return id
        }

        let pathParts = components.path.split(separator: "/").map(String.init)

        if host == "youtu.be" || host.hasSuffix(".youtu.be") {
            return pathParts.first.flatMap(cleanID)
        }

        guard host == "youtube.com" || host.hasSuffix(".youtube.com") else {
            return nil
        }

        if let index = pathParts.firstIndex(of: "shorts"), index + 1 < pathParts.count {
            return cleanID(pathParts[index + 1])
        }
        if let index = pathParts.firstIndex(of: "live"), index + 1 < pathParts.count {
            return cleanID(pathParts[index + 1])
        }
        if pathParts.first == "watch" {
            let value = components.queryItems?.first(where: { $0.name == "v" })?.value
            return value.flatMap(cleanID)
        }
        return nil
    }

    public static func isYouTubeURL(_ urlString: String) -> Bool {
        youtubeID(from: urlString) != nil
    }

    /// Parses "[download]  42.3%" progress lines from yt-dlp stdout.
    /// Returns a fraction in [0, 1].
    public static func parseProgressLine(_ line: String) -> Double? {
        guard line.hasPrefix("[download]") else { return nil }
        for token in line.split(separator: " ") {
            if token.hasSuffix("%"), let value = Double(token.dropLast()) {
                return min(max(value / 100.0, 0), 1)
            }
        }
        return nil
    }

    /// The download command from spec §9.1 (`--newline` added so progress is
    /// emitted line-by-line).
    public static func downloadArguments(url: String, cacheDir: URL) -> [String] {
        [
            "-f", "bv*[height<=1080][ext=mp4]+ba[ext=m4a]/b[height<=1080][ext=mp4]/b",
            "--merge-output-format", "mp4",
            "--newline",
            "-o", cacheDir.path + "/%(id)s.%(ext)s",
            url,
        ]
    }

    /// Returns the cached file for a video id if one exists.
    public static func cachedFile(id: String, cacheDir: URL) -> URL? {
        let expected = cacheDir.appendingPathComponent("\(id).mp4")
        if FileManager.default.fileExists(atPath: expected.path) {
            return expected
        }
        let contents = (try? FileManager.default.contentsOfDirectory(
            at: cacheDir, includingPropertiesForKeys: nil)) ?? []
        return contents.first { $0.deletingPathExtension().lastPathComponent == id }
    }

    /// Downloads a YouTube video into the cache dir, re-using an existing
    /// cached file for the same id. Returns the local file URL.
    public func download(
        url: String,
        id: String,
        ytDlp: URL,
        cacheDir: URL,
        runner: ProcessRunner,
        progress: @escaping @Sendable (Double) -> Void
    ) async throws -> URL {
        if let cached = YtDlpImporter.cachedFile(id: id, cacheDir: cacheDir) {
            progress(1.0)
            return cached
        }

        try FileManager.default.createDirectory(at: cacheDir, withIntermediateDirectories: true)

        let result = try await runner.run(
            executable: ytDlp,
            arguments: YtDlpImporter.downloadArguments(url: url, cacheDir: cacheDir),
            onStdoutLine: { line in
                if let fraction = YtDlpImporter.parseProgressLine(line) {
                    progress(fraction)
                }
            }
        )

        if runner.isCancelled {
            throw CRTError.message("The download was cancelled.")
        }
        guard result.status == 0 else {
            let tail = result.stderr.split(separator: "\n").suffix(3).joined(separator: "\n")
            throw CRTError.message("yt-dlp failed:\n\(tail)")
        }
        guard let file = YtDlpImporter.cachedFile(id: id, cacheDir: cacheDir) else {
            throw CRTError.message("yt-dlp finished but the downloaded file was not found.")
        }
        progress(1.0)
        return file
    }

    /// Fetches `yt-dlp -j` metadata (used for the innertube fallback in
    /// `InnertubeClient.parseYtDlpInfo`).
    public func fetchInfoJSON(url: String, ytDlp: URL) async throws -> Data {
        let result = try await Subprocess.run(
            executable: ytDlp,
            arguments: ["-j", "--no-warnings", url]
        )
        guard result.status == 0 else {
            throw CRTError.message("yt-dlp -j failed: \(result.stderr)")
        }
        return Data(result.stdout.utf8)
    }
}
