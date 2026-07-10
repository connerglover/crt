# Standard library
from typing import Optional
from webbrowser import open as open_url

# Third-party
from requests import get as get_url

# Local application
from crt._version import __version__

RELEASES_URL = "https://github.com/connerglover/Conners-Retime-Tool/releases/latest"


def check_for_updates() -> Optional[str]:
    """Checks GitHub for a newer release.

    Silently ignores network errors — this runs on every startup and a flaky
    connection shouldn't interrupt using the app.

    Returns:
        Optional[str]: The latest version tag if newer than the running version,
            otherwise None.
    """
    try:
        response = get_url(
            "https://api.github.com/repos/connerglover/Conners-Retime-Tool/releases/latest",
            timeout=5
        )
        if response.status_code == 200:
            latest_release = response.json()
            latest_version = latest_release["tag_name"]
            if str(latest_version) != str(__version__):
                return str(latest_version)
    except Exception:
        pass
    return None


def open_releases_page() -> None:
    """Opens the latest release page in the default browser."""
    open_url(RELEASES_URL)
