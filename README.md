# Conner's Retime Tool (CRT)

[![License: MIT](https://img.shields.io/github/license/connerglover/crt)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/connerglover/crt)](https://github.com/connerglover/crt/releases/latest)

CRT is a tool that aids speedrunners and moderators in finding the accurate time of a speedrun with or without loads. The GUI is built with [PySide6](https://doc.qt.io/qtforpython-6/) and supports light, dark, and automatic themes.

## Features

- Time a run by frame or by pasting a timestamp/YouTube debug string directly into a field
- Track and edit individual loads, with automatic totals for time with and without loads
- Generate a customizable mod note (see [Mod Note Format.MD](Mod%20Note%20Format.MD) for the available placeholders)
- Save/load sessions to a file and revisit recent sessions from Session History
- Available in English, Français, Polski, and Español
- Automatic update checks against the latest GitHub release

## Installation

1. Navigate to the [releases](https://github.com/connerglover/crt/releases/) page, here is every binary of CRT.
2. Locate your desired binary, the version is indicated by the title of each release, as every binary is named the same.
3. Once the binary has been located, click on its name to download it. Once the download has finished, open the file.

## Running from Source

CRT targets Python 3.10+ (the codebase uses `match` statements).

```bash
pip install -r requirements.txt
python src/main.py
```

## Building the Executable

Windows binaries are built with [PyInstaller](https://pyinstaller.org/) and are produced automatically by the [build workflow](.github/workflows/build.yml) on every push to `main`, and attached to a GitHub Release whenever a `v*` tag is pushed. To build one yourself:

```bash
pip install -r requirements.txt pyinstaller
cd src
pyinstaller --onefile --windowed --icon=icon.ico --name ConnersRetimeTool main.py
```

The resulting executable is written to `src/dist/ConnersRetimeTool.exe`.

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

## Star History

<a href="https://www.star-history.com/?repos=connerglover%2Fcrt&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=connerglover/crt&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=connerglover/crt&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=connerglover/crt&type=date&legend=top-left" />
 </picture>
</a>
