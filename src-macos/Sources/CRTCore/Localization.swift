import Foundation

/// Localization layer. Port of `src/crt/language.py`.
///
/// Keyed by the same display names the settings dropdown stores; anything
/// else (including the "en" written by default settings) falls back to
/// English. Keys missing from a language fall back to the English value,
/// then to the key itself — so new-UI strings (video retimer, dashboard,
/// segments, speedrun.com) whose keys *are* their English text degrade
/// gracefully in every language.
public struct Localization: Sendable {
    public static let languageNames = ["English", "Français", "Polski", "Español"]

    let languageName: String
    private let table: [String: String]

    public init(language: String) {
        self.languageName = language
        switch language {
        case "Français":
            self.table = LocalizationTables.french
        case "Polski":
            self.table = LocalizationTables.polish
        case "Español":
            self.table = LocalizationTables.spanish
        default:
            self.table = LocalizationTables.english
        }
    }

    public subscript(_ key: String) -> String {
        if let value = table[key] {
            return value
        }
        if let value = LocalizationTables.english[key] {
            return value
        }
        return key
    }

    private static func table(for language: String) -> [String: String] {
        switch language {
        case "Français":
            return LocalizationTables.french
        case "Polski":
            return LocalizationTables.polish
        case "Español":
            return LocalizationTables.spanish
        default:
            return LocalizationTables.english
        }
    }

    /// Translates display text from one language to another by reverse key
    /// lookup (port of `Language.translate`). Returns the input unchanged
    /// when no key matches.
    ///
    /// Several keys share a value (English "Edit (Menu Bar)"/"Edit", French
    /// "Add Loads"/"Add Load", …). Python dicts are insertion-ordered so the
    /// first-declared key always wins there; Swift dictionaries have no stable
    /// iteration order, so the declared key order is walked explicitly and any
    /// remaining keys are visited sorted — never at the mercy of hash seeding.
    static func translate(from fromLanguage: String, to toLanguage: String, text: String) -> String {
        let source = table(for: fromLanguage)
        let target = table(for: toLanguage)
        for key in LocalizationTables.orderedKeys where source[key] == text {
            return target[key] ?? text
        }
        for key in source.keys.sorted() where source[key] == text {
            return target[key] ?? text
        }
        return text
    }
}
