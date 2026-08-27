#!/usr/bin/env bash
#
# make-app.sh — build CRT in release configuration and assemble a double
# clickable macOS application bundle at <package root>/build/CRT.app.
#
# Usage:  bash Scripts/make-app.sh          (or: make app)
#
# The script works from any working directory: every path is derived from the
# location of this file. Icon generation and code signing are best effort and
# never fail the build.

set -euo pipefail

APP_NAME="CRT"
BUNDLE_ID="com.connerglover.crt"
DISPLAY_NAME="Conner's Retime Tool"
VERSION="2.0.0"
MIN_MACOS="14.0"

# --- Paths -------------------------------------------------------------------

SCRIPT_DIR="$(cd -- "$(dirname -- "$0")" && pwd -P)"
PACKAGE_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd -P)"
BUILD_DIR="${PACKAGE_ROOT}/build"
APP_DIR="${BUILD_DIR}/${APP_NAME}.app"
CONTENTS_DIR="${APP_DIR}/Contents"
MACOS_DIR="${CONTENTS_DIR}/MacOS"
RESOURCES_DIR="${CONTENTS_DIR}/Resources"
# Shared with the Windows build: repo-root src/icon.ico.
ICON_SOURCE="${PACKAGE_ROOT}/../src/icon.ico"

# --- Output helpers ----------------------------------------------------------

info() { printf '==> %s\n' "$*"; }
warn() { printf 'warning: %s\n' "$*" >&2; }
die() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

# --- Preconditions -----------------------------------------------------------

[ "$(uname -s)" = "Darwin" ] || die "make-app.sh assembles a macOS .app bundle and must run on macOS."
command -v swift >/dev/null 2>&1 || die "swift was not found on PATH. Install Xcode 15+ or a Swift 5.9+ toolchain."

# --- Build -------------------------------------------------------------------

cd "${PACKAGE_ROOT}"

info "Building ${APP_NAME} ${VERSION} (release)…"
swift build -c release

BIN_DIR="$(swift build -c release --show-bin-path)"
BINARY="${BIN_DIR}/${APP_NAME}"
[ -x "${BINARY}" ] || die "Release binary not found at ${BINARY}."

# --- Bundle layout -----------------------------------------------------------

info "Assembling ${APP_DIR}…"
rm -rf "${APP_DIR}"
mkdir -p "${MACOS_DIR}" "${RESOURCES_DIR}"

cp "${BINARY}" "${MACOS_DIR}/${APP_NAME}"
chmod +x "${MACOS_DIR}/${APP_NAME}"

cat > "${CONTENTS_DIR}/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleIdentifier</key>
    <string>${BUNDLE_ID}</string>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>
    <string>${DISPLAY_NAME}</string>
    <key>CFBundleExecutable</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>${MIN_MACOS}</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
PLIST

# Legacy type/creator stub; harmless and expected in a standard bundle.
printf 'APPL????' > "${CONTENTS_DIR}/PkgInfo"

# --- Icon (best effort) ------------------------------------------------------

# Converts src/icon.ico into Contents/Resources/AppIcon.icns using sips +
# iconutil. Always returns 0: a missing icon must never fail the build.
make_icon() {
    if [ ! -f "${ICON_SOURCE}" ]; then
        warn "icon source not found at ${ICON_SOURCE} — building without an app icon."
        return 0
    fi
    if ! command -v sips >/dev/null 2>&1; then
        warn "sips not available — building without an app icon."
        return 0
    fi
    if ! command -v iconutil >/dev/null 2>&1; then
        warn "iconutil not available — building without an app icon."
        return 0
    fi

    icon_work=""
    if ! icon_work="$(mktemp -d "${TMPDIR:-/tmp}/crt-icon.XXXXXX")"; then
        warn "could not create a temporary directory — building without an app icon."
        return 0
    fi

    iconset_dir="${icon_work}/${APP_NAME}.iconset"
    base_png="${icon_work}/base.png"
    mkdir -p "${iconset_dir}"

    if ! sips -s format png "${ICON_SOURCE}" --out "${base_png}" >/dev/null 2>&1; then
        warn "sips could not read ${ICON_SOURCE} — building without an app icon."
        rm -rf "${icon_work}"
        return 0
    fi

    # "<pixel size> <iconset file name>" — the full set iconutil expects.
    icon_ok=1
    for spec in \
        "16 icon_16x16.png" \
        "32 icon_16x16@2x.png" \
        "32 icon_32x32.png" \
        "64 icon_32x32@2x.png" \
        "128 icon_128x128.png" \
        "256 icon_128x128@2x.png" \
        "256 icon_256x256.png" \
        "512 icon_256x256@2x.png" \
        "512 icon_512x512.png" \
        "1024 icon_512x512@2x.png"
    do
        size="${spec%% *}"
        name="${spec#* }"
        if ! sips -z "${size}" "${size}" "${base_png}" --out "${iconset_dir}/${name}" >/dev/null 2>&1; then
            icon_ok=0
            break
        fi
    done

    if [ "${icon_ok}" -eq 1 ] && iconutil -c icns "${iconset_dir}" -o "${RESOURCES_DIR}/AppIcon.icns" >/dev/null 2>&1; then
        info "Generated Resources/AppIcon.icns from src/icon.ico."
    else
        warn "could not generate AppIcon.icns — the app will use the default icon."
    fi

    rm -rf "${icon_work}"
    return 0
}

make_icon || warn "icon generation failed — continuing without an app icon."

# --- Ad-hoc code signing (best effort) ---------------------------------------

if command -v codesign >/dev/null 2>&1; then
    info "Signing ${APP_NAME}.app (ad-hoc)…"
    if ! codesign --force --deep --sign - "${APP_DIR}" >/dev/null 2>&1; then
        warn "ad-hoc codesign failed — macOS may ask for a Gatekeeper override on first launch."
    fi
else
    warn "codesign not available — the bundle is unsigned."
fi

# --- Done --------------------------------------------------------------------

info "Built ${APP_NAME} ${VERSION}:"
printf '%s\n' "${APP_DIR}"
