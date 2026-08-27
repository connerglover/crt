import SwiftUI
import CRTCore

private enum RetimerInput: Hashable {
    case framerate
    case start
    case end
    case loadStart
    case loadEnd
    case segStart
    case segEnd
}

struct FrameRetimerView: View {
    @Environment(AppModel.self) private var model
    @FocusState private var focusedInput: RetimerInput?

    private static let quickPicks = ["24", "25", "29.97", "30", "50", "59.94", "60"]

    var body: some View {
        HStack(spacing: 0) {
            mainColumn
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)

            if !model.sidebarCollapsed {
                Divider()
                MarksSidebarView()
                    .environment(model)
                    .frame(width: 250)
            }
        }
        .navigationTitle(windowTitle)
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                Button {
                    model.sidebarCollapsed.toggle()
                } label: {
                    Image(systemName: "sidebar.trailing")
                }
                .help(model.loc[model.files.session.mode == .segments ? "Segments" : "Loads"])
            }
        }
        .onChange(of: focusedInput) { oldValue, _ in
            commit(oldValue)
        }
    }

    private var windowTitle: String {
        var title = "CRT"
        if let path = model.files.filePath {
            title += " — " + URL(fileURLWithPath: path).lastPathComponent
        }
        if model.files.dirty {
            title += " •"
        }
        return title
    }

    private var mainColumn: some View {
        ScrollView {
            VStack(spacing: 16) {
                modePicker
                timeCards
                inputRows
                actionButtons
                if model.busyLookup {
                    HStack(spacing: 6) {
                        ProgressView()
                            .controlSize(.small)
                        Text(model.loc.text("Checking Framerate", "Checking the video's framerate…"))
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
            }
            .padding(20)
        }
    }

    // MARK: - Mode (spec §1.1)

    private var modePicker: some View {
        Picker("", selection: Binding(
            get: { model.files.session.mode },
            set: { newMode in
                if newMode != model.files.session.mode {
                    model.toggleMode()
                }
            }
        )) {
            Text(model.loc["Loads"]).tag(TimingMode.loads)
            Text(model.loc["Segments"]).tag(TimingMode.segments)
        }
        .pickerStyle(.segmented)
        .frame(width: 260)
    }

    // MARK: - Time cards (click to copy)

    private var timeCards: some View {
        let session = model.files.session
        let isSegments = session.mode == .segments
        return HStack(spacing: 14) {
            TimeCard(
                title: isSegments ? model.loc["Segment Total"] : model.loc["Without Loads"],
                value: session.isoWithoutLoads(),
                hint: model.loc["Click to Copy Time"]
            ) {
                model.copyTimeWithoutLoads()
            }
            TimeCard(
                title: isSegments ? model.loc["Full Run"] : model.loc["With Loads"],
                value: session.isoWithLoads(),
                hint: model.loc["Click to Copy Time"]
            ) {
                model.copyTimeWithLoads()
            }
        }
    }

    // MARK: - Inputs (spec §6)

    @ViewBuilder
    private var inputRows: some View {
        let isSegments = model.files.session.mode == .segments
        VStack(spacing: 10) {
            framerateRow

            if isSegments {
                frameRow(label: model.loc["Segment Start"], field: .segStart, input: .segStart)
                frameRow(label: model.loc["Segment End"], field: .segEnd, input: .segEnd)
            } else {
                frameRow(label: model.loc["Start Frame"], field: .start, input: .start)
                frameRow(label: model.loc["End Frame"], field: .end, input: .end)
                frameRow(label: model.loc["Start Frame (Loads)"], field: .loadStart, input: .loadStart)
                frameRow(label: model.loc["End Frame (Loads)"], field: .loadEnd, input: .loadEnd)
            }
        }
        .frame(maxWidth: 520)
    }

    private var framerateRow: some View {
        HStack(spacing: 8) {
            Text(model.loc["Framerate"])
                .frame(width: 180, alignment: .trailing)
            TextField("60", text: Binding(
                get: { model.framerateText },
                set: { model.framerateText = $0 }
            ))
            .textFieldStyle(.roundedBorder)
            .frame(width: 120)
            .focused($focusedInput, equals: .framerate)
            .onSubmit { model.commitFramerate() }

            Menu {
                ForEach(FrameRetimerView.quickPicks, id: \.self) { pick in
                    Button(pick) {
                        model.framerateText = pick
                        model.commitFramerate()
                    }
                }
            } label: {
                Image(systemName: "chevron.down")
            }
            .menuStyle(.borderlessButton)
            .frame(width: 28)

            Spacer(minLength: 0)
        }
    }

    private func frameRow(label: String, field: FrameField, input: RetimerInput) -> some View {
        HStack(spacing: 8) {
            Text(label)
                .frame(width: 180, alignment: .trailing)
            TextField("0", text: Binding(
                get: { model.text(for: field) },
                set: { newValue in setText(newValue, for: field) }
            ))
            .textFieldStyle(.roundedBorder)
            .frame(width: 160)
            .focused($focusedInput, equals: input)
            .onSubmit { model.commitFrameField(field) }

            Button(model.loc["Paste"]) {
                model.paste(into: field)
            }

            Spacer(minLength: 0)
        }
    }

    private func setText(_ newValue: String, for field: FrameField) {
        switch field {
        case .start: model.startText = newValue
        case .end: model.endText = newValue
        case .loadStart: model.loadStartText = newValue
        case .loadEnd: model.loadEndText = newValue
        case .segStart: model.segStartText = newValue
        case .segEnd: model.segEndText = newValue
        }
    }

    private func commit(_ input: RetimerInput?) {
        switch input {
        case .framerate:
            model.commitFramerate()
        case .start:
            model.commitFrameField(.start)
        case .end:
            model.commitFrameField(.end)
        case .loadStart:
            model.commitFrameField(.loadStart)
        case .loadEnd:
            model.commitFrameField(.loadEnd)
        case .segStart:
            model.commitFrameField(.segStart)
        case .segEnd:
            model.commitFrameField(.segEnd)
        case nil:
            break
        }
    }

    // MARK: - Action buttons

    private var actionButtons: some View {
        let isSegments = model.files.session.mode == .segments
        return HStack(spacing: 12) {
            // Split button: Copy Mod Note + dropdown (spec §6).
            Menu {
                Button(model.loc["Copy Discord Message"]) {
                    model.copyDiscordMessage()
                }
                Button(model.loc["Copy YouTube Chapters"]) {
                    model.copyYouTubeChapters()
                }
            } label: {
                Text(model.loc["Copy Mod Note"])
            } primaryAction: {
                model.copyModNote()
            }
            .frame(width: 220)

            if isSegments {
                // No explicit focus commit here: clicking a button does not
                // move focus on macOS, and `addSegment`/`addLoads` run the
                // raw field text through the §2 parser themselves.
                Button(model.loc["Add Segment"]) {
                    model.addSegment()
                }
            } else {
                Button(model.loc["Add Loads"]) {
                    model.addLoads()
                }
            }
        }
    }
}

/// A large click-to-copy time display card.
struct TimeCard: View {
    let title: String
    let value: String
    let hint: String
    let onCopy: () -> Void

    @State private var hovering = false

    var body: some View {
        VStack(spacing: 6) {
            Text(title.uppercased())
                .font(.caption.weight(.semibold))
                .foregroundStyle(.secondary)
            Text(value)
                .font(.system(size: 34, weight: .semibold, design: .monospaced))
                .monospacedDigit()
                .lineLimit(1)
                .minimumScaleFactor(0.5)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 18)
        .padding(.horizontal, 12)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(hovering ? Color.accentColor.opacity(0.08) : Color.primary.opacity(0.04))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .strokeBorder(Color.primary.opacity(0.08), lineWidth: 1)
        )
        .contentShape(Rectangle())
        .onTapGesture {
            onCopy()
        }
        .onHover { inside in
            hovering = inside
        }
        .help(hint)
    }
}
