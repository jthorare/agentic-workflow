#!/usr/bin/env bash
# scripts/warp-screen-layout/install.sh stays quiet in --dry-run and refuses an unknown flag.
# Run: bash scripts/tests/warp-screen-layout.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
INSTALL="$ROOT/scripts/warp-screen-layout/install.sh"
fail=0
WORK="$(mktemp -d)"
trap 'rm -r "$WORK"' EXIT
export HOME="$WORK/home"
mkdir -p "$HOME"

check() {
  if [ "$2" = "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac; }

out="$(bash "$INSTALL" --dry-run 2>&1)"; rc=$?
check "dry-run exits 0" "$rc" "0"
contains "dry-run names the build" "$out" "dry-run: would build Warp Screen Layout"
if [ -e "$HOME/Applications/Warp Screen Layout.app" ]; then
  echo "not ok - dry-run wrote an app into HOME"; fail=1
else
  echo "ok - dry-run wrote nothing"
fi

out="$(bash "$INSTALL" --nope 2>&1)"; rc=$?
check "unknown flag exits 1" "$rc" "1"
contains "usage names the flag" "$out" "usage: scripts/warp-screen-layout/install.sh [--dry-run]"

setup="$(cat "$ROOT/setup.sh")"
contains "setup.sh runs the installer" "$setup" 'scripts/warp-screen-layout/install.sh'

# Non-dry install on this box (Linux in CI): skip and exit 0, no shell rc written.
out="$(bash "$INSTALL" 2>&1)"; rc=$?
check "non-dry install exits 0" "$rc" "0"
if [ "$(uname -s)" != "Darwin" ]; then contains "non-dry install skips on non-macOS" "$out" "skipped on"; fi
# On a Mac with Warp.app the installer writes the startup block into the test HOME.
if [ "$(uname -s)" = "Darwin" ] && [ -d /Applications/Warp.app ]; then
  if [ -e "$HOME/.zshrc" ]; then echo "ok - non-dry install wrote ~/.zshrc"; else echo "not ok - non-dry install wrote no ~/.zshrc"; fail=1; fi
else
  if [ -e "$HOME/.zshrc" ]; then echo "not ok - non-dry install wrote ~/.zshrc"; fail=1; else echo "ok - non-dry install wrote no ~/.zshrc"; fi
fi

# A uname stub says Darwin; with no Warp.app the installer skips and writes nothing.
if [ -d /Applications/Warp.app ]; then
  echo "ok - skipped the no-Warp stub run (Warp.app is installed here)"
else
  stub="$WORK/stub"; mkdir -p "$stub"
  printf '#!/bin/sh\necho Darwin\n' >"$stub/uname"; chmod +x "$stub/uname"
  out="$(PATH="$stub:$PATH" bash "$INSTALL" 2>&1)"; rc=$?
  check "Darwin without Warp.app exits 0" "$rc" "0"
  contains "Darwin without Warp.app says so" "$out" "Warp.app is not installed"
  if [ -e "$HOME/.zshrc" ] || [ -e "$HOME/Applications" ]; then echo "not ok - no-Warp run wrote into HOME"; fail=1; else echo "ok - no-Warp run wrote nothing"; fi
fi

# setup.sh only runs the installer with --with-warp-layout.
out="$(env -u AW_META_LANE -u AW_META_TEAM bash "$ROOT/setup.sh" --providers claude --dry-run 2>&1)"
contains "setup without the flag skips the layout" "$out" "skipped (opt-in: --with-warp-layout)"
case "$out" in *"dry-run: would build Warp Screen Layout"*) echo "not ok - setup ran the installer without the flag"; fail=1 ;; *) echo "ok - setup did not run the installer without the flag" ;; esac
out="$(env -u AW_META_LANE -u AW_META_TEAM bash "$ROOT/setup.sh" --providers claude --dry-run --with-warp-layout 2>&1)"
contains "setup with the flag runs the installer" "$out" "dry-run: would build Warp Screen Layout"

# Read the sources. Do not run the helper against the live socket.
swift="$(cat "$ROOT/scripts/warp-screen-layout/main.swift")"
tap_slice="$(printf '%s\n' "$swift" | awk '
  /key tap was not created/ { capture = 1 }
  capture { buf = buf $0 "\n" }
  capture && /return/ { exit }
  END { printf "%s", buf }
')"
contains "failed tap asks for Accessibility" "$tap_slice" "showPermissionPromptOnce"

install_src="$(cat "$ROOT/scripts/warp-screen-layout/install.sh")"
case "$install_src" in
  *"os.setsid"*) echo "not ok - installer detaches with os.setsid before the mover runs"; fail=1 ;;
  *) echo "ok - installer does not setsid before the mover runs" ;;
esac
contains "mover signature is the bundle id" "$install_src" 'designated => identifier "dev.agentic-workflow.warp-screen-layout"'
contains "keys signature is the bundle id" "$install_src" 'designated => identifier "dev.agentic-workflow.warp-screen-layout.keys"'

exit "$fail"
