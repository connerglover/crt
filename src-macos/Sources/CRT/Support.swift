import AppKit
import Security
import SwiftUI
import UniformTypeIdentifiers
import CRTCore

// MARK: - Clipboard

enum Clipboard {
    @MainActor static func set(_ text: String) {
        let pasteboard = NSPasteboard.general
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
    }

    @MainActor static func get() -> String {
        NSPasteboard.general.string(forType: .string) ?? ""
    }
}

// MARK: - Keychain (speedrun.com API key, spec §4)

enum KeychainStore {
    static let service = "CRT Speedrun.com"
    static let account = "api-key"

    static func read() -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var item: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &item)
        guard status == errSecSuccess, let data = item as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }

    /// Stores the key, returning the `SecItemAdd` status so callers can tell
    /// the user when the Keychain refused the write (a silent failure would
    /// leave the app "signed in" with no key to send).
    @discardableResult
    static func write(_ value: String) -> OSStatus {
        delete()
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecValueData as String: Data(value.utf8),
        ]
        return SecItemAdd(query as CFDictionary, nil)
    }

    static func delete() {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        _ = SecItemDelete(query as CFDictionary)
    }
}

// MARK: - Alerts

enum Alerts {
    enum SaveChoice {
        case yes
        case no
        case cancel
    }

    enum ThreeChoice {
        case first
        case second
        case third
    }

    /// Button titles default to the localized strings (spec §13).
    @MainActor private static func localized(_ key: String) -> String {
        AppModel.shared.loc[key]
    }

    @MainActor static func error(title: String, message: String) {
        let alert = NSAlert()
        alert.alertStyle = .critical
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: localized("OK"))
        _ = alert.runModal()
    }

    @MainActor static func info(title: String, message: String) {
        let alert = NSAlert()
        alert.alertStyle = .informational
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: localized("OK"))
        _ = alert.runModal()
    }

    @MainActor static func confirmYesNo(title: String, message: String,
                                        yesTitle: String? = nil, noTitle: String? = nil) -> Bool {
        let alert = NSAlert()
        alert.alertStyle = .informational
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: yesTitle ?? localized("Yes"))
        alert.addButton(withTitle: noTitle ?? localized("No"))
        return alert.runModal() == .alertFirstButtonReturn
    }

    /// Save / Don't Save / Cancel (port of `_prompt_save_if_dirty`).
    @MainActor static func yesNoCancel(title: String, message: String,
                                       saveTitle: String? = nil,
                                       dontSaveTitle: String? = nil,
                                       cancelTitle: String? = nil) -> SaveChoice {
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: saveTitle ?? localized("Save"))
        alert.addButton(withTitle: dontSaveTitle ?? localized("Discard Changes"))
        alert.addButton(withTitle: cancelTitle ?? localized("Cancel"))
        switch alert.runModal() {
        case .alertFirstButtonReturn:
            return .yes
        case .alertSecondButtonReturn:
            return .no
        default:
            return .cancel
        }
    }

    @MainActor static func threeButton(title: String, message: String,
                                       first: String, second: String, third: String) -> ThreeChoice {
        let alert = NSAlert()
        alert.alertStyle = .informational
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: first)
        alert.addButton(withTitle: second)
        alert.addButton(withTitle: third)
        switch alert.runModal() {
        case .alertFirstButtonReturn:
            return .first
        case .alertSecondButtonReturn:
            return .second
        default:
            return .third
        }
    }
}

// MARK: - File panels

enum Panels {
    @MainActor static func openJSON(title: String) -> URL? {
        let panel = NSOpenPanel()
        panel.title = title
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        panel.allowedContentTypes = [.json]
        return panel.runModal() == .OK ? panel.url : nil
    }

    @MainActor static func saveJSON(title: String, defaultName: String) -> URL? {
        let panel = NSSavePanel()
        panel.title = title
        panel.allowedContentTypes = [.json]
        panel.canCreateDirectories = true
        panel.nameFieldStringValue = defaultName
        return panel.runModal() == .OK ? panel.url : nil
    }

