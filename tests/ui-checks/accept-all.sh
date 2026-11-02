#!/usr/bin/env bash
# tests/ui-checks/accept-all.sh — proof check for Accept all suggested (#1724, P2b-2b) in the real app: inside a concept's Why drill the
# Pages and actions section's "Accept all suggested (2)" opens a sheet with one ticked row per suggested page; a row opens into its name and
# concept ticks (mockup B); "Save all and approve" writes both pages and approves them, so the section lists them as the owner's pages
# ("Edit page: ...") with nothing waiting and no suggested page left. Two clicks from the drill (Accept all suggested, Save all and approve).
# Then "Pages in no concept" has its own Accept all suggested: each row with actions gets a concept picker, the page whose name matches a
# concept (Billing overview -> Billing) is pre-picked and ticked, the others stay unticked, and the page with no actions says why it is not added.
# Expected: step 1 the button reads "Accept all suggested (2)" and the sheet shows 2 ticked rows; step 2 a row opens into a "Page name" box;
# step 3 the sheet closes, 2 "Edit page" rows and no Accept all button; step 4 the no-concept sheet has 2 pickers, one ticked row of 3, and the
# "Not added: this page has no actions" line. On main it FAILs at step 1 (no Accept all suggested button).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole pages`, the concepts map plus Roster, Leave requests, Billing overview and Settings pages). Every
# driver call has `timeout 120`; a timeout is a setup failure. Frames (compare with mockup B, docs/superpowers/specs/2026-10-29-sindri-pages-actions-design/mockups/pages.html):
# accept-all-open.png, accept-all-row-open.png, accept-all-saved.png, accept-all-no-concept.png in $CTXUX_OUT.
#   scripts/run-test-guard.sh -- bash tests/ui-checks/accept-all.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init accept-all

d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
card_id() { d search --type Button --limit 60 2>/dev/null | jq -r --arg w "$1" 'first(.matches[] | select(.visible and (((.name // "") + " " + (.text // "")) | contains($w))) | .id) // empty'; }
click_card() {
  local id
  id="$(card_id "$1")"
  [ -n "$id" ] || return 1
  d click --node-id "$id" >/dev/null 2>&1
}
vis() { d search --type "$1" --name "$2" --limit 20 2>/dev/null | jq -e 'any(.matches[]; .visible)' >/dev/null; }
# Poll (about 30 s) until a control of type $1 named $2 is visible.
poll_vis() {
  local i
  for i in $(seq 1 30); do
    vis "$1" "$2" && return 0
    sleep 1
  done
  return 1
}
# Poll (about 30 s) until no control of type $1 named $2 is visible.
poll_gone() {
  local i
  for i in $(seq 1 30); do
    vis "$1" "$2" || return 0
    sleep 1
  done
  return 1
}
# The ids of the visible controls of type $1 whose name starts with $2 (and, when $3 is given, contains it), one per line.
ids_of() { d search --type "$1" --limit 80 2>/dev/null | jq -r --arg p "$2" --arg c "${3:-}" '.matches[] | select(.visible and ((.name // "") | startswith($p)) and ((.name // "") | contains($c))) | .id'; }
# True when the control with node id $1 is checked (its IsChecked property reads True).
checked() { d props --node-id "$1" 2>/dev/null | grep -Eq 'IsChecked[^A-Za-z0-9]+True'; }
count_checked() {
  local id n=0
  for id in "$@"; do checked "$id" && n=$((n + 1)); done
  echo "$n"
}
text_vis() { d search --contains "$1" --limit 20 2>/dev/null | jq -e 'any(.matches[]; .visible)' >/dev/null; }

bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" context whole pages >"$OUT/pages-up.log" 2>&1 || setup_fail "sindri-ui-fixture context whole pages (see $OUT/pages-up.log)"
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

