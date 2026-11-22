#!/usr/bin/env bash
# tests/ui-checks/pin-two-clicks.sh — proof check for Q7b of #1667 (concept pins spec, owner steps): pinning a suggested concept is two
# clicks. Pin this on the Self scheduling card opens the Pin sheet prefilled (name, definition, its words, its features);
# Pin writes the pin (no approval: edits apply on save), the sheet closes, and the card turns into the person's pin.
# Expected: step 1 the card carries Pin this; step 2 the sheet shows "Pin this", the name and the Pin
# button; step 3 after one click the sheet is gone, the card carries Edit pin and the live profile holds the pin. On main (no Pin
# this) it FAILs at step 1; on the branch it passes. Frames land in $OUT (frame-1-card, -2-sheet, -3-pinned, -4-word-refused, -5-unpin-confirm .png).
# Step 4 reopens the pin with Edit pin, types 'the' (refused: only small words) and presses Unpin (asks, naming the pin).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole concepts`). Every driver call has `timeout 120`; a timeout is a setup failure.
#   bash tests/ui-checks/pin-two-clicks.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init pin-two-clicks

# Every driver call: timeout 120; a timeout (124) is a setup failure, any other exit is the caller's to read.
d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
frame() { d screenshot --out "$OUT/$1.png" >/dev/null 2>&1 || true; }
visible() { d search "$@" --limit 40 2>/dev/null | jq -e 'any(.matches[]; .visible)' >/dev/null; }
# Every card carries a Pin this and an Edit pin button, hidden unless its state shows them; wait-for reads only the first match,
# so a visible one is polled for (every 250 ms, up to $1 seconds).
wait_visible() {
  local secs="$1" i
  shift
  for i in $(seq 1 $((secs * 4))); do visible "$@" && return 0; sleep 0.25; done
  return 1
}
texts() { d search --type TextBlock --limit 400 2>/dev/null | jq -r '.matches[] | select(.visible) | .text // empty'; }
CLICKS=0

# Start: the fixture with the concept map, this checkout's debug sindri-ui under xvfb, the window, the Context page.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" context whole concepts >"$OUT/concepts-up.log" 2>&1 || setup_fail "sindri-ui-fixture context whole concepts (see $OUT/concepts-up.log)"
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$CTX_ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
setsid env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
d ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
d window --width 1280 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
d click --type ListBoxItem --name Context >/dev/null 2>&1 || setup_fail "no Context tab"

# Step 1: the Self scheduling card is Suggested and carries Pin this.
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "the Context page did not open on the concept list (no 'Self scheduling' card)"
# The pin buttons show only while the row has the pointer or focus: focus the first row's open button (its name holds the concept name).
# The open button has the automation id ConceptOpen.<concept id>; its name starts with the concept name (the id is read from the tree, not guessed).
OPEN_ID="$(d search --type Button --limit 200 2>/dev/null | jq -r 'first(.matches[] | select((.automationId // "") | startswith("ConceptOpen.")) | select((.name // "") | startswith("Self scheduling,")) | .automationId)')"
[ -n "$OPEN_ID" ] || setup_fail "no ConceptOpen.* button named Self scheduling"
CONCEPT_ID="${OPEN_ID#ConceptOpen.}"   # the pin-slot buttons carry ConceptPin.<id> and ConceptEditPin.<id>; Billing's hidden Pin this comes first by index
focus_row() { d action --automation-id "$OPEN_ID" --do focus >/dev/null 2>&1; }
focus_row || setup_fail "could not focus the Self scheduling row"
wait_visible 30 --type Button --automation-id "ConceptPin.$CONCEPT_ID" || fail "step 1: no card carries Pin this"
# No "Suggested" mark here: it shows only when pinned and unpinned concepts are mixed, and nothing is pinned yet.
sleep 1
frame frame-1-card
texts >"$OUT/frame-1-texts.txt"

