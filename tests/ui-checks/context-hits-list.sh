#!/usr/bin/env bash
# tests/ui-checks/context-hits-list.sh — proof check for the Context page's hits list (ui map-hits): a search lists its hits beside
# the map, one row per hit, and choosing a row focuses that node on the map.
# Expected: after "tab" is searched on the tabs map, a "Matches" list shows with at least 2 rows (a ListBoxItem named by each hit's
# label); a click on row 1 pins its node, which opens the map's node panel (the label then shows a second time, as the panel's title).
# On main (no hits list) it FAILs at step 2; on the branch it passes.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole under` for the tabs map). The search goes to the real /sindri/context/search the fixture's sindri
# serves; if a fixture sindri did not serve it the page would fall back to the drawn search and the rows would carry no snippet line.
# Every driver call has `timeout 120`; a timeout is a setup failure.
#   bash tests/ui-checks/context-hits-list.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init context-hits-list

# Every driver call: timeout 120; a timeout (124) is a setup failure, any other exit is the caller's to read.
d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
# The names of the visible list rows (ListBoxItems) whose name has the word "tab" in it; the navigation tabs have none.
rows() { d search --type ListBoxItem --limit 100 2>/dev/null | jq -r '.matches[] | select(.visible and ((.name // "") | test("tab"; "i"))) | .name'; }
# How many visible text blocks show exactly this text.
shown() { d search --type TextBlock --text "$1" --limit 40 2>/dev/null | jq '[.matches[] | select(.visible)] | length'; }

bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" context whole under >"$OUT/whole-up.log" 2>&1 || setup_fail "sindri-ui-fixture context whole under (see $OUT/whole-up.log)"
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$CTX_ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
d ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
d window --width 1400 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
d click --type ListBoxItem --name Context >/dev/null 2>&1 || setup_fail "no Context tab"
d wait-for --type TextBlock --contains "links" --state visible --wait-ms 60000 >/dev/null 2>&1 || setup_fail "the whole map never showed its size line"

# Step 1: search the tabs map.
d type --type TextBox --name "Ask or look up" --value "tab" --replace >/dev/null 2>&1 || setup_fail "could not type into the search box"
d key --chord Enter --type TextBox --name "Ask or look up" >/dev/null 2>&1 || setup_fail "could not press Enter in the search box"

# Step 2: the hits list shows beside the map with its rows.
d wait-for --type ListBox --name Matches --state visible --wait-ms 30000 >/dev/null 2>&1 || fail "step 2: no hits list ('Matches') after the search"
mapfile -t names < <(rows)
[ "${#names[@]}" -ge 2 ] || fail "step 2: the hits list has ${#names[@]} row(s), expected at least 2"
first="${names[0]}"

# Step 3: choosing row 1 focuses its node: the node panel opens, so the label shows once more.
before="$(shown "$first")"
d click --type ListBoxItem --name "$first" --index 0 >/dev/null 2>&1 || fail "step 3: could not click the first row ('$first')"
sleep 2
after="$(shown "$first")"
[ "${after:-0}" -gt "${before:-0}" ] || fail "step 3: choosing '$first' did not open its node on the map (label shown $before time(s) before, $after after)"
echo "PASS: the search listed ${#names[@]} hits beside the map and choosing '$first' focused its node"
