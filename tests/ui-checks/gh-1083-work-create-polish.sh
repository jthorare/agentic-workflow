#!/usr/bin/env bash
# tests/ui-checks/gh-1083-work-create-polish.sh — regression check for #1083 (UX pass C-13, C-14): on Work after `items --large`
# (1) every row's Repo cell shows something (a dash when the item has no repo), (2) a group header has a gap above it, and in the
# Create item form (3) the typed title survives Esc and C, (4) the gap between Title and Repo is a single spacing step.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh).
#   bash tests/ui-checks/gh-1083-work-create-polish.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UX1083_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui; default this checkout's debug build).
set -uo pipefail
unset TMUX DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1083-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1083-driver-XXXXXX")" || exit 2
OUT="${UX1083_OUT:-$(mktemp -d "$TMP_ROOT/gh-1083-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "gh-1083-work-create-polish: setup failed: $* (see $OUT)" >&2; exit 2; }
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

# 1. The control plane with 34 items (none has a repo), then the real app on this checkout's debug build.
bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" items --large >"$OUT/items.log" 2>&1 || setup_fail "sindri-ui-fixture items --large"
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 || setup_fail "the sindri-ui build failed"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
source "$ROOT/tests/ui-checks/xvfb-lib.sh"
xvfb_start || setup_fail "no Xvfb display could be started"
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
drv ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# 2. Work.
nav_ok=0
for _ in $(seq 1 60); do
  if drv click --type ListBoxItem --name Work >"$OUT/nav.log" 2>&1; then nav_ok=1; break; fi
  sleep 1
done
[ "$nav_ok" = 1 ] || setup_fail "the Work nav entry could not be clicked (see $OUT/nav.log)"
heads=""
for _ in $(seq 1 30); do
  heads="$(drv search --type ToggleButton --limit 200 2>/dev/null | jq -c '[.matches[] | select((.name // "") | startswith("Queued"))][0] // empty')"
  [ -n "$heads" ] && break
  sleep 1
done
[ -n "$heads" ] || setup_fail "no Queued group header on Work"
drv screenshot --out "$OUT/work.png" >/dev/null 2>&1

# 3. Assert 1: the Repo cells. The header text "Repo" gives the column's x; every TextBlock under it needs text.
repo_x="$(drv search --type TextBlock --limit 500 2>/dev/null | jq -r '[.matches[] | select(.text == "Repo")][0].x // empty')"
[ -n "$repo_x" ] || setup_fail "no Repo column header"
cells="$(drv search --type TextBlock --limit 500 2>/dev/null | jq -c --argjson x "$repo_x" '[.matches[] | select(.x > ($x - 2) and .x < ($x + 2) and .text != "Repo" and .visible)]')"
empty="$(printf '%s' "$cells" | jq '[.[] | select((.text // "") == "")] | length')"
total="$(printf '%s' "$cells" | jq 'length')"
[ "$total" -gt 0 ] || setup_fail "no Repo cells found under the header"
[ "$empty" -eq 0 ] || fail "$empty of $total Repo cells are empty: no placeholder for an item without a repo (screenshot $OUT/work.png)"

# 4. Assert 2: the gap above the Queued header, from the lowest row-sized Border above it.
head_y="$(printf '%s' "$heads" | jq '.y')"
above="$(drv search --type Border --limit 500 2>/dev/null | jq --argjson y "$head_y" '[.matches[] | select(.visible and .width > 600 and .height > 10 and .height < 90 and (.y + .height) <= ($y + 1)) | (.y + .height)] | max // empty')"
[ -n "$above" ] || setup_fail "no row above the Queued header"
gap="$(jq -n --argjson a "$above" --argjson y "$head_y" '$y - $a')"
jq -n -e --argjson g "$gap" '$g >= 8' >/dev/null || fail "the Queued group header sits $gap px under the group above (screenshot $OUT/work.png)"

# 5. Create item: C opens it, type a title, Esc, C again.
drv key --chord C --node-id 0 >/dev/null 2>&1 || setup_fail "the C chord was not accepted by the window"
title_id=""
for _ in $(seq 1 20); do
  title_id="$(drv search --type TextBox --limit 50 2>/dev/null | jq -r '[.matches[] | select((.name // "") | startswith("Title"))][0].id // empty')"
  [ -n "$title_id" ] && break
  sleep 0.5
done
[ -n "$title_id" ] || setup_fail "the Create item form has no Title field"
drv screenshot --out "$OUT/create-blank.png" >/dev/null 2>&1
# Assert 4 (while the title is still blank): Title field bottom to the Repo label top is one spacing step.
box="$(drv search --type TextBox --limit 50 2>/dev/null | jq -c --arg id "$title_id" '[.matches[] | select(.id == $id)][0]')"
label_y="$(drv search --type TextBlock --limit 500 2>/dev/null | jq -r '[.matches[] | select(.text == "Repo" and .visible)] | map(.y) | max // empty')"
[ -n "$label_y" ] || setup_fail "no Repo label in the Create item form"
form_gap="$(jq -n --argjson b "$box" --argjson l "$label_y" '$l - ($b.y + $b.height)')"
jq -n -e --argjson g "$form_gap" '$g <= 14' >/dev/null || fail "the gap between Title and Repo is $form_gap px in the Create item form (screenshot $OUT/create-blank.png)"

# Assert 3: the draft survives Esc and C.
drv type --node-id "$title_id" --value "Draft kept" >/dev/null 2>&1 || setup_fail "typing into the Title field was not accepted"
drv key --chord Escape >/dev/null 2>&1 || setup_fail "Esc was not accepted in the Create item form"
sleep 1
drv key --chord C --node-id 0 >/dev/null 2>&1 || setup_fail "the C chord was not accepted the second time"
sleep 1
drv screenshot --out "$OUT/create-reopened.png" >/dev/null 2>&1
kept="$(drv search --type TextBox --limit 50 2>/dev/null | jq -r '[.matches[] | select((.name // "") | startswith("Title"))][0].text // empty')"
[ "$kept" = "Draft kept" ] || fail "the reopened Create item form has title '$kept', not the draft 'Draft kept' (screenshot $OUT/create-reopened.png)"

echo "PASS: Repo placeholder, group gap, kept draft and the Title gap all hold (screenshots in $OUT)"
