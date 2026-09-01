# Standard Library
from typing import NoReturn

# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QListWidget, QListWidgetItem
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont


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

        self._selected = None
        self.list_widget = QListWidget()
        self.list_widget.setObjectName("session_history")
        self.list_widget.setFont(QFont("Segoe UI", 12))
        for path in past_file_paths:
            self.list_widget.addItem(QListWidgetItem(path))
        layout.addWidget(self.list_widget)
        self.list_widget.itemActivated.connect(self._pick)
        self.list_widget.itemDoubleClicked.connect(self._pick)

    def _pick(self, item: QListWidgetItem) -> NoReturn:
        """Accepts the dialog with the double-clicked/activated path."""
        self._selected = item.text()
        self.accept()

    def run(self) -> str:
        """Shows the dialog modally.

        Returns:
            str: The selected file path, or None if cancelled.
        """
        return self._selected if self.exec() == QDialog.DialogCode.Accepted else None
