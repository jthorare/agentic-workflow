#!/usr/bin/env bash
# tests/ui-checks/projects-page.sh — the Projects page's screenshot run on the UI fixture (projects page spec §12 Ph5 proof, §13.1; #1566).
# The fixture (sindri-ui-fixture.sh up, context up, projects) seeds six Linear projects, their issues, the viewer (Sam Rivera) and the owner's own
# state (Billing migration attached, Hiring loop made by hand, Mobile offline sync hidden). The check opens the REAL sindri-ui on the Projects tab and:
#   1. waits for the Checkout rewrite card; asserts "Linear says you're Sam Rivera", Checkout rewrite shows 5 open issues, Billing migration and
#      Hiring loop are on the page, Mobile offline sync is not (it is hidden); screenshot projects-page.png
#   2. opens the Checkout rewrite card's sheet; screenshot projects-sheet.png; closes it
#   3. "+ Add project": the From Linear list names Platform API v3; screenshot projects-add-linear.png; By hand; screenshot projects-add-hand.png; closes it
#   4. opens Finished (Onboarding revamp); screenshot projects-finished.png
#   5. opens Hidden (Mobile offline sync); screenshot projects-hidden.png
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh). Every driver call has `timeout 120`.
#   bash tests/ui-checks/projects-page.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Run it guarded: scripts/run-test-guard.sh -- bash tests/ui-checks/projects-page.sh
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init projects-page

# Every driver call: timeout 120; a timeout (124) is a setup failure, any other exit is the caller's to read.
d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
# The visible nodes (any type) whose name or text contains $1, as JSON lines. Search finds nothing by --automation-id here, and --text is exact.
find_nodes() {
  d search --limit 50000 2>/dev/null | jq -c --arg w "$1" '.matches[] | select(.visible and (((.name // "") + " " + (.text // "")) | contains($w)))'
}
has() { [ -n "$(find_nodes "$1" | head -n 1)" ]; }
node_id() { find_nodes "$1" | jq -r '.id' | head -n 1; }
click_on() { # $1 a word of the node's name or text
  local id
  id="$(node_id "$1")"
  [ -n "$id" ] || return 1
  d click --node-id "$id" >/dev/null 2>&1
}
wait_text() { # $1 text, $2 what it is for
  local i
  for i in $(seq 1 60); do has "$1" && return 0; sleep 1; done
  fail "$2: no '$1' on screen after 60 s"
}
snap() { # $1 file stem
  d screenshot --out "$OUT/$1.png" >/dev/null 2>&1 || setup_fail "no screenshot for $1"
  echo "screenshot: $OUT/$1.png"
}
close_sheet() {
  d click --type Button --name "Close sheet" >/dev/null 2>&1 || d key --chord Escape >/dev/null 2>&1 || true
  sleep 1
}

# Start: the fixture with the Projects data, this checkout's debug sindri-ui under xvfb, the window, the Projects tab.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" projects >"$OUT/projects-up.log" 2>&1 || setup_fail "sindri-ui-fixture projects (see $OUT/projects-up.log)"
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$CTX_ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
setsid env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
d ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
d window --width 1280 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
d click --type ListBoxItem --name Projects >/dev/null 2>&1 || setup_fail "no Projects tab"

# Step 1: the page.
wait_text "Checkout rewrite" "step 1"
wait_text "Hiring loop" "step 1"
sleep 2
has "Linear says you're Sam Rivera" || fail "step 1: the freshness line does not say \"Linear says you're Sam Rivera\""
find_nodes "Checkout rewrite" | jq -r '.name // empty' | grep -q "5 open issues" || fail "step 1: the Checkout rewrite card does not show 5 open issues"
has "Billing migration" || fail "step 1: the attached project, Billing migration, is not on the page"
has "Hiring loop" || fail "step 1: the hand-made project, Hiring loop, is not on the page"
! has "Mobile offline sync" || fail "step 1: Mobile offline sync is on the page before Hidden is opened"
snap projects-page

# Step 2: the card's sheet.
click_on "Checkout rewrite, " || fail "step 2: could not click the Checkout rewrite card"
wait_text "Close sheet" "step 2 (the card's sheet)"
snap projects-sheet
close_sheet

# Step 3: Add project, From Linear then By hand.
click_on "+ Add project" || fail "step 3: no + Add project button"
wait_text "Platform API v3" "step 3 (the From Linear list)"
snap projects-add-linear
click_on "By hand" || fail "step 3: no By hand tab"
sleep 1
snap projects-add-hand
close_sheet

# Step 4: Finished.
click_on "Finished (" || fail "step 4: no Finished fold"
wait_text "Onboarding revamp" "step 4"
snap projects-finished

# Step 5: Hidden.
click_on "Hidden (" || fail "step 5: no Hidden fold"
wait_text "Mobile offline sync" "step 5"
snap projects-hidden

echo "PASS: the Projects page showed the viewer, the cards and counts, the sheet, both Add tabs, and the Finished and Hidden folds (screenshots in $OUT)"
