#!/usr/bin/env bash
# tests/ui-checks/ux-a15-map-overlays.sh — frozen regression check for the UX-pass finding A-15 (the minimap and legend cover the map; the map is not fitted at 900).
# Expected: at 900x600 (after the window shrank) the drawn map does not run under the mini-map or the legend.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-a15-map-overlays.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-a15-map-overlays
ctx_start 1280 900
ctx_window 900 600
ctx_geometry

mini="$(ctx_node --type Border --name "Mini-map: arrow keys move the view")"; [ -n "$mini" ] || setup_fail "no mini-map host"
legend="$(ctx_node --type WholeMapLegend)"; [ -n "$legend" ] || setup_fail "no legend"
# "$1" is a name, "$2" a node: fail when the drawn map's rectangle and the node's overlap.
covers() {
  local x y w h
  x="$(jq .x <<<"$2")"; y="$(jq .y <<<"$2")"; w="$(jq .width <<<"$2")"; h="$(jq .height <<<"$2")"
  awk -v l="$WORLD_L" -v t="$WORLD_T" -v r="$WORLD_R" -v b="$WORLD_B" -v x="$x" -v y="$y" -v w="$w" -v h="$h" \
    'BEGIN { exit !(l < x + w && r > x && t < y + h && b > y) }' && fail "at 900x600 the $1 ($x,$y ${w}x$h) sits on the map (it is drawn at $WORLD_L,$WORLD_T to $WORLD_R,$WORLD_B)"
}
covers "mini-map" "$mini"
covers "legend" "$legend"
echo "PASS: at 900x600 the mini-map and the legend are clear of the map"
