# Standard library
import json
import re
from decimal import Decimal as d
from typing import Optional


def is_debug_info(text: str) -> bool:
    """Returns True if the text looks like YouTube debug info JSON."""
    return '{' in text and '"cmt"' in text


def parse_debug_info(text: str) -> Optional[dict]:
    """Parses the JSON object out of a YouTube debug info blob.

    Returns None if the text holds no object or isn't valid JSON.
    """
    start_pos = text.find('{')
    if start_pos == -1:
        return None
    try:
        return json.loads(text[start_pos:])
    except json.decoder.JSONDecodeError:
        return None


def debug_info_to_frame(framerate: d, debug_info: str) -> int:
    """Converts YouTube debug info JSON to a frame number."""
    parsed = parse_debug_info(debug_info)
    if parsed is None or "cmt" not in parsed:
        raise ValueError("The debug info provided is invalid.\nPlease re-enter debug info.")
    return int(round(d(str(parsed["cmt"])) * d(str(framerate)), 0))


def _clean_number(text: str) -> str:
    """Strips everything but digits and the first decimal point.

    Returns "" if no digit survives, so callers can treat that as "no value".
    """
    cleaned = re.sub(r'[^0-9.]', '', text)
    if not re.search(r'[0-9]', cleaned):
        return ""
    head, dot, tail = cleaned.partition(".")
    return head + dot + tail.replace(".", "")


def clean_framerate(framerate: str) -> d:
    """Cleans a framerate string into a valid Decimal, Decimal("0") if there's no number in it."""
    cleaned = _clean_number(framerate)
    return d(cleaned) if cleaned else d('0')


def parse_frame_input(text: str, framerate: d) -> int:
    """Parse a frame input field according to the full validation spec:

    1. If it looks like YouTube debug info, extract the frame from JSON.
    2. Otherwise strip all non-numeric, non-decimal characters.
    3. If empty after stripping, return 0.
    4. If a decimal point is present, treat the value as a timestamp in
       seconds and convert to a frame number (value * framerate, rounded).
    5. Otherwise return the integer value.
    """
    text = text.strip()

    if is_debug_info(text):
        return debug_info_to_frame(framerate, text)

    cleaned = _clean_number(text)
    if not cleaned:
        return 0

    # A decimal point means the user typed a timestamp in seconds, not a frame.
    if '.' in cleaned:
        fps = d(str(framerate))
        return int(round(d(cleaned) * fps, 0)) if fps else 0

    return int(cleaned)
