#!/usr/bin/env bash
# tests/ui-checks/gh-1100c-sheet-reopen.sh — corrected copy of gh-1100b (it looked the instance title up as `--name InstanceTitle`, but the
# TextBlock sets AutomationProperties.Name to the item's title, so the driver names it "Fixture incident"; this copy looks it up by that name
# and the sheet's scroll viewer). gh-1100b was itself a corrected copy of the frozen #1100 check (it registers and approves recipes/dev/incident-hold.yaml first: the control plane embeds only recipes/sindri, so `incident start` was refused with SND-RECIPE-002) for #1100 (UX-pass finding B-17).
# At 900 wide a sheet covers the page. (1) A sheet closed after it was scrolled down and reopened must start at the top: the
# sheet host's scroll viewers keep their offset. (2) The sheet's title takes focus when the sheet opens, and it must not look
# like a focused text box: the default focus ring is a 2 px box drawn all round the control, so no ring pixels may sit above
# the title's top edge (an underline may be drawn below it).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh
# through scripts/sindri-prove.sh), on its own Xvfb.
#   bash tests/ui-checks/gh-1100c-sheet-reopen.sh      exit 0 pass; 1 FAIL (a line each on stderr); 2 the setup broke
# Env: GH1100_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui; default this checkout's debug build).
set -uo pipefail
unset TMUX
unset DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1100-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1100-driver-XXXXXX")" || exit 2
OUT="${GH1100_OUT:-$(mktemp -d "$TMP_ROOT/gh1100-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER" "$@"; }
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1100b-sheet-reopen: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
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
command -v python3 >/dev/null 2>&1 || setup_fail "python3 is needed"

nodes() { # a search answer ({"matches":[...]}, never empty)
  local out
  out="$(timeout 120 "$DRIVER" search "$@" --limit 1000 2>/dev/null)"
  [ -n "$out" ] || out='{"matches":[]}'
  printf '%s' "$out"
}

# The instance title (InstanceFrameView's InstanceTitle) is named by AutomationProperties.Name, which is the item's title. Only the
# match that is visible and inside the sheet's scroll viewer's columns counts; anything else is a setup failure with the matches saved.
INCIDENT_TITLE="Fixture incident"
title_node() {
  local view m
  view="$(nodes --name InstanceSheetScroll | jq -c '[.matches[] | select(.visible)][0] // empty')"
  [ -n "$view" ] || return 1
  m="$(nodes --type TextBlock --name "$INCIDENT_TITLE" | jq -c --argjson v "$view" \
    '[.matches[] | select(.visible and .x >= $v.x and .x < $v.x + $v.width)]')"
  [ "$(jq length <<<"$m")" = 1 ] || { printf '%s\n' "$m" >"$OUT/title-matches.json"; return 1; }
  jq -c '.[0]' <<<"$m"
}
wait_title() { local i; for i in $(seq 1 "$1"); do title_node >/dev/null && return 0; sleep 1; done; return 1; }

# 1. The control plane with an incident run (its instance sheet is long enough to scroll in a short window).
bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"
# recipes/dev is not embedded in the control plane: register and approve the incident recipe the way the fixture's agent-run does.
recipe_file="$ROOT/recipes/dev/incident-hold.yaml"
recipe_version="$(sed -n 's/^version: *//p' "$recipe_file" | head -1)"
printf 'approve workflow.dev-incident-hold %s\n' "$recipe_version" | bash "$ROOT/scripts/sindri-prove.sh" human recipe approve "$recipe_file" >"$OUT/recipe-approve.log" 2>&1 \
  || setup_fail "the incident recipe was not approved (see $OUT/recipe-approve.log)"
bash "$FIXTURE" incident start >"$OUT/incident.log" 2>&1 || setup_fail "sindri-ui-fixture incident start (see $OUT/incident.log)"

# 2. The real app, drivable, on its own display: this checkout's debug build unless AW_SINDRI_UI names a binary.
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
drv window --width 900 --height 400 >/dev/null 2>&1 || setup_fail "the window could not be resized"

