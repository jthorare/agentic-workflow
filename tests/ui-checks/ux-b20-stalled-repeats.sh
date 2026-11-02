#!/usr/bin/env bash
# tests/ui-checks/ux-b20-stalled-repeats.sh — frozen regression check for the UX-pass finding B-20: with the views stalled, Home must say
# "Views are stalled at N: run `sindri views rebuild`" once (the banner), not in the pill, the Needs-you panel and the Running panel too, and
# the Workflows strip rows' texts must lie inside their rows (today they are clipped: "needs your output (desk answe").
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh): `workflows` starts the strip's instances, then `stall` stalls the views.
#   bash tests/ui-checks/ux-b20-stalled-repeats.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB20_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b20-stalled-repeats-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b20-stalled-repeats-driver-XXXXXX")" || exit 2
OUT="${UXB20_OUT:-$(mktemp -d "$TMP_ROOT/ux-b20-stalled-repeats-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b20-stalled-repeats: setup failed: $*" >&2; exit 2; }
fail() { echo "FAIL: $*" >&2; exit 1; }

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

# 1. The control plane with the strip's instances (started before the stall: a stalled control plane refuses commands), then stalled views.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" workflows >/dev/null 2>&1 || setup_fail "sindri-ui-fixture workflows"

# The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
# make the build the owner's current one), unless AW_SINDRI_UI names a binary.
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
if [ -n "${DISPLAY:-}" ]; then
  bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
else
  xvfb-run -a bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
fi
APP_WRAPPER_PID=$!
timeout 130 "$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# ---- helpers: every driver call runs under timeout 120; a timeout is a setup failure, never a FAIL ----
drv_to() {  # drv_to <outfile> <verb and flags...>
  local out="$1"; shift
  timeout 120 "$DRIVER" "$@" >"$out" 2>>"$OUT/driver.err"
  local rc=$?
  if [ "$rc" = 124 ]; then setup_fail "driver call timed out: $*"; fi
  return "$rc"
}
nodes() {  # nodes <outfile> <search flags...>: the matches of a search, to a file
  local f="$1"; shift
  drv_to "$f" search --limit 500 "$@" || setup_fail "driver search failed: $*"
}
visible_n() { jq "[.matches[] | select(.visible)] | length" "$1"; }
first_text() { jq -r "[.matches[] | select(.visible)][0].text // empty" "$1"; }
poll() {  # poll <seconds> <command...>: true as soon as the command succeeds
  local end=$((SECONDS + $1)); shift
  while :; do
    if "$@"; then return 0; fi
    if [ "$SECONDS" -ge "$end" ]; then return 1; fi
    sleep 0.5
  done
}
shot() { drv_to /dev/null screenshot --out "$OUT/$1.png" || true; }


# 2. The strip shows its rows; then the views stall.
drv_to /dev/null wait-for --type StripRow --state visible --wait-ms 90000 || setup_fail "no Workflows strip row on Home (see $OUT/app.log)"
bash "$FIXTURE" stall >/dev/null 2>&1 || setup_fail "sindri-ui-fixture stall"
stalled() { nodes "$OUT/stalled.json" --type TextBlock --contains "Views are stalled"; [ "$(visible_n "$OUT/stalled.json")" -ge 1 ]; }
poll 90 stalled || setup_fail "no stalled banner on Home"
sleep 8   # every panel has taken the state
shot stalled

# 3. What the user sees.
fails=()
nodes "$OUT/stalled.json" --type TextBlock --contains "Views are stalled"
times="$(visible_n "$OUT/stalled.json")"
[ "$times" -le 1 ] || fails+=("the stalled sentence is on screen $times times, not once")

nodes "$OUT/strip.json" --type StripRow
nodes "$OUT/texts.json" --type TextBlock
# a text inside a strip row (its node id starts with the row's id and a slash) must not stick out of the row's right edge
clipped="$(jq -rn --slurpfile rows "$OUT/strip.json" --slurpfile texts "$OUT/texts.json" '
  [ $rows[0].matches[] | select(.visible) ] as $r
  | [ $texts[0].matches[] | select(.visible) | . as $t
      | $r[] | . as $row
      | select(($t.id | startswith($row.id + "/")) and ((($t.x // $t.X) + ($t.width // $t.Width)) > (($row.x // $row.X) + ($row.width // $row.Width) + 0.5)))
      | $t.text ]
  | first // empty')"
[ -z "$clipped" ] || fails+=("a Workflows strip row text sticks out of its row: '$clipped'")

if [ "${#fails[@]}" -gt 0 ]; then
  fail "$(IFS=';'; echo "${fails[*]}") (screenshots in $OUT)"
fi

echo "PASS: the stalled sentence shows once and the strip rows hold their text (screenshots in $OUT)"
