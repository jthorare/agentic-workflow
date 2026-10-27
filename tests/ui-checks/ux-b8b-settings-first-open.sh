#!/usr/bin/env bash
# tests/ui-checks/ux-b8b-settings-first-open.sh — corrected copy of ux-b8-settings-first-open.sh (frozen original untouched). It read
# the settings page out of `tree --depth 60`, which did not hold the setup steps panel (the driver clamps depth, and the page sits deep in
# the template), so it died in setup with no red. This copy reads every node through `search` (flat, tree order, no depth), with keys
# lower-cased, and finds the panel by its x:Name (the node NAME; automationId is null). Same three assertions. Was: frozen regression check for the UX-pass finding B-8 (ui-ux-pass-2026-10-25):
# Settings, opened on first arrival with changed settings waiting for approval, must (1) not already show a red
# "This changed while you were reading it" before anything changed, (2) show its "Settings" title in the page's viewport
# (not scrolled up under the top bar), and (3) keep every step card's content inside the card at a 765 px window.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh): `drift` makes the live profile differ from its approval, so the profile row asks for approval.
#   bash tests/ui-checks/ux-b8b-settings-first-open.sh  exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB8_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b8-settings-first-open-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b8-settings-first-open-driver-XXXXXX")" || exit 2
OUT="${UXB8_OUT:-$(mktemp -d "$TMP_ROOT/ux-b8-settings-first-open-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b8b-settings-first-open: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane with the live profile drifted from its approval.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" drift >/dev/null 2>&1 || setup_fail "sindri-ui-fixture drift"

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

# Settings opens by the Ctrl+, chord; nothing has focus in a fresh window, so the chord goes to the window (node 0).
open_settings() {
  drv key --chord Ctrl+OemComma --node-id 0 >/dev/null 2>&1 || setup_fail "the Ctrl+, chord was not accepted by the window"
}
# Every node as a flat JSON array (tree order, keys lower-cased), or setup_fail. `search` has no depth limit, unlike `tree`.
tree() {
  drv search --limit 50000 2>/dev/null \
    | jq -c '[.matches[] | with_entries(.key |= ((.[0:1] | ascii_downcase) + .[1:]))]' 2>/dev/null \
    || setup_fail "the driver gave no nodes"
}

# 2. A 765 px window, then Settings; wait for the approval step (the drifted profile row) to be on screen.
drv window --width 765 --height 900 >/dev/null 2>&1 || setup_fail "the window could not be resized"
open_settings
drv wait-for --name SetupStepsPanel --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "no setup steps panel in Settings (see $OUT/app.log)"
sleep 2   # the page settles: the reveal scroll runs after the first layout
drv screenshot --out "$OUT/settings.png" >/dev/null 2>&1
T="$(tree)"

# 3. (1) No red changed-while-reading line before anything changed.
if printf '%s' "$T" | jq -e '[.[] | select(.visible == true) | (.text // "") , (.name // "") | select(contains("changed while you were reading"))] | length > 0' >/dev/null; then
  fail "Settings shows 'This changed while you were reading it' on first open (screenshot in $OUT)"
fi

# (2) The page title is inside the viewport of the page's scroll viewer (the largest one holding the steps panel).
panel="$(printf '%s' "$T" | jq -c '[.[] | select(.name == "SetupStepsPanel")][0] // empty')"
[ -n "$panel" ] || setup_fail "no setup steps panel in the tree"
title="$(printf '%s' "$T" | jq -c --argjson p "$panel" '[.[] | select(.type == "TextBlock" and .text == "Settings" and ((.x - $p.x) | fabs) < 2)][0] // empty')"
[ -n "$title" ] || setup_fail "no Settings title node in the tree"
viewer="$(printf '%s' "$T" | jq -c --argjson p "$panel" '[.[] | select(.type == "ScrollViewer" and .x <= $p.x and (.x + .width) >= ($p.x + $p.width))] | sort_by(-.height)[0] // empty')"
[ -n "$viewer" ] || setup_fail "no scroll viewer around the settings page in the tree"
if ! jq -n -e --argjson t "$title" --argjson v "$viewer" '$t.y >= $v.y - 0.5' >/dev/null; then
  fail "the Settings title is scrolled out of view (title y=$(jq -n --argjson t "$title" '$t.y'), viewport top y=$(jq -n --argjson v "$viewer" '$v.y'); screenshot in $OUT)"
fi

# (3) Nothing inside the steps panel runs past the panel's right edge.
over="$(printf '%s' "$T" | jq -r --argjson p "$panel" '
  [.[] | select(.visible == true and .width > 0 and .x >= $p.x and .y >= $p.y and (.y + .height) <= ($p.y + $p.height + 1) and ((.x + .width) > ($p.x + $p.width + 1)))]
  | map("\(.type) \(.name // .text // "")") | first // empty')"
[ -z "$over" ] || fail "step content runs past the card's right edge at 765 px: $over (screenshot in $OUT)"

echo "PASS: Settings first open is calm, its title is in view and the step cards hold their content (screenshots in $OUT)"
