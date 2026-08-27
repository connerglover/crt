import Foundation

/// Persisted recent files (`recent.json`, most recent first, capped at 20).
public struct RecentFilesStore: Sendable {
    public static let cap = 20
    public let url: URL

    public init(configDir: URL) {
        self.url = configDir.appendingPathComponent("recent.json")
    }

    public func load() -> [String] {
        guard let data = try? Data(contentsOf: url),
              let paths = try? JSONDecoder().decode([String].self, from: data) else {
            return []
        }
        return paths
    }

    public func save(_ paths: [String]) {
        let capped = Array(paths.prefix(RecentFilesStore.cap))
        if let data = try? JSONEncoder().encode(capped) {
            try? data.write(to: url, options: [.atomic])
        }
    }

    /// Moves (or inserts) a path to the front and persists.
    @discardableResult
    public func add(_ path: String) -> [String] {
        var paths = load()
        paths.removeAll { $0 == path }
        paths.insert(path, at: 0)
        save(paths)
        return Array(paths.prefix(RecentFilesStore.cap))
    }

    public func remove(_ path: String) {
        var paths = load()
        paths.removeAll { $0 == path }
        save(paths)
    }
}

/// One dashboard run-library row (`library.json`, spec §11.1).
public struct RunLibraryEntry: Codable, Sendable, Equatable, Identifiable {
    public var path: String
    public var title: String
    public var game: String
    public var mode: String
    public var timeWithoutLoads: String
    public var timeWithLoads: String
    public var modified: String

    public var id: String { path }

    public init(path: String, title: String, game: String, mode: String,
                timeWithoutLoads: String, timeWithLoads: String, modified: String) {
        self.path = path
        self.title = title
        self.game = game
        self.mode = mode
        self.timeWithoutLoads = timeWithoutLoads
        self.timeWithLoads = timeWithLoads
        self.modified = modified
    }

    /// Builds an entry from a session + path (title falls back to file name).
    ///
    /// `modified` comes from the session's own metadata so that merely opening
    /// an old run does not restamp it with the current date — the save path
    /// stamps `meta.modified` itself (`SessionFileManager.stampMeta`). Only a
    /// session that carries no timestamp falls back to "now".
    public static func from(session: TimeSession, path: String) -> RunLibraryEntry {
        let fileName = URL(fileURLWithPath: path).deletingPathExtension().lastPathComponent
        let modified: String
        if let stamped = session.meta.modified, !stamped.isEmpty {
            modified = stamped
        } else {
            modified = ISO8601DateFormatter().string(from: Date())
        }
        return RunLibraryEntry(
            path: path,
            title: session.meta.title ?? fileName,
            game: session.meta.game ?? "",
            mode: session.mode.rawValue,
            timeWithoutLoads: TimeFormatter.iso(session.displayWithoutLoads),
            timeWithLoads: TimeFormatter.iso(session.displayWithLoads),
            modified: modified
        )
    }
}

/// The run library index (spec §11.1) — everything saved/opened by the app.
public struct RunLibraryStore: Sendable {
    public let url: URL

    public init(configDir: URL) {
        self.url = configDir.appendingPathComponent("library.json")
    }

    public func load() -> [RunLibraryEntry] {
        guard let data = try? Data(contentsOf: url),
              let entries = try? JSONDecoder().decode([RunLibraryEntry].self, from: data) else {
            return []
        }
        return entries
    }

    public func save(_ entries: [RunLibraryEntry]) {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        if let data = try? encoder.encode(entries) {
            try? data.write(to: url, options: [.atomic])
        }
    }

    /// Inserts or replaces the entry with the same path, newest first.
    @discardableResult
    public func upsert(_ entry: RunLibraryEntry) -> [RunLibraryEntry] {
        var entries = load()
        entries.removeAll { $0.path == entry.path }
        entries.insert(entry, at: 0)
        save(entries)
        return entries
    }

    @discardableResult
    public func remove(path: String) -> [RunLibraryEntry] {
        var entries = load()
        entries.removeAll { $0.path == path }
        save(entries)
        return entries
    }
}

/// Autosave (`autosave.json`) for crash restore (spec §14). Deleted on clean
/// exit; offered for restore on the next launch otherwise.
public struct AutosaveService: Sendable {
    public let url: URL

    public init(configDir: URL) {
        self.url = configDir.appendingPathComponent("autosave.json")
    }

    public func save(session: TimeSession, filePath: String?) {
        var dict: [String: Any] = ["session": RunFileStore.encodeDictionary(session)]
        if let filePath {
            dict["path"] = filePath
        }
        if let data = try? JSONSerialization.data(withJSONObject: dict, options: [.sortedKeys]) {
            try? data.write(to: url, options: [.atomic])
        }
    }

    public func restore() -> (session: TimeSession, filePath: String?)? {
        guard let data = try? Data(contentsOf: url),
              let object = try? JSONSerialization.jsonObject(with: data),
              let dict = object as? [String: Any],
              let sessionDict = dict["session"] as? [String: Any],
              let session = try? RunFileStore.decodeDictionary(sessionDict) else {
            return nil
        }
        return (session, dict["path"] as? String)
    }

    public var exists: Bool {
        FileManager.default.fileExists(atPath: url.path)
    }

    public func clear() {
        try? FileManager.default.removeItem(at: url)
    }
}
