# Standard library
from decimal import Decimal as d

# Third-party libraries
from PySide6.QtWidgets import (
    QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QLabel, QLineEdit, QPushButton, QFrame, QMenuBar, QMenu,
    QSizePolicy, QApplication, QSplitter, QScrollArea
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


class LoadSidebarRow(QWidget):
    """A compact, always-visible card for editing a single load inline.

    Layout (stacked, narrow-friendly):
        Load N   [duration]           [Delete]
        Start [input]   End [input]
    """

    edited = Signal(int)
    delete_requested = Signal(int)

    def __init__(self, index: int, load, framerate: d, precision: int, content: dict, parent=None):
        super().__init__(parent)
        self.index = index
        self.load = load
        self.framerate = framerate
        self.precision = precision
        self.content = content
        self._build_ui()

    def _duration_str(self) -> str:
        try:
            if self.framerate and self.framerate != 0:
                t = round(d(self.load.length) / d(self.framerate), self.precision)
            else:
                t = d(0)
            return str(t)
        except Exception:
            return "0"

    def _build_ui(self):
        self.setProperty("cssClass", "card")
        outer = QVBoxLayout(self)
        outer.setContentsMargins(10, 8, 10, 8)
        outer.setSpacing(6)

        top_row = QHBoxLayout()
        top_row.setSpacing(6)

        title = QLabel(f"Load {self.index + 1}")
        title.setFont(QFont("Segoe UI", 11, QFont.Weight.Bold))
        top_row.addWidget(title)

        self._duration_lbl = QLabel(self._duration_str())
        self._duration_lbl.setProperty("cssClass", "chip")
        self._duration_lbl.setFont(QFont("Consolas", 10))
        self._duration_lbl.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._duration_lbl.setFixedHeight(22)
        top_row.addWidget(self._duration_lbl, 1)

        self._btn_delete = QPushButton("✕")
        self._btn_delete.setToolTip(self.content.get("Delete", "Delete"))
        self._btn_delete.setProperty("cssClass", "danger-compact")
        self._btn_delete.setFont(QFont("Segoe UI", 9))
        self._btn_delete.setFixedSize(22, 22)
        self._btn_delete.clicked.connect(lambda: self.delete_requested.emit(self.index))
        top_row.addWidget(self._btn_delete)

        outer.addLayout(top_row)

        fields_row = QHBoxLayout()
        fields_row.setSpacing(4)

        start_lbl = QLabel("Start")
        start_lbl.setFont(QFont("Segoe UI", 9))
        fields_row.addWidget(start_lbl)

        self.start_input = QLineEdit(str(self.load.start_frame))
        self.start_input.setFont(QFont("Segoe UI", 10))
        self.start_input.setFixedHeight(24)
        self.start_input.setFixedWidth(58)
        self.start_input.editingFinished.connect(lambda: self.edited.emit(self.index))
        fields_row.addWidget(self.start_input)

        end_lbl = QLabel("End")
        end_lbl.setFont(QFont("Segoe UI", 9))
        fields_row.addWidget(end_lbl)

        self.end_input = QLineEdit(str(self.load.end_frame))
        self.end_input.setFont(QFont("Segoe UI", 10))
        self.end_input.setFixedHeight(24)
        self.end_input.setFixedWidth(58)
        self.end_input.editingFinished.connect(lambda: self.edited.emit(self.index))
        fields_row.addWidget(self.end_input)

        fields_row.addStretch()

        outer.addLayout(fields_row)

    def get_values(self) -> tuple[str, str]:
        return self.start_input.text(), self.end_input.text()


class MainWindow(QMainWindow):
    """The main QMainWindow for CRT."""

    load_edited = Signal(int, str, str)
    load_delete_requested = Signal(int)
    update_link_clicked = Signal()

    _BASE_WIDTH = 900
    _BASE_HEIGHT = 530
    _UPDATE_BANNER_HEIGHT = 34

    def __init__(self, content: dict):
        super().__init__()
        self.content = content
        self._load_rows: dict[int, LoadSidebarRow] = {}
        self.setWindowTitle("Conner's Retime Tool")
        self._build_ui()
        self.setFixedSize(self._BASE_WIDTH, self._BASE_HEIGHT)

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

        view_menu = menubar.addMenu(c["View"])
        self.action_always_on_top = self._add_action(view_menu, c["Always on Top"], "Always on Top")
        self.action_always_on_top.setCheckable(True)

        help_menu = menubar.addMenu(c["Help"])
        self._add_action(help_menu, c["About"], "About")

        # ── Central widget: update banner above main panel + loads sidebar ─────
        central = QWidget()
        self.setCentralWidget(central)
        central_layout = QVBoxLayout(central)
        central_layout.setContentsMargins(0, 0, 0, 0)
        central_layout.setSpacing(0)

        self.update_banner = self._build_update_banner()
        central_layout.addWidget(self.update_banner)

        body = QWidget()
        outer = QHBoxLayout(body)
        outer.setContentsMargins(0, 0, 0, 0)
        outer.setSpacing(0)
        central_layout.addWidget(body, 1)

        splitter = QSplitter(Qt.Orientation.Horizontal)
        splitter.setChildrenCollapsible(False)
        outer.addWidget(splitter)

        main_panel = self._build_main_panel(c)
        main_panel.setMinimumWidth(480)
        splitter.addWidget(main_panel)

        loads_panel = self._build_loads_panel(c)
        loads_panel.setMinimumWidth(240)
        splitter.addWidget(loads_panel)

        splitter.setStretchFactor(0, 0)
        splitter.setStretchFactor(1, 1)
        splitter.setSizes([540, 300])

    def _build_main_panel(self, c: dict) -> QWidget:
        panel = QWidget()
        root = QVBoxLayout(panel)
        root.setContentsMargins(20, 14, 20, 16)
        root.setSpacing(0)

        # ── Time display section (most prominent) ─────────────────────────────
        display_col = QVBoxLayout()
        display_col.setSpacing(10)

        without_loads_card, self.without_loads_display = self._make_time_display(
            label_text=c["Without Loads"],
            key="without_loads_display",
            default="00.000",
            tooltip=c.get("Click to Copy Time", "Click to copy"),
        )
        display_col.addWidget(without_loads_card)

        loads_card, self.loads_display = self._make_time_display(
            label_text=c["With Loads"],
            key="loads_display",
            default="00.000",
            tooltip=c.get("Click to Copy Time", "Click to copy"),
        )
        display_col.addWidget(loads_card)

        root.addLayout(display_col)
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
        for btn in (self.btn_copy_mod_note, self.btn_add_loads):
            btn.setFont(QFont("Segoe UI", 13))
            btn.setMinimumHeight(38)
            btn_row.addWidget(btn)
        root.addLayout(btn_row)
        root.addStretch(1)

        return panel

    def _build_loads_panel(self, c: dict) -> QWidget:
        """Builds the scrollable sidebar for viewing/editing loads inline."""
        panel = QWidget()
        panel.setObjectName("loads_panel")
        layout = QVBoxLayout(panel)
        layout.setContentsMargins(12, 14, 16, 16)
        layout.setSpacing(8)

        header = QLabel(c.get("Loads", "Loads"))
        header.setProperty("cssClass", "heading")
        header.setFont(QFont("Segoe UI", 15, QFont.Weight.Bold))
        layout.addWidget(header)

        self._loads_summary = QLabel("")
        self._loads_summary.setProperty("cssClass", "muted")
        self._loads_summary.setFont(QFont("Segoe UI", 10))
        layout.addWidget(self._loads_summary)

        self._loads_scroll = QScrollArea()
        self._loads_scroll.setWidgetResizable(True)
        self._loads_scroll.setFrameShape(QFrame.Shape.NoFrame)
        layout.addWidget(self._loads_scroll, 1)

        self._loads_list_widget = QWidget()
        self._loads_list_layout = QVBoxLayout(self._loads_list_widget)
        self._loads_list_layout.setContentsMargins(0, 0, 0, 2)
        self._loads_list_layout.setSpacing(6)
        self._loads_list_layout.addStretch()
        self._loads_scroll.setWidget(self._loads_list_widget)

        # Empty-state placeholder lives in its own container with a leading stretch,
        # so it sits low in the panel (mirroring where the scroll area's content
        # would otherwise start) instead of hugging the header once loads exist.
        self._loads_empty_container = QWidget()
        empty_layout = QVBoxLayout(self._loads_empty_container)
        empty_layout.setContentsMargins(0, 0, 0, 0)
        empty_layout.addStretch(1)

        self._loads_empty_label = QLabel(
            c.get("No Loads", "No loads yet. Use Add Loads to create one.")
        )
        self._loads_empty_label.setProperty("cssClass", "muted")
        self._loads_empty_label.setWordWrap(True)
        self._loads_empty_label.setAlignment(Qt.AlignmentFlag.AlignHCenter | Qt.AlignmentFlag.AlignTop)
        empty_layout.addWidget(self._loads_empty_label)

        layout.addWidget(self._loads_empty_container, 1)

        return panel

    def refresh_loads(self, loads: list, framerate: d, precision: int, content: dict):
        """Rebuilds the sidebar's load rows to match the given list of loads."""
        for row in self._load_rows.values():
            row.setParent(None)
            row.deleteLater()
        self._load_rows.clear()

        for index, load in enumerate(loads):
            row = LoadSidebarRow(index, load, framerate, precision, content, self._loads_list_widget)
            row.edited.connect(self._on_load_row_edited)
            row.delete_requested.connect(self.load_delete_requested.emit)
            self._loads_list_layout.insertWidget(self._loads_list_layout.count() - 1, row)
            self._load_rows[index] = row

        has_loads = bool(loads)
        self._loads_empty_container.setVisible(not has_loads)
        self._loads_scroll.setVisible(has_loads)

        n = len(loads)
        self._loads_summary.setText(f"{n} load{'s' if n != 1 else ''}" if n else "")

    def _on_load_row_edited(self, index: int):
        row = self._load_rows.get(index)
        if row:
            start, end = row.get_values()
            self.load_edited.emit(index, start, end)

    def _build_update_banner(self) -> QWidget:
        """Builds the dismissible yellow update-available bar shown above the main panel."""
        banner = QWidget()
        banner.setProperty("cssClass", "update-banner")
        banner.setFixedHeight(self._UPDATE_BANNER_HEIGHT)
        banner.setVisible(False)

        layout = QHBoxLayout(banner)
        layout.setContentsMargins(14, 0, 6, 0)
        layout.setSpacing(8)

        self.update_banner_label = ClickableLabel("")
        self.update_banner_label.setObjectName("update_banner_label")
        self.update_banner_label.setProperty("cssClass", "update-banner-text")
        self.update_banner_label.setFont(QFont("Segoe UI", 10, QFont.Weight.DemiBold))
        self.update_banner_label.setCursor(Qt.CursorShape.PointingHandCursor)
        self.update_banner_label.clicked.connect(self.update_link_clicked.emit)
        layout.addWidget(self.update_banner_label, 1)

        close_btn = QPushButton("✕")
        close_btn.setObjectName("update_banner_close")
        close_btn.setProperty("cssClass", "update-banner-close")
        close_btn.setFixedSize(22, 22)
        close_btn.setFont(QFont("Segoe UI", 9))
        close_btn.setCursor(Qt.CursorShape.PointingHandCursor)
        close_btn.setToolTip(self.content.get("Dismiss", "Dismiss"))
        close_btn.clicked.connect(self.hide_update_banner)
        layout.addWidget(close_btn)

        return banner

    def show_update_banner(self, version: str) -> None:
        """Shows the update banner for the given version, growing the window to fit it."""
        text = self.content.get(
            "Update Available Banner", "A new version ({version}) is available — click to download."
        ).format(version=version)
        self.update_banner_label.setText(text)

        if not self.update_banner.isVisible():
            self.update_banner.setVisible(True)
            self.setFixedSize(self._BASE_WIDTH, self._BASE_HEIGHT + self._UPDATE_BANNER_HEIGHT)

    def hide_update_banner(self) -> None:
        """Hides the update banner, shrinking the window back to its base size."""
        if self.update_banner.isVisible():
            self.update_banner.setVisible(False)
            self.setFixedSize(self._BASE_WIDTH, self._BASE_HEIGHT)

    def _add_action(self, menu: QMenu, text: str, key: str) -> QAction:
        action = QAction(text, self)
        action.setData(key)
        menu.addAction(action)
        return action

    def _make_time_display(self, label_text: str, key: str, default: str,
                           tooltip: str) -> tuple[QWidget, ClickableLabel]:
        """Builds a card-style time display: a small muted caption stacked above
        a large, centered value. Returns the card and the value label."""
        card = QWidget()
        card.setProperty("cssClass", "card")
        card.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Maximum)
        layout = QVBoxLayout(card)
        layout.setContentsMargins(12, 8, 12, 8)
        layout.setSpacing(2)

        lbl = QLabel(label_text.upper())
        lbl.setProperty("cssClass", "muted")
        lbl.setFont(QFont("Segoe UI", 10, QFont.Weight.DemiBold))
        lbl.setAlignment(Qt.AlignmentFlag.AlignCenter)
        lbl.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Fixed)
        layout.addWidget(lbl)

        display = ClickableLabel(default)
        display.setObjectName(key)
        display.setProperty("cssClass", "time-value")
        # Monospaced digits keep the value from jittering as it updates
        display.setFont(QFont("Consolas", 30, QFont.Weight.Bold))
        display.setAlignment(Qt.AlignmentFlag.AlignCenter)
        display.setToolTip(tooltip)
        display.setCursor(Qt.CursorShape.PointingHandCursor)
        display.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Fixed)
        layout.addWidget(display)

        return card, display

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
