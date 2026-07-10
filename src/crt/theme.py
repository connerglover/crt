# Standard library
import sys


def is_dark_mode() -> bool:
    """Detects whether the OS is using a dark theme.

    Reads the Windows registry directly instead of depending on the
    third-party `darkdetect` package, which pulls in a platform-specific
    submodule (`_windows_detect`) that's easy to lose track of — either
    it's missing because dependencies weren't installed, or PyInstaller's
    static import analysis fails to bundle it into the frozen exe.
    Falls back to light mode on any failure or on non-Windows platforms.
    """
    if sys.platform != "win32":
        return False
    try:
        import winreg
        key = winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        )
        value, _ = winreg.QueryValueEx(key, "AppsUseLightTheme")
        return value == 0
    except OSError:
        return False


# A neutral grey dark theme (no blue/purple tint in the base surfaces) with a
# plain steel-blue accent reserved for interactive/selected elements.
DARK_PALETTE = """
QWidget {
    background-color: #1e1e1e;
    color: #d4d4d4;
    font-family: "Segoe UI", Helvetica, Arial, sans-serif;
}
QMainWindow, QDialog {
    background-color: #1e1e1e;
}
QMenuBar {
    background-color: #181818;
    color: #d4d4d4;
    border-bottom: 1px solid #2e2e2e;
    padding: 2px 4px;
}
QMenuBar::item {
    padding: 4px 10px;
    border-radius: 5px;
}
QMenuBar::item:selected {
    background-color: #2e2e2e;
}
QMenu {
    background-color: #181818;
    color: #d4d4d4;
    border: 1px solid #2e2e2e;
    border-radius: 8px;
    padding: 6px;
}
QMenu::item {
    padding: 6px 24px 6px 12px;
    border-radius: 5px;
}
QMenu::item:selected {
    background-color: #2e2e2e;
}
QMenu::separator {
    height: 1px;
    background-color: #2e2e2e;
    margin: 6px 4px;
}
QLineEdit {
    background-color: #2e2e2e;
    color: #d4d4d4;
    border: 1px solid #454545;
    border-radius: 7px;
    padding: 3px 10px;
    selection-background-color: #5b9bd5;
    selection-color: #1e1e1e;
}
QLineEdit:hover {
    border: 1px solid #5a5a5a;
}
QLineEdit:focus {
    border: 1px solid #5b9bd5;
}
QLineEdit:disabled {
    color: #6e6e6e;
    background-color: #242424;
}
QPushButton {
    background-color: #2e2e2e;
    color: #d4d4d4;
    border: 1px solid #454545;
    border-radius: 7px;
    padding: 6px 14px;
    font-weight: 500;
}
QPushButton:hover {
    background-color: #454545;
    border-color: #5a5a5a;
}
QPushButton:pressed {
    background-color: #262626;
}
QPushButton:disabled {
    color: #6e6e6e;
    background-color: #242424;
    border-color: #2e2e2e;
}
QPushButton[cssClass="primary"] {
    background-color: #5b9bd5;
    color: #1e1e1e;
    border: 1px solid #5b9bd5;
    font-weight: 600;
}
QPushButton[cssClass="primary"]:hover {
    background-color: #77aee0;
    border-color: #77aee0;
}
QPushButton[cssClass="primary"]:pressed {
    background-color: #4a86bd;
}
QPushButton[cssClass="danger"] {
    background-color: transparent;
    color: #e06c75;
    border: 1px solid #454545;
}
QPushButton[cssClass="danger"]:hover {
    background-color: rgba(224, 108, 117, 0.15);
    border-color: #e06c75;
}
QPushButton[cssClass="danger"]:pressed {
    background-color: rgba(224, 108, 117, 0.28);
}
QPushButton[cssClass="compact"] {
    padding: 2px 6px;
    font-weight: 400;
}
QPushButton[cssClass="danger-compact"] {
    background-color: transparent;
    color: #e06c75;
    border: 1px solid #454545;
    padding: 2px 8px;
    font-weight: 400;
}
QPushButton[cssClass="danger-compact"]:hover {
    background-color: rgba(224, 108, 117, 0.15);
    border-color: #e06c75;
}
QPushButton[cssClass="danger-compact"]:pressed {
    background-color: rgba(224, 108, 117, 0.28);
}
QLabel {
    color: #d4d4d4;
}
QLabel[cssClass="heading"] {
    color: #5b9bd5;
}
QLabel[cssClass="muted"] {
    color: #9a9a9a;
}
QLabel[cssClass="chip"] {
    background-color: #2e2e2e;
    border: 1px solid #454545;
    border-radius: 6px;
}
QFrame[frameShape="4"],
QFrame[frameShape="5"] {
    color: #2e2e2e;
    max-height: 1px;
}
QComboBox {
    background-color: #2e2e2e;
    color: #d4d4d4;
    border: 1px solid #454545;
    border-radius: 7px;
    padding: 3px 10px;
}
QComboBox:hover {
    border: 1px solid #5a5a5a;
}
QComboBox::drop-down {
    border: none;
    width: 22px;
}
QComboBox QAbstractItemView {
    background-color: #181818;
    color: #d4d4d4;
    border: 1px solid #2e2e2e;
    border-radius: 8px;
    selection-background-color: #454545;
    outline: none;
    padding: 4px;
}
QCheckBox {
    color: #d4d4d4;
    spacing: 8px;
}
QCheckBox::indicator {
    width: 16px;
    height: 16px;
    border: 1px solid #5a5a5a;
    border-radius: 4px;
    background-color: #2e2e2e;
}
QCheckBox::indicator:hover {
    border-color: #5b9bd5;
}
QCheckBox::indicator:checked {
    background-color: #5b9bd5;
    border-color: #5b9bd5;
}
QListWidget {
    background-color: #181818;
    color: #d4d4d4;
    border: 1px solid #2e2e2e;
    border-radius: 8px;
    padding: 4px;
}
QListWidget::item {
    padding: 7px 8px;
    border-radius: 5px;
}
QListWidget::item:hover {
    background-color: #242424;
}
QListWidget::item:selected {
    background-color: #5b9bd5;
    color: #1e1e1e;
}
QScrollBar:vertical {
    background: transparent;
    width: 12px;
    margin: 2px;
}
QScrollBar::handle:vertical {
    background: #454545;
    border-radius: 5px;
    min-height: 24px;
}
QScrollBar::handle:vertical:hover {
    background: #5a5a5a;
}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {
    height: 0px;
}
QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical {
    background: transparent;
}
QScrollBar:horizontal {
    background: transparent;
    height: 12px;
    margin: 2px;
}
QScrollBar::handle:horizontal {
    background: #454545;
    border-radius: 5px;
    min-width: 24px;
}
QScrollBar::handle:horizontal:hover {
    background: #5a5a5a;
}
QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {
    width: 0px;
}
QScrollBar::add-page:horizontal, QScrollBar::sub-page:horizontal {
    background: transparent;
}
QToolTip {
    background-color: #181818;
    color: #d4d4d4;
    border: 1px solid #454545;
    border-radius: 5px;
    padding: 4px 8px;
}
QWidget[cssClass="card"] {
    background-color: #242424;
    border: 1px solid #2e2e2e;
    border-radius: 10px;
}
QLabel[cssClass="time-value"] {
    color: #d4d4d4;
}
QLabel[cssClass="time-value"]:hover {
    color: #5b9bd5;
}
QWidget[cssClass="update-banner"] {
    background-color: #5b9bd5;
    border-bottom: 1px solid #4a86bd;
}
QLabel[cssClass="update-banner-text"] {
    color: #1e1e1e;
    background-color: transparent;
}
QLabel[cssClass="update-banner-text"]:hover {
    color: #000000;
    background-color: transparent;
}
QPushButton[cssClass="update-banner-close"] {
    background-color: transparent;
    border: none;
    color: #1e1e1e;
    padding: 0px;
    font-weight: 600;
}
QPushButton[cssClass="update-banner-close"]:hover {
    background-color: rgba(0, 0, 0, 0.15);
    border-radius: 4px;
}
"""

