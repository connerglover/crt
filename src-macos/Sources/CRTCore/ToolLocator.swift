import Foundation

/// The external tools CRT can locate and download at runtime (spec §8).
public enum ExternalTool: String, CaseIterable, Sendable {
    case ffmpeg
    case ffprobe
    case ytDlp

    public var binaryName: String {
        switch self {
        case .ffmpeg: return "ffmpeg"
        case .ffprobe: return "ffprobe"
        case .ytDlp: return "yt-dlp"
        }
    }

    public var displayName: String {
        switch self {
        case .ffmpeg: return "ffmpeg"
        case .ffprobe: return "ffprobe"
        case .ytDlp: return "yt-dlp"
        }
    }

    /// Rough download size shown in the consent prompt.
    public var approximateSize: String {
        switch self {
        case .ffmpeg: return "~40 MB"
        case .ffprobe: return "~40 MB"
        case .ytDlp: return "~35 MB"
        }
    }

    /// macOS download source (spec §8): evermeet.cx static builds for
    /// ffmpeg/ffprobe (zip), GitHub latest release binary for yt-dlp.
    public var downloadURLString: String {
        switch self {
        case .ffmpeg: return "https://evermeet.cx/ffmpeg/getrelease/zip"
        case .ffprobe: return "https://evermeet.cx/ffmpeg/getrelease/ffprobe/zip"
        case .ytDlp: return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos"
        }
    }

    var isZip: Bool {
        switch self {
        case .ffmpeg, .ffprobe: return true
        case .ytDlp: return false
        }
    }
}

/// Locates external tool binaries: explicit settings path → tools dir →
/// PATH (+ common Homebrew locations, since GUI apps don't inherit a shell
/// PATH).
public struct ToolLocator: Sendable {
    public let configDir: URL
    public var ffmpegOverride: String
    public var ytDlpOverride: String

    public init(configDir: URL, ffmpegOverride: String = "", ytDlpOverride: String = "") {
        self.configDir = configDir
        self.ffmpegOverride = ffmpegOverride
        self.ytDlpOverride = ytDlpOverride
    }

    public var toolsDir: URL {
        configDir.appendingPathComponent("tools", isDirectory: true)
    }

    private func isExecutable(_ path: String) -> Bool {
        var isDirectory: ObjCBool = false
        let fm = FileManager.default
        guard fm.fileExists(atPath: path, isDirectory: &isDirectory), !isDirectory.boolValue else {
            return false
        }
        return fm.isExecutableFile(atPath: path)
    }

    public func locate(_ tool: ExternalTool) -> URL? {
        // 1. Explicit path from settings.
        let override: String
        switch tool {
        case .ffmpeg:
            override = ffmpegOverride
        case .ffprobe:
            // ffprobe reuses the ffmpeg override's directory (sibling binary).
            if !ffmpegOverride.isEmpty {
                let sibling = URL(fileURLWithPath: ffmpegOverride)
                    .deletingLastPathComponent()
                    .appendingPathComponent("ffprobe").path
                override = isExecutable(sibling) ? sibling : ""
            } else {
                override = ""
            }
        case .ytDlp:
            override = ytDlpOverride
        }
        if !override.isEmpty, isExecutable(override) {
            return URL(fileURLWithPath: override)
        }

        // 2. Tools dir.
        let bundled = toolsDir.appendingPathComponent(tool.binaryName).path
        if isExecutable(bundled) {
            return URL(fileURLWithPath: bundled)
        }

        // 3. PATH + common locations.
        var directories = (ProcessInfo.processInfo.environment["PATH"] ?? "")
            .split(separator: ":")
            .map(String.init)
        directories.append(contentsOf: ["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin"])
        for directory in directories {
            let candidate = directory.hasSuffix("/")
                ? directory + tool.binaryName
                : directory + "/" + tool.binaryName
            if isExecutable(candidate) {
                return URL(fileURLWithPath: candidate)
            }
        }
        return nil
    }
}

/// Downloads a missing tool into the tools dir with progress (spec §8).
public struct ToolDownloader: Sendable {
    public let toolsDir: URL

    public init(toolsDir: URL) {
        self.toolsDir = toolsDir
    }

    /// Downloads (and unzips when needed) the tool. Returns the binary URL.
    public func download(_ tool: ExternalTool,
                         progress: @escaping @Sendable (Double) -> Void) async throws -> URL {
        try FileManager.default.createDirectory(at: toolsDir, withIntermediateDirectories: true)

        guard let sourceURL = URL(string: tool.downloadURLString) else {
            throw CRTError.message("Invalid download URL for \(tool.displayName).")
        }

        let destination = toolsDir.appendingPathComponent(tool.binaryName)

        if tool.isZip {
            let zipURL = toolsDir.appendingPathComponent("\(tool.binaryName)-download.zip")
            try await streamDownload(from: sourceURL, to: zipURL, progress: progress)
            defer { try? FileManager.default.removeItem(at: zipURL) }

            // The evermeet zip contains the bare binary at its root.
            let unzip = URL(fileURLWithPath: "/usr/bin/unzip")
            let result = try await Subprocess.run(
                executable: unzip,
                arguments: ["-o", zipURL.path, "-d", toolsDir.path]
            )
            guard result.status == 0 else {
                throw CRTError.message("Could not unpack \(tool.displayName): \(result.stderr)")
            }
        } else {
            try await streamDownload(from: sourceURL, to: destination, progress: progress)
        }

        guard FileManager.default.fileExists(atPath: destination.path) else {
            throw CRTError.message("The \(tool.displayName) download did not produce \(destination.lastPathComponent).")
        }

        try makeExecutable(destination)
        await removeQuarantine(destination)
        return destination
    }

