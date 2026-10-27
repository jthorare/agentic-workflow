#!/usr/bin/env bash
# tests/ui-checks/ux-c7-layout-fit.sh — frozen regression check for the UX-pass finding C-7 (the Context layout does not fit its window).
# Expected: after the window is resized the stage starts under the controls (no dead band) and fills the window, the map is centred at 1920x1080, and at 900x600 the whole map is on the canvas.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c7-layout-fit.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c7-layout-fit
ctx_start 1280 900
files_bottom() { ctx_node --type RadioButton --name Files | jq '.y + .height'; }
gap_at_1280="$(awk -v c="$(ctx_node --type ZoomBorder | jq .y)" -v f="$(files_bottom)" 'BEGIN { print c - f }')"

# 1920x1080, resized after the page opened (a window is resized by its owner as often as it is opened at a size).
ctx_window 1920 1080
canvas="$(ctx_node --type ZoomBorder)"; [ -n "$canvas" ] || setup_fail "no canvas at 1920x1080"
gap="$(awk -v c="$(jq .y <<<"$canvas")" -v f="$(files_bottom)" 'BEGIN { print c - f }')"
lt "$(awk -v g="$gap" -v b="$gap_at_1280" 'BEGIN { print g - b }')" 12 || fail "at 1920x1080 the map starts ${gap} px under the controls (${gap_at_1280} px at 1280x900): a dead band"
bottom="$(jq '.y + .height' <<<"$canvas")"
lt 1020 "$bottom" || fail "at 1920x1080 the canvas ends at y=$bottom, more than 60 px short of the window's bottom (1080)"
ctx_geometry
off="$(awk -v l="$WORLD_L" -v r="$WORLD_R" -v cx="$CANVAS_X" -v cw="$CANVAS_W" 'BEGIN { d = (l + r) / 2 - (cx + cw / 2); print (d < 0 ? -d : d) }')"
lt "$off" "$(awk -v cw="$CANVAS_W" 'BEGIN { print cw * 0.1 }')" || fail "at 1920x1080 the map is $off px off the middle of the canvas (it was fitted at the old size)"

# 900x600: the whole map is on the canvas.
ctx_window 900 600
ctx_geometry
lt "$WORLD_R" "$(awk -v cx="$CANVAS_X" -v cw="$CANVAS_W" 'BEGIN { print cx + cw + 1 }')" || fail "at 900x600 the map runs to x=$WORLD_R but the canvas ends at $(awk -v cx="$CANVAS_X" -v cw="$CANVAS_W" 'BEGIN { print cx + cw }') (not fitted)"
echo "PASS: the stage fills the window, the map is centred at 1920x1080 and fitted at 900x600"