LIGHT_PALETTE = """
QWidget {
    background-color: #eff1f5;
    color: #4c4f69;
    font-family: "Segoe UI", Helvetica, Arial, sans-serif;
}
QMainWindow, QDialog {
    background-color: #eff1f5;
}
QMenuBar {
    background-color: #e6e9ef;
    color: #4c4f69;
    border-bottom: 1px solid #ccd0da;
    padding: 2px 4px;
}
QMenuBar::item {
    padding: 4px 10px;
    border-radius: 5px;
}
QMenuBar::item:selected {
    background-color: #ccd0da;
}
QMenu {
    background-color: #ffffff;
    color: #4c4f69;
    border: 1px solid #ccd0da;
    border-radius: 8px;
    padding: 6px;
}
QMenu::item {
    padding: 6px 24px 6px 12px;
    border-radius: 5px;
}
QMenu::item:selected {
    background-color: #e6e9ef;
}
QMenu::separator {
    height: 1px;
    background-color: #ccd0da;
    margin: 6px 4px;
}
QLineEdit {
    background-color: #ffffff;
    color: #4c4f69;
    border: 1px solid #bcc0cc;
    border-radius: 7px;
    padding: 3px 10px;
    selection-background-color: #1e66f5;
    selection-color: #eff1f5;
}
QLineEdit:hover {
    border: 1px solid #acb0be;
}
QLineEdit:focus {
    border: 1px solid #1e66f5;
}
QLineEdit:disabled {
    color: #9ca0b0;
    background-color: #e6e9ef;
}
QPushButton {
    background-color: #ffffff;
    color: #4c4f69;
    border: 1px solid #bcc0cc;
    border-radius: 7px;
    padding: 6px 14px;
    font-weight: 500;
}
QPushButton:hover {
    background-color: #e6e9ef;
    border-color: #acb0be;
}
QPushButton:pressed {
    background-color: #ccd0da;
}
QPushButton:disabled {
    color: #9ca0b0;
    background-color: #e6e9ef;
    border-color: #ccd0da;
}
QPushButton[cssClass="primary"] {
    background-color: #1e66f5;
    color: #eff1f5;
    border: 1px solid #1e66f5;
    font-weight: 600;
}
QPushButton[cssClass="primary"]:hover {
    background-color: #4783f6;
    border-color: #4783f6;
}
QPushButton[cssClass="primary"]:pressed {
    background-color: #1857d1;
}
QPushButton[cssClass="danger"] {
    background-color: transparent;
    color: #d20f39;
    border: 1px solid #bcc0cc;
}
QPushButton[cssClass="danger"]:hover {
    background-color: rgba(210, 15, 57, 0.10);
    border-color: #d20f39;
}
QPushButton[cssClass="danger"]:pressed {
    background-color: rgba(210, 15, 57, 0.20);
}
QPushButton[cssClass="compact"] {
    padding: 2px 6px;
    font-weight: 400;
}
QPushButton[cssClass="danger-compact"] {
    background-color: transparent;
    color: #d20f39;
    border: 1px solid #bcc0cc;
    padding: 2px 8px;
    font-weight: 400;
}
QPushButton[cssClass="danger-compact"]:hover {
    background-color: rgba(210, 15, 57, 0.10);
    border-color: #d20f39;
}
QPushButton[cssClass="danger-compact"]:pressed {
    background-color: rgba(210, 15, 57, 0.20);
}
QLabel {
    color: #4c4f69;
}
QLabel[cssClass="heading"] {
    color: #1e66f5;
}
QLabel[cssClass="muted"] {
    color: #6c6f85;
}
QLabel[cssClass="chip"] {
    background-color: #ffffff;
    border: 1px solid #ccd0da;
    border-radius: 6px;
}
QFrame[frameShape="4"],
QFrame[frameShape="5"] {
    color: #ccd0da;
    max-height: 1px;
}
QComboBox {
    background-color: #ffffff;
    color: #4c4f69;
    border: 1px solid #bcc0cc;
    border-radius: 7px;
    padding: 3px 10px;
}
QComboBox:hover {
    border: 1px solid #acb0be;
}
QComboBox::drop-down {
    border: none;
    width: 22px;
}
QComboBox QAbstractItemView {
    background-color: #ffffff;
    color: #4c4f69;
    border: 1px solid #ccd0da;
    border-radius: 8px;
    selection-background-color: #e6e9ef;
    outline: none;
    padding: 4px;
}
QCheckBox {
    color: #4c4f69;
    spacing: 8px;
}
QCheckBox::indicator {
    width: 16px;
    height: 16px;
    border: 1px solid #acb0be;
    border-radius: 4px;
    background-color: #ffffff;
}
QCheckBox::indicator:hover {
    border-color: #1e66f5;
}
QCheckBox::indicator:checked {
    background-color: #1e66f5;
    border-color: #1e66f5;
}
QListWidget {
    background-color: #ffffff;
    color: #4c4f69;
    border: 1px solid #ccd0da;
    border-radius: 8px;
    padding: 4px;
}
QListWidget::item {
    padding: 7px 8px;
    border-radius: 5px;
}
QListWidget::item:hover {
    background-color: #e6e9ef;
}
QListWidget::item:selected {
    background-color: #1e66f5;
    color: #eff1f5;
}
QScrollBar:vertical {
    background: transparent;
    width: 12px;
    margin: 2px;
}
QScrollBar::handle:vertical {
    background: #bcc0cc;
    border-radius: 5px;
    min-height: 24px;
}
QScrollBar::handle:vertical:hover {
    background: #acb0be;
}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {
    height: 0px;
}
QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical {
    background: transparent;
}
QScrollBar:horizontal {
    background: transparent;
    height: 12px;
    margin: 2px;
}
QScrollBar::handle:horizontal {
    background: #bcc0cc;
    border-radius: 5px;
    min-width: 24px;
}
QScrollBar::handle:horizontal:hover {
    background: #acb0be;
}
QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {
    width: 0px;
}
QScrollBar::add-page:horizontal, QScrollBar::sub-page:horizontal {
    background: transparent;
}
QToolTip {
    background-color: #ffffff;
    color: #4c4f69;
    border: 1px solid #ccd0da;
    border-radius: 5px;
    padding: 4px 8px;
}
QWidget[cssClass="card"] {
    background-color: #ffffff;
    border: 1px solid #ccd0da;
    border-radius: 10px;
}
QLabel[cssClass="time-value"] {
    color: #4c4f69;
}
QLabel[cssClass="time-value"]:hover {
    color: #1e66f5;
}
QWidget[cssClass="update-banner"] {
    background-color: #1e66f5;
    border-bottom: 1px solid #1857d1;
}
QLabel[cssClass="update-banner-text"] {
    color: #eff1f5;
    background-color: transparent;
}
QLabel[cssClass="update-banner-text"]:hover {
    color: #ffffff;
    background-color: transparent;
}
QPushButton[cssClass="update-banner-close"] {
    background-color: transparent;
    border: none;
    color: #eff1f5;
    padding: 0px;
    font-weight: 600;
}
QPushButton[cssClass="update-banner-close"]:hover {
    background-color: rgba(0, 0, 0, 0.08);
    border-radius: 4px;
}
"""


def stylesheet_for(theme: str) -> str:
    """Resolves a theme name (as stored in settings) to a Qt stylesheet."""
    match theme:
        case "Dark":
            return DARK_PALETTE
        case "Light":
            return LIGHT_PALETTE
        case _:
            # "Automatic" and any unrecognized value both follow the OS theme.
            return DARK_PALETTE if is_dark_mode() else LIGHT_PALETTE
