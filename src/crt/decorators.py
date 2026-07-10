# Standard library
from decimal import Decimal as d
from functools import wraps
from typing import Callable, Tuple

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

def validate_load(func: Callable) -> Callable:
    """Validates the load.

    Args:
        func (Callable): The function to wrap. 

    Raises:
        ValueError: The duration of the code is 0.000
        ValueError: The load time ends before it starts.

    Returns:
        Callable: The function containing the validated loads.
    """    
    @wraps(func)
    def wrapper(self, *args, **kwargs):
        start_frame = kwargs.get('start_frame', args[0] if args else None)
        end_frame = kwargs.get('end_frame', args[1] if len(args) > 1 else None)
        
        if start_frame is not None and end_frame is not None:
            if start_frame == end_frame:
                raise ValueError("The duration of the load is 0.000")
            if start_frame > end_frame:
                raise ValueError("The load time ends before it starts.")
        return func(self, *args, **kwargs)
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

def format_time(func: Callable) -> Callable:
    """Pre-formats time into hours, minutes, seconds, and milliseconds.

    Args:
        func (Callable): Function to wrap.

    Returns:
        Callable: Function containing the formatted time.
    """
    @wraps(func)
    def wrapper(self, loads: bool = False) -> str:
        time_value = self.without_loads if loads else self.with_loads
        hours, minutes, seconds, ms = format_components(time_value)
        return func(self, hours, minutes, seconds, ms)
    return wrapper
