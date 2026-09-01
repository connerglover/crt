# Third-party
from PySide6.QtWidgets import QMessageBox
from PySide6.QtCore import Qt

_SB = QMessageBox.StandardButton


def _box(title: str, message: str, parent, on_top: bool, buttons, default=None,
         icon=QMessageBox.Icon.NoIcon) -> int:
    """Shows a modal message box and returns the button the user clicked.

    Qt would otherwise let a plain QMessageBox sink behind an owner window that
    has WindowStaysOnTopHint set, since the OS-level topmost flag only applies
    to the window it's set on — hence the on_top flag.
    """
    box = QMessageBox(parent)
    box.setWindowTitle(title)
    box.setText(str(message))
    box.setIcon(icon)
    box.setStandardButtons(buttons)
    if default is not None:
        box.setDefaultButton(default)
    if on_top:
        box.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)
    return box.exec()


def popup_yes_no(title: str, message: str, parent=None, on_top: bool = False) -> bool:
    """Shows a Yes/No message box. Returns True if Yes."""
    return _box(title, message, parent, on_top, _SB.Yes | _SB.No, _SB.No) == _SB.Yes


def popup_yes_no_cancel(title: str, message: str, parent=None, on_top: bool = False) -> str:
    """Shows a Yes/No/Cancel message box. Returns "yes", "no", or "cancel"."""
    result = _box(title, message, parent, on_top, _SB.Yes | _SB.No | _SB.Cancel, _SB.Cancel)
    return {_SB.Yes: "yes", _SB.No: "no"}.get(result, "cancel")


def popup_ok(title: str, message: str, parent=None, on_top: bool = False) -> None:
    """Shows an informational popup."""
    _box(title, message, parent, on_top, _SB.Ok)


def popup_error(title: str, message: str, parent=None, on_top: bool = False) -> None:
    """Shows an error popup."""
    _box(title, message, parent, on_top, _SB.Ok, icon=QMessageBox.Icon.Critical)
