# Standard library
from typing import NoReturn
from webbrowser import open as open_url

# Third-party
from requests import get as get_url

# Local application
from crt._version import __version__
from crt.popups import popup_yes_no


def check_for_updates() -> NoReturn:
    """Checks GitHub for a newer release and offers to open the download page.

    Silently ignores network errors — this runs on every startup and a flaky
    connection shouldn't interrupt using the app.
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
                if popup_yes_no(
                    "Update Available",
                    f"A new version of CRT is available: {latest_version}.\nWould you like to update?"
                ):
                    open_url("https://github.com/connerglover/Conners-Retime-Tool/releases/latest")
    except Exception:
        pass
