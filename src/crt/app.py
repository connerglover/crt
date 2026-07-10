# Standard library
import sys
from decimal import InvalidOperation, DivisionByZero, DivisionUndefined
from pathlib import Path
from typing import NoReturn

# Third-party
from PySide6.QtWidgets import (
    QApplication, QFileDialog, QLineEdit, QPushButton
)
from PySide6.QtCore import Qt
from PySide6.QtGui import QAction, QGuiApplication, QIcon

# Local application
from crt._version import __version__
from crt.app_settings.app import Settings
from crt.decorators import error_handler, format_components, format_frame_time
from crt.file_manager import FileManager
from crt.frame_input import clean_framerate, parse_frame_input
from crt.gui import ClickableLabel, MainGUI
from crt.popups import popup_yes_no as _popup_yes_no, popup_ok as _popup_ok, popup_error as _popup_error
from crt.session_history import SessionHistory
from crt.theme import stylesheet_for
from crt.updater import check_for_updates, open_releases_page


def _icon_path() -> str:
    """Resolves the path to icon.ico for both `python main.py` and the frozen PyInstaller exe.

    PyInstaller's --icon flag only stamps the .exe file's Explorer/taskbar icon;
    it does not make the file available to the running program. Qt then falls back
    to its own generic icon for the window/taskbar unless we load and set one
    ourselves, so the icon has to be bundled as data (see build.yml's --add-data)
    and located at runtime via sys._MEIPASS when frozen.
    """
    base_path = getattr(sys, "_MEIPASS", None)
    if base_path is None:
        base_path = Path(__file__).resolve().parent.parent
    return str(Path(base_path) / "icon.ico")


def _clipboard_get() -> str:
    """Returns the current clipboard text."""
    return QGuiApplication.clipboard().text()


def _clipboard_set(text: str):
    """Sets the clipboard text."""
    QGuiApplication.clipboard().setText(str(text))


