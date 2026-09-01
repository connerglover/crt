# Standard library
import os
from configparser import ConfigParser, ParsingError

# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QFormLayout, QLabel, QLineEdit,
    QPushButton, QCheckBox, QComboBox, QFrame, QSizePolicy,
    QColorDialog
)
from PySide6.QtCore import QStandardPaths, Qt
from PySide6.QtGui import QFont, QColor

# Local application
from crt.hotkeys import DEFAULT_HOTKEYS, HotkeysDialog
from crt.language import LANGUAGE_NAMES, content_for
from crt.popups import popup_yes_no as _popup_yes_no
from crt.theme import DEFAULT_ACCENT_COLOR


RESTORE_DEFAULTS = 2  # Custom QDialog.done() code, alongside Accepted/Rejected.


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

        form = QFormLayout()
        form.setSpacing(8)
        form.setLabelAlignment(Qt.AlignmentFlag.AlignRight)
        layout.addLayout(form)

        def label(text: str) -> QLabel:
            lbl = QLabel(text)
            lbl.setFont(QFont("Segoe UI", 13))
            return lbl

        self.enable_updates = QCheckBox(c["Automatically Check for Updates"])
        self.enable_updates.setObjectName("enable_updates")
        self.enable_updates.setChecked(settings.get("enable_updates", True))
        self.enable_updates.setFont(QFont("Segoe UI", 12))
        form.addRow(self.enable_updates)

        self.theme = QComboBox()
        self.theme.setObjectName("theme")
        self.theme.setFont(QFont("Segoe UI", 12))
        # Localized label shown, English name stored as the item's data.
        for name in ("Automatic", "Dark", "Light"):
            self.theme.addItem(c[name], name)
        self.theme.setCurrentIndex(max(0, self.theme.findData(settings.get("theme", "Automatic"))))
        form.addRow(label(c["Theme"]), self.theme)

        self._accent_color = settings.get("accent_color", DEFAULT_ACCENT_COLOR)
        self.accent_color_button = QPushButton(self._accent_color)
        self.accent_color_button.setObjectName("accent_color")
        self.accent_color_button.setFont(QFont("Segoe UI", 12))
        self.accent_color_button.setSizePolicy(QSizePolicy.Policy.Fixed, QSizePolicy.Policy.Fixed)
        self.accent_color_button.setMinimumWidth(90)
        self.accent_color_button.clicked.connect(self._pick_accent_color)
        self._update_accent_button()
        form.addRow(label(c["Accent Color"]), self.accent_color_button)

        self.language = QComboBox()
        self.language.setObjectName("language")
        self.language.setFont(QFont("Segoe UI", 12))
        for code, name in LANGUAGE_NAMES.items():
            self.language.addItem(name, code)
        self.language.setCurrentIndex(max(0, self.language.findData(settings.get("language", "en"))))
        form.addRow(label(c["Language"]), self.language)

        self.mod_note_format = QLineEdit(settings.get("mod_note_format", ""))
        self.mod_note_format.setObjectName("mod_note_format")
        self.mod_note_format.setFont(QFont("Segoe UI", 11))
        self.mod_note_format.setMinimumWidth(220)
        form.addRow(label(c["Mod Note Format"]), self.mod_note_format)

        self.btn_hotkeys = QPushButton(c.get("Customize Hotkeys", "Customize Hotkeys") + "...")
        self.btn_hotkeys.setObjectName("Customize Hotkeys")
        self.btn_hotkeys.setFont(QFont("Segoe UI", 12))
        self.btn_hotkeys.clicked.connect(self._open_hotkeys_dialog)
        form.addRow(self.btn_hotkeys)

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

        self.btn_restore.clicked.connect(lambda: self.done(RESTORE_DEFAULTS))
        self.btn_apply.clicked.connect(self.accept)
        self.btn_cancel.clicked.connect(self.reject)

    def _update_accent_button(self) -> None:
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

    def _pick_accent_color(self) -> None:
        """Opens a hex color picker dialog and stores the chosen accent color."""
        color = QColorDialog.getColor(QColor(self._accent_color), self, "Select Accent Color")
        if color.isValid():
            self._accent_color = color.name()
            self._update_accent_button()

    def _open_hotkeys_dialog(self) -> None:
        """Opens the hotkeys rebinding dialog and stores the result if confirmed."""
        dialog = HotkeysDialog(self._hotkeys, self.content, self, self._on_top)
        if dialog.exec() == QDialog.DialogCode.Accepted:
            self._hotkeys = dialog.get_values()

    def get_values(self) -> dict:
        """Returns current widget values as a dict compatible with the Settings controller."""
        return {
            "enable_updates": self.enable_updates.isChecked(),
            "theme": self.theme.currentData(),
            "accent_color": self._accent_color,
            "language": self.language.currentData(),
            "mod_note_format": self.mod_note_format.text(),
            "hotkeys": self._hotkeys,
        }


