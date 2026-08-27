import Foundation

/// YouTube innertube client identities. YouTube rotates accepted client
/// versions periodically — update these constants (only) when lookups start
/// returning HTTP 400 FAILED_PRECONDITION.
public enum InnertubeClientIdentity {
    /// Primary: ANDROID client (verified working 2026-08).
    public static let androidClientName = "ANDROID"
    public static let androidClientVersion = "20.10.38"
    public static let androidSdkVersion = 30
    public static let androidUserAgent = "com.google.android.youtube/20.10.38 (Linux; U; Android 11) gzip"

    /// Fallback: IOS client. Never use the WEB client (zero formats).
    public static let iosClientName = "IOS"
    public static let iosClientVersion = "20.10.4"
    public static let iosDeviceModel = "iPhone16,2"
    public static let iosUserAgent = "com.google.ios.youtube/20.10.4 (iPhone16,2; U; CPU iOS 17_5_1 like Mac OS X)"
}

/// Looks up the real encoded framerate of a YouTube video format via the
/// innertube player endpoint (spec §2.1). All failures return nil — callers
/// treat that as "couldn't be verified". Results (including failures) are
/// cached per (videoId, itag) for the app session.
public actor InnertubeClient {
    public static let shared = InnertubeClient()

    private var cache: [String: Decimal?] = [:]
    private let session: URLSession

    public init() {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 8
        configuration.timeoutIntervalForResource = 8
        self.session = URLSession(configuration: configuration)
    }

    /// The cached lookup. `formatID` is the debug info's `fmt` value (string).
    public func framerate(videoID: String, formatID: String) async -> Decimal? {
        let key = "\(videoID)|\(formatID)"
        if let cached = cache[key] {
            return cached
        }
        let result = await lookup(videoID: videoID, formatID: formatID)
        cache[key] = result
        return result
    }

    private func lookup(videoID: String, formatID: String) async -> Decimal? {
        if let fps = await requestPlayer(videoID: videoID, formatID: formatID, useIOSClient: false) {
            return fps
        }
        return await requestPlayer(videoID: videoID, formatID: formatID, useIOSClient: true)
    }

    private func requestPlayer(videoID: String, formatID: String, useIOSClient: Bool) async -> Decimal? {
        guard let url = URL(string: "https://www.youtube.com/youtubei/v1/player") else {
            return nil
        }

        var client: [String: Any]
        let userAgent: String
        if useIOSClient {
            client = [
                "clientName": InnertubeClientIdentity.iosClientName,
                "clientVersion": InnertubeClientIdentity.iosClientVersion,
                "deviceModel": InnertubeClientIdentity.iosDeviceModel,
                "hl": "en",
            ]
            userAgent = InnertubeClientIdentity.iosUserAgent
        } else {
            client = [
                "clientName": InnertubeClientIdentity.androidClientName,
                "clientVersion": InnertubeClientIdentity.androidClientVersion,
                "androidSdkVersion": InnertubeClientIdentity.androidSdkVersion,
                "hl": "en",
            ]
            userAgent = InnertubeClientIdentity.androidUserAgent
        }

        let body: [String: Any] = [
            "videoId": videoID,
            "context": ["client": client],
        ]

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue(userAgent, forHTTPHeaderField: "User-Agent")
        guard let bodyData = try? JSONSerialization.data(withJSONObject: body) else {
            return nil
        }
        request.httpBody = bodyData

        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
                return nil
            }
            return InnertubeClient.parsePlayerResponse(data, formatID: formatID)
        } catch {
            return nil
        }
    }

    /// Reads `streamingData.formats` + `streamingData.adaptiveFormats`, finds
    /// the entry whose itag matches (string-compared), returns its fps.
    public static func parsePlayerResponse(_ data: Data, formatID: String) -> Decimal? {
        guard
            let object = try? JSONSerialization.jsonObject(with: data),
            let dict = object as? [String: Any],
            let streamingData = dict["streamingData"] as? [String: Any]
        else {
            return nil
        }

        var formats: [[String: Any]] = []
        if let plain = streamingData["formats"] as? [[String: Any]] {
            formats.append(contentsOf: plain)
        }
        if let adaptive = streamingData["adaptiveFormats"] as? [[String: Any]] {
            formats.append(contentsOf: adaptive)
        }

        for format in formats {
            guard let itag = format["itag"] as? NSNumber else { continue }
            if itag.stringValue == formatID {
                if let fps = format["fps"] as? NSNumber {
                    return Decimal(string: fps.stringValue)
                }
                return nil
            }
        }
        return nil
    }

    /// Parses `yt-dlp -j` output for the fallback path (spec §2.1):
    /// `formats[].format_id == formatID → fps`.
    public static func parseYtDlpInfo(_ data: Data, formatID: String) -> Decimal? {
        guard
            let object = try? JSONSerialization.jsonObject(with: data),
            let dict = object as? [String: Any],
            let formats = dict["formats"] as? [[String: Any]]
        else {
            return nil
        }
        for format in formats {
            let id: String?
            if let text = format["format_id"] as? String {
                id = text
            } else if let number = format["format_id"] as? NSNumber {
                id = number.stringValue
            } else {
                id = nil
            }
            guard id == formatID else { continue }
            if let fps = format["fps"] as? NSNumber {
                return Decimal(string: fps.stringValue)
            }
            return nil
        }
        return nil
    }
}
