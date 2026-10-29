#!/usr/bin/env bash
# tests/ui-checks/gh-1544-pill-search.sh — regression check for #1544 (a long health message in the top-bar pill squeezes the ⌘K search box).
# At 1280x800, in the real sindri-ui on the guarded fixture, after `fixture drift` (the pill reads "Degraded ... the profile changed since approval"):
#   1. the search placeholder (the TextBlock named "Search or run a command") is at least 160 px wide, not squeezed to "Search or run a c…";
#   2. the pill still names the problem in full (its accessible name) and a visible TextBlock named PillProblems shows the (maybe trimmed) text.
# All parts run, every FAIL is printed, and the exit is 1 when any failed.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh).
#   bash tests/ui-checks/gh-1544-pill-search.sh        exit 0 pass; 1 FAIL (stderr lines); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui; default the debug build of this checkout).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1544-pill-search-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1544-pill-search-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/gh-1544-pill-search-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1544-pill-search: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
}
FAILED=0
fail() { echo "FAIL: $*" >&2; FAILED=1; }

cleanup() {
  local app_pid=""
  if [ -f "$AW_UI_DRIVER_DIR/driver.json" ]; then
    app_pid="$(sed -n 's/.*"pid":\([0-9][0-9]*\).*/\1/p' "$AW_UI_DRIVER_DIR/driver.json")"
  fi
  if [ -n "$app_pid" ]; then kill "$app_pid" 2>/dev/null || true; fi
  if [ -n "$APP_WRAPPER_PID" ]; then kill "$APP_WRAPPER_PID" 2>/dev/null || true; fi
  bash "$FIXTURE" down >/dev/null 2>&1 || true
}
trap cleanup EXIT

command -v jq >/dev/null 2>&1 || setup_fail "jq is needed"

# 1. The control plane with an approved profile.
bash "$FIXTURE" up >"$OUT/fixture-up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/fixture-up.log)"

# 2. The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
#    make the build the owner's current one), unless AW_SINDRI_UI names a binary.
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
if [ -z "${DISPLAY:-}" ]; then
  # An Xvfb this check owns and stops by pid on exit (xvfb-run leaves its Xvfb behind when a timeout kills the check).
  source "$ROOT/tests/ui-checks/xvfb-lib.sh"
  xvfb_start || setup_fail "no Xvfb display could be started"
fi
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
"$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
timeout 120 "$DRIVER" window --width 1280 --height 800 >/dev/null 2>&1 || setup_fail "could not size the window"
sleep 2

# 3. A profile drift puts a long problem text in the pill.
bash "$FIXTURE" drift >"$OUT/fixture-drift.log" 2>&1 || setup_fail "sindri-ui-fixture drift (see $OUT/fixture-drift.log)"

# The pill: a visible Button whose accessible name starts with "Degraded" and names the drift (polled up to 60 s).
pill_filter='[.. | objects | select(.type == "Button" and .visible and ((.name // "") | startswith("Degraded")) and ((.name // "") | contains("the profile changed since approval")))][0] // empty'
pill=""
for _ in $(seq 1 120); do
  "$DRIVER" tree --depth 64 >"$OUT/tree-poll.json" 2>/dev/null
  pill="$(jq -c "$pill_filter" "$OUT/tree-poll.json" 2>/dev/null)"
  [ -n "$pill" ] && break
  sleep 0.5
done
[ -n "$pill" ] || setup_fail "the top-bar pill never named 'the profile changed since approval' within 60 s"
sleep 2
"$DRIVER" screenshot --out "$OUT/pill.png" >/dev/null 2>&1
"$DRIVER" tree --depth 64 >"$OUT/tree.json" 2>/dev/null || setup_fail "the driver gave no tree"

# ---- Part 1: the search placeholder is not squeezed -----------------------------------------------------------------------------
# The placeholder TextBlock sits inside the Border of the same name; it needs about 150 px at text scale 1.
width="$(jq -r '[.. | objects | select(.type == "TextBlock" and .visible and (.name // "") == "Search or run a command")][0].width // empty' "$OUT/tree.json")"
[ -n "$width" ] || setup_fail "no visible TextBlock named 'Search or run a command' (see $OUT/tree.json)"
if ! awk -v w="$width" 'BEGIN { exit !(w >= 160) }'; then
  fail "the search placeholder is squeezed to $width px (needs 160) while the pill names a problem (screenshot in $OUT)"
fi

# ---- Part 2: the pill still names the problem ------------------------------------------------------------------------------------
# The Button's accessible name was matched by the wait; the text itself shows in a TextBlock named PillProblems (it may be trimmed).
if ! jq -e '[.. | objects | select(.type == "TextBlock" and .visible and (.name // "") == "PillProblems")] | length > 0' "$OUT/tree.json" >/dev/null 2>&1; then
  fail "the pill no longer shows its problem text (screenshot in $OUT)"
fi

if [ "$FAILED" = 0 ]; then
  echo "PASS: the search placeholder is $width px wide and the pill still shows its problem text (screenshot in $OUT)"
  exit 0
fi
exit 1
