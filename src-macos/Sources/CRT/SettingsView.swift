import AppKit
import SwiftUI
import CRTCore

/// The Settings scene (spec §4, §12): Apply / Cancel / Restore Defaults
/// semantics as in the Python app, plus the native-only video keys.
@MainActor
struct SettingsView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.dismiss) private var dismiss

    /// The stored language codes mapped to the display names the picker (and
    /// the Python dialog's `lang_map`) uses.
    private static let languageDisplayNames = [
        "en": "English",
        "es": "Español",
        "fr": "Français",
        "pl": "Polski",
    ]

    @State private var enableUpdates = true
    @State private var theme = "Automatic"
    @State private var accentColor = Color(hexString: "#5b9bd5")
    @State private var language = "English"
    @State private var modNoteFormat = ""
    @State private var timerCorner = "bottom-right"
    @State private var timerStyle = "pill"
    @State private var defaultMode = TimingMode.loads.rawValue
    @State private var ffmpegPath = ""
    @State private var ytdlpPath = ""
    @State private var hotkeys: [String: String] = [:]

    var body: some View {
        TabView {
            generalTab
                .tabItem { Label(model.loc["Settings"], systemImage: "gearshape") }
            videoTab
                .tabItem { Label(model.loc["Video Retimer"], systemImage: "film") }
            HotkeyEditorView(hotkeys: $hotkeys)
                .environment(model)
                .tabItem { Label(model.loc["Hotkeys"], systemImage: "keyboard") }
        }
        .frame(width: 560, height: 460)
        .onAppear {
            // Always re-read: the Settings scene keeps this view alive across
            // openings, so abandoned edits must not survive a reopen.
            loadFromSettings()
        }
    }

    // MARK: - General

    private var generalTab: some View {
        VStack(spacing: 0) {
            Form {
                Toggle(model.loc["Automatically Check for Updates"], isOn: $enableUpdates)

                Picker(model.loc["Theme"], selection: $theme) {
                    Text(model.loc["Automatic"]).tag("Automatic")
                    Text(model.loc["Dark"]).tag("Dark")
                    Text(model.loc["Light"]).tag("Light")
                }

                ColorPicker(model.loc["Accent Color"], selection: $accentColor, supportsOpacity: false)

                Picker(model.loc["Language"], selection: $language) {
                    ForEach(languageOptions, id: \.self) { name in
                        Text(name).tag(name)
                    }
                }

                VStack(alignment: .leading, spacing: 4) {
                    Text(model.loc["Mod Note Format"])
                    TextField("", text: $modNoteFormat)
                        .textFieldStyle(.roundedBorder)
                    Text(model.loc.text(
                        "Mod Note Placeholders",
                        "Placeholders: {time_without_loads} {time_with_loads} {fps} {start_frame} {end_frame} {total_frames} {plug} …"
                    ))
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                }
            }
            .formStyle(.grouped)

            buttonBar
        }
    }

    private var languageOptions: [String] {
        // Stored codes are mapped to display names in `loadFromSettings`, so
        // this only ever grows for a genuinely unknown stored value.
        var options = Localization.languageNames
        if !options.contains(language) {
            options.insert(language, at: 0)
        }
        return options
    }

    // MARK: - Video / tools

    private var videoTab: some View {
        VStack(spacing: 0) {
            Form {
                Picker(model.loc["Timer Corner"], selection: $timerCorner) {
                    Text(model.loc["Top Left"]).tag(TimerCorner.topLeft.rawValue)
                    Text(model.loc["Top Right"]).tag(TimerCorner.topRight.rawValue)
                    Text(model.loc["Bottom Left"]).tag(TimerCorner.bottomLeft.rawValue)
                    Text(model.loc["Bottom Right"]).tag(TimerCorner.bottomRight.rawValue)
                }

                Picker(model.loc["Timer Style"], selection: $timerStyle) {
                    Text(model.loc["Pill"]).tag(TimerStyle.pill.rawValue)
                    Text(model.loc["Plain"]).tag(TimerStyle.plain.rawValue)
                }

                Picker(model.loc["Default Mode"], selection: $defaultMode) {
                    Text(model.loc["Loads"]).tag(TimingMode.loads.rawValue)
                    Text(model.loc["Segments"]).tag(TimingMode.segments.rawValue)
                }

                toolPathRow(title: model.loc.text("FFmpeg Path", "FFmpeg Path (empty = auto)"),
                            text: $ffmpegPath)
                toolPathRow(title: model.loc.text("yt-dlp Path", "yt-dlp Path (empty = auto)"),
                            text: $ytdlpPath)
            }
            .formStyle(.grouped)

            buttonBar
        }
    }

    private func toolPathRow(title: String, text: Binding<String>) -> some View {
        HStack(spacing: 8) {
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                TextField("", text: text)
                    .textFieldStyle(.roundedBorder)
            }
            Button(model.loc.text("Browse", "Browse…")) {
                let panel = NSOpenPanel()
                panel.canChooseFiles = true
                panel.canChooseDirectories = false
                if panel.runModal() == .OK, let url = panel.url {
                    text.wrappedValue = url.path
                }
            }
        }
    }

    // MARK: - Apply / Cancel / Restore Defaults

    private var buttonBar: some View {
        HStack {
            Button(model.loc["Restore Defaults"]) {
                restoreDefaults()
            }
            Spacer()
            Button(model.loc["Cancel"]) {
                loadFromSettings()
                dismiss()
            }
            Button(model.loc["Apply"]) {
                apply()
            }
            .buttonStyle(.borderedProminent)
        }
        .padding(12)
    }

    private func loadFromSettings() {
        let settings = model.settings
        enableUpdates = settings.enableUpdates
        theme = settings.theme
        accentColor = Color(hexString: settings.accentColorHex)
        language = SettingsView.languageDisplayNames[settings.language] ?? settings.language
        modNoteFormat = settings.modNoteFormat
        timerCorner = settings.timerCorner
        timerStyle = settings.timerStyle
        defaultMode = settings.defaultMode
        ffmpegPath = settings.ffmpegPath
        ytdlpPath = settings.ytDlpPath
        hotkeys = settings.allHotkeys()
    }

    private func apply() {
        let settings = model.settings

        // Port of `_validate_and_accept`: duplicates never reach the file.
        let duplicates = HotkeyRegistry.duplicateGroups(in: hotkeys)
        if !duplicates.isEmpty {
            Alerts.error(
                title: model.loc["Duplicate Hotkey"],
                message: HotkeyEditorView.duplicateMessage(duplicates, loc: model.loc)
            )
            return
        }

        let before = settings.ini

        settings.setValue("enable_updates", enableUpdates ? "True" : "False")
        settings.setValue("theme", theme)
        settings.setValue("accent_color", accentColor.hexRGBString())
        settings.setValue("language", language)
        settings.setValue("mod_note_format", modNoteFormat)
        settings.setValue("timer_corner", timerCorner)
        settings.setValue("timer_style", timerStyle)
        settings.setValue("default_mode", defaultMode)
        settings.setValue("ffmpeg_path", ffmpegPath)
        settings.setValue("ytdlp_path", ytdlpPath)
        for (actionID, shortcut) in hotkeys {
            settings.setHotkey(actionID, shortcut)
        }

        do {
            try settings.save()
        } catch {
            model.showError(error)
            return
        }

        model.applyAppearance()

        if settings.ini != before {
            Alerts.info(
                title: model.loc["Settings"],
                message: model.loc["Please restart the application to apply the changes."]
            )
        }
    }

    private func restoreDefaults() {
        let confirmed = Alerts.confirmYesNo(
            title: model.loc["Restore Defaults"],
            message: model.loc.text("Restore Defaults Message",
                                    "Are you sure you want to restore the default settings?")
        )
        guard confirmed else { return }
        model.settings.restoreDefaults()
        loadFromSettings()
        model.applyAppearance()
    }
}
