# Standard library
import json
import re
from decimal import Decimal as d
from typing import NoReturn

# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QLabel, QPushButton,
    QScrollArea, QWidget, QFrame, QLineEdit, QSizePolicy
)
from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QFont, QGuiApplication

# Local application
from crt.time import Time
from crt.base_gui import BaseGUI


class LoadRow(QWidget):
    """A single load row with always-visible inline start/end frame inputs.

    Layout:  Load N  [duration]  Start [input]  End [input]  [Delete]
    """

    delete_requested = Signal(int)

    def __init__(self, index: int, load, framerate: d, precision: int, content: dict, parent=None):
        super().__init__(parent)
        self.index = index
        self.load = load
        self.framerate = framerate
        self.precision = precision
        self.content = content
        self._build_ui()

    def _load_duration_str(self) -> str:
        """Returns the load duration as a formatted time string."""
        try:
            if self.framerate and self.framerate != 0:
                t = round(d(self.load.length) / d(self.framerate), self.precision)
            else:
                t = d(0)
            return str(t)
        except Exception:
            return "0"

    def _build_ui(self):
        layout = QHBoxLayout(self)
        layout.setContentsMargins(4, 4, 4, 4)
        layout.setSpacing(8)

        # Load number label
        num_lbl = QLabel(f"Load {self.index + 1}")
        num_lbl.setFont(QFont("Segoe UI", 12, QFont.Weight.Bold))
        num_lbl.setFixedWidth(64)
        layout.addWidget(num_lbl)

        # Duration display
        self._duration_lbl = QLabel(self._load_duration_str())
        self._duration_lbl.setObjectName(f"duration_{self.index}")
        self._duration_lbl.setProperty("cssClass", "chip")
        self._duration_lbl.setFont(QFont("Consolas", 12))
        self._duration_lbl.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._duration_lbl.setFixedWidth(80)
        self._duration_lbl.setFixedHeight(28)
        layout.addWidget(self._duration_lbl)

        # Start frame label + input
        start_lbl = QLabel(self.content.get("Start Frame", "Start"))
        start_lbl.setFont(QFont("Segoe UI", 11))
        start_lbl.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        layout.addWidget(start_lbl)

        self.start_input = QLineEdit(str(self.load.start_frame))
        self.start_input.setObjectName(f"start_{self.index}")
        self.start_input.setFont(QFont("Segoe UI", 12))
        self.start_input.setFixedWidth(100)
        self.start_input.setFixedHeight(28)
        layout.addWidget(self.start_input)

        # End frame label + input
        end_lbl = QLabel(self.content.get("End Frame", "End"))
        end_lbl.setFont(QFont("Segoe UI", 11))
        end_lbl.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        layout.addWidget(end_lbl)

        self.end_input = QLineEdit(str(self.load.end_frame))
        self.end_input.setObjectName(f"end_{self.index}")
        self.end_input.setFont(QFont("Segoe UI", 12))
        self.end_input.setFixedWidth(100)
        self.end_input.setFixedHeight(28)
        layout.addWidget(self.end_input)

        # Delete button
        self._btn_delete = QPushButton(self.content.get("Delete", "Delete"))
        self._btn_delete.setObjectName(f"delete_{self.index}")
        self._btn_delete.setProperty("cssClass", "danger-compact")
        self._btn_delete.setFont(QFont("Segoe UI", 10))
        self._btn_delete.setFixedHeight(28)
        self._btn_delete.setMinimumWidth(84)
        self._btn_delete.clicked.connect(lambda: self.delete_requested.emit(self.index))
        layout.addWidget(self._btn_delete)

    def get_values(self) -> tuple[str, str]:
        return self.start_input.text(), self.end_input.text()

    def refresh(self):
        """Refresh inputs and duration label from the underlying load object."""
        self.start_input.setText(str(self.load.start_frame))
        self.end_input.setText(str(self.load.end_frame))
        self._duration_lbl.setText(self._load_duration_str())

    def refresh_duration(self):
        """Refresh only the duration label (after a save)."""
        self._duration_lbl.setText(self._load_duration_str())


