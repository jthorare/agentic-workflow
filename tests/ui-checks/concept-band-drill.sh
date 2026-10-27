#!/usr/bin/env bash
# tests/ui-checks/concept-band-drill.sh — proof check for drilling into one band of a concept's map (ui #1289): inside a concept
# each drawn band has an "Open the <Band> band" button; opening Why shows its links to the other bands first, then (segment
# switch) only the links inside Why; Back returns to the concept's whole map, Back again to the concept cards.
# Expected: step 1 the concept map draws all bands ("Code ("); step 2 the "Open the Why band" button drills in: the canvas name
# lists "Why (1 decision" and "Product (1 feature" but not "Code (", the last crumb is "Why" and "Self scheduling" is a crumb
# button; step 3 "Links inside Why" shows Why alone; step 4 "Links to other bands" brings Product back; step 5 Back returns to
# the whole concept map and its band button; step 6 Back returns to the concept cards. On main (no band button) it FAILs at step 2.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole concepts` for the concept map). Every driver call has `timeout 120`; a timeout is a setup failure.
#   bash tests/ui-checks/concept-band-drill.sh  exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init concept-band-drill

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

# The visible map canvas's accessible name (empty when it is not drawn).
canvas_name() { d search --type ZoomBorder --limit 5 2>/dev/null | jq -r 'first(.matches[] | select(.visible and .width > 0 and .height > 0)) | .name // empty'; }
# Poll (about 30 s) until the predicate named in $1 holds for the canvas name; the last name stays in $cname.
cname=""
poll_canvas() {
  local i
  for i in $(seq 1 30); do
    cname="$(canvas_name)"
    "$1" && return 0
    sleep 1
  done
  return 1
}
has() { case "$cname" in *"$1"*) return 0 ;; *) return 1 ;; esac; }
is_cross() { has "Why (1 decision" && has "Product (1 feature" && ! has "Code ("; }
is_own() { has "Why (1 decision" && ! has "Product (" && ! has "Code ("; }
is_product_back() { has "Product (1 feature"; }
is_full() { has "Code ("; }
vis_button() { d search --type Button --name "$1" --limit 20 2>/dev/null | jq -e 'any(.matches[]; .visible)' >/dev/null; }
click_named() { d click --type "$1" --name "$2" >/dev/null 2>&1; }

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

# Step 1: open Self scheduling; the concept's whole map draws every band.
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || fail "step 1: the Context page did not open on the concept list (no 'Self scheduling' card)"
click_card "Self scheduling" || fail "step 1: could not click the Self scheduling card"
d wait-for --type TextBlock --contains "links" --state visible --wait-ms 60000 >/dev/null 2>&1 || fail "step 1: no scoped map (no size line) after opening Self scheduling"
poll_canvas is_full || fail "step 1: the concept map does not draw the Code band (canvas: '$cname')"

# Step 2: the Why band button drills in; the other bands it links to stay, the rest goes.
# The band button's label is a child TextBlock, so the driver reports no text on it: it is found by its name.
vis_button "Open the Why band" || fail "step 2: no 'Open the Why band' button inside the concept (canvas: '$cname')"
click_named Button "Open the Why band" || fail "step 2: could not click 'Open the Why band'"
poll_canvas is_cross || fail "step 2: after opening Why the canvas should list Why (1 decision) and Product (1 feature) but not Code (canvas: '$cname')"
echo "$cname" >"$OUT/why-cross.txt"
[ -n "$(ctext --type TextBlock --text "Why")" ] || fail "step 2: the last crumb 'Why' is not shown (canvas: '$cname')"
vis_button "Self scheduling" || fail "step 2: the concept crumb 'Self scheduling' is not a button while drilled (canvas: '$cname')"
d screenshot --out "$OUT/why-cross.png" >/dev/null 2>&1 || true

# Step 3: only the links inside Why.
click_named RadioButton "Links inside Why" || fail "step 3: no 'Links inside Why' segment (canvas: '$cname')"
poll_canvas is_own || fail "step 3: 'Links inside Why' should draw Why alone, no Product or Code (canvas: '$cname')"
echo "$cname" >"$OUT/why-own.txt"
d screenshot --out "$OUT/why-own.png" >/dev/null 2>&1 || true

# Step 4: back to the links to other bands.
click_named RadioButton "Links to other bands" || fail "step 4: no 'Links to other bands' segment (canvas: '$cname')"
poll_canvas is_product_back || fail "step 4: 'Links to other bands' did not bring Product back (canvas: '$cname')"

# Step 5: Back returns to the concept's whole map.
click_card "Back" || fail "step 5: could not click Back while drilled"
poll_canvas is_full || fail "step 5: Back did not return to the concept's whole map with the Code band (canvas: '$cname')"
vis_button "Open the Why band" || fail "step 5: the 'Open the Why band' button is not back after Back"
# The band buttons (each a child TextBlock "Why", "Product", "Code") are back, so the crumb is checked by the drill's own segment being gone.
d search --type RadioButton --name "Links inside Why" --limit 10 2>/dev/null | jq -e 'any(.matches[]; .visible)' >/dev/null && fail "step 5: the drill's segments are still shown after Back"

# Step 6: Back again returns to the concept cards.
click_card "Back" || fail "step 6: could not click Back inside the concept"
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 30000 >/dev/null 2>&1 || fail "step 6: Back did not return to the concept list"
[ -n "$(card_id "Billing")" ] || fail "step 6: the concept list has no Billing card after Back"
echo "PASS: drilled into Why (cross-band, then in-band), Back returned to the concept and then to the concepts"
