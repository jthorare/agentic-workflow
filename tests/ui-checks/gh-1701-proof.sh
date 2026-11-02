#!/usr/bin/env bash
# tests/ui-checks/gh-1701-proof.sh — #1701's pages proof in the real app: a named page whose action relates to two concepts, navigated
# both ways, still there after a new map version. (The concept-list half, pin -> rebuild, needs a built map: a refresh replaces this
# fixture's imported one.) Step 2 opens Self scheduling's
# Why drill and names a page, "Week schedule", whose action "Submit a schedule" relates to Self scheduling and Billing (Save and
# approve; Billing is picked as the first visible item of "Relate to another concept", which assumes it is the only concept offered
# there; step 2 fails on the "Relates to Billing" box if another concept gets picked). Step 3 opens the action from the Pages and
# actions section: its panel's Relates to lists both concepts. Step 4 opens
# Billing's Why drill: the same page is listed there. Step 5 imports a new map version (`context whole concepts`, then `pages` again)
# and checks the page is still under both concepts.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole pages`). Every driver call has `timeout 120`; a timeout is a setup failure.
#   scripts/run-test-guard.sh -- bash tests/ui-checks/gh-1701-proof.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots and each step's visible texts; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init gh-1701-proof

d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
frame() { d screenshot --out "$OUT/$1.png" >/dev/null 2>&1 || true; texts >"$OUT/$1-texts.txt"; }
visible() { d search "$@" --limit 60 2>/dev/null | jq -e 'any(.matches[]; .visible)' >/dev/null; }
count_visible() { d search "$@" --limit 60 2>/dev/null | jq '[.matches[] | select(.visible)] | length'; }
# Poll (every 250 ms, up to $1 seconds) until a control matching the rest of the arguments is visible.
wait_visible() {
  local secs="$1" i
  shift
  for i in $(seq 1 $((secs * 4))); do visible "$@" && return 0; sleep 0.25; done
  return 1
}
# The whole map's version from the page header ("Context · the whole map · v2"), empty when the header is not shown.
map_version() { texts | sed -n 's/^Context · the whole map · v\([0-9][0-9]*\)$/\1/p' | head -n 1; }
texts() { d search --type TextBlock --limit 600 2>/dev/null | jq -r '.matches[] | select(.visible) | .text // empty'; }
card_id() { d search --type Button --limit 80 2>/dev/null | jq -r --arg w "$1" 'first(.matches[] | select(.visible and (((.name // "") + " " + (.text // "")) | contains($w))) | .id) // empty'; }
click_card() {
  local id
  id="$(card_id "$1")"
  [ -n "$id" ] || return 1
  d click --node-id "$id" >/dev/null 2>&1
}
# Back to the concept list (the crumb), then into concept $1's Why drill.
open_why() {
  { visible --type Button --name "Concepts" && d click --type Button --name "Concepts" >/dev/null 2>&1; } || click_card "Concepts" || true   # the crumb
  wait_visible 30 --type TextBlock --contains "$1" || return 1
  click_card "$1" || return 1
  wait_visible 30 --type Button --name "Open the Why band" || return 1
  d click --type Button --name "Open the Why band" >/dev/null 2>&1 || return 1
  wait_visible 30 --type Button --name "New page"
}

bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" context whole pages >"$OUT/pages-up.log" 2>&1 || setup_fail "sindri-ui-fixture context whole pages (see $OUT/pages-up.log)"
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$CTX_ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
setsid env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
d ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
d window --width 1440 --height 960 >/dev/null 2>&1 || setup_fail "could not size the window"
d click --type ListBoxItem --name Context >/dev/null 2>&1 || setup_fail "no Context tab"
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "the Context page did not open on the concept list (no 'Self scheduling' card)"
frame 0-concepts

