# Standard library
from typing import NoReturn

# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit,
    QPushButton, QCheckBox, QComboBox, QFrame, QWidget, QSizePolicy,
    QColorDialog
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont, QColor

# Local application
from crt.base_gui import BaseGUI
from crt.hotkeys import DEFAULT_HOTKEYS, HotkeysDialog
from crt.theme import DEFAULT_ACCENT_COLOR


class SettingsDialog(QDialog):
    """Settings dialog for CRT."""

    def __init__(self, settings: dict, content: dict, parent=None, on_top: bool = False):
        super().__init__(parent)
        self.content = content
        self._on_top = on_top
        self._hotkeys = dict(settings.get("hotkeys", DEFAULT_HOTKEYS))
        self.setWindowTitle("CRT Settings")
        self.setFixedWidth(500)
        self.setWindowModality(Qt.WindowModality.ApplicationModal)
        if on_top:
            self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self._build_ui(settings, content)

    def _build_ui(self, settings: dict, content: dict):
        c = content
        layout = QVBoxLayout(self)
        layout.setContentsMargins(20, 16, 20, 16)
        layout.setSpacing(12)

        # Title
        title = QLabel(c["CRT Settings"])
        title.setProperty("cssClass", "heading")
        title.setFont(QFont("Segoe UI", 18, QFont.Weight.Bold))
        layout.addWidget(title)

        # Enable updates checkbox
        row0 = QHBoxLayout()
        spacer0 = QWidget(); spacer0.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        row0.addWidget(spacer0)
        self.enable_updates = QCheckBox(c["Automatically Check for Updates"])
        self.enable_updates.setObjectName("enable_updates")
        self.enable_updates.setChecked(settings.get("enable_updates", True))
        self.enable_updates.setFont(QFont("Segoe UI", 12))
        row0.addWidget(self.enable_updates)
        layout.addLayout(row0)

        # Theme
        row1 = QHBoxLayout()
        row1.setSpacing(8)
        spacer1 = QWidget(); spacer1.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        row1.addWidget(spacer1)
        lbl_theme = QLabel(c["Theme"])
        lbl_theme.setFont(QFont("Segoe UI", 13))
        row1.addWidget(lbl_theme)
        self.theme = QComboBox()
        self.theme.setObjectName("theme")
        self.theme.setFont(QFont("Segoe UI", 12))
        self.theme.addItems([c["Automatic"], c["Dark"], c["Light"]])
        current_theme = settings.get("theme", "Automatic")
        # Try to match stored English value to localized display
        for i, text in enumerate([c["Automatic"], c["Dark"], c["Light"]]):
            if text == current_theme or ["Automatic", "Dark", "Light"][i] == current_theme:
                self.theme.setCurrentIndex(i)
                break
        row1.addWidget(self.theme)
        layout.addLayout(row1)

        # Accent color
        row1b = QHBoxLayout()
        row1b.setSpacing(8)
        spacer1b = QWidget(); spacer1b.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        row1b.addWidget(spacer1b)
        lbl_accent = QLabel(c["Accent Color"])
        lbl_accent.setFont(QFont("Segoe UI", 13))
        row1b.addWidget(lbl_accent)
        self._accent_color = settings.get("accent_color", DEFAULT_ACCENT_COLOR)
        self.accent_color_button = QPushButton(self._accent_color)
        self.accent_color_button.setObjectName("accent_color")
        self.accent_color_button.setFont(QFont("Segoe UI", 12))
        self.accent_color_button.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        self.accent_color_button.setMinimumWidth(90)
        self.accent_color_button.clicked.connect(self._pick_accent_color)
        self._update_accent_button()
        row1b.addWidget(self.accent_color_button)
        layout.addLayout(row1b)

        # Language
        row2 = QHBoxLayout()
        row2.setSpacing(8)
        spacer2 = QWidget(); spacer2.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        row2.addWidget(spacer2)
        lbl_lang = QLabel(c["Language"])
        lbl_lang.setFont(QFont("Segoe UI", 13))
        row2.addWidget(lbl_lang)
        self.language = QComboBox()
        self.language.setObjectName("language")
        self.language.setFont(QFont("Segoe UI", 12))
        self.language.addItems(["English", "Español", "Français", "Polski"])
        lang_map = {"en": "English", "es": "Español", "fr": "Français", "pl": "Polski"}
        stored_lang = settings.get("language", "en")
        display_lang = lang_map.get(stored_lang, stored_lang)
        idx = self.language.findText(display_lang)
        if idx >= 0:
            self.language.setCurrentIndex(idx)
        row2.addWidget(self.language)
        layout.addLayout(row2)

        # Mod note format
        row3 = QHBoxLayout()
        row3.setSpacing(8)
        spacer3 = QWidget(); spacer3.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        row3.addWidget(spacer3)
        lbl_mod = QLabel(c["Mod Note Format"])
        lbl_mod.setFont(QFont("Segoe UI", 13))
        row3.addWidget(lbl_mod)
        self.mod_note_format = QLineEdit(settings.get("mod_note_format", ""))
        self.mod_note_format.setObjectName("mod_note_format")
        self.mod_note_format.setFont(QFont("Segoe UI", 11))
        self.mod_note_format.setMinimumWidth(220)
        row3.addWidget(self.mod_note_format)
        layout.addLayout(row3)

        # Hotkeys
        row4 = QHBoxLayout()
        row4.setSpacing(8)
        spacer4 = QWidget(); spacer4.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        row4.addWidget(spacer4)
        self.btn_hotkeys = QPushButton(c.get("Customize Hotkeys", "Customize Hotkeys") + "...")
        self.btn_hotkeys.setObjectName("Customize Hotkeys")
        self.btn_hotkeys.setFont(QFont("Segoe UI", 12))
        self.btn_hotkeys.clicked.connect(self._open_hotkeys_dialog)
        row4.addWidget(self.btn_hotkeys)
        layout.addLayout(row4)

        # Separator
        sep = QFrame()
        sep.setFrameShape(QFrame.Shape.HLine)
        sep.setFrameShadow(QFrame.Shadow.Sunken)
        layout.addWidget(sep)

        # Buttons
        btn_row = QHBoxLayout()
        btn_row.setSpacing(8)
        self.btn_restore = QPushButton(c["Restore Defaults"])
        self.btn_restore.setObjectName("Restore Defaults")
        self.btn_apply = QPushButton(c["Apply"])
        self.btn_apply.setObjectName("Apply")
        self.btn_apply.setProperty("cssClass", "primary")
        self.btn_cancel = QPushButton(c["Cancel"])
        self.btn_cancel.setObjectName("Cancel")
        for btn in (self.btn_restore, self.btn_apply, self.btn_cancel):
            btn.setFont(QFont("Segoe UI", 12))
            btn.setMinimumHeight(34)
            btn_row.addWidget(btn)
        layout.addLayout(btn_row)

    def _update_accent_button(self) -> NoReturn:
        """Refreshes the accent color button's swatch color, label, and text contrast."""
        color = QColor(self._accent_color)
        # Standard relative luminance threshold for picking readable text on a color swatch.
        luminance = 0.299 * color.red() + 0.587 * color.green() + 0.114 * color.blue()
        text_color = "#000000" if luminance > 140 else "#ffffff"
        self.accent_color_button.setText(self._accent_color)
        self.accent_color_button.setStyleSheet(
            f"background-color: {self._accent_color}; color: {text_color}; "
            f"border: 1px solid {text_color}; padding: 6px;"
        )
        self.accent_color_button.adjustSize()

    def _pick_accent_color(self) -> NoReturn:
        """Opens a hex color picker dialog and stores the chosen accent color."""
        color = QColorDialog.getColor(QColor(self._accent_color), self, "Select Accent Color")
        if color.isValid():
            self._accent_color = color.name()
            self._update_accent_button()

    def _open_hotkeys_dialog(self) -> NoReturn:
        """Opens the hotkeys rebinding dialog and stores the result if confirmed."""
        dialog = HotkeysDialog(self._hotkeys, self.content, self, self._on_top)
        if dialog.exec() == QDialog.DialogCode.Accepted:
            self._hotkeys = dialog.get_values()

    def get_values(self) -> dict:
        """Returns current widget values as a dict compatible with the Settings controller."""
        return {
            "enable_updates": self.enable_updates.isChecked(),
            "theme": self.theme.currentText(),
            "accent_color": self._accent_color,
            "language": self.language.currentText(),
            "mod_note_format": self.mod_note_format.text(),
            "hotkeys": self._hotkeys,
        }


class SettingsGUI(BaseGUI):
    """Wrapper around SettingsDialog to match the BaseGUI interface."""

    def __init__(self, settings: dict, content: dict, parent=None, on_top: bool = False):
        self.window = SettingsDialog(settings, content, parent, on_top)
        self._connect_signals()

    def _connect_signals(self):
        d = self.window
        d.btn_restore.clicked.connect(lambda: self._emit("Restore Defaults", self.window.get_values()))
        d.btn_apply.clicked.connect(lambda: self._emit("Apply", self.window.get_values()))
        d.btn_cancel.clicked.connect(lambda: self._emit("Cancel", self.window.get_values()))
