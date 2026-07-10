# Conner's Retime Tool (CRT)

[![License: MIT](https://img.shields.io/github/license/connerglover/crt)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/connerglover/crt)](https://github.com/connerglover/crt/releases/latest)

CRT is a tool that aids speedrunners and moderators in finding the accurate time of a speedrun with or without loads.

## Features

- Time a run by frame or by pasting a timestamp/YouTube debug string directly into a field
- Track and edit individual loads, with automatic totals for time with and without loads
- Generate a customizable mod note (see [Mod Note Format.MD](Mod%20Note%20Format.MD) for the available placeholders)
- Save/load sessions to a file and revisit recent sessions from Session History
- Available in English, Français, Polski, and Español
- Always on Top mode keeps CRT above other windows
- Automatic update checks against the latest GitHub release

## Installation

1. Navigate to the [releases](https://github.com/connerglover/crt/releases/) page, here is every binary of CRT.
2. Locate your desired binary, the version is indicated by the title of each release, as every binary is named the same.
3. Once the binary has been located, click on its name to download it. Once the download has finished, open the file.

## Running from Source

CRT targets Python 3.10+.

```bash
pip install -r requirements.txt
python src/main.py
```

## Building the Executable

Windows, macOS, and Linux binaries are built with [PyInstaller](https://pyinstaller.org/) and are produced automatically by the [build workflow](.github/workflows/build.yml) on every push to `main`, and attached to a GitHub Release whenever a version tag (e.g. `1.2.0` or `1.2.0-rc1`) is pushed. To build one yourself:

### Windows

```bash
pip install -r requirements.txt pyinstaller
cd src
pyinstaller --onefile --windowed --icon=icon.ico --add-data "icon.ico;." --name crt main.py
```

The resulting executable is written to `src/dist/crt.exe`.

### macOS

```bash
pip install -r requirements.txt pyinstaller pillow
cd src
python - <<'PY'
import os
from PIL import Image

im = Image.open("icon.ico").convert("RGBA")
os.makedirs("crt.iconset", exist_ok=True)
for size in (16, 32, 128, 256, 512):
    im.resize((size, size), Image.LANCZOS).save(f"crt.iconset/icon_{size}x{size}.png")
    im.resize((size * 2, size * 2), Image.LANCZOS).save(f"crt.iconset/icon_{size}x{size}@2x.png")
PY
iconutil -c icns crt.iconset -o icon.icns
pyinstaller --onefile --windowed --icon=icon.icns --add-data "icon.ico:." --name crt main.py
hdiutil create -volname CRT -srcfolder dist/crt.app -ov -format UDZO ../crt-macos.dmg
```

The resulting app bundle is written to `src/dist/crt.app`, packaged as a disk image at `crt-macos.dmg`.

### Linux

```bash
pip install -r requirements.txt pyinstaller
cd src
pyinstaller --onefile --name crt main.py
```

The resulting executable is written to `src/dist/crt`. The [build workflow](.github/workflows/build.yml) additionally packages this into an AppImage for distribution.

## Contributing

Bug reports, feature requests, and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Help Development

- [Translate](https://forms.gle/7R2og1FAcXDrfr7c9)
- [Feature Request](https://forms.gle/gxr8dVEU5buHSYmN6) 
- [Report Bugs](https://forms.gle/YniGotPDvy4Cb2v57)

## Credits

- Menzo - French & Polish Translation
- Cris - Spanish Translation

## License

CRT is licensed under the [MIT License](LICENSE).