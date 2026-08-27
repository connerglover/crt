import AppKit
import Foundation
import Observation
import Security
import CRTCore

/// Speedrun.com dashboard state (spec §11.2).
@Observable
@MainActor
final class SpeedrunModel {
    @ObservationIgnored weak var app: AppModel?

    var profile: SRCProfile?
    var apiKeyInput = ""
    var signingIn = false
    var signInError: String?

    var runsToVerify: [SRCRun] = []
    var recentRuns: [SRCRun] = []
    var loadingRuns = false
    var runsError: String?

    /// The run id currently being retimed ("Retime this" was clicked) — used
    /// to prefill verify/reject text with the generated mod note.
    var retimingRunID: String?

    var rejectTarget: SRCRun?
    var rejectReason = ""

    /// Runs with a status change in flight (Verify / Reject), so the row's
    /// buttons can't fire the same PUT twice.
    var pendingRunIDs: Set<String> = []

    /// Wall-clock gate for the dashboard auto-refresh, so navigating away and
    /// back doesn't restart a fresh 5-minute wait every time.
    @ObservationIgnored var lastRefresh: Date?

    /// Cached client — rebuilding it per access re-read the Keychain on the
    /// main thread and threw away every pooled connection. Invalidated on
    /// sign-in / sign-out, the only times the stored key changes.
    @ObservationIgnored private var cachedClient: SpeedrunClient?

    var client: SpeedrunClient {
        if let cachedClient { return cachedClient }
        let fresh = SpeedrunClient(apiKey: KeychainStore.read())
        cachedClient = fresh
        return fresh
    }

    private func invalidateClient() {
        cachedClient = nil
    }

    /// Localization lookup (spec §13).
    private func loc(_ key: String, _ english: String) -> String {
        app?.loc.text(key, english) ?? english
    }

    var isSignedIn: Bool { profile != nil }

    // MARK: - Sign in / out

    /// Silent sign-in from the Keychain at launch.
    @MainActor func restoreSignIn() async {
        guard KeychainStore.read() != nil else { return }
        signingIn = true
        if let restored = try? await client.profile() {
            profile = restored
            await refreshRuns()
        }
        signingIn = false
    }

    @MainActor func signIn() async {
        let key = apiKeyInput.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty else { return }
        signingIn = true
        signInError = nil
        do {
            let validated = try await SpeedrunClient(apiKey: key).profile()
            let status = KeychainStore.write(key)
            if status == errSecSuccess {
                invalidateClient()
                profile = validated
                apiKeyInput = ""
                await refreshRuns()
            } else {
                // Without a stored key every later request goes out
                // unauthenticated, so don't pretend the sign-in worked.
                signInError = loc("Keychain Write Failed",
                                  "Couldn't save the API key to the Keychain.")
            }
        } catch {
            signInError = (error as? CRTError)?.messageText ?? error.localizedDescription
        }
        signingIn = false
    }

    @MainActor func signOut() {
        KeychainStore.delete()
        invalidateClient()
        profile = nil
        runsToVerify = []
        recentRuns = []
        runsError = nil
        rejectTarget = nil
        rejectReason = ""
        retimingRunID = nil
        pendingRunIDs = []
        lastRefresh = nil
    }

    @MainActor func openAPIKeyPage() {
        if let url = URL(string: SpeedrunClient.apiKeyPage) {
            NSWorkspace.shared.open(url)
        }
    }

    // MARK: - Runs to verify (spec §11.2)

    @MainActor func refreshRuns() async {
        guard let profile else { return }
        guard !loadingRuns else { return }
        loadingRuns = true
        runsError = nil
        let apiClient = client
        do {
            let games = try await apiClient.moderatedGames(userID: profile.id)

            // Fetch pending runs per game, at most 4 games in flight. One
            // game failing must not discard the other games' results.
            var collected: [SRCRun] = []
            var failures = 0
            var index = 0
            while index < games.count {
                let upper = min(index + 4, games.count)
                let chunk = Array(games[index..<upper])
                index = upper
                await withTaskGroup(of: Optional<[SRCRun]>.self) { group in
                    for game in chunk {
                        group.addTask {
                            try? await apiClient.newRuns(gameID: game.id, gameName: game.name)
                        }
                    }
                    for await runs in group {
                        if let runs {
                            collected.append(contentsOf: runs)
                        } else {
                            failures += 1
                        }
                    }
                }
            }
            collected.sort { ($0.submitted ?? "") < ($1.submitted ?? "") }
            runsToVerify = collected
            if failures > 0 {
                runsError = loc("Partial Runs Failure", "Couldn't load {failed} of {total} games.")
                    .replacingOccurrences(of: "{failed}", with: String(failures))
                    .replacingOccurrences(of: "{total}", with: String(games.count))
            }
        } catch {
            runsError = (error as? CRTError)?.messageText ?? error.localizedDescription
        }

        // Independent of the verification queue — a failure above must not
        // skip it.
        recentRuns = (try? await apiClient.recentRuns(userID: profile.id)) ?? []

        lastRefresh = Date()
        loadingRuns = false
    }

