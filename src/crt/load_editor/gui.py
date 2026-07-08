# Standard library
from decimal import Decimal as d
from typing import NoReturn

# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit,
    QPushButton, QWidget, QSizePolicy
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont

# Local application
from crt.load import Load
from crt.base_gui import BaseGUI


class LoadEditorDialog(QDialog):
    """Dialog for editing a single load."""

    def __init__(self, load: Load, framerate: d, content: dict, parent=None):
        super().__init__(parent)
        self.content = content
        self.setWindowTitle("Editing Load")
        self.setFixedWidth(420)
        self.setWindowModality(Qt.WindowModality.ApplicationModal)
        self._build_ui(load, content)

    def _build_ui(self, load: Load, content: dict):
        c = content
        layout = QVBoxLayout(self)
        layout.setContentsMargins(20, 16, 20, 16)
        layout.setSpacing(10)

        # Title
        title = QLabel(c["Edit Load"])
        title.setProperty("cssClass", "heading")
        title.setFont(QFont("Segoe UI", 18, QFont.Weight.Bold))
        layout.addWidget(title)

        # Start frame row
        layout.addLayout(self._make_frame_row("start", c["Start Frame"], str(load.start_frame), c["Paste"]))

        # End frame row
        layout.addLayout(self._make_frame_row("end", c["End Frame"], str(load.end_frame), c["Paste"]))

        # Buttons
        btn_row = QHBoxLayout()
        btn_row.setSpacing(8)
        self.btn_save = QPushButton(c["Save Edits"])
        self.btn_save.setObjectName("Save Edits")
        self.btn_save.setProperty("cssClass", "primary")
        self.btn_discard = QPushButton(c["Discard Changes"])
        self.btn_discard.setObjectName("Discard Changes")
        for btn in (self.btn_save, self.btn_discard):
            btn.setFont(QFont("Segoe UI", 12))
            btn.setMinimumHeight(34)
            btn_row.addWidget(btn)
        layout.addLayout(btn_row)

        self._inputs = {}

    def _make_frame_row(self, key: str, label_text: str, default: str, paste_label: str) -> QHBoxLayout:
        row = QHBoxLayout()
        row.setSpacing(6)

        spacer = QWidget()
        spacer.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        row.addWidget(spacer)

        lbl = QLabel(label_text)
        lbl.setFont(QFont("Segoe UI", 13))
        lbl.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        lbl.setMinimumWidth(120)
        row.addWidget(lbl)

        inp = QLineEdit(default)
        inp.setObjectName(key)
        inp.setFont(QFont("Segoe UI", 13))
        inp.setFixedWidth(130)
        inp.setFixedHeight(30)
        row.addWidget(inp)

        paste_btn = QPushButton(paste_label)
        paste_btn.setObjectName(f"{key}_paste")
        paste_btn.setProperty("cssClass", "compact")
        paste_btn.setFont(QFont("Segoe UI", 10))
        paste_btn.setFixedWidth(58)
        paste_btn.setFixedHeight(30)
        row.addWidget(paste_btn)

        return row

    def get_values(self) -> dict:
        return {
            "start": self.findChild(QLineEdit, "start").text(),
            "end": self.findChild(QLineEdit, "end").text(),
        }


class LoadEditorGUI(BaseGUI):
    """Wrapper around LoadEditorDialog to match the BaseGUI/event-loop interface."""

    def __init__(self, load: Load, framerate: d, content: dict):
        self.window = LoadEditorDialog(load, framerate, content)
        self._last_event = None
        self._last_values = {}
        self._connect_signals()

    def _connect_signals(self):
        d = self.window
        d.btn_save.clicked.connect(lambda: self._emit("Save Edits"))
        d.btn_discard.clicked.connect(lambda: self._emit("Discard Changes"))

        start_input = d.findChild(QLineEdit, "start")
        end_input = d.findChild(QLineEdit, "end")
        start_paste = d.findChild(QPushButton, "start_paste")
        end_paste = d.findChild(QPushButton, "end_paste")

        if start_input:
            start_input.textEdited.connect(lambda: self._emit("start"))
        if end_input:
            end_input.textEdited.connect(lambda: self._emit("end"))
        if start_paste:
            start_paste.clicked.connect(lambda: self._emit("start_paste"))
        if end_paste:
            end_paste.clicked.connect(lambda: self._emit("end_paste"))

    def _emit(self, event: str):
        self._last_event = event
        self._last_values = self.window.get_values()

    def read(self) -> tuple:
        """Blocking read: shows the dialog and returns (event, values)."""
        from PySide6.QtWidgets import QApplication
        self._last_event = None
        self.window.show()
        while self._last_event is None and self.window.isVisible():
            QApplication.processEvents()
        event = self._last_event if self._last_event is not None else None
        values = self._last_values
        return event, values

    def close(self):
        if self.window:
            self.window.close()
