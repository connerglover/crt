"""Looks up the true framerate a YouTube video was encoded at.

Pasted YouTube debug info ("Copy debug info" from the stats-for-nerds panel)
gives a "cmt" field — the current playback time in seconds — which the app
converts to a frame number using whatever framerate the user has set (see
frame_input.py). That conversion is only correct if the user's framerate
setting actually matches the video.

The debug info also carries "docid" (the video ID) and "fmt" (the itag of
the video format currently playing). Itags alone aren't enough to recover an
exact framerate, so instead of guessing from a static itag table, this
module asks yt-dlp for the video's real format list (mirroring YouTube's own
player response) and reads the "fps" of the matching itag.

Known limitation: YouTube pads any sub-60fps source (23.976, 24, 25, 29.97,
etc.) up to a flat 30fps container for every standard-tier rendition, and
that's the value exposed in the format list — there's no field anywhere in
the response that recovers the true pre-padding rate. So this can reliably
distinguish 60fps-tier video from everything else, but not 24 vs. 25 vs. 30.
Recovering the real source rate would require downloading a stream sample
and detecting the duplicate-frame/pulldown pattern directly, which this
module intentionally doesn't attempt.
"""

# Standard library
import json
from decimal import Decimal as d
from typing import Optional

# Third-party
from yt_dlp import YoutubeDL

_YDL_OPTS = {
    "quiet": True,
    "no_warnings": True,
    "skip_download": True,
    "simulate": True,
    "socket_timeout": 8,
}

# Keyed by (video_id, format_id) — avoids re-querying yt-dlp when debug info
# for the same video is pasted more than once in a session (e.g. once for
# the start frame, once for the end frame).
_fps_cache: dict[tuple[str, str], Optional[d]] = {}


def extract_debug_info_ids(debug_info: str) -> Optional[tuple[str, str]]:
    """Extracts (video_id, format_id) from a YouTube debug info blob.

    Returns None if the text isn't parseable JSON or is missing either field.
    """
    start_pos = debug_info.find('{')
    if start_pos == -1:
        return None
    try:
        parsed = json.loads(debug_info[start_pos:])
    except json.decoder.JSONDecodeError:
        return None

    video_id = parsed.get("docid")
    format_id = parsed.get("fmt")
    if not video_id or not format_id:
        return None
    return str(video_id), str(format_id)


def get_format_framerate(video_id: str, format_id: str) -> Optional[d]:
    """Returns the real encoded framerate for a specific YouTube video/itag.

    Returns None on any failure (network error, video unavailable, itag not
    found in the format list) — callers should treat that as "couldn't be
    verified" and fall back to the user's existing framerate rather than
    blocking on it.
    """
    key = (video_id, format_id)
    if key in _fps_cache:
        return _fps_cache[key]

    result = _lookup_format_framerate(video_id, format_id)
    _fps_cache[key] = result
    return result


def _lookup_format_framerate(video_id: str, format_id: str) -> Optional[d]:
    try:
        with YoutubeDL(_YDL_OPTS) as ydl:
            info = ydl.extract_info(f"https://www.youtube.com/watch?v={video_id}", download=False)
    except Exception:
        return None

    for fmt in info.get("formats", []):
        if str(fmt.get("format_id")) == format_id:
            fps = fmt.get("fps")
            if fps:
                return d(str(fps))
    return None
