class BaseGUI:
    """Base class for all CRT GUIs.

    Subclasses must set self.window to a QDialog or QMainWindow instance,
    then call _connect_signals() to wire widget signals to self._emit(...).
    """

    _last_event = None
    _last_values = {}

    def _emit(self, event: str, values: dict = None):
        """Records the most recent event/values, read back by read()."""
        self._last_event = event
        self._last_values = values if values is not None else {}

    def read(self) -> tuple:
        """Blocking read: shows the dialog and returns (event, values) once
        a signal handler calls self._emit(...) or the window is closed."""
        from PySide6.QtWidgets import QApplication
        self._last_event = None
        self.window.show()
        while self._last_event is None and self.window.isVisible():
            QApplication.processEvents()
        return self._last_event, self._last_values

    def close(self):
        """Closes the window."""
        if hasattr(self, "window") and self.window is not None:
            self.window.close()

    def __enter__(self):
        """Context manager enter."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit."""
        self.close()
