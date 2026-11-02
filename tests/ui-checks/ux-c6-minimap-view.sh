#!/usr/bin/env bash
# tests/ui-checks/ux-c6-minimap-view.sh — frozen regression check for the UX-pass finding C-6 (the mini-map's view rectangle is bigger than the mini-map).
# Expected: the rectangle the mini-map draws for the visible part of the map lies inside the world it draws (Viewport inside World).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c6-minimap-view.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c6-minimap-view
ctx_start 1920 1080

mini="$(ctx_node --type MiniMapControl)"; [ -n "$mini" ] || setup_fail "no visible mini-map"
view="$(ctx_prop --node-id "$(jq -r .id <<<"$mini")" Viewport)"
world="$(ctx_prop --node-id "$(jq -r .id <<<"$mini")" World)"
mapfile -t v < <(ctx_nums "$view"); mapfile -t w < <(ctx_nums "$world")
[ "${#v[@]}" -eq 4 ] && [ "${#w[@]}" -eq 4 ] || setup_fail "the mini-map's Viewport ('$view') or World ('$world') did not read as a rectangle"
inside="$(awk -v vx="${v[0]}" -v vy="${v[1]}" -v vw="${v[2]}" -v vh="${v[3]}" -v wx="${w[0]}" -v wy="${w[1]}" -v ww="${w[2]}" -v wh="${w[3]}" \
  'BEGIN { t = 1; print (vx >= wx - t && vy >= wy - t && vx + vw <= wx + ww + t && vy + vh <= wy + wh + t) ? 1 : 0 }')"
[ "$inside" = 1 ] || fail "the mini-map's view rectangle ($view) runs outside the world it draws ($world)"
echo "PASS: the mini-map's view rectangle is inside its world ($view in $world)"
