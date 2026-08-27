import Foundation

/// A tiny INI reader/writer compatible with Python's `ConfigParser` output
/// (`[Section]` headers, `key = value` lines, blank line between sections).
/// Option names are lowercased like ConfigParser's default `optionxform`;
/// section names stay case-sensitive.
public struct IniFile: Sendable, Equatable {
    private var sections: [String: [String: String]] = [:]
    private var sectionOrder: [String] = []
    private var keyOrder: [String: [String]] = [:]

    public init() {}

    public init(text: String) {
        var currentSection: String?

        for rawLine in text.components(separatedBy: .newlines) {
            let line = rawLine.trimmingCharacters(in: .whitespaces)
            if line.isEmpty || line.hasPrefix("#") || line.hasPrefix(";") {
                continue
            }
            if line.hasPrefix("[") && line.hasSuffix("]") {
                let name = String(line.dropFirst().dropLast()).trimmingCharacters(in: .whitespaces)
                currentSection = name
                if sections[name] == nil {
                    sections[name] = [:]
                    keyOrder[name] = []
                    sectionOrder.append(name)
                }
                continue
            }
            guard let section = currentSection else { continue }

            // Split on the first '=' or ':' (whichever comes first), like
            // ConfigParser's default delimiters.
            let equals = line.firstIndex(of: "=")
            let colon = line.firstIndex(of: ":")
            var delimiter: String.Index?
            switch (equals, colon) {
            case let (.some(e), .some(c)):
                delimiter = min(e, c)
            case let (.some(e), nil):
                delimiter = e
            case let (nil, .some(c)):
                delimiter = c
            case (nil, nil):
                delimiter = nil
            }
            guard let split = delimiter else { continue }

            let key = String(line[line.startIndex..<split]).trimmingCharacters(in: .whitespaces).lowercased()
            let value = String(line[line.index(after: split)...]).trimmingCharacters(in: .whitespaces)
            if key.isEmpty { continue }
            setRaw(section: section, key: key, value: value)
        }
    }

    public static func read(from url: URL) throws -> IniFile {
        let text = try String(contentsOf: url, encoding: .utf8)
        return IniFile(text: text)
    }

    // MARK: - Access

    var sectionNames: [String] { sectionOrder }

    public func hasSection(_ section: String) -> Bool {
        sections[section] != nil
    }

    public func hasOption(_ section: String, _ key: String) -> Bool {
        sections[section]?[key.lowercased()] != nil
    }

    public func get(_ section: String, _ key: String) -> String? {
        sections[section]?[key.lowercased()]
    }

    func keys(in section: String) -> [String] {
        keyOrder[section] ?? []
    }

    public mutating func addSection(_ section: String) {
        if sections[section] == nil {
            sections[section] = [:]
            keyOrder[section] = []
            sectionOrder.append(section)
        }
    }

    public mutating func set(_ section: String, _ key: String, _ value: String) {
        addSection(section)
        setRaw(section: section, key: key.lowercased(), value: value)
    }

    private mutating func setRaw(section: String, key: String, value: String) {
        if sections[section] == nil {
            sections[section] = [:]
            keyOrder[section] = []
            sectionOrder.append(section)
        }
        if sections[section]?[key] == nil {
            keyOrder[section, default: []].append(key)
        }
        sections[section]?[key] = value
    }

    /// ConfigParser-style boolean: 1/yes/true/on are true; 0/no/false/off are
    /// false; anything else returns nil.
    public func getBool(_ section: String, _ key: String) -> Bool? {
        guard let raw = get(section, key)?.lowercased() else { return nil }
        switch raw {
        case "1", "yes", "true", "on":
            return true
        case "0", "no", "false", "off":
            return false
        default:
            return nil
        }
    }

    // MARK: - Serialization

    public func serialized() -> String {
        var output = ""
        for section in sectionOrder {
            output += "[\(section)]\n"
            for key in keyOrder[section] ?? [] {
                let value = sections[section]?[key] ?? ""
                output += "\(key) = \(value)\n"
            }
            output += "\n"
        }
        return output
    }

    public func write(to url: URL) throws {
        try FileManager.default.createDirectory(
            at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try serialized().write(to: url, atomically: true, encoding: .utf8)
    }
}
