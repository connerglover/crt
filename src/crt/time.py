# Standard library
from decimal import Decimal as d
from typing import Optional

# Local application
from crt.load import Load
from crt.decorators import PRECISION, format_iso

def _check(start_frame: Optional[int], end_frame: Optional[int]) -> None:
    """Raises if a load is zero-length or ends before it starts."""
    if start_frame is None or end_frame is None:
        return
    if start_frame == end_frame:
        raise ValueError("The duration of the load is 0.000")
    if start_frame > end_frame:
        raise ValueError("The load time ends before it starts.")


class Time:
    """
    A class that represents a time in a video.
    """
    
    def __init__(self, start_frame: Optional[int] = 0, end_frame: Optional[int] = 0, framerate: Optional[d] = 60, loads: Optional[list[Load]] = None) -> None:
        """Initializes the Time class.
        
        Args:
            start_frame (int): The start frame of the time.
            end_frame (int): The end frame of the time.
            framerate (d): The framerate of the video.
            loads (list[Load] | None): The loads of the time.
        """
        self.loads = loads if loads is not None else []
        self.start_frame = start_frame
        self.end_frame = end_frame
        self.framerate = framerate

    @property
    def length_with_loads(self) -> int:
        """Calculates the total length in frames including loads.

        Returns:
            int: The total length in frames including loads.
        """        
        return int(self.end_frame - self.start_frame)
    
    @property
    def length_without_loads(self) -> int:
        """Calculates the total length in frames excluding loads.

        Returns:
            int: The total length in frames excluding loads.
        """        
        return int(self.length_with_loads - sum(load.length for load in self.loads))
    
    @property
    def average_load_length(self) -> int:
        """Calculates the average load length.

        Returns:
            int: The average load length.
        """        
        return int(sum(load.length for load in self.loads) / len(self.loads)) if self.loads else 0
    
    def _secs(self, frames: int) -> d:
        """Converts a frame count to seconds at the current framerate."""
        if not self.framerate:
            return d(0.000)
        return round(d(frames) / d(self.framerate), PRECISION)

    @property
    def with_loads(self) -> d:
        """The total time including loads, in seconds."""
        return self._secs(self.length_with_loads)

    @property
    def without_loads(self) -> d:
        """The total time excluding loads, in seconds."""
        return self._secs(self.length_without_loads)

    def delete_load(self, index: int) -> None:
        """Deletes the load.
        
        Args:
            index (int): The index of the load.
        """
        del self.loads[index]
    
    def mutate_load(self, index: int, start_frame: Optional[int] = None, end_frame: Optional[int] = None) -> None:
        """Mutates the load.
        
        Args:
            index (int): The index of the load.
            start_frame (int): The start frame of the load.
            end_frame (int): The end frame of the load.
        """
        _check(start_frame, end_frame)

        if start_frame is not None:
            self.loads[index].start_frame = start_frame
        if end_frame is not None:
            self.loads[index].end_frame = end_frame
    
    def add_load(self, start_frame: int, end_frame: int) -> None:
        """Adds a load.

        Args:
            start_frame (int): The first frame of the laod.
            end_frame (int): The final frame of the load.

        Raises:
            ValueError: You must provide an input for the loads.
        """
        _check(start_frame, end_frame)

        if start_frame == 0 and end_frame == 0:
            raise ValueError("You must provide an input for the loads")

        load = Load(start_frame, end_frame)
        self.loads.append(load)

    def iso_format(self, loads: bool = False) -> str:
        """Formats the time into ISO Format

        Args:
            loads (bool): Whether to format the time excluding loads.

        Returns:
            str: Formatted time
        """
        time_value = self.without_loads if loads else self.with_loads
        return format_iso(time_value)
