import Foundation

// MARK: - Models

public struct SRCProfile: Sendable, Equatable {
    public let id: String
    public let name: String
    public let avatarURL: String?

    public init(id: String, name: String, avatarURL: String?) {
        self.id = id
        self.name = name
        self.avatarURL = avatarURL
    }
}

public struct SRCGame: Sendable, Equatable, Identifiable {
    public let id: String
    public let name: String

    public init(id: String, name: String) {
        self.id = id
        self.name = name
    }
}

public struct SRCRun: Sendable, Equatable, Identifiable {
    public let id: String
    public var gameID: String
    public var gameName: String
    public let categoryName: String
    public let levelName: String?
    public let playerNames: [String]
    public let submitted: String?
    public let primarySeconds: Decimal
    public let videoURL: String?
    public let weblink: String?
    public let status: String

    public init(id: String, gameID: String, gameName: String, categoryName: String,
                levelName: String?, playerNames: [String], submitted: String?,
                primarySeconds: Decimal, videoURL: String?, weblink: String?, status: String) {
        self.id = id
        self.gameID = gameID
        self.gameName = gameName
        self.categoryName = categoryName
        self.levelName = levelName
        self.playerNames = playerNames
        self.submitted = submitted
        self.primarySeconds = primarySeconds
        self.videoURL = videoURL
        self.weblink = weblink
        self.status = status
    }

    public var claimedTimeISO: String {
        TimeFormatter.iso(TimeFormatter.rounded(primarySeconds, scale: 3))
    }
}

// MARK: - Throttle

/// Global request gate: at most 100 requests per rolling minute (spec §11.2).
actor SRCRequestGate {
    static let shared = SRCRequestGate()
    private var timestamps: [Date] = []

    func waitTurn() async {
        while true {
            let now = Date()
            timestamps.removeAll { now.timeIntervalSince($0) > 60 }
            if timestamps.count < 100 {
                timestamps.append(now)
                return
            }
            try? await Task.sleep(nanoseconds: 500_000_000)
        }
    }
}

// MARK: - Client

/// speedrun.com REST v1 client (spec §11.2). 10s timeouts, `X-API-Key` when
/// authenticated, pagination via `max=200` + `pagination.links[rel=next]`.
/// `@unchecked` because of the stored `URLSession`: it is documented as
/// thread-safe, but older SDKs do not declare it `Sendable`, and this value is
/// captured by the `@Sendable` task-group closures in `SpeedrunModel`.
public struct SpeedrunClient: @unchecked Sendable {
    public static let baseURL = "https://www.speedrun.com/api/v1"
    public static let apiKeyPage = "https://www.speedrun.com/settings/api-key"

    public let apiKey: String?
    private let session: URLSession

    public init(apiKey: String?) {
        self.apiKey = apiKey
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 10
        configuration.timeoutIntervalForResource = 30
        self.session = URLSession(configuration: configuration)
    }

    // MARK: Requests

    private func makeRequest(url: URL, method: String = "GET", body: Data? = nil) -> URLRequest {
        var request = URLRequest(url: url)
        request.httpMethod = method
        request.setValue(CRTVersion.userAgent, forHTTPHeaderField: "User-Agent")
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let apiKey, !apiKey.isEmpty {
            request.setValue(apiKey, forHTTPHeaderField: "X-API-Key")
        }
        if let body {
            request.httpBody = body
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }
        return request
    }

    private func send(_ request: URLRequest) async throws -> (Data, Int) {
        await SRCRequestGate.shared.waitTurn()
        let (data, response) = try await session.data(for: request)
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        return (data, status)
    }

    private func url(path: String, query: [String: String]) throws -> URL {
        guard var components = URLComponents(string: SpeedrunClient.baseURL + path) else {
            throw CRTError.message("Invalid speedrun.com URL.")
        }
        if !query.isEmpty {
            components.queryItems = query
                .sorted { $0.key < $1.key }
                .map { URLQueryItem(name: $0.key, value: $0.value) }
        }
        guard let url = components.url else {
            throw CRTError.message("Invalid speedrun.com URL.")
        }
        return url
    }

    /// Fetches one JSON object, throwing a readable error on non-2xx.
    private func getJSON(url: URL) async throws -> [String: Any] {
        let (data, status) = try await send(makeRequest(url: url))
        guard (200..<300).contains(status) else {
            throw SpeedrunClient.apiError(data: data, status: status)
        }
        guard
            let object = try? JSONSerialization.jsonObject(with: data),
            let dict = object as? [String: Any]
        else {
            throw CRTError.message("speedrun.com returned an unreadable response.")
        }
        return dict
    }

    private static func apiError(data: Data, status: Int) -> CRTError {
        if let object = try? JSONSerialization.jsonObject(with: data),
           let dict = object as? [String: Any],
           let message = dict["message"] as? String {
            return CRTError.message("speedrun.com error (\(status)): \(message)")
        }
        return CRTError.message("speedrun.com request failed (HTTP \(status)).")
    }

    /// Follows `pagination.links[rel=next]` collecting every `data` element.
    private func getAllPages(startingAt firstURL: URL) async throws -> [[String: Any]] {
        var items: [[String: Any]] = []
        var nextURL: URL? = firstURL
        var pageCount = 0

        while let url = nextURL, pageCount < 25 {
            pageCount += 1
            let dict = try await getJSON(url: url)
            if let page = dict["data"] as? [[String: Any]] {
                items.append(contentsOf: page)
            }
            nextURL = nil
            if let pagination = dict["pagination"] as? [String: Any],
               let links = pagination["links"] as? [[String: Any]] {
                for link in links {
                    if let rel = link["rel"] as? String, rel == "next",
                       let uri = link["uri"] as? String, let next = URL(string: uri) {
                        nextURL = next
                    }
                }
            }
        }
        return items
    }

