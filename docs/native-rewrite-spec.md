# CRT Native Rewrite — Shared Specification

This document is the single source of truth for the two native rewrites of
Conner's Retime Tool (CRT):

- `src-windows/` — C# / .NET 8 / **WinUI 3** (Windows App SDK)
- `src-macos/` — Swift 5.9+ / **SwiftUI** (macOS 14+)

Both apps must implement everything in this spec. Where the spec references the
Python implementation, the Python code under `src/crt/` is normative — port its
observable behavior exactly unless this spec says otherwise.

Version for both apps: **2.0.0**.

---

## 1. Core timing model

Port from `src/crt/time.py`, `src/crt/load.py`, `src/crt/decorators.py`.

- All time math uses **decimal** arithmetic (C#: `decimal`; Swift: `Decimal`),
  never binary floats, so `29.97` behaves exactly.
- `Load { startFrame: Int, endFrame: Int }`, `length = end - start`.
- `TimeSession`:
  - `startFrame`, `endFrame` (Int), `framerate` (decimal, default 60),
    `precision` (Int, default 3), `loads: [Load]`.
  - `lengthWithLoads = endFrame - startFrame` (frames)
  - `lengthWithoutLoads = lengthWithLoads - Σ load.length`
  - `withLoads = round(lengthWithLoads / framerate, precision)` seconds;
    **0 if framerate == 0** (never divide by zero).
  - `withoutLoads` analogous.
  - Load validation (on add AND edit): reject `start == end`
    ("The duration of the load is 0.000") and `start > end`
    ("The load time ends before it starts."). Adding a `0,0` load →
    "You must provide an input for the loads".
  - “Concerningly long load” guard: when adding a load and previous loads
    exist, if `newLength > 10 × (average existing load length)`, confirm with
    the user before adding (see `_add_loads` in `src/crt/app/app.py`).

### 1.1 NEW — Segment mode

The retimer has two modes, switchable via a segmented control at the top of the
retimer page:

- **Load mode** (classic): start of run + end of run + loads (subtracted).
- **Segment mode**: a list of `Segment { startFrame, endFrame }`. The run time
  is the **sum of segment lengths**. Two computed displays:
  - *Segment Total* = `Σ (seg.end - seg.start) / framerate` (primary display)
  - *Full Run* = `(max seg.end - min seg.start) / framerate` (secondary)
- Segments use the same validation as loads and the same inline-editable
  sidebar (the sidebar header/labels switch between "Loads" and "Segments").
- Mode is per-session and saved in the file (see §3).
- In segment mode the main input rows become: Framerate, then an
  "add segment" pair (Segment Start / Segment End + paste buttons) with an
  **Add Segment** button. Copy actions (§5) use *Segment Total* as
  time-without-loads and *Full Run* as time-with-loads.

## 2. Frame input parsing

Port `src/crt/frame_input.py` exactly:

1. Trim. If text contains `{` and `"cmt"` → YouTube debug-info JSON: find first
   `{`, parse JSON, read `cmt` (seconds, string or number) →
   `frame = round(cmt × framerate)`. Parse failure / missing `cmt` → error
   "The debug info provided is invalid.\nPlease re-enter debug info."
2. Else strip every char except `[0-9.]`. No digits → `0`.
3. Collapse multiple `.` (keep the first, drop the rest).
4. If a `.` remains → value is **seconds**: `frame = round(value × framerate)`
   (0 if framerate is 0).
5. Else plain integer.

`cleanFramerate` (for the framerate field): same stripping/collapsing; empty →
0; trailing `.` gets `0` appended; parse as decimal, failures → 0.

### 2.1 YouTube framerate mismatch detection

Port the behavior of `src/crt/youtube_format.py` + `_confirm_framerate_from_debug_info`
in `src/crt/app/app.py`:

- Debug info also carries `docid` (video id) and `fmt` (itag). When a paste
  contains both, look up the video's real encoded fps for that itag and, if it
  differs from the session framerate by ≥ 1 fps, ask
  "This video appears to be {fps} FPS, but the session is set to {cur} FPS.
  Update the framerate before calculating the frame?" — on yes, set framerate
  first. Ask at most once per (videoId, itag) per app session; cache lookups.
- **Lookup implementation (no yt-dlp dependency for this):** POST
  `https://www.youtube.com/youtubei/v1/player` with JSON body
  `{"videoId": "<id>", "context": {"client": {"clientName": "ANDROID", "clientVersion": "20.10.38", "androidSdkVersion": 30, "hl": "en"}}}`
  and header `User-Agent: com.google.android.youtube/20.10.38 (Linux; U; Android 11) gzip`.
  (Verified working 2026-08: returns `playabilityStatus.status == "OK"` and
  itag+fps for ~30 formats. The older 19.x client version is rejected with
  FAILED_PRECONDITION, and the WEB client returns no formats — do not use
  those.) If ANDROID fails, retry once with clientName `IOS`, clientVersion
  `20.10.4`, `deviceModel: "iPhone16,2"`, UA
  `com.google.ios.youtube/20.10.4 (iPhone16,2; U; CPU iOS 17_5_1 like Mac OS X)`.
  Read `streamingData.formats` + `streamingData.adaptiveFormats`, find the
  entry whose `itag` (int) matches `fmt` (note: `fmt` from debug info is a
  string; compare after string-conversion), return its `fps`. Any failure
  (network, parsing, itag missing) → silently give up (no prompt). 8s timeout.
  Run off the UI thread; show a busy cursor/spinner while looking up.
  If the innertube call fails and a `yt-dlp` binary is available (§8), fall
  back to `yt-dlp -j <url>` and read `formats[].format_id == fmt → fps`.

## 3. File format (must stay interchangeable with the Python app)

`*.json` run files. The Python app writes/reads:

```json
{"start_frame": 0, "end_frame": 0, "framerate": "60", "loads": [[s, e], ...]}
```

- `framerate` is a **string** on write. On read accept string or number.
- The native apps write a superset (Python ignores nothing — it only reads the
  four keys above, so extra keys are safe):

```json
{
  "start_frame": 0, "end_frame": 5000, "framerate": "60",
  "loads": [[100, 200]],
  "mode": "loads" | "segments",
  "segments": [[s, e], ...],
  "meta": {"title": "Any%", "game": "…", "notes": "…", "created": "ISO8601", "modified": "ISO8601", "video_url": "…"}
}
```

- In segment mode, still write `start_frame` = min segment start, `end_frame` =
  max segment end, `loads`: the **gaps between segments** (so the file degrades
  gracefully in the Python app: run bounds minus gaps == segment total).
- On read: if `mode`/`segments` missing → loads mode.
- Reading a corrupt file → error "The file provided is corrupted."

## 4. Settings

Port `src/crt/app_settings/app.py`. INI file named `settings.ini`, section
`[Settings]` + `[Hotkeys]`, kept **compatible with the Python app's file**:

- Windows path: `%LOCALAPPDATA%\CRT\CRT\settings.ini` (that is what
  `appdirs.user_config_dir("CRT")` resolves to; keep it so existing users'
  settings carry over). C#: implement a tiny INI reader/writer (no dependency).
- macOS path: `~/Library/Application Support/CRT/settings.ini`.
- Keys (defaults): `enable_updates=True`, `theme=Automatic` (stored in
  English: Automatic/Dark/Light), `accent_color=#5b9bd5`, `language=en`,
  `mod_note_format=Mod Note: Retimed to {time_without_loads}`.
- Missing keys/sections are synced in with defaults on startup (file rewritten).
- Restore Defaults (with confirm), Apply, Cancel semantics as in Python.
- New keys (native only, synced with defaults the same way):
  `timer_corner=bottom-right` (top-left|top-right|bottom-left|bottom-right),
  `timer_style=pill` (pill|plain), `ffmpeg_path=` (empty = auto),
  `ytdlp_path=` (empty = auto), `default_mode=loads`.
- Speedrun.com API key is **not** stored in settings.ini: Windows → DPAPI
  (`ProtectedData.Protect`, CurrentUser scope) in `src_api_key.bin` next to
  settings.ini; macOS → Keychain (`kSecClassGenericPassword`,
  service "CRT Speedrun.com").

## 5. Copy actions & formats

Port exactly from `src/crt/app/app.py`:

- **ISO display format** (`format_iso`): drops leading zero *units*, but every
  unit that is shown is **two digits**:
  - under a minute → `SS.mmm` (zero-state `"00.000"`)
  - under an hour → `MM:SS.mmm` — e.g. 60s renders `01:00.000`, **not**
    `1:00.000`
  - an hour or more → `HH:MM:SS.mmm` — e.g. 3600s renders `01:00:00.000`,
    and 999999 frames @60fps renders `04:37:46.650`

  This is verified against the shipping Python app (`format_components` returns
  `f"{minutes:02}"` / `f"{hours:02}"` and `format_iso` interpolates those padded
  strings directly). Do **not** un-pad the leading unit: mod notes produced by
  this tool are pasted into speedrun.com by moderators, so the format must stay
  byte-identical to the Python app's. Negative →
  clamp to 0. Milliseconds always 3 digits (left-pad the fractional part:
  a value of `0.05` renders `.050`... implement by decimal string split as the
  Python does: `str` the rounded decimal, split on '.', left side → h/m/s via
  divmod, right side left-padded to 3 with zeros — for precision 3 this is
  exact).
- **Mod note**: template with placeholders `{time_with_loads}`,
  `{time_without_loads}`, `{hours}` `{minutes}` `{seconds}` (2-digit)
  `{milliseconds}` (3-digit) — components of the **with-loads** time,
  `{start_frame}`, `{end_frame}`, `{start_time}`, `{end_time}` (start/end frame
  ÷ fps rounded to precision; 0 when fps is 0), `{total_frames}`
  (with-loads frame length), `{fps}`, `{plug}` = 
  `[Conner's Retime Tool](https://github.com/connerglover/conners-retime-tool)`.
  Unknown placeholders in the user's template must not crash — leave them
  literal.
- **Discord message**: code block:
  ```
  Time: {without}
  Time (with loads): {with}

  Loads (N):
  1. {startTime} - {endTime} ({duration})
  ```
  (loads section only when loads exist; times via frame→ISO at session fps).
  In segment mode, list segments instead ("Segments (N):"), Time = segment
  total, Time (with loads) = full-run span.
- **YouTube chapters**: loads sorted by start; lines `0:00 Gameplay`, then per
  load `{ts} Loading` / `{ts} Gameplay`; timestamps `M:SS` or `H:MM:SS`
  (floor to seconds). Segment mode: `0:00 Waiting` then per segment
  `{ts} Segment {i}` / `{ts} Waiting` (first line at 0:00 always).
- Clicking either big time display copies its ISO string.

## 6. Main retimer UI (parity)

Mirror the Qt layout (`src/crt/app/gui.py`) in native idiom:

- Two large click-to-copy time cards ("WITHOUT LOADS" / "WITH LOADS" — in
  segment mode "SEGMENT TOTAL" / "FULL RUN"), monospaced digits.
- Input rows: Framerate (no paste), Start Frame, End Frame, Start Frame
  (Loads), End Frame (Loads) — each frame row has a Paste button that pulls
  from the clipboard through the §2 parser (with §2.1 mismatch check).
  Values commit on Enter/focus-loss; committed value replaces the field text
  with the parsed frame number.
- Buttons: **Copy Mod Note** (primary; split-button with dropdown: Copy
  Discord Message, Copy YouTube Chapters) and **Add Loads**.
- Right sidebar: collapsible loads/segments panel — count summary, per-row
  card: "Load N" + duration chip + delete (✕) + inline Start/End edit fields,
  Clear Loads button (disabled when empty), empty-state hint text.
- Menus (native menu bar on macOS; a Menu bar in-window on WinUI):
  File (New Time Ctrl/Cmd+N, Open Time Ctrl/Cmd+O, Session History Ctrl/Cmd+H,
  Save Ctrl/Cmd+S, Save As Ctrl/Cmd+Shift+S, Settings Ctrl/Cmd+Comma, Exit),
  Edit (Copy Mod Note Ctrl/Cmd+M, Copy Discord Message, Copy YouTube Chapters,
  Clear Loads), View (Always on Top — Windows: `OverlappedPresenter.IsAlwaysOnTop`,
  macOS: `NSWindow.level = .floating`; **on by default** at launch), Help (About).
- Unsaved-changes ("dirty") tracking with Save/Don't Save/Cancel prompts before
  New/Open/switch/exit — port `_prompt_save_if_dirty`.
- Session history: in-app list of previously opened/saved file paths this
  session (port `src/crt/file_manager.py` history rules) + **persisted recent
  files** across launches (new; store in config dir `recent.json`, cap 20).
- Update check on launch (unless disabled): GET
  `https://api.github.com/repos/connerglover/crt/releases/latest`, 5s timeout,
  silent on failure; if `tag_name` ≠ current version → dismissible banner
  "A new version ({v}) is available — click to download." → opens releases page.
- Errors surface as dialogs, never crashes; all handlers guarded.

## 7. Localization

Port all four dictionaries from `src/crt/language.py` (en, fr, pl, es) — read
that file for every key/translation. Keep the same key set, add new keys for
new UI (video retimer, dashboard, segments, SRC) with English fallback when a
translation is missing (the Python translate falls back to the key). Language
switch requires restart (same as Python; show the same "Please restart" info
dialog after settings change).

## 8. External tools (ffmpeg / yt-dlp)

`ToolLocator` service, same logic both platforms:

1. Explicit path from settings if set and exists.
2. Bundled/tool dir: `<config dir>/tools/ffmpeg(.exe)`, `.../yt-dlp(.exe)`.
3. `PATH` lookup (`where`/`which`).
4. If still missing when a feature needs it: prompt "CRT needs {tool} for this
   feature. Download it now? (~{size})" → download into the tools dir with a
   progress dialog, then continue the original action.
   - ffmpeg Windows: latest `ffmpeg-master-latest-win64-gpl.zip` from
     `https://github.com/BtbN/FFmpeg-Builds/releases/latest` (extract
     `bin/ffmpeg.exe` only). macOS: `https://evermeet.cx/ffmpeg/getrelease/zip`
     (universal static build), unzip, `chmod +x`, remove quarantine attr
     (`xattr -d com.apple.quarantine`).
   - yt-dlp: GitHub latest release asset `yt-dlp.exe` (win) /
     `yt-dlp_macos` (mac) from `yt-dlp/yt-dlp`.
5. Every subprocess: no shell, argument arrays, hidden window (Windows
   `CreateNoWindow`), capture stderr for error surfacing, cancellable.

## 9. NEW — Video Retimer mode

A second workspace (navigation item) for retiming directly from video.

### 9.1 Import

Three sources, one import box (segmented control or auto-detect):
- **Local file** — file picker (mp4/mkv/mov/webm/avi…), used directly.
- **Direct video URL** — downloaded to cache via HTTP (progress), or if
  ffprobe can stream it, still download for reliable frame stepping.
- **YouTube URL** (watch/shorts/youtu.be) — via yt-dlp:
  `yt-dlp -f "bv*[height<=1080][ext=mp4]+ba[ext=m4a]/b[height<=1080][ext=mp4]/b" --merge-output-format mp4 -o <cache>/%(id)s.%(ext)s <url>`
  with progress parsed from stdout (`[download]  42.3%`). Cache dir:
  `<config>/video-cache/`; re-use an existing cached file for the same id.
  Also read the video's fps via `yt-dlp -j` (or ffprobe after download) and
  **auto-set the session framerate** to the real value (with a toast).
- After import, probe with ffprobe (`-print_format json -show_streams`):
  fps (`avg_frame_rate` as a rational — keep exact, e.g. 30000/1001), duration,
  resolution. Set the retimer framerate from it.

### 9.2 Player

- Native playback: WinUI `MediaPlayerElement` / SwiftUI `VideoPlayer`
  (AVPlayer). Below the video: timeline slider, current frame + current time
  (frame-accurate readout: `frame = round(position × fps)`), play/pause.
- **Frame stepping**: buttons `◀` `▶` and keys `,` / `.` **and** `<` / `>`
  (Shift variants) step exactly one frame (pause first, then step).
  Windows: `MediaPlayer.StepForwardOneFrame()` / `StepBackwardOneFrame()`;
  macOS: `AVPlayerItem.step(byCount:)`. Hold Shift+Arrow → 1s jumps; arrow
  keys left/right = 5 frames.
- **Marking**: big buttons + keys — `[` = mark segment start at current frame,
  `]` = mark segment end (completes the segment and adds it to the list),
  `Space` = play/pause. In loads mode the same keys mark run start/end and
  `L` / `Shift+L` (or the buttons) mark load start/end. All these are
  customizable hotkeys (§10).
- Marks list mirrors the retimer sidebar (same rows, inline edit, delete) and
  is **the same session data** — the video retimer and frame retimer are two
  views over one `TimeSession` (switching pages keeps state; "Send to Frame
  Retimer" isn't needed — it *is* the same run). Current time displays
  (segment total / without loads etc.) are always visible.
- Show marked segments as colored regions on the timeline slider.

### 9.3 Export with timer overlay

"Export Retimed Video" → output file picker → runs ffmpeg with a generated
filtergraph; progress dialog (parse `time=` from ffmpeg stderr against known
duration), cancellable.

Timer semantics (LiveSplit-style):
- Video is trimmed to `[runStart − lead, runEnd + tail]` (lead/tail = 2s,
  clamped to video bounds).
- The overlay shows the **retimed** clock: 0.000 until run start; during
  gameplay it runs; during loads (loads mode) or between segments (segment
  mode) it **freezes**; after run end it stays at the final time.
- Implementation: piecewise `drawtext` chain — for every *running* window
  `[a,b)` with accumulated paused time `p` before it, add
  `drawtext=enable='between(t,a,b)':text='%{eif\:trunc((t-o)/3600)\:d\:2}\:%{eif\:trunc(mod((t-o)/60,60))\:d\:2}\:%{eif\:trunc(mod(t-o,60))\:d\:2}.%{eif\:trunc(mod((t-o)*1000,1000))\:d\:3}'`
  where `o = runStart + p` (constant per window); for every *frozen* window,
  a `drawtext` with the precomputed constant elapsed string. Before run start:
  constant `00:00:00.000`; after run end: constant final time. (Formatting of
  the final rendered strings may be `H:MM:SS.mmm` with zero units dropped only
  in the constant strings; the running expression uses `HH:MM:SS.mmm` — that
  is acceptable.)
- Style: monospace font (Windows: Consolas via `font=Consolas` — but prefer
  `fontfile=C\:/Windows/Fonts/consola.ttf`; macOS: `fontfile=/System/Library/Fonts/Menlo.ttc`),
  white text, `box=1:boxcolor=black@0.55:boxborderw=10`, fontsize = videoHeight/18,
  12px margin in the corner chosen in settings (default bottom-right:
  `x=w-tw-24:y=h-th-24`).
- Command shape:
  `ffmpeg -y -ss <trimStart> -to <trimEnd> -i <in> -vf "<drawtext chain>" -c:v libx264 -preset veryfast -crf 18 -c:a aac -movflags +faststart <out.mp4>`
  (`-ss` before `-i` for fast seek is fine since we re-encode; timer window
  times are then relative to the trimmed start — subtract `trimStart` from all
  window times).
- After export: success dialog with "Open" / "Show in folder".

## 10. Hotkeys

Port the registry (`src/crt/hotkeys/app.py`): every action id, label key and
default; INI option names via the same `[^a-z0-9]+ → _` slug rule. Editor
dialog: rows of action + current shortcut, click to capture a new combination
live, Reset per-row, Reset All, duplicate detection with the
"Duplicate Hotkey Message" warning. Defaults on Windows as in Python
(Ctrl+…); macOS swaps Ctrl→Cmd.

New action ids (same slug rule; defaults):
`video_frame_back` (,), `video_frame_forward` (.), `video_play_pause` (Space),
`video_mark_start` ([), `video_mark_end` (]), `video_mark_load_start` (L),
`video_mark_load_end` (Shift+L), `Toggle Mode` (Ctrl/Cmd+T).

## 11. NEW — Dashboard

The app opens on the Dashboard (navigation: Dashboard / Frame Retimer /
Video Retimer / Settings).

### 11.1 Run library

- Card grid/list of known runs: everything in `recent.json` plus any file
  saved/opened by the app (a `library.json` index in the config dir storing
  path, title, game, mode, final times, modified date). Rows show title (file
  name if none), time without loads (big), with loads, mode chip, modified.
- Actions per run: Open (→ Frame Retimer), Reveal in Explorer/Finder, Remove
  from library (not delete file), Copy Mod Note directly.
- Quick actions header: New Retime, Open File…, Import Video (→ Video
  Retimer), plus a live tile of the current unsaved session if dirty.

### 11.2 Speedrun.com integration

REST v1, base `https://www.speedrun.com/api/v1`, header `X-API-Key` when
authenticated, `User-Agent: crt/2.0.0`. All requests 10s timeout, paginated
via `max=200` + `pagination.links[rel=next]`, throttled ≤ 100 req/min, all off
the UI thread, all failures → inline error state (never crash).

- **Sign in**: paste API key (link "Get your key" →
  `https://www.speedrun.com/settings/api-key`). Validate via `GET /profile` →
  store key (§4), show username + avatar
  (`https://www.speedrun.com/userasset/{user}/image`? no — use
  `data.assets.image.uri` from the profile response; omit if null).
- **Runs to Verify view**: fetch `GET /games?moderator={userId}&max=200` (id +
  names). For each moderated game (parallel, ≤4 at a time):
  `GET /runs?status=new&game={gameId}&max=200&embed=players,category,level&orderby=submitted&direction=asc`.
  Flatten to a table: Game, Category (+level), Player(s)
  (`players[].names.international`, guests via `name`), submitted date,
  claimed time (`times.primary_t` seconds → ISO format §5), video link
  (first of `videos.links[].uri`).
  Row actions:
  - **Watch** → open video URL in browser
  - **Retime this** → if the video is a YouTube link, jump to Video Retimer
    with the URL prefilled (import starts immediately); else open Frame
    Retimer fresh session. Either way remember the run id as "retiming".
  - **Verify** / **Reject** → confirm dialog (reject requires a reason text
    box) → `PUT /runs/{id}/status` body
    `{"status":{"status":"verified"}}` or
    `{"status":{"status":"rejected","reason":"…"}}`. On the run being
    "retiming", prefill the reject reason / verify confirm text with the
    generated mod note. Refresh the row on success (remove from list).
  - Count badge on the nav item ("Runs to Verify (12)").
- **My recent runs** section: `GET /runs?user={userId}&orderby=date&direction=desc&max=10&embed=game,category`
  → compact list (game, category, time, status icon: verified ✓ / new ⏳ /
  rejected ✕).
- Poll/refresh button; auto-refresh every 5 min while dashboard visible.
- Signed-out state: explainer + sign-in box; library still fully works.

## 12. Theming

- **Windows**: Mica backdrop, respect system light/dark when theme=Automatic,
  force via `ElementTheme` otherwise; accent_color drives `AccentFillColorDefaultBrush`
  override for primary buttons/highlights (fall back to system accent if unset).
- **macOS**: native appearance; theme=Dark/Light forces
  `NSApp.appearance = NSAppearance(named:)`; accent color used as `.tint`.
- Both: the settings accent color picker uses the native color picker.

## 13. Project structure & quality bar

Shared expectations:
- MVVM: all logic in testable, UI-free core types (`Core/` — timing, parsing,
  formatting, files, settings, INI, ffmpeg filtergraph builder, SRC client,
  innertube client, tool locator). Views bind to view models; no business
  logic in code-behind/views beyond wiring.
- **Unit tests for the core** (`dotnet test` xUnit project; Swift Testing or
  XCTest via SPM): parsing (§2) including debug-info strings, ISO formatting
  edge cases (0, sub-minute, exact minute, hour, negative clamp), mod-note
  placeholder substitution incl. unknown placeholders, file round-trip incl.
  Python-format compatibility both directions, segment↔loads gap conversion,
  ffmpeg filtergraph builder (assert exact filter strings for a known
  segment/load layout), INI round-trip, hotkey slug rule.
- No blocking the UI thread anywhere (downloads, probing, SRC, innertube,
  export all async).
- Every user-visible string routed through the localization layer.

### 13.1 Windows (`src-windows/`)

- `CRT.sln`; projects `CRT/` (WinUI 3 app, `net8.0-windows10.0.19041.0`,
  `Microsoft.WindowsAppSDK` 1.6.x, `Microsoft.Windows.SDK.BuildTools`,
  `CommunityToolkit.Mvvm`; **unpackaged**: `<WindowsPackageType>None</WindowsPackageType>`,
  `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`) and
  `CRT.Core/` (`net8.0`, no UI deps) and `CRT.Core.Tests/` (xUnit).
- Must build with `dotnet build` alone (no Visual Studio requirement); keep
  XAML simple enough for the WindowsAppSDK XAML compiler.
- App icon: reuse `src/icon.ico` (copy to `src-windows/CRT/Assets/`).
- NavigationView shell (Dashboard, Frame Retimer, Video Retimer; Settings via
  footer). In-window MenuBar on the retimer pages per §6.
- README.md in `src-windows/` with build/run instructions
  (`dotnet build`, `dotnet run --project CRT`), including the note that the
  Windows App SDK runtime is bundled when self-contained.

### 13.2 macOS (`src-macos/`)

- Swift Package layout (buildable & testable headlessly with `swift build` /
  `swift test`; Xcode can open Package.swift directly):
  - `Package.swift` — executable target `CRT` (SwiftUI app, macOS 14),
    library target `CRTCore` (Foundation-only), test target `CRTCoreTests`.
  - `make app` / `Scripts/make-app.sh` builds a proper `CRT.app` bundle
    (release build, Info.plist with CFBundleIdentifier `com.connerglover.crt`,
    version 2.0.0, `NSHighResolutionCapable`, icon generated from
    `src/icon.ico` if `iconutil`/`sips` available; codesign ad-hoc).
  - `README.md` with instructions.
- SwiftUI `NavigationSplitView` shell; native menu bar via `.commands`;
  windows: main window; Settings scene (`Settings{}`) for preferences.
- Use `AppKit` bridges where SwiftUI lacks features (always-on-top, key
  capture for the hotkey editor, NSOpenPanel/NSSavePanel are fine via
  SwiftUI fileImporter/exporter or AppKit).

## 14. Nice-to-haves implemented as part of this rewrite

(Do implement — they're cheap on the new architecture:)

- **Undo/redo** on the session (Ctrl/Cmd+Z / Shift+Z): snapshot stack in the
  session view model (frame edits, load/segment add/edit/delete/clear, mode
  switch, framerate change).
- **Autosave**: dirty sessions snapshot to `<config>/autosave.json` every 30s;
  offer restore on next launch after a crash (delete on clean exit).
- Framerate quick-picks: 24 / 25 / 29.97 / 30 / 50 / 59.94 / 60 dropdown next
  to the framerate field.
- Status-bar toasts for copy actions ("Mod note copied") instead of silent.
- `Ctrl/Cmd+Shift+C` copies the without-loads time directly.

## 15. Explicitly out of scope

- Linux native rewrite (Python app remains for that).
- Bundled ffmpeg/yt-dlp binaries in the repo (downloaded at runtime instead).
- Speedrun.com OAuth (v1 API keys only).
- Twitch/other video-host download support beyond what yt-dlp handles.
