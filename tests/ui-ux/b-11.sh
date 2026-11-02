#!/usr/bin/env bash
# B-11 (UX pass 2026-10-25): the Agent sheet shows almost nothing until Details is opened, and mixes UTC with local time.
# Expected: the sheet is titled with the agent's name (not the raw ref) and shows its key facts (when it started) before Details is
# opened; times are local like the rest of the app (no " UTC"); no repeated Host label, no dead Focus button; the row's
# highlight clears when the sheet closes and focus has left the list; the Agents grid never leaves the doing cell empty
# and its "Where it runs" header fits its column.
source "$(dirname "$0")/lib.sh"
uxu_start up "sessions up"

# Every driver call is bounded (lib.sh's d() is not).
d() { timeout 120 "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" "$@"; }


d click --type ListBoxItem --name Agents >/dev/null || uxu_die "no Agents tab"
d wait-for --type Button --name "Claude Code, api work, working, Warp" --state visible --wait-ms 30000 >/dev/null || uxu_die "no 'api work' row"
sleep 2
row="$(d search --type Button --limit 200 | jq -r '[.matches[] | select((.name // "") | contains("api work"))][0].name // empty')"
[ -n "$row" ] || uxu_die "the 'api work' row has no name"

# Page list, before the sheet opens.
expect "every visible row's doing cell shows something (a fallback word when it neither asks nor runs a step)" \
  bash -c 'timeout 120 "$0" --dir "$1" tree --name AgentGroups --depth 20 | jq -e "
    [.. | objects | select(.automationId == \"SessionRowGrid\" or .name == \"SessionRowGrid\") | select(.visible and .width > 0)
     | .children[4] | [.. | objects | select(.visible and ((.text // \"\") != \"\"))] | length]
    | (length > 0) and all(. > 0)"' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"
expect "the 'Where it runs' header fits its column (wide enough, or wrapped)" \
  bash -c 'id="$(timeout 120 "$0" --dir "$1" search --text "Where it runs" | jq -r ".matches[0].id // empty")"; [ -n "$id" ] &&
    { [ "$(timeout 120 "$0" --dir "$1" search --node-id "$id" | jq ".matches[0].width >= 120")" = true ] ||
      timeout 120 "$0" --dir "$1" props --node-id "$id" | grep -E "TextWrapping.*Wrap" >/dev/null; }' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"

d click --type Button --name "$row" >/dev/null || uxu_die "could not open the row '$row'"
d wait-for --name SheetSubjectText --state visible --wait-ms 15000 >/dev/null || uxu_die "the sheet never opened"
d wait-for --name SessionBody --state visible --wait-ms 15000 >/dev/null || uxu_die "the sheet never loaded"
sleep 2

expect "the sheet title is the agent's name, not the raw reference" \
  bash -c 't="$(timeout 120 "$0" --dir "$1" search --name SheetSubjectText | jq -r ".matches[0].text // \"\"")"; [ -n "$t" ] && ! printf %s "$t" | grep -qE "^[a-z-]+/[0-9a-f-]{8}"' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"
expect "when it started is visible in the sheet before Details is opened" \
  bash -c 'timeout 120 "$0" --dir "$1" tree --name SessionBody --depth 20 | jq -e --arg r "$2" "
    [.. | objects | select(.visible and (((.text // \"\") | contains(\"Started\")) or ((.name // \"\") | contains(\"Started\"))))] | length > 0"' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"
expect "no Focus row (a disabled Coming-soon button) in the sheet" \
  bash -c '[ "$(timeout 120 "$0" --dir "$1" search --name SessionFocusRow | jq "[.matches[] | select(.visible)] | length")" = 0 ]' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"

exp="$(d tree --name SessionBody --depth 20 | jq -r '[.. | objects | select(.type == "Expander")][0].id // empty')"
[ -n "$exp" ] || uxu_die "no Details expander in the sheet"
d action --do expand --node-id "$exp" >/dev/null || uxu_die "could not expand Details"
sleep 1

expect "no time in the sheet says UTC" \
  bash -c 'timeout 120 "$0" --dir "$1" tree --name SessionBody --depth 20 | jq -e "[.. | objects | select(.visible and (((.text // \"\") | test(\" UTC\")) or ((.name // \"\") | test(\" UTC\"))))] | length == 0"' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"
expect "the Host heading is not followed by a field with the same label" \
  bash -c 'timeout 120 "$0" --dir "$1" tree --name SessionBody --depth 20 | jq -e "
    ([.. | objects | select(.name == \"SessionHostHeading\" and .visible) | .text] | first // \"\") as \$h
    | ([.. | objects | select(.name == \"SessionHostFields\")][0] // {}) as \$f
    | (\$h == \"\") or (([\$f | .. | objects | select(.type == \"Border\" and .visible)] | length > 0)
    and ([\$f | .. | objects | select(.visible and ((.text // \"\") == \$h))] | length == 0))"' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR"

d click --type Button --name "Close sheet" >/dev/null 2>&1 || d key --chord Escape >/dev/null 2>&1 || true
d wait-for --name SheetSubjectText --state hidden --wait-ms 10000 >/dev/null 2>&1 || true
d action --do focus --type ListBoxItem --name Agents >/dev/null 2>&1 || true
sleep 1
# Classes.selected="{Binding}" is the attached property __AvaloniaReserved::Classes::selected, listed by props on every row as True or
# False: the row must be found and read False (round 2 grepped for the name, which is always there).
expect "the row is not highlighted after the sheet closes and focus has left the list" \
  bash -c 'v="$(timeout 120 "$0" --dir "$1" props --type Border --name "$2" | jq -r "[.. | objects | .[\"__AvaloniaReserved::Classes::selected\"]? // empty] | first // \"missing\"")"; echo "selected=$v"; [ "$v" = False ]' \
  "$UXU_DRIVER" "$AW_UI_DRIVER_DIR" "$row"

uxu_done