class LoadViewerDialog(QDialog):
    """Dialog for viewing and managing loads with always-visible inline editing."""

    def __init__(self, time: Time, content: dict, parent=None):
        super().__init__(parent)
        self._time = time
        self.content = content
        self.setWindowTitle(content.get("Loads", "Load Viewer"))
        self.setMinimumWidth(720)
        self.setWindowModality(Qt.WindowModality.ApplicationModal)
        self._load_rows: dict[int, LoadRow] = {}
        self._build_ui(time, content)

    def _build_ui(self, time: Time, content: dict):
        outer = QVBoxLayout(self)
        outer.setContentsMargins(16, 14, 16, 14)
        outer.setSpacing(10)

        # Title
        title = QLabel(content.get("Loads", "Loads"))
        title.setProperty("cssClass", "heading")
        title.setFont(QFont("Segoe UI", 18, QFont.Weight.Bold))
        outer.addWidget(title)

        # Scrollable load list
        self._scroll = QScrollArea()
        self._scroll.setWidgetResizable(True)
        self._scroll.setFrameShape(QFrame.Shape.NoFrame)
        outer.addWidget(self._scroll)

        self._list_widget = QWidget()
        self._list_layout = QVBoxLayout(self._list_widget)
        self._list_layout.setContentsMargins(0, 0, 0, 0)
        self._list_layout.setSpacing(2)
        self._list_layout.addStretch()
        self._scroll.setWidget(self._list_widget)

        for index, load in enumerate(time.loads):
            row = LoadRow(index, load, time.framerate, time.precision, content, self._list_widget)
            self._list_layout.insertWidget(self._list_layout.count() - 1, row)
            self._load_rows[index] = row

        # Size scroll area to content
        row_h = 42
        max_vis = 8
        n = len(time.loads)
        self._scroll.setFixedHeight(min(n, max_vis) * row_h + 10)

        # Separator
        sep = QFrame()
        sep.setFrameShape(QFrame.Shape.HLine)
        sep.setFrameShadow(QFrame.Shadow.Sunken)
        outer.addWidget(sep)

        # Save / Discard buttons (no Done button)
        btn_row = QHBoxLayout()
        btn_row.setSpacing(8)

        self._btn_save = QPushButton(content.get("Save Edits", "Save Edits"))
        self._btn_save.setProperty("cssClass", "primary")
        self._btn_save.setFont(QFont("Segoe UI", 12))
        self._btn_save.setMinimumHeight(34)
        btn_row.addWidget(self._btn_save)

        self._btn_discard = QPushButton(content.get("Discard Changes", "Discard Changes"))
        self._btn_discard.setFont(QFont("Segoe UI", 12))
        self._btn_discard.setMinimumHeight(34)
        btn_row.addWidget(self._btn_discard)

        btn_row.addStretch()
        outer.addLayout(btn_row)
        self.adjustSize()


class LoadViewerGUI(BaseGUI):
    """Wrapper around LoadViewerDialog to match the BaseGUI/event-loop interface."""

    def __init__(self, time: Time, content: dict):
        self.window = LoadViewerDialog(time, content)
        self._last_event = None
        self._last_values = {}
        self._connect_signals()

    def _connect_signals(self):
        for index, row in self.window._load_rows.items():
            row.delete_requested.connect(self._on_delete)

        self.window._btn_save.clicked.connect(self._on_save_all)
        self.window._btn_discard.clicked.connect(self._on_discard)

    def _on_save_all(self):
        """Collect all row values and emit a save_all event."""
        rows_data = {}
        for index, row in self.window._load_rows.items():
            if row.isVisible():
                start, end = row.get_values()
                rows_data[index] = {"start": start, "end": end}
        self._last_event = "save_all"
        self._last_values = {"rows": rows_data}

    def _on_discard(self):
        """Reset all inputs to the current load values and emit discard."""
        for index, row in self.window._load_rows.items():
            if row.isVisible():
                row.refresh()
        self._emit("discard")

    def _on_delete(self, index: int):
        self._emit(f"delete_{index}")

    def _emit(self, event: str):
        self._last_event = event
        self._last_values = {}

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

    def hide_row(self, index: int):
        """Hide a load row after deletion."""
        row = self.window._load_rows.get(index)
        if row:
            row.setVisible(False)
