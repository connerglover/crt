import SwiftUI
import CRTCore

/// The collapsible loads/segments panel (spec §6). The header and labels
/// switch between "Loads" and "Segments" with the session mode; rows are
/// inline-editable cards with a duration chip and a delete button.
@MainActor
struct MarksSidebarView: View {
    @Environment(AppModel.self) private var model

    private var isSegments: Bool {
        model.files.session.mode == .segments
    }

    private var marks: [Load] {
        isSegments ? model.files.session.segments : model.files.session.loads
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            header

            if marks.isEmpty {
                emptyState
            } else {
                ScrollView {
                    VStack(spacing: 8) {
                        ForEach(Array(marks.enumerated()), id: \.offset) { pair in
                            MarkRowView(
                                index: pair.offset,
                                mark: pair.element,
                                isSegment: isSegments
                            )
                            .environment(model)
                            .id("\(isSegments ? "s" : "l")-\(pair.offset)-\(pair.element.startFrame)-\(pair.element.endFrame)")
                        }
                    }
                    .padding(.horizontal, 2)
                }
            }

            Spacer(minLength: 0)

            Button(isSegments ? model.loc["Clear Segments"] : model.loc["Clear Loads"]) {
                model.clearMarks()
            }
            .disabled(marks.isEmpty)
            .frame(maxWidth: .infinity)
        }
        .padding(12)
    }

    private var header: some View {
        HStack {
            Text("\(isSegments ? model.loc["Segments"] : model.loc["Loads"]) (\(marks.count))")
                .font(.headline)
            Spacer()
        }
    }

    private var emptyState: some View {
        VStack(spacing: 6) {
            Image(systemName: isSegments ? "square.split.2x1" : "hourglass")
                .font(.title2)
                .foregroundStyle(.tertiary)
            Text(isSegments
                 ? model.loc.text("No segments yet",
                                  "No segments yet. Mark them in the video retimer or add them from the inputs.")
                 : model.loc.text("No loads yet",
                                  "No loads yet. Enter load frames and press Add Loads."))
                .font(.caption)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 24)
    }
}

/// One inline-editable load/segment card.
@MainActor
struct MarkRowView: View {
    @Environment(AppModel.self) private var model

    let index: Int
    let mark: Load
    let isSegment: Bool

    @State private var startText: String
    @State private var endText: String
    @FocusState private var focusedField: Int?

    init(index: Int, mark: Load, isSegment: Bool) {
        self.index = index
        self.mark = mark
        self.isSegment = isSegment
        _startText = State(initialValue: String(mark.startFrame))
        _endText = State(initialValue: String(mark.endFrame))
    }

    private var durationText: String {
        TimeFormatter.frameTime(
            frames: mark.length,
            framerate: model.files.session.framerate,
            precision: model.files.session.precision
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 6) {
                Text("\(rowTitle) \(index + 1)")
                    .font(.subheadline.weight(.semibold))
                Text(durationText)
                    .font(.caption.monospaced())
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Capsule().fill(Color.accentColor.opacity(0.15)))
                Spacer()
                Button {
                    model.deleteMark(at: index)
                } label: {
                    Image(systemName: "xmark")
                        .font(.caption)
                }
                .buttonStyle(.plain)
                .help(model.loc["Delete"])
            }

            HStack(spacing: 6) {
                Text(model.loc["Start"])
                    .font(.caption)
                    .foregroundStyle(.secondary)
                TextField("0", text: $startText)
                    .textFieldStyle(.roundedBorder)
                    .font(.caption)
                    .frame(width: 62)
                    .focused($focusedField, equals: 0)
                    .onSubmit { commit() }
                Text(model.loc["End"])
                    .font(.caption)
                    .foregroundStyle(.secondary)
                TextField("0", text: $endText)
                    .textFieldStyle(.roundedBorder)
                    .font(.caption)
                    .frame(width: 62)
                    .focused($focusedField, equals: 1)
                    .onSubmit { commit() }
                Spacer(minLength: 0)
            }
        }
        .padding(8)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color.primary.opacity(0.04))
        )
        .onChange(of: focusedField) { oldValue, newValue in
            if oldValue != nil && newValue == nil {
                commit()
            }
        }
    }

    private var rowTitle: String {
        // Singular keys of their own — deriving them by matching the English
        // plural left non-English rows reading "Chargements 1".
        model.loc[isSegment ? "Segment" : "Load"]
    }

    private func commit() {
        if startText != String(mark.startFrame) || endText != String(mark.endFrame) {
            model.editMark(at: index, startText: startText, endText: endText)
        }
    }
}
