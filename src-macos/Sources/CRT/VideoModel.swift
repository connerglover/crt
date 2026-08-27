import AppKit
import AVFoundation
import AVKit
import Foundation
import Observation
import CRTCore

/// State for the Video Retimer workspace (spec §9). The video retimer and
/// the frame retimer are two views over the same `TimeSession` in AppModel.
@Observable
final class VideoModel {
    @ObservationIgnored weak var app: AppModel?

    let player = AVPlayer()
    var hasVideo = false
    var videoFile: URL?
    var currentSeconds: Double = 0
    var durationSeconds: Double = 0
    var videoWidth = 0
    var videoHeight = 0
    var isPlaying = false

    var importURLText = ""
    var importProgress: Double?
    var importStage: String?

    var exportProgress: Double?

    var pendingSegmentStart: Int?
    var pendingLoadStart: Int?

    @ObservationIgnored private var timeObserverToken: Any?
    @ObservationIgnored private var statusObservation: NSKeyValueObservation?
    @ObservationIgnored private var importRunner: ProcessRunner?
    @ObservationIgnored private var exporter: FfmpegExporter?
    @ObservationIgnored var importTask: Task<Void, Never>?
    /// True when ffprobe supplied the session framerate for the current file.
    @ObservationIgnored private var appliedFramerateFromProbe = false

    deinit {
        // Apple requires removing a periodic observer before the player goes
        // away.
        if let timeObserverToken {
            player.removeTimeObserver(timeObserverToken)
        }
        statusObservation?.invalidate()
    }

    /// Localization lookup (spec §13); falls back to the key when no app is
    /// attached (previews/tests).
    private func loc(_ key: String) -> String {
        app?.loc[key] ?? key
    }

    private func loc(_ key: String, _ english: String) -> String {
        app?.loc.text(key, english) ?? english
    }

    // MARK: - Derived

    var fps: Double {
        guard let app else { return 30 }
        let value = NSDecimalNumber(decimal: app.files.session.framerate).doubleValue
        return value > 0 ? value : 30
    }

    var currentFrame: Int {
        Int((currentSeconds * fps).rounded())
    }

    var currentTimeText: String {
        guard let app else { return "00.000" }
        return TimeFormatter.frameTime(
            frames: currentFrame,
            framerate: app.files.session.framerate,
            precision: app.files.session.precision
        )
    }

    func resetMarks() {
        pendingSegmentStart = nil
        pendingLoadStart = nil
    }

    // MARK: - Loading (spec §9.1)

    @MainActor func chooseLocalFile() {
        guard let url = Panels.openMovie(title: loc("Import Video")) else { return }
        Task { @MainActor in
            await self.loadLocalVideo(url: url)
        }
    }

    @MainActor func loadLocalVideo(url: URL) async {
        let item = AVPlayerItem(url: url)
        observeStatus(of: item)
        player.replaceCurrentItem(with: item)
        player.pause()
        isPlaying = false
        installTimeObserver()
        videoFile = url
        hasVideo = true
        currentSeconds = 0
        // Never carry the previous video's numbers over: they drive the
        // timeline range, the region overlay and the export trim window.
        durationSeconds = 0
        videoWidth = 0
        videoHeight = 0
        resetMarks()
        await probeAndApply(url: url)
    }

    /// Surfaces decode failures as a dialog instead of a black player with a
    /// success toast (spec §6), and announces the load once playback is ready.
    @MainActor private func observeStatus(of item: AVPlayerItem) {
        statusObservation?.invalidate()
        statusObservation = item.observe(\.status, options: [.new]) { [weak self] observed, _ in
            let ready = observed.status == .readyToPlay
            let failed = observed.status == .failed
            let failureMessage = observed.error?.localizedDescription
            Task { @MainActor in
                guard let self else { return }
                if ready {
                    let name = self.videoFile?.lastPathComponent ?? ""
                    self.app?.toast(self.loc("Video loaded {file}", "Video loaded: {file}")
                        .replacingOccurrences(of: "{file}", with: name))
                } else if failed {
                    self.hasVideo = false
                    let base = self.loc(
                        "Video Unplayable",
                        "The video could not be opened. Its format may not be playable on this Mac."
                    )
                    self.app?.showErrorMessage(
                        failureMessage.map { base + "\n\n" + $0 } ?? base
                    )
                }
            }
        }
    }