# 3. Workflows -> List -> the incident run's row opens its instance sheet (covers the page at 900 wide).
drv click --type ListBoxItem --name Workflows >/dev/null 2>&1 || setup_fail "the Workflows page could not be opened"
drv wait-for --type ListBoxItem --name List --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "the Workflows page never drew its view switch"
drv click --type ListBoxItem --name List >/dev/null 2>&1 || setup_fail "the List view could not be chosen"
open=""
for _ in $(seq 1 60); do
  open="$(nodes --type Button | jq -r '[.matches[] | select(.visible) | .name // empty | select(startswith("Open ") and test("incident"; "i"))][0] // empty')"
  [ -n "$open" ] && break
  sleep 1
done
[ -n "$open" ] || setup_fail "no Open button for the incident run in the Workflows list (see $OUT/app.log)"
drv click --type Button --name "$open" >/dev/null 2>&1 || setup_fail "the click on \"$open\" was not accepted"
wait_title 60 || setup_fail "the instance sheet never showed its title \"$INCIDENT_TITLE\" (matches in $OUT/title-matches.json, see $OUT/app.log)"
sleep 1

# The sheet is scrolled to the top when the instance title (its first line) is not above the scroll viewer's top edge.
scrolled_down() {
  local view_y title_y
  view_y="$(nodes --name InstanceSheetScroll | jq -r '.matches[0].y // empty')"
  title_y="$(title_node | jq -r '.y // empty')"
  [ -n "$view_y" ] && [ -n "$title_y" ] || setup_fail "the sheet's scroll viewer or title is not in the tree"
  awk -v t="$title_y" -v v="$view_y" 'BEGIN { exit !(t < v - 1) }'
}

# 4. Scroll the sheet to the bottom (the setup must really scroll), then close it and reopen it by its edge tab.
drv scroll --dx 0 --dy 5000 --name InstanceSheetScroll >/dev/null 2>&1 || setup_fail "the sheet could not be scrolled"
sleep 0.5
scrolled_down || setup_fail "the sheet did not scroll in a 900x400 window: nothing to reset (screenshot in $OUT)"
drv click --type Button --name "Close sheet" >/dev/null 2>&1 || setup_fail "the sheet's Close was not accepted"
reopen=""
for _ in $(seq 1 20); do
  reopen="$(nodes --type Button | jq -r '[.matches[] | select(.visible) | .name // empty | select(startswith("Reopen"))][0] // empty')"
  [ -n "$reopen" ] && break
  sleep 0.5
done
[ -n "$reopen" ] || setup_fail "no edge tab to reopen the sheet by"
drv click --type Button --name "$reopen" >/dev/null 2>&1 || setup_fail "the click on \"$reopen\" was not accepted"
wait_title 30 || setup_fail "the sheet did not reopen (matches in $OUT/title-matches.json)"
sleep 1
drv screenshot --out "$OUT/reopened.png" >/dev/null 2>&1

reasons=()
# (a) The reopened sheet starts at the top.
if scrolled_down; then
  reasons+=("the reopened sheet starts scrolled to its old position (its first line is above the top of the view)")
fi

# (b) The title, focused as the sheet opened, draws no box ring above its top edge.
title="$(nodes --name SheetSubjectText | jq -c '.matches[0] // empty')"
[ -n "$title" ] || setup_fail "the sheet's title is not in the tree"
ring="$(python3 -I - "$OUT/reopened.png" "$title" <<'PY'
import json, sys
from PIL import Image
img = Image.open(sys.argv[1]).convert("RGB")
t = json.loads(sys.argv[2])
x0, x1 = int(t["x"]), int(t["x"] + t["width"])
y = int(t["y"])
want = (0x82, 0x8F, 0xFF)  # Color.PrimaryHover, the focus ring
n = 0
for yy in range(max(0, y - 5), y):
    for xx in range(max(0, x0), min(img.width, x1)):
        p = img.getpixel((xx, yy))
        if sum(abs(p[i] - want[i]) for i in range(3)) <= 40:
            n += 1
print(n)
PY
)" || setup_fail "the screenshot could not be read"
if [ "${ring:-0}" -ge 10 ]; then
  reasons+=("the sheet's title is drawn with a box focus ring ($ring ring pixels above its top edge), like a focused text box")
fi

if [ "${#reasons[@]}" -gt 0 ]; then
  echo "FAIL: ${reasons[0]}" >&2
  for r in "${reasons[@]:1}"; do echo "FAIL: $r" >&2; done
  echo "(screenshot in $OUT)" >&2
  exit 1
fi
echo "PASS: the reopened sheet starts at the top and its title draws no box ring (screenshot in $OUT)"
