# Standard library
import json
import re
from decimal import Decimal as d, InvalidOperation


def is_debug_info(text: str) -> bool:
    """Returns True if the text looks like YouTube debug info JSON."""
    return '{' in text and '"cmt"' in text


def debug_info_to_frame(framerate: d, debug_info: str) -> int:
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
    return int(round(d(str(cmt)) * d(str(framerate)), 0))


def clean_framerate(framerate: str) -> d:
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

    # Step 1 — debug info
    if is_debug_info(text):
        return debug_info_to_frame(framerate, text)

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
            fps = d(str(framerate))
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
