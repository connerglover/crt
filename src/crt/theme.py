# Standard library
import sys


def is_dark_mode() -> bool:
    """Detects whether the OS is using a dark theme.

    Reads the Windows registry directly instead of depending on the
    third-party `darkdetect` package, which pulls in a platform-specific
    submodule (`_windows_detect`) that's easy to lose track of â€” either
    it's missing because dependencies weren't installed, or PyInstaller's
    static import analysis fails to bundle it into the frozen exe.
    Falls back to light mode on any failure or on non-Windows platforms.
    """
    if sys.platform != "win32":
        return False
    try:
        import winreg
        key = winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        )
        value, _ = winreg.QueryValueEx(key, "AppsUseLightTheme")
        return value == 0
    except OSError:
        return False


DEFAULT_ACCENT_COLOR = "#5b9bd5"


def _hex_to_rgb(hex_color: str) -> tuple[int, int, int]:
    """Converts a "#rrggbb" hex string into an (r, g, b) tuple."""
    hex_color = hex_color.lstrip("#")
    return tuple(int(hex_color[i:i + 2], 16) for i in (0, 2, 4))


def _rgb_to_hex(rgb: tuple[int, int, int]) -> str:
    """Converts an (r, g, b) tuple into a "#rrggbb" hex string."""
    return "#{:02x}{:02x}{:02x}".format(*(max(0, min(255, c)) for c in rgb))


def _lighten(hex_color: str, amount: float = 0.2) -> str:
    """Blends a hex color toward white by `amount` (0-1), for hover states."""
    r, g, b = _hex_to_rgb(hex_color)
    return _rgb_to_hex((
        round(r + (255 - r) * amount),
        round(g + (255 - g) * amount),
        round(b + (255 - b) * amount),
    ))


def _darken(hex_color: str, amount: float = 0.22) -> str:
    """Blends a hex color toward black by `amount` (0-1), for pressed states."""
    r, g, b = _hex_to_rgb(hex_color)
    return _rgb_to_hex((round(r * (1 - amount)), round(g * (1 - amount)), round(b * (1 - amount))))


DARK_COLORS = {
    "__BG__":                 "#1e1e1e",
    "__TEXT__":               "#d4d4d4",
    "__BAR__":                "#181818",
    "__LINE__":               "#2e2e2e",
    "__SURFACE__":            "#181818",
    "__SURFACE_HOVER__":      "#2e2e2e",
    "__FIELD__":              "#2e2e2e",
    "__FIELD_BORDER__":       "#454545",
    "__FIELD_BORDER_HOVER__": "#5a5a5a",
    "__TEXT_DISABLED__":      "#6e6e6e",
    "__DISABLED_BG__":        "#242424",
    "__BTN_HOVER__":          "#454545",
    "__BTN_PRESSED__":        "#262626",
    "__ON_ACCENT_LINE__":     "rgba(0, 0, 0, 0.2)",
    "__DANGER__":             "#e06c75",
    "__DANGER_HOVER__":       "rgba(224, 108, 117, 0.15)",
    "__DANGER_PRESSED__":     "rgba(224, 108, 117, 0.28)",
    "__TEXT_MUTED__":         "#9a9a9a",
    "__BORDER_SOFT__":        "#454545",
    "__CARD__":               "#242424",
    "__ON_ACCENT_STRONG__":   "#000000",
    "__ON_ACCENT_WASH__":     "rgba(0, 0, 0, 0.15)",
    "__BAR_HOVER__":          "#242424",
}

