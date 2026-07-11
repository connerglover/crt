# Third-party
from PySide6.QtWidgets import QMessageBox
from PySide6.QtCore import Qt


def _apply_on_top(box: QMessageBox, on_top: bool) -> None:
    """Keeps the popup above the main window when always-on-top is enabled.

    Qt would otherwise let a plain QMessageBox sink behind an owner window
    that has WindowStaysOnTopHint set, since the OS-level topmost flag only
    applies to the window it's set on.
    """
    if on_top:
        box.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)


def popup_yes_no(title: str, message: str, parent=None, on_top: bool = False) -> bool:
    """Shows a Yes/No message box. Returns True if Yes."""
    box = QMessageBox(parent)
    box.setWindowTitle(title)
    box.setText(message)
    box.setStandardButtons(QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
    box.setDefaultButton(QMessageBox.StandardButton.No)
    _apply_on_top(box, on_top)
    return box.exec() == QMessageBox.StandardButton.Yes


def popup_yes_no_cancel(title: str, message: str, parent=None, on_top: bool = False) -> str:
    """Shows a Yes/No/Cancel message box. Returns "yes", "no", or "cancel"."""
    box = QMessageBox(parent)
    box.setWindowTitle(title)
    box.setText(message)
    box.setStandardButtons(
        QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No | QMessageBox.StandardButton.Cancel
    )
    box.setDefaultButton(QMessageBox.StandardButton.Cancel)
    _apply_on_top(box, on_top)
    result = box.exec()
    if result == QMessageBox.StandardButton.Yes:
        return "yes"
    if result == QMessageBox.StandardButton.No:
        return "no"
    return "cancel"


def popup_ok(title: str, message: str, parent=None, on_top: bool = False):
    """Shows an informational popup."""
    box = QMessageBox(parent)
    box.setWindowTitle(title)
    box.setText(message)
    box.setStandardButtons(QMessageBox.StandardButton.Ok)
    _apply_on_top(box, on_top)
    box.exec()


def popup_error(title: str, message: str, parent=None, on_top: bool = False):
    """Shows an error popup."""
    box = QMessageBox(parent)
    box.setWindowTitle(title)
    box.setText(str(message))
    box.setIcon(QMessageBox.Icon.Critical)
    box.setStandardButtons(QMessageBox.StandardButton.Ok)
    _apply_on_top(box, on_top)
    box.exec()
