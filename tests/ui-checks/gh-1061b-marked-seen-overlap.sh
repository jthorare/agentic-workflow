#!/usr/bin/env bash
# tests/ui-checks/gh-1061b-marked-seen-overlap.sh — frozen regression check for issue #1061 (UX finding A-7): after Mark all seen, the
# Needs you header shows the summary line on the left and "N marked seen · Undo" on the right; at a 1280 px window the summary
# ("N need you · ... · N queued") ran under the "N marked seen" text. Expect the summary's right edge to stop before the undo text's
# left edge (and before the Mark all seen button). It drives the REAL sindri-ui (docs/csharp/ui-driver.md) against a guarded fixture home.
#   bash tests/ui-checks/gh-1061b-marked-seen-overlap.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER_SH="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1061-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1061-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/gh1061-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER_SH" "$@"; }
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1061b-marked-seen-overlap: setup failed: $* (screenshot $OUT/setup-fail.png)" >&2
  exit 2
}
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

# 1. The control plane with items and requests (rows in Needs you, so Mark all seen is offered).
bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" items --large >"$OUT/items.log" 2>&1 || setup_fail "sindri-ui-fixture items --large"

# 2. The real app on an Xvfb this check owns, at the width of the report.
drv ready --wait-ms 1 >/dev/null 2>&1 || true
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
source "$ROOT/tests/ui-checks/xvfb-lib.sh"
xvfb_start || setup_fail "no Xvfb display could be started"
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
drv ready --wait-ms 110000 >"$OUT/ready.log" 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
drv window --width 1280 --height 800 >"$OUT/window.log" 2>&1 || setup_fail "the window could not be sized"

# 3. Mark all seen, then read the header's boxes.
mark=(--type Button --name "Mark all seen")
drv wait-for "${mark[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "no visible Mark all seen button on Home"
drv click "${mark[@]}" >"$OUT/mark.log" 2>&1 || setup_fail "Mark all seen was not clicked"
drv wait-for --type TextBlock --name NeedsYouUndoText --state visible --wait-ms 10000 >/dev/null 2>&1 \
  || setup_fail "no marked-seen text after Mark all seen"
sleep 1
drv screenshot --out "$OUT/after-mark.png" >/dev/null 2>&1

# The driver reports x:Name as the node NAME (not the automation id, which is empty here), and NeedsYouSummary's name is its own text
# (AutomationProperties.Name), so the summary is the visible TextBlock whose text is the summary line.
box_by_name() { drv search --type TextBlock --name "$1" --limit 5 2>"$OUT/$1.err" | jq -c '[.matches[] | select((.visible // .Visible) == true)][0] // empty'; }
box_by_text() { drv search --type TextBlock --limit 500 2>"$OUT/summary.err" | jq -c '[.matches[] | select((.visible // .Visible) == true) | select(((.text // .Text) // "") | test("need you · .* queued"))][0] // empty'; }
summary="$(box_by_text)"; undo="$(box_by_name NeedsYouUndoText)"
[ -n "$summary" ] || setup_fail "no visible NeedsYouSummary"
[ -n "$undo" ] || setup_fail "no visible NeedsYouUndoText"
s_right="$(jq -n --argjson b "$summary" '($b.x // $b.X) + ($b.width // $b.Width)')"
u_left="$(jq -n --argjson b "$undo" '($b.x // $b.X)')"
echo "summary right edge $s_right, marked-seen left edge $u_left" >"$OUT/boxes.log"
jq -n -e --argjson r "$s_right" --argjson l "$u_left" '$r <= $l' >/dev/null \
  || fail "the Home summary line ends at x=$s_right, past where the marked-seen text starts (x=$u_left) at 1280 px: they overlap (screenshot in $OUT)"

echo "PASS: the summary line and the marked-seen text do not overlap at 1280 px (screenshots in $OUT)"
