# Standard library
from decimal import Decimal as d
from functools import wraps
from typing import Callable, Tuple

PRECISION = 3  # Decimal places every displayed time is rounded to.

def error_handler(func: Callable) -> Callable:
    """Handles errors by showing popup rather than crashing the program.

    Args:
        func (Callable): The function to wrap.

    Returns:
        Callable: The function containing the error handling.
    """    
    @wraps(func)
    def wrapper(self, *args, **kwargs):
        try:
            return func(self, *args, **kwargs)
        except Exception as e:
            self._show_error(str(e))
            return None
    return wrapper

def format_components(time: d) -> Tuple[str, str, str, str]:
    """Formats a time value into (hours, minutes, seconds, milliseconds) strings.

    Args:
        time (d): The time to format.

    Returns:
        Tuple[str, str, str, str]: A tuple containing the elements of the formatted time.
    """
    time_str = str(max(time, d(0)))

    if '.' in time_str:
        seconds, milliseconds = map(int, time_str.split(".", 1))
    else:
        seconds, milliseconds = int(time_str), 0

    minutes, seconds = divmod(seconds, 60)
    hours, minutes = divmod(minutes, 60)

    return (
        f"{hours:02}",
        f"{minutes:02}",
        f"{seconds:02}",
        str(milliseconds).rjust(3, "0")
    )

def format_iso(time: d) -> str:
    """Formats a raw time value (in seconds) into ISO-style H:MM:SS.mmm, omitting
    leading hour/minute units that are zero — the same style used by the main
    time displays.

    Args:
        time (d): The time to format, in seconds.

    Returns:
        str: The formatted time.
    """
    hours, minutes, seconds, ms = format_components(time)
    if int(hours) > 0:
        return f"{hours}:{minutes}:{seconds}.{ms}"
    elif int(minutes) > 0:
        return f"{minutes}:{seconds}.{ms}"
    return f"{seconds}.{ms}"

def format_frame_time(frames: int, framerate: d) -> str:
    """Converts a frame count/position at the given framerate into an ISO-style timestamp.

    Args:
        frames (int): A frame count (duration) or absolute frame position.
        framerate (d): The framerate to convert with.

    Returns:
        str: The formatted time, e.g. "01:15.000". "0.000" if framerate is falsy.
    """
    if not framerate:
        return format_iso(d(0))
    return format_iso(round(d(frames) / d(framerate), PRECISION))
