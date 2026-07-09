# Third-party
from PySide6.QtWidgets import QMessageBox


def popup_yes_no(title: str, message: str) -> bool:
    """Shows a Yes/No message box. Returns True if Yes."""
    box = QMessageBox()
    box.setWindowTitle(title)
    box.setText(message)
    box.setStandardButtons(QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
    box.setDefaultButton(QMessageBox.StandardButton.No)
    return box.exec() == QMessageBox.StandardButton.Yes


def popup_ok(title: str, message: str):
    """Shows an informational popup."""
    box = QMessageBox()
    box.setWindowTitle(title)
    box.setText(message)
    box.setStandardButtons(QMessageBox.StandardButton.Ok)
    box.exec()


def popup_error(title: str, message: str):
    """Shows an error popup."""
    box = QMessageBox()
    box.setWindowTitle(title)
    box.setText(str(message))
    box.setIcon(QMessageBox.Icon.Critical)
    box.setStandardButtons(QMessageBox.StandardButton.Ok)
    box.exec()
