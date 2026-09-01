"""Smoke checks for dialogs, settings fallthrough and frame parsing.

Run: python test_dialogs.py
"""
import os
import sys
from decimal import Decimal as d

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
sys.path.insert(0, "src")

from PySide6.QtCore import QStandardPaths, QTimer
from PySide6.QtWidgets import QApplication, QMessageBox

from crt.app import App
from crt.app_gui import MainWindow
from crt.settings import RESTORE_DEFAULTS, Settings, SettingsDialog
from crt.hotkeys import HOTKEY_ACTIONS
from crt.language import content_for
from crt.frame_input import clean_framerate, debug_info_to_frame, parse_frame_input
from crt.popups import popup_yes_no, popup_yes_no_cancel
from crt.session_history import SessionHistoryDialog

app = QApplication([])
app.setOrganizationName("CRT")
app.setApplicationName("CRT")
QStandardPaths.setTestModeEnabled(True)  # Keep the real user config untouched.
content = content_for("en")

# Framerates and frame inputs: digits and at most one decimal point survive.
assert clean_framerate("29.97fps") == d("29.97")
assert clean_framerate("1.2.3") == d("1.23")
assert clean_framerate("30.") == d(30)
assert clean_framerate("nope") == d(0)
assert parse_frame_input("100", d(30)) == 100
assert parse_frame_input("1.5", d(30)) == 45     # Seconds, not frames.
assert parse_frame_input("1.5", d(0)) == 0
assert parse_frame_input("", d(30)) == 0
assert debug_info_to_frame(d(30), 'x {"cmt":"2.5"}') == 75
for bad in ("no json here", '{"missing":"cmt"}'):
    try:
        debug_info_to_frame(d(30), bad)
        raise AssertionError(bad)
    except ValueError:
        pass

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

# Popups return the button the user pressed.
def _click(button):
    def go():
        for widget in app.topLevelWidgets():
            if isinstance(widget, QMessageBox) and widget.isVisible():
                widget.button(button).click()
    QTimer.singleShot(0, go)


_click(QMessageBox.StandardButton.Yes)
assert popup_yes_no("t", "m") is True
_click(QMessageBox.StandardButton.No)
assert popup_yes_no_cancel("t", "m") == "no"
_click(QMessageBox.StandardButton.Cancel)
assert popup_yes_no_cancel("t", "m") == "cancel"

# Settings: defaults fall through for anything the config file doesn't set,
# and nothing is written to disk until Apply.
config_dir = QStandardPaths.writableLocation(QStandardPaths.StandardLocation.AppConfigLocation)
os.makedirs(config_dir, exist_ok=True)
config_path = os.path.join(config_dir, "settings.ini")
if os.path.exists(config_path):
    os.remove(config_path)

stored = Settings()
assert not os.path.exists(config_path), "startup must not write a config file"
assert stored.config_to_dict()["hotkeys"]["Save"] == "Ctrl+S"

with open(config_path, "w") as file:
    file.write("[Settings]" + chr(10) + "theme = Dark" + chr(10))
values = Settings().config_to_dict()
assert values["theme"] == "Dark" and values["language"] == "en", values
assert values["hotkeys"]["Add Loads"] == "Ctrl+L"
os.remove(config_path)

# Every menu entry and hotkey binds to a real handler.
app_obj = App.__new__(App)
app_obj.window = MainWindow(content)
handlers = app_obj._action_handlers()
assert set(app_obj.window.menu_actions) <= set(handlers), set(app_obj.window.menu_actions) - set(handlers)
assert {action_id for action_id, _, _ in HOTKEY_ACTIONS} <= set(handlers)

print("ok")
