# Standard library
import json
import re
import sys
from decimal import Decimal as d, InvalidOperation, DivisionByZero, DivisionUndefined
from webbrowser import open as open_url
from typing import NoReturn

# Third-party
from PySide6.QtWidgets import (
    QApplication, QMessageBox, QFileDialog
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QGuiApplication
from requests import get as get_url

# Local application
from crt._version import __version__
from crt.app_settings.app import Settings
from crt.decorators import error_handler
from crt.gui import MainGUI
from crt.load import Load
from crt.save_as.app import SaveAs
from crt.session_history import SessionHistory
from crt.time import Time


def _is_dark_mode() -> bool:
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


# ── Palette helpers ────────────────────────────────────────────────────────────

DARK_PALETTE = """
QWidget {
    background-color: #1e1e2e;
    color: #cdd6f4;
    font-family: "Segoe UI", Helvetica, Arial, sans-serif;
}
QMainWindow, QDialog {
    background-color: #1e1e2e;
}
QMenuBar {
    background-color: #181825;
    color: #cdd6f4;
    border-bottom: 1px solid #313244;
    padding: 2px 4px;
}
QMenuBar::item {
    padding: 4px 10px;
    border-radius: 5px;
}
QMenuBar::item:selected {
    background-color: #313244;
}
QMenu {
    background-color: #181825;
    color: #cdd6f4;
    border: 1px solid #313244;
    border-radius: 8px;
    padding: 6px;
}
QMenu::item {
    padding: 6px 24px 6px 12px;
    border-radius: 5px;
}
QMenu::item:selected {
    background-color: #313244;
}
QMenu::separator {
    height: 1px;
    background-color: #313244;
    margin: 6px 4px;
}
QLineEdit {
    background-color: #313244;
    color: #cdd6f4;
    border: 1px solid #45475a;
    border-radius: 7px;
    padding: 3px 10px;
    selection-background-color: #89b4fa;
    selection-color: #1e1e2e;
}
QLineEdit:hover {
    border: 1px solid #585b70;
}
QLineEdit:focus {
    border: 1px solid #89b4fa;
}
QLineEdit:disabled {
    color: #6c7086;
    background-color: #232334;
}
QPushButton {
    background-color: #313244;
    color: #cdd6f4;
    border: 1px solid #45475a;
    border-radius: 7px;
    padding: 6px 14px;
    font-weight: 500;
}
QPushButton:hover {
    background-color: #45475a;
    border-color: #585b70;
}
QPushButton:pressed {
    background-color: #292a3a;
}
QPushButton:disabled {
    color: #6c7086;
    background-color: #232334;
    border-color: #313244;
}
QPushButton[cssClass="primary"] {
    background-color: #89b4fa;
    color: #1e1e2e;
    border: 1px solid #89b4fa;
    font-weight: 600;
}
QPushButton[cssClass="primary"]:hover {
    background-color: #a6c8fc;
    border-color: #a6c8fc;
}
QPushButton[cssClass="primary"]:pressed {
    background-color: #74a8f9;
}
QPushButton[cssClass="danger"] {
    background-color: transparent;
    color: #f38ba8;
    border: 1px solid #45475a;
}
QPushButton[cssClass="danger"]:hover {
    background-color: rgba(243, 139, 168, 0.15);
    border-color: #f38ba8;
}
QPushButton[cssClass="danger"]:pressed {
    background-color: rgba(243, 139, 168, 0.28);
}
QPushButton[cssClass="compact"] {
    padding: 2px 6px;
    font-weight: 400;
}
QPushButton[cssClass="danger-compact"] {
    background-color: transparent;
    color: #f38ba8;
    border: 1px solid #45475a;
    padding: 2px 8px;
    font-weight: 400;
}
QPushButton[cssClass="danger-compact"]:hover {
    background-color: rgba(243, 139, 168, 0.15);
    border-color: #f38ba8;
}
QPushButton[cssClass="danger-compact"]:pressed {
    background-color: rgba(243, 139, 168, 0.28);
}
QLabel {
    color: #cdd6f4;
}
QLabel[cssClass="heading"] {
    color: #b4befe;
}
QLabel[cssClass="muted"] {
    color: #a6adc8;
}
QLabel[cssClass="chip"] {
    background-color: #313244;
    border: 1px solid #45475a;
    border-radius: 6px;
}
QFrame[frameShape="4"],
QFrame[frameShape="5"] {
    color: #313244;
    max-height: 1px;
}
QComboBox {
    background-color: #313244;
    color: #cdd6f4;
    border: 1px solid #45475a;
    border-radius: 7px;
    padding: 3px 10px;
}
QComboBox:hover {
    border: 1px solid #585b70;
}
QComboBox::drop-down {
    border: none;
    width: 22px;
}
QComboBox QAbstractItemView {
    background-color: #181825;
    color: #cdd6f4;
    border: 1px solid #313244;
    border-radius: 8px;
    selection-background-color: #45475a;
    outline: none;
    padding: 4px;
}
QCheckBox {
    color: #cdd6f4;
    spacing: 8px;
}
QCheckBox::indicator {
    width: 16px;
    height: 16px;
    border: 1px solid #585b70;
    border-radius: 4px;
    background-color: #313244;
}
QCheckBox::indicator:hover {
    border-color: #89b4fa;
}
QCheckBox::indicator:checked {
    background-color: #89b4fa;
    border-color: #89b4fa;
}
QListWidget {
    background-color: #181825;
    color: #cdd6f4;
    border: 1px solid #313244;
    border-radius: 8px;
    padding: 4px;
}
QListWidget::item {
    padding: 7px 8px;
    border-radius: 5px;
}
QListWidget::item:hover {
    background-color: #232334;
}
QListWidget::item:selected {
    background-color: #89b4fa;
    color: #1e1e2e;
}
QScrollBar:vertical {
    background: transparent;
    width: 12px;
    margin: 2px;
}
QScrollBar::handle:vertical {
    background: #45475a;
    border-radius: 5px;
    min-height: 24px;
}
QScrollBar::handle:vertical:hover {
    background: #585b70;
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
    background: #45475a;
    border-radius: 5px;
    min-width: 24px;
}
QScrollBar::handle:horizontal:hover {
    background: #585b70;
}
QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {
    width: 0px;
}
QScrollBar::add-page:horizontal, QScrollBar::sub-page:horizontal {
    background: transparent;
}
QToolTip {
    background-color: #181825;
    color: #cdd6f4;
    border: 1px solid #45475a;
    border-radius: 5px;
    padding: 4px 8px;
}
QWidget[cssClass="card"] {
    background-color: #232334;
    border: 1px solid #313244;
    border-radius: 10px;
}
QLabel[cssClass="time-value"] {
    color: #cdd6f4;
}
QLabel[cssClass="time-value"]:hover {
    color: #89b4fa;
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
"""


def _popup_yes_no(title: str, message: str) -> bool:
    """Shows a Yes/No message box. Returns True if Yes."""
    box = QMessageBox()
    box.setWindowTitle(title)
    box.setText(message)
    box.setStandardButtons(QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
    box.setDefaultButton(QMessageBox.StandardButton.No)
    return box.exec() == QMessageBox.StandardButton.Yes


def _popup_ok(title: str, message: str):
    """Shows an informational popup."""
    box = QMessageBox()
    box.setWindowTitle(title)
    box.setText(message)
    box.setStandardButtons(QMessageBox.StandardButton.Ok)
    box.exec()


def _popup_error(title: str, message: str):
    """Shows an error popup."""
    box = QMessageBox()
    box.setWindowTitle(title)
    box.setText(str(message))
    box.setIcon(QMessageBox.Icon.Critical)
    box.setStandardButtons(QMessageBox.StandardButton.Ok)
    box.exec()


def _clipboard_get() -> str:
    """Returns the current clipboard text."""
    return QGuiApplication.clipboard().text()


def _clipboard_set(text: str):
    """Sets the clipboard text."""
    QGuiApplication.clipboard().setText(str(text))


def _time_components(time_val: d):
    """Extracts (hours, minutes, seconds, milliseconds) strings from a Decimal time value.

    This replicates the logic inside the @format_time decorator's format_components helper
    so it can be called directly without going through the decorator wrapper.
    """
    time_str = str(max(time_val, d(0)))
    if '.' in time_str:
        seconds_part, ms_part = time_str.split(".", 1)
        seconds_total = int(seconds_part)
        milliseconds = int(ms_part)
    else:
        seconds_total = int(time_str)
        milliseconds = 0

    minutes, seconds = divmod(seconds_total, 60)
    hours, minutes = divmod(minutes, 60)

    return (
        f"{hours:02}",
        f"{minutes:02}",
        f"{seconds:02}",
        str(milliseconds).rjust(3, "0"),
    )


class App:
    """Main application for CRT."""

    def __init__(self) -> NoReturn:
        """Initializes the App class."""
        # QApplication must exist before any Qt widgets
        self._qt_app = QApplication.instance() or QApplication([])

        self.time = Time()
        self.file_path = None
        self.past_file_paths = []

        self.settings = Settings()
        self.settings_dict = self.settings.config_to_dict()

        # Load stats tracking
        self._load_stats = {
            'total_loads': 0,
            'avg_length': 0
        }

        # Apply theme via stylesheet
        self._apply_theme(self.settings_dict["theme"])

        if self.settings_dict["enable_updates"]:
            self._check_for_updates()

        self.language = self.settings.language
        self.window = MainGUI(self.language.content)

    def _apply_theme(self, theme: str):
        """Applies a Qt stylesheet theme."""
        match theme:
            case "Automatic":
                stylesheet = DARK_PALETTE if _is_dark_mode() else LIGHT_PALETTE
            case "Dark":
                stylesheet = DARK_PALETTE
            case "Light":
                stylesheet = LIGHT_PALETTE
            case _:
                stylesheet = DARK_PALETTE if _is_dark_mode() else LIGHT_PALETTE
        self._qt_app.setStyleSheet(stylesheet)

    def _check_for_updates(self) -> NoReturn:
        """Checks for updates."""
        try:
            response = get_url(
                "https://api.github.com/repos/connerglover/Conners-Retime-Tool/releases/latest",
                timeout=5
            )
            if response.status_code == 200:
                latest_release = response.json()
                latest_version = latest_release["tag_name"]
                if str(latest_version) != str(__version__):
                    if _popup_yes_no(
                        "Update Available",
                        f"A new version of CRT is available: {latest_version}.\nWould you like to update?"
                    ):
                        open_url("https://github.com/connerglover/Conners-Retime-Tool/releases/latest")
        except Exception:
            pass  # Silently ignore network errors during update check

    @error_handler
    def _on_load_edited(self, index: int, start_text: str, end_text: str) -> NoReturn:
        """Handles an inline edit to a load row in the sidebar."""
        if index >= len(self.time.loads):
            return
        start_frame = self._parse_frame_input(start_text, self.time)
        end_frame = self._parse_frame_input(end_text, self.time)
        self.time.mutate_load(index, start_frame=start_frame, end_frame=end_frame)
        self._update_displays()

    def _on_load_deleted(self, index: int) -> NoReturn:
        """Handles a delete request from a load row in the sidebar."""
        if 0 <= index < len(self.time.loads):
            self.time.delete_load(index)
        self._update_displays()

    @error_handler
    def _add_loads(self, values: dict) -> NoReturn:
        """Adds the loads."""
        start_frame = int(values.get("start_loads") or 0)
        end_frame = int(values.get("end_loads") or 0)

        if self.time.loads:
            if self._load_stats['total_loads'] != len(self.time.loads):
                self._load_stats['avg_length'] = (
                    sum(load.end_frame - load.start_frame for load in self.time.loads)
                    / len(self.time.loads)
                )
                self._load_stats['total_loads'] = len(self.time.loads)

            if (end_frame - start_frame) > self._load_stats['avg_length'] * 10:
                if not _popup_yes_no("Woah!", "This load is concerningly long. Would you like to add the load anyway?"):
                    return

        self.time.add_load(start_frame, end_frame)
        self._set_input("start_loads", "0")
        self._set_input("end_loads", "0")
        self._update_displays()
        _popup_ok("Loads", "Load added successfully.")

    # ── Input parsing helpers ──────────────────────────────────────────────────

    @staticmethod
    def _is_debug_info(text: str) -> bool:
        """Returns True if the text looks like YouTube debug info JSON."""
        return '{' in text and '"cmt"' in text

    def debug_info_to_frame(self, time: Time, debug_info: str) -> int:
        """Converts YouTube debug info JSON to a frame number."""
        start_pos = debug_info.find('{')
        if start_pos == -1:
            raise ValueError("The debug info provided is invalid.\nPlease re-enter debug info.")
        debug_info = debug_info[start_pos:]
        try:
            parsed = json.loads(debug_info)
            cmt = parsed["cmt"]
        except (json.decoder.JSONDecodeError, KeyError):
            raise ValueError("The debug info provided is invalid.\nPlease re-enter debug info.")
        output = int(round(d(str(cmt)) * d(str(time.framerate)), 0))
        return output

    def _clean_framerate(self, framerate: str) -> d:
        """Cleans a framerate string into a valid Decimal.

        Rules:
        - Strip all non-numeric, non-decimal characters.
        - If empty or no digits remain, return Decimal('0').
        - Collapse multiple decimal points (keep only the first).
        - Trailing decimal point gets a '0' appended.
        """
        cleaned = re.sub(r'[^0-9.]', '', framerate)
        if not re.search(r'[0-9]', cleaned):
            return d('0')
        # Collapse multiple decimal points
        if cleaned.count('.') > 1:
            idx = cleaned.find('.')
            cleaned = cleaned[:idx + 1] + cleaned[idx + 1:].replace('.', '')
        if cleaned.endswith('.'):
            cleaned += '0'
        try:
            return d(cleaned)
        except (InvalidOperation, ValueError):
            return d('0')

    def _parse_frame_input(self, text: str, time: Time) -> int:
        """Parse a frame input field according to the full validation spec:

        1. If it looks like YouTube debug info, extract the frame from JSON.
        2. Otherwise strip all non-numeric, non-decimal characters.
        3. If empty after stripping, return 0.
        4. If a decimal point is present, treat the value as a timestamp in
           seconds and convert to a frame number (value * framerate, rounded).
        5. Otherwise return the integer value.
        """
        text = text.strip()

        # Step 1 — debug info
        if self._is_debug_info(text):
            return self.debug_info_to_frame(time, text)

        # Step 2 — strip non-numeric/non-decimal characters
        cleaned = re.sub(r'[^0-9.]', '', text)

        # Step 3 — empty → 0
        if not cleaned or not re.search(r'[0-9]', cleaned):
            return 0

        # Collapse multiple decimal points
        if cleaned.count('.') > 1:
            idx = cleaned.find('.')
            cleaned = cleaned[:idx + 1] + cleaned[idx + 1:].replace('.', '')

        # Step 4 — decimal → timestamp conversion
        if '.' in cleaned:
            try:
                fps = d(str(time.framerate))
                if fps == 0:
                    return 0
                return int(round(d(cleaned) * fps, 0))
            except (InvalidOperation, ValueError):
                return 0

        # Step 5 — plain integer
        try:
            return int(cleaned)
        except ValueError:
            return 0

    def clean_frame(self, frame: str) -> int:
        """Legacy wrapper — cleans a frame string to an integer (no decimal/debug handling)."""
        return self._parse_frame_input(frame, self.time)

    # ── Widget accessors ───────────────────────────────────────────────────────

    def _set_input(self, key: str, value: str):
        """Updates a QLineEdit in the main window by object name."""
        from PySide6.QtWidgets import QLineEdit
        widget = self.window.window.findChild(QLineEdit, key)
        if widget:
            widget.blockSignals(True)
            widget.setText(str(value))
            widget.blockSignals(False)

    def _get_input(self, key: str) -> str:
        """Gets the text of a QLineEdit in the main window by object name."""
        from PySide6.QtWidgets import QLineEdit
        widget = self.window.window.findChild(QLineEdit, key)
        return widget.text() if widget else ""

    # ── Input event handlers ───────────────────────────────────────────────────

    def _set_framerate(self, new_value: str) -> NoReturn:
        """Handles the framerate input."""
        framerate = self._clean_framerate(new_value)
        self._set_input("framerate", str(framerate))
        self.time.mutate(framerate=framerate)
        self._update_displays()

    @error_handler
    def _set_time(self, key: str, new_value: str) -> NoReturn:
        """Handles the start/end frame inputs."""
        frame = self._parse_frame_input(new_value, self.time)
        match key:
            case "start":
                self.time.mutate(start_frame=frame)
            case "end":
                self.time.mutate(end_frame=frame)
        self._set_input(key, frame)
        self._update_displays()

    def _set_loads(self, key: str, new_value: str) -> NoReturn:
        """Handles the loads start/end frame inputs."""
        try:
            frame = self._parse_frame_input(new_value, self.time)
        except ValueError as e:
            frame = 0
            self._show_error(e)
        self._set_input(key, frame)

    # ── File operations ────────────────────────────────────────────────────────

    def _new_time(self) -> NoReturn:
        """Creates a new time."""
        self._save_as_time()
        self.time = Time()
        self._set_input("framerate", self.time.framerate)
        self._set_input("start", self.time.start_frame)
        self._set_input("end", self.time.end_frame)
        self._set_input("start_loads", "0")
        self._set_input("end_loads", "0")
        self._update_displays()

    def _convert_to_dict(self) -> dict:
        """Converts the time to a JSON dictionary."""
        return {
            "start_frame": self.time.start_frame,
            "end_frame": self.time.end_frame,
            "framerate": str(self.time.framerate),
            "loads": [(load.start_frame, load.end_frame) for load in self.time.loads]
        }

    def _open_time(self) -> NoReturn:
        """Opens a time."""
        old_file_path = self.file_path
        new_file_path, _ = QFileDialog.getOpenFileName(
            self.window.window, "Open Time", "", "Time Files (*.json)"
        )
        if not new_file_path:
            return

        if new_file_path != old_file_path:
            self.file_path = new_file_path
            if old_file_path not in self.past_file_paths and old_file_path is not None:
                self.past_file_paths.append(old_file_path)

        if self.file_path and self.file_path != old_file_path:
            with open(self.file_path, "r") as file:
                try:
                    file_data = json.load(file)
                except json.decoder.JSONDecodeError:
                    raise ValueError("The file provided is corrupted.")

                loads = [Load(load[0], load[1]) for load in file_data["loads"]]
                self.time.mutate(
                    start_frame=file_data["start_frame"],
                    end_frame=file_data["end_frame"],
                    framerate=file_data["framerate"]
                )
                self.time.loads = loads

                self._set_input("start", self.time.start_frame)
                self._set_input("end", self.time.end_frame)
                self._set_input("framerate", self.time.framerate)
                self._set_input("start_loads", "0")
                self._set_input("end_loads", "0")
                self._update_displays()

    def _save_time(self) -> NoReturn:
        """Saves the time."""
        if self.file_path:
            with open(self.file_path, "w") as file:
                json.dump(self._convert_to_dict(), file)
            _popup_ok("Save", "Time saved successfully.")
        else:
            self._save_as_time()

    @error_handler
    def _session_history(self) -> NoReturn:
        """Opens the session history."""
        old_file_path = self.file_path
        session_history = SessionHistory(self.language, self.past_file_paths)
        new_file_path = session_history.run()

        if old_file_path and _popup_yes_no("Save", "Would you like to save the current file?"):
            self._save_time()

        if new_file_path and new_file_path != old_file_path:
            self.file_path = new_file_path
            if new_file_path in self.past_file_paths:
                self.past_file_paths.remove(new_file_path)
            if old_file_path and old_file_path not in self.past_file_paths:
                self.past_file_paths.append(old_file_path)

            try:
                with open(new_file_path, "r") as file:
                    file_data = json.load(file)

                loads = [Load(load[0], load[1]) for load in file_data["loads"]]
                self.time.mutate(
                    start_frame=file_data["start_frame"],
                    end_frame=file_data["end_frame"],
                    framerate=file_data["framerate"]
                )
                self.time.loads = loads

                self._set_input("start", self.time.start_frame)
                self._set_input("end", self.time.end_frame)
                self._set_input("framerate", self.time.framerate)
                self._set_input("start_loads", "0")
                self._set_input("end_loads", "0")

            except json.decoder.JSONDecodeError:
                raise ValueError("The file provided is corrupted.")

        self._update_displays()

    def _settings(self) -> NoReturn:
        """Opens the settings."""
        old_settings_dict = self.settings_dict
        self.settings.open_window()
        self.settings_dict = self.settings.config_to_dict()

        if self.settings_dict != old_settings_dict:
            _popup_ok("Settings", "Please restart the application to apply the changes.")

    def _save_as_time(self) -> NoReturn:
        """Saves the time as a new time."""
        old_file_path = self.file_path
        new_file_path = SaveAs(self.language).run()
        if new_file_path != old_file_path:
            self.file_path = new_file_path
            if old_file_path not in self.past_file_paths and old_file_path is not None:
                self.past_file_paths.append(old_file_path)

        if self.file_path:
            with open(self.file_path, "w") as file:
                json.dump(self._convert_to_dict(), file)

    # ── Mod note ───────────────────────────────────────────────────────────────

    @property
    def _mod_note(self) -> str:
        """Gets the mod note."""
        # Guard against zero framerate to avoid DivisionUndefined
        fps = self.time.framerate
        if not fps or fps == 0:
            start_time = 0
            end_time = 0
        else:
            start_time = round(float(self.time.start_frame) / float(fps), self.time.precision)
            end_time = round(float(self.time.end_frame) / float(fps), self.time.precision)

        # Extract time components without calling the non-existent format_time_components method
        hours, minutes, seconds, milliseconds = _time_components(self.time.with_loads)

        return self.settings_dict["mod_note_format"].format(
            time_with_loads=self.time.iso_format(False),
            time_without_loads=self.time.iso_format(True),
            hours=hours,
            minutes=minutes,
            seconds=seconds,
            milliseconds=milliseconds,
            start_frame=self.time.start_frame,
            end_frame=self.time.end_frame,
            start_time=start_time,
            end_time=end_time,
            total_frames=self.time.length_with_loads,
            fps=fps,
            plug="[Conner's Retime Tool](https://github.com/connerglover/conners-retime-tool)",
        )

    # ── Display updates ────────────────────────────────────────────────────────

    def _update_displays(self) -> NoReturn:
        """Update time displays."""
        from crt.gui import ClickableLabel
        wl = self.window.window.findChild(ClickableLabel, "without_loads_display")
        ld = self.window.window.findChild(ClickableLabel, "loads_display")
        if wl:
            try:
                wl.setText(self.time.iso_format(True))
            except (DivisionByZero, DivisionUndefined, InvalidOperation):
                wl.setText("00.000")
        if ld:
            try:
                ld.setText(self.time.iso_format(False))
            except (DivisionByZero, DivisionUndefined, InvalidOperation):
                ld.setText("00.000")
        self._refresh_load_sidebar()

    def _refresh_load_sidebar(self) -> NoReturn:
        """Refreshes the embedded loads sidebar to match self.time.loads."""
        self.window.window.refresh_loads(
            self.time.loads, self.time.framerate, self.time.precision, self.language.content
        )

    def _show_error(self, message):
        """Shows a popup message of the error."""
        _popup_error("Error", message)

    def _get_all_values(self) -> dict:
        """Reads all input values from the main window."""
        return {
            key: self._get_input(key)
            for key in ("framerate", "start", "end", "start_loads", "end_loads")
        }

    # ── Main event loop ────────────────────────────────────────────────────────

    def run(self) -> NoReturn:
        """Runs the application."""
        from PySide6.QtWidgets import QLineEdit, QPushButton
        from crt.gui import ClickableLabel

        win = self.window.window

        def _on_menu_action(action):
            key = action.data()
            self._dispatch(key, self._get_all_values())

        # Connect all QActions from all menus
        for action in win.findChildren(type(win.menuBar().actions()[0])):
            action.triggered.connect(lambda checked=False, a=action: _on_menu_action(a))

        # Input fields — use editingFinished so we only validate on focus-out / Enter
        # but also connect textEdited for live load-field cleaning
        for key in ("framerate", "start", "end", "start_loads", "end_loads"):
            inp = win.findChild(QLineEdit, key)
            if inp:
                inp.editingFinished.connect(
                    lambda k=key: self._dispatch(k, {k: self._get_input(k)})
                )

        # Paste buttons
        for key in ("framerate", "start", "end", "start_loads", "end_loads"):
            btn = win.findChild(QPushButton, f"{key}_paste")
            if btn:
                btn.clicked.connect(lambda checked=False, k=key: self._dispatch(f"{k}_paste", {}))

        # Action buttons
        win.btn_copy_mod_note.clicked.connect(lambda: self._dispatch("Copy Mod Note", {}))
        win.btn_add_loads.clicked.connect(lambda: self._dispatch("Add Loads", self._get_all_values()))

        # Embedded loads sidebar — live inline editing, no modal dialog
        win.load_edited.connect(self._on_load_edited)
        win.load_delete_requested.connect(self._on_load_deleted)

        # Clickable display labels
        wl = win.findChild(ClickableLabel, "without_loads_display")
        ld = win.findChild(ClickableLabel, "loads_display")
        if wl:
            wl.clicked.connect(lambda: _clipboard_set(self.time.iso_format(True)))
        if ld:
            ld.clicked.connect(lambda: _clipboard_set(self.time.iso_format(False)))

        # Show the window and start the Qt event loop
        win.show()
        self._qt_app.exec()

        # On exit, offer to save
        if self.file_path and _popup_yes_no("Exit", "Would you like to save?"):
            self._save_time()

    def _dispatch(self, event: str, values: dict):
        """Dispatches an event to the appropriate handler."""
        match event:
            case "New Time":
                self._new_time()
            case "Open Time":
                self._open_time()
            case "Session History":
                self._session_history()
            case "Save":
                self._save_time()
            case "Save As":
                self._save_as_time()
            case "Settings":
                self._settings()
            case "Clear Loads":
                self.time.clear_loads()
                self._update_displays()
            case "Check for Updates":
                self._check_for_updates()
            case "Report Issue":
                open_url("https://forms.gle/mnmbgt6cBeL6Dykk6")
            case "Suggest Feature":
                open_url("https://forms.gle/V5bPaQbcFsk6Cijr5")
            case "About":
                _popup_ok(
                    "About",
                    f"Conner's Retime Tool v{__version__}\n\n"
                    "Created by Conner Glover\n\n"
                    "Credits:\nMenzo: French and Polish Translations\n"
                    "AmazinCris: Spanish Translations\n\n"
                    "© 2024 Conner Glover"
                )
            case "Add Loads":
                self._add_loads(values)
            case "Copy Mod Note":
                try:
                    _clipboard_set(self._mod_note)
                except Exception as e:
                    self._show_error(e)
            case "framerate_paste":
                self._set_framerate(_clipboard_get())
            case "start_paste":
                self._set_time("start", _clipboard_get())
            case "end_paste":
                self._set_time("end", _clipboard_get())
            case "start_loads_paste":
                self._set_loads("start_loads", _clipboard_get())
            case "end_loads_paste":
                self._set_loads("end_loads", _clipboard_get())
            case "framerate":
                self._set_framerate(values.get("framerate", ""))
            case "start":
                self._set_time("start", values.get("start", ""))
            case "end":
                self._set_time("end", values.get("end", ""))
            case "start_loads":
                self._set_loads("start_loads", values.get("start_loads", ""))
            case "end_loads":
                self._set_loads("end_loads", values.get("end_loads", ""))
            case "Exit":
                self.window.window.close()
            case _ as e:
                print(f"Unhandled event: {e}")

        self._update_displays()
