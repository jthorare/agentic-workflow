#!/usr/bin/env bash
# tests/ui-checks/ux-b19-failure-state.sh — frozen regression check for the UX-pass finding B-19: a failed service makes the pill read
# Degraded, and the screen must not show it with the red-X (Down) glyph, a raw ISO time ("failed at 2026-10-25T07:06:29Z"), or a Needs-you row
# titled just "broken failed".
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh): `fail` adds a service the build cannot run (named "broken"); its failure row and the Degraded pill show on Home.
#   bash tests/ui-checks/ux-b19-failure-state.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB19_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b19-failure-state-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b19-failure-state-driver-XXXXXX")" || exit 2
OUT="${UXB19_OUT:-$(mktemp -d "$TMP_ROOT/ux-b19-failure-state-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b19-failure-state: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane with a failing service (the scheduler fails it within ~5 s).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" fail >/dev/null 2>&1 || setup_fail "sindri-ui-fixture fail"

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


# 2. Degraded pill, and the failure row on Home.
drv_to /dev/null wait-for --name HealthPillText --state textContains --expect-text Degraded --wait-ms 90000 || setup_fail "the health pill never read Degraded (see $OUT/app.log)"
drv_to /dev/null wait-for --type NeedsYouRow --state visible --wait-ms 60000 || setup_fail "no Needs-you row for the failed service"
sleep 2
shot failure-state

# 3. What the user sees.
fails=()
drv_to "$OUT/glyph.json" props --name PillGlyph || setup_fail "no PillGlyph (the pill's icon) in the tree"
glyph="$(jq -r '.properties.Glyph // empty' "$OUT/glyph.json")"
[ -n "$glyph" ] || setup_fail "the driver gave no Glyph for the pill's icon"
[ "$glyph" != "Down" ] || fails+=("the Degraded pill carries the red-X glyph (Down)")

nodes "$OUT/texts.json" --type TextBlock
iso="$(jq -r '[.matches[] | select(.visible) | .text // empty | select(test("[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}"))][0] // empty' "$OUT/texts.json")"
[ -z "$iso" ] || fails+=("a raw ISO time is on screen: '$iso'")

bare="$(jq -r '[.matches[] | select(.visible) | .text // empty | select(. == "broken failed")][0] // empty' "$OUT/texts.json")"
[ -z "$bare" ] || fails+=("a Needs-you row is titled just '$bare'")

if [ "${#fails[@]}" -gt 0 ]; then
  fail "$(IFS=';'; echo "${fails[*]}") (screenshots in $OUT)"
fi

echo "PASS: the failure state reads as a warning with a human time and a row that names the problem (screenshots in $OUT)"
