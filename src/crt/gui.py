# Third-party libraries
from PySide6.QtWidgets import (
    QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QLabel, QLineEdit, QPushButton, QFrame, QMenuBar, QMenu,
    QSizePolicy, QApplication
)
from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QAction, QFont

# Local application
from crt.base_gui import BaseGUI


class ClickableLabel(QLabel):
    """A QLabel that emits a clicked signal — used for the time display fields."""
    clicked = Signal()

    def mousePressEvent(self, event):
        self.clicked.emit()
        super().mousePressEvent(event)


class MainWindow(QMainWindow):
    """The main QMainWindow for CRT."""

    def __init__(self, content: dict):
        super().__init__()
        self.content = content
        self.setWindowTitle("Conner's Retime Tool")
        self.setFixedWidth(540)
        self.setMinimumHeight(460)
        self._build_ui()
        self.adjustSize()

    def _build_ui(self):
        c = self.content

        # ── Menu bar ──────────────────────────────────────────────────────────
        menubar = self.menuBar()

        file_menu = menubar.addMenu(c["File"])
        self._add_action(file_menu, c["New Time"],        "New Time")
        file_menu.addSeparator()
        self._add_action(file_menu, c["Open Time"],       "Open Time")
        self._add_action(file_menu, c["Session History"], "Session History")
        file_menu.addSeparator()
        self._add_action(file_menu, c["Save"],            "Save")
        self._add_action(file_menu, c["Save As"],         "Save As")
        file_menu.addSeparator()
        self._add_action(file_menu, c["Settings"],        "Settings")
        file_menu.addSeparator()
        self._add_action(file_menu, c["Exit"],            "Exit")

        edit_menu = menubar.addMenu(c["Edit (Menu Bar)"])
        self._add_action(edit_menu, c["Copy Mod Note"],   "Copy Mod Note")
        edit_menu.addSeparator()
        self._add_action(edit_menu, c["Clear Loads"],     "Clear Loads")
        self._add_action(edit_menu, c["Edit Loads"],      "Edit Loads")

        help_menu = menubar.addMenu(c["Help"])
        self._add_action(help_menu, c["Check for Updates"], "Check for Updates")
        help_menu.addSeparator()
        self._add_action(help_menu, c["Report Issue"],    "Report Issue")
        self._add_action(help_menu, c["Suggest Feature"], "Suggest Feature")
        help_menu.addSeparator()
        self._add_action(help_menu, c["About"],           "About")

        # ── Central widget ────────────────────────────────────────────────────
        central = QWidget()
        self.setCentralWidget(central)
        root = QVBoxLayout(central)
        root.setContentsMargins(20, 14, 20, 16)
        root.setSpacing(0)

        # ── Time display section (most prominent) ─────────────────────────────
        display_section = QWidget()
        display_section.setObjectName("display_section")
        display_section.setProperty("cssClass", "card")
        ds_layout = QVBoxLayout(display_section)
        ds_layout.setContentsMargins(16, 14, 16, 14)
        ds_layout.setSpacing(10)

        self.without_loads_display = self._make_time_display(
            ds_layout,
            label_text=c["Without Loads"],
            key="without_loads_display",
            default="00.000",
            tooltip=c.get("Click to Copy Time", "Click to copy"),
        )

        card_sep = QFrame()
        card_sep.setFrameShape(QFrame.Shape.HLine)
        card_sep.setFrameShadow(QFrame.Shadow.Sunken)
        ds_layout.addWidget(card_sep)

        self.loads_display = self._make_time_display(
            ds_layout,
            label_text=c["With Loads"],
            key="loads_display",
            default="00.000",
            tooltip=c.get("Click to Copy Time", "Click to copy"),
        )

        root.addWidget(display_section)
        root.addSpacing(14)

        # ── Thin separator ────────────────────────────────────────────────────
        sep_top = QFrame()
        sep_top.setFrameShape(QFrame.Shape.HLine)
        sep_top.setFrameShadow(QFrame.Shadow.Sunken)
        root.addWidget(sep_top)
        root.addSpacing(10)

        # ── Input rows ────────────────────────────────────────────────────────
        self._inputs = {}
        rows = [
            ("framerate",   c["Framerate"],           "60"),
            ("start",       c["Start Frame"],         "0"),
            ("end",         c["End Frame"],            "0"),
            ("start_loads", c["Start Frame (Loads)"], "0"),
            ("end_loads",   c["End Frame (Loads)"],   "0"),
        ]
        for key, label_text, default in rows:
            root.addLayout(self._make_input_row(key, label_text, default, c["Paste"]))
            root.addSpacing(4)

        root.addSpacing(6)

        # ── Separator ─────────────────────────────────────────────────────────
        sep_bot = QFrame()
        sep_bot.setFrameShape(QFrame.Shape.HLine)
        sep_bot.setFrameShadow(QFrame.Shadow.Sunken)
        root.addWidget(sep_bot)
        root.addSpacing(10)

        # ── Action buttons ────────────────────────────────────────────────────
        btn_row = QHBoxLayout()
        btn_row.setSpacing(8)
        self.btn_copy_mod_note = QPushButton(c["Copy Mod Note"])
        self.btn_copy_mod_note.setObjectName("Copy Mod Note")
        self.btn_copy_mod_note.setProperty("cssClass", "primary")
        self.btn_add_loads = QPushButton(c["Add Loads"])
        self.btn_add_loads.setObjectName("Add Loads")
        self.btn_edit_loads = QPushButton(c["Edit Loads"])
        self.btn_edit_loads.setObjectName("Edit Loads")
        for btn in (self.btn_copy_mod_note, self.btn_add_loads, self.btn_edit_loads):
            btn.setFont(QFont("Segoe UI", 13))
            btn.setMinimumHeight(38)
            btn_row.addWidget(btn)
        root.addLayout(btn_row)

    def _add_action(self, menu: QMenu, text: str, key: str) -> QAction:
        action = QAction(text, self)
        action.setData(key)
        menu.addAction(action)
        return action

    def _make_time_display(self, parent_layout: QVBoxLayout, label_text: str,
                           key: str, default: str, tooltip: str) -> ClickableLabel:
        """Creates a large, prominent time display row inside the display card."""
        container = QWidget()
        row = QHBoxLayout(container)
        row.setContentsMargins(0, 0, 0, 0)
        row.setSpacing(10)

        lbl = QLabel(label_text)
        lbl.setProperty("cssClass", "muted")
        lbl.setFont(QFont("Segoe UI", 13, QFont.Weight.DemiBold))
        lbl.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        lbl.setMinimumWidth(150)
        row.addWidget(lbl)

        display = ClickableLabel(default)
        display.setObjectName(key)
        display.setProperty("cssClass", "time-value")
        # Monospaced digits keep the value from jittering as it updates
        display.setFont(QFont("Consolas", 24, QFont.Weight.Bold))
        display.setAlignment(Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignVCenter)
        display.setToolTip(tooltip)
        display.setFixedHeight(42)
        display.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Fixed)
        display.setCursor(Qt.CursorShape.PointingHandCursor)
        row.addWidget(display)

        parent_layout.addWidget(container)
        return display

    def _make_input_row(self, key: str, label_text: str, default: str, paste_label: str) -> QHBoxLayout:
        row = QHBoxLayout()
        row.setSpacing(6)

        lbl = QLabel(label_text)
        lbl.setFont(QFont("Segoe UI", 12))
        lbl.setAlignment(Qt.AlignmentFlag.AlignRight | Qt.AlignmentFlag.AlignVCenter)
        lbl.setMinimumWidth(170)
        row.addWidget(lbl)

        inp = QLineEdit(default)
        inp.setObjectName(key)
        inp.setFont(QFont("Segoe UI", 12))
        inp.setFixedHeight(32)
        inp.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Fixed)
        row.addWidget(inp)
        self._inputs[key] = inp

        paste_btn = QPushButton(paste_label)
        paste_btn.setObjectName(f"{key}_paste")
        paste_btn.setProperty("cssClass", "compact")
        paste_btn.setFont(QFont("Segoe UI", 10))
        paste_btn.setFixedWidth(58)
        paste_btn.setFixedHeight(32)
        row.addWidget(paste_btn)

        return row


class MainGUI(BaseGUI):
    """Main window for CRT."""

    def __init__(self, content: dict):
        self.window = MainWindow(content)
