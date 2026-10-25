#!/usr/bin/env bash
# tests/ui-checks/gh-1096-filter-zoom.sh — frozen regression check for #1096 (filtering the Context map does not zoom to the matches).
# Expected: after the whole map is filtered to a term, the visible viewport frames everything the filter keeps (the matches and their
# links: the canvas's WorldBounds, which the camera is meant to fit), and that box is smaller than the whole world, so a filter happened.
# Half 2 of #1096 (every node's y inside its band) cannot be read through the driver: the world is one custom control and no node
# rectangle is in the element tree. It is a unit test on the pure layout (WholeMapLayout.Place / PlaceMixed and WholeMapFilter) on the fix branch.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole under` for the tabs map). Every driver call has `timeout 120`; a timeout is a setup failure.
#   bash tests/ui-checks/gh-1096-filter-zoom.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init gh-1096-filter-zoom

# Every driver call: timeout 120. context-lib runs "$DRIVER" ..., so DRIVER names this function.
REAL_DRIVER="$DRIVER"
bounded_driver() { timeout 120 "$REAL_DRIVER" "$@"; }
DRIVER=bounded_driver
d() {
  "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}

bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" context whole under >"$OUT/whole-up.log" 2>&1 || setup_fail "sindri-ui-fixture context whole under (see $OUT/whole-up.log)"
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$CTX_ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
d ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
d window --width 1400 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
d click --type ListBoxItem --name Context >/dev/null 2>&1 || setup_fail "no Context tab"
d wait-for --type TextBlock --contains "links" --state visible --wait-ms 60000 >/dev/null 2>&1 || setup_fail "the whole map never showed its size line"
sleep 1

# The whole map, fitted: the world the camera fits now.
ctx_geometry
whole_area="$(awk -v w="$WB_W" -v h="$WB_H" 'BEGIN { print w * h }')"

# Filter the map to a term.
d type --type TextBox --name "Ask or look up" --value "tab" --replace >/dev/null 2>&1 || setup_fail "could not type into the search box"
d key --chord Enter --type TextBox --name "Ask or look up" >/dev/null 2>&1 || setup_fail "could not press Enter in the search box"
d wait-for --type ListBox --name Matches --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "no hits list after the search (the filter never ran)"
sleep 3   # the camera's move is animated

ctx_geometry
filtered_area="$(awk -v w="$WB_W" -v h="$WB_H" 'BEGIN { print w * h }')"
lt "$filtered_area" "$whole_area" || setup_fail "the filter did not narrow the map (box $filtered_area, whole $whole_area): nothing to frame"

# The viewport frames the kept set: its rectangle on screen lies inside the canvas (2 px of slack).
read -r cr cb < <(awk -v x="$CANVAS_X" -v w="$CANVAS_W" -v y="$CANVAS_Y" -v h="$CANVAS_H" 'BEGIN { print x + w + 2, y + h + 2 }')
lt "$(awk -v x="$CANVAS_X" 'BEGIN { print x - 2 }')" "$WORLD_L" || fail "after the filter the matches' box starts at x=$WORLD_L, left of the canvas (x=$CANVAS_X): not framed"
lt "$(awk -v y="$CANVAS_Y" 'BEGIN { print y - 2 }')" "$WORLD_T" || fail "after the filter the matches' box starts at y=$WORLD_T, above the canvas (y=$CANVAS_Y): not framed"
lt "$WORLD_R" "$cr" || fail "after the filter the matches' box ends at x=$WORLD_R, right of the canvas (x=$cr): not framed (scale $VIEW_S)"
lt "$WORLD_B" "$cb" || fail "after the filter the matches' box ends at y=$WORLD_B, under the canvas (y=$cb): not framed (scale $VIEW_S)"
echo "PASS: after the filter the viewport (scale $VIEW_S) frames the matches' box $WORLD_L,$WORLD_T to $WORLD_R,$WORLD_B"