# Step 1: Self scheduling's Why drill lists the two suggested pages; its button opens the sheet over them, every row ticked.
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || fail "step 1: the Context page did not open on the concept list (no 'Self scheduling' card)"
click_card "Self scheduling" || fail "step 1: could not click the Self scheduling card"
poll_vis Button "Open the Why band" || fail "step 1: no 'Open the Why band' button inside the concept"
d click --type Button --name "Open the Why band" >/dev/null 2>&1 || fail "step 1: could not click 'Open the Why band'"
poll_vis Button "Accept all suggested (2)" || fail "step 1: the Why drill shows no 'Accept all suggested (2)' button"
d click --type Button --name "Accept all suggested (2)" >/dev/null 2>&1 || fail "step 1: could not click 'Accept all suggested (2)'"
poll_vis Button "Save all and approve" || fail "step 1: the sheet shows no 'Save all and approve' button"
mapfile -t ticks < <(ids_of CheckBox "Add ")
[ "${#ticks[@]}" -eq 2 ] || fail "step 1: the sheet lists ${#ticks[@]} rows, want 2"
[ "$(count_checked "${ticks[@]}")" -eq 2 ] || fail "step 1: not every row starts ticked"
vis TextBox "Pages to add" && fail "step 1: the paste box shows in Accept all suggested"
d screenshot --out "$OUT/accept-all-open.png" >/dev/null 2>&1 || true

# Step 2: a row opens into its fields (mockup B): a name box and the concept ticks of its actions.
mapfile -t opens < <(ids_of ToggleButton "Edit ")
[ "${#opens[@]}" -ge 1 ] || fail "step 2: no row has an Edit toggle"
d click --node-id "${opens[0]}" >/dev/null 2>&1 || fail "step 2: could not open the first row"
poll_vis TextBox "Page name" || fail "step 2: the open row shows no 'Page name' box"
d screenshot --out "$OUT/accept-all-row-open.png" >/dev/null 2>&1 || true

# Step 3 (click 2): Save all and approve writes and approves both pages; the section then lists them as the owner's and offers nothing to accept.
d click --type Button --name "Save all and approve" >/dev/null 2>&1 || fail "step 3: could not click 'Save all and approve'"
poll_gone Button "Save all and approve" || fail "step 3: the sheet is still open after Save all and approve"
poll_gone Button "Accept all suggested (2)" || fail "step 3: the section still offers the pages it just accepted"
mapfile -t edits < <(ids_of Button "Edit page:")
[ "${#edits[@]}" -ge 2 ] || fail "step 3: the section lists ${#edits[@]} 'Edit page' rows after the save, want at least 2"
text_vis "Waiting for approval" && fail "step 3: a row reads 'Waiting for approval' after Save all and approve (the approve did not land)"
d screenshot --out "$OUT/accept-all-saved.png" >/dev/null 2>&1 || true

# Step 4: back to the concept list, then Pages in no concept: its Accept all has a picker per row that has actions.
for _ in 1 2 3 4; do
  [ -n "$(card_id "Pages in no concept")" ] && break
  # The breadcrumb's Back, not Esc: focus sits in the search box after the save, which keeps Esc.
  d click --type Button --name Back >/dev/null 2>&1 || true
  sleep 1
done
click_card "Pages in no concept" || fail "step 4: could not click the 'Pages in no concept' link"
poll_vis Button "Accept all suggested (3)" || fail "step 4: Pages in no concept shows no 'Accept all suggested (3)' button"
d click --type Button --name "Accept all suggested (3)" >/dev/null 2>&1 || fail "step 4: could not click 'Accept all suggested (3)'"
poll_vis Button "Save all and approve" || fail "step 4: the no-concept sheet shows no 'Save all and approve' button"
mapfile -t picks < <(ids_of ComboBox "Concept for ")
[ "${#picks[@]}" -eq 2 ] || fail "step 4: the sheet has ${#picks[@]} concept pickers, want 2 (the page with no actions has none)"
mapfile -t rows < <(ids_of CheckBox "Add ")
[ "${#rows[@]}" -eq 3 ] || fail "step 4: the sheet lists ${#rows[@]} rows, want 3"
[ "$(count_checked "${rows[@]}")" -eq 1 ] || fail "step 4: want exactly one ticked row (Billing overview, pre-picked as Billing)"
mapfile -t billing < <(ids_of CheckBox "Add " "Billing")
[ "${#billing[@]}" -eq 1 ] && checked "${billing[0]}" || fail "step 4: the Billing overview row is not ticked"
text_vis "Not added: this page has no actions to link a concept to" || fail "step 4: the page with no actions does not say why it is not added"
d screenshot --out "$OUT/accept-all-no-concept.png" >/dev/null 2>&1 || true
echo "PASS: Why drill -> Accept all suggested -> Save all and approve (2 clicks) wrote and approved both pages; Pages in no concept -> Accept all suggested shows a picker per row, Billing overview pre-picked ($OUT)"
