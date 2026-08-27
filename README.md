<div align="center">
  <img src="src-macos/Resources/icon.ico" width="84" alt="CRT icon" />

  # Conner's Retime Tool — macOS

  <b>A desktop app for speedrunners and moderators to time runs accurately — with or without loads.</b>

  <p>
    <a href="https://github.com/connerglover/crt/releases/latest"><img src="https://img.shields.io/github/v/release/connerglover/crt?style=flat-square&color=6f42c1" alt="Latest Release" /></a>
    <a href="https://github.com/connerglover/crt/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/connerglover/crt/build.yml?style=flat-square" alt="Build Status" /></a>
    <a href="LICENSE"><img src="https://img.shields.io/github/license/connerglover/crt?style=flat-square" alt="License: MIT" /></a>
    <img src="https://img.shields.io/badge/platform-macOS%2014%2B-6f42c1?style=flat-square" alt="Platform: macOS 14+" />
    <img src="https://img.shields.io/badge/Swift-5.9-f05138?style=flat-square" alt="Swift 5.9" />
  </p>
</div>

> **This branch is the native macOS rewrite only.** It contains no Python.
> The original cross-platform Python app lives on `main`, and the native Windows
> rewrite lives on `feat/windows-winui3`.

## ✨ Features

**Retiming**

- Time a run by frame, or by pasting a timestamp / YouTube debug string
- **Load mode** — start of run, end of run, and individual loads subtracted, with
  automatic totals both with and without loads
- **Segment mode** — mark any number of segments and total them, with the
  full-run span tracked alongside
- Automatic YouTube framerate detection, so a pasted debug string can't be
  silently converted at the wrong FPS
- Inline-editable load/segment sidebar with per-entry durations

**Video retimer**

- Import a YouTube URL, a direct video URL, or a local video file
- Frame-by-frame navigation with `<` / `>`, play/pause, and jump controls
- Mark segment or load boundaries with a button or a hotkey, straight from playback
- Export the retimed video with a LiveSplit-style timer burned into the corner —
  the clock runs during gameplay, freezes through loads, and holds the final time

**Dashboard**

- A run library of everything you've timed, with times, mode, and quick actions
- Speedrun.com integration — sign in with your API key
- **Runs to Verify** — every pending run across the games you moderate, with
  watch, retime, verify, and reject without leaving the app

**Everything else**

- Customizable mod note format ([available placeholders](Mod%20Note%20Format.MD))
- Copy as a mod note, a Discord message, or a YouTube chapter list
- Fully customizable hotkeys for every action
- Session history and a persistent recent-files list
- Undo/redo, autosave, and crash recovery
- Always-on-top mode and automatic update checks
- English, Français, Polski, and Español
- Native light/dark appearance with a custom accent color

## 📥 Installation

Grab the latest macOS build from
[Releases](https://github.com/connerglover/crt/releases/latest), open the `.dmg`,
and drag CRT to Applications.

The bundle is ad-hoc signed rather than notarized, so on first launch macOS may
warn you. Right-click the app and choose **Open** to get the override prompt.

## 🔨 Building from source

Requires macOS 14+ and Swift 5.9 (Xcode 15+).

```bash
cd src-macos
swift build
swift test
make app       # assembles build/CRT.app
make run       # builds the bundle and launches it
```

Or open `src-macos/Package.swift` directly in Xcode.

See [`src-macos/README.md`](src-macos/README.md) for the package layout and where
settings, tools, and caches live on disk.

## 🎬 Video features

The video retimer and YouTube import rely on **ffmpeg** and **yt-dlp**. The app
looks for them on your `PATH` first and offers to download them on demand if
they're missing — nothing to install up front.

## 🔄 Compatibility

Run `.json` files and `settings.ini` are interchangeable with the Python app on
`main` and the Windows app on `feat/windows-winui3`, in both directions. A run
saved in segment mode still opens correctly in the Python app, where the segment
total appears as the without-loads time.

## 🤝 Contributing

Bug reports, feature requests, and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Please also read our [Code of Conduct](CODE_OF_CONDUCT.md); project decisions are explained in [GOVERNANCE.md](GOVERNANCE.md). Found a security issue? See [SECURITY.md](SECURITY.md) instead of opening a public issue.

Both native rewrites are built to a shared specification —
[`docs/native-rewrite-spec.md`](docs/native-rewrite-spec.md) — which is the
source of truth for behavior on both platforms.

## 🙌 Credits

- Menzo — French & Polish translation
- Cris — Spanish translation

## 📄 License

CRT is licensed under the [MIT License](LICENSE).