    /// Probe with ffprobe when available (exact fps/duration/size), with an
    /// AVFoundation fallback when the user declines the download.
    @MainActor func probeAndApply(url: URL) async {
        appliedFramerateFromProbe = false
        var probed = false
        // Spec §8: offer the download when the feature needs the tool —
        // without ffprobe the framerate is only ever approximate.
        if let app, let ffprobe = await app.ensureTool(.ffprobe) {
            if let result = try? await FfprobeClient().probe(file: url, ffprobe: ffprobe) {
                durationSeconds = NSDecimalNumber(decimal: result.durationSeconds).doubleValue
                if result.width > 0 { videoWidth = result.width }
                if result.height > 0 { videoHeight = result.height }
                if result.fps > 0 {
                    app.setFramerate(result.fps, announce: true)
                    appliedFramerateFromProbe = true
                }
                probed = true
            }
        }
        if !probed {
            await probeWithAVFoundation(url: url)
        }
    }

    @MainActor private func probeWithAVFoundation(url: URL) async {
        let asset = AVURLAsset(url: url)
        if let duration = try? await asset.load(.duration), duration.seconds.isFinite {
            durationSeconds = duration.seconds
        }
        guard let tracks = try? await asset.loadTracks(withMediaType: .video),
              let track = tracks.first else {
            return
        }
        if let size = try? await track.load(.naturalSize) {
            videoWidth = Int(size.width)
            videoHeight = Int(size.height)
        }
        if let nominal = try? await track.load(.nominalFrameRate), nominal > 0 {
            let text = String(format: "%.3f", Double(nominal))
            if let fpsDecimal = Decimal(string: text), fpsDecimal > 0 {
                app?.setFramerate(TimeFormatter.rounded(fpsDecimal, scale: 3), announce: true)
            }
        }
    }

    private func installTimeObserver() {
        if let token = timeObserverToken {
            player.removeTimeObserver(token)
            timeObserverToken = nil
        }
        let interval = CMTime(value: 1, timescale: 60)
        timeObserverToken = player.addPeriodicTimeObserver(forInterval: interval, queue: .main) { [weak self] time in
            Task { @MainActor in
                guard let self else { return }
                if time.seconds.isFinite {
                    self.currentSeconds = time.seconds
                }
                self.isPlaying = self.player.rate != 0
            }
        }
    }

    // MARK: - Transport (spec §9.2)

    @MainActor func togglePlay() {
        guard hasVideo else { return }
        if player.rate != 0 {
            player.pause()
            isPlaying = false
        } else {
            player.play()
            isPlaying = true
        }
    }

    /// Steps exactly `count` frames (pause first, then step). `step(byCount:)`
    /// does nothing unless the item is ready and can step, so fall back to a
    /// frame-snapped seek in that case.
    @MainActor func stepFrames(_ count: Int) {
        guard hasVideo, let item = player.currentItem else { return }
        player.pause()
        isPlaying = false
        let canStep = count < 0 ? item.canStepBackward : item.canStepForward
        if item.status == .readyToPlay && canStep {
            item.step(byCount: count)
        } else {
            seek(to: Double(currentFrame + count) / fps)
        }
    }

    @MainActor func jumpSeconds(_ delta: Double) {
        guard hasVideo else { return }
        let frameDelta = Int((delta * fps).rounded())
        seek(to: Double(currentFrame + frameDelta) / fps)
    }

    /// Seeks to the frame containing `seconds`. The target lands a quarter of
    /// a frame inside that frame so AVPlayer's floor() and the readout's
    /// `round(position × fps)` (spec §9.2) agree on the frame number.
    @MainActor func seek(to seconds: Double) {
        guard hasVideo else { return }
        let framerate = fps
        var frame = Int((seconds * framerate).rounded())
        frame = max(0, frame)
        if durationSeconds > 0 {
            frame = min(frame, max(0, Int(durationSeconds * framerate) - 1))
        }
        let target = (Double(frame) + 0.25) / framerate
        player.seek(to: CMTime(seconds: target, preferredTimescale: 600),
                    toleranceBefore: .zero, toleranceAfter: .zero)
        currentSeconds = target
    }

    // MARK: - Marking (spec §9.2)

    @MainActor func markPrimaryStart() {
        guard let app, hasVideo else { return }
        let frame = currentFrame
        if app.files.session.mode == .segments {
            pendingSegmentStart = frame
            app.toast(frameToast("Segment Start Marked", "Segment start marked at frame {frame}", frame))
        } else {
            app.mutateSession { $0.startFrame = frame }
            app.startText = String(frame)
            app.toast(frameToast("Run Start Set", "Run start set to frame {frame}", frame))
        }
    }

