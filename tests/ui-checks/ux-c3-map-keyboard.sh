#!/usr/bin/env bash
# tests/ui-checks/ux-c3-map-keyboard.sh — frozen regression check for the UX-pass finding C-3 (map nodes cannot be reached from the keyboard or a screen reader).
# Expected: the map is reachable without a pointer: some element named for a map item (the node 'Tab groups', or an entry of a list of the map's features) is in the automation tree and takes focus.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c3-map-keyboard.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c3-map-keyboard
ctx_start 1280 900

# The nodes are drawn by one custom control with no peers (WholeMapCanvas: AccessibilityView Raw), so the automation tree holds none of them.
hit="$("$DRIVER" tree --depth 80 2>/dev/null | jq -c 'first(.. | objects | select(.type != null and .type != "TextBlock" and (((.name // "") | contains("Tab groups")) or ((.text // "") | contains("Tab groups"))))) // empty')"
[ -n "$hit" ] || fail "no element named for a map item ('Tab groups') is in the automation tree: the map has no keyboard or screen-reader path"
"$DRIVER" action --node-id "$(jq -r .id <<<"$hit")" --do focus >/dev/null 2>&1 \
  || fail "the element named 'Tab groups' ($(jq -r .type <<<"$hit")) cannot take keyboard focus"
echo "PASS: a map item is in the automation tree and takes focus"
