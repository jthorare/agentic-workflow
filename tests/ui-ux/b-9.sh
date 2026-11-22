#!/usr/bin/env bash
# B-9 (UX pass 2026-10-25): System says 'Everything is running' and Healthy while parts of it are not.
# Expected: the System overview agrees with its own cards and the services: no 'Everything is running.' while the Code
# index card needs a look; the Agents card names its count with the Agents page's word (Working) and counts only the
# working sessions. (The services' frozen next slot is the server's listing, fixed in the API: not checked here.)
# A fresh scratch home needs no look since #3430 (the cards read the server's needsLook flag), so the check makes one: it pauses
# code-index-main-move, then puts a code index at an old commit (the 'context publish stale' fixture). A stale index needs a look
# on the server even while builds are paused, and the pause stops a rebuild from clearing it during the check.
source "$(dirname "$0")/lib.sh"
uxu_start up "sessions up" "context up"
bash "$UXU_ROOT/scripts/sindri-prove.sh" human services pause code-index-main-move </dev/null >/dev/null 2>&1 || uxu_die "could not pause code-index-main-move"
uxu_fixture context publish stale >/dev/null 2>&1 || uxu_die "fixture 'context publish stale' failed"

working="$($SINDRI agents --json | jq '[.sessions[] | select(.state == "running")] | length')"

d click --type ListBoxItem --name System >/dev/null || uxu_die "no System tab"
d wait-for --text "needs a look" --state visible --wait-ms 30000 >/dev/null || uxu_die "the Code index card never said 'needs a look' for the stale index"
sleep 3

expect "the headline does not say everything is running while the Code index needs a look" \
  bash -c '[ "$("$0" --dir "$1" search --text "Everything is running." | jq .total)" = 0 ]' "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"
expect "the Agents card has no 'Running' row" \
  bash -c '[ "$("$0" --dir "$1" search --text Running | jq .total)" = 0 ]' "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"
expect "the Agents card's Working row counts the working sessions ($working)" \
  bash -c 'id="$("$0" --dir "$1" search --text Working | jq -r ".matches[0].id // empty")"; [ -n "$id" ] &&
    [ "$("$0" --dir "$1" tree --node-id "${id%/*}" --depth 2 | jq -r "[.. | objects | select(.text != null) | .text] | .[1]")" = "$2" ]' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR" "$working"

uxu_done
