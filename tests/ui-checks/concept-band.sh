#!/usr/bin/env bash
# tests/ui-checks/concept-band.sh — proof check for the Context page's concept band (ui #1012): the page opens on the product's
# concepts, a click opens the bands scoped to that concept, Back returns to the concepts.
# Expected: the concept list shows FIRST (a card per concept, no whole-map size line yet); a click on Self scheduling shows the
# scoped map (size line, zoom canvas, "Concepts" and "Self scheduling" crumbs, Back); Back shows the cards again; a click on Billing
# shows its scoped map. On main (no concept band) it FAILs at step 1; on the branch it passes.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole concepts` for the concept map). Every driver call has `timeout 120`; a timeout is a setup failure.
#   bash tests/ui-checks/concept-band.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init concept-band

# Every driver call: timeout 120; a timeout (124) is a setup failure, any other exit is the caller's to read.
d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
# The text of the first visible match (empty when none), and its node id.
ctext() { d search "$@" --limit 40 2>/dev/null | jq -r 'first(.matches[] | select(.visible)) | .text // empty'; }
card_id() { d search --type Button --limit 60 2>/dev/null | jq -r --arg w "$1" 'first(.matches[] | select(.visible and (((.name // "") + " " + (.text // "")) | contains($w))) | .id) // empty'; }
click_card() {
  local id
  id="$(card_id "$1")"
  [ -n "$id" ] || return 1
  d click --node-id "$id" >/dev/null 2>&1
}

# Start: the fixture with the concept map, this checkout's debug sindri-ui under xvfb, the window, the Context page.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" context whole concepts >"$OUT/concepts-up.log" 2>&1 || setup_fail "sindri-ui-fixture context whole concepts (see $OUT/concepts-up.log)"
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$CTX_ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
d ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
d window --width 1280 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
d click --type ListBoxItem --name Context >/dev/null 2>&1 || setup_fail "no Context tab"

# Step 1: the concept list comes first.
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || fail "step 1: the Context page did not open on the concept list (no 'Self scheduling' card)"
[ -n "$(card_id "Billing")" ] || fail "step 1: the concept list has no Billing card"
sleep 1
[ -z "$(ctext --type TextBlock --contains "links")" ] || fail "step 1: the whole map's size line showed before a concept was opened"

# Step 2: a click opens the bands scoped to the concept.
click_card "Self scheduling" || fail "step 2: could not click the Self scheduling card"
d wait-for --type TextBlock --contains "links" --state visible --wait-ms 60000 >/dev/null 2>&1 || fail "step 2: no scoped map (no size line) after opening Self scheduling"
canvas="$(d search --type ZoomBorder --limit 5 2>/dev/null | jq -c 'first(.matches[] | select(.visible and .width > 0 and .height > 0)) // empty')"
[ -n "$canvas" ] || fail "step 2: the scoped map's canvas (the bands) is not drawn"

# Step 3: the path and the way back are on screen; the cards are gone.
d search --type Button --contains "Concepts" --limit 20 2>/dev/null | jq -e 'any(.matches[]; .visible)' >/dev/null || fail "step 3: no 'Concepts' crumb"
[ -n "$(ctext --type TextBlock --text "Self scheduling")" ] || fail "step 3: the concept's own crumb ('Self scheduling') is not shown"
[ -n "$(card_id "Back")" ] || fail "step 3: no Back button inside the concept"
[ -z "$(card_id "Billing")" ] || fail "step 3: the concept cards are still on screen inside a concept"

# Step 4: Back returns to the concepts.
click_card "Back" || fail "step 4: could not click Back"
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 30000 >/dev/null 2>&1 || fail "step 4: Back did not return to the concept list"
sleep 1
[ -z "$(ctext --type TextBlock --contains "links")" ] || fail "step 4: the scoped map's size line is still shown after Back"

# Step 5: another concept opens its own scoped map.
click_card "Billing" || fail "step 5: could not click the Billing card"
d wait-for --type TextBlock --contains "links" --state visible --wait-ms 60000 >/dev/null 2>&1 || fail "step 5: no scoped map after opening Billing"
echo "PASS: the Context page opened on the concepts, a concept opened its scoped map, Back returned and another concept opened"
