"""
Customizable hotkeys for CRT.
"""

from crt.hotkeys.app import (
    DEFAULT_HOTKEYS, HOTKEY_ACTIONS, HOTKEY_OPTION_NAMES, MENU_ACTION_IDS, hotkey_option_name
)
from crt.hotkeys.gui import HotkeysDialog

__name__ = "crt.hotkeys"
__author__ = "Conner Glover"
__description__ = "Customizable hotkeys for CRT."
__all__ = [
    "DEFAULT_HOTKEYS", "HOTKEY_ACTIONS", "HOTKEY_OPTION_NAMES", "MENU_ACTION_IDS",
    "hotkey_option_name", "HotkeysDialog",
]
