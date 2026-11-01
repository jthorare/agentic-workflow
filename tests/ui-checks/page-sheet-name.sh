#!/usr/bin/env bash
# tests/ui-checks/page-sheet-name.sh — proof check for the page sheet (#1724, P2b-2a) in the real app: inside a concept's Why drill
# the Pages and actions section's New page opens the page sheet on the shell's sheet; a name, one action related to the concept
# and Save and approve write the page and approve it, so the section lists it as the owner's page ("Edit page") with no "Waiting
# for approval" mark. Three clicks from the drill (New page, the concept tick, Save and approve) and the typed names.
# Expected: step 1 the Why drill shows "New page"; step 2 the sheet opens with a Name field and "Save and approve"; step 3 Save
# and approve closes the sheet and a row reads "Edit page: Week schedule" without "Waiting for approval". On main it FAILs at step 1
# (no page sheet, no New page).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole concepts`). Every driver call has `timeout 120`; a timeout is a setup failure.
#   scripts/run-test-guard.sh -- bash tests/ui-checks/page-sheet-name.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init page-sheet-name

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
prop() { d search --type "$1" --name "$2" --limit 5 2>/dev/null | jq -r 'first(.matches[] | select(.visible)) | .text // empty'; }

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

# Step 1: Self scheduling, then its Why drill: the Pages and actions section with its New page button (the fixture's map has no
# page under this concept, so the section reads empty).
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || fail "step 1: the Context page did not open on the concept list (no 'Self scheduling' card)"
click_card "Self scheduling" || fail "step 1: could not click the Self scheduling card"
poll_vis Button "Open the Why band" || fail "step 1: no 'Open the Why band' button inside the concept"
d click --type Button --name "Open the Why band" >/dev/null 2>&1 || fail "step 1: could not click 'Open the Why band'"
poll_vis Button "New page" || fail "step 1: the Why drill shows no Pages and actions section with 'New page'"
d screenshot --out "$OUT/1-pages-section.png" >/dev/null 2>&1 || true

# Step 2 (click 1): New page opens the page sheet with an empty Name.
d click --type Button --name "New page" >/dev/null 2>&1 || fail "step 2: could not click 'New page'"
d wait-for --type TextBox --name "Name" --state visible --wait-ms 30000 >/dev/null 2>&1 || fail "step 2: the page sheet has no Name field"
d type --type TextBox --name "Name" --value "Week schedule" --replace >/dev/null 2>&1 || fail "step 2: could not type the page's name"
# A page is listed under a concept through its actions' ticks: one named action, related to Self scheduling (a page with no named
# action saves but sits under no concept, spec §3.6).
d type --type TextBox --name "Action name" --value "Submit week" --replace >/dev/null 2>&1 || fail "step 2: could not type the action's name"
d click --type CheckBox --name "Relates to Self scheduling" >/dev/null 2>&1 || fail "step 2: could not tick 'Relates to Self scheduling'"
poll_vis Button "Save and approve" || fail "step 2: the page sheet shows no 'Save and approve' button"
d screenshot --out "$OUT/2-page-sheet.png" >/dev/null 2>&1 || true

# Step 3 (click 3): Save and approve writes and approves; the new row is the owner's page, with nothing waiting.
d click --type Button --name "Save and approve" >/dev/null 2>&1 || fail "step 3: could not click 'Save and approve'"
poll_vis Button "Edit page: Week schedule" || fail "step 3: after Save and approve there is no 'Edit page: Week schedule' row"
vis Button "Save and approve" && fail "step 3: the sheet is still open after Save and approve"
d search --contains "Waiting for approval" --limit 20 2>/dev/null | jq -e 'any(.matches[]; .visible)' >/dev/null \
  && fail "step 3: a row reads 'Waiting for approval' after Save and approve (the approve did not land)"
d screenshot --out "$OUT/3-saved.png" >/dev/null 2>&1 || true
echo "PASS: Why drill -> New page -> name, one action ticked -> Save and approve (3 clicks) wrote the page and approved it ($OUT)"
