import Foundation

public struct ProcessResult: Sendable {
    public let status: Int32
    public let stdout: String
    public let stderr: String

    public init(status: Int32, stdout: String, stderr: String) {
        self.status = status
        self.stdout = stdout
        self.stderr = stderr
    }
}

/// Runs an external tool as a subprocess: argument arrays (never a shell),
/// stdout/stderr captured and streamed line-by-line, cancellable.
/// One runner runs one process.
public final class ProcessRunner: @unchecked Sendable {
    private let process = Process()
    private let lock = NSLock()
    private var started = false
    private var cancelled = false

    public init() {}

    public var isCancelled: Bool {
        lock.lock()
        defer { lock.unlock() }
        return cancelled
    }

    /// Terminates the running process (safe to call from any thread/task).
    public func cancel() {
        lock.lock()
        cancelled = true
        let shouldTerminate = started && process.isRunning
        lock.unlock()
        if shouldTerminate {
            process.terminate()
        }
    }

    /// Runs to completion. `onStdoutLine`/`onStderrLine` are invoked from a
    /// background task for every output line (\n, \r and \r\n all terminate
    /// lines, which covers ffmpeg/yt-dlp progress output).
    public func run(
        executable: URL,
        arguments: [String],
        currentDirectory: URL? = nil,
        onStdoutLine: (@Sendable (String) -> Void)? = nil,
        onStderrLine: (@Sendable (String) -> Void)? = nil
    ) async throws -> ProcessResult {
        let stdoutPipe = Pipe()
        let stderrPipe = Pipe()

        try launch(
            executable: executable,
            arguments: arguments,
            currentDirectory: currentDirectory,
            stdoutPipe: stdoutPipe,
            stderrPipe: stderrPipe
        )

        return await withTaskCancellationHandler {
            async let stdoutText = ProcessRunner.collect(stdoutPipe.fileHandleForReading, onLine: onStdoutLine)
            async let stderrText = ProcessRunner.collect(stderrPipe.fileHandleForReading, onLine: onStderrLine)

            let status: Int32 = await withCheckedContinuation { continuation in
                let proc = self.process
                DispatchQueue.global(qos: .userInitiated).async {
                    proc.waitUntilExit()
                    continuation.resume(returning: proc.terminationStatus)
                }
            }

            let stdout = await stdoutText
            let stderr = await stderrText
            return ProcessResult(status: status, stdout: stdout, stderr: stderr)
        } onCancel: {
            self.cancel()
        }
    }

    /// Claims the runner and starts the process.
    ///
    /// Deliberately synchronous: taking an `NSLock` directly inside an async
    /// function is unsupported (the thread can change across a suspension, and
    /// it is a hard error in Swift 6), so the whole critical section lives here
    /// where no suspension is possible.
    ///
    /// Everything that touches `process` happens under the lock, and only once:
    /// re-launching a `Process` raises an uncatchable Objective-C exception
    /// rather than throwing, which would abort the app instead of surfacing a
    /// dialog (spec §6).
    private func launch(
        executable: URL,
        arguments: [String],
        currentDirectory: URL?,
        stdoutPipe: Pipe,
        stderrPipe: Pipe
    ) throws {
        lock.lock()
        defer { lock.unlock() }

        if started {
            throw CRTError.message("This process runner has already been used.")
        }
        if cancelled {
            throw CRTError.message("The operation was cancelled.")
        }

        process.executableURL = executable
        process.arguments = arguments
        process.standardOutput = stdoutPipe
        process.standardError = stderrPipe
        process.standardInput = FileHandle.nullDevice
        if let currentDirectory {
            process.currentDirectoryURL = currentDirectory
        }

        do {
            try process.run()
            started = true
        } catch {
            throw CRTError.message(
                "Could not start \(executable.lastPathComponent): \(error.localizedDescription)"
            )
        }
    }

    /// Lines are accumulated as raw bytes and decoded as UTF-8 once per line —
    /// appending `UnicodeScalar(byte)` would be Latin-1 decoding and would
    /// mangle every non-ASCII character coming out of ffmpeg/yt-dlp. Splitting
    /// on 0x0A/0x0D is safe for UTF-8: those bytes never appear inside a
    /// multi-byte sequence.
    private static func collect(_ handle: FileHandle, onLine: (@Sendable (String) -> Void)?) async -> String {
        var collected = ""
        var lineBytes: [UInt8] = []

        func flushLine() {
            let line = String(decoding: lineBytes, as: UTF8.self)
            collected += line + "\n"
            onLine?(line)
            lineBytes.removeAll(keepingCapacity: true)
        }

        do {
            var previousWasCR = false
            for try await byte in handle.bytes {
                switch byte {
                case 0x0A: // \n — collapse \r\n into a single line break
                    if previousWasCR {
                        previousWasCR = false
                    } else {
                        flushLine()
                    }
                case 0x0D: // \r (ffmpeg progress lines)
                    previousWasCR = true
                    flushLine()
                default:
                    previousWasCR = false
                    lineBytes.append(byte)
                }
            }
        } catch {
            // Reading errors just end collection.
        }
        if !lineBytes.isEmpty {
            flushLine()
        }
        return collected
    }
}

/// Convenience one-shot run.
public enum Subprocess {
    @discardableResult
    public static func run(executable: URL, arguments: [String]) async throws -> ProcessResult {
        try await ProcessRunner().run(executable: executable, arguments: arguments)
    }
}
