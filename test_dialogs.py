"""Smoke check for the modal dialog return values. Run: python test_dialogs.py"""
import os
import sys

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
sys.path.insert(0, "src")

from PySide6.QtCore import QTimer
from PySide6.QtWidgets import QApplication

from crt.app.app import App
from crt.app.gui import MainWindow
from crt.app_settings.gui import RESTORE_DEFAULTS, SettingsDialog
from crt.hotkeys import HOTKEY_ACTIONS
from crt.language import Language
from crt.session_history.gui import SessionHistoryDialog

app = QApplication([])
content = Language("en").content

# Session history: activating an item returns that path, closing returns None.
dialog = SessionHistoryDialog(["a.json", "b.json"], content)
QTimer.singleShot(0, lambda: dialog.list_widget.itemActivated.emit(dialog.list_widget.item(1)))
assert dialog.run() == "b.json"

dialog = SessionHistoryDialog(["a.json"], content)
QTimer.singleShot(0, dialog.reject)
assert dialog.run() is None

# Settings: each button maps to its own exec() code.
settings = {"enable_updates": True, "theme": "Dark", "accent_color": "#ff0000",
            "language": "en", "mod_note_format": "x", "hotkeys": {}}
for button, expected in (("btn_apply", 1), ("btn_cancel", 0), ("btn_restore", RESTORE_DEFAULTS)):
    dialog = SettingsDialog(settings, content)
    QTimer.singleShot(0, getattr(dialog, button).click)
    assert dialog.exec() == expected, button
    assert dialog.get_values()["accent_color"] == "#ff0000"

# Every menu entry and hotkey binds to a real handler.
app_obj = App.__new__(App)
app_obj.window = MainWindow(content)
handlers = app_obj._action_handlers()
assert set(app_obj.window.menu_actions) <= set(handlers), set(app_obj.window.menu_actions) - set(handlers)
assert {action_id for action_id, _, _ in HOTKEY_ACTIONS} <= set(handlers)

print("ok")