    /// Auto-refresh every 5 minutes while the dashboard is visible (spec
    /// §11.2). The interval is wall-clock based so switching pages more often
    /// than every 5 minutes still refreshes.
    func autoRefreshLoop() async {
        while !Task.isCancelled {
            await refreshIfDue()
            try? await Task.sleep(nanoseconds: 15_000_000_000)
        }
    }

    @MainActor private func refreshIfDue() async {
        guard profile != nil else { return }
        if let lastRefresh, Date().timeIntervalSince(lastRefresh) < 300 { return }
        await refreshRuns()
    }

    // MARK: - Row actions

    @MainActor func watch(_ run: SRCRun) {
        guard let text = run.videoURL, let url = URL(string: text) else {
            app?.toast(loc("No Video Link", "This run has no video link"))
            return
        }
        NSWorkspace.shared.open(url)
    }

    @MainActor func retime(_ run: SRCRun) {
        guard let app else { return }
        if let videoLink = run.videoURL, YtDlpImporter.isYouTubeURL(videoLink) {
            app.page = .videoRetimer
            app.video.importURLText = videoLink
            app.video.startImport()
            retimingRunID = run.id
        } else {
            // Only mark the run as "retiming" once the flow actually
            // proceeds — Cancel here must leave the row untouched.
            guard app.promptSaveIfDirty(title: app.loc["New Time"]) else { return }
            app.files.newSession(mode: .loads)
            app.clearUndo()
            app.syncInputs()
            app.video.resetMarks()
            app.page = .frameRetimer
            retimingRunID = run.id
        }
    }

    @MainActor func verify(_ run: SRCRun) async {
        guard let app else { return }
        guard !pendingRunIDs.contains(run.id) else { return }
        var message = loc("Verify Run Message", "Verify \"{run}\" by {players}?")
            .replacingOccurrences(of: "{run}", with: "\(run.gameName) — \(run.categoryName)")
            .replacingOccurrences(of: "{players}", with: run.playerNames.joined(separator: ", "))
        if retimingRunID == run.id {
            message += "\n\n" + app.currentModNote()
        }
        guard Alerts.confirmYesNo(title: app.loc["Verify"], message: message) else { return }
        pendingRunIDs.insert(run.id)
        defer { pendingRunIDs.remove(run.id) }
        do {
            try await client.setRunStatus(runID: run.id, verified: true, reason: nil)
            runsToVerify.removeAll { $0.id == run.id }
            if retimingRunID == run.id {
                retimingRunID = nil
            }
            app.toast(loc("Run verified", "Run verified"))
        } catch {
            app.showError(error)
        }
    }

    @MainActor func beginReject(_ run: SRCRun) {
        rejectReason = retimingRunID == run.id ? (app?.currentModNote() ?? "") : ""
        rejectTarget = run
    }

    @MainActor func confirmReject() async {
        guard let app, let run = rejectTarget else { return }
        guard !pendingRunIDs.contains(run.id) else { return }
        pendingRunIDs.insert(run.id)
        defer { pendingRunIDs.remove(run.id) }
        do {
            try await client.setRunStatus(runID: run.id, verified: false, reason: rejectReason)
            runsToVerify.removeAll { $0.id == run.id }
            if retimingRunID == run.id {
                retimingRunID = nil
            }
            rejectTarget = nil
            rejectReason = ""
            app.toast(loc("Run rejected", "Run rejected"))
        } catch {
            app.showError(error)
        }
    }

    @MainActor func cancelReject() {
        rejectTarget = nil
        rejectReason = ""
    }
}
