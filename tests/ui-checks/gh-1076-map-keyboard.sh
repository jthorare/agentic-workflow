#!/usr/bin/env bash
# tests/ui-checks/gh-1076-map-keyboard.sh — frozen regression check for the UX-pass finding C-3 (map nodes cannot be reached from the keyboard or a screen reader).
# Supersedes ux-c3-map-keyboard.sh, whose single `tree --depth 80` call hung. This one asks for depth 60 and wraps every driver call in `timeout 120`; a timeout is a setup failure (exit 2), never a FAIL.
# Expected: the map is reachable without a pointer: some element named for a map item (the node 'Tab groups', or an entry of a list of the map's features) is in the automation tree and takes focus.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/gh-1076-map-keyboard.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
source "$(dirname "${BASH_SOURCE[0]}")/xvfb-lib.sh"
# The check starts its own Xvfb (stopped by pid on exit); ctx_start then runs the app on it.
unset DISPLAY

# Every driver call goes through this bound: the lib's own calls too (it runs "$DRIVER"). A call that outlives 120 s is a setup failure.
REAL_DRIVER="$DRIVER"
DRIVER=bounded_driver
CTX_TIMED_OUT=0
bounded_driver() {
  timeout 120 "$REAL_DRIVER" "$@"
  local rc=$?
  if [ "$rc" -eq 124 ]; then
    if [ "$CTX_TIMED_OUT" -eq 0 ]; then
      CTX_TIMED_OUT=1
      setup_fail "driver call timed out: $*"
    fi
    exit 2
  fi
  return "$rc"
}

ctx_init gh-1076-map-keyboard
xvfb_start 2200x1400x24 || setup_fail "no Xvfb display came up"
ctx_start 1280 900

# The nodes are drawn by one custom control with no peers (WholeMapCanvas: AccessibilityView Raw), so the automation tree holds none of them.
# The tree is read into a file, not through a pipe in a subshell, so a timeout stops the check instead of the subshell.
"$DRIVER" tree --depth 60 >"$OUT/tree.json" 2>"$OUT/tree.err"
rc=$?
[ "$rc" -eq 0 ] || setup_fail "driver call failed: tree --depth 60 (exit $rc: $(head -c 200 "$OUT/tree.err"))"
hit="$(jq -c 'first(.. | objects | select(.type != null and .type != "TextBlock" and (((.name // "") | contains("Tab groups")) or ((.text // "") | contains("Tab groups"))))) // empty' "$OUT/tree.json")"
[ -n "$hit" ] || fail "no element named for a map item ('Tab groups') is in the automation tree: the map has no keyboard or screen-reader path"
"$DRIVER" action --node-id "$(jq -r .id <<<"$hit")" --do focus >/dev/null 2>&1 \
  || fail "the element named 'Tab groups' ($(jq -r .type <<<"$hit")) cannot take keyboard focus"
echo "PASS: a map item is in the automation tree and takes focus"
