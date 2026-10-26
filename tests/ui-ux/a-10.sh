#!/usr/bin/env bash
# A-10 (UX pass 2026-10-25): Shortcuts sheet: the G chord keys are clipped and list letters the description lacks.
# Expected: the Key column is fully visible (the G-chord keycap is not cut at the column edge: it has room beside the
# description or wraps) and matches the text (every letter the key lists is named by the description), and the card
# fits the window (at a 600 px high window its bottom edge is inside the window, not cut until scrolled).
source "$(dirname "$0")/lib.sh"
uxu_start up

d key --chord Shift+OemQuestion >/dev/null || uxu_die "could not send the ? key"
d wait-for --type Border --name "Keyboard shortcuts" --state visible --wait-ms 15000 >/dev/null || uxu_die "the Shortcuts sheet never opened"
sleep 1

# The G-chord row(s): the keycap TextBlocks whose text starts "G then"; their grid is two levels up (grid/Border/TextBlock).
keys="$(d search --type TextBlock --contains "G then" | jq -c '.matches[]')"
[ -n "$keys" ] || uxu_die "no G-chord row in the sheet"

# For each row: the keycap Border and the description TextBlock (the grid's children 0 and 1; the keycap text is Border child 0).
rows="$(printf '%s\n' "$keys" | while read -r k; do
  id="$(jq -r .id <<<"$k")"; grid="${id%/*/*}"
  d tree --node-id "$grid" --depth 2 | jq -c .root
done)"
[ -n "$rows" ] || uxu_die "could not read the G-chord rows"

expect "the G-chord keycap is not clipped at the column edge (room before the description, or it wraps)" \
  bash -c 'jq -e -s "length > 0 and all(.[]; (.children[0].x + .children[0].width < .children[1].x) or (.children[0].children[0].height > 20))"' <<<"$rows"

expect "every letter the G-chord key lists is named by the description (N, D and S are not unexplained)" \
  bash -c '
    keyletters="$(jq -r -s "[.[].children[0].children[0].text | ltrimstr(\"G then \") | split(\" \") | .[]] | unique | .[]" <<<"$1")"
    desc="$(jq -r -s "[.[].children[1].text] | join(\" \")" <<<"$1")"
    single="$(jq -r -s "[.[] | select((.children[0].children[0].text | ltrimstr(\"G then \") | split(\" \") | length) == 1)] | length" <<<"$1")"
    total="$(jq -r -s "length" <<<"$1")"
    # a row per single key describes itself; only a multi-letter key needs every letter named
    [ "$single" = "$total" ] && exit 0
    bad=""
    for l in $keyletters; do
      case "$l" in .) printf "%s" "$desc" | grep -qF ". " || printf "%s" "$desc" | grep -qE "\.\s*[A-Za-z]|\.$" || bad="$bad $l" ;;
        *) printf "%s" "$desc" | grep -qE "(^|[^A-Za-z])$l([^A-Za-z]|$)" || bad="$bad $l" ;; esac
    done
    [ -z "$bad" ] || { echo "letters in the key but not the description:$bad"; exit 1; }' _ "$rows"

d window --width 1280 --height 600 >/dev/null || uxu_die "could not resize the window"
sleep 2
expect "at a 600 px high window the card fits (its bottom edge is inside the window)" \
  bash -c '[ "$("$0" --dir "$1" search --type Border --name "Keyboard shortcuts" | jq "[.matches[] | select(.y >= 0 and (.y + .height) <= 600)] | length")" -ge 1 ]' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"

uxu_done
