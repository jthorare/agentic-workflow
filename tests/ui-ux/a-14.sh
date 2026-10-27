#!/usr/bin/env bash
# A-14 (UX pass 2026-10-25): Work 'Closed (1)' shows a collapsed arrow but its row is open, unstyled, with Stage 'Not started'.
# Expected: once the Closed fold is open its arrow points down, its rows lie inside the Work card, and a closed item's
# Stage column does not say 'Not started'.
source "$(dirname "$0")/lib.sh"
# A driver call that hangs must not hold the run: every call here is bounded.
dt() { timeout 120 "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" "$@"; }

uxu_start up items

item="$($SINDRI items | grep -o 'ITM-[A-Za-z0-9]*' | head -1)"
[ -n "$item" ] || uxu_die "the fixture has no item to close"
close_out="$(uxu_fixture close "$item" 2>&1)"; close_rc=$?
# The default item list hides closed items: ask for the done state, and give the views a moment to catch up.
closed=0
for _ in $(seq 1 20); do
  $SINDRI items --state done 2>&1 | grep -q "$item" && { closed=1; break; }
  sleep 1
done
if [ "$closed" != 1 ]; then
  { echo "close exit code: $close_rc"; echo "close output: $close_out"
    echo "items --state done:"; $SINDRI items --state done --json 2>&1; } >&2
  uxu_die "$item is not closed at the CLI"
fi

dt click --type ListBoxItem --name Work >/dev/null || uxu_die "no Work tab"
# Before its first opening the fold reads "Closed" (the count comes with the read), so select it by that name.
dt wait-for --type ToggleButton --name Closed --state visible --wait-ms 30000 >/dev/null || uxu_die "the Closed group never showed"
dt click --type ToggleButton --name Closed >/dev/null || uxu_die "could not open the Closed group"
dt wait-for --name QueueClosedRows --state visible --wait-ms 10000 >/dev/null || uxu_die "the Closed rows never showed"
sleep 2

# $@ = selector args (--name is AutomationProperties.Name first, else x:Name); prints that node's whole subtree as JSON.
subtree() {
  local id
  if [ "$1" = closed-toggle ]; then   # the fold's own header: its name is "Closed" or "Closed (n)"
    id="$(dt search --type ToggleButton --limit 200 | jq -r '[.matches[] | select((.name // "") | startswith("Closed"))][0].id // empty')"
  else
    id="$(dt search "$@" | jq -r '.matches[0].id // empty')"
  fi
  [ -n "$id" ] || { echo "no node matching $*" >&2; return 1; }
  dt tree --node-id "$id" --depth 15
}
export -f subtree dt
export UXU_DRIVER AW_UI_DRIVER_DIR

expect "the open Closed group's arrow points down, not right" \
  bash -c 'out="$(subtree closed-toggle)" || exit 1; echo "$out" | grep -q "▾" && ! echo "$out" | grep -q "▸"'
expect "the Closed rows lie inside the Work card" \
  bash -c 'subtree --type Border --name Work | grep -q "Closed ("'
expect "a closed item's Stage does not say 'Not started'" \
  bash -c 'out="$(subtree --name QueueClosedRows)" || exit 1; echo "$out" | grep -q "Not started" && exit 1; [ -n "$out" ]'

uxu_done
