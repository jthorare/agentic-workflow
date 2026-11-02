#!/usr/bin/env bash
# tests/ui-checks/concept-search.sh — proof check for #1699: on the Context page's Concepts screen, a search does not filter the concept cards.
# Expected: with the fixture's two concepts (Self scheduling, Billing) a search for "Billing" (typed in the anchor box, then Enter) leaves
# the Billing card and no Self scheduling card; a search for "zzqx" shows no card and says "No matches"; the "Clear search" button brings both
# cards back. On main the cards ignore the search (Self scheduling stays on screen at step 2) and it FAILs there; on the fix it passes.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole concepts` for the concept map). Every driver call has `timeout 120`; a timeout is a setup failure.
#   bash tests/ui-checks/concept-search.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init concept-search

# Every driver call: timeout 120; a timeout (124) is a setup failure, any other exit is the caller's to read.
d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
# The id of the first visible Button whose name or text holds $1 (empty when none).
card_id() { d search --type Button --limit 60 2>/dev/null | jq -r --arg w "$1" 'first(.matches[] | select(.visible and (((.name // "") + " " + (.text // "")) | contains($w))) | .id) // empty'; }
# The id of the anchor search box: the TextBox named AnchorBox, else (its AutomationProperties.Name is the label) the first visible TextBox.
box_id() {
  d search --type TextBox --limit 20 2>/dev/null | jq -r 'first(.matches[] | select(.visible and (.name == "AnchorBox"))) // first(.matches[] | select(.visible)) | .id // empty'
}
# Which concept cards are on screen, as "Self scheduling,Billing" style text (empty when none).
seen() {
  local out="" c
  for c in "Self scheduling" "Billing"; do [ -z "$(card_id "$c")" ] || out="${out:+$out,}$c"; done
  printf '%s' "$out"
}
# Type a term into the anchor box (replacing what is there) and press Enter.
search_for() {
  local id
  id="$(box_id)"
  [ -n "$id" ] || fail "$1: no anchor search box on the Concepts screen"
  d type --node-id "$id" --value "$2" --replace >/dev/null 2>&1 || fail "$1: could not type '$2' into the search box"
  d key --node-id "$id" --chord Enter >/dev/null 2>&1 || fail "$1: could not press Enter in the search box"
}
# Poll up to 15 s until the visible cards read $1.
settle() {
  local i
  for i in $(seq 1 30); do [ "$(seen)" = "$1" ] && return 0; sleep 0.5; done
  return 1
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

# Step 1: both concept cards show (setup).
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "step 1: the Context page did not open on the concept list (no 'Self scheduling' card)"
sleep 1
[ "$(seen)" = "Self scheduling,Billing" ] || setup_fail "step 1: expected both concept cards, saw '$(seen)'"

# Step 2: a search for Billing leaves only the Billing card.
search_for "step 2" "Billing"
settle "Billing"
d screenshot --out "$OUT/step2-billing.png" >/dev/null 2>&1
got="$(seen)"
[ "$got" = "Billing" ] || fail "step 2: a search for Billing still shows the Self scheduling card (cards visible: '${got:-none}')"

# Step 3: a search with no hit shows no card and says "No matches".
search_for "step 3" "zzqx"
settle ""
d screenshot --out "$OUT/step3-nomatch.png" >/dev/null 2>&1
got="$(seen)"
[ -z "$got" ] || fail "step 3: a search for zzqx still shows concept cards (cards visible: '$got')"
d wait-for --type TextBlock --contains "No matches" --state visible --wait-ms 15000 >/dev/null 2>&1 \
  || fail "step 3: a search for zzqx shows no cards but never says 'No matches'"

# Step 4: Clear search brings both cards back.
clear_id="$(card_id "Clear search")"
[ -n "$clear_id" ] || fail "step 4: no 'Clear search' button while a search is active"
d click --node-id "$clear_id" >/dev/null 2>&1 || fail "step 4: could not click 'Clear search'"
settle "Self scheduling,Billing"
d screenshot --out "$OUT/step4-cleared.png" >/dev/null 2>&1
got="$(seen)"
[ "$got" = "Self scheduling,Billing" ] || fail "step 4: Clear search did not restore both cards (cards visible: '${got:-none}')"
echo "PASS: a search narrowed the concept cards, a no-match said so and Clear search restored both"