    // MARK: Profile

    public func profile() async throws -> SRCProfile {
        let dict = try await getJSON(url: try url(path: "/profile", query: [:]))
        guard let data = dict["data"] as? [String: Any],
              let id = data["id"] as? String else {
            throw CRTError.message("The API key could not be validated.")
        }
        let names = data["names"] as? [String: Any]
        let name = (names?["international"] as? String) ?? (data["name"] as? String) ?? id
        var avatar: String?
        if let assets = data["assets"] as? [String: Any],
           let image = assets["image"] as? [String: Any],
           let uri = image["uri"] as? String {
            avatar = uri
        }
        return SRCProfile(id: id, name: name, avatarURL: avatar)
    }

    // MARK: Games

    public func moderatedGames(userID: String) async throws -> [SRCGame] {
        let first = try url(path: "/games", query: ["moderator": userID, "max": "200"])
        let items = try await getAllPages(startingAt: first)
        var games: [SRCGame] = []
        for item in items {
            guard let id = item["id"] as? String else { continue }
            let names = item["names"] as? [String: Any]
            let name = (names?["international"] as? String) ?? id
            games.append(SRCGame(id: id, name: name))
        }
        return games
    }

    // MARK: Runs

    public func newRuns(gameID: String, gameName: String) async throws -> [SRCRun] {
        let first = try url(path: "/runs", query: [
            "status": "new",
            "game": gameID,
            "max": "200",
            "embed": "players,category,level",
            "orderby": "submitted",
            "direction": "asc",
        ])
        let items = try await getAllPages(startingAt: first)
        return items.compactMap { SpeedrunClient.parseRun($0, gameID: gameID, gameName: gameName) }
    }

    public func recentRuns(userID: String) async throws -> [SRCRun] {
        let first = try url(path: "/runs", query: [
            "user": userID,
            "orderby": "date",
            "direction": "desc",
            "max": "10",
            "embed": "game,category",
        ])
        let dict = try await getJSON(url: first)
        let items = (dict["data"] as? [[String: Any]]) ?? []
        return items.compactMap { item in
            var gameName = ""
            var gameID = ""
            if let game = item["game"] as? [String: Any],
               let data = game["data"] as? [String: Any] {
                gameID = (data["id"] as? String) ?? ""
                let names = data["names"] as? [String: Any]
                gameName = (names?["international"] as? String) ?? ""
            }
            return SpeedrunClient.parseRun(item, gameID: gameID, gameName: gameName)
        }
    }

    /// Parses one run object (with players/category/level embeds when present).
    public static func parseRun(_ item: [String: Any], gameID: String, gameName: String) -> SRCRun? {
        guard let id = item["id"] as? String else { return nil }

        var categoryName = ""
        if let category = item["category"] as? [String: Any],
           let data = category["data"] as? [String: Any] {
            categoryName = (data["name"] as? String) ?? ""
        }

        // The API returns `"data": []` for an absent level embed.
        var levelName: String?
        if let level = item["level"] as? [String: Any],
           let data = level["data"] as? [String: Any] {
            levelName = data["name"] as? String
        }

        var playerNames: [String] = []
        if let players = item["players"] as? [String: Any],
           let list = players["data"] as? [[String: Any]] {
            for player in list {
                if let names = player["names"] as? [String: Any],
                   let international = names["international"] as? String {
                    playerNames.append(international)
                } else if let guest = player["name"] as? String {
                    playerNames.append(guest)
                }
            }
        }

        var primary = Decimal(0)
        if let times = item["times"] as? [String: Any],
           let primaryT = times["primary_t"] as? NSNumber {
            primary = Decimal(string: primaryT.stringValue) ?? Decimal(0)
        }

        var videoURL: String?
        if let videos = item["videos"] as? [String: Any],
           let links = videos["links"] as? [[String: Any]],
           let firstLink = links.first {
            videoURL = firstLink["uri"] as? String
        }

        var status = "new"
        if let statusDict = item["status"] as? [String: Any],
           let value = statusDict["status"] as? String {
            status = value
        }

        return SRCRun(
            id: id,
            gameID: gameID,
            gameName: gameName,
            categoryName: categoryName,
            levelName: levelName,
            playerNames: playerNames,
            submitted: item["submitted"] as? String,
            primarySeconds: primary,
            videoURL: videoURL,
            weblink: item["weblink"] as? String,
            status: status
        )
    }

    // MARK: Verify / Reject

    public func setRunStatus(runID: String, verified: Bool, reason: String?) async throws {
        let statusBody: [String: Any]
        if verified {
            statusBody = ["status": ["status": "verified"]]
        } else {
            statusBody = ["status": ["status": "rejected", "reason": reason ?? ""]]
        }
        let body = try JSONSerialization.data(withJSONObject: statusBody)
        let endpoint = try url(path: "/runs/\(runID)/status", query: [:])
        let request = makeRequest(url: endpoint, method: "PUT", body: body)
        let (data, status) = try await send(request)
        guard (200..<300).contains(status) else {
            throw SpeedrunClient.apiError(data: data, status: status)
        }
    }
}
