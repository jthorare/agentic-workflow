#!/usr/bin/env bash
# tests/ui-checks/concept-terms.sh — proof check for the concept card's top terms (ui #1036): each card shows its own concept's terms,
# read from GET /sindri/context/concepts/glossary (q110), with no "No terms yet." line when the glossary has terms.
# Expected: the Self scheduling card says "Terms: shift · swap · roster +1" (four terms: the first three and a count), the Billing card
# "Terms: invoice", and no "No terms yet." line. On main (the cards never read the glossary) it FAILs at step 1; on the branch it passes.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (context-lib.sh: sindri-ui-fixture.sh
# up + context up, then `context whole concepts`, whose concepts carry `terms` attributes). Every driver call has `timeout 120`.
#   bash tests/ui-checks/concept-terms.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init concept-terms

d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
ctext() { d search "$@" --limit 40 2>/dev/null | jq -r 'first(.matches[] | select(.visible)) | .text // empty'; }

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
d wait-for --type TextBlock --contains "Self scheduling" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "the Context page did not open on the concept list (no 'Self scheduling' card)"

# Step 1: each card shows its own concept's terms (the glossary is read right after the concepts).
d wait-for --type TextBlock --contains "Terms: shift" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || fail "step 1: the Self scheduling card shows no terms"
got="$(ctext --type TextBlock --contains "Terms: shift")"
[ "$got" = "Terms: shift · swap · roster +1" ] || fail "step 1: the Self scheduling card says '$got', expected 'Terms: shift · swap · roster +1'"
got="$(ctext --type TextBlock --contains "Terms: invoice")"
[ "$got" = "Terms: invoice" ] || fail "step 1: the Billing card says '$got', expected 'Terms: invoice'"

# Step 2: a glossary with terms says nothing over the cards.
[ -z "$(ctext --type TextBlock --contains "No terms yet")" ] || fail "step 2: 'No terms yet.' shows although the glossary has terms"
shot terms
echo "PASS: each concept card shows its own top terms from the glossary, and no 'No terms yet.' line"
