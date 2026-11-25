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

exit "$fail"
