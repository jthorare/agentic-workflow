#!/usr/bin/env bash
# B-14 (UX pass 2026-10-25): Agents filters work, but the header count ignores them and the status words differ from the rows.
# Expected: with a filter on, the header counts the shown agents ("N of M agents"); the status filter offers the same words
# as the row chips (Working, Asking you, Idle, Unknown); the no-match line and its Clear filters button share one alignment.
source "$(dirname "$0")/lib.sh"

# Every driver call is bounded.
dt() { timeout 120 "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" "$@"; }
d() { dt "$@"; }

uxu_start up "sessions up"

# A control named through AutomationProperties.Name has no shown text, so find it by type and name; --contains would miss it.
# nid <type> <name-substring>: the node id, or a dump of what is there on stderr.
nid() {
  local id
  id="$(dt search --type "$1" --limit 200 | jq -r --arg s "$2" '[.matches[] | select((.name // "") | contains($s))][0].id // empty')"
  if [ -z "$id" ]; then
    echo "no $1 named like '$2'; visible $1 names:" >&2
    dt search --type "$1" --limit 200 | jq -r '.matches[] | "  \(.type) \(.name // "-")"' >&2
    return 1
  fi
  printf '%s\n' "$id"
}

d click --type ListBoxItem --name Agents >/dev/null || uxu_die "no Agents tab"
d wait-for --name SessionRowRepo --state visible --wait-ms 30000 >/dev/null || uxu_die "no agent rows appeared"
sleep 2

# One SessionRowRepo cell per live row (the row frame carries a dynamic automation name).
rows() { dt search --name SessionRowRepo --limit 200 | jq .total; }
head_text() { dt search --type Border --limit 200 | jq -r '[.matches[] | select((.name // "") | startswith("Agents, ") and contains(" asking you"))][0].name // ""'; }
combo_prop() { local id; id="$(nid ComboBox "$1")" || return 1; dt props --node-id "$id" | grep -E "SelectedItem"; }
press_down() { local id; id="$(nid ComboBox "$1")" || return 1; dt key --chord Down --node-id "$id" >/dev/null; }

all="$(rows)"
[ "$all" -gt 1 ] || uxu_die "the fixture shows $all agents; need several"

press_down "filter by status" || uxu_die "could not drive the status filter"
sleep 2
shown="$(rows)"
[ "$shown" -lt "$all" ] || uxu_die "the first status choice did not narrow the list ($shown of $all)"
head="$(head_text)"
[ -n "$head" ] || uxu_die "no Agents header Border found"

first_number() { printf %s "$1" | grep -oE "[0-9]+" | head -1; }
expect "the header counts the shown agents ($shown) once a filter narrows the list" \
  test "$(first_number "$head" | sed 's/^$/none/')" = "$shown"
expect "the header still names the full count ($all) when filtered" \
  bash -c 'printf %s "$1" | grep -qE "(^|[^0-9])$2([^0-9]|$)"' _ "$head" "$all"
expect "the status filter's first choice is a row chip word (Working)" \
  bash -c 'printf %s "$1" | grep -q "Working"' _ "$(combo_prop "filter by status")"
expect "the status filter does not show the served word 'running'" \
  bash -c '! printf %s "$1" | grep -q "running"' _ "$(combo_prop "filter by status")"

# Find a provider and status pair that matches nothing.
clear_filters() { local id; id="$(nid Button "Clear filters" 2>/dev/null)" && dt click --node-id "$id" >/dev/null 2>&1; return 0; }
empty=""
for p in 1 2 3; do
  for s in 1 2 3 4; do
    clear_filters
    for _ in $(seq 1 "$p"); do press_down "filter by provider"; done
    for _ in $(seq 1 "$s"); do press_down "filter by status"; done
    sleep 1
    if [ "$(rows)" = 0 ]; then empty=1; break 2; fi
  done
done
[ -n "$empty" ] || uxu_die "no provider and status pair matched nothing"
d wait-for --type Button --name "Clear filters" --state visible --wait-ms 10000 >/dev/null || uxu_die "no Clear filters button"

# props answers one JSON document: read exactly the HorizontalAlignment property, not the first alignment word anywhere in it.
align() { local id; id="$1"; dt props --node-id "$id" | jq -r '.properties.HorizontalAlignment // empty'; }
line_id="$(dt search --type TextBlock --text "No sessions match these filters." | jq -r '.matches[0].id // empty')"
btn_id="$(nid Button "Clear filters")" || uxu_die "no Clear filters button node"
[ -n "$line_id" ] || uxu_die "no no-match line"
line_a="$(align "$line_id")"; btn_a="$(align "$btn_id")"
expect "the no-match line ($line_a) and the Clear filters button ($btn_a) share one horizontal alignment" \
  test -n "$line_a" -a "$line_a" = "$btn_a"

uxu_done
