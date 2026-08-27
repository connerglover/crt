# CRT for macOS

Conner's Retime Tool (CRT) is a retiming utility for speedrunners: it turns
frame numbers into exact run times, subtracts loads (or sums segments), and
produces the mod notes, Discord messages and YouTube chapters moderators paste
into submissions.

This directory is the native macOS rewrite — a Swift 5.9 / SwiftUI app built as
a Swift package. It is version **2.0.0**, feature-matched with the Windows
rewrite in `../src-windows/` and behavior-matched with the original Python app
in `../src/crt/`.

The package has three targets:

| Target         | Kind       | Contents                                                        |
| -------------- | ---------- | --------------------------------------------------------------- |
| `CRTCore`      | library    | Foundation-only logic: timing, parsing, formatting, run files, settings/INI, tool locator, ffmpeg filtergraph builder, speedrun.com and YouTube clients |
| `CRT`          | executable | The SwiftUI app (views, view models, AppKit bridges)             |
| `CRTCoreTests` | test       | XCTest coverage for everything in `CRTCore`                      |

## Requirements

- **macOS 14 (Sonoma) or newer** — the app uses SwiftUI's `@Observable`,
  `NavigationSplitView` and `onKeyPress`.
- **Xcode 15+** (or a standalone Swift 5.9+ toolchain) to build.
- **ffmpeg / ffprobe** and **yt-dlp** — only needed for the Video Retimer
  (probing, YouTube import, timer-overlay export). You do not have to install
  them yourself: when a feature needs a missing tool, CRT offers to download it
  into its own `tools/` folder. If you already have them (for example via
  Homebrew), CRT finds them on `PATH`, in `/opt/homebrew/bin` or
  `/usr/local/bin`, or at an explicit path you set in Settings → Video Retimer.

## Build and run

Everything works headlessly from the command line, from this directory:

```sh
swift build            # debug build
swift test             # run the CRTCoreTests suite
swift build -c release # optimized build
swift run CRT          # quick run of the debug binary (no bundle)
```

A `Makefile` wraps the same commands and adds the bundle steps:

```sh
make            # swift build
make test       # swift test
make release    # swift build -c release
make app        # release build + assemble build/CRT.app
make run        # make app, then open build/CRT.app
make clean      # swift package clean + rm -rf build
```

`make app` runs `Scripts/make-app.sh`, which assembles a real application
bundle at `build/CRT.app`: the release binary in `Contents/MacOS/CRT`, an
`Info.plist` (bundle id `com.connerglover.crt`, version 2.0.0, minimum system
version 14.0), an `AppIcon.icns` generated from `../src/icon.ico` with `sips`
and `iconutil`, and an ad-hoc code signature. Icon generation and signing are
best effort — if `sips`, `iconutil` or `codesign` is unavailable the script
warns and still produces a working bundle. Use the bundle rather than
`swift run` for day-to-day use: only a bundled app gets the proper Dock
identity, application icon and menu-bar behavior.

Xcode users can simply **open `Package.swift`** in Xcode and build/run the
`CRT` scheme; there is no `.xcodeproj` to keep in sync.

Build output lives in `.build/` (SwiftPM) and `build/` (the app bundle).

## Features

**Frame Retimer**

- Two large click-to-copy time cards, monospaced digits — "Without Loads" /
  "With Loads", or "Segment Total" / "Full Run" in segment mode.
- **Load mode**: run start + run end, minus a list of loads.
  **Segment mode**: a list of segments whose lengths are summed, with the full
  run span shown alongside. The mode is per-session, saved in the file, and
  toggled with ⌘T.
- Frame fields accept plain frame numbers, seconds (anything containing a `.`),
  messy pasted text, and full **YouTube "Stats for nerds" debug info** — which
  is converted through `cmt × framerate`. If the pasted debug info says the
  video is a different framerate than the session, CRT looks the real fps up
  and offers to fix the framerate first.
- Paste buttons on every frame row, framerate quick-picks
  (24 / 25 / 29.97 / 30 / 50 / 59.94 / 60), and all time math in `Decimal`, so
  29.97 behaves exactly.
- Inline-editable Loads/Segments sidebar: per-row duration chip, edit, delete,
  Clear, and a guard against accidentally adding a "concerningly long" load.
