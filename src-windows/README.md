# CRT for Windows

The Windows build of **Conner's Retime Tool** — a desktop app for speedrunners
and moderators to time runs accurately, with or without loads.

This is a native rewrite of the original Python app (`../src/`) in **C# / .NET 8
/ WinUI 3 (Windows App SDK)**. It implements
[`../docs/native-rewrite-spec.md`](../docs/native-rewrite-spec.md), which is the
source of truth for behavior shared with the macOS build in `../src-macos/`.

Version **2.0.0**.

## Requirements

**To run**

- Windows 10 version 1809 (build 17763) or newer, or Windows 11. x64 only.
- Nothing else. The app is **unpackaged**
  (`<WindowsPackageType>None</WindowsPackageType>`) and **self-contained**
  (`<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`), so the
  Windows App SDK runtime ships inside the build output — there is no separate
  runtime installer and no MSIX deployment step.
- `ffmpeg` / `ffprobe` and `yt-dlp` are **not** bundled. The video-retimer
  features (YouTube/URL import, probing, timer-overlay export) need them, and
  the app downloads them on demand the first time you use a feature that
  requires one — after asking, with a progress dialog — into
  `%LOCALAPPDATA%\CRT\CRT\tools\`. If you already have them on `PATH`, or point
  Settings at explicit paths, nothing is downloaded.

**To build**

- The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- That's it — Visual Studio is not required. Everything below works from a plain
  terminal. The XAML is deliberately kept simple enough for the Windows App SDK
  XAML compiler to handle under `dotnet build`.

NuGet packages are restored from nuget.org (pinned in `nuget.config`):
`Microsoft.WindowsAppSDK` 1.6.250205002, `Microsoft.Windows.SDK.BuildTools`
10.0.26100.1742, `CommunityToolkit.Mvvm` 8.3.2, and
`System.Security.Cryptography.ProtectedData` 8.0.0 for the core library.

## Build, test, run

Run these from the **repository root**:

```sh
dotnet build src-windows/CRT.sln
dotnet test src-windows/CRT.Core.Tests/CRT.Core.Tests.csproj
dotnet run --project src-windows/CRT/CRT.csproj
```

`dotnet test` runs the 133 xUnit tests covering the UI-free core: frame-input
parsing, ISO time formatting, mod-note placeholder substitution, the Discord and
YouTube-chapter builders, run-file round-tripping (including Python-format
compatibility in both directions), segment↔loads gap conversion, the ffmpeg
filtergraph builder, INI round-tripping, and the hotkey slug rule.

> **Do not pass `-p:Platform=x64`.** The app project targets `x64` only, but the
> solution deliberately exposes just `Debug|Any CPU` and `Release|Any CPU` and
> maps `Any CPU` → `x64` for `CRT`. Passing an explicit platform fails with:
>
> ```
> error MSB4126: The specified solution configuration "Debug|x64" is invalid.
> ```
>
> Leave the platform properties blank and the mapping does the right thing.

### Distributable build

```sh
dotnet publish src-windows/CRT/CRT.csproj -c Release -r win-x64 --self-contained
```

Output lands in
`src-windows/CRT/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`.
The folder is fully standalone — the .NET runtime, the Windows App SDK, and the
WinUI 3 framework are all in there next to `CRT.exe`. Copy the folder anywhere
and run it.

## Project layout

```
src-windows/
├─ CRT.sln
├─ nuget.config
├─ CRT.Core/                 net8.0 class library — no UI dependencies
│  ├─ Models/                TimeSession, Load, Segment, RunMeta (decimal math)
│  ├─ Parsing/               frame-input parser, YouTube debug-info parsing
│  ├─ Formatting/            ISO times, mod notes, Discord, YouTube chapters
│  ├─ Files/                 run files, recents, library, autosave, config paths
│  ├─ Settings/              INI reader/writer, settings service, DPAPI key store
│  ├─ Net/                   speedrun.com, YouTube innertube, update checker
│  ├─ Tools/                 tool locator, ffprobe, yt-dlp, ffmpeg filtergraph
│  ├─ Hotkeys/               hotkey action registry
│  └─ Localization/          en / fr / pl / es string catalogs
├─ CRT.Core.Tests/           net8.0 xUnit tests for CRT.Core
└─ CRT/                      net8.0-windows10.0.19041.0 WinUI 3 app
   ├─ Views/                 Dashboard, Retimer, Video Retimer, Settings, sidebar
   ├─ ViewModels/            MVVM view models (CommunityToolkit.Mvvm)
   ├─ Services/              dialogs, clipboard, theming, key gestures, DI-ish root
   └─ Assets/icon.ico
