#!/usr/bin/env bash
# tests/ui-checks/gh-1082b-fold-arrows.sh — copy of gh-1082-fold-arrows.sh (which stays frozen) that picks a group WITH rows: the original
# took the first header by name, "Waiting on you 0", an empty group whose panel is hidden, so its arrow is never in the glyph search.
# Original header follows.
# tests/ui-checks/gh-1082-fold-arrows.sh — frozen regression check for GitHub issue #1082 (UX pass C-11): the Work page's group fold arrows
# never change. Each group header (Queued, Waiting on you, ...) shows a fixed "▾" and the Closed header a fixed "▸", whatever the state.
# Expect: folding a group turns its arrow to "▸" (right = folded), and opening Closed turns its arrow to "▾" (down = open).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh). Every driver call is bounded by `timeout 120`; it starts its own Xvfb and never uses the caller's DISPLAY.
#   bash tests/ui-checks/gh-1082-fold-arrows.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1082B_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX
unset DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1082-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1082-driver-XXXXXX")" || exit 2
OUT="${GH1082B_OUT:-$(mktemp -d "$TMP_ROOT/gh1082-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER" "$@"; }
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1082b-fold-arrows: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
}
fail() {
  drv screenshot --out "$OUT/fail.png" >/dev/null 2>&1 || true
  echo "FAIL: $* (screenshots in $OUT)" >&2
  exit 1
}

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

bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"
bash "$FIXTURE" items >"$OUT/items.log" 2>&1 || setup_fail "sindri-ui-fixture items (see $OUT/items.log)"

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
drv ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# Work page (the top bar's page list is a ListBox of items named by their label).
drv click --type ListBoxItem --name Work >"$OUT/nav.log" 2>&1 || setup_fail "the Work nav item was not clicked"
toggles() { drv search --type ToggleButton --limit 200 2>/dev/null | jq -r '.matches[].name // empty'; }
# The count of arrows of one glyph on the page (the arrows are TextBlocks in the group headers).
arrows() { drv search --type TextBlock --text "$1" --limit 200 2>/dev/null | jq -r '.total // 0'; }

group=""
for _ in $(seq 1 60); do
  group="$(toggles | grep -E '^(Queued|Waiting on you|Running|Working) [1-9]' | head -1)"
  [ -n "$group" ] && break
  sleep 0.5
done
[ -n "$group" ] || setup_fail "the Work page shows no group header (toggles: $(toggles | tr '\n' ',' ))"
# --contains matches shown text, and a ToggleButton's content is a panel, so the Closed header is found by its accessible name.
closed=""
for _ in $(seq 1 120); do
  closed="$(toggles | grep -F Closed | head -1)"
  [ -n "$closed" ] && break
  sleep 0.5
done
if [ -z "$closed" ]; then
  drv search --type ToggleButton --limit 200 >"$OUT/toggles.json" 2>&1
  setup_fail "the Work page shows no Closed header (toggles in $OUT/toggles.json)"
fi
drv screenshot --out "$OUT/before.png" >/dev/null 2>&1

# 1. Folding a group: its arrow turns right (one more right-pointing arrow than before; the Closed header already shows one).
right0="$(arrows '▸')"
down0="$(arrows '▾')"
[ "$down0" -ge 1 ] || setup_fail "no open group arrow (down) on the Work page"
drv click --type ToggleButton --name "$group" >/dev/null 2>&1 || setup_fail "the \"$group\" header click was not accepted"
sleep 1
drv screenshot --out "$OUT/folded.png" >/dev/null 2>&1
right1="$(arrows '▸')"
[ "$right1" -eq $((right0 + 1)) ] \
  || fail "folding \"$group\" left its arrow pointing down (right arrows $right0 -> $right1; expected $((right0 + 1)))"

# 2. Unfolding it turns the arrow down again.
drv click --type ToggleButton --name "$group" >/dev/null 2>&1 || setup_fail "the \"$group\" header second click was not accepted"
sleep 1
[ "$(arrows '▸')" -eq "$right0" ] || fail "unfolding \"$group\" left its arrow pointing right"

# 3. Opening Closed: its arrow turns down (one fewer right-pointing arrow).
drv click --type ToggleButton --name "$closed" >/dev/null 2>&1 || setup_fail "the Closed header click was not accepted"
sleep 1
drv screenshot --out "$OUT/closed-open.png" >/dev/null 2>&1
right2="$(arrows '▸')"
[ "$right2" -eq $((right0 - 1)) ] \
  || fail "opening Closed left its arrow pointing right (right arrows $right0 -> $right2; expected $((right0 - 1)))"

echo "PASS: every fold arrow follows its group's state (screenshots in $OUT)"
