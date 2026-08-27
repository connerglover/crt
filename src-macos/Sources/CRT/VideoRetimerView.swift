import AppKit
import AVKit
import SwiftUI
import CRTCore

/// The Video Retimer workspace (spec §9): import box, native player with
/// frame stepping, timeline with marked regions, marking buttons and export.
@MainActor
struct VideoRetimerView: View {
    @Environment(AppModel.self) private var model
    @FocusState private var keyFocus: Bool
    @FocusState private var urlFieldFocused: Bool

    private var video: VideoModel { model.video }

    var body: some View {
        HStack(spacing: 0) {
            VStack(spacing: 12) {
                importBox
                if video.hasVideo {
                    playerArea
                    timeline
                    transportRow
                    markRow
                } else {
                    emptyState
                }
                Spacer(minLength: 0)
            }
            .padding(16)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)

            Divider()
            sidePanel
                .frame(width: 260)
        }
        .navigationTitle(model.loc["Video Retimer"])
        .focusable()
        .focusEffectDisabled()
        .focused($keyFocus)
        .onKeyPress(phases: [.down, .repeat]) { press in
            handleKey(press)
        }
        .contentShape(Rectangle())
        .onTapGesture {
            // Clicking anywhere on the page hands the marking keys back.
            urlFieldFocused = false
            keyFocus = true
        }
        .onAppear {
            keyFocus = true
        }
        .onChange(of: video.hasVideo) { _, loaded in
            if loaded {
                urlFieldFocused = false
                keyFocus = true
            }
        }
    }

    // MARK: - Import (spec §9.1)

    private var importBox: some View {
        HStack(spacing: 8) {
            TextField(model.loc.text("Video URL", "YouTube URL or direct video URL…"), text: Binding(
                get: { video.importURLText },
                set: { video.importURLText = $0 }
            ))
            .textFieldStyle(.roundedBorder)
            .focused($urlFieldFocused)
            .onSubmit {
                startImport()
            }

            if video.importProgress != nil {
                ProgressView(value: video.importProgress ?? 0, total: 1.0)
                    .frame(width: 110)
                Button(model.loc["Cancel"]) {
                    video.cancelImport()
                }
            } else {
                Button(model.loc["Import Video"]) {
                    startImport()
                }
                Button(model.loc.text("Local File", "Local File…")) {
                    video.chooseLocalFile()
                }
            }
        }
    }

    /// Starts an import and hands keyboard focus back to the key handler —
    /// otherwise every marking key is dead right after the most common import
    /// path (type a URL, press Return).
    private func startImport() {
        urlFieldFocused = false
        keyFocus = true
        video.startImport()
    }

    private var emptyState: some View {
        VStack(spacing: 10) {
            Image(systemName: "film.stack")
                .font(.system(size: 44))
                .foregroundStyle(.tertiary)
            Text(model.loc.text("No video loaded", "Import a video to retime it directly."))
                .foregroundStyle(.secondary)
            Text(model.loc.text(
                "Video Key Hints",
                "Paste a YouTube link above, or choose a local file.\nKeys: , . step frames · [ ] mark · Space play/pause · L load marks"
            ))
            .font(.caption)
            .foregroundStyle(.tertiary)
            .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, minHeight: 260)
    }

    // MARK: - Player (spec §9.2)

    private var playerArea: some View {
        VideoPlayer(player: video.player)
            .frame(minHeight: 260, maxHeight: .infinity)
            .background(Color.black)
            .clipShape(RoundedRectangle(cornerRadius: 8))
    }

    private var timeline: some View {
        VStack(spacing: 2) {
            // Marked regions overlaid above the scrubber.
            GeometryReader { geometry in
                // The slider insets its track by the knob radius; match it so
                // the regions line up with the scrubber they annotate.
                let width = max(geometry.size.width - 20, 1)
                let duration = max(video.durationSeconds, 0.001)
                let fps = video.fps
                ZStack(alignment: .leading) {
                    RoundedRectangle(cornerRadius: 2)
                        .fill(Color.primary.opacity(0.08))
                        .frame(height: 6)
                    // Run bounds first, in a neutral tone, so the marks on top
                    // stay legible as distinct regions.
                    if let bounds = runBoundsRegion {
                        let startX = (Double(bounds.startFrame) / fps) / duration * width
                        let endX = (Double(bounds.endFrame) / fps) / duration * width
                        RoundedRectangle(cornerRadius: 2)
                            .fill(Color.primary.opacity(0.22))
                            .frame(width: max(endX - startX, 2), height: 6)
                            .offset(x: startX)
                    }
                    ForEach(Array(regionFrames.enumerated()), id: \.offset) { pair in
                        let startX = (Double(pair.element.startFrame) / fps) / duration * width
                        let endX = (Double(pair.element.endFrame) / fps) / duration * width
                        RoundedRectangle(cornerRadius: 2)
                            .fill(Color.accentColor.opacity(0.8))
                            .frame(width: max(endX - startX, 2), height: 6)
                            .offset(x: startX)
                    }
                }
                .frame(width: width, height: 6, alignment: .leading)
                .padding(.horizontal, 10)
                .frame(maxHeight: .infinity, alignment: .center)
            }
            .frame(height: 10)

            Slider(
                value: Binding(
                    get: { min(video.currentSeconds, video.durationSeconds) },
                    set: { newValue in video.seek(to: newValue) }
                ),
                in: 0...max(video.durationSeconds, 0.001)
            )
        }
    }

    /// The marks drawn on the timeline: segments in segment mode, loads in
    /// loads mode.
    private var regionFrames: [Load] {
        let session = model.files.session
        return session.mode == .segments ? session.segments : session.loads
    }

    /// The run span, drawn separately from the marks (loads mode only).
    private var runBoundsRegion: Load? {
        let session = model.files.session
        guard session.mode == .loads, session.endFrame > session.startFrame else { return nil }
        return Load(startFrame: session.startFrame, endFrame: session.endFrame)
    }

    private var transportRow: some View {
        HStack(spacing: 10) {
            Button {
                video.stepFrames(-1)
            } label: {
                Image(systemName: "backward.frame")
            }
            .help(model.loc["Frame Back"])

            Button {
                video.togglePlay()
            } label: {
                Image(systemName: video.isPlaying ? "pause.fill" : "play.fill")
                    .frame(width: 24)
            }
            .help(model.loc["Play/Pause"])

            Button {
                video.stepFrames(1)
            } label: {
                Image(systemName: "forward.frame")
            }
            .help(model.loc["Frame Forward"])

            Divider()
                .frame(height: 16)

            Text("\(model.loc["Current Frame"]) \(video.currentFrame)")
                .font(.callout.monospaced())
            Text(video.currentTimeText)
                .font(.callout.monospaced())
                .foregroundStyle(.secondary)

            Spacer()

            if video.exportProgress != nil {
                ProgressView(value: video.exportProgress ?? 0, total: 1.0)
                    .frame(width: 120)
                Button(model.loc["Cancel"]) {
                    video.cancelExport()
                }
            } else {
                Button(model.loc["Export Retimed Video"]) {
                    Task { @MainActor in
                        await video.exportRetimedVideo()
                    }
                }
                .disabled(!video.hasVideo)
            }
        }
    }

    // MARK: - Marking buttons (spec §9.2)

    private var markRow: some View {
        let isSegments = model.files.session.mode == .segments
        return HStack(spacing: 10) {
            Button((isSegments ? model.loc["Mark Segment Start"] : model.loc["Mark Run Start"]) + "  [") {
                video.markPrimaryStart()
            }
            Button((isSegments ? model.loc["Mark Segment End"] : model.loc["Mark Run End"]) + "  ]") {
                video.markPrimaryEnd()
            }
            if !isSegments {
                Button(model.loc["Mark Load Start"] + "  L") {
                    video.markLoadStart()
                }
                Button(model.loc["Mark Load End"] + "  ⇧L") {
                    video.markLoadEnd()
                }
            }
            if let pending = video.pendingSegmentStart, isSegments {
                Text("\(model.loc["Segment Start"]): \(pending)")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            if let pending = video.pendingLoadStart, !isSegments {
                Text("\(model.loc["Load Start"]): \(pending)")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
        }
    }

    // MARK: - Side panel: live times + shared marks list

    private var sidePanel: some View {
        VStack(spacing: 0) {
            let session = model.files.session
            let isSegments = session.mode == .segments
            VStack(spacing: 8) {
                miniTime(title: isSegments ? model.loc["Segment Total"] : model.loc["Without Loads"],
                         value: session.isoWithoutLoads())
                miniTime(title: isSegments ? model.loc["Full Run"] : model.loc["With Loads"],
                         value: session.isoWithLoads())
            }
            .padding(12)
            Divider()
            MarksSidebarView()
                .environment(model)
        }
    }

    private func miniTime(title: String, value: String) -> some View {
        HStack {
            Text(title.uppercased())
                .font(.caption2.weight(.semibold))
                .foregroundStyle(.secondary)
            Spacer()
            Text(value)
                .font(.callout.monospaced())
        }
    }

    // MARK: - Key handling (spec §9.2 / §10)

    private func handleKey(_ press: KeyPress) -> KeyPress.Result {
        // Never steal keys from text fields (the URL field or any inline
        // mark editor — when one is focused the field editor is first
        // responder).
        guard !urlFieldFocused else { return .ignored }
        if NSApplication.shared.keyWindow?.firstResponder is NSTextView {
            return .ignored
        }
        guard video.hasVideo else { return .ignored }

        let character = press.key.character
        let typed = press.characters
        let shift = press.modifiers.contains(.shift)
        // The fixed keys below are plain/shift only; configured combos are
        // matched with their full modifier set further down.
        let chorded = press.modifiers.contains(.command) || press.modifiers.contains(.control)
            || press.modifiers.contains(.option)

        // Arrow keys: 5 frames, Shift+arrow: 1 second (spec §9.2).
        if !chorded && character == KeyEquivalent.leftArrow.character {
            if shift {
                video.jumpSeconds(-1)
            } else {
                video.stepFrames(-5)
            }
            return .handled
        }
        if !chorded && character == KeyEquivalent.rightArrow.character {
            if shift {
                video.jumpSeconds(1)
            } else {
                video.stepFrames(5)
            }
            return .handled
        }

        // Shift variants < > always step one frame. Which of `key` and
        // `characters` carries the shifted glyph is not guaranteed, so test both.
        if !chorded && (character == "<" || typed == "<") {
            video.stepFrames(-1)
            return .handled
        }
        if !chorded && (character == ">" || typed == ">") {
            video.stepFrames(1)
            return .handled
        }

        if matches(press, actionID: "video_frame_back") {
            video.stepFrames(-1)
            return .handled
        }
        if matches(press, actionID: "video_frame_forward") {
            video.stepFrames(1)
            return .handled
        }
        if matches(press, actionID: "video_play_pause") {
            video.togglePlay()
            return .handled
        }
        if matches(press, actionID: "video_mark_start") {
            video.markPrimaryStart()
            return .handled
        }
        if matches(press, actionID: "video_mark_end") {
            video.markPrimaryEnd()
            return .handled
        }
        if matches(press, actionID: "video_mark_load_end") {
            video.markLoadEnd()
            return .handled
        }
        if matches(press, actionID: "video_mark_load_start") {
            video.markLoadStart()
            return .handled
        }
        return .ignored
    }

    /// Compares a key press against the user's configured combo for an action.
    /// The full modifier set is compared, so a rebind to a Cmd/Ctrl/Option
    /// chord works instead of being silently dead (spec §10).
    private func matches(_ press: KeyPress, actionID: String) -> Bool {
        guard let combo = KeyCombo(string: model.settings.hotkey(for: actionID)) else {
            return false
        }
        if combo.command != press.modifiers.contains(.command) { return false }
        if combo.control != press.modifiers.contains(.control) { return false }
        if combo.option != press.modifiers.contains(.option) { return false }
        if combo.shift != press.modifiers.contains(.shift) { return false }

        let pressKey: String
        if press.key.character == KeyEquivalent.space.character {
            pressKey = "Space"
        } else {
            pressKey = String(press.key.character).lowercased()
        }
        if pressKey == combo.key { return true }
        // The editor records `charactersIgnoringModifiers`, so a shifted
        // binding may be stored as its shifted glyph (e.g. "<" for Shift+,).
        let typed = press.characters.lowercased()
        return !typed.isEmpty && typed == combo.key.lowercased()
    }
}
