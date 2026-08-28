# Third-party tools bundled with CRT

CRT ships two external programs in the `tools` folder beside the executable.
Neither is linked into the application — both are launched as separate
processes — so they are aggregated with CRT rather than derived from it, and
they keep their own licences.

If the folder is missing or a tool has been removed, CRT offers to download the
same tools on first use instead. Nothing here is required for the app to start.

## FFmpeg (`ffmpeg.exe`, `ffprobe.exe`)

- Upstream: <https://ffmpeg.org>
- Build: <https://github.com/BtbN/FFmpeg-Builds> — the `win64-lgpl` variant.
- Licence: LGPL v2.1 or later. The full text ships as `tools/FFMPEG-LICENSE.txt`.
- Source: available from the upstream project and from the build repository
  above.

The LGPL build is used in preference to the GPL one. CRT invokes ffmpeg only
through the command line, so either would be permissible as mere aggregation,
but the LGPL variant keeps the obligations narrowly on the shipped binary.

Used for: probing video metadata (`ffprobe`) and rendering the exported video
with the burned-in timer (`ffmpeg`).

## yt-dlp (`yt-dlp.exe`)

- Upstream: <https://github.com/yt-dlp/yt-dlp>
- Licence: The Unlicense (public domain).

Used for: importing a run from a YouTube or other supported video URL.
