#!/usr/bin/env bash
# tests/ui-checks/ux-c8-files-empty.sh — frozen regression check for the UX-pass finding C-8 (Files: a filter that matches nothing says 'Nothing in this folder.').
# Expected: a filter with no match says so, naming the filter; 'Nothing in this folder.' is for a folder that is empty.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c8-files-empty.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c8-files-empty
ctx_start 1280 900

"$DRIVER" click --type RadioButton --name Files >/dev/null 2>&1 || setup_fail "no Files view switch"
"$DRIVER" wait-for --type TextBlock --contains "files" --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "the Files view never showed its totals"
"$DRIVER" type --name "Filter by name" --value zzzz --replace >/dev/null 2>&1 || setup_fail "could not type into the filter"
sleep 2

empty="$(ctx_text --type TextBlock --text "Nothing in this folder.")"
[ -z "$empty" ] || fail "a filter that matches nothing says 'Nothing in this folder.'"
named="$(ctx_text --type TextBlock --contains zzzz)"
[ -n "$named" ] || fail "a filter that matches nothing shows no message that names the filter"
echo "PASS: the empty filter says '$named'"