class App:
    """Main application for CRT."""

    def __init__(self) -> NoReturn:
        """Initializes the App class."""
        # QApplication must exist before any Qt widgets
        self._qt_app = QApplication.instance() or QApplication([])

        icon_path = _icon_path()
        if Path(icon_path).exists():
            self._qt_app.setWindowIcon(QIcon(icon_path))

        self.files = FileManager()

        self.settings = Settings()
        self.settings_dict = self.settings.config_to_dict()

        # Load stats tracking
        self._load_stats = {
            'total_loads': 0,
            'avg_length': 0
        }

        # Apply theme via stylesheet
        self._apply_theme(self.settings_dict["theme"])

        self.language = self.settings.language
        self.window = MainGUI(self.language.content)
        if Path(icon_path).exists():
            self.window.window.setWindowIcon(QIcon(icon_path))

        # Enabled by default. Set the flag directly rather than via
        # _set_always_on_top(), which calls win.show() to reapply the flag on an
        # already-visible window — calling that here, before run() wires up the
        # window and shows it, would flash a blank window while startup work
        # (e.g. the update check below) is still blocking.
        self._always_on_top = True
        self.window.window.action_always_on_top.setChecked(True)
        self.window.window.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, True)

        self.window.window.update_link_clicked.connect(open_releases_page)

        if self.settings_dict["enable_updates"]:
            self._check_for_updates()

        # The loads sidebar defaults both its empty-state message and its (empty)
        # scroll area to visible until refresh_loads() runs once to reconcile them —
        # without this, the first dispatch (e.g. clicking off any input) collapses the
        # stretchy hidden scroll area out from under the empty-state label, causing a
        # visible jump. Refresh once up front so the sidebar starts in its final state.
        self._refresh_load_sidebar()

    def _apply_theme(self, theme: str):
        """Applies a Qt stylesheet theme."""
        self._qt_app.setStyleSheet(stylesheet_for(theme))

    @error_handler
    def _on_load_edited(self, index: int, start_text: str, end_text: str) -> NoReturn:
        """Handles an inline edit to a load row in the sidebar."""
        time = self.files.time
        if index >= len(time.loads):
            return
        start_frame = self._parse_frame(start_text)
        end_frame = self._parse_frame(end_text)
        time.mutate_load(index, start_frame=start_frame, end_frame=end_frame)
        self.files.dirty = True
        self._update_displays()

    def _on_load_deleted(self, index: int) -> NoReturn:
        """Handles a delete request from a load row in the sidebar."""
        time = self.files.time
        if 0 <= index < len(time.loads):
            time.delete_load(index)
            self.files.dirty = True
        self._update_displays()

    @error_handler
    def _add_loads(self, values: dict) -> NoReturn:
        """Adds the loads."""
        start_frame = int(values.get("start_loads") or 0)
        end_frame = int(values.get("end_loads") or 0)
        time = self.files.time

        if time.loads:
            if self._load_stats['total_loads'] != len(time.loads):
                self._load_stats['avg_length'] = (
                    sum(load.end_frame - load.start_frame for load in time.loads)
                    / len(time.loads)
                )
                self._load_stats['total_loads'] = len(time.loads)

            if (end_frame - start_frame) > self._load_stats['avg_length'] * 10:
                if not _popup_yes_no(
                    "Woah!", "This load is concerningly long. Would you like to add the load anyway?",
                    self.window.window, self._always_on_top
                ):
                    return

        time.add_load(start_frame, end_frame)
        self.files.dirty = True
        self._set_input("start_loads", "0")
        self._set_input("end_loads", "0")
        self._update_displays()
        _popup_ok("Loads", "Load added successfully.", self.window.window, self._always_on_top)

    # ── Input parsing helpers ──────────────────────────────────────────────────

    def _parse_frame(self, text: str) -> int:
        """Parses a frame input field against the current session's framerate."""
        return parse_frame_input(text, self.files.time.framerate)

    # ── Widget accessors ───────────────────────────────────────────────────────

    def _set_input(self, key: str, value: str):
        """Updates a QLineEdit in the main window by object name."""
        widget = self.window.window.findChild(QLineEdit, key)
        if widget:
            widget.blockSignals(True)
            widget.setText(str(value))
            widget.blockSignals(False)

    def _get_input(self, key: str) -> str:
        """Gets the text of a QLineEdit in the main window by object name."""
        widget = self.window.window.findChild(QLineEdit, key)
        return widget.text() if widget else ""

    # ── Input event handlers ───────────────────────────────────────────────────

    def _set_framerate(self, new_value: str) -> NoReturn:
        """Handles the framerate input."""
        framerate = clean_framerate(new_value)
        self._set_input("framerate", str(framerate))
        self.files.time.mutate(framerate=framerate)
        self.files.dirty = True
        self._update_displays()

    @error_handler
    def _set_time(self, key: str, new_value: str) -> NoReturn:
        """Handles the start/end frame inputs."""
        frame = self._parse_frame(new_value)
        match key:
            case "start":
                self.files.time.mutate(start_frame=frame)
            case "end":
                self.files.time.mutate(end_frame=frame)
        self.files.dirty = True
        self._set_input(key, frame)
        self._update_displays()

    def _set_loads(self, key: str, new_value: str) -> NoReturn:
        """Handles the loads start/end frame inputs."""
        try:
            frame = self._parse_frame(new_value)
        except ValueError as e:
            frame = 0
            self._show_error(e)
        self._set_input(key, frame)

    # ── File operations ────────────────────────────────────────────────────────

    def _prompt_save_if_dirty(self, title: str) -> bool:
        """Offers to save unsaved changes before a destructive action (new/open/switch).

        Only asks when there's actually something unsaved — the old behavior asked
        unconditionally whenever a file happened to be open, even with no changes.

        Returns:
            bool: False if the user backed out of saving (e.g. cancelled the file
                picker), meaning the caller should abort the destructive action
                rather than discard unsaved work.
        """
        if not self.files.dirty:
            return True
        if not _popup_yes_no(
            title, "Would you like to save the current time first?",
            self.window.window, self._always_on_top
        ):
            return True
        self._save_time()
        return not self.files.dirty

    def _sync_time_inputs(self) -> NoReturn:
        """Refreshes the input fields to match the active session (after new/open/load)."""
        time = self.files.time
        self._set_input("framerate", time.framerate)
        self._set_input("start", time.start_frame)
        self._set_input("end", time.end_frame)
        self._set_input("start_loads", "0")
        self._set_input("end_loads", "0")

    @error_handler
    def _new_time(self) -> NoReturn:
        """Starts a blank time, first offering to save unsaved changes."""
        if not self._prompt_save_if_dirty("New Time"):
            return

        self.files.new_time()
        self._sync_time_inputs()
        self._update_displays()

    @error_handler
    def _open_time(self) -> NoReturn:
        """Opens a time file, first offering to save unsaved changes."""
        if not self._prompt_save_if_dirty("Open Time"):
            return

        new_file_path, _ = QFileDialog.getOpenFileName(
            self.window.window, "Open Time", "", "Time Files (*.json)"
        )
        if not new_file_path or new_file_path == self.files.file_path:
            return

        self.files.load_file(new_file_path)
        self._sync_time_inputs()
        self._update_displays()

    @error_handler
    def _save_time(self) -> NoReturn:
        """Saves the time to the current file, or prompts for a path if there isn't one."""
        if not self.files.file_path:
            self._save_as_time()
            return

        self.files.save()
        self.window.window.statusBar().showMessage(f"Saved to {self.files.file_path}", 3000)

    @error_handler
    def _save_as_time(self) -> NoReturn:
        """Saves the time to a new file chosen via a native file dialog."""
        path, _ = QFileDialog.getSaveFileName(
            self.window.window, "Save As", "", "Time Files (*.json)"
        )
        if not path:
            return
        if not path.endswith(".json"):
            path += ".json"

        self.files.save_as(path)
        self.window.window.statusBar().showMessage(f"Saved to {self.files.file_path}", 3000)

    @error_handler
    def _session_history(self) -> NoReturn:
        """Opens the session history and switches to the selected file, if any."""
        session_history = SessionHistory(
            self.language, self.files.history(), self.window.window, self._always_on_top
        )
        selected_file_path = session_history.run()

        if not selected_file_path or selected_file_path == self.files.file_path:
            return

        if not self._prompt_save_if_dirty("Save"):
            return

        self.files.load_file(selected_file_path)
        self._sync_time_inputs()
        self._update_displays()

    def _set_always_on_top(self, enabled: bool) -> NoReturn:
        """Toggles whether the main window stays above all other windows.

        Changing window flags requires the window to be re-shown, since Qt
        hides it as a side effect of applying the new flags.
        """
        win = self.window.window
        win.setWindowFlag(Qt.WindowType.WindowStaysOnTopHint, enabled)
        win.show()
        self._always_on_top = enabled

    def _check_for_updates(self) -> NoReturn:
        """Checks for a newer release and shows the update banner if one exists."""
        latest_version = check_for_updates()
        if latest_version:
            self.window.window.show_update_banner(latest_version)
        else:
            self.window.window.hide_update_banner()

    def _settings(self) -> NoReturn:
        """Opens the settings."""
        old_settings_dict = self.settings_dict
        self.settings.open_window(self.window.window, self._always_on_top)
        self.settings_dict = self.settings.config_to_dict()

        if self.settings_dict != old_settings_dict:
            _popup_ok(
                "Settings", "Please restart the application to apply the changes.",
                self.window.window, self._always_on_top
            )

    # ── Mod note ───────────────────────────────────────────────────────────────

    @property
    def _mod_note(self) -> str:
        """Gets the mod note."""
        time = self.files.time

        # Guard against zero framerate to avoid DivisionUndefined
        fps = time.framerate
        if not fps or fps == 0:
            start_time = 0
            end_time = 0
        else:
            start_time = round(float(time.start_frame) / float(fps), time.precision)
            end_time = round(float(time.end_frame) / float(fps), time.precision)

        hours, minutes, seconds, milliseconds = format_components(time.with_loads)

        return self.settings_dict["mod_note_format"].format(
            time_with_loads=time.iso_format(False),
            time_without_loads=time.iso_format(True),
            hours=hours,
            minutes=minutes,
            seconds=seconds,
            milliseconds=milliseconds,
            start_frame=time.start_frame,
            end_frame=time.end_frame,
            start_time=start_time,
            end_time=end_time,
            total_frames=time.length_with_loads,
            fps=fps,
            plug="[Conner's Retime Tool](https://github.com/connerglover/conners-retime-tool)",
        )

    # ── Copy actions ──────────────────────────────────────────────────────────

    def _frame_time(self, frame: int) -> str:
        """Formats an absolute frame position as an ISO-style timestamp."""
        time = self.files.time
        return format_frame_time(frame, time.framerate, time.precision)

    def _load_duration(self, load) -> str:
        """Formats a load's duration as an ISO-style timestamp."""
        time = self.files.time
        return format_frame_time(load.length, time.framerate, time.precision)

    def _youtube_timestamp(self, frame: int) -> str:
        """Formats an absolute frame position as a YouTube chapter timestamp
        (M:SS or H:MM:SS) — YouTube chapters don't support milliseconds."""
        fps = self.files.time.framerate
        total_seconds = int(frame / fps) if fps else 0
        hours, remainder = divmod(total_seconds, 3600)
        minutes, seconds = divmod(remainder, 60)
        if hours:
            return f"{hours}:{minutes:02}:{seconds:02}"
        return f"{minutes}:{seconds:02}"

    @property
    def _discord_message(self) -> str:
        """Builds a Discord-friendly code block summarizing the run and its loads."""
        time = self.files.time
        lines = [
            f"Time: {time.iso_format(True)}",
            f"Time (with loads): {time.iso_format(False)}",
        ]
        if time.loads:
            lines.append("")
            lines.append(f"Loads ({len(time.loads)}):")
            for index, load in enumerate(time.loads, start=1):
                lines.append(
                    f"{index}. {self._frame_time(load.start_frame)} - "
                    f"{self._frame_time(load.end_frame)} ({self._load_duration(load)})"
                )
        return "```\n" + "\n".join(lines) + "\n```"

    @property
    def _youtube_chapters(self) -> str:
        """Builds a YouTube chapters list alternating Gameplay/Loading segments.

        Load frame positions are assumed to be absolute positions in the source
        video (matching the YouTube-debug-string paste feature), so they can be
        used directly as chapter timestamps. YouTube requires the first chapter
        to start at 0:00, so gameplay always opens the list.
        """
        loads = sorted(self.files.time.loads, key=lambda load: load.start_frame)

        lines = ["0:00 Gameplay"]
        for load in loads:
            lines.append(f"{self._youtube_timestamp(load.start_frame)} Loading")
            lines.append(f"{self._youtube_timestamp(load.end_frame)} Gameplay")
        return "\n".join(lines)

    # ── Display updates ────────────────────────────────────────────────────────

    def _update_displays(self) -> NoReturn:
        """Update time displays."""
        time = self.files.time
        wl = self.window.window.findChild(ClickableLabel, "without_loads_display")
        ld = self.window.window.findChild(ClickableLabel, "loads_display")
        if wl:
            try:
                wl.setText(time.iso_format(True))
            except (DivisionByZero, DivisionUndefined, InvalidOperation):
                wl.setText("00.000")
        if ld:
            try:
                ld.setText(time.iso_format(False))
            except (DivisionByZero, DivisionUndefined, InvalidOperation):
                ld.setText("00.000")
        self._refresh_load_sidebar()

    def _refresh_load_sidebar(self) -> NoReturn:
        """Refreshes the embedded loads sidebar to match the active session's loads."""
        time = self.files.time
        self.window.window.refresh_loads(
            time.loads, time.framerate, time.precision, self.language.content
        )

    def _show_error(self, message):
        """Shows a popup message of the error."""
        _popup_error("Error", message, self.window.window, self._always_on_top)

    def _get_all_values(self) -> dict:
        """Reads all input values from the main window."""
        return {
            key: self._get_input(key)
            for key in ("framerate", "start", "end", "start_loads", "end_loads")
        }

    # ── Main event loop ────────────────────────────────────────────────────────

    def run(self) -> NoReturn:
        """Runs the application."""
        win = self.window.window

        def _on_menu_action(action):
            key = action.data()
            self._dispatch(key, self._get_all_values())

        # Connect all QActions from all menus
        for action in win.findChildren(QAction):
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
        win.btn_clear_loads.clicked.connect(lambda: self._dispatch("Clear Loads", {}))

        # Embedded loads sidebar — live inline editing, no modal dialog
        win.load_edited.connect(self._on_load_edited)
        win.load_delete_requested.connect(self._on_load_deleted)

        # Clickable display labels
        wl = win.findChild(ClickableLabel, "without_loads_display")
        ld = win.findChild(ClickableLabel, "loads_display")
        if wl:
            wl.clicked.connect(lambda: _clipboard_set(self.files.time.iso_format(True)))
        if ld:
            ld.clicked.connect(lambda: _clipboard_set(self.files.time.iso_format(False)))

        # Show the window and start the Qt event loop
        win.show()
        self._qt_app.exec()

        # On exit, offer to save unsaved changes
        if self.files.dirty and _popup_yes_no(
            "Exit", "Would you like to save?", self.window.window, self._always_on_top
        ):
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
                self.files.time.clear_loads()
                self.files.dirty = True
                self._update_displays()
            case "Always on Top":
                self._set_always_on_top(self.window.window.action_always_on_top.isChecked())
            case "About":
                _popup_ok(
                    "About",
                    f"Conner's Retime Tool v{__version__}\n\n"
                    "Created by Conner Glover\n\n"
                    "Credits:\nMenzo: French and Polish Translations\n"
                    "AmazinCris: Spanish Translations\n\n"
                    "© 2026 Conner Glover",
                    self.window.window, self._always_on_top
                )
            case "Add Loads":
                self._add_loads(values)
            case "Copy Mod Note":
                try:
                    _clipboard_set(self._mod_note)
                except Exception as e:
                    self._show_error(e)
            case "Copy Discord Message":
                try:
                    _clipboard_set(self._discord_message)
                except Exception as e:
                    self._show_error(e)
            case "Copy YouTube Chapters":
                try:
                    _clipboard_set(self._youtube_chapters)
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
