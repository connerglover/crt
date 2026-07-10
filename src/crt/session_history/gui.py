# Standard Library
from typing import NoReturn

# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QListWidget, QListWidgetItem
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont

# Local application
from crt.base_gui import BaseGUI


class SessionHistoryDialog(QDialog):
    """Session history dialog for CRT."""

    def __init__(self, past_file_paths: list, content: dict, parent=None, on_top: bool = False):
        super().__init__(parent)
        self.setWindowTitle("Session History")
        self.setFixedSize(560, 300)
        self.setWindowModality(Qt.WindowModality.ApplicationModal)
        if on_top:
            self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self._build_ui(past_file_paths, content)

    def _build_ui(self, past_file_paths: list, content: dict):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(12, 12, 12, 12)
        layout.setSpacing(8)

        self.list_widget = QListWidget()
        self.list_widget.setObjectName("session_history")
        self.list_widget.setFont(QFont("Segoe UI", 12))
        for path in past_file_paths:
            self.list_widget.addItem(QListWidgetItem(path))
        layout.addWidget(self.list_widget)

    def get_selected(self):
        items = self.list_widget.selectedItems()
        return [item.text() for item in items]


class SessionHistoryGUI(BaseGUI):
    """Wrapper around SessionHistoryDialog to match the BaseGUI/event-loop interface."""

    def __init__(self, past_file_paths: list, content: dict, parent=None, on_top: bool = False):
        self.window = SessionHistoryDialog(past_file_paths, content, parent, on_top)
        self._connect_signals()

    def _connect_signals(self):
        self.window.list_widget.itemDoubleClicked.connect(
            lambda item: self._emit("session_history", {"session_history": [item.text()]})
        )
        self.window.list_widget.itemActivated.connect(
            lambda item: self._emit("session_history", {"session_history": [item.text()]})
        )
