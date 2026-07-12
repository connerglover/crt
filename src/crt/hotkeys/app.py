"""Central registry of user-customizable hotkey actions.

Each entry is (action_id, label_content_key, default_shortcut):
  - action_id: the dispatch key used by crt.app.App._dispatch (matches the
    QAction.data() for menu-backed actions, or the paste/add-load keys wired
    directly in App.run() for the plain-button ones).
  - label_content_key: key into the language content dict for the row label
    shown in the hotkeys editor.
  - default_shortcut: default Qt key sequence string (e.g. "Ctrl+S").
"""

import re

HOTKEY_ACTIONS = [
    ("New Time", "New Time", "Ctrl+N"),
    ("Open Time", "Open Time", "Ctrl+O"),
    ("Session History", "Session History", "Ctrl+H"),
    ("Save", "Save", "Ctrl+S"),
    ("Save As", "Save As", "Ctrl+Shift+S"),
    ("Settings", "Settings", "Ctrl+,"),
    ("Copy Mod Note", "Copy Mod Note", "Ctrl+M"),
    ("Copy Discord Message", "Copy Discord Message", "Ctrl+Shift+D"),
    ("Copy YouTube Chapters", "Copy YouTube Chapters", "Ctrl+Shift+Y"),
    ("Clear Loads", "Clear Loads", "Ctrl+Shift+L"),
    ("start_paste", "Paste Start Frame", "Ctrl+1"),
    ("end_paste", "Paste End Frame", "Ctrl+2"),
    ("start_loads_paste", "Paste Start Frame (Loads)", "Ctrl+3"),
    ("end_loads_paste", "Paste End Frame (Loads)", "Ctrl+4"),
    ("Add Loads", "Add Load", "Ctrl+L"),
]

DEFAULT_HOTKEYS = {action_id: default for action_id, _, default in HOTKEY_ACTIONS}

# Actions with a QAction in a menu get their shortcut set directly on the
# QAction (so it also shows next to the menu entry). Everything else is a
# plain button with no QAction backing it, so App.run() binds a standalone
# QShortcut instead.
MENU_ACTION_IDS = {
    "New Time", "Open Time", "Session History", "Save", "Save As", "Settings",
    "Copy Mod Note", "Copy Discord Message", "Copy YouTube Chapters", "Clear Loads",
}


def hotkey_option_name(action_id: str) -> str:
    """Converts an action id into a safe, stable ConfigParser option name."""
    return re.sub(r"[^a-z0-9]+", "_", action_id.lower()).strip("_")


HOTKEY_OPTION_NAMES = {action_id: hotkey_option_name(action_id) for action_id, _, _ in HOTKEY_ACTIONS}
