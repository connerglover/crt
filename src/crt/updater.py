# Standard library
import json
from typing import Optional
from urllib.request import urlopen

# Local application
from crt._version import __version__

RELEASES_URL = "https://github.com/connerglover/crt/releases/latest"


def check_for_updates() -> Optional[str]:
    """Checks GitHub for a newer release.

    Silently ignores network errors — this runs on every startup and a flaky
    connection shouldn't interrupt using the app.

    Returns:
        Optional[str]: The latest version tag if newer than the running version,
            otherwise None.
    """
    try:
        with urlopen(
            "https://api.github.com/repos/connerglover/crt/releases/latest", timeout=5
        ) as response:
            latest_version = json.load(response)["tag_name"]
        if str(latest_version) != str(__version__):
            return str(latest_version)
    except Exception:
        pass
    return None
