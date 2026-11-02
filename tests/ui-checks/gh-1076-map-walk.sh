#!/usr/bin/env bash
# tests/ui-checks/gh-1076-map-walk.sh — check for #1076 (UX pass C-3): the Context map's nodes and edges can be reached from the keyboard alone.
# Expected (the issue): a map you can walk with Tab or arrows, Enter opening the item card, Esc closing it. Steps, keys only after the first focus:
#   1. focus the search box ('Ask or look up'), then Tab (at most 25 times) until focus is inside the 'Map items' list;
#   2. Down, then Enter: the node's card opens (the Border named 'Item details', with its title);
#   3. Tab (at most 25 times) until an edge row of that card (a Button under WholeNodeGroups) has focus, then Enter: the card shows the node at the
#      other end of that edge (the title changes) - that is how an edge is reached;
#   4. Esc: the card closes ('Item details' not visible).
# The card and its title are found by their accessible names: the driver's --name is AutomationProperties.Name first and x:Name only without one, and
# the card's controls carry names (the card 'Item details', the title its own text). The title is the card's second TextBlock (the kind is the first).
# The driver has no focus query, so focus is read as IsFocused / IsKeyboardFocusWithin from the props verb. It drives the REAL sindri-ui against a guarded
# fixture home with the Context page's data (context-lib.sh: sindri-ui-fixture.sh up + context up). This check only measures: red or green is the answer.
#   bash tests/ui-checks/gh-1076-map-walk.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init gh-1076-map-walk
ctx_start 1280 900

# prop_true <nodeId> <Property>: the registered property reads True.
# card_title: the open card's title (its second visible TextBlock, after the kind); empty when the card is not open.
card_title() {
  "$DRIVER" tree --name "Item details" --depth 12 2>/dev/null | jq -r '[.. | objects | select(.type == "TextBlock" and .visible == true) | .text // empty] | .[1] // empty'
}
prop_true() { "$DRIVER" props --node-id "$1" 2>/dev/null | jq -e --arg p "$2" '(.properties // {})[$p] | tostring | ascii_downcase == "true"' >/dev/null; }
press() { "$DRIVER" key --chord "$1" >/dev/null 2>&1 || setup_fail "the driver could not press $1"; }

list="$(ctx_node --type ListBox --name "Map items")"
[ -n "$list" ] || fail "no 'Map items' list beside the map: nothing on the map is in the automation tree"
list_id="$(jq -r .id <<<"$list")"

# 1. Tab from the search box into the list.
"$DRIVER" action --name "Ask or look up" --do focus >/dev/null 2>&1 || setup_fail "could not focus the search box"
reached=0
for i in $(seq 1 25); do
  press Tab
  if prop_true "$list_id" IsKeyboardFocusWithin || prop_true "$list_id" IsFocused; then reached="$i"; break; fi
done
[ "$reached" != 0 ] || fail "25 Tabs from the search box never reached the 'Map items' list"
shot list-focused

# 2. Down + Enter opens a node's card.
press Down
press Enter
"$DRIVER" wait-for --name "Item details" --state visible --wait-ms 10000 >/dev/null 2>&1 || fail "Down + Enter in the 'Map items' list did not open the item card"
sleep 1
title1="$(card_title)"
[ -n "$title1" ] || fail "the item card opened from the keyboard shows no title"
shot card-open

# 3. Tab to an edge row of the card; Enter follows the edge.
"$DRIVER" tree --name WholeNodeGroups --depth 30 >"$OUT/groups.json" 2>"$OUT/groups.err" || setup_fail "could not read the card's edge groups ($(head -c 200 "$OUT/groups.err"))"
mapfile -t rows < <(jq -r '.. | objects | select(.type == "Button" and .visible == true) | .id' "$OUT/groups.json")
[ "${#rows[@]}" -gt 0 ] || fail "the card for '$title1' lists no edges (no row under WholeNodeGroups)"
edge=""
for _ in $(seq 1 25); do
  press Tab
  for r in "${rows[@]}"; do
    if prop_true "$r" IsFocused; then edge="$r"; break; fi
  done
  [ -n "$edge" ] && break
done
[ -n "$edge" ] || fail "25 Tabs after the card for '$title1' opened never reached one of its ${#rows[@]} edge rows"
edge_name="$(jq -r --arg id "$edge" 'first(.. | objects | select(.id == $id)) | .name // .text // ""' "$OUT/groups.json")"
press Enter
title2=""
for _ in $(seq 1 20); do
  sleep 0.5
  title2="$(card_title)"
  [ -n "$title2" ] && [ "$title2" != "$title1" ] && break
done
[ -n "$title2" ] && [ "$title2" != "$title1" ] || fail "Enter on the edge row '$edge_name' of '$title1' did not open the node at its other end (title still '$title2')"
shot edge-followed

# 4. Esc closes the card.
press Escape
"$DRIVER" wait-for --name "Item details" --state notVisible --wait-ms 10000 >/dev/null 2>&1 || fail "Esc did not close the item card for '$title2'"
echo "PASS: Tab x$reached reached 'Map items'; Enter opened '$title1'; its edge row '$edge_name' took focus and Enter opened '$title2'; Esc closed the card"