- Copy actions: **Mod Note** (your own template with `{time_without_loads}`,
  `{fps}`, `{total_frames}`, `{plug}`, … placeholders), **Discord message**,
  **YouTube chapters**, plus ⇧⌘C for the without-loads time. Every copy shows a
  toast.
- Undo/redo (⌘Z / ⇧⌘Z) over the whole session, dirty-state prompts before
  New/Open/Quit, session history, persisted recent files, and a 30-second
  autosave that offers to restore the session after a crash.
- Native menu bar (File / Edit / View / Help), Always on Top (on by default),
  and an update banner when a newer GitHub release exists.

**Video Retimer**

- Import a local file, a direct video URL, or a YouTube link (downloaded with
  yt-dlp into a cache, with progress and cancel). The session framerate is set
  automatically from the probed video.
- Native `AVPlayer` playback with a frame-accurate readout, timeline scrubber
  and colored regions for everything you have marked.
- **Frame stepping** with `,` / `.` (and `<` / `>`), 5-frame arrow-key steps,
  1-second Shift+arrow jumps, `Space` to play/pause.
- **Marking** with `[` and `]` (run bounds or segments, depending on mode) and
  `L` / `Shift+L` for load start/end. The marks list is the *same session* as
  the Frame Retimer — switching pages never loses state.
- **Export Retimed Video**: ffmpeg re-encode trimmed to the run (with 2s lead
  and tail) and a LiveSplit-style timer overlay burned in — the clock runs
  during gameplay, freezes during loads or between segments, and holds the
  final time. Corner and pill/plain style are configurable; progress is shown
  and the export is cancellable.

**Dashboard** (the page the app opens on)

- Quick actions (New Retime, Open File…, Import Video) plus a live tile for the
  current unsaved session.
- **Run library**: every run you open or save, with title, both times, mode and
  modified date; Open, Reveal in Finder, Copy Mod Note, and Remove from library.
- **speedrun.com integration**: sign in with your API key to get a
  **Runs to Verify** queue across every game you moderate — watch the video,
  jump straight into retiming it (YouTube links go to the Video Retimer
  prefilled), then verify or reject with a reason, prefilled with your
  generated mod note. Also shows your recent runs and auto-refreshes while the
  dashboard is visible. Everything still works signed out.

**Settings and appearance**

- Update checks, theme (Automatic / Dark / Light), accent color (native color
  picker, used as the app tint), language, mod-note template, timer corner and
  style, default mode, and explicit ffmpeg / yt-dlp paths — with Apply, Cancel
  and Restore Defaults.
- **Four languages**: English, Français, Polski, Español — the same
  translations as the Python app, with English fallback for newer strings.
  Changing language asks you to restart.
- **Customizable hotkeys** for every action (menu commands, paste buttons, add
  load, toggle mode and all the video transport/marking keys). The editor
  captures new combinations live and flags duplicates; per-row Reset and Reset
  All are available. Defaults follow the Python app with Ctrl swapped for ⌘.

## Where CRT keeps things

Everything lives in `~/Library/Application Support/CRT/`:

| Path             | What it is                                                  |
| ---------------- | ----------------------------------------------------------- |
| `settings.ini`   | Settings + hotkeys, in the same `[Settings]` / `[Hotkeys]` INI format the Python app uses |
| `tools/`         | ffmpeg, ffprobe and yt-dlp when CRT downloads them for you   |
| `video-cache/`   | Downloaded videos, reused when you import the same one again |
| `library.json`   | The dashboard run-library index                              |
| `recent.json`    | Recent file paths (most recent first, capped at 20)          |
| `autosave.json`  | Crash-restore snapshot; removed on a clean exit              |

Your speedrun.com API key is **not** written to any of these files — it is
stored in the macOS Keychain under the service **"CRT Speedrun.com"** and is
removed when you sign out.

## Run files stay interchangeable with the Python app

Runs are saved as ordinary `*.json` files with the same keys the Python app
reads and writes (`start_frame`, `end_frame`, `framerate` as a string, and
`loads` as `[[start, end], …]`), so a file saved here opens in the Python app
and vice versa. The native apps add `mode`, `segments` and `meta` keys, which
Python simply ignores; segment-mode runs additionally write run bounds plus the
gaps between segments as `loads`, so the Python app still computes the correct
time.