# Step 2: click 1, Pin this on the first card (Self scheduling: the derived one; Billing is set by a person) opens the sheet prefilled.
focus_row
d click --automation-id "ConceptPin.$CONCEPT_ID" >/dev/null 2>&1 || fail "step 2: could not click Pin this"
CLICKS=$((CLICKS + 1))
d wait-for --type Button --name "Pin" --state enabled --wait-ms 30000 >/dev/null 2>&1 || fail "step 2: the sheet has no enabled Pin"
d wait-for --type TextBlock --text "Pin this" --state visible --wait-ms 10000 >/dev/null 2>&1 || fail "step 2: the sheet's heading is not 'Pin this'"
name="$(d search --type TextBox --limit 20 2>/dev/null | jq -r 'first(.matches[] | select(.visible) | .text // empty)')"
[ "$name" = "Self scheduling" ] || fail "step 2: the sheet's name is '$name', not the card's 'Self scheduling'"
sleep 1
frame frame-2-sheet
texts >"$OUT/frame-2-texts.txt"

# Step 3: click 2, Pin writes the pin (edits apply on save, there is no approval); the sheet closes.
d click --type Button --name "Pin" >/dev/null 2>&1 || fail "step 3: could not click Pin"
CLICKS=$((CLICKS + 1))
d wait-for --type Button --name "Pin" --state gone --wait-ms 90000 >/dev/null 2>&1 \
  || d wait-for --type Button --name "Pin" --state notVisible --wait-ms 1000 >/dev/null 2>&1 \
  || { texts >"$OUT/frame-3-texts.txt"; fail "step 3: the sheet did not close after Pin (texts in $OUT/frame-3-texts.txt)"; }
sleep 2
frame frame-3-pinned
texts >"$OUT/frame-3-texts.txt"
# The card now reads as the person's pin: Edit pin replaces Pin this, and the live profile holds the pin with no approval waiting.
focus_row || fail "step 3: could not focus the Self scheduling row again"   # the pin slot is hidden without pointer or focus, and the sheet's close left focus on a button that is gone
wait_visible 30 --type Button --automation-id "ConceptEditPin.$CONCEPT_ID" || fail "step 3: no card carries Edit pin after Pin"
grep -q "Self scheduling" "$AW_SINDRI_HOME/profile/profile.yaml" || fail "step 3: the live profile has no Self scheduling pin"
"$PROVE" human profile validate >"$OUT/profile-validate.txt" 2>&1 || true
[ "$CLICKS" -eq 2 ] || fail "the path took $CLICKS clicks"

# Step 4 (frames for review, not part of the two clicks): Edit pin reopens the sheet as "Edit pin"; a word made only of small words is
# refused with the sheet's own sentence; Unpin asks first, naming the pin.
focus_row || fail "step 4: could not focus the Self scheduling row again"
d click --automation-id "ConceptEditPin.$CONCEPT_ID" >/dev/null 2>&1 || fail "step 4: could not click Edit pin"
d wait-for --type TextBlock --text "Edit pin" --state visible --wait-ms 30000 >/dev/null 2>&1 || fail "step 4: the sheet's heading is not 'Edit pin'"
d type --name "Add a word" --value "the" --replace >/dev/null 2>&1 || fail "step 4: could not type a word"
d click --type Button --name "Add word" >/dev/null 2>&1 || fail "step 4: could not click Add word"
d wait-for --type TextBlock --contains "not only small words" --state visible --wait-ms 10000 >/dev/null 2>&1 || fail "step 4: the word 'the' was not refused with the word-shape sentence"
sleep 1
frame frame-4-word-refused
d type --name "Add a word" --value "" --replace >/dev/null 2>&1 || true
d click --type Button --name "Unpin" >/dev/null 2>&1 || fail "step 4: could not click Unpin"
d wait-for --type TextBlock --contains "Unpin Self scheduling?" --state visible --wait-ms 10000 >/dev/null 2>&1 || fail "step 4: Unpin did not ask 'Unpin Self scheduling? ...'"
sleep 1
frame frame-5-unpin-confirm
texts >"$OUT/frame-5-texts.txt"
echo "PASS: Pin this -> sheet prefilled -> Pin = $CLICKS clicks; Edit pin refuses a small word and Unpin asks first (frames in $OUT)"
