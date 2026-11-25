#!/usr/bin/env bash
# Build and start the Warp screen layout helper on macOS.
#
# Control-Option-1 through Control-Option-9 tile that many Warp windows.
# Control-Option-0 tiles ten. Run this from a Warp or Cursor terminal: the mover
# inherits that app's Accessibility access. A login item does not get the grant.
#
# Usage: scripts/warp-screen-layout/install.sh [--dry-run]
set -euo pipefail

DRY=0
if [ "${AW_DRY_RUN:-0}" = "1" ]; then DRY=1; fi
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY=1 ;;
    *) echo "usage: scripts/warp-screen-layout/install.sh [--dry-run]" >&2; exit 1 ;;
  esac
done

if [ "$DRY" = 1 ]; then
  echo "dry-run: would build Warp Screen Layout and Warp Screen Layout Keys, start the mover, and open the key listener"
  exit 0
fi

if [ "$(uname -s)" != "Darwin" ]; then
  echo "warp-screen-layout: skipped on $(uname -s)"
  exit 0
fi
command -v swiftc >/dev/null 2>&1 || { echo "warp-screen-layout: swiftc is required" >&2; exit 1; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$ROOT/main.swift"
APPS="$HOME/Applications"
MOVER="$APPS/Warp Screen Layout.app"
KEYS="$APPS/Warp Screen Layout Keys.app"
BIN_NAME="Warp Screen Layout"

write_plist() {
  local dest="$1" bundle_id="$2" bundle_name="$3"
  mkdir -p "$(dirname "$dest")"
  cat >"$dest" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key>
  <string>${BIN_NAME}</string>
  <key>CFBundleIdentifier</key>
  <string>${bundle_id}</string>
  <key>CFBundleName</key>
  <string>${bundle_name}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>LSBackgroundOnly</key>
  <false/>
  <key>LSUIElement</key>
  <true/>
</dict>
</plist>
EOF
}

mkdir -p "$MOVER/Contents/MacOS" "$KEYS/Contents/MacOS"
write_plist "$MOVER/Contents/Info.plist" "com.joi.warp-screen-layout" "Warp Screen Layout"
write_plist "$KEYS/Contents/Info.plist" "com.joi.warp-screen-layout.keys" "Warp Screen Layout Keys"
swiftc -O -framework AppKit -framework Carbon -o "$MOVER/Contents/MacOS/$BIN_NAME" "$SRC"
cp "$MOVER/Contents/MacOS/$BIN_NAME" "$KEYS/Contents/MacOS/$BIN_NAME"
codesign --force --sign - "$MOVER"
codesign --force --sign - "$KEYS"

if ! pgrep -f "Warp Screen Layout.app/Contents/MacOS/${BIN_NAME} --worker" >/dev/null 2>&1; then
  log="$HOME/Library/Logs/warp-screen-layout.log"
  mkdir -p "$(dirname "$log")"
  python3 - "$MOVER/Contents/MacOS/$BIN_NAME" "$log" <<'PY'
import os, sys
binary, log_path = sys.argv[1], sys.argv[2]
log = open(log_path, "ab")
if os.fork():
    raise SystemExit(0)
os.setsid()
if os.fork():
    raise SystemExit(0)
os.dup2(log.fileno(), 1)
os.dup2(log.fileno(), 2)
os.execv(binary, [binary, "--worker"])
PY
fi

open -g -a "$KEYS"

marker="# warp-screen-layout: start"
zshrc="$HOME/.zshrc"
block=$(cat <<EOF
$marker
# Control-Option-1 through 0 tiles open Warp windows.
# The mover has to start from a terminal so it inherits Warp or Cursor Accessibility access.
if ! pgrep -f "Warp Screen Layout --worker" >/dev/null 2>&1; then
  nohup "$MOVER/Contents/MacOS/$BIN_NAME" --worker >>"\$HOME/Library/Logs/warp-screen-layout.log" 2>&1 &
  disown
fi
open -g -a "$KEYS"
# warp-screen-layout: end
EOF
)
if [ -f "$zshrc" ] && grep -q "$marker" "$zshrc"; then
  echo "warp-screen-layout: shell startup already present"
else
  printf '\n%s\n' "$block" >>"$zshrc"
  echo "warp-screen-layout: added the startup lines to $zshrc"
fi
echo "warp-screen-layout: press Control-Option and the number of Warp windows"
