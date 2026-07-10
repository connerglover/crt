# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit,
    QPushButton, QCheckBox, QComboBox, QFrame, QWidget, QSizePolicy
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont

# Local application
from crt.base_gui import BaseGUI


class SettingsDialog(QDialog):
    """Settings dialog for CRT."""

    def __init__(self, settings: dict, content: dict, parent=None, on_top: bool = False):
        super().__init__(parent)
        self.content = content
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

    def get_values(self) -> dict:
        """Returns current widget values as a dict compatible with the Settings controller."""
        return {
            "enable_updates": self.enable_updates.isChecked(),
            "theme": self.theme.currentText(),
            "language": self.language.currentText(),
            "mod_note_format": self.mod_note_format.text(),
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