    @MainActor func markPrimaryEnd() {
        guard let app, hasVideo else { return }
        let frame = currentFrame
        if app.files.session.mode == .segments {
            guard let start = pendingSegmentStart else {
                app.toast(loc("Mark Segment Start First", "Mark a segment start first ( [ )"))
                return
            }
            do {
                try app.mutateSessionThrowing { try $0.addSegment(startFrame: start, endFrame: frame) }
                pendingSegmentStart = nil
                app.toast(loc("Segment added successfully."))
            } catch {
                app.showError(error)
            }
        } else {
            app.mutateSession { $0.endFrame = frame }
            app.endText = String(frame)
            app.toast(frameToast("Run End Set", "Run end set to frame {frame}", frame))
        }
    }

    @MainActor func markLoadStart() {
        guard let app, hasVideo, app.files.session.mode == .loads else { return }
        pendingLoadStart = currentFrame
        app.toast(frameToast("Load Start Marked", "Load start marked at frame {frame}", currentFrame))
    }

    @MainActor func markLoadEnd() {
        guard let app, hasVideo, app.files.session.mode == .loads else { return }
        guard let start = pendingLoadStart else {
            app.toast(loc("Mark Load Start First", "Mark a load start first (L)"))
            return
        }
        do {
            try app.mutateSessionThrowing { try $0.addLoad(startFrame: start, endFrame: currentFrame) }
            pendingLoadStart = nil
            app.toast(loc("Load added successfully."))
        } catch {
            app.showError(error)
        }
    }

    private func frameToast(_ key: String, _ english: String, _ frame: Int) -> String {
        loc(key, english).replacingOccurrences(of: "{frame}", with: String(frame))
    }

    // MARK: - Import (spec §9.1)

