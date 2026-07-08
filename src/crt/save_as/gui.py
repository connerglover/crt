# Standard Library
from typing import NoReturn

# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit,
    QPushButton, QFileDialog, QWidget, QSizePolicy
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont

# Local application
from crt.base_gui import BaseGUI


class SaveAsDialog(QDialog):
    """Save As dialog for CRT."""

    def __init__(self, content: dict, parent=None):
        super().__init__(parent)
        self.content = content
        self.setWindowTitle("Save As")
        self.setFixedWidth(420)
        self.setWindowModality(Qt.WindowModality.ApplicationModal)
        self._file_path = None
        self._build_ui(content)

    def _build_ui(self, content: dict):
        c = content
        layout = QVBoxLayout(self)
        layout.setContentsMargins(20, 16, 20, 16)
        layout.setSpacing(12)

        # Title
        title = QLabel(c["Save As"])
        title.setProperty("cssClass", "heading")
        title.setFont(QFont("Segoe UI", 18, QFont.Weight.Bold))
        layout.addWidget(title)

        # File name row
        row = QHBoxLayout()
        row.setSpacing(8)
        lbl = QLabel(c["File Name"])
        lbl.setFont(QFont("Segoe UI", 12))
        row.addWidget(lbl)
        self.file_name = QLineEdit()
        self.file_name.setObjectName("file_name")
        self.file_name.setFont(QFont("Segoe UI", 12))
        self.file_name.setPlaceholderText("path/to/file.json")
        row.addWidget(self.file_name, stretch=1)
        browse_btn = QPushButton("Browse…")
        browse_btn.setFont(QFont("Segoe UI", 11))
        browse_btn.setFixedHeight(30)
        browse_btn.clicked.connect(self._browse)
        row.addWidget(browse_btn)
        layout.addLayout(row)

        # Buttons
        btn_row = QHBoxLayout()
        btn_row.setSpacing(8)
        self.btn_save = QPushButton(c["Save"])
        self.btn_save.setObjectName("save")
        self.btn_save.setProperty("cssClass", "primary")
        self.btn_cancel = QPushButton(c["Cancel"])
        self.btn_cancel.setObjectName("cancel")
        for btn in (self.btn_save, self.btn_cancel):
            btn.setFont(QFont("Segoe UI", 12))
            btn.setMinimumHeight(34)
            btn_row.addWidget(btn)
        layout.addLayout(btn_row)

    def _browse(self):
        path, _ = QFileDialog.getSaveFileName(
            self, "Save As", "", "Load Files (*.json)"
        )
        if path:
            if not path.endswith(".json"):
                path += ".json"
            self.file_name.setText(path)

    def get_values(self) -> dict:
        return {"file_name": self.file_name.text() or None}


class SaveAsGUI(BaseGUI):
    """Wrapper around SaveAsDialog to match the BaseGUI/event-loop interface."""

    def __init__(self, content: dict):
        self.window = SaveAsDialog(content)
        self._last_event = None
        self._last_values = {}
        self._connect_signals()

    def _connect_signals(self):
        d = self.window
        d.btn_save.clicked.connect(lambda: self._emit("save"))
        d.btn_cancel.clicked.connect(lambda: self._emit("cancel"))

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
        event = self._last_event
        values = self._last_values
        return event, values

    def close(self):
        if self.window:
            self.window.close()