# Step 2: Self scheduling's Why drill -> New page "Week schedule", action "Submit a schedule" relating to both concepts -> Save and approve.
open_why "Self scheduling" || { frame 2-no-drill; fail "step 2: Self scheduling's Why drill shows no New page (texts in $OUT/2-no-drill-texts.txt)"; }
frame 2-drill
d click --type Button --name "New page" >/dev/null 2>&1 || fail "step 2: could not click New page"
d wait-for --type TextBox --name "Name" --state visible --wait-ms 30000 >/dev/null 2>&1 || fail "step 2: the page sheet has no Name field"
d type --type TextBox --name "Name" --value "Week schedule" --replace >/dev/null 2>&1 || fail "step 2: could not type the page's name"
d type --type TextBox --name "Action name" --value "Submit a schedule" --replace >/dev/null 2>&1 || fail "step 2: could not type the action's name"
# The sheet starts with the drill's concept ticked; a second one comes from "Relate to another concept" (it can arrive unticked).
d click --type ComboBox --name "Relate to another concept" >/dev/null 2>&1 || fail "step 2: could not open 'Relate to another concept'"
sleep 1
d search --type ComboBoxItem --limit 20 >"$OUT/2-relate-items.json" 2>&1 || true
wait_visible 10 --type TextBlock --text "Billing" || { frame 2-no-billing; fail "step 2: 'Relate to another concept' offers no Billing (items in $OUT/2-relate-items.json)"; }
# The item carries no name of its own (its text is the TextBlock inside), so it is clicked by node id: the one visible item.
item="$(jq -r 'first(.matches[] | select(.visible) | .id) // empty' "$OUT/2-relate-items.json")"
[ -n "$item" ] && d click --node-id "$item" >"$OUT/2-pick.txt" 2>&1 || fail "step 2: could not pick Billing (see $OUT/2-pick.txt)"
wait_visible 10 --type CheckBox --name "Relates to Billing" || { frame 2-no-billing-tick; fail "step 2: picking Billing added no 'Relates to Billing' box (texts in $OUT/2-no-billing-tick-texts.txt)"; }
# The box is ticked when its IsChecked property reads True (the search result carries no checked field); tick it otherwise.
box="$(d search --type CheckBox --name "Relates to Billing" --limit 5 2>/dev/null | jq -r 'first(.matches[] | select(.visible) | .id) // empty')"
[ -n "$box" ] || fail "step 2: no visible 'Relates to Billing' box"
d props --node-id "$box" 2>/dev/null | grep -Eq 'IsChecked[^A-Za-z0-9]+True' || d click --node-id "$box" >/dev/null 2>&1
d props --node-id "$box" 2>/dev/null | grep -Eq 'IsChecked[^A-Za-z0-9]+True' || { frame 2-billing-unticked; fail "step 2: 'Relates to Billing' stays unticked (texts in $OUT/2-billing-unticked-texts.txt)"; }
frame 2-page-sheet
d click --type Button --name "Save and approve" >/dev/null 2>&1 || fail "step 2: could not click Save and approve"
wait_visible 60 --type Button --name "Edit page: Week schedule" || { frame 2-not-saved; fail "step 2: no 'Edit page: Week schedule' row after Save and approve (texts in $OUT/2-not-saved-texts.txt)"; }
sleep 1
frame 2-saved
grep -q "Week schedule" "$AW_SINDRI_HOME/profile/profile.yaml" || fail "step 2: the live profile has no Week schedule page"

# Step 3: the action opens its panel; Relates to names both concepts.
# The action's link is a Button whose name is its row ("Submit a schedule · on Week schedule · ..."), with the text in a TextBlock inside.
click_card "Submit a schedule" || { frame 3-no-action; fail "step 3: the page's action is not a link in the section (texts in $OUT/3-no-action-texts.txt)"; }
wait_visible 120 --type TextBlock --text "Relates to" || { frame 3-no-panel; fail "step 3: the action's panel has no Relates to (texts in $OUT/3-no-panel-texts.txt)"; }
sleep 1
frame 3-action-panel
sed -n '/^Relates to$/,/^Why$/p' "$OUT/3-action-panel-texts.txt" >"$OUT/3-relates-to.txt"
for c in "Self scheduling" "Billing"; do
  grep -qx -- "$c" "$OUT/3-relates-to.txt" || fail "step 3: the action's panel does not relate it to $c (texts in $OUT/3-action-panel-texts.txt)"
done
d click --type Button --name "Close (Esc)" >/dev/null 2>&1 || true

# Step 4: from the other concept: Billing's Why drill lists the same page.
open_why "Billing" || { frame 4-no-drill; fail "step 4: Billing's Why drill did not open (texts in $OUT/4-no-drill-texts.txt)"; }
wait_visible 30 --type Button --name "Edit page: Week schedule" || { frame 4-no-page; fail "step 4: Billing's Why drill does not list Week schedule (texts in $OUT/4-no-page-texts.txt)"; }
sleep 1
frame 4-billing-drill

# Step 5: a new map version (another map, then this one again: two new versions) keeps the page under both concepts. The drills are
# checked only once a drill shows the version two above the one before, so they never read the old map.
before="$(map_version)"
[ -n "$before" ] || { frame 5-no-version; fail "step 5: the header shows no map version before the import (texts in $OUT/5-no-version-texts.txt)"; }
bash "$FIXTURE" context whole concepts >"$OUT/concepts-switch.log" 2>&1 || setup_fail "context whole concepts (see $OUT/concepts-switch.log)"
bash "$FIXTURE" context whole pages >"$OUT/pages-switch.log" 2>&1 || setup_fail "context whole pages again (see $OUT/pages-switch.log)"
# The open Billing drill follows the stored map (#1930): it is not opened again while waiting.
want=$((before + 2)) now="" end=$((SECONDS + 120))
while [ "$SECONDS" -lt "$end" ]; do
  now="$(map_version)"
  [ -n "$now" ] && [ "$now" -ge "$want" ] && break
  sleep 3
done
[ -n "$now" ] && [ "$now" -ge "$want" ] || { frame 5-no-new-version; fail "step 5: the app still shows map v${now:-?} 120 s after the import, wanted v$want (texts in $OUT/5-no-new-version-texts.txt)"; }
echo "v$before -> v$now" >"$OUT/5-versions.txt"
visible --type Button --name "Concepts" && d click --type Button --name "Concepts" >/dev/null 2>&1
for c in "Self scheduling" "Billing"; do
  open_why "$c" || { frame "5-no-drill"; fail "step 5: $c's Why drill did not open after the new map version"; }
  wait_visible 30 --type Button --name "Edit page: Week schedule" || { frame 5-lost-page; fail "step 5: after the new map version $c's Why drill does not list Week schedule (texts in $OUT/5-lost-page-texts.txt)"; }
done
frame 5-after-rebuild
echo "PASS: Week schedule's action relates to Self scheduling and Billing and is reached from both; the page survives a new map version (frames in $OUT)"
