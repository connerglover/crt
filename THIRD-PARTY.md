# Third-party tools bundled with CRT

CRT ships two external programs in the `tools` folder beside the executable.
Neither is linked into the application — both are launched as separate
processes — so they are aggregated with CRT rather than derived from it, and
they keep their own licences.

If the folder is missing or a tool has been removed, CRT offers to download the
same tools on first use instead. Nothing here is required for the app to start.

## FFmpeg (`ffmpeg.exe`, `ffprobe.exe`)

- Upstream: <https://ffmpeg.org>
- Build: <https://github.com/BtbN/FFmpeg-Builds> — the `win64-gpl` variant.
- Licence: GPL v2 or later, because this build includes x264. The full text
  ships as `tools/FFMPEG-LICENSE.txt`.
- Source: FFmpeg's is at <https://github.com/FFmpeg/FFmpeg> and the exact build
  configuration at the BtbN repository above. Both are unmodified upstream
  builds; CRT patches neither.

The GPL build is used because the export encodes with x264, which is
GPL-licensed and therefore absent from the LGPL build — against that variant
every export fails with "unknown encoder". CRT invokes ffmpeg only as a
separate process and links against none of it, so bundling it is aggregation
rather than derivation, and CRT's own licence is unaffected.

If you would rather not redistribute a GPL binary, delete the `tools` folder:
CRT then offers to download the same tools on first use, and nothing else
changes. The exporter also picks whichever H.264 encoder the ffmpeg in use
actually provides, so an LGPL build still works — via OpenH264 instead of x264.

Used for: probing video metadata (`ffprobe`) and rendering the exported video
with the burned-in timer (`ffmpeg`).

## yt-dlp (`yt-dlp.exe`)

- Upstream: <https://github.com/yt-dlp/yt-dlp>
- Licence: The Unlicense (public domain).

Used for: importing a run from a YouTube or other supported video URL.