    private func makeExecutable(_ url: URL) throws {
        try FileManager.default.setAttributes(
            [.posixPermissions: NSNumber(value: Int16(0o755))],
            ofItemAtPath: url.path
        )
    }

    private func removeQuarantine(_ url: URL) async {
        let xattr = URL(fileURLWithPath: "/usr/bin/xattr")
        _ = try? await Subprocess.run(
            executable: xattr,
            arguments: ["-d", "com.apple.quarantine", url.path]
        )
    }

    /// Streams a URL to disk, reporting progress in [0, 1] when the expected
    /// length is known. Cancelling the surrounding task cancels the transfer.
    public func streamDownload(from source: URL, to destination: URL,
                        progress: @escaping @Sendable (Double) -> Void) async throws {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 30
        configuration.timeoutIntervalForResource = 3600

        var request = URLRequest(url: source)
        request.setValue(CRTVersion.userAgent, forHTTPHeaderField: "User-Agent")

        let coordinator = DownloadCoordinator(destination: destination, progress: progress)
        try await coordinator.download(request: request, configuration: configuration)
    }
}

/// Drives one `URLSessionDownloadTask`: progress callbacks while it runs, then
/// the finished temp file is moved into place.
///
/// URLSession writes the body to disk in large chunks itself. Consuming a
/// `URLSession.AsyncBytes` sequence instead costs one async-iterator step (and
/// previously one `Task.checkCancellation()`) per *byte* — tens of millions of
/// them for a ~40 MB tool and hundreds of millions for a direct-URL video
/// import (spec §9.1), which is what this replaces.
private final class DownloadCoordinator: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    private let destination: URL
    private let progress: @Sendable (Double) -> Void
    private let lock = NSLock()
    private var continuation: CheckedContinuation<Void, Error>?
    private var task: URLSessionDownloadTask?

    init(destination: URL, progress: @escaping @Sendable (Double) -> Void) {
        self.destination = destination
        self.progress = progress
    }

    /// Synchronous on purpose: taking an `NSLock` inside an async function is
    /// unsupported (a hard error in Swift 6), so the critical section lives in
    /// a non-async helper where no suspension can occur.
    private func setTask(_ value: URLSessionDownloadTask?) {
        lock.lock()
        defer { lock.unlock() }
        task = value
    }

    func download(request: URLRequest, configuration: URLSessionConfiguration) async throws {
        let session = URLSession(configuration: configuration, delegate: self, delegateQueue: nil)
        defer { session.finishTasksAndInvalidate() }

        let downloadTask = session.downloadTask(with: request)
        setTask(downloadTask)

        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
                self.lock.lock()
                self.continuation = continuation
                self.lock.unlock()
                downloadTask.resume()
            }
        } onCancel: {
            self.cancel()
        }

        progress(1.0)
    }

    private func cancel() {
        lock.lock()
        let running = task
        lock.unlock()
        running?.cancel()
    }

    /// Resumes the waiter exactly once; later delegate callbacks are no-ops.
    private func finish(_ result: Result<Void, Error>) {
        lock.lock()
        let waiting = continuation
        continuation = nil
        lock.unlock()
        waiting?.resume(with: result)
    }

    // MARK: - URLSessionDownloadDelegate

    func urlSession(_ session: URLSession,
                    downloadTask: URLSessionDownloadTask,
                    didWriteData bytesWritten: Int64,
                    totalBytesWritten: Int64,
                    totalBytesExpectedToWrite: Int64) {
        guard totalBytesExpectedToWrite > 0 else { return }
        progress(min(1.0, Double(totalBytesWritten) / Double(totalBytesExpectedToWrite)))
    }

    func urlSession(_ session: URLSession,
                    downloadTask: URLSessionDownloadTask,
                    didFinishDownloadingTo location: URL) {
        // `location` is deleted as soon as this returns — move it now.
        if let http = downloadTask.response as? HTTPURLResponse,
           !(200..<300).contains(http.statusCode) {
            finish(.failure(CRTError.message("Download failed (HTTP \(http.statusCode)).")))
            return
        }
        do {
            let manager = FileManager.default
            try manager.createDirectory(at: destination.deletingLastPathComponent(),
                                        withIntermediateDirectories: true)
            if manager.fileExists(atPath: destination.path) {
                try manager.removeItem(at: destination)
            }
            try manager.moveItem(at: location, to: destination)
            finish(.success(()))
        } catch {
            finish(.failure(CRTError.message("Could not save the download: \(error.localizedDescription)")))
        }
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        guard let error else {
            // Success was already reported from didFinishDownloadingTo.
            finish(.success(()))
            return
        }
        if (error as? URLError)?.code == .cancelled {
            finish(.failure(CancellationError()))
        } else {
            finish(.failure(CRTError.message("Download failed: \(error.localizedDescription)")))
        }
    }
}