LIGHT_COLORS = {
    "__BG__":                 "#eff1f5",
    "__TEXT__":               "#4c4f69",
    "__BAR__":                "#e6e9ef",
    "__LINE__":               "#ccd0da",
    "__SURFACE__":            "#ffffff",
    "__SURFACE_HOVER__":      "#e6e9ef",
    "__FIELD__":              "#ffffff",
    "__FIELD_BORDER__":       "#bcc0cc",
    "__FIELD_BORDER_HOVER__": "#acb0be",
    "__TEXT_DISABLED__":      "#9ca0b0",
    "__DISABLED_BG__":        "#e6e9ef",
    "__BTN_HOVER__":          "#e6e9ef",
    "__BTN_PRESSED__":        "#ccd0da",
    "__ON_ACCENT_LINE__":     "rgba(255, 255, 255, 0.35)",
    "__DANGER__":             "#d20f39",
    "__DANGER_HOVER__":       "rgba(210, 15, 57, 0.10)",
    "__DANGER_PRESSED__":     "rgba(210, 15, 57, 0.20)",
    "__TEXT_MUTED__":         "#6c6f85",
    "__BORDER_SOFT__":        "#ccd0da",
    "__CARD__":               "#ffffff",
    "__ON_ACCENT_STRONG__":   "#ffffff",
    "__ON_ACCENT_WASH__":     "rgba(0, 0, 0, 0.08)",
    "__BAR_HOVER__":          "#dce0e8",
}


