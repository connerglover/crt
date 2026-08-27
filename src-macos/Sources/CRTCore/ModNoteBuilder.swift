import Foundation

/// Builds the mod note from the user's template. Port of `App._mod_note`
/// (spec §5). Unknown placeholders are left literal instead of crashing.
public enum ModNoteBuilder {

    /// The placeholder values for a session (all pre-formatted strings).
    public static func placeholderValues(for session: TimeSession) -> [String: String] {
        let fps = session.framerate

        let startTime: String
        let endTime: String
        if fps == 0 {
            startTime = "0"
            endTime = "0"
        } else {
            startTime = formatSeconds(
                TimeFormatter.rounded(Decimal(session.effectiveStartFrame) / fps, scale: session.precision))
            endTime = formatSeconds(
                TimeFormatter.rounded(Decimal(session.effectiveEndFrame) / fps, scale: session.precision))
        }

        let (hours, minutes, seconds, milliseconds) = TimeFormatter.components(session.displayWithLoads)

        return [
            "time_with_loads": TimeFormatter.iso(session.displayWithLoads),
            "time_without_loads": TimeFormatter.iso(session.displayWithoutLoads),
            "hours": hours,
            "minutes": minutes,
            "seconds": seconds,
            "milliseconds": milliseconds,
            "start_frame": String(session.effectiveStartFrame),
            "end_frame": String(session.effectiveEndFrame),
            "start_time": startTime,
            "end_time": endTime,
            "total_frames": String(session.effectiveEndFrame - session.effectiveStartFrame),
            "fps": TimeFormatter.string(fps),
            "plug": CRTVersion.plug,
        ]
    }

    /// Formats a rounded seconds value the way Python's `float` `str()` renders
    /// it in the mod note: trailing zeros trimmed, but always at least one
    /// fractional digit (`0` → `"0.0"`, `120` → `"120.0"`). The Python
    /// `_mod_note` divides floats and interpolates them with `str.format`, so a
    /// whole-second value keeps its `.0` — `TimeFormatter.string` would drop it.
    private static func formatSeconds(_ value: Decimal) -> String {
        var text = TimeFormatter.string(value)
        if text.contains(".") {
            while text.hasSuffix("0") { text.removeLast() }
            if text.hasSuffix(".") { text += "0" }
        } else {
            text += ".0"
        }
        return text
    }

    public static func build(template: String, session: TimeSession) -> String {
        return substitute(template: template, values: placeholderValues(for: session))
    }

    /// Python-style `str.format` substitution with two differences the spec
    /// requires: unknown placeholders stay literal, and nothing ever throws.
    /// `{{`/`}}` escape to literal braces like Python.
    public static func substitute(template: String, values: [String: String]) -> String {
        var output = ""
        let characters = Array(template)
        var index = 0

        while index < characters.count {
            let character = characters[index]

            if character == "{" {
                // "{{" → "{"
                if index + 1 < characters.count && characters[index + 1] == "{" {
                    output.append("{")
                    index += 2
                    continue
                }
                // Scan for a placeholder name up to "}".
                var nameEnd = index + 1
                var name = ""
                var valid = true
                while nameEnd < characters.count && characters[nameEnd] != "}" {
                    let c = characters[nameEnd]
                    if c.isLetter || c.isNumber || c == "_" {
                        name.append(c)
                        nameEnd += 1
                    } else {
                        valid = false
                        break
                    }
                }
                if valid, nameEnd < characters.count, characters[nameEnd] == "}",
                   let value = values[name] {
                    output += value
                    index = nameEnd + 1
                    continue
                }
                // Unknown or malformed — leave literal.
                output.append("{")
                index += 1
                continue
            }

            if character == "}" {
                // "}}" → "}"
                if index + 1 < characters.count && characters[index + 1] == "}" {
                    output.append("}")
                    index += 2
                    continue
                }
                output.append("}")
                index += 1
                continue
            }

            output.append(character)
            index += 1
        }

        return output
    }
}
