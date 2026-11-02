#!/usr/bin/env bash
# tests/ui-checks/gh-1072-failure-states.sh — regression check for #1072 (failure and drift states).
# At 1280x800, in the real sindri-ui on the guarded fixture:
#   1. after `fixture drift`, Home's Needs-you list has a profile drift row (guards B-21; passes on main);
#   2. after `fixture drift` again (the profile matches its approval) and `fixture fail` (a service 'broken' the build has no code for),
#      Home has a failure row for it, and the row's title names the problem (the build has no service 'broken');
#   3. the Health page (opened from the top-bar pill, "Degraded ...") lists its problems under ProblemLines with
#      a. no raw ISO time in any line, and
#      b. no ✕ (Down) StateIcon while the health word is Degraded.
# All parts run, every FAIL is printed, and the exit is 1 when any failed.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh).
#   bash tests/ui-checks/gh-1072-failure-states.sh        exit 0 pass; 1 FAIL (stderr lines); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui; default the debug build of this checkout).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1072-failure-states-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1072-failure-states-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/gh-1072-failure-states-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1072-failure-states: setup failed: $* (screenshot in $OUT)" >&2
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

# --contains matches shown text and a Border shows none: find rows and the pill by accessible name, with jq over search output.
# find_name <type> <name needle> <tries of 0.5 s>: prints the first visible match's JSON (id and name), empty when none shows up.
find_name() {
  local m=""
  for _ in $(seq 1 "$3"); do
    m="$("$DRIVER" search --type "$1" --limit 500 2>/dev/null \
      | jq -c --arg n "$2" '[.matches[] | select(.visible and ((.name // "") | contains($n)))][0] // empty')"
    [ -n "$m" ] && break
    sleep 0.5
  done
  printf '%s' "$m"
}

# ---- Part 1: the profile drift row ---------------------------------------------------------------------------------------------
bash "$FIXTURE" drift >"$OUT/fixture-drift.log" 2>&1 || setup_fail "sindri-ui-fixture drift (see $OUT/fixture-drift.log)"
drift_row="$(find_name Border "profile:drift" 120)"
"$DRIVER" screenshot --out "$OUT/part1.png" >/dev/null 2>&1
if [ -z "$drift_row" ]; then
  fail "no profile drift row in Needs you (screenshot in $OUT)"
fi

# ---- Part 2: the failure row names the problem ---------------------------------------------------------------------------------
# The second drift toggles the cadence back, so the profile matches its approval again and only the failure remains.
bash "$FIXTURE" drift >"$OUT/fixture-drift2.log" 2>&1 || setup_fail "sindri-ui-fixture drift, second (see $OUT/fixture-drift2.log)"
bash "$FIXTURE" fail >"$OUT/fixture-fail.log" 2>&1 || setup_fail "sindri-ui-fixture fail (see $OUT/fixture-fail.log)"
fail_row="$(find_name Border "failure:service:broken" 180)"
[ -n "$fail_row" ] || setup_fail "no failure:service:broken row on Home within 90 s"
"$DRIVER" screenshot --out "$OUT/part2.png" >/dev/null 2>&1
# The name is "<tone>, <CHIP>, <title>, <id>": the title is everything between the second ", " and the last one.
row_title="$(printf '%s' "$fail_row" | jq -r '(.name // "") | sub("^[^,]*, [^,]*, "; "") | sub(", failure:service:broken$"; "")')"
if ! printf '%s' "$row_title" | grep -qiE "isn't in this build|not in this build|no service"; then
  fail "the failure row's title doesn't name the problem: '$row_title' (screenshot in $OUT)"
fi

# ---- Part 3: the Health page ---------------------------------------------------------------------------------------------------
pill="$(find_name Button "Degraded" 60)"
pill_id="$(printf '%s' "$pill" | jq -r 'select((.name // "") | startswith("Degraded")) | .id // empty')"
if [ -z "$pill_id" ]; then
  # The first match may be a Button whose name merely contains the word: pick the one that starts with it.
  pill_id="$("$DRIVER" search --type Button --limit 500 2>/dev/null \
    | jq -r '[.matches[] | select(.visible and ((.name // "") | startswith("Degraded"))) | .id][0] // empty')"
fi
[ -n "$pill_id" ] || setup_fail "no top-bar pill whose name starts with 'Degraded'"
"$DRIVER" click --node-id "$pill_id" >/dev/null 2>&1 || setup_fail "the driver would not click the top-bar pill (node $pill_id)"
"$DRIVER" wait-for --type HealthPage --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "the Health page never showed after the pill click"
sleep 1
"$DRIVER" screenshot --out "$OUT/part3.png" >/dev/null 2>&1
"$DRIVER" tree --depth 64 >"$OUT/tree3.json" 2>/dev/null || setup_fail "the driver gave no tree"

lines='[.. | objects | select(.type == "HealthPage" and .visible)][0] | [.. | objects | select(.type == "ItemsControl" and .visible and (.name // "") == "ProblemLines")][0]'
jq -e "$lines"' != null' "$OUT/tree3.json" >/dev/null 2>&1 || setup_fail "no visible ItemsControl named ProblemLines under the HealthPage (see $OUT/tree3.json)"
jq -e "$lines"' | [.. | objects | select(.type == "StateIcon" and .visible)] | length > 0' "$OUT/tree3.json" >/dev/null 2>&1 \
  || setup_fail "ProblemLines has no visible rows (see $OUT/tree3.json)"

# 3a. No raw ISO time in any problem line.
while IFS= read -r txt; do
  [ -n "$txt" ] && fail "the Health page shows a raw time: '$txt' (screenshot in $OUT)"
done < <(jq -r "$lines"' | [.. | objects | select(.type == "TextBlock" and .visible and ((.text // "") | test("[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:")))] | .[].text' "$OUT/tree3.json")

# 3b. While the health word is Degraded, no problem is marked with the Down glyph.
for icon_id in $(jq -r "$lines"' | [.. | objects | select(.type == "StateIcon" and .visible)] | .[].id' "$OUT/tree3.json"); do
  props="$OUT/icon-props-${icon_id//\//-}.json"   # node ids hold slashes
  "$DRIVER" props --node-id "$icon_id" >"$props" 2>/dev/null
  glyph="$(jq -r '.properties.Glyph // empty' "$props")"
  [ -n "$glyph" ] || setup_fail "the driver gave no Glyph property for StateIcon $icon_id (props in $props)"
  if [ "$glyph" = "Down" ]; then
    fail "the Health page marks a degraded problem with the ✕ glyph (StateIcon $icon_id, screenshot in $OUT)"
  fi
done

if [ "$FAILED" = 0 ]; then
  echo "PASS: the drift row shows, the failure row names the problem, and the Health page has no raw time or ✕ for a degraded problem (screenshots in $OUT)"
  exit 0
fi
exit 1