# A single QSS template shared by both themes; every color is a `__TOKEN__`
# resolved from the palette dicts above (plus the accent tokens, which come
# from the user-configurable accent color instead of the theme).
PALETTE = """
QWidget {
    background-color: __BG__;
    color: __TEXT__;
    font-family: "Segoe UI", Helvetica, Arial, sans-serif;
}
QMainWindow, QDialog {
    background-color: __BG__;
}
QMenuBar {
    background-color: __BAR__;
    color: __TEXT__;
    border-bottom: 1px solid __LINE__;
    padding: 2px 4px;
}
QMenuBar::item {
    padding: 4px 10px;
    border-radius: 5px;
}
QMenuBar::item:selected {
    background-color: __LINE__;
}
QMenu {
    background-color: __SURFACE__;
    color: __TEXT__;
    border: 1px solid __LINE__;
    border-radius: 8px;
    padding: 6px;
}
QMenu::item {
    padding: 6px 24px 6px 12px;
    border-radius: 5px;
}
QMenu::item:selected {
    background-color: __SURFACE_HOVER__;
}
QMenu::separator {
    height: 1px;
    background-color: __LINE__;
    margin: 6px 4px;
}
QLineEdit {
    background-color: __FIELD__;
    color: __TEXT__;
    border: 1px solid __FIELD_BORDER__;
    border-radius: 7px;
    padding: 3px 10px;
    selection-background-color: __ACCENT__;
    selection-color: __BG__;
}
QLineEdit:hover {
    border: 1px solid __FIELD_BORDER_HOVER__;
}
QLineEdit:focus {
    border: 1px solid __ACCENT__;
}
QLineEdit:disabled {
    color: __TEXT_DISABLED__;
    background-color: __DISABLED_BG__;
}
QPushButton {
    background-color: __FIELD__;
    color: __TEXT__;
    border: 1px solid __FIELD_BORDER__;
    border-radius: 7px;
    padding: 6px 14px;
    font-weight: 500;
}
QPushButton:hover {
    background-color: __BTN_HOVER__;
    border-color: __FIELD_BORDER_HOVER__;
}
QPushButton:pressed {
    background-color: __BTN_PRESSED__;
}
QPushButton:disabled {
    color: __TEXT_DISABLED__;
    background-color: __DISABLED_BG__;
    border-color: __LINE__;
}
QPushButton[cssClass="primary"] {
    background-color: __ACCENT__;
    color: __BG__;
    border: 1px solid __ACCENT__;
    font-weight: 600;
}
QPushButton[cssClass="primary"]:hover {
    background-color: __ACCENT_HOVER__;
    border-color: __ACCENT_HOVER__;
}
QPushButton[cssClass="primary"]:pressed {
    background-color: __ACCENT_PRESSED__;
}
QToolButton {
    background-color: __FIELD__;
    color: __TEXT__;
    border: 1px solid __FIELD_BORDER__;
    border-radius: 7px;
    padding: 6px 14px;
    font-weight: 500;
}
QToolButton:hover {
    background-color: __BTN_HOVER__;
    border-color: __FIELD_BORDER_HOVER__;
}
QToolButton:pressed {
    background-color: __BTN_PRESSED__;
}
QToolButton[cssClass="primary"] {
    background-color: __ACCENT__;
    color: __BG__;
    border: 1px solid __ACCENT__;
    font-weight: 600;
}
QToolButton[cssClass="primary"]:hover {
    background-color: __ACCENT_HOVER__;
    border-color: __ACCENT_HOVER__;
}
QToolButton[cssClass="primary"]:pressed {
    background-color: __ACCENT_PRESSED__;
}
QToolButton::menu-button {
    border: none;
    border-left: 1px solid __ON_ACCENT_LINE__;
    width: 24px;
}
QPushButton[cssClass="danger"] {
    background-color: transparent;
    color: __DANGER__;
    border: 1px solid __FIELD_BORDER__;
}
QPushButton[cssClass="danger"]:hover {
    background-color: __DANGER_HOVER__;
    border-color: __DANGER__;
}
QPushButton[cssClass="danger"]:pressed {
    background-color: __DANGER_PRESSED__;
}
QPushButton[cssClass="compact"] {
    padding: 2px 6px;
    font-weight: 400;
}
QPushButton[cssClass="danger-compact"] {
    background-color: transparent;
    color: __DANGER__;
    border: 1px solid __FIELD_BORDER__;
    padding: 2px 8px;
    font-weight: 400;
}
QPushButton[cssClass="danger-compact"]:hover {
    background-color: __DANGER_HOVER__;
    border-color: __DANGER__;
}
QPushButton[cssClass="danger-compact"]:pressed {
    background-color: __DANGER_PRESSED__;
}
QPushButton[cssClass="danger-compact"]:disabled {
    color: __TEXT_DISABLED__;
    background-color: transparent;
    border-color: __SURFACE_HOVER__;
}
QLabel {
    color: __TEXT__;
}
QLabel[cssClass="heading"] {
    color: __ACCENT__;
}
QLabel[cssClass="muted"] {
    color: __TEXT_MUTED__;
}
QLabel[cssClass="chip"] {
    background-color: __FIELD__;
    border: 1px solid __BORDER_SOFT__;
    border-radius: 6px;
}
QFrame[frameShape="4"],
QFrame[frameShape="5"] {
    color: __LINE__;
    max-height: 1px;
}
QComboBox {
    background-color: __FIELD__;
    color: __TEXT__;
    border: 1px solid __FIELD_BORDER__;
    border-radius: 7px;
    padding: 3px 10px;
}
QComboBox:hover {
    border: 1px solid __FIELD_BORDER_HOVER__;
}
QComboBox::drop-down {
    border: none;
    width: 22px;
}
QComboBox QAbstractItemView {
    background-color: __SURFACE__;
    color: __TEXT__;
    border: 1px solid __LINE__;
    border-radius: 8px;
    selection-background-color: __BTN_HOVER__;
    outline: none;
    padding: 4px;
}
QCheckBox {
    color: __TEXT__;
    spacing: 8px;
}
QCheckBox::indicator {
    width: 16px;
    height: 16px;
    border: 1px solid __FIELD_BORDER_HOVER__;
    border-radius: 4px;
    background-color: __FIELD__;
}
QCheckBox::indicator:hover {
    border-color: __ACCENT__;
}
QCheckBox::indicator:checked {
    background-color: __ACCENT__;
    border-color: __ACCENT__;
}
QListWidget {
    background-color: __SURFACE__;
    color: __TEXT__;
    border: 1px solid __LINE__;
    border-radius: 8px;
    padding: 4px;
}
QListWidget::item {
    padding: 7px 8px;
    border-radius: 5px;
}
QListWidget::item:hover {
    background-color: __DISABLED_BG__;
}
QListWidget::item:selected {
    background-color: __ACCENT__;
    color: __BG__;
}
QScrollBar:vertical {
    background: transparent;
    width: 12px;
    margin: 2px;
}
QScrollBar::handle:vertical {
    background: __FIELD_BORDER__;
    border-radius: 5px;
    min-height: 24px;
}
QScrollBar::handle:vertical:hover {
    background: __FIELD_BORDER_HOVER__;
}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {
    height: 0px;
}
QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical {
    background: transparent;
}
QScrollBar:horizontal {
    background: transparent;
    height: 12px;
    margin: 2px;
}
QScrollBar::handle:horizontal {
    background: __FIELD_BORDER__;
    border-radius: 5px;
    min-width: 24px;
}
QScrollBar::handle:horizontal:hover {
    background: __FIELD_BORDER_HOVER__;
}
QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {
    width: 0px;
}
QScrollBar::add-page:horizontal, QScrollBar::sub-page:horizontal {
    background: transparent;
}
QToolTip {
    background-color: __SURFACE__;
    color: __TEXT__;
    border: 1px solid __BORDER_SOFT__;
    border-radius: 5px;
    padding: 4px 8px;
}
QWidget[cssClass="card"] {
    background-color: __CARD__;
    border: 1px solid __LINE__;
    border-radius: 10px;
}
QLabel[cssClass="time-value"] {
    color: __TEXT__;
}
QLabel[cssClass="time-value"]:hover {
    color: __ACCENT__;
}
QWidget[cssClass="update-banner"] {
    background-color: __ACCENT__;
    border-bottom: 1px solid __ACCENT_PRESSED__;
}
QLabel[cssClass="update-banner-text"] {
    color: __BG__;
    background-color: transparent;
}
QLabel[cssClass="update-banner-text"]:hover {
    color: __ON_ACCENT_STRONG__;
    background-color: transparent;
}
QPushButton[cssClass="update-banner-close"] {
    background-color: transparent;
    border: none;
    color: __BG__;
    padding: 0px;
    font-weight: 600;
}
QPushButton[cssClass="update-banner-close"]:hover {
    background-color: __ON_ACCENT_WASH__;
    border-radius: 4px;
}
QPushButton[cssClass="panel-toggle"] {
    background-color: __BAR__;
    border: none;
    border-left: 1px solid __LINE__;
    border-radius: 0px;
    color: __TEXT_MUTED__;
    padding: 0px;
    font-weight: 400;
}
QPushButton[cssClass="panel-toggle"]:hover {
    background-color: __BAR_HOVER__;
    color: __TEXT__;
}
"""


