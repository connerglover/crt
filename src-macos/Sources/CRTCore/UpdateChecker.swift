import Foundation

/// Startup update check against the GitHub releases API (spec §6).
/// Silent on any failure — a flaky connection must never interrupt the app.
public struct UpdateChecker: Sendable {

    public init() {}

    /// Returns the latest release tag when it differs from the running
    /// version, otherwise nil. 5-second timeout.
    public func latestVersionIfNewer() async -> String? {
        guard let url = URL(string: "https://api.github.com/repos/connerglover/crt/releases/latest") else {
            return nil
        }

        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 5
        configuration.timeoutIntervalForResource = 5
        let session = URLSession(configuration: configuration)

        var request = URLRequest(url: url)
        request.setValue(CRTVersion.userAgent, forHTTPHeaderField: "User-Agent")
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")

        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
                return nil
            }
            guard
                let object = try? JSONSerialization.jsonObject(with: data),
                let dict = object as? [String: Any],
                let tag = dict["tag_name"] as? String
            else {
                return nil
            }
            if tag != CRTVersion.current {
                return tag
            }
            return nil
        } catch {
            return nil
        }
    }
}