    @MainActor static func openMovie(title: String) -> URL? {
        let panel = NSOpenPanel()
        panel.title = title
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        var types: [UTType] = [.movie, .mpeg4Movie, .quickTimeMovie, .avi]
        for ext in ["mkv", "webm", "m4v", "ts", "flv"] {
            if let type = UTType(filenameExtension: ext) {
                types.append(type)
            }
        }
        panel.allowedContentTypes = types
        return panel.runModal() == .OK ? panel.url : nil
    }

    @MainActor static func saveMovie(title: String, defaultName: String) -> URL? {
        let panel = NSSavePanel()
        panel.title = title
        panel.allowedContentTypes = [.mpeg4Movie]
        panel.canCreateDirectories = true
        panel.nameFieldStringValue = defaultName
        return panel.runModal() == .OK ? panel.url : nil
    }
}

// MARK: - Window access (always-on-top, spec §6)

struct WindowAccessor: NSViewRepresentable {
    let onWindow: (NSWindow) -> Void

    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async {
            if let window = view.window {
                self.onWindow(window)
            }
        }
        return view
    }

    func updateNSView(_ nsView: NSView, context: Context) {
        DispatchQueue.main.async {
            if let window = nsView.window {
                self.onWindow(window)
            }
        }
    }
}

// MARK: - Colors

extension Color {
    /// Parses "#rrggbb"; falls back to the CRT default accent (#5b9bd5).
    init(hexString: String) {
        var text = hexString.trimmingCharacters(in: .whitespaces)
        if text.hasPrefix("#") {
            text.removeFirst()
        }
        var value: UInt64 = 0
        let scanner = Scanner(string: text)
        if text.count == 6, scanner.scanHexInt64(&value) {
            self.init(
                red: Double((value >> 16) & 0xFF) / 255.0,
                green: Double((value >> 8) & 0xFF) / 255.0,
                blue: Double(value & 0xFF) / 255.0
            )
        } else {
            self.init(red: 0x5B / 255.0, green: 0x9B / 255.0, blue: 0xD5 / 255.0)
        }
    }

    @MainActor func hexRGBString() -> String {
        let fallback = "#5b9bd5"
        guard let converted = NSColor(self).usingColorSpace(.sRGB) else { return fallback }
        let red = Int((converted.redComponent * 255.0).rounded())
        let green = Int((converted.greenComponent * 255.0).rounded())
        let blue = Int((converted.blueComponent * 255.0).rounded())
        return String(format: "#%02x%02x%02x", red, green, blue)
    }
}

// MARK: - Localization helpers

extension Localization {
    /// Looks up a key that the shared language tables do not carry yet,
    /// falling back to the supplied English text instead of the bare key.
    ///
    /// The native-only UI (dashboard, video retimer, speedrun.com, the new
    /// settings) uses the same short keys as the Windows catalog
    /// (`CRT.Core/Localization/LanguageCatalog.cs`); until those keys land in
    /// `LocalizationTables`, this keeps every user-visible string routed
    /// through the localization layer (spec §13) while still rendering
    /// English rather than a key name.
    func text(_ key: String, _ english: String) -> String {
        let value = self[key]
        return value == key ? english : value
    }
}

// MARK: - Misc

/// Runs a main-actor action from any context (used by menu commands whose
/// closures are not statically isolated).
func runOnMain(_ action: @escaping @MainActor @Sendable () -> Void) {
    Task { @MainActor in
        action()
    }
}

/// A stable (FNV-1a) hash used for cache file names.
func stableHash(_ text: String) -> String {
    var hash: UInt64 = 0xcbf29ce484222325
    for byte in text.utf8 {
        hash ^= UInt64(byte)
        hash = hash &* 0x100000001b3
    }
    return String(hash, radix: 16)
}
