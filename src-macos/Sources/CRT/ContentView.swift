import SwiftUI
import CRTCore

struct ContentView: View {
    @Environment(AppModel.self) private var model

    private var accentColor: Color {
        // Observed mirror, not `settings` — SettingsService publishes nothing,
        // so reading it here would leave the tint stale after Apply.
        Color(hexString: model.accentColorHex)
    }

    var body: some View {
        NavigationSplitView {
            sidebar
        } detail: {
            detail
        }
        .frame(minWidth: 1000, minHeight: 620)
        .background(WindowAccessor { window in
            AppModel.shared.attachWindow(window)
        })
        .safeAreaInset(edge: .top, spacing: 0) {
            if model.updateVersion != nil {
                updateBanner
            }
        }
        .overlay(alignment: .bottom) {
            toastOverlay
        }
        .sheet(isPresented: Binding(
            get: { model.showHistorySheet },
            set: { model.showHistorySheet = $0 }
        )) {
            SessionHistorySheet()
                .environment(model)
        }
        .sheet(isPresented: Binding(
            get: { model.toolDownloadName != nil },
            set: { presented in
                if !presented {
                    model.cancelToolDownload()
                }
            }
        )) {
            toolDownloadSheet
        }
        .tint(accentColor)
        .task {
            await model.onLaunch()
        }
    }

    // MARK: - Sidebar

    private var sidebar: some View {
        List(selection: Binding<AppPage?>(
            get: { model.page },
            set: { newValue in
                if let newValue {
                    model.page = newValue
                }
            }
        )) {
            Label(model.loc["Dashboard"], systemImage: "square.grid.2x2")
                .badge(model.src.isSignedIn ? model.src.runsToVerify.count : 0)
                .tag(AppPage.dashboard)
            Label(model.loc["Frame Retimer"], systemImage: "timer")
                .tag(AppPage.frameRetimer)
            Label(model.loc["Video Retimer"], systemImage: "film")
                .tag(AppPage.videoRetimer)
        }
        .navigationSplitViewColumnWidth(min: 190, ideal: 210, max: 260)
        .listStyle(.sidebar)
    }

    @ViewBuilder
    private var detail: some View {
        switch model.page {
        case .dashboard:
            DashboardView()
                .environment(model)
        case .frameRetimer:
            FrameRetimerView()
                .environment(model)
        case .videoRetimer:
            VideoRetimerView()
                .environment(model)
        }
    }

    // MARK: - Update banner (spec §6)

    private var updateBanner: some View {
        HStack(spacing: 8) {
            Image(systemName: "arrow.down.circle.fill")
            Button {
                model.openReleasesPage()
            } label: {
                Text(model.loc.text("Update Available",
                                    "A new version ({version}) is available — click to download.")
                    .replacingOccurrences(of: "{version}", with: model.updateVersion ?? ""))
                    .underline()
            }
            .buttonStyle(.plain)
            Spacer()
            Button {
                model.updateVersion = nil
            } label: {
                Image(systemName: "xmark")
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 6)
        .background(accentColor.opacity(0.18))
    }

    // MARK: - Toast (spec §14)

    @ViewBuilder
    private var toastOverlay: some View {
        if let message = model.toastMessage {
            Text(message)
                .font(.callout)
                .padding(.horizontal, 14)
                .padding(.vertical, 8)
                .background(.regularMaterial, in: Capsule())
                .overlay(Capsule().strokeBorder(.separator, lineWidth: 0.5))
                .padding(.bottom, 16)
                .transition(.move(edge: .bottom).combined(with: .opacity))
        }
    }

    // MARK: - Tool download progress (spec §8)

    private var toolDownloadSheet: some View {
        VStack(spacing: 12) {
            Text(model.loc.text("Downloading {tool}", "Downloading {tool}…")
                .replacingOccurrences(of: "{tool}", with: model.toolDownloadName ?? ""))
                .font(.headline)
            ProgressView(value: model.toolDownloadProgress ?? 0, total: 1.0)
                .frame(width: 260)
            Text(model.loc.text("Tool Download Note",
                                "This only happens once — the tool is kept in CRT's folder."))
                .font(.caption)
                .foregroundStyle(.secondary)
            Button(model.loc["Cancel"]) {
                model.cancelToolDownload()
            }
            .keyboardShortcut(.cancelAction)
        }
        .padding(24)
        .frame(width: 340)
    }
}

/// In-app session history list (spec §6).
struct SessionHistorySheet: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(model.loc["Session History"])
                .font(.title3.bold())

            // This session's paths plus the persisted recents (spec §6).
            let history = Array(model.files.history().reversed())
            let recents = model.recentPaths.filter {
                $0 != model.files.filePath && !history.contains($0)
            }
            if history.isEmpty && recents.isEmpty {
                Text(model.loc.text("Empty History", "No other files were opened this session."))
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, minHeight: 120)
            } else {
                List {
                    if !history.isEmpty {
                        Section(model.loc["This Session"]) {
                            ForEach(history, id: \.self) { path in
                                pathRow(path)
                            }
                        }
                    }
                    if !recents.isEmpty {
                        Section(model.loc["Recent Files"]) {
                            ForEach(recents, id: \.self) { path in
                                pathRow(path)
                            }
                        }
                    }
                }
                .frame(minHeight: 180)
            }

            HStack {
                Spacer()
                Button(model.loc["Cancel"]) {
                    model.showHistorySheet = false
                }
                .keyboardShortcut(.cancelAction)
            }
        }
        .padding(20)
        .frame(width: 460, height: 320)
    }

    private func pathRow(_ path: String) -> some View {
        Button {
            model.openFromHistory(path)
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(URL(fileURLWithPath: path).lastPathComponent)
                    .fontWeight(.medium)
                Text(path)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                    .truncationMode(.middle)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}
