# Standard library
import json
from typing import NoReturn, Optional

# Local application
from crt.load import Load
from crt.time import Time


class FileManager:
    """Owns the current Time session plus its on-disk path and history.

    Tracks a `dirty` flag so callers know when it's necessary to prompt the
    user to save before a destructive action (new/open/switch).
    """

    def __init__(self) -> NoReturn:
        self.time = Time()
        self.file_path: Optional[str] = None
        self.past_file_paths: list = []
        self.dirty = False

    def history(self) -> list:
        """Past file paths, excluding whichever file is currently active."""
        return [p for p in self.past_file_paths if p != self.file_path]

    def _remember_past_path(self, path: Optional[str]) -> NoReturn:
        """Adds a file path to history, keeping it free of duplicates and the active file."""
        if path and path != self.file_path and path not in self.past_file_paths:
            self.past_file_paths.append(path)

    def to_dict(self) -> dict:
        """Converts the current time to a JSON-serializable dictionary."""
        return {
            "start_frame": self.time.start_frame,
            "end_frame": self.time.end_frame,
            "framerate": str(self.time.framerate),
            "loads": [(load.start_frame, load.end_frame) for load in self.time.loads]
        }

    def load_file(self, path: str) -> NoReturn:
        """Loads a time file from disk into the current session."""
        with open(path, "r") as file:
            try:
                file_data = json.load(file)
            except json.decoder.JSONDecodeError:
                raise ValueError("The file provided is corrupted.")

        old_file_path = self.file_path

        self.time.mutate(
            start_frame=file_data["start_frame"],
            end_frame=file_data["end_frame"],
            framerate=file_data["framerate"]
        )
        self.time.loads = [Load(load[0], load[1]) for load in file_data["loads"]]

        self.file_path = path
        self.dirty = False
        if path in self.past_file_paths:
            self.past_file_paths.remove(path)
        self._remember_past_path(old_file_path)

    def new_time(self) -> NoReturn:
        """Starts a blank time, remembering the previous file in history."""
        old_file_path = self.file_path
        self.file_path = None
        self.time = Time()
        self.dirty = False
        self._remember_past_path(old_file_path)

    def save(self) -> NoReturn:
        """Saves to the current file path. Raises if there isn't one yet."""
        if not self.file_path:
            raise ValueError("No file path set — use save_as() first.")
        with open(self.file_path, "w") as file:
            json.dump(self.to_dict(), file)
        self.dirty = False

    def save_as(self, path: str) -> NoReturn:
        """Saves to a new file path, remembering the previous one in history."""
        old_file_path = self.file_path
        self.file_path = path
        with open(self.file_path, "w") as file:
            json.dump(self.to_dict(), file)
        self.dirty = False
        self._remember_past_path(old_file_path)
