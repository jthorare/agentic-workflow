#!/usr/bin/env bash
# tests/ui-checks/ux-b6-rebuild-stalled.sh — frozen regression check for the UX-pass finding B-6: when "Rebuild views" cannot finish (the views
# stall again at the same bad event), Home must say the views are stalled and nowhere claim "Rebuilding views (cursor N of M)...".
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh): `stall` makes the projection throw, the check clicks the banner's "Rebuild views", waits until the rebuild was
# recorded (views.rebuilt by human:ui) and the views are stalled again, and reads the screen.
#   bash tests/ui-checks/ux-b6-rebuild-stalled.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB6_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b6-rebuild-stalled-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b6-rebuild-stalled-driver-XXXXXX")" || exit 2
OUT="${UXB6_OUT:-$(mktemp -d "$TMP_ROOT/ux-b6-rebuild-stalled-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b6-rebuild-stalled: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane with stalled views (the projection throws on a row it cannot read).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" stall >/dev/null 2>&1 || setup_fail "sindri-ui-fixture stall"

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


# 2. The stalled banner is up, with its Rebuild views control.
stalled_banner() { nodes "$OUT/banner.json" --type TextBlock --contains "Views are stalled"; [ "$(visible_n "$OUT/banner.json")" -ge 1 ]; }
poll 90 stalled_banner || setup_fail "no stalled banner on Home (see $OUT/app.log)"
drv_to /dev/null wait-for --name "Rebuild views" --state visible --wait-ms 30000 || setup_fail "no Rebuild views control on the banner"
shot before-rebuild

# 3. Rebuild views: the click files the command (the ledger records views.rebuilt by the UI) and the rebuild stalls again at the same event.
drv_to /dev/null click --name "Rebuild views" || setup_fail "the Rebuild views click was not accepted by the driver"
rebuilt() { "$PROVE" human ledger --json --type views.rebuilt --max 1000 2>&1 | tr -d '\r' | grep -q 'human:ui'; }
poll 20 rebuilt || setup_fail "no views.rebuilt by human:ui in the ledger after the click"
poll 60 stalled_banner || setup_fail "the stalled banner did not come back after the rebuild"
sleep 12   # the report: it stayed "Rebuilding views" for good; give every panel its chance to re-read
shot after-rebuild

# 4. What the user sees: the views are stalled, so no element may say they are rebuilding.
nodes "$OUT/rebuilding.json" --type TextBlock --contains "Rebuilding views"
stalled_banner || setup_fail "the stalled banner is gone after the rebuild (the rebuild may have finished)"
if [ "$(visible_n "$OUT/rebuilding.json")" -ge 1 ]; then
  fail "the views are stalled but '$(first_text "$OUT/rebuilding.json")' is on screen (screenshots in $OUT)"
fi

echo "PASS: a rebuild that cannot finish shows the stalled state, not a rebuild in progress (screenshots in $OUT)"
