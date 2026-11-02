#!/usr/bin/env bash
# tests/ui-checks/gh-1096-filter-zoom.sh — check for #1096 (half 1): filtering the Context map to a term must zoom and pan to the matches.
# Expected: on the 612-node map, searching one feature ('Area 3', 1 of 12) frames it: the zoom stays near or above the whole-map fit and the middle of the canvas
# lands inside that feature's part of the world (its opened column, the first in the world), not on the middle of the world.
# Why not the tabs map: 'context up' loads a 14-item map, and a term there (e.g. 'Pane layout' + its neighbours) is 7 of the 14 items across both columns,
# so the zoom box is most of the map and a correct zoom looks like no zoom (the first version of this check read 'zoomed out' 1.42 -> 1.40 on main).
# So it imports the generated map (sindri-ui-fixture.sh context whole over: 12 features x 50 files; it opens by feature, as tiles) and searches 'Area 3':
# the box takes the 'text' shape, which anchors on a node whose whole label the question uses, and 'Area 3' is the label of exactly one node (feature:area3);
# a file label such as 'File17.ts' is shared by all 12 features, so it would match 12 spread-out nodes. The answer opens that feature as a column of its
# files (the page reads it with area=), so the check waits until the camera has settled.
# The matches' rectangles are not in the element tree (one custom control draws the world); the driver reads the ZoomBorder's View (scale, offset) and
# WorldBounds, which is what this asserts: (1) the zoom didn't drop below 0.9 x the whole-map fit (the opened feature is 51 nodes in a tall column, so
# its fit is close to the whole map's: the first run on main framed it at 0.497 against a whole-map fit of 0.502, filling ~80% of the canvas height, so a
# "zoomed in" rule would fail a correct frame); (2) the world point at the canvas centre is in the first fifth of the world's width (the opened feature's
# column), not near the world's middle, which a re-fit of the whole map would give. The fraction is of the world read before the search: after it,
# WorldBounds can be the shown set only (seen after a main merge: 294 wide instead of 2030), while world coordinates stay the same.
# It drives the REAL sindri-ui. This check only measures: red or green on main is the answer.
#   bash tests/ui-checks/gh-1096-filter-zoom.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init gh-1096-filter-zoom
ctx_start 1280 900

# The generated map as the next version; the page follows it (ux-c1-map-refresh.sh): wait for its size line to change.
before_size="$(ctx_text --type TextBlock --contains "links")"
[ -n "$before_size" ] || setup_fail "no size line before the import"
bash "$FIXTURE" context whole over >"$OUT/whole-over.log" 2>&1 || setup_fail "sindri-ui-fixture context whole over (see $OUT/whole-over.log)"
followed=0
for _ in $(seq 1 40); do
  size="$(ctx_text --type TextBlock --contains "links")"
  if [ -n "$size" ] && [ "$size" != "$before_size" ]; then followed=1; break; fi
  sleep 1
done
[ "$followed" = 1 ] || setup_fail "the page never followed the 612-node map (still '$before_size')"

# settle: the view reads the same twice in a row (at most ~10 s); leaves VIEW_*, WB_* and CANVAS_* set.
settle() {
  local prev="" cur
  for _ in $(seq 1 20); do
    sleep 0.5
    ctx_geometry
    cur="$VIEW_S $VIEW_X $VIEW_Y $WB_W"
    [ "$cur" = "$prev" ] && return 0
    prev="$cur"
  done
}
settle
S0="$VIEW_S"; WB_X0="$WB_X"; WB_W0="$WB_W"   # the whole world, before the search: the filter keeps world coordinates, but WorldBounds may shrink to the shown set
shot before-filter

term="Area 3"
"$DRIVER" type --name "Ask or look up" --value "$term" --replace >/dev/null 2>&1 || setup_fail "could not type into the search box"
"$DRIVER" key --chord Enter --name "Ask or look up" >/dev/null 2>&1 || setup_fail "Enter in the search box was not accepted"
sleep 3   # the feature's files are read before the camera moves
settle
shot after-filter

lt "$(awk -v s0="$S0" 'BEGIN { print s0 * 0.9 }')" "$VIEW_S" || fail "filtering to '$term' (1 feature of 12) zoomed out to $VIEW_S (whole-map fit $S0): the matches are not framed"

read -r cx rel < <(awk -v cw="$CANVAS_W" -v s="$VIEW_S" -v vx="$VIEW_X" -v wx="$WB_X0" -v ww="$WB_W0" \
  'BEGIN { mx = (cw / 2 - vx) / s; printf "%.1f %.3f\n", mx, (mx - wx) / ww }')
lt "$rel" 0.2 || fail "filtering to '$term' put the middle of the canvas at world x=$cx, $(awk -v r="$rel" 'BEGIN { printf "%d", r * 100 }')% across the world: not on the feature's column (the first fifth)"
echo "PASS: filtering to '$term' framed the feature's column: zoom $S0 -> $VIEW_S, canvas middle at world x=$cx ($(awk -v r="$rel" 'BEGIN { printf "%d", r * 100 }')% across)"
