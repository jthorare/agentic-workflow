#!/usr/bin/env bash
# A-16 (UX pass 2026-10-25): a leftover sheet edge tab with a raw instance id stays on every page.
# Expected: after an instance sheet is closed with Esc, its edge tab reads the instance's readable title (not the raw
# address), and the tab goes when the page it belongs to is left (Work, Agents, System).
source "$(dirname "$0")/lib.sh"
uxu_start up recipes workflows

# The strip row's open button is named "<subject>, <recipe>" (AutomationProperties.Name wins over x:Name) and sets no
# automation id, so take the first Button under WorkflowsRows (the row-title button comes before the answer buttons) by node id.
d click --type ListBoxItem --name Home >/dev/null || uxu_die "no Home tab"
row=""
for _ in $(seq 1 40); do
  row="$(d tree --name WorkflowsRows --depth 14 2>/dev/null | jq -r '[.. | objects | select(.type == "Button") | .id][0] // empty')"
  [ -n "$row" ] && break
  sleep 1.5
done
if [ -z "$row" ]; then
  d tree --name WorkflowsPanel --depth 14 2>&1 | jq -r '[.. | objects | select(((.text // "") != "") or ((.name // "") != "")) | "\(.type) name=\(.name) text=\(.text)"] | .[]' >&2
  uxu_die "no workflow row on Home"
fi
d click --node-id "$row" >/dev/null || uxu_die "could not open the instance sheet"

# AutomationProperties.Name wins over x:Name: InstanceTitle is named by its own text and EdgeTab by "Reopen workflow ...", so
# neither is found by --name. The sheet body (InstanceBody) has no automation name, and its first TextBlock is the title;
# the edge tab is the Button named "Reopen ...".
d wait-for --name InstanceBody --state visible --wait-ms 30000 >/dev/null || uxu_die "the instance sheet never showed its body"
sleep 2
title="$(d tree --name InstanceBody --depth 6 | jq -r '[.. | objects | select(.type == "TextBlock" and ((.text // "") != "")) | .text][0] // empty')"
[ -n "$title" ] || uxu_die "the sheet has no readable title"

edge_id() { "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" search --type Button --limit 200 | jq -r '[.matches[] | select((.name // "") | startswith("Reopen"))][0].id // empty'; }
edge_text() { "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" tree --node-id "$(edge_id)" --depth 4 | jq -r '[.. | objects | select(.type == "TextBlock") | .text // empty][0] // empty'; }
edge_shown() { local id; id="$(edge_id)"; [ -n "$id" ] && "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" props --node-id "$id" | jq -e '.properties.IsVisible == "True"' >/dev/null; }
export -f edge_id edge_text edge_shown
export UXU_DRIVER AW_UI_DRIVER_DIR

d key --chord Escape >/dev/null || uxu_die "Esc failed"
for _ in $(seq 1 20); do edge_shown && break; sleep 0.5; done
edge_shown || uxu_die "no edge tab after Esc"

expect "the edge tab reads the readable title ($title)" \
  bash -c '[ "$(edge_text)" = "$0" ]' "$title"
expect "the edge tab text is not a raw address" \
  bash -c 't="$(edge_text)"; [ -n "$t" ] && ! printf %s "$t" | grep -qE "^ITM-|^desk:WFI-|/[0-9]+$"'

for page in Work Agents System; do
  d click --type ListBoxItem --name "$page" >/dev/null || uxu_die "no $page tab"
  sleep 2
  expect "the edge tab is gone on $page" \
    bash -c '! edge_shown'
done

uxu_done
