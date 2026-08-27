import SwiftUI
import CRTCore

/// The startup Dashboard (spec §11): quick actions, run library, and the
/// speedrun.com moderation panel.
struct DashboardView: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 20) {
                quickActions
                if model.files.dirty {
                    dirtySessionTile
                }
                librarySection
                SpeedrunSection()
                    .environment(model)
            }
            .padding(20)
        }
        .navigationTitle(model.loc["Dashboard"])
        .task {
            await model.src.autoRefreshLoop()
        }
    }

    // MARK: - Quick actions (spec §11.1)

    private var quickActions: some View {
        HStack(spacing: 10) {
            Button {
                model.newTime()
            } label: {
                Label(model.loc["New Retime"], systemImage: "plus")
            }
            .buttonStyle(.borderedProminent)

            Button {
                model.openTime()
            } label: {
                Label(model.loc["Open File…"], systemImage: "folder")
            }

            Button {
                model.page = .videoRetimer
            } label: {
                Label(model.loc["Import Video"], systemImage: "film")
            }

            Spacer()
        }
    }

    private var dirtySessionTile: some View {
        let session = model.files.session
        return Button {
            model.page = .frameRetimer
        } label: {
            HStack(spacing: 12) {
                Image(systemName: "pencil.circle.fill")
                    .font(.title2)
                VStack(alignment: .leading, spacing: 2) {
                    Text(model.loc.text("Unsaved Session", "Current session (unsaved changes)"))
                        .fontWeight(.semibold)
                    Text("\(session.isoWithoutLoads())  ·  \(session.mode == .segments ? model.loc["Segments"] : model.loc["Loads"])")
                        .font(.caption.monospaced())
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Image(systemName: "chevron.right")
                    .foregroundStyle(.secondary)
            }
            .padding(12)
            .background(RoundedRectangle(cornerRadius: 10).fill(Color.accentColor.opacity(0.10)))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    // MARK: - Run library (spec §11.1)

    private var librarySection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(model.loc["Run Library"])
                .font(.title3.bold())

            if model.libraryEntries.isEmpty {
                Text(model.loc.text("Empty Library", "Runs you open or save will show up here."))
                    .foregroundStyle(.secondary)
                    .padding(.vertical, 12)
            } else {
                VStack(spacing: 6) {
                    ForEach(model.libraryEntries) { entry in
                        libraryRow(entry)
                    }
                }
            }
        }
    }

    private func libraryRow(_ entry: RunLibraryEntry) -> some View {
        HStack(spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                Text(entry.title)
                    .fontWeight(.medium)
                HStack(spacing: 6) {
                    if !entry.game.isEmpty {
                        Text(entry.game)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    Text(entry.mode == TimingMode.segments.rawValue
                         ? model.loc["Segments"] : model.loc["Loads"])
                        .font(.caption2.weight(.semibold))
                        .padding(.horizontal, 6)
                        .padding(.vertical, 1)
                        .background(Capsule().fill(Color.accentColor.opacity(0.15)))
                    Text(formattedDate(entry.modified))
                        .font(.caption)
                        .foregroundStyle(.tertiary)
                }
            }
            Spacer()
            VStack(alignment: .trailing, spacing: 0) {
                Text(entry.timeWithoutLoads)
                    .font(.title3.monospaced().weight(.semibold))
                Text(entry.timeWithLoads)
                    .font(.caption.monospaced())
                    .foregroundStyle(.secondary)
            }

            Menu {
                Button(model.loc["Open Time"]) {
                    model.openPathPrompting(entry.path)
                }
                Button(model.loc["Copy Mod Note"]) {
                    model.copyModNote(forPath: entry.path)
                }
                Button(model.loc.text("Reveal in Explorer", "Reveal in Finder")) {
                    model.revealInFinder(entry.path)
                }
                Divider()
                Button(model.loc["Remove from Library"]) {
                    model.removeFromLibrary(entry.path)
                }
            } label: {
                Image(systemName: "ellipsis.circle")
            }
            .menuStyle(.borderlessButton)
            .frame(width: 30)
        }
        .padding(10)
        .background(RoundedRectangle(cornerRadius: 10).fill(Color.primary.opacity(0.04)))
        .contentShape(Rectangle())
        .onTapGesture(count: 2) {
            model.openPathPrompting(entry.path)
        }
    }

    private func formattedDate(_ iso: String) -> String {
        guard let date = ISO8601DateFormatter().date(from: iso) else { return iso }
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        return formatter.string(from: date)
    }
}
