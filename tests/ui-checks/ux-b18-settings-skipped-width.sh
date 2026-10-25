#!/usr/bin/env bash
# tests/ui-checks/ux-b18-settings-skipped-width.sh — frozen regression check for the UX-pass finding B-18 (ui-ux-pass-2026-10-25):
# on Settings (1) a skipped setup step must not carry the tick of a done one, (2) the page must use a 1280 px window's width
# instead of a 745 px column, and (3) the Text size slider must show its range ends ("90%" and "130%") beside the current value.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh); the fixture's Onyx step is Skipped.
#   bash tests/ui-checks/ux-b18-settings-skipped-width.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB18_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b18-settings-skipped-width-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b18-settings-skipped-width-driver-XXXXXX")" || exit 2
OUT="${UXB18_OUT:-$(mktemp -d "$TMP_ROOT/ux-b18-settings-skipped-width-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b18-settings-skipped-width: setup failed: $*" >&2; exit 2; }
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

# Settings opens by the Ctrl+, chord; nothing has focus in a fresh window, so the chord goes to the window (node 0).
open_settings() {
  drv key --chord Ctrl+OemComma --node-id 0 >/dev/null 2>&1 || setup_fail "the Ctrl+, chord was not accepted by the window"
}
# The whole tree as JSON (depth <= 60), or setup_fail.
tree() { drv tree --depth 60 2>/dev/null || setup_fail "the driver gave no tree"; }

# 2. A 1280 px window, then Settings; wait for the checklist and for a step that says Skipped.
drv window --width 1280 --height 900 >/dev/null 2>&1 || setup_fail "the window could not be resized"
open_settings
drv wait-for --name SetupStepsPanel --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "no setup steps panel in Settings (see $OUT/app.log)"
skipped=0
for _ in $(seq 1 60); do
  T="$(tree)"
  if printf '%s' "$T" | jq -e '[.. | objects | select(.type == "TextBlock" and .text == "Skipped" and .visible == true)] | length > 0' >/dev/null; then skipped=1; break; fi
  sleep 1
done
[ "$skipped" = 1 ] || setup_fail "no step says Skipped in Settings (see $OUT/app.log)"
drv screenshot --out "$OUT/settings.png" >/dev/null 2>&1

# 3. (1) The skipped step's row: its number circle must not show the tick (a done step's mark). A row is the Expander
#    holding the "Skipped" chip; its number circle is the first caption text of the row's header.
tick="$(printf '%s' "$T" | jq -r '
  [.. | objects | select(.type == "Expander" and ([.. | objects | select(.type == "TextBlock" and .text == "Skipped")] | length > 0))]
  | sort_by(.height)[0] // empty
  | [.. | objects | select(.type == "TextBlock" and (.text // "") != "")][0].text // empty')"
[ -n "$tick" ] || setup_fail "no number circle text in the skipped step's row"
[ "$tick" != "✓" ] || fail "the Skipped step shows the same tick as a Done step (screenshot in $OUT)"

# (2) The page uses the window: the steps panel is wider than the old 760 px column.
panel_w="$(printf '%s' "$T" | jq -r '[.. | objects | select(.name == "SetupStepsPanel" or .automationId == "SetupStepsPanel")][0].width // empty')"
[ -n "$panel_w" ] || setup_fail "no setup steps panel in the tree"
jq -n -e --argjson w "$panel_w" '$w > 760' >/dev/null || fail "Settings stays in a ${panel_w}px column on a 1280 px window (screenshot in $OUT)"

# (3) The Text size slider shows both ends of its range.
for end in "90%" "130%"; do
  printf '%s' "$T" | jq -e --arg e "$end" '[.. | objects | select(.type == "TextBlock" and .text == $e and .visible == true)] | length > 0' >/dev/null \
    || fail "the Text size slider shows no '$end' range label (screenshot in $OUT)"
done

echo "PASS: the Skipped step has its own mark, Settings uses the width and the slider shows its range (screenshots in $OUT)"