def _render(colors: dict[str, str], accent: str) -> str:
    """Substitutes the palette and accent tokens in PALETTE with real colors.

    Uses plain token replacement rather than `str.format` because QSS itself
    uses `{`/`}` for rule blocks, which would collide with format fields.
    """
    qss = PALETTE
    for token, value in (
        *colors.items(),
        ("__ACCENT_HOVER__", _lighten(accent)),
        ("__ACCENT_PRESSED__", _darken(accent)),
        ("__ACCENT__", accent),
    ):
        qss = qss.replace(token, value)
    return qss


def stylesheet_for(theme: str, accent_color: str = DEFAULT_ACCENT_COLOR) -> str:
    """Resolves a theme name (as stored in settings) to a Qt stylesheet.

    Args:
        theme (str): Name of the theme ("Dark", "Light", or "Automatic").
        accent_color (str): User-selected accent color as a "#rrggbb" hex string.
    """
    match theme:
        case "Dark":
            colors = DARK_COLORS
        case "Light":
            colors = LIGHT_COLORS
        case _:
            # "Automatic" and any unrecognized value both follow the OS theme.
            colors = DARK_COLORS if is_dark_mode() else LIGHT_COLORS

    return _render(colors, accent_color or DEFAULT_ACCENT_COLOR)


if __name__ == "__main__":
    # Every token in the template must be defined by both palettes.
    for name in ("Dark", "Light"):
        assert "__" not in stylesheet_for(name), f"unresolved token in {name} theme"
    print("ok")
