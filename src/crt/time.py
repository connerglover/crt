# Standard library
from decimal import Decimal as d
from typing import Optional, NoReturn

# Local application
from crt.load import Load
from crt.decorators import validate_load, format_time, format_iso

class Time:
    """
    A class that represents a time in a video.
    """
    
    def __init__(self, start_frame: Optional[int] = 0, end_frame: Optional[int] = 0, framerate: Optional[d] = 60, precision: Optional[int] = 3, loads: Optional[list[Load]] = None) -> NoReturn:
        """Initializes the Time class.
        
        Args:
            start_frame (int): The start frame of the time.
            end_frame (int): The end frame of the time.
            framerate (d): The framerate of the video.
            precision (int): The precision of the time.
            loads (list[Load] | None): The loads of the time.
        """
        self.loads = loads if loads is not None else []
        self.start_frame = start_frame
        self.end_frame = end_frame
        self.framerate = framerate
        self.precision = precision

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
    
    @property
    def with_loads(self) -> d:
        """Calculates the total time including loads.

        Returns:
            d: The total time.
        """        
        if int(self.framerate) == 0:
            return d(0.000)
        return round(d(self.length_with_loads / d(self.framerate)), self.precision)
    
    @property
    def without_loads(self) -> d:
        """Caclulates the total time excluding loads.

        Returns:
            d: The time excluding loads.
        """        
        if self.framerate == 0:
            return d(0.000)
        return round(d(self.length_without_loads / d(self.framerate)), self.precision)
    
    def clear_loads(self) -> None:
        """Clears the loads.
        """
        self.loads = []
    
    def mutate(self, start_frame: Optional[int] = None, end_frame: Optional[int] = None, framerate: Optional[d] = None) -> None:
        """Mutates the time.
        
        Args:
            start_frame (int): The start frame of the time. Default to None.
            end_frame (int): The end frame of the time. Default to None.
            framerate (d): The framerate of the video. Defaults to None
        """
        self.start_frame = start_frame if start_frame is not None else self.start_frame
        self.end_frame = end_frame if end_frame is not None else self.end_frame
        self.framerate = framerate if framerate is not None else self.framerate
    
    def get_load(self, index: int) -> d:
        """Gets the load time.
        
        Args:
            index (int): The index of the load.
        
        Returns:
            d: The load time.
        """
        load_time = self.loads[index].length / self.framerate
        
        return load_time
    
    def delete_load(self, index: int) -> NoReturn:
        """Deletes the load.
        
        Args:
            index (int): The index of the load.
        """
        del self.loads[index]
    
    @validate_load
    def mutate_load(self, index: int, start_frame: Optional[int] = None, end_frame: Optional[int] = None) -> NoReturn:
        """Mutates the load.
        
        Args:
            index (int): The index of the load.
            start_frame (int): The start frame of the load.
            end_frame (int): The end frame of the load.
        """
        if start_frame is not None:
            self.loads[index].start_frame = start_frame
        if end_frame is not None:
            self.loads[index].end_frame = end_frame
    
    @validate_load
    def add_load(self, start_frame: int, end_frame: int) -> NoReturn:
        """Adds a load.

        Args:
            start_frame (int): The first frame of the laod.
            end_frame (int): The final frame of the load.

        Raises:
            ValueError: You must provide an input for the loads.
        """
        if start_frame == 0 and end_frame == 0:
            raise ValueError("You must provide an input for the loads")

        load = Load(start_frame, end_frame)
        self.loads.append(load)

    @format_time
    def src_format(self, hours: str, minutes: str, seconds: str, ms: str) -> str:
        """Formats time into Speedrun.com Format

        Args:
            hours (str): String of hours
            minutes (str): String of minutes
            seconds (str): String of seconds
            ms (str): String of Milliseconds

        Returns:
            str: Formatted time
        """        
        return f"{hours}h {minutes}m {seconds}s {ms}ms"
    
    def iso_format(self, loads: bool = False) -> str:
        """Formats the time into ISO Format

        Args:
            loads (bool): Whether to format the time excluding loads.

        Returns:
            str: Formatted time
        """
        time_value = self.without_loads if loads else self.with_loads
        return format_iso(time_value)
