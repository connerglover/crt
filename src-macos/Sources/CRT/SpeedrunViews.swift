import SwiftUI
import CRTCore

/// The speedrun.com panel on the dashboard (spec §11.2).
struct SpeedrunSection: View {
    @Environment(AppModel.self) private var model

    private var src: SpeedrunModel { model.src }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            header

            if src.isSignedIn {
                runsToVerifySection
                recentRunsSection
            } else {
                signInBox
            }
        }
        .sheet(isPresented: Binding(
            get: { src.rejectTarget != nil },
            set: { presented in
                if !presented {
                    src.cancelReject()
                }
            }
        )) {
            RejectSheet()
                .environment(model)
        }
    }

    private var header: some View {
        HStack(spacing: 10) {
            Text("speedrun.com")
                .font(.title3.bold())
            Spacer()
            if let profile = src.profile {
                if let avatar = profile.avatarURL, let url = URL(string: avatar) {
                    AsyncImage(url: url) { image in
                        image.resizable().scaledToFill()
                    } placeholder: {
                        Color.clear
                    }
                    .frame(width: 22, height: 22)
                    .clipShape(Circle())
                }
                Text(profile.name)
                    .fontWeight(.medium)
                Button(model.loc["Refresh"]) {
                    Task { await src.refreshRuns() }
                }
                .disabled(src.loadingRuns)
                Button(model.loc["Sign Out"]) {
                    src.signOut()
                }
            }
        }
    }

    // MARK: - Signed out (spec §11.2)

    private var signInBox: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(model.loc.text(
                "Sign in explainer",
                "Sign in with your speedrun.com API key to see runs waiting for verification, verify or reject them, and jump straight into retiming."
            ))
            .foregroundStyle(.secondary)
            .fixedSize(horizontal: false, vertical: true)

            HStack(spacing: 8) {
                SecureField(model.loc["API Key"], text: Binding(
                    get: { src.apiKeyInput },
                    set: { src.apiKeyInput = $0 }
                ))
                .textFieldStyle(.roundedBorder)
                .frame(maxWidth: 280)
                .onSubmit {
                    Task { await src.signIn() }
                }

                Button(model.loc["Sign In"]) {
                    Task { await src.signIn() }
                }
                .disabled(src.signingIn || src.apiKeyInput.isEmpty)

                if src.signingIn {
                    ProgressView()
                        .controlSize(.small)
                }

                Button(model.loc["Get your key"]) {
                    src.openAPIKeyPage()
                }
                .buttonStyle(.link)
            }

            if let error = src.signInError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
            }
        }
        .padding(12)
        .background(RoundedRectangle(cornerRadius: 10).fill(Color.primary.opacity(0.04)))
    }

    // MARK: - Runs to verify

    private var runsToVerifySection: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                Text("\(model.loc["Runs to Verify"]) (\(src.runsToVerify.count))")
                    .font(.headline)
                if src.loadingRuns {
                    ProgressView()
                        .controlSize(.small)
                }
            }

            if let error = src.runsError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
            }

            if src.runsToVerify.isEmpty && !src.loadingRuns {
                Text(model.loc.text("No runs to verify", "Nothing waiting for verification. Nice."))
                    .foregroundStyle(.secondary)
                    .padding(.vertical, 6)
            } else {
                VStack(spacing: 6) {
                    ForEach(src.runsToVerify) { run in
                        verifyRow(run)
                    }
                }
            }
        }
    }

    private func verifyRow(_ run: SRCRun) -> some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Text(run.gameName)
                        .fontWeight(.medium)
                    Text(categoryText(run))
                        .foregroundStyle(.secondary)
                }
                HStack(spacing: 6) {
                    Text(run.playerNames.joined(separator: ", "))
                        .font(.caption)
                    if let submitted = run.submitted {
                        Text(submittedText(submitted))
                            .font(.caption)
                            .foregroundStyle(.tertiary)
                    }
                }
            }
            Spacer()
            Text(run.claimedTimeISO)
                .font(.callout.monospaced().weight(.semibold))

            Button(model.loc["Watch"]) {
                src.watch(run)
            }
            .disabled(run.videoURL == nil)

            Button(model.loc["Retime This"]) {
                src.retime(run)
            }
            .disabled(src.pendingRunIDs.contains(run.id))

            Button(model.loc["Verify"]) {
                Task { await src.verify(run) }
            }
            .disabled(src.pendingRunIDs.contains(run.id))

            Button(model.loc["Reject"]) {
                src.beginReject(run)
            }
            .disabled(src.pendingRunIDs.contains(run.id))
        }
        .padding(8)
        .background(RoundedRectangle(cornerRadius: 8).fill(
            src.retimingRunID == run.id ? Color.accentColor.opacity(0.10) : Color.primary.opacity(0.04)))
    }

    private func categoryText(_ run: SRCRun) -> String {
        if let level = run.levelName {
            return "\(run.categoryName) · \(level)"
        }
        return run.categoryName
    }

    private func submittedText(_ iso: String) -> String {
        guard let date = ISO8601DateFormatter().date(from: iso) else { return iso }
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .none
        return formatter.string(from: date)
    }

    // MARK: - My recent runs

    private var recentRunsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(model.loc["My Recent Runs"])
                .font(.headline)

            if src.recentRuns.isEmpty {
                Text(model.loc.text("No recent runs", "No recent runs."))
                    .foregroundStyle(.secondary)
            } else {
                VStack(spacing: 4) {
                    ForEach(src.recentRuns) { run in
                        HStack(spacing: 8) {
                            statusIcon(run.status)
                            Text(run.gameName)
                                .fontWeight(.medium)
                            Text(run.categoryName)
                                .foregroundStyle(.secondary)
                            Spacer()
                            Text(run.claimedTimeISO)
                                .font(.callout.monospaced())
                        }
                        .font(.callout)
                        .padding(.vertical, 3)
                        .padding(.horizontal, 8)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func statusIcon(_ status: String) -> some View {
        switch status {
        case "verified":
            Image(systemName: "checkmark.circle.fill")
                .foregroundStyle(.green)
        case "rejected":
            Image(systemName: "xmark.circle.fill")
                .foregroundStyle(.red)
        default:
            Image(systemName: "hourglass.circle")
                .foregroundStyle(.orange)
        }
    }
}

/// Reject confirmation with a required reason box (prefilled with the mod
/// note when the run is being retimed).
struct RejectSheet: View {
    @Environment(AppModel.self) private var model

    private var src: SpeedrunModel { model.src }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(model.loc["Reject"])
                .font(.title3.bold())
            if let run = src.rejectTarget {
                Text(model.loc.text("Run Summary", "{run} by {players}")
                    .replacingOccurrences(of: "{run}", with: "\(run.gameName) — \(run.categoryName)")
                    .replacingOccurrences(of: "{players}",
                                          with: run.playerNames.joined(separator: ", ")))
                    .foregroundStyle(.secondary)
            }
            Text(model.loc["Reason"])
                .font(.caption.weight(.semibold))
            TextEditor(text: Binding(
                get: { src.rejectReason },
                set: { src.rejectReason = $0 }
            ))
            .font(.body)
            .frame(minHeight: 110)
            .overlay(RoundedRectangle(cornerRadius: 6).strokeBorder(Color.primary.opacity(0.15)))

            HStack {
                Spacer()
                Button(model.loc["Cancel"]) {
                    src.cancelReject()
                }
                .keyboardShortcut(.cancelAction)
                Button(model.loc["Reject"]) {
                    Task { await src.confirmReject() }
                }
                .buttonStyle(.borderedProminent)
                .disabled(src.rejectReason.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                          || src.rejectTarget.map { src.pendingRunIDs.contains($0.id) } == true)
            }
        }
        .padding(20)
        .frame(width: 480)
    }
}
