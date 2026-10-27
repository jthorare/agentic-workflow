#!/usr/bin/env bash
# tests/ui-checks/ux-c10b-dialog-focus.sh — frozen regression check for the UX-pass finding C-10 (ui-ux-pass-2026-10-25):
# closing a dialog must give keyboard focus back to the control that opened it, and clicking a page tab must not leave focus
# (with its 2 px ring) on the whole page region. Step 1: Home > Start workflow > Esc, the Start workflow button must be focused.
# Step 2: click the Work tab; the page region (the big focusable Border) must not hold focus.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh). The driver has no focus query, so it reads IsFocused from the `props` verb.
#   bash tests/ui-checks/ux-c10b-dialog-focus.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXC10_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-c10b-dialog-focus-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-c10b-dialog-focus-driver-XXXXXX")" || exit 2
OUT="${UXC10_OUT:-$(mktemp -d "$TMP_ROOT/ux-c10b-dialog-focus-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-c10b-dialog-focus: setup failed: $*" >&2; exit 2; }
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

# Every driver call is bounded: a hang is a broken setup, never a verdict.
drv() { timeout 120 "$DRIVER" "$@"; }

# 1. The control plane (the default fixture).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"

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
drv ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# True when the node at nodeId $1 reports IsFocused (props verb); setup_fail when the driver cannot say.
is_focused() {
  local p
  p="$(drv props --node-id "$1" 2>/dev/null)" || setup_fail "props failed for node $1"
  printf '%s' "$p" | jq -e '(.properties // .root.properties // {}) | .IsFocused == "True" or .IsFocused == true' >/dev/null
}

# 2. Step 1: Home > Start workflow > Esc.
opener=""
for _ in $(seq 1 60); do
  opener="$(drv search --type Button --limit 200 2>/dev/null | jq -r '[.matches[] | select((.name // "") | startswith("Start workflow"))][0].id // empty')"
  [ -n "$opener" ] && break
  sleep 1
done
[ -n "$opener" ] || setup_fail "no Start workflow button on Home (see $OUT/app.log)"
drv click --node-id "$opener" >/dev/null 2>&1 || setup_fail "the Start workflow click was not accepted by the driver"
sleep 2   # the dialog opens and takes focus
drv screenshot --out "$OUT/dialog-open.png" >/dev/null 2>&1
drv key --chord Escape >/dev/null 2>&1 || setup_fail "Esc was not accepted: nothing has focus in the dialog (see $OUT/dialog-open.png)"
sleep 1.5
drv screenshot --out "$OUT/after-esc.png" >/dev/null 2>&1
opener="$(drv search --type Button --limit 200 2>/dev/null | jq -r '[.matches[] | select((.name // "") | startswith("Start workflow"))][0].id // empty')"
[ -n "$opener" ] || setup_fail "the Start workflow button is gone after Esc"
is_focused "$opener" || fail "after Esc the Start workflow button does not have focus (screenshot in $OUT/after-esc.png)"

# 3. Step 2: click the Work tab; no big page region may hold focus.
# b copy: the top-bar tabs are ListBoxItems whose label is their name (text is null), so match by name first, then by text.
pick='[.matches[] | select(.visible) | select(.type == "Button" or .type == "TabItem" or .type == "ListBoxItem" or .type == "RadioButton" or .type == "ToggleButton")][0].id // empty'
tab="$(drv search --name Work --limit 50 2>/dev/null | jq -r "$pick")"
[ -n "$tab" ] || tab="$(drv search --text Work --limit 50 2>/dev/null | jq -r "$pick")"
[ -n "$tab" ] || setup_fail "no Work tab in the top bar"
drv click --node-id "$tab" >/dev/null 2>&1 || setup_fail "the Work tab click was not accepted by the driver"
sleep 1.5
drv screenshot --out "$OUT/after-tab.png" >/dev/null 2>&1
big="$(drv search --type Border --limit 200 2>/dev/null | jq -r '[.matches[] | select(.width > 600 and .height > 300) | .id][]')"
[ -n "$big" ] || setup_fail "no page region found"
for id in $big; do
  if is_focused "$id"; then fail "after clicking the Work tab the page region (node $id) holds focus, ringed (screenshot in $OUT/after-tab.png)"; fi
done

echo "PASS: Esc returned focus to Start workflow and the Work tab left no page region focused (screenshots in $OUT)"
