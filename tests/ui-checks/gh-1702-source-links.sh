#!/usr/bin/env bash
# tests/ui-checks/gh-1702-source-links.sh — check for #1702: the whole map's node panel opens a source at its real link.
# Expected (the issue): a Linear ticket node's panel offers "Open the ticket in the browser" (the record's Linear issue URL), and a node that rests on
# a repo file (here migrations/0001_staffing/migration.sql) offers "Open the file on GitHub in the browser" (the file's GitHub URL pinned to the map's
# commit), instead of the muted "No link to the source" line.
# The fixture (sindri-ui-fixture.sh up, context up, context sources) imports a small map of the tabs repo with the commit set to the repo's HEAD and
# an origin of https://github.com/acme/tabs.git, a doc node (feature:staffing.schema) and a ticket node (record:linear:issue/0e5a1b00-...-702) whose
# stored record has the Linear URL. The check opens the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) on the whole map and, per node:
#   1. picks the node in the 'Map items' list (Enter on its row: what a click on the map does), waits for its panel;
#   2. asserts the panel's source line (doc: ... migration.sql / ticket: ...), the open button visible with its accessible name, and no "No link" line;
#      screenshots gh-1702-doc-panel.png and gh-1702-ticket-panel.png;
#   3. asserts the URL: the button's link is not readable from the driver (the view model keeps it private), so the exact URL is read from the same
#      node as the app reads it, `sindri context show <key> --json` as the owner (GET /sindri/context/node, the app's own call), and compared with
#      https://github.com/acme/tabs/blob/<tabs repo HEAD>/migrations/0001_staffing/migration.sql and the stored Linear URL. A button on screen means the
#      app got a non-null link (HasLink), so the URL asserts and the button assert together say the panel opens that URL.
# Every driver call has `timeout 120`.
#   bash tests/ui-checks/gh-1702-source-links.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Run it guarded: scripts/run-test-guard.sh -- bash tests/ui-checks/gh-1702-source-links.sh
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init gh-1702-source-links

DOC_KEY="feature:staffing.schema"
DOC_LABEL="Staffing schema migration"
TICKET_KEY="record:linear:issue/0e5a1b00-0000-4000-8000-000000000702"
TICKET_LABEL="Roll out staffing"
TICKET_URL="https://linear.app/acme/issue/SPL-142/roll-out-staffing"
DOC_PATH="migrations/0001_staffing/migration.sql"
FILE_BUTTON="Open the file on GitHub in the browser"
TICKET_BUTTON="Open the ticket in the browser"
NO_LINK="No link to the source"

# Every driver call: timeout 120; a timeout (124) is a setup failure, any other exit is the caller's to read.
d() {
  timeout 120 "$DRIVER" "$@"
  local rc=$?
  [ "$rc" -ne 124 ] || setup_fail "the driver timed out: $*"
  return "$rc"
}
# The visible nodes (of type $2 when given) whose name or text contains $1, as JSON lines. Search finds nothing by --automation-id here, and --text is exact.
find_nodes() {
  d search --limit 50000 2>/dev/null | jq -c --arg w "$1" --arg t "${2:-}" '.matches[] | select(.visible and ($t == "" or .type == $t) and (((.name // "") + " " + (.text // "")) | contains($w)))'
}
has() { [ -n "$(find_nodes "$@" | head -n 1)" ]; }
node_id() { find_nodes "$@" | jq -r '.id' | head -n 1; }
wait_has() { # $1 text, $2 type or '', $3 what it is for
  local i
  for i in $(seq 1 30); do has "$1" "$2" && return 0; sleep 1; done
  fail "$3: no '$1' on screen after 30 s"
}
snap() { # $1 file stem
  d screenshot --out "$OUT/$1.png" >/dev/null 2>&1 || setup_fail "no screenshot for $1"
  echo "screenshot: $OUT/$1.png"
}
enabled() { # $1 the button's name: its IsEnabled property reads True (or is not reported)
  local id
  id="$(node_id "$1" Button)"
  [ -n "$id" ] && d props --node-id "$id" 2>/dev/null | jq -e '(.properties.IsEnabled // "True") | tostring | ascii_downcase == "true"' >/dev/null
}
# The node's source url as the app reads it: the owner's `context show --json`, the url member of the node's source.
served_url() { # $1 node key
  "$PROVE" human context show "$1" --json 2>/dev/null | sed -n '/^{/,$p' | jq -r '[.. | objects | select(has("url")) | .url] | first // empty'
}
# Picks a node in the 'Map items' list: Enter on its row does what a click on the map does (pins it, opens its panel).
pick() { # $1 label
  local id
  id="$(node_id "$1" ListBoxItem)"
  [ -n "$id" ] || fail "no '$1' row in the 'Map items' list"
  d key --chord Enter --node-id "$id" >/dev/null 2>&1 || setup_fail "could not press Enter on the '$1' row"
}

