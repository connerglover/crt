import Foundation

/// Frame input parsing pipeline. Port of `src/crt/frame_input.py` (spec §2)
/// plus the debug-info id extraction from `src/crt/youtube_format.py`.
public enum FrameInputParser {

    /// True if the text looks like YouTube debug-info JSON.
    public static func isDebugInfo(_ text: String) -> Bool {
        return text.contains("{") && text.contains("\"cmt\"")
    }

    /// Converts YouTube debug-info JSON to a frame number.
    /// Throws `CRTError.invalidDebugInfo` on any parse failure.
    public static func debugInfoFrame(framerate: Decimal, debugInfo: String) throws -> Int {
        guard let start = debugInfo.firstIndex(of: "{") else {
            throw CRTError.invalidDebugInfo
        }
        let jsonText = String(debugInfo[start...])
        guard
            let object = try? JSONSerialization.jsonObject(with: Data(jsonText.utf8)),
            let dict = object as? [String: Any],
            let cmtValue = dict["cmt"]
        else {
            throw CRTError.invalidDebugInfo
        }

        let cmtString: String
        if let text = cmtValue as? String {
            cmtString = text
        } else if let number = cmtValue as? NSNumber {
            cmtString = number.stringValue
        } else {
            throw CRTError.invalidDebugInfo
        }

        guard let cmt = Decimal(string: cmtString) else {
            throw CRTError.invalidDebugInfo
        }
        return TimeFormatter.roundedToInt(cmt * framerate)
    }

    /// Extracts (videoID, formatID) from a debug-info blob; nil when the text
    /// is not parseable JSON or either field is missing/empty. The `fmt`
    /// field may arrive as a string or a number — both convert to String.
    public static func extractDebugInfoIDs(_ debugInfo: String) -> (videoID: String, formatID: String)? {
        guard let start = debugInfo.firstIndex(of: "{") else { return nil }
        let jsonText = String(debugInfo[start...])
        guard
            let object = try? JSONSerialization.jsonObject(with: Data(jsonText.utf8)),
            let dict = object as? [String: Any]
        else {
            return nil
        }

        func stringValue(_ any: Any?) -> String? {
            if let text = any as? String { return text.isEmpty ? nil : text }
            if let number = any as? NSNumber { return number.stringValue }
            return nil
        }

        guard
            let videoID = stringValue(dict["docid"]),
            let formatID = stringValue(dict["fmt"])
        else {
            return nil
        }
        return (videoID, formatID)
    }

    /// Strips every char except `[0-9.]` and collapses extra decimal points
    /// (keeps the first, drops the rest).
    static func stripAndCollapse(_ text: String) -> String {
        var cleaned = text.filter { $0 == "." || ($0 >= "0" && $0 <= "9") }
        if cleaned.filter({ $0 == "." }).count > 1, let dot = cleaned.firstIndex(of: ".") {
            let head = String(cleaned[...dot])
            let tail = String(cleaned[cleaned.index(after: dot)...]).replacingOccurrences(of: ".", with: "")
            cleaned = head + tail
        }
        return cleaned
    }

    /// Cleans a framerate string into a valid Decimal (port of clean_framerate).
    public static func cleanFramerate(_ text: String) -> Decimal {
        var cleaned = stripAndCollapse(text)
        guard cleaned.contains(where: { $0 >= "0" && $0 <= "9" }) else {
            return Decimal(0)
        }
        if cleaned.hasSuffix(".") {
            cleaned += "0"
        }
        return Decimal(string: cleaned) ?? Decimal(0)
    }

    /// The full parsing pipeline (spec §2). Throws only for invalid
    /// debug-info input.
    public static func parse(_ text: String, framerate: Decimal) throws -> Int {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)

        // Step 1 — debug info
        if isDebugInfo(trimmed) {
            return try debugInfoFrame(framerate: framerate, debugInfo: trimmed)
        }

        // Step 2/3 — strip; no digits → 0
        let cleaned = stripAndCollapse(trimmed)
        guard cleaned.contains(where: { $0 >= "0" && $0 <= "9" }) else {
            return 0
        }

        // Step 4 — decimal → seconds × framerate
        if cleaned.contains(".") {
            if framerate == 0 { return 0 }
            guard let value = Decimal(string: cleaned) else { return 0 }
            return TimeFormatter.roundedToInt(value * framerate)
        }

        // Step 5 — plain integer
        return Int(cleaned) ?? 0
    }
}
