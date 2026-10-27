#!/usr/bin/env bash
# tests/ui-checks/ux-c2-search-question.sh — frozen regression check for the UX-pass finding C-2 ('Starts at' search cuts a question at its first space and shows CLI error text).
# Expected: a question with spaces is searched as asked; a miss says what to try in the app's own words, never a CLI hint such as '(fix: sindri context map status)'.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c2-search-question.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c2-search-question
ctx_start 1280 900

# A question with spaces whose words name nothing on the map: the text query finds no anchors and no suggestions, so the baseline
# splits it at its spaces and asks the lenses for the first word alone ("Tab groups" would match a node whole and never take this path).
q="zzqx wwvk"
"$DRIVER" type --name "Ask or look up" --value "$q" --replace >/dev/null 2>&1 || setup_fail "could not type into the search box"
"$DRIVER" key --chord Enter --name "Ask or look up" >/dev/null 2>&1 || setup_fail "Enter in the search box was not accepted"
sleep 4

cli="$(ctx_text --type TextBlock --contains "(fix:")"
[ -z "$cli" ] || fail "the question '$q' ended in CLI error text: '$cli'"
cut="$(ctx_text --type TextBlock --contains "is named 'zzqx'")"
[ -z "$cut" ] || fail "the question '$q' was cut at its first space: '$cut'"
blank="$(ctx_text --type TextBlock --text "No matches")"
[ -z "$blank" ] || fail "the failed question '$q' blanked the map (the page says 'No matches')"
echo "PASS: the question '$q' was searched whole, showed no CLI text and left the map drawn"
