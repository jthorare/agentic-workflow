#!/usr/bin/env bash
# tests/ui-checks/ux-c4-clear-focus.sh — frozen regression check for the UX-pass finding C-4 (Clear search removes the focused button and every shortcut dies).
# Expected: after the keyboard user presses Clear search, keyboard focus is on an element (the search box), so a shortcut still reaches the app.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c4-clear-focus.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c4-clear-focus
ctx_start 1280 900

"$DRIVER" type --name "Ask or look up" --value "zzzz" --replace >/dev/null 2>&1 || setup_fail "could not type into the search box"
"$DRIVER" wait-for --type Button --name "Clear search" --state visible --wait-ms 5000 >/dev/null 2>&1 || setup_fail "no Clear search button after typing"
# A keyboard user Tabs to Clear search and presses Enter (click through the peer would not move focus, so focus it first).
"$DRIVER" action --type Button --name "Clear search" --do focus >/dev/null 2>&1 || setup_fail "Clear search cannot take focus"
"$DRIVER" key --chord Enter >/dev/null 2>&1 || setup_fail "Enter on Clear search was not accepted"
sleep 1

err="$("$DRIVER" key --chord Ctrl+K 2>&1 >/dev/null)" || fail "nothing has keyboard focus after Clear search ($err)"
echo "PASS: focus is on an element after Clear search"
