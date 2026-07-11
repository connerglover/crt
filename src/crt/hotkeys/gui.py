# Third-party
from PySide6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QLabel, QPushButton, QKeySequenceEdit,
    QScrollArea, QWidget, QFrame
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont, QKeySequence

# Local application
from crt.hotkeys.app import HOTKEY_ACTIONS
from crt.popups import popup_error as _popup_error

_LABEL_KEYS = {action_id: label_key for action_id, label_key, _ in HOTKEY_ACTIONS}


class HotkeysDialog(QDialog):
    """Modal dialog for rebinding the app's customizable hotkeys."""

    def __init__(self, hotkeys: dict, content: dict, parent=None, on_top: bool = False):
        super().__init__(parent)
        self.content = content
        self.setWindowTitle(content.get("Hotkeys", "Hotkeys"))
        self.setFixedSize(440, 480)
        self.setWindowModality(Qt.WindowModality.ApplicationModal)
        if on_top:
            self.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
        self._editors: dict[str, QKeySequenceEdit] = {}
        self._build_ui(hotkeys, content)

    def _build_ui(self, hotkeys: dict, content: dict) -> None:
        c = content
        layout = QVBoxLayout(self)
        layout.setContentsMargins(20, 16, 20, 16)
        layout.setSpacing(10)

        title = QLabel(c.get("Customize Hotkeys", "Customize Hotkeys"))
        title.setProperty("cssClass", "heading")
        title.setFont(QFont("Segoe UI", 16, QFont.Weight.Bold))
        layout.addWidget(title)

        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.Shape.NoFrame)
        layout.addWidget(scroll, 1)

        rows_widget = QWidget()
        rows_layout = QVBoxLayout(rows_widget)
        rows_layout.setContentsMargins(0, 0, 4, 0)
        rows_layout.setSpacing(8)

        for action_id, label_key, default in HOTKEY_ACTIONS:
            row = QHBoxLayout()
            row.setSpacing(8)

            lbl = QLabel(c.get(label_key, label_key))
            lbl.setFont(QFont("Segoe UI", 11))
            lbl.setMinimumWidth(180)
            row.addWidget(lbl)

            editor = QKeySequenceEdit(QKeySequence(hotkeys.get(action_id, default)))
            editor.setObjectName(f"hotkey_{action_id}")
            editor.setMaximumSequenceLength(1)
            editor.setToolTip(c.get("Press a Key Combination", "Press a key combination"))
            row.addWidget(editor, 1)
            self._editors[action_id] = editor

            reset_btn = QPushButton(c.get("Reset", "Reset"))
            reset_btn.setProperty("cssClass", "compact")
            reset_btn.setFont(QFont("Segoe UI", 9))
            reset_btn.clicked.connect(
                lambda checked=False, e=editor, d=default: e.setKeySequence(QKeySequence(d))
            )
            row.addWidget(reset_btn)

            rows_layout.addLayout(row)

        rows_layout.addStretch(1)
        scroll.setWidget(rows_widget)

        sep = QFrame()
        sep.setFrameShape(QFrame.Shape.HLine)
        sep.setFrameShadow(QFrame.Shadow.Sunken)
        layout.addWidget(sep)

        btn_row = QHBoxLayout()
        btn_row.setSpacing(8)
        self.btn_reset_all = QPushButton(c.get("Reset All", "Reset All"))
        self.btn_reset_all.setObjectName("Reset All")
        self.btn_ok = QPushButton(c.get("OK", "OK"))
        self.btn_ok.setObjectName("OK")
        self.btn_ok.setProperty("cssClass", "primary")
        self.btn_cancel = QPushButton(c["Cancel"])
        self.btn_cancel.setObjectName("Cancel")
        for btn in (self.btn_reset_all, self.btn_ok, self.btn_cancel):
            btn.setFont(QFont("Segoe UI", 11))
            btn.setMinimumHeight(32)
            btn_row.addWidget(btn)
        layout.addLayout(btn_row)

        self.btn_reset_all.clicked.connect(self._reset_all)
        self.btn_ok.clicked.connect(self._validate_and_accept)
        self.btn_cancel.clicked.connect(self.reject)

    def _reset_all(self) -> None:
        for action_id, _, default in HOTKEY_ACTIONS:
            self._editors[action_id].setKeySequence(QKeySequence(default))

    def _validate_and_accept(self) -> None:
        """Blocks Qt's "ambiguous shortcut" failure mode by catching duplicate
        key sequences here, before they ever reach two live QActions/QShortcuts."""
        c = self.content
        values = self.get_values()

        seen: dict[str, str] = {}
        conflicting_ids: set[str] = set()
        for action_id, seq in values.items():
            if not seq:
                continue
            if seq in seen:
                conflicting_ids.add(seen[seq])
                conflicting_ids.add(action_id)
            else:
                seen[seq] = action_id

        if conflicting_ids:
            names = ", ".join(
                c.get(_LABEL_KEYS[action_id], _LABEL_KEYS[action_id]) for action_id in conflicting_ids
            )
            _popup_error(
                c.get("Duplicate Hotkey", "Duplicate Hotkey"),
                c.get(
                    "Duplicate Hotkey Message",
                    "The same key combination is assigned to more than one action: {names}"
                ).format(names=names),
                self, self.windowFlags() & Qt.WindowType.WindowStaysOnTopHint != 0
            )
            return

        self.accept()

    def get_values(self) -> dict:
        """Returns the action-id -> key-sequence-string mapping from the editors."""
        return {
            action_id: editor.keySequence().toString(QKeySequence.SequenceFormat.PortableText)
            for action_id, editor in self._editors.items()
        }