class Settings:
    """Settings for CRT."""

    def __init__(self) -> None:
        """Initializes the Settings class."""
        self.file_path = os.path.join(
            QStandardPaths.writableLocation(QStandardPaths.StandardLocation.AppConfigLocation),
            "settings.ini"
        )
        self.defaults = {
            "Settings": {
                "enable_updates": "True",
                "theme": "Automatic",
                "accent_color": DEFAULT_ACCENT_COLOR,
                "language": "en",
                "mod_note_format": "Mod Note: Retimed to {time_without_loads}",
            },
            "Hotkeys": DEFAULT_HOTKEYS,
        }

        # Defaults first, then the file on top of them: anything the user has
        # never set (or a key added in a later version) just falls through.
        self.config = ConfigParser()
        self.config.read_dict(self.defaults)
        try:
            self.config.read(self.file_path)
        except ParsingError:
            pass  # Hand-edited into a corrupt state: start from defaults instead.

        self.content = content_for(self.config.get("Settings", "language"))

    def _restore_defaults(self, parent=None, on_top: bool = False) -> None:
        """Restores the settings back to the defaults. Persisted on Apply."""
        if not _popup_yes_no(
            "Restore Defaults", "Are you sure you want to restore the default settings?",
            parent, on_top
        ):
            return

        self.config = ConfigParser()
        self.config.read_dict(self.defaults)

    def _apply(self, values: dict) -> None:
        """Applies the settings.

        Args:
            values (dict): The values from the settings window.
        """
        os.makedirs(os.path.dirname(self.file_path), exist_ok=True)
        with open(self.file_path, "w") as file:
            self.config.set("Settings", "enable_updates", str(values["enable_updates"]))
            self.config.set("Settings", "theme", str(values["theme"]))
            self.config.set("Settings", "accent_color", str(values["accent_color"]))
            self.config.set("Settings", "language", str(values["language"]))
            self.config.set("Settings", "mod_note_format", str(values["mod_note_format"]))

            for action_id, shortcut in values.get("hotkeys", {}).items():
                self.config.set("Hotkeys", action_id, str(shortcut))

            self.config.write(file)

    def config_to_dict(self) -> dict:
        """Converts the settings into a dictionary."""
        return {
            "enable_updates": self.config.getboolean("Settings", "enable_updates"),
            "theme": self.config.get("Settings", "theme"),
            "accent_color": self.config.get("Settings", "accent_color"),
            "language": self.config.get("Settings", "language"),
            "mod_note_format": self.config.get("Settings", "mod_note_format"),
            "hotkeys": {
                action_id: self.config.get("Hotkeys", action_id)
                for action_id in DEFAULT_HOTKEYS
            },
        }

    def open_window(self, parent=None, on_top: bool = False) -> None:
        """Opens the settings window."""
        while True:
            dialog = SettingsDialog(self.config_to_dict(), self.content, parent, on_top)
            result = dialog.exec()

            if result == RESTORE_DEFAULTS:
                # Re-open with fresh defaults.
                self._restore_defaults(parent, on_top)
                continue

            if result == QDialog.DialogCode.Accepted:
                self._apply(dialog.get_values())
            return