    @MainActor func startImport() {
        let text = importURLText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return }
        importTask?.cancel()
        importTask = Task { @MainActor in
            if YtDlpImporter.isYouTubeURL(text) {
                await self.importYouTube(urlString: text)
            } else if text.lowercased().hasPrefix("http") {
                await self.importDirectURL(text)
            } else {
                self.app?.showErrorMessage(self.loc(
                    "Invalid Import URL",
                    "Enter a YouTube or direct video URL, or choose a local file."
                ))
            }
            self.importTask = nil
        }
    }

    @MainActor func cancelImport() {
        importRunner?.cancel()
        importTask?.cancel()
        importProgress = nil
        importStage = nil
    }

    var cacheDir: URL {
        (app?.settings.configDir ?? SettingsService.defaultConfigDir())
            .appendingPathComponent("video-cache", isDirectory: true)
    }

    @MainActor func importYouTube(urlString: String) async {
        guard let app, let id = YtDlpImporter.youtubeID(from: urlString) else { return }
        guard let ytDlp = await app.ensureTool(.ytDlp) else { return }

        importStage = loc("Downloading", "Downloading video…")
        importProgress = 0
        let runner = ProcessRunner()
        importRunner = runner
        do {
            let file = try await YtDlpImporter().download(
                url: urlString,
                id: id,
                ytDlp: ytDlp,
                cacheDir: cacheDir,
                runner: runner
            ) { fraction in
                Task { @MainActor in
                    self.importProgress = fraction
                }
            }
            importProgress = nil
            importStage = nil
            app.mutateSession { $0.meta.videoURL = urlString }
            await loadLocalVideo(url: file)
            // Spec §9.1: yt-dlp's metadata is the fps source when ffprobe is
            // not available.
            if !appliedFramerateFromProbe,
               let data = try? await YtDlpImporter().fetchInfoJSON(url: urlString, ytDlp: ytDlp),
               let detected = YtDlpImporter.parseTopLevelFps(data), detected > 0 {
                app.setFramerate(detected, announce: true)
            }
        } catch {
            importProgress = nil
            importStage = nil
            if !runner.isCancelled {
                app.showError(error)
            }
        }
        importRunner = nil
    }

    @MainActor func importDirectURL(_ urlString: String) async {
        guard let app, let url = URL(string: urlString) else { return }
        try? FileManager.default.createDirectory(at: cacheDir, withIntermediateDirectories: true)

        let name = url.lastPathComponent.isEmpty ? "video.mp4" : url.lastPathComponent
        let destination = cacheDir.appendingPathComponent("direct-\(stableHash(urlString))-\(name)")

        importStage = loc("Downloading", "Downloading video…")
        importProgress = 0
        do {
            if !FileManager.default.fileExists(atPath: destination.path) {
                let downloader = ToolDownloader(toolsDir: cacheDir)
                try await downloader.streamDownload(from: url, to: destination) { fraction in
                    Task { @MainActor in
                        self.importProgress = fraction
                    }
                }
            }
            importProgress = nil
            importStage = nil
            app.mutateSession { $0.meta.videoURL = urlString }
            await loadLocalVideo(url: destination)
        } catch {
            importProgress = nil
            importStage = nil
            app.showError(error)
        }
    }

    // MARK: - Export (spec §9.3)

    @MainActor func exportRetimedVideo() async {
        guard let app else { return }
        guard let input = videoFile else {
            app.showErrorMessage(loc("No video loaded", "Import a video first."))
            return
        }
        let session = app.files.session
        let fpsDecimal = session.framerate
        guard fpsDecimal > 0 else {
            app.showErrorMessage(loc("No Framerate", "Set a framerate first."))
            return
        }
        let startFrame = session.effectiveStartFrame
        let endFrame = session.effectiveEndFrame
        guard endFrame > startFrame else {
            app.showErrorMessage(loc("Run End Before Start", "Mark the run start and end first."))
            return
        }
        guard let ffmpeg = await app.ensureTool(.ffmpeg) else { return }

        let baseName = input.deletingPathExtension().lastPathComponent
        guard let output = Panels.saveMovie(title: loc("Export Retimed Video"),
                                            defaultName: "\(baseName)-retimed.mp4") else { return }

        let runStart = TimeFormatter.rounded(Decimal(startFrame) / fpsDecimal, scale: 3)
        let runEnd = TimeFormatter.rounded(Decimal(endFrame) / fpsDecimal, scale: 3)
        let duration = Decimal(string: String(format: "%.3f", durationSeconds)) ?? Decimal(0)
        let bounds = TimerFiltergraphBuilder.trimBounds(runStart: runStart, runEnd: runEnd, videoDuration: duration)

        let pauseSource: [Load] = session.mode == .segments ? session.gapsBetweenSegments() : session.loads
        let pauses: [(start: Decimal, end: Decimal)] = pauseSource.map { load in
            (TimeFormatter.rounded(Decimal(load.startFrame) / fpsDecimal, scale: 3),
             TimeFormatter.rounded(Decimal(load.endFrame) / fpsDecimal, scale: 3))
        }

        let spec = TimerOverlaySpec(
            videoHeight: videoHeight > 0 ? videoHeight : 1080,
            corner: TimerCorner(rawValue: app.settings.timerCorner) ?? .bottomRight,
            style: TimerStyle(rawValue: app.settings.timerStyle) ?? .pill
        )
        let chain = TimerFiltergraphBuilder.build(
            runStart: runStart, runEnd: runEnd, pauses: pauses,
            trimStart: bounds.start, spec: spec
        )
        let job = ExportJob(input: input, output: output,
                            trimStart: bounds.start, trimEnd: bounds.end,
                            filtergraph: chain)

        let newExporter = FfmpegExporter()
        exporter = newExporter
        exportProgress = 0
        do {
            try await newExporter.export(job: job, ffmpeg: ffmpeg) { fraction in
                Task { @MainActor in
                    self.exportProgress = fraction
                }
            }
            exportProgress = nil
            let choice = Alerts.threeButton(
                title: loc("Export Complete"),
                message: loc("Export Complete Message",
                             "The retimed video was exported to {file}.")
                    .replacingOccurrences(of: "{file}", with: output.lastPathComponent),
                first: loc("Open"), second: loc("Show in Folder"), third: app.loc["OK"]
            )
            switch choice {
            case .first:
                NSWorkspace.shared.open(output)
            case .second:
                NSWorkspace.shared.activateFileViewerSelecting([output])
            case .third:
                break
            }
        } catch {
            exportProgress = nil
            if let crtError = error as? CRTError, crtError.messageText == "The export was cancelled." {
                app.toast(loc("Export cancelled"))
            } else {
                app.showError(error)
            }
        }
        exporter = nil
    }

    @MainActor func cancelExport() {
        exporter?.cancel()
    }
}
