import Foundation

/// A user-presentable error. All CRTCore errors carry the exact message the
/// Python implementation surfaces, so dialogs stay identical.
public enum CRTError: Error, LocalizedError, Equatable {
    case message(String)

    public var errorDescription: String? {
        switch self {
        case .message(let text):
            return text
        }
    }

    /// The message text (same as `errorDescription`, but non-optional).
    public var messageText: String {
        switch self {
        case .message(let text):
            return text
        }
    }

    // Exact strings ported from the Python implementation.
    public static let loadZeroDuration = CRTError.message("The duration of the load is 0.000")
    public static let loadEndsBeforeStart = CRTError.message("The load time ends before it starts.")
    public static let loadInputRequired = CRTError.message("You must provide an input for the loads")
    public static let invalidDebugInfo = CRTError.message("The debug info provided is invalid.\nPlease re-enter debug info.")
    public static let corruptedFile = CRTError.message("The file provided is corrupted.")
    public static let noFilePath = CRTError.message("No file path set — use save_as() first.")
}