```

All timing, parsing, formatting, file and network logic lives in `CRT.Core` so
it can be tested headlessly; the WinUI layer binds to view models and holds no
business logic.

## What's in the app

A `NavigationView` shell with **Dashboard**, **Frame Retimer**, **Video
Retimer**, and **Settings** in the footer.

**Frame Retimer**

- Two modes, switched by a segmented control: **Load mode** (start of run, end of
  run, individual loads subtracted) and **Segment mode** (mark any number of
  segments and total them, with the full-run span shown alongside).
- Two large click-to-copy time cards — *Without Loads* / *With Loads*, or
  *Segment Total* / *Full Run* in segment mode.
- Framerate field with a quick-pick flyout, plus Start/End frame rows and a
  loads/segments entry pair. Each frame row has a **Paste** button that runs the
  clipboard through the frame parser — raw frames, seconds, or a pasted YouTube
  "Stats for nerds" debug blob — and warns when the video's real encoded
  framerate doesn't match the session framerate.
- **Copy Mod Note** as a split button, with Copy Discord Message and Copy
  YouTube Chapters in the dropdown.
- Collapsible sidebar listing every load/segment with its duration, inline
  start/end editing, per-row delete, and Clear.
- In-window menu bar: File (New, Open, Session History, Save, Save As, Settings,
  Exit), Edit (Undo, Redo, the three copy actions, Clear Loads), View (Always on
  Top, enabled by default), Help (About).
- Undo/redo, dirty tracking with save prompts, session history, and a persistent
  recent-files list.

**Video Retimer**

- Import a local video file, a direct video URL, or a YouTube URL (downloaded
  with yt-dlp into the video cache and re-used on the next import of the same
  video). The session framerate is set from the video's real fps.
- `MediaPlayerElement` playback with a frame-accurate frame/time readout, a
  timeline slider that draws the marked segments as coloured regions, play/pause,
  and true single-frame stepping.
- Mark run/segment/load boundaries with buttons or hotkeys, writing straight into
  the same `TimeSession` the Frame Retimer edits — the two pages are two views of
  one run, so nothing needs to be "sent" between them.
- **Export Retimed Video**: ffmpeg burns a LiveSplit-style timer into the corner
  — the clock runs during gameplay, freezes through loads (or between segments),
  and holds the final time — with a cancellable progress dialog.

**Dashboard**

- A run library of everything you've timed, showing title, game, mode, times and
  modified date, with Open, Reveal in Explorer, Copy Mod Note and Remove.
- Quick actions: New Retime, Open File, Import Video, plus a live tile for the
  current unsaved session.
- **Speedrun.com integration**: sign in with an API key, then see **Runs to
  Verify** across every game you moderate — game, category, players, submitted
  date, claimed time — and Watch, Retime (jumps into the Video Retimer with a
  YouTube link prefilled), Verify or Reject each one without leaving the app.
  A "My recent runs" list sits below it. Signed out, the library still works.

**Settings**

Update checks, theme (Automatic/Dark/Light), accent colour with a native colour
picker, language, mod-note format, timer overlay corner and style, explicit
ffmpeg/yt-dlp paths, default mode, and a full hotkey editor (capture a new
combination live, reset per row or all, duplicate detection). Apply / Cancel /
Restore Defaults, as in the Python app. Changing language asks you to restart.

**Hotkeys** — every action has a customisable shortcut, including the video
marking and stepping keys.

**Languages** — English, Français, Polski and Español, ported from the Python
app's catalogs, with English fallback for any missing key.

**Theming** — Mica backdrop, follows the system light/dark theme on Automatic or
forces one otherwise, and your accent colour overrides the system accent for
primary buttons and highlights.

## Where your files live

Everything is per-user, under:

```
%LOCALAPPDATA%\CRT\CRT\
├─ settings.ini        preferences + [Hotkeys]
├─ src_api_key.bin     speedrun.com API key, DPAPI-protected (CurrentUser)
├─ library.json        the dashboard run library index
├─ recent.json         recent files (capped at 20)
├─ autosave.json       crash-recovery snapshot; removed on a clean exit
├─ tools\              ffmpeg.exe / ffprobe.exe / yt-dlp.exe, if downloaded
└─ video-cache\        videos downloaded for the video retimer
```

That base directory is exactly what `appdirs.user_config_dir("CRT")` resolves to
on Windows, which is deliberate:

- **`settings.ini` is the same file the Python app uses.** Install this build
  over an existing Python install and your preferences, mod-note format and
  hotkeys carry over untouched.
- **Run `.json` files are interchangeable in both directions.** This app writes
  the Python app's `start_frame` / `end_frame` / `framerate` / `loads` keys plus
  a few extra ones (`mode`, `segments`, `meta`), and the Python app simply
  ignores the extras. Segment-mode runs are written with the run bounds and the
  gaps between segments stored as loads, so they still total correctly when
  opened in the Python app or on macOS.

The API key is the one exception — it is never written to `settings.ini`, and
because it is DPAPI-protected for the current user it does not travel between
accounts or machines.
