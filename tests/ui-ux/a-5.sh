#!/usr/bin/env bash
# A-5 (UX pass 2026-10-25): Workflows (default Board) looks empty and its counts say 0 while the header says N open.
# Expected: on the page's default view, read from VISIBLE elements only, the "All" chip equals the header's open count,
# the "Needs you" chip is not 0 (every seeded run waits on the human), no "Nothing here." shows, and the three seeded
# workflows (Fixture triage, Fixture work one, Fixture work two) are drawn as cards on the board.
source "$(dirname "$0")/lib.sh"
uxu_start up items recipes workflows

d click --type ListBoxItem --name Workflows >/dev/null || uxu_die "no Workflows tab"
# The page's own header, "N open · M need you · K with you": the " open ·" with the dot is neither the hidden strip's "N open" nor the key hint.
d wait-for --contains " open ·" --state visible --wait-ms 60000 >/dev/null || uxu_die "the Workflows header never showed its open count"
sleep 3

# Visible TextBlock texts on the page, one per line.
vtexts() { "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" search --type TextBlock --limit 1000 | jq -r '.matches[] | select(.visible == true) | .text // empty'; }
export -f vtexts
export UXU_DRIVER AW_UI_DRIVER_DIR

open="$(vtexts | sed -nE 's/^([0-9]+) open ·.*/\1/p' | head -1)"
[ -n "$open" ] && [ "$open" -gt 0 ] || uxu_die "the visible header shows no open runs (got '${open:-none}')"

expect "the 'All' chip counts the header's $open open runs" \
  bash -c 'vtexts | grep -qxE "All $0"' "$open"
expect "the 'Needs you' chip is not 0 while every run needs you" \
  bash -c 'vtexts | grep -qE "^Needs you [1-9]"'
expect "the page does not say 'Nothing here.' while $open runs are open" \
  bash -c '! vtexts | grep -qxF "Nothing here."'
for run in "Fixture triage" "Fixture work one" "Fixture work two"; do
  expect "the board draws a card for '$run'" \
    bash -c 'vtexts | grep -qF "$0"' "$run"
done

uxu_done
