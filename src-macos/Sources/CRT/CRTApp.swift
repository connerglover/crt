import AppKit
import SwiftUI
import CRTCore

@main
struct CRTApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        WindowGroup("CRT") {
            ContentView()
                .environment(AppModel.shared)
        }
        .defaultSize(width: 1080, height: 720)
        .commands {
            CRTCommands()
        }

        Settings {
            SettingsView()
                .environment(AppModel.shared)
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        let model = AppModel.shared
        // Spec §6: Save / Don't Save / Cancel before exit, like every other
        // destructive transition. The autosave is only deleted once the
        // session is actually clean, so an abandoned save panel still leaves
        // a crash-restore file behind.
        guard model.promptSaveIfDirty(title: model.loc["Exit"]) else { return .terminateCancel }
        if !model.files.dirty {
            model.autosave.clear()
        }
        return .terminateNow
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }
}