# Start: the fixture with the source data, this checkout's debug sindri-ui under xvfb, the window, the Context tab.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" context sources >"$OUT/context-sources.log" 2>&1 || setup_fail "sindri-ui-fixture context sources (see $OUT/context-sources.log)"
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
# A map with concepts opens on its concept cards (#1281): the Map toggle shows the whole map.
d wait-for --name Map --state visible --wait-ms 60000 >/dev/null 2>&1 && d click --name Map >/dev/null 2>&1
d wait-for --type TextBlock --contains "links" --state visible --wait-ms 60000 >/dev/null 2>&1 || setup_fail "the whole map never showed its size line"
sleep 1

REPO_DIR="$AW_SINDRI_HOME/ui-fixture-context/aw-split-panes"
HEAD_SHA="$(env -u GIT_DIR -u GIT_WORK_TREE git -C "$REPO_DIR" rev-parse HEAD 2>/dev/null)"
[ -n "$HEAD_SHA" ] || setup_fail "no HEAD in the fixture repo $REPO_DIR"
DOC_URL="https://github.com/acme/tabs/blob/$HEAD_SHA/$DOC_PATH"

# Step 1: the repo file. The doc node's panel shows its source and the GitHub button.
wait_has "$DOC_LABEL" ListBoxItem "step 1 (the doc node in the 'Map items' list)"
pick "$DOC_LABEL"
wait_has "doc:" TextBlock "step 1 (the doc node's source line)"
sleep 1
has "$DOC_PATH" TextBlock || fail "step 1: the source line of '$DOC_LABEL' does not name $DOC_PATH"
has "$FILE_BUTTON" Button || fail "step 1: the panel of '$DOC_LABEL' has no '$FILE_BUTTON' button (a repo file with a GitHub origin must open on GitHub)"
! has "$NO_LINK" || fail "step 1: the panel of '$DOC_LABEL' says '$NO_LINK' although the repo has a GitHub origin"
enabled "$FILE_BUTTON" || fail "step 1: the '$FILE_BUTTON' button is not enabled"
snap gh-1702-doc-panel
got="$(served_url "$DOC_KEY")"
[ "$got" = "$DOC_URL" ] || fail "step 1: the app is served '$got' for $DOC_KEY, not $DOC_URL"

# Step 2: the Linear ticket. The record node's panel shows the ticket source and the ticket button.
wait_has "$TICKET_LABEL" ListBoxItem "step 2 (the ticket node in the 'Map items' list)"
pick "$TICKET_LABEL"
for i in $(seq 1 30); do has "ticket:" TextBlock && break; sleep 1; done
has "ticket:" TextBlock || fail "step 2: the panel of '$TICKET_LABEL' shows no 'ticket:' source line"
sleep 1
has "$TICKET_BUTTON" Button || fail "step 2: the panel of '$TICKET_LABEL' has no '$TICKET_BUTTON' button (a stored Linear record must open in Linear)"
! has "$NO_LINK" || fail "step 2: the panel of '$TICKET_LABEL' says '$NO_LINK' although its record has a Linear URL"
enabled "$TICKET_BUTTON" || fail "step 2: the '$TICKET_BUTTON' button is not enabled"
snap gh-1702-ticket-panel
got="$(served_url "$TICKET_KEY")"
[ "$got" = "$TICKET_URL" ] || fail "step 2: the app is served '$got' for $TICKET_KEY, not $TICKET_URL"

echo "PASS: '$DOC_LABEL' offered '$FILE_BUTTON' at $DOC_URL and '$TICKET_LABEL' offered '$TICKET_BUTTON' at $TICKET_URL (screenshots in $OUT)"
