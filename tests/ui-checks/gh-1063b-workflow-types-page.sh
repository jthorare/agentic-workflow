#!/usr/bin/env bash
# tests/ui-checks/gh-1063b-workflow-types-page.sh — corrected copy of the frozen #1063 check (nodes looked up by name or text, not automation id: the driver reports x:Name as the node name) for GitHub issue #1063 (UX-pass finding A-12):
# the Workflow types tab must be titled 'Workflow types' (not 'Workflows'), its column headers must have room for their words,
# and at a 900 px window the workflow's slug must not run into the Standing column and the Version, Size and Needs columns must stay.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh).
# Every driver call is bounded by `timeout 120`; it starts its own Xvfb (tests/ui-checks/xvfb-lib.sh) and never uses the caller's DISPLAY.
#   bash tests/ui-checks/gh-1063b-workflow-types-page.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1063_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX
unset DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1063-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1063-driver-XXXXXX")" || exit 2
OUT="${GH1063_OUT:-$(mktemp -d "$TMP_ROOT/gh1063-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER" "$@"; }
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1063b-workflow-types-page: setup failed: $* (screenshot in $OUT)" >&2
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

# 1. The control plane (its shipped workflows are the table's rows).
bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"

# 2. The real app, drivable, on the fixture's control plane: this checkout's debug build, unless AW_SINDRI_UI names a binary.
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

# 3. Open the Workflow types tab.
nav_ok=0
for _ in $(seq 1 60); do
  if drv click --type ListBoxItem --name "Workflow types" >"$OUT/nav.log" 2>&1; then nav_ok=1; break; fi
  sleep 1
done
[ "$nav_ok" = 1 ] || setup_fail "no Workflow types entry in the top bar (see $OUT/nav.log)"
drv wait-for --type TextBlock --name RecipeRowVersion --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "no workflow row on the Workflow types page (see $OUT/app.log)"
sleep 1
drv screenshot --out "$OUT/wide.png" >/dev/null 2>&1

# The driver's node name is AutomationProperties.Name, else x:Name, and automationId is empty here: a TextBlock named in XAML is found by --name.
field() { drv search --type TextBlock --name "$1" --limit 1 2>/dev/null | jq -r ".matches[0].$2 // empty"; }
# The header cell's left edge is its column's left edge (the row shares the header's column widths).
header_x() { drv search --type TextBlock --limit 300 2>/dev/null | jq -r --arg t "$1" '[.matches[] | select(.visible and .text == $t)][0].x // empty'; }

# 4. The page is titled like its tab (the panel carries the page's title as its name).
title="$(drv search --type Border --limit 300 2>/dev/null | jq -r '[.matches[] | select(.name == "Workflows" or .name == "Workflow types")][0].name // empty')"
[ "$title" = "Workflow types" ] || fail "the Workflow types page is titled '$title', not 'Workflow types'"

# 5. Each header has room for its words: a header cell narrower than about 7 px a character is trimmed ('Needs and pr...').
bad="$(drv search --type TextBlock --limit 300 2>/dev/null | jq -r '[.matches[] | select(.visible and .y > 0 and (.text // "") != "") | select(.automationId == null or .automationId == "") | select(.text | IN("Workflow","Version","Standing","Size","Needs and produces","Fingerprint","Runs","Finished","Typical time")) | select(.width < ((.text | length) * 7)) | .text] | first // empty')"
[ -z "$bad" ] || fail "the column header '$bad' has no room for its words (clipped)"

# 6. At a 900 px window the table still shows Version, Size and Needs, and the slug stays inside its column.
drv window --width 900 --height 800 >/dev/null 2>&1 || setup_fail "the window could not be resized"
sleep 1.5
drv screenshot --out "$OUT/narrow.png" >/dev/null 2>&1
for id in RecipeRowVersion RecipeRowSteps RecipeRowFlow; do
  seen="$(drv search --type TextBlock --name "$id" --limit 1 2>/dev/null | jq -r '.matches[0].visible // false')"
  [ "$seen" = "true" ] || fail "at 900 px the $id column is gone"
done
slug_right="$(drv search --type TextBlock --name RecipeRowId --limit 1 2>/dev/null | jq -r '.matches[0] | (.x + .width)')"
standing_x="$(header_x Standing)"
[ -n "$slug_right" ] && [ -n "$standing_x" ] || setup_fail "no slug or standing node on the first row"
awk -v a="$slug_right" -v b="$standing_x" 'BEGIN { exit !(a <= b) }' \
  || fail "at 900 px the slug ends at x=$slug_right and runs into the Standing column at x=$standing_x"

echo "PASS: the Workflow types page is titled 'Workflow types', its headers fit, and at 900 px the columns and slugs hold (screenshots in $OUT)"
