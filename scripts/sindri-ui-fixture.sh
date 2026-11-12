#!/usr/bin/env bash
# scripts/sindri-ui-fixture.sh — the Sindri UI's proof fixture (UI spec §18.2, first part: U1). It builds the state a
# UI phase is proven against in a guarded temp home, through scripts/sindri-prove.sh only (its guard runs before
# anything is written, so the owner's real home is never touched).
#
#   sindri-ui-fixture.sh up        write the live profile (a heartbeat service, port 0), validate it and
#                                  start the control plane (sindri --headless, through sindri-prove.sh human)
#   sindri-ui-fixture.sh stall     insert one row of a registered event type with an unparseable payload into
#                                  <home>/ledger.db: the projection throws and the views stall (the stalled banner)
#   sindri-ui-fixture.sh unstall   delete that row again (then Rebuild views recovers)
#   sindri-ui-fixture.sh drift     edit the live profile (edits apply on save)
#   sindri-ui-fixture.sh down      stop the control plane (sindri stop, as the owner)
#   sindri-ui-fixture.sh items [--large]
#                                  add the default fixture's 4 items (sindri item add, as the owner; U2, API P3);
#                                  --large adds 30 more for the fold test (U3 adds its 12 needs-you rows)
#   sindri-ui-fixture.sh close <item> [--cancelled]
#                                  close an item at the CLI (sindri item close, as the owner): with the app open on
#                                  the same item, the app's own Close then sends a stale version (SND-ITEM-409)
#
#   sindri-ui-fixture.sh request [--item ID] [--repo R] <text>
#                                  file a request as a session (a POST to /sindri/requests with no caller header, so the
#                                  API reads a session; --repo sends X-Sindri-Repo): a decision lands in Needs you (U3')
#   sindri-ui-fixture.sh todos     (run after 'up')
#                                  the todo panel's data (todo spec §10 Ph2): 11 todos added at the CLI as the owner (sindri todo add, through the
#                                  real routes): one overdue, two due today, three upcoming (tomorrow twice, a weekday, a MM-dd), three with no
#                                  date, links on four (one with a PR and a Linear issue), and the last one marked done. Each add carries a fixed
#                                  --client-id (ui-fixture-todo-NN), so a repeat adds nothing and marks nothing done twice
#   sindri-ui-fixture.sh decide <DEC-id> <choice>
#                                  decide at the CLI (sindri decide, as the owner): with the app open on the same
#                                  decision, the app's own Decide is then refused (SND-ITEM-409)
#   sindri-ui-fixture.sh resources (run after 'up')
#                                  the Resources page's data (#2258 PR18b, #2494): files under <home>/resources, which is all
#                                  GET /sindri/resources reads: workflow:morning-check gets a 1.2 MB database (state.db) and a
#                                  300 KB disk (scratch/), toolkit:notes a 40 KB disk (cache/). Rewritten to the same sizes on a repeat
#   sindri-ui-fixture.sh connections empty|typical|error|down  (run after 'up')
#                                  the Connections page's data (#2537): rows of the 'connection' table of <home>/ledger.db, which is all
#                                  GET /sindri/connections reads (the tile shows the label of the name as the account and last_refreshed_at
#                                  as "last used"). empty removes every row (the five tiles all offer Connect). typical writes github:sam-rivera,
#                                  linear:acme and google:sam-acme, connected, last used 30 minutes, 2 hours and 3 days ago, plus one custom
#                                  connection, billing-api:prod (rest, no preset, a probe URL on an .invalid host). error is typical with
#                                  linear:acme as needs-sign-in and the reason the refresh writes when the provider refuses the grant
#                                  ("the provider refused the refresh (invalid_grant)"). Rows are keyed by name and written with INSERT OR
#                                  REPLACE, so a repeat or a switch between states leaves one row each; the rows carry created_by
#                                  'ui-fixture' and down removes only those. No secret is stored anywhere: the rows point at
#                                  keychain:sindri-connection/<name> entries that do not exist, so the tiles read right but Test and a refresh
#                                  fail with SND-CONN-006 (no stored sign-in). It writes no Needs-you event for the error row: the tile reads
#                                  only the row. Run 'up' first (the table is made by the control plane's migrations). Needs sqlite3 or python3.
#
#   sindri-ui-fixture.sh fail      add a service the build cannot run to the live profile (a failure row)
#   sindri-ui-fixture.sh unfail    remove it again (the row clears)
#                                  (items --large also files 12 requests, in FIXTURE_REPO when set, else with no repo)
#   sindri-ui-fixture.sh hide-item <ITM-id>
#                                  delete the item's row from <home>/views.db (view_items): the control plane stays up and the run
#                                  still serves, but GET /sindri/items/<id> and /scope answer SND-ITEM-404 for it (gh-1046: the
#                                  Context page's "Could not read the context of <item>"). Derived state: 'views rebuild' brings it back
#   sindri-ui-fixture.sh context up|draft|down
#                                  the Context page's data (U9; run after 'up'): up writes a git repo, aw-split-panes, under
#                                  <home>/ui-fixture-context (the tabs repo of tests/Aw.Context.Testing/Fixtures/MapTabsRepo.cs),
#                                  adds it to the live profile's repos, then imports the tabs map
#                                  (map-tabs.json) as the owner, typing 'import map <hash6>', and prints the repo name
#                                  (the app's repo= argument); draft runs 'context map build' as the owner (a draft to
#                                  review; a missing graphify is a degraded line, not a failure); down removes the repo, the
#                                  map copy and the profile entry. Idempotent: a repeat up imports the map
#                                  only when the store has no map or the map file changed (the imported version stays in
#                                  context.db through down, so the next draft's number depends on earlier runs)
#
#   sindri-ui-fixture.sh context whole over|under|concepts|pages
#                                  the whole-map fixture (whole-map spec 8; run after 'context up'): over imports a generated map of 12
#                                  features with 50 code nodes each (612 nodes, over the 500-node budget: the Context page opens by feature),
#                                  under imports the tabs map again (10 nodes: the page draws every node). Each switch is a new map version
#                                  and a repeat with the same map imports nothing; concepts imports a small authored map with two concepts
#                                  (Self scheduling, Billing) over four features and a decision on Swaps, so the Context page opens on
#                                  its concept band and a concept draws its Why, product and code; pages is the concepts map plus the pages
#                                  Accept all suggested (#1724) needs: under Self scheduling two suggested pages (Roster, Leave requests, each
#                                  with one action that expresses the concept), and in no concept three more (Billing overview with an action,
#                                  whose name matches the Billing concept; Settings with no action; and the Shifts screen the concepts map has)
#
#   sindri-ui-fixture.sh context sources
#                                  the node panel's source links (#1702; run after 'context up'): gives the tabs repo a GitHub remote
#                                  (origin https://github.com/acme/tabs.git, set again on a repeat), imports a small authored map
#                                  (map-sources.json, with "commit" set to the tabs repo's HEAD) whose doc node, feature:staffing.schema,
#                                  rests on repo:aw-split-panes/migrations/0001_staffing/migration.sql, and whose Linear ticket node,
#                                  record:linear:issue/0e5a1b00-0000-4000-8000-000000000702 (team SPL), has a stored record (source_record,
#                                  url https://linear.app/acme/issue/SPL-142/roll-out-staffing). It adds context.sources.linear.teams [SPL]
#                                  to the live profile like 'projects' does. Prints three lines: the repo name, the doc node key and the
#                                  ticket node key. The migration file is not added to the repo: the link is built from the path and the
#                                  map's commit, never read from the tree
#
#   sindri-ui-fixture.sh context publish held|overdue|published|without-waiting|stale|by-hand|pause|resume
#                                  the Health page's Context map card (F1P-c, code index spec 6.4; run after 'context up'): puts the
#                                  map into one of the six publish states by writing the stored state the card reads, then prints the
#                                  words 'sindri context map status' should give. held writes a draft (copied heads) with a hold whose
#                                  wait just began into <home>/context/context.db and pauses the context-map-refresh service so no
#                                  tick publishes it; overdue does the same with the wait started an hour ago; published, without-waiting
#                                  (a 'context.map.published' FirstMap event), by-hand (a 'context.map.promoted' by human:fixture) and
#                                  stale (the current version's recorded head changed to 1234567, a header-only code index file at
#                                  abcdef0 under <home>/code-index so the real outlook reads it, and a TimedOut published event)
#                                  drop the hold and resume the service first; every other state removes the index file again;
#                                  pause and resume only toggle the service
#
#   sindri-ui-fixture.sh linear-catchup [empty|typical|error|read-error]
#                                  the Work > Linear "Since you last looked" strip (Linear hub spec 3, #2536; run after 'context up'; needs
#                                  the migrations through 0036). Seeds 25 SPL issues (audience linear:SPL, ids fxc-N, 3+ projects, priorities
#                                  1 to 4, titles up to 85 characters, 12 over 60) into <home>/context/context.db: linear_issue, linear_issue_change
#                                  (assignments to the viewer and state moves with names, via 'feed') and linear_comment (5 comments that
#                                  @-mention the viewer), 37 strip rows in all, spread over the last 71 hours, 10 issues with 2 or 3 of them.
#                                  typical (default) clears the marker; empty sets the marker to now (nothing new); error records a failed
#                                  Linear sync on the feed's source_cursor row (source 'linear', repo 'records': last_error SND-CONTEXT-071,
#                                  failures 3, last_sync 4 minutes ago, last_error_text 'Linear refused the key (401)'), rows kept: the
#                                  Reconnect card; read-error renames linear_issue_change so the strip's stored read fails (it says it could
#                                  not read, with Retry). The other states put the cursor and the table back. The marker lives in
#                                  <home>/ui/preferences.json (linearSince): restart the app after a run, it keeps preferences in memory.
#                                  Idempotent: a repeat replaces the fxc rows and puts a renamed table back; 'projects' leaves them alone
#
#   sindri-ui-fixture.sh projects  (run after 'context up')
#                                  the Projects page's data (projects page spec 4.1, 5, 12 Ph5, 13.1): seeds six Linear projects into
#                                  <home>/context/context.db (source_record, record_link; source 'linear-projects', audience linear:SPL, a
#                                  'led-by' link each): Checkout rewrite (in progress, at risk) and Search relevance v2 (planned), both led
#                                  by the viewer, Onboarding revamp (completed 3 days ago, led by the viewer: the Finished fold), Billing
#                                  migration (paused, Dana Okafor), Mobile offline sync and Platform API v3 (in progress, Lee Tran and Mira
#                                  Costa); open and closed Linear issues with 'in-project' links (the cards' open counts); the viewer's
#                                  'source_identity' row (Sam Rivera: "Linear says you're Sam Rivera"); and both Linear sources' cursor rows
#                                  (phase incremental), so the page is past its first read and not counting. It adds
#                                  context.sources.linear.teams [SPL] and context.sources.github.repos [acme/api, acme/web] (no token) to the
#                                  live profile (a human reads only the profile's teams). Three open GitHub PRs
#                                  (github:pr/acme/api/41, acme/api/42, acme/web/17, audience linear:SPL) are each linked 'relates' from a Checkout
#                                  rewrite issue, so that card shows 3 open PRs and the repos acme/api, acme/web. Then, as the owner: attaches Billing migration, adds a hand-made project (Hiring loop) and
#                                  hides Search relevance v2 (a project the viewer leads: Hide acts only on a project on the page), each once (the markers <home>/ui-fixture-projects-owner-attach|add|hide; the store rows are rewritten
#                                  every run, so a repeat adds no row and no project). The store is read when the page asks, so no rebuild is
#                                  needed; the owner's writes are ledger events and refresh the view. Exit 2 without a context store (run
#                                  'context up' first) or when the profile already has a context: section without the SPL team.
#
#   sindri-ui-fixture.sh reviews up|down  (run after 'context up')
#                                  a review page that opens on a fixture PR, for sending a fix from its fix sheet end to end
#                                  (POST /sindri/reviews/acme/api/<N>/fix; PR review page spec 8.2). up adds what projects adds to the live
#                                  profile (the SPL team, github repos acme/api and acme/web, and a github token pointer env:FIXTURE_GITHUB_TOKEN that nothing sets: without a token or a GitHub connection Reviews shows Connect GitHub and no rows, #2619), review.login fx-viewer and workflow.forge
#                                  (adapter file, hosts github.com), It builds a file-backed forge world inside the home,
#                                  <home>/ui-fixture-forge/state/forge/file (never the owner's $AW_STATE_DIR), with ONE open PR on
#                                  github.com/acme/api (branch feat/retry-budget, authored by fx-viewer so the fix posts a sindri.fix thread):
#                                  the changed file src/Sync.cs (3 added lines at 41-43), three check runs (lint failing) and one open thread
#                                  on src/Sync.cs; the world is rebuilt every run, so the PR is always #1. Then 3 PRs by other authors that
#                                  request fx-viewer's review (#2-#4: dana-okafor and kai-reviewer green, lee-park's test failing; REVIEWS_OTHERS),
#                                  so the Reviews list has a queue. It stores that PR as the Context
#                                  record github:pr/acme/api/<N> (audience linear:SPL, with a github cursor row), replacing the last run's,
#                                  and restarts the control plane with AW_STATE_DIR and AW_FORGE_LOGIN pointing at the forge state (the
#                                  real state's cache/bin, where serve finds Temporal, is linked into it), which every later restart keeps
#                                  while <home>/ui-fixture-forge exists. aw-forge-fake is built into <home>/ui-fixture-forge/bin (dotnet,
#                                  one project) or taken from AW_FORGE_FAKE (the command line, split on spaces). Prints three lines: the page
#                                  address (/sindri/reviews/acme/api/<N>), the PR's head sha and the file:line a line fix targets (src/Sync.cs:42).
#                                  The home must be under $TMPDIR (a proof-folder home is refused: the changed AW_STATE_DIR would fail
#                                  sindri-prove.sh's guard). 'projects' deletes every github:pr/acme/ row: run reviews up after it. down
#                                  removes the PR record, the forge world and the profile's review and forge entries, then restarts the
#                                  control plane when it is running. Needs jq and sqlite3 or python3.
#
#   sindri-ui-fixture.sh sessions up|ask <pid>|end <pid>|back <pid>|disable|enable|down
#                                  the Agents page's data (U11a; run after 'up'. Without it the control plane reads an empty
#                                  tree, <home>/ui-fixture-no-sessions/, so the Agents page shows no session). up copies tests/fixtures/sessions/
#                                  (the /proc-shaped tree and the Claude records) to <home>/ui-fixture-sessions/ with cp -a,
#                                  makes its fleet/ (0700, with the .aw-fleet marker), writes a bare launcher.json there and
#                                  restarts the control plane (stop, then --headless) with AW_SESSIONS_PROC_ROOT,
#                                  AW_SESSIONS_CLAUDE_DIR, AW_FLEET_DIR and AW_LAUNCHER_CONFIG pointing at the copy, so the
#                                  collector never reads the real /proc, ~/.claude, fleet or launcher.json; it needs the
#                                  fixture's ownUid (manifest.json; exit 2 otherwise). ask writes the fleet state file
#                                  (asking, approval, tool Bash) of the Claude session with that pid, the pid and start ticks
#                                  from its stat; end moves proc/<pid>/ to <copy>/ended/ and back moves it back (the row
#                                  leaves to Recently ended after two passes); disable and enable set sessions.enabled in
#                                  the copy's launcher.json and restart (SND-SESSION-002); down stops the control plane and
#                                  removes the copy. Every start exports the four variables (the copy while it exists, else the empty tree), and
#                                  down never restarts. Needs jq.
#
#   sindri-ui-fixture.sh model-calls fell-back|no-answer|paused|none
#                                  a job with model calls for the Jobs page and the job sheet (context model routing spec 8.2, #1567),
#                                  written as ledger rows the jobs view folds (job.started, job.progress, context.model.called and,
#                                  for paused, job.waiting with signal model-limit): fell-back is a running job with six calls (a
#                                  limited harness, a skipped one, a failed one, a Jev label pick, a product-surface pick and a pick
#                                  that was not asked: one call fell back to Codex); no-answer is three calls no harness answered;
#                                  paused is a job parked on model-limit with its progress line, so its row shows Resume; none is a
#                                  running job that made no call. Each state is its own job (fixed ids, INSERT OR IGNORE, a rerun adds
#                                  nothing); prints the job id. Needs jq and sqlite3 or python3.
#   sindri-ui-fixture.sh sandbox blocked|clear
#   sindri-ui-fixture.sh sandbox denials|denials-many|waiting|limit-memory|limit-wall|learning|takeover|attended <ITM-id> <instance-address>
#                                  the sandbox surfaces' states (sandbox spec section 8, States; goldens tests/Aw.Sindri.Tests/Golden/Sandbox/).
#                                  blocked and clear need a live answer from the control plane (/sindri/sandbox/status and /check), which
#                                  ledger rows can't fake, so they restart serve (as 'sessions up' does) with AW_SANDBOX_BWRAP=/nonexistent
#                                  (blocked: no bubblewrap, the Health row, the Settings section and the park card show the first blocker) or
#                                  with it unset (clear: needs a working bwrap, nothing blocks). The setting is remembered in
#                                  <home>/ui-fixture-sandbox/bwrap, so every later restart keeps it. blocked starts no run: it only
#                                  turns the blocker on. Run after 'sessions up'; needs jq.
#                                  The other states are ledger rows on a launch of an existing item (see 'items'), written as ledger rows (fixed ids, INSERT OR IGNORE, a rerun adds nothing): every one starts with the
#                                  launch.started row of events/launch-started-sandbox.json (all but waiting). denials: sandbox.denied x10 for github.com
#                                  (some) and one blocked path; denials-many: the overflow row too (top 5 of n, with a fix of every kind);
#                                  waiting: launch.waiting for a full session cap (3 of 3, SND-LAUNCH-020) and no launch.started, which
#                                  would end the wait; limit-memory and limit-wall: sandbox.limit-hit and
#                                  session.ended with reason limit (12GiB, 40m); learning: launch.started with networkMode learn;
#                                  takeover: launch.takeover-opened on the sandboxed run; attended: a launch.started in open mode with
#                                  no instance. The Agents sheet line needs the session row to carry the sandbox summary, which the
#                                  control plane serves from the launch's plan file, not from these rows.
#
# FIXTURE_REPO names the items' repo (default: none, so the API's default applies).
# Environment: AW_SINDRI_HOME (required: sindri-prove.sh's guard, a temp or proof folder), SINDRI (the built C# CLI,
# as sindri-prove.sh reads it), STALL_TYPE (the stalled row's type; default service.ran, a type the projector reads),
# SINDRI_UI_FIXTURE_SQL=python (use python3's sqlite3 module even when the sqlite3 CLI is installed; without the CLI it is
# the fallback anyway). Needs sqlite3 or python3 for stall and unstall, jq for up.
# Exit: 0 done; 2 the guard, a usage error, a missing tool or (context up) a refused map import; otherwise the failing step's exit code.
# Tests: bash scripts/tests/sindri-ui-fixture.test.sh
set -uo pipefail
# The fixture's profile is always <guarded home>/profile/profile.yaml: an exported AW_SINDRI_PROFILE (the owner's real
# profile, .env.example) must not be edited or read by any verb or by the CLI calls through sindri-prove.sh.
unset AW_SINDRI_PROFILE

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROVE="$ROOT/scripts/sindri-prove.sh"
STALL_KEY="sindri-ui-fixture:stall"
STALL_ID="7f3c1a52-0b6e-4d1e-9a55-3c2f6d8e1b04"
STALL_CORRELATION="7f3c1a52-0b6e-4d1e-9a55-3c2f6d8e1b05"

die() { echo "sindri-ui-fixture: $*" >&2; exit 2; }
usage() { awk 'NR >= 5 { if (/^#/) print; else exit }' "$0" | sed 's/^# \{0,1\}//'; }

# sindri-prove.sh's guard alone: it runs before any verb, and SINDRI=true makes the verb a no-op.
guard() { SINDRI=true "$PROVE" guard >/dev/null || exit 2; }

need() { command -v "$1" >/dev/null 2>&1 || die "$1 is needed for '$verb'"; }

# The sqlite3 CLI when there is one, else python3's sqlite3 module (a box without the CLI). Both run the statements in
# order, stop at the first error and print each selected row with its columns joined by '|'.
use_cli() { [ "${SINDRI_UI_FIXTURE_SQL:-}" != python ] && command -v sqlite3 >/dev/null 2>&1; }
need_sql() { use_cli || { command -v python3 >/dev/null 2>&1 && python3 -I -c 'import sqlite3' >/dev/null 2>&1; } || die "sqlite3 (or python3) is needed for '$verb'"; }

# $1 db file, $2 statements.
sql_on() {
  if use_cli; then sqlite3 -bail "$1" "$2"; else python_sql "$1" "$2"; fi
}

ledger() {
  [ -f "$AW_SINDRI_HOME/ledger.db" ] || die "no ledger at $AW_SINDRI_HOME/ledger.db: run 'up' first"
  sql_on "$AW_SINDRI_HOME/ledger.db" "$1"
}

python_sql() {
  python3 -I -c '
import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
pending = ""
for part in sys.argv[2].split(";"):
    pending += part
    if not sqlite3.complete_statement(pending + ";"):
        pending += ";"
        continue
    for row in con.execute(pending):
        print("|".join("" if v is None else str(v) for v in row))
    pending = ""
' "$1" "$2"
}

profile_path() { printf '%s\n' "$AW_SINDRI_HOME/profile/profile.yaml"; }

up() {
  need jq
  local profile hash
  profile="$(profile_path)"
  mkdir -p "$(dirname "$profile")"
  cat > "$profile" <<'YAML'
schemaVersion: 1
user: fixture
services:
  heartbeat: { every: 5s }
api: { port: 0 }
YAML
  approve || return
  # The collector reads the box's real /proc, ~/.claude/sessions, fleet and launcher.json unless all four overrides point into the home.
  sessions_export || return
  "$PROVE" human --headless
}

# Validates the live profile. Edits apply on save: there is no approval step.
approve() {
  local hash
  hash="$("$PROVE" profile validate --json | jq -r '.hash // empty')"
  [ -n "$hash" ] || die "profile validate gave no hash"
}

# A service the build has no code for: the scheduler reports its failure, and Needs you shows a failure row.
fail() {
  need jq
  local profile
  profile="$(profile_path)"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  if ! grep -q 'broken:' "$profile"; then
    awk '{print} /^  heartbeat:/ {print "  broken: { every: 5s }"}' "$profile" > "$profile.new" && mv "$profile.new" "$profile"
  fi
  approve
}

unfail() {
  need jq
  local profile
  profile="$(profile_path)"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  grep -v '^  broken:' "$profile" > "$profile.new"; mv "$profile.new" "$profile"
  approve
}

# A session's request: no X-Sindri-Caller, so the API reads a session.
request() {
  need jq
  local item="" repo="" url body
  while [ $# -gt 1 ]; do
    case "$1" in
      --item) item="$2"; shift 2 ;;
      --repo) repo="$2"; shift 2 ;;
      *) break ;;
    esac
  done
  [ $# -eq 1 ] && [ -n "$1" ] || die "usage: request [--item ID] [--repo R] <text>"
  if [ -n "$item" ] && ! [[ "$item" =~ ^[A-Za-z]+-[A-Za-z0-9]+$ ]]; then die "request: '$item' is not an item id"; fi
  if [ -n "$repo" ] && ! [[ "$repo" =~ ^[A-Za-z0-9._-]+$ ]]; then die "request: '$repo' is not a repo name"; fi
  url="$("$PROVE" url)" || return
  body="$(jq -nc --arg text "$1" --arg item "$item" '{kind: "rescope", text: $text} + (if $item == "" then {} else {item: $item} end)')"
  if [ -n "$repo" ]; then
    curl -sS --fail-with-body -X POST -H 'Content-Type: application/json' -H "X-Sindri-Repo: $repo" -d "$body" "$url/sindri/requests"
  else
    curl -sS --fail-with-body -X POST -H 'Content-Type: application/json' -d "$body" "$url/sindri/requests"
  fi
}

decide() {
  [[ "$1" =~ ^[A-Za-z]+-[A-Za-z0-9]+$ ]] || die "decide: '$1' is not a decision id"
  [[ "$2" =~ ^[A-Za-z0-9._-]+$ ]] || die "decide: '$2' is not a choice"
  "$PROVE" human decide "$1" "$2"
}

# One row, the same one every time (INSERT OR IGNORE on its idempotency key), at the ledger's current epoch. The ledger's
# reader takes only GUIDs for id and correlation_id and the 7-digit round-trip timestamp for occurred_at, or the row
# is rejected, the projection never stalls and /events answers 500.
stall() {
  need_sql
  local type="${STALL_TYPE:-service.ran}"
  case "$type" in *[!a-z0-9._-]*) die "STALL_TYPE must be an event type (a-z, 0-9, . _ -): $type" ;; esac
  ledger "INSERT OR IGNORE INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
          VALUES ('$STALL_ID', '$type', 1, strftime('%Y-%m-%dT%H:%M:%S', 'now') || '.0000000Z', 'prover:fixture', 'local', 0, '$STALL_CORRELATION',
                  COALESCE((SELECT MAX(epoch) FROM events), 0), '$STALL_KEY', '{not json');
          SELECT seq || ' ' || type FROM events WHERE idempotency_key = '$STALL_KEY';"
}

unstall() {
  need_sql
  ledger "DELETE FROM events WHERE idempotency_key = '$STALL_KEY'; SELECT changes() || ' row(s) removed';"
}

# A changed cadence changes the profile's hash (comments and key order don't), so the live profile differs from the one the app loaded.
drift() {
  local profile
  profile="$(profile_path)"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  if grep -q 'every: 5s' "$profile"; then
    sed -i.bak 's/every: 5s/every: 7s/' "$profile"
  else
    sed -i.bak 's/every: 7s/every: 5s/' "$profile"
  fi
  rm -f "$profile.bak"
  grep 'every:' "$profile"
}

down() { "$PROVE" human stop; }

# The default fixture's items (UI spec §16's queue, U2's spec-shaped queue/fixture.json), oldest first.
ITEMS=("Stream limits 429 fallback" "Retry storm in outbox dispatcher" "Context map review for tabs.layout" "Try the queue")

add() {
  if [ -n "${FIXTURE_REPO:-}" ]; then "$PROVE" human item add "$1" --repo "$FIXTURE_REPO"; else "$PROVE" human item add "$1"; fi
}

items() {
  local title n
  for title in "${ITEMS[@]}"; do add "$title" || return; done
  [ "${1:-}" = "--large" ] || return 0
  for n in $(seq 1 30); do add "Large fixture item $n" || return; done
  # 12 requests (U3's large Needs-you list), filed as a session. The API refuses a repo the profile lacks and the
  # fixture's profile has none, so they name FIXTURE_REPO when it is set (a repo the profile has) and no repo otherwise.
  for n in $(seq 1 12); do
    if [ -n "${FIXTURE_REPO:-}" ]; then
      request --repo "$FIXTURE_REPO" "Large fixture request $n" || return
    else
      request "Large fixture request $n" || return
    fi
  done
}

# The todo panel's 11 todos: "client id|text|due word or date|links, comma-separated" (a past date is overdue for any run).
TODOS=(
  "01|renew the staging cert|2020-01-01|"
  "02|reply to Sam about the offsite|today|"
  "03|check the flaky sync test|today|"
  "04|rotate the staging key|tomorrow|acme/api#12,ENG-12"
  "05|answer the review comments|mon|acme/web#40"
  "06|book the offsite room|12-20|"
  "07|call the dentist||"
  "08|read the sandbox spec||"
  "09|plan next week||ENG-34"
  "10|file the expense report|tomorrow|"
  "11|update the runbook|2020-01-02|"
)

# Add one todo through the real CLI route. The client id makes a repeat a no-op; prints the todo id.
todo_add() { # $1 client id, $2 text, $3 due, $4 links
  local args=(todo add "$2" --client-id "ui-fixture-todo-$1" --json) link
  [ -z "$3" ] || args+=(--due "$3")
  if [ -n "$4" ]; then
    IFS=',' read -ra links <<< "$4"
    for link in "${links[@]}"; do args+=(--link "$link"); done
  fi
  "$PROVE" human "${args[@]}" | jq -r '.id'
}

todos() {
  local row id cid text due links done_id=""
  for row in "${TODOS[@]}"; do
    IFS='|' read -r cid text due links <<< "$row"
    id="$(todo_add "$cid" "$text" "$due" "$links")" || return
    [ -n "$id" ] && [ "$id" != null ] || die "todo add printed no todo id for '$text'"
    [ "$cid" = 11 ] && done_id="$id"
  done
  # The last one is the done one; marking it twice would fail, so it's marked only while it is still open.
  if "$PROVE" human todos --json --no-fence | jq -e --arg id "$done_id" '.todos[] | select(.id == $id)' >/dev/null; then
    "$PROVE" human todo done "$done_id" || return
  fi
}

resources() {
  local root="$AW_SINDRI_HOME/resources"
  # Sizes in bytes: the page shows them as 1.2 MB, 300 KB and 40 KB.
  seed_file() { mkdir -p "$(dirname "$1")" && head -c "$2" /dev/zero > "$1" || die "resources: can't write $1"; }
  seed_file "$root/workflow/morning-check/state.db" 1258291
  seed_file "$root/workflow/morning-check/scratch/notes.txt" 307200
  seed_file "$root/toolkit/notes/cache/index.bin" 40960
}

# A UTC timestamp N seconds ago in the store's text form (GNU date -d, else BSD date -r).
ts_ago() {
  local at=$(( $(date -u +%s) - $1 ))
  date -u -d "@$at" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -r "$at" +%Y-%m-%dT%H:%M:%SZ
}

# One connection row: name kind preset|- config-json status reason|- refreshed-seconds-ago|-. The pointer names a keychain
# entry nobody wrote: the fixture never stores a secret.
conn_row() {
  local name="$1" kind="$2" preset="$3" config="$4" status="$5" reason="$6" ago="$7"
  local now pointer refreshed
  now="$(ts_ago 0)"
  pointer="keychain:sindri-connection/${name/:/.}"
  [ "$preset" = - ] && preset="NULL" || preset="'$preset'"
  [ "$reason" = - ] && reason="NULL" || reason="'$reason'"
  [ "$ago" = - ] && refreshed="NULL" || refreshed="'$(ts_ago "$ago")'"
  printf "INSERT OR REPLACE INTO connection (name, kind, preset, config_json, secret_pointer, status, status_reason, created_by, created_at, updated_at, last_refreshed_at) VALUES ('%s', '%s', %s, '%s', '%s', '%s', %s, 'ui-fixture', '%s', '%s', %s);" \
    "$name" "$kind" "$preset" "$config" "$pointer" "$status" "$reason" "$now" "$now" "$refreshed"
}

connections() {
  need_sql
  local state="$1" sql
  [ -f "$AW_SINDRI_HOME/ledger.db" ] || die "no ledger at $AW_SINDRI_HOME/ledger.db: run 'up' first"
  [ "$(sql_on "$AW_SINDRI_HOME/ledger.db" "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'connection';")" = 1 ] \
    || die "connections: the ledger has no connection table yet (the control plane makes it: run 'up' first)"
  case "$state" in
    empty) sql="DELETE FROM connection;" ;;
    down) sql="DELETE FROM connection WHERE created_by = 'ui-fixture';" ;;
    typical | error)
      local linear=connected linear_reason=-
      if [ "$state" = error ]; then linear=needs-sign-in; linear_reason="the provider refused the refresh (invalid_grant)"; fi
      sql="$(conn_row github:sam-rivera oauth github '{"clientId":"fixture-client-id"}' connected - 1800)
$(conn_row linear:acme oauth linear '{"clientId":"fixture-client-id"}' "$linear" "$linear_reason" 7200)
$(conn_row google:sam-acme oauth google '{"clientId":"fixture-client-id"}' connected - 259200)
$(conn_row billing-api:prod rest - '{"probeUrl":"https://billing.example.invalid/health"}' connected - -)" ;;
  esac
  sql_on "$AW_SINDRI_HOME/ledger.db" "$sql SELECT COUNT(*) || ' connection row(s)' FROM connection;"
}

close() {
  [[ "$1" =~ ^[A-Za-z]+-[A-Za-z0-9]+$ ]] || die "close: '$1' is not an item id"
  if [ "${2:-}" = "--cancelled" ]; then "$PROVE" human item close "$1" --cancelled; else "$PROVE" human item close "$1"; fi
}

hide_item() {
  need_sql
  local db="$AW_SINDRI_HOME/views.db"
  [ -f "$db" ] || die "no views at $db: run 'up' first"
  sql_on "$db" "DELETE FROM view_items WHERE key = '$1'; SELECT changes() || ' row(s) removed';"
}

CONTEXT_REPO="aw-split-panes"
context_dir() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-context"; }

# The tabs repo: the files and the one commit of MapTabsRepo, with a fixed author and date (so the sha is the same every time).
context_repo() {
  local dir="$1"
  [ ! -d "$dir/.git" ] || return 0
  need git
  mkdir -p "$dir/src/tabs" "$dir/src/layout" || return
  printf 'export class TabManager {\n  restoreLayout(): void {}\n}\n' > "$dir/src/tabs/TabManager.ts"
  printf 'export class TabGroups {\n  groupSelected(): void {}\n}\n' > "$dir/src/tabs/TabGroups.ts"
  printf 'export const pinned: string[] = [];\n' > "$dir/src/tabs/Pinned.ts"
  printf 'export class Grid {}\n' > "$dir/src/layout/Grid.ts"
  printf '# Tabs\n' > "$dir/README.md"
  printf 'PRIVATE=1\n' > "$dir/.env"
  local g=(env -u GIT_DIR -u GIT_WORK_TREE -u GIT_INDEX_FILE -u GIT_COMMON_DIR GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1 GIT_AUTHOR_NAME="Fixture Author" GIT_AUTHOR_EMAIL=fixture@example.invalid
     GIT_COMMITTER_NAME="Fixture Author" GIT_COMMITTER_EMAIL=fixture@example.invalid
     GIT_AUTHOR_DATE="@1782896400 +0000" GIT_COMMITTER_DATE="@1782896400 +0000" git -C "$dir")
  "${g[@]}" init -q -b main && "${g[@]}" config core.hooksPath "$dir/.no-hooks" && "${g[@]}" add -A \
    && "${g[@]}" commit -q --no-gpg-sign -m "Tabs fixture" || die "context up: can't build the repo in $dir"
}

# Adds the repo to the live profile's repos: (a repos: block after user: when there is none).
context_profile_add() {
  local profile="$1" dir="$2"
  grep -q "name: $CONTEXT_REPO" "$profile" && return 0
  if grep -q '^repos:' "$profile"; then
    awk -v e="  - { name: $CONTEXT_REPO, path: $dir }" '{print} /^repos:/ {print e}' "$profile" > "$profile.new"
  else
    awk -v e="repos:\n  - { name: $CONTEXT_REPO, path: $dir }" '{print} /^user:/ {print e}' "$profile" > "$profile.new"
  fi && mv "$profile.new" "$profile"
}

context_up() {
  need jq
  local profile dir map hash out marker
  profile="$(profile_path)"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  dir="$(context_dir)/$CONTEXT_REPO"
  context_repo "$dir" || return
  if ! grep -q "name: $CONTEXT_REPO" "$profile"; then
    context_profile_add "$profile" "$dir" || die "context up: can't add the repo to $profile"
  fi
  approve >&2 || return
  map="$(context_dir)/tabs-map.json"
  cp "$ROOT/tests/Aw.Context.Testing/Fixtures/map-tabs.json" "$map" || die "context up: can't copy the tabs map"
  context_import "$map" || return
  printf '%s\n' "$CONTEXT_REPO"
}

# Imports a map file as the owner, typing 'import map <hash6>'. Each import makes a new map version, so it runs only when the
# last import was another file (the marker holds its hash) or the store has no map. Exit 2 when the import is refused.
context_import() {
  local map="$1" hash out marker
  hash="$(sha256sum "$map" 2>/dev/null | cut -d' ' -f1)"
  [ -n "$hash" ] || hash="$(shasum -a 256 "$map" | cut -d' ' -f1)"
  [ -n "$hash" ] || die "context: no sha256sum or shasum"
  marker="$AW_SINDRI_HOME/ui-fixture-context-imported"
  if [ "$(cat "$marker" 2>/dev/null)" = "$hash" ] && "$PROVE" human context map status --json >/dev/null 2>&1; then
    return 0
  fi
  # The challenge is 'import map ' and the first six characters of the file's sha-256 (the content hash the CLI shows).
  if ! out="$(printf 'import map %s\n' "${hash:0:6}" | "$PROVE" human context map import "$map" 2>&1)"; then
    die "context: the map import was refused: $out"
  fi
  printf '%s\n' "$hash" > "$marker"
}

# The whole-map fixture (whole-map spec 8): 'over' imports a generated map of 12 features with 50 code nodes each (612 nodes, over
# the 500-node budget, so the whole map opens by feature); 'under' imports the tabs map (10 nodes, drawn node by node).
context_whole() {
  need jq
  local dir map
  dir="$(context_dir)"
  [ -d "$dir/$CONTEXT_REPO/.git" ] || die "context whole: run 'context up' first"
  case "$1" in
    under) map="$dir/tabs-map.json"; cp "$ROOT/tests/Aw.Context.Testing/Fixtures/map-tabs.json" "$map" || die "context whole: can't copy the tabs map" ;;
    concepts | pages)
      map="$dir/concepts-map.json"
      # A decision's node is implied by its row: its key is 'decision:' and the sha256 of the record, a newline and the quote (DecisionRow.KeyFor).
      # A concept opens only its own members, so the decision belongs to Self scheduling as well as deciding Swaps (ConceptScope).
      drec="repo:fixture/docs/decisions.md"; dquote="swaps close a day before the shift"
      dkey="decision:$(printf '%s\n%s' "$drec" "$dquote" | sha256sum | cut -c1-64)"
      jq -n --arg repo "$CONTEXT_REPO" --arg drec "$drec" --arg dquote "$dquote" --arg dkey "$dkey" '{schemaVersion: 1, kind: "authored", repo: $repo,
        decisions: [{record: $drec, quote: $dquote, wording: "Swaps close a day before the shift.", at: "2026-10-01T10:00:00Z"}],
        nodes: [
          {key: "concept:scheduling", label: "Self scheduling", attributes: {summary: "People pick and swap shifts.", source: "derived", seed: "feature:shifts", terms: ["shift", "swap", "roster", "rota"]}},
          {key: "concept:billing", label: "Billing", attributes: {summary: "Invoices and payment.", source: "override", terms: ["invoice"]}},
          {key: "feature:shifts", label: "Shifts"}, {key: "feature:swaps", label: "Swaps"}, {key: "feature:invoices", label: "Invoices"}, {key: "feature:lone", label: "Loose end"},
          {key: "surface:screen/shifts", label: "Shifts screen"}, {key: "action:pick", label: "Pick a shift"},
          {key: "code:src/shifts.ts", label: "shifts.ts", provenance: "deterministic"}, {key: "code:src/invoices.ts", label: "invoices.ts", provenance: "deterministic"}],
        edges: [
          {from: "feature:shifts", to: "concept:scheduling", type: "belongs-to"}, {from: "feature:swaps", to: "concept:scheduling", type: "belongs-to"},
          {from: "feature:invoices", to: "concept:billing", type: "belongs-to"},
          {from: "code:src/shifts.ts", to: "feature:shifts", type: "implements", provenance: "deterministic"},
          {from: "code:src/invoices.ts", to: "feature:invoices", type: "implements", provenance: "deterministic"},
          {from: "feature:invoices", to: "feature:shifts", type: "depends-on"}, {from: "feature:swaps", to: "feature:invoices", type: "depends-on"},
          {from: "surface:screen/shifts", to: "feature:shifts", type: "part-of"}, {from: "action:pick", to: "surface:screen/shifts", type: "on"},
          {from: $dkey, to: "concept:scheduling", type: "belongs-to"}, {from: $dkey, to: "feature:swaps", type: "decides"}]}' > "$map" || die "context whole: can't write the concepts map"
      if [ "$1" = pages ]; then
        # Pages for Accept all suggested: an action that expresses a concept puts its page under it; a page no action expresses a concept for sits in no concept.
        jq '.nodes += [
              {key: "surface:screen/roster", label: "Roster"}, {key: "action:view-roster", label: "View roster"},
              {key: "surface:screen/leave", label: "Leave requests"}, {key: "action:request-leave", label: "Request leave"},
              {key: "surface:screen/billing-overview", label: "Billing overview"}, {key: "action:pay-invoice", label: "Pay invoice"},
              {key: "surface:screen/settings", label: "Settings"}]
            | .edges += [
              {from: "action:view-roster", to: "surface:screen/roster", type: "on"}, {from: "action:view-roster", to: "concept:scheduling", type: "expresses"},
              {from: "action:request-leave", to: "surface:screen/leave", type: "on"}, {from: "action:request-leave", to: "concept:scheduling", type: "expresses"},
              {from: "action:pay-invoice", to: "surface:screen/billing-overview", type: "on"}]' "$map" > "$map.pages" \
          && mv "$map.pages" "$map" || die "context whole: can't add the pages to the concepts map"
      fi ;;
    over)
      map="$dir/over-map.json"
      jq -n --arg repo "$CONTEXT_REPO" '{schemaVersion: 1, kind: "authored", repo: $repo,
        nodes: [(range(0; 12) as $a | {key: "feature:area\($a)", label: "Area \($a)"}), (range(0; 12) as $a | range(0; 50) as $c
          | {key: "code:src/area\($a)/File\($c).ts", label: "File\($c).ts", provenance: "deterministic"})],
        edges: [range(0; 12) as $a | range(0; 50) as $c
          | {from: "code:src/area\($a)/File\($c).ts", to: "feature:area\($a)", type: "implements"}]}' > "$map" || die "context whole: can't write the over-budget map" ;;
  esac
  context_import "$map" || return
  printf '%s\n' "$1"
}

context_draft() { "$PROVE" human context map build; }

context_down() {
  local dir profile
  dir="$(context_dir)"
  case "$dir" in */ui-fixture-context) [ ! -d "$dir" ] || rm -rf -- "$dir" ;; *) die "context down: refusing to delete $dir" ;; esac
  index_clear
  profile="$(profile_path)"
  if [ -f "$profile" ] && grep -q "name: $CONTEXT_REPO" "$profile"; then
    grep -v "name: $CONTEXT_REPO" "$profile" | awk '{l[NR]=$0} END {for (i=1;i<=NR;i++) if (!(l[i]=="repos:" && (i==NR || l[i+1] !~ /^  /))) print l[i]}' > "$profile.new" \
      && mv "$profile.new" "$profile"
  fi
  return 0
}

# --- context publish: the six states of the Health page's Context map card (F1P-c, code index spec 6.4) ---
PUBLISH_SERVICE="context-map-refresh"

context_db() { printf '%s\n' "$AW_SINDRI_HOME/context/context.db"; }

# One ledger row of a registered map event for the current version, the same row every time (INSERT OR IGNORE on its key).
# $1 key suffix, $2 type, $3 actor, $4 payload JSON. The id and correlation id are GUIDs and the time has seven fractional digits (see stall).
publish_event() {
  local key="sindri-ui-fixture:publish:$1:v$version" id corr
  id="$(printf '%s' "$key" | cksum | awk '{printf "5c0f1e00-%04x-4d1e-9a55-3c2f6d8e1b04", $1 % 65536}')"
  corr="${id%?}5"
  ledger "INSERT OR IGNORE INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
          VALUES ('$id', '$2', 1, strftime('%Y-%m-%dT%H:%M:%S', 'now') || '.0000000Z', '$3', 'local', 0, '$corr',
                  COALESCE((SELECT MAX(epoch) FROM events), 0), '$key', '$4');" >/dev/null
}

# The stale state's code index (code index spec 6.4): a header-only index file (the meta and layers tables, user_version 1) and its 0600 pointer, in
# the layout the real store reads ($AW_SINDRI_HOME/code-index/<repo>.<ulid>.db and <repo>.current), so the real outlook path reads the commit.
# The ULID is this fixture's own, so clearing removes only what it wrote.
INDEX_ULID="0123456789ABCDEFGHJKMNPQRS"
INDEX_COMMIT="abcdef0000000000000000000000000000000000"
index_dir() { printf '%s\n' "$AW_SINDRI_HOME/code-index"; }

index_write() { # $1 repo
  local dir db
  dir="$(index_dir)"
  db="$dir/$1.$INDEX_ULID.db"
  mkdir -p -m 700 "$dir" && rm -f -- "$db" || return
  sql_on "$db" "PRAGMA user_version = 1;
    CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
    CREATE TABLE layers (layer TEXT PRIMARY KEY, stamp TEXT NOT NULL, status TEXT NOT NULL, code TEXT NULL, detail TEXT NOT NULL, built_at TEXT NOT NULL);
    INSERT INTO meta VALUES ('commit', '$INDEX_COMMIT'), ('default_branch', 'main'), ('built_at', strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
      ('schema', '1'), ('parser_id', 'fixture'), ('files', '0'), ('symbols', '0'), ('edges', '0');" >/dev/null || return
  chmod 600 "$db" && (umask 077; printf '%s\n' "$INDEX_ULID" > "$dir/$1.current")
}

# Removes the index files index_write made, and a pointer only while it still names this fixture's file.
index_clear() {
  local dir f
  dir="$(index_dir)"
  [ -d "$dir" ] || return 0
  for f in "$dir"/*."$INDEX_ULID".db; do [ ! -e "$f" ] || rm -f -- "$f"; done
  for f in "$dir"/*.current; do
    [ ! -f "$f" ] || [ "$(cat "$f")" != "$INDEX_ULID" ] || rm -f -- "$f"
  done
}

# Removes the hold, its chain, the draft and the map events this fixture wrote; the current version stays. The events go too, so every state is
# the only one current: the status reads the newest map event of the version, and a state that records none must not inherit an earlier one's.
publish_clear() {
  index_clear
  ledger "DELETE FROM events WHERE idempotency_key LIKE 'sindri-ui-fixture:publish:%';" >/dev/null || return
  sql_on "$(context_db)" "DELETE FROM map_hold; DELETE FROM map_publish_chain; DELETE FROM map_version WHERE status = 'draft' AND commit_sha = 'fixture-hold'; DELETE FROM map_version_head WHERE version NOT IN (SELECT id FROM map_version);"
}

# A draft with the current version's heads, held, with the wait started $1 seconds ago.
publish_hold() {
  local ago="$1" db live
  db="$(context_db)"
  publish_clear || return
  # The held map is for the repo's live head and the index is at another commit, so the repo is waiting (build queued), not "newer".
  live="$(git -C "$AW_SINDRI_HOME/ui-fixture-context/$repo" rev-parse HEAD 2>/dev/null)" || live=""
  [ -n "$live" ] || live="9999999999999999999999999999999999999999"
  sql_on "$db" "INSERT INTO map_version (repo, commit_sha, key_version, created, status)
                  SELECT repo, 'fixture-hold', key_version, strftime('%Y-%m-%dT%H:%M:%S', 'now') || '.0000000Z', 'draft' FROM map_version WHERE status = 'current';
                INSERT INTO map_version_head (version, repo, commit_sha, on_default)
                  SELECT d.id, h.repo, h.commit_sha, h.on_default FROM map_version d, map_version_head h, map_version c
                  WHERE d.commit_sha = 'fixture-hold' AND c.status = 'current' AND h.version = c.id;
                INSERT INTO map_hold (version) SELECT id FROM map_version WHERE commit_sha = 'fixture-hold';
                INSERT OR REPLACE INTO map_publish_chain (id, since) VALUES (1, strftime('%Y-%m-%dT%H:%M:%S', 'now', '-$ago seconds') || '.0000000Z');
                UPDATE map_version_head SET commit_sha = '$live', on_default = 1
                  WHERE version = (SELECT id FROM map_version WHERE commit_sha = 'fixture-hold');" >/dev/null \
    && index_write "$repo"
}

context_publish() {
  need jq
  local db state current repo
  db="$(context_db)"
  [ -f "$db" ] || die "no context store at $db: run 'context up' first"
  current="$(sql_on "$db" "SELECT id || '|' || repo FROM map_version WHERE status = 'current'")"
  [ -n "$current" ] || die "no current map: run 'context up' first"
  version="${current%%|*}"; repo="${current#*|}"
  # The current map's head for the repo (a map built without heads has none): the held draft copies it and the stale state moves it.
  sql_on "$db" "INSERT OR IGNORE INTO map_version_head (version, repo, commit_sha, on_default) VALUES ($version, '$repo', '$INDEX_COMMIT', 1);" >/dev/null || return
  case "$1" in
    pause) "$PROVE" human services pause "$PUBLISH_SERVICE"; return ;;
    resume) "$PROVE" human services resume "$PUBLISH_SERVICE"; return ;;
    held) publish_hold 0 && "$PROVE" human services pause "$PUBLISH_SERVICE" >/dev/null && state="held" ;;
    overdue) publish_hold 3600 && "$PROVE" human services pause "$PUBLISH_SERVICE" >/dev/null && state="overdue" ;;
    *)
      publish_clear || return
      "$PROVE" human services resume "$PUBLISH_SERVICE" >/dev/null 2>&1 || true
      case "$1" in
        published) state="published" ;;
        without-waiting)
          publish_event firstmap context.map.published "service:$PUBLISH_SERVICE" "{\"repo\":\"$repo\",\"version\":$version,\"reason\":\"FirstMap\",\"via\":\"refresh\",\"repos\":1,\"staleRepos\":0,\"waitedSeconds\":0,\"heads\":{}}" \
            && state="published-without-waiting" ;;
        by-hand)
          publish_event byhand context.map.promoted "human:fixture" "{\"version\":$version,\"repo\":\"$repo\",\"retained\":null}" && state="published-by-hand" ;;
        stale)
          sql_on "$db" "UPDATE map_version_head SET commit_sha = '1234567' WHERE version = $version AND repo = '$repo';" >/dev/null \
            && index_write "$repo" \
            && publish_event timedout context.map.published "service:$PUBLISH_SERVICE" "{\"repo\":\"$repo\",\"version\":$version,\"reason\":\"TimedOut\",\"via\":\"check\",\"repos\":1,\"staleRepos\":1,\"waitedSeconds\":900,\"heads\":{}}" \
            && state="published-stale" ;;
      esac ;;
  esac || return
  printf '%s\n' "$state"
}

context() {
  case "$1" in up) context_up ;; draft) context_draft ;; down) context_down ;; publish) context_publish "$2" ;; whole) context_whole "$2" ;; sources) context_sources ;; esac
}

# --- projects: the Projects page's data (projects page spec 4.1, 5, 12 Ph5, 13.1) ---
PROJECTS_TEAM="SPL"
PROJECTS_AUDIENCE="linear:$PROJECTS_TEAM"
PROJECTS_GH_REPOS="acme/api, acme/web"
PROJECTS_VIEWER="fx-viewer"
PROJECTS_VIEWER_NAME="Sam Rivera"
projects_marker() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-projects-owner"; }

# A SQL expression for a store timestamp ('2026-10-29T12:00:00.0000000Z', seven fractional digits) at now plus the modifier ($1: '-2 hours').
projects_ts() { printf "(strftime('%%Y-%%m-%%dT%%H:%%M:%%S', 'now', '%s') || '.0000000Z')" "$1"; }

# A date $1 days from now (YYYY-MM-DD), GNU or BSD date.
projects_day() { date -u -d "+$1 days" +%F 2>/dev/null || date -u -v+"$1"d +%F; }

# A human reads only the profile's Linear teams (ProfileAudiencePolicy), so the team goes into the live profile (a context: block after user:).
projects_profile() {
  local profile
  profile="$(profile_path)"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  if ! grep -q "teams: \[$PROJECTS_TEAM\]" "$profile"; then
    ! grep -q '^context:' "$profile" || die "projects: the profile already has a context: section; add 'sources: { linear: { teams: [$PROJECTS_TEAM] } }' to it by hand"
    awk -v e="context:\n  sources:\n    linear: { teams: [$PROJECTS_TEAM] }\n    github: { repos: [$PROJECTS_GH_REPOS] }" '{print} /^user:/ {print e}' "$profile" > "$profile.new" && mv "$profile.new" "$profile" \
      || die "projects: can't add the Linear team to $profile"
  elif ! grep -q 'github:' "$profile"; then
    # An older fixture home has the team and no GitHub source: add it once, under the Linear line. The token pointer is optional, so none is written.
    awk -v e="    github: { repos: [$PROJECTS_GH_REPOS] }" '{print} /teams: \[/ && !done {print e; done=1}' "$profile" > "$profile.new" && mv "$profile.new" "$profile" \
      || die "projects: can't add the GitHub repos to $profile"
  fi
  approve >&2 || return
}

# One stored project and its 'led-by' link, appended to $projects_sql. $1 id, $2 name, $3 state, $4 state_raw, $5 health or '', $6 due in days or '',
# $7 lead id, $8 lead name, $9 updated ago ('-2 hours'), $10 closed ago or '', $11 description.
projects_row() {
  local key="linear:project/fx-$1" health="NULL" due="NULL" closed="NULL"
  [ -z "$5" ] || health="'$5'"
  [ -z "$6" ] || due="'$(projects_day "$6")'"
  [ -z "${10}" ] || closed="$(projects_ts "${10}")"
  projects_sql+="INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at, health, due_at, description)
      VALUES ('$key', 'linear-projects', 'project', NULL, '$PROJECTS_AUDIENCE', '$2', 'https://linear.app/fixture/project/fx-$1', '$8', '$3', '$4', $(projects_ts '-60 days'), $(projects_ts "$9"), $closed, 'fixture', 'fixture-$1', 'fixture', 0, NULL, $(projects_ts '+0 seconds'), $health, $due, '${11}');
    INSERT INTO record_link (from_key, to_ref, to_key, kind, title) VALUES ('$key', 'linear:user/$7', NULL, 'led-by', '$8');
"
}

# $1 project id, $2 project name, $3 open issues, $4 closed issues: stored issues with an 'in-project' link (the cards' open counts).
projects_issues() {
  local n key project="linear:project/fx-$1"
  for ((n = 1; n <= $3 + $4; n++)); do
    key="linear:issue/fx-$1-$n"
    if [ "$n" -le "$3" ]; then
      projects_sql+="INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at)
          VALUES ('$key', 'linear', 'issue', NULL, '$PROJECTS_AUDIENCE', '$2 task $n', 'https://linear.app/fixture/issue/$PROJECTS_TEAM-$1-$n', '$PROJECTS_VIEWER_NAME', 'open', 'In Progress', $(projects_ts '-40 days'), $(projects_ts "-$((n * 7)) hours"), NULL, 'fixture', 'fixture-$1-$n', 'fixture', 0, NULL, $(projects_ts '+0 seconds'));
"
    else
      projects_sql+="INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at)
          VALUES ('$key', 'linear', 'issue', NULL, '$PROJECTS_AUDIENCE', '$2 task $n', 'https://linear.app/fixture/issue/$PROJECTS_TEAM-$1-$n', '$PROJECTS_VIEWER_NAME', 'done', 'Done', $(projects_ts '-40 days'), $(projects_ts "-$((n * 2)) days"), $(projects_ts "-$((n * 2)) days"), 'fixture', 'fixture-$1-$n', 'fixture', 0, NULL, $(projects_ts '+0 seconds'));
"
    fi
    projects_sql+="INSERT INTO record_link (from_key, to_ref, to_key, kind, title) VALUES ('$key', '$project', '$project', 'in-project', '$2');
"
  done
}

# One open GitHub PR and the 'relates' link from a Checkout rewrite issue to it, appended to $projects_sql. $1 owner/name, $2 number, $3 the
# issue's number under fx-checkout, $4 title. The audience is linear:SPL because that is the one a human's mask always allows here.
projects_pr() {
  local key="github:pr/$1/$2"
  projects_sql+="INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at)
      VALUES ('$key', 'github', 'pr', NULL, '$PROJECTS_AUDIENCE', '$4', 'https://github.com/$1/pull/$2', '$PROJECTS_VIEWER_NAME', 'open', 'open', $(projects_ts '-3 days'), $(projects_ts '-1 days'), NULL, 'fixture', 'fixture-pr-$2', 'fixture', 0, NULL, $(projects_ts '+0 seconds'));
INSERT INTO record_link (from_key, to_ref, to_key, kind, title) VALUES ('linear:issue/fx-checkout-$3', '$key', '$key', 'relates', '$4');
"
}

projects() {
  need_sql
  local db marker target
  db="$(context_db)"
  [ -f "$db" ] || die "no context store at $db: run 'context up' first"
  sql_on "$db" "SELECT 1 FROM source_identity LIMIT 1" >/dev/null 2>&1 || die "the context store at $db has no source_identity table (migration 0020): rebuild sindri and run 'context up' first"
  projects_profile || return

  local projects_sql="DELETE FROM record_link WHERE from_key LIKE 'linear:project/fx-%' OR from_key LIKE 'linear:issue/fx-%';
DELETE FROM record_link WHERE to_key LIKE 'github:pr/acme/%';
DELETE FROM source_record WHERE key LIKE 'linear:project/fx-%' OR key LIKE 'linear:issue/fx-%' OR key LIKE 'github:pr/acme/%';
"
  projects_row checkout    'Checkout rewrite'      open started   atRisk  16  "$PROJECTS_VIEWER" "$PROJECTS_VIEWER_NAME" '-2 hours'  ''        'Rebuild the checkout flow on the new payments API.'
  projects_row search      'Search relevance v2'   open planned   ''      45  "$PROJECTS_VIEWER" "$PROJECTS_VIEWER_NAME" '-1 days'   ''        'Rank results by what people open, not by title match.'
  projects_row onboarding  'Onboarding revamp'     done completed onTrack ''  "$PROJECTS_VIEWER" "$PROJECTS_VIEWER_NAME" '-3 days'   '-3 days' 'A shorter first-run flow for new workspaces.'
  projects_row billing     'Billing migration'     open paused    ''      30  fx-dana Dana\ Okafor                       '-5 days'   ''        'Move invoices to the new billing provider.'
  projects_row mobile      'Mobile offline sync'   open started   onTrack 21  fx-lee   Lee\ Tran                          '-6 hours'  ''        'Let the mobile app queue edits while offline.'
  projects_row platform    'Platform API v3'       open started   atRisk  38  fx-mira  Mira\ Costa                        '-2 days'   ''        'The v3 public API and its SDKs.'
  projects_issues checkout   'Checkout rewrite'    5 1
  projects_issues search     'Search relevance v2' 3 0
  projects_issues onboarding 'Onboarding revamp'   0 2
  projects_issues billing    'Billing migration'   2 1
  projects_issues mobile     'Mobile offline sync' 3 0
  projects_issues platform   'Platform API v3'     2 2
  projects_pr acme/api 41 1 'Move checkout to the payments API client'
  projects_pr acme/api 42 2 'Retry failed card captures'
  projects_pr acme/web 17 3 'Checkout form: new payment step'
  projects_sql+="INSERT OR REPLACE INTO source_identity (source, user_ref, name, read_at) VALUES ('linear', 'linear:user/$PROJECTS_VIEWER', '$PROJECTS_VIEWER_NAME', $(projects_ts '+0 seconds'));
INSERT OR REPLACE INTO source_cursor (source, repo, cursor, last_sync, last_error, phase) VALUES
  ('linear-projects', 'records', NULL, $(projects_ts '-3 minutes'), NULL, 'incremental'),
  ('linear', 'records', NULL, $(projects_ts '-3 minutes'), NULL, 'incremental');
UPDATE store_epoch SET record_stamp = record_stamp + 1 WHERE id = 1;
"
  sql_on "$db" "$projects_sql" >/dev/null || die "projects: can't write the Linear rows to $db"

  # The owner's writes, each once (one marker each, so a run that stopped half way resumes): attach a Linear project, add a hand-made one,
  # hide one the viewer leads (Hide acts only on a project that is on the page: led, or attached).
  marker="$(projects_marker)"
  if [ ! -f "$marker-attach" ]; then
    "$PROVE" human project add --linear "linear:project/fx-billing" >&2 || return
    : > "$marker-attach"
  fi
  if [ ! -f "$marker-add" ]; then
    target="$(projects_day 14)"
    "$PROVE" human project add --name "Hiring loop" --state in-progress --notes "Two onsite slots left this month" --target "$target" >&2 || return
    : > "$marker-add"
  fi
  if [ ! -f "$marker-hide" ]; then
    if ! "$PROVE" human project hide "linear:project/fx-search" >&2; then
      echo "projects: hide was refused; this is what the page sees (sindri projects --json):" >&2
      "$PROVE" human projects --json >&2 || true
      die "projects: can't hide linear:project/fx-search"
    fi
    : > "$marker-hide"
  fi
}

# --- linear-catchup: the Work > Linear "Since you last looked" strip (Linear hub spec §3; #2536) ---
# The strip reads linear_issue_change (assignments to the viewer and state moves on the viewer's issues, via 'feed') and linear_comment (an
# '@' mention by someone else) after the marker held in <home>/ui/preferences.json (linearSince); no marker reads from the start of the store.
LC_TEAM_ID="fxc-team"
LC_SQL_BAK="linear_issue_change_fxc_off"
lc_prefs() { printf '%s\n' "$AW_SINDRI_HOME/ui/preferences.json"; }

# The strip's marker for an instant ($1: seconds since the epoch): 'lin_' and the UTC ticks as 16 hex digits (LinearSinceMarkers.Format).
lc_marker() { printf 'lin_%016x' $(($1 * 10000000 + 621355968000000000)); }

# Sets or clears linearSince in the preferences file ($1: a marker, or '' to clear). The app rewrites the file from memory when it saves: restart it after this.
lc_set_marker() {
  local f dir
  f="$(lc_prefs)"
  dir="$(dirname "$f")"
  need jq
  if [ -n "$1" ]; then
    mkdir -p "$dir" || die "linear-catchup: can't make $dir"
    if [ -f "$f" ]; then jq --arg m "$1" '.linearSince = $m' "$f" > "$f.new" || die "linear-catchup: $f is not JSON"; else jq -n --arg m "$1" '{version: 1, linearSince: $m}' > "$f.new"; fi
    chmod 600 "$f.new"; mv "$f.new" "$f"
  elif [ -f "$f" ]; then
    jq 'del(.linearSince)' "$f" > "$f.new" || die "linear-catchup: $f is not JSON"
    chmod 600 "$f.new"; mv "$f.new" "$f"
  fi
}

# One issue, its events and its mention, appended to $lc_sql. $1 number, $2 project, $3 priority, $4 assignee ('viewer' or 'other'), $5 title, $6... events:
# A@H (assigned to the viewer H hours ago), S:Old>New@H (a state move), M@H (someone else's comment that @-mentions the viewer).
lc_issue() {
  local n="$1" project="$2" prio="$3" who="$4" title="$5" key id assignee ev state_name state_type updated=0 old new h
  shift 5
  key="linear:issue/fxc-$n"
  id="$PROJECTS_TEAM-$((200 + n))"
  assignee="fx-other"; [ "$who" = viewer ] && assignee="$PROJECTS_VIEWER"
  for ev in "$@"; do h="${ev##*@}"; if [ "$updated" -eq 0 ] || [ "$h" -lt "$updated" ]; then updated="$h"; fi; done
  state_name="Todo"; state_type="unstarted"
  for ev in "$@"; do
    case "$ev" in S:*) new="${ev#S:*>}"; new="${new%@*}"; state_name="$new" ;; esac   # the last state move listed is the issue's current state
  done
  case "$state_name" in "In Progress" | "In Review") state_type="started" ;; Done) state_type="completed" ;; Backlog) state_type="backlog" ;; esac
  lc_sql+="INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at)
    VALUES ('$key', 'linear', 'issue', NULL, '$PROJECTS_AUDIENCE', '$title', 'https://linear.app/fixture/issue/$id', 'Dana Okafor', '$([ "$state_type" = completed ] && echo done || echo open)', '$state_name', $(projects_ts '-20 days'), $(projects_ts "-$updated hours"), NULL, 'fixture', 'fixture-lc-$n', 'fixture', 0, NULL, $(projects_ts '+0 seconds'));
INSERT INTO linear_issue (key, team_id, identifier, state_id, state_type, assignee_id, priority, estimate, cycle_id, project_id, parent_id, labels, due_date, sort_order, updated_at, description, state_name)
    VALUES ('$key', '$LC_TEAM_ID', '$id', 'st-$(printf '%s' "$state_name" | tr 'A-Z ' 'a-z-')', '$state_type', '$assignee', $prio, NULL, NULL, 'fxc-$project', NULL, '[]', NULL, $n, $(projects_ts "-$updated hours"), NULL, '$state_name');
"
  for ev in "$@"; do
    h="${ev##*@}"
    case "$ev" in
      A@*) lc_sql+="INSERT INTO linear_issue_change (issue_key, field, \"old\", \"new\", at, via, old_name, new_name) VALUES ('$key', 'assignee', NULL, '$PROJECTS_VIEWER', $(projects_ts "-$h hours"), 'feed', NULL, NULL);
" ;;
      S:*) old="${ev#S:}"; old="${old%%>*}"; new="${ev#*>}"; new="${new%@*}"
        lc_sql+="INSERT INTO linear_issue_change (issue_key, field, \"old\", \"new\", at, via, old_name, new_name) VALUES ('$key', 'state', 'st-$(printf '%s' "$old" | tr 'A-Z ' 'a-z-')', 'st-$(printf '%s' "$new" | tr 'A-Z ' 'a-z-')', $(projects_ts "-$h hours"), 'feed', '$old', '$new');
" ;;
      M@*) lc_sql+="INSERT INTO linear_comment (id, issue_key, author_id, created_at, body, sindri) VALUES ('fxc-c-$n', '$key', 'fx-dana', $(projects_ts "-$h hours"), '@$PROJECTS_VIEWER_NAME can you look at this one before Thursday?', 0);
" ;;
    esac
  done
}

# Linear reads as connected only when the profile names a token pointer (LinearSettings: no pointer, no feed, so the page shows
# "Linear isn't connected"). The pointer names an env var nothing sets and the url is a dead loopback port, so a tick (none runs in
# this fixture: its only service is the heartbeat) could never reach Linear. Written once, on the profile's Linear line.
lc_connect() {
  local profile
  profile="$(profile_path)"
  grep -q 'linear: { teams: \[.*token:' "$profile" && return 0
  sed "s|linear: { teams: \[$PROJECTS_TEAM\] }|linear: { teams: [$PROJECTS_TEAM], token: \"env:FIXTURE_LINEAR_KEY\", url: \"http://127.0.0.1:9/graphql\" }|" "$profile" > "$profile.new" \
    && mv "$profile.new" "$profile" || die "linear-catchup: can't add the Linear token pointer to $profile"
  grep -q 'token: "env:FIXTURE_LINEAR_KEY"' "$profile" || die "linear-catchup: the profile's Linear line isn't 'linear: { teams: [$PROJECTS_TEAM] }'; add a token pointer to it by hand"
}

linear_catchup() {
  local mode="${1:-typical}" db now
  case "$mode" in empty | typical | error | read-error) ;; *) die "linear-catchup: '$mode' is not a state (empty, typical, error, read-error)" ;; esac
  need_sql
  need jq
  db="$(context_db)"
  [ -f "$db" ] || die "no context store at $db: run 'context up' first"
  sql_on "$db" "SELECT 1 FROM linear_issue_change LIMIT 1" >/dev/null 2>&1 \
    || sql_on "$db" "SELECT 1 FROM $LC_SQL_BAK LIMIT 1" >/dev/null 2>&1 \
    || die "the context store at $db has no linear_issue_change table (migrations 0032 and 0036): rebuild sindri and run 'context up' first"
  sql_on "$db" "SELECT state_name FROM linear_issue LIMIT 1" >/dev/null 2>&1 || die "the context store at $db has no linear_issue.state_name (migration 0036): rebuild sindri and run 'context up' first"
  projects_profile || return
  lc_connect

  # An 'error' run left the changes table renamed: put it back before anything else.
  sql_on "$db" "SELECT 1 FROM $LC_SQL_BAK LIMIT 1" >/dev/null 2>&1 && { sql_on "$db" "ALTER TABLE $LC_SQL_BAK RENAME TO linear_issue_change" >/dev/null || die "linear-catchup: can't restore linear_issue_change"; }

  local lc_sql="DELETE FROM linear_comment WHERE issue_key LIKE 'linear:issue/fxc-%';
DELETE FROM linear_issue_change WHERE issue_key LIKE 'linear:issue/fxc-%';
DELETE FROM linear_issue WHERE key LIKE 'linear:issue/fxc-%';
DELETE FROM source_record WHERE key LIKE 'linear:issue/fxc-%';
"
  # 25 issues, 37 events (32 changes and 5 mentions), the oldest 71 hours ago. Issues 1, 8, 11, 17, 24 and others carry two or three events.
  lc_issue 1  checkout   1 viewer 'Retry failed card captures when the payment provider times out' 'A@70' 'S:Todo>In Progress@50' 'S:In Progress>In Review@6'
  lc_issue 2  checkout   2 viewer 'Checkout form shows a stale total after a coupon is removed twice' 'A@60' 'S:Todo>In Progress@30'
  lc_issue 3  billing    3 viewer 'Export invoices as CSV' 'A@5'
  lc_issue 4  search     2 viewer 'Search results ignore diacritics in product names and brand names' 'A@48' 'S:Backlog>Todo@47'
  lc_issue 5  mobile     1 viewer 'Offline queue drops edits when the app is killed during sync' 'S:In Progress>In Review@20' 'S:In Review>Done@3'
  lc_issue 6  platform   2 viewer 'Rate limit headers missing on the v3 list endpoints and the export routes' 'A@40'
  lc_issue 7  onboarding 4 viewer 'Onboarding checklist' 'S:Todo>In Progress@12'
  lc_issue 8  platform   1 viewer 'Migrate legacy webhooks to the signed delivery format before the old endpoint is removed' 'A@71' 'M@10' 'S:Todo>In Progress@9'
  lc_issue 9  mobile     3 viewer 'Dark mode contrast on the settings page' 'A@2'
  lc_issue 10 search     2 viewer 'Search relevance: boost recently opened documents for returning users' 'S:In Progress>In Review@28'
  lc_issue 11 billing    1 viewer 'Billing portal redirects to a blank page for annual plans after a downgrade' 'A@36' 'S:Todo>In Progress@35'
  lc_issue 12 platform   3 viewer 'Add pagination to the audit log API' 'A@55'
  lc_issue 13 checkout   4 viewer 'Tooltip clipped on the narrow sidebar' 'A@1'
  lc_issue 14 billing    3 other  'Decide on the retention window for exported reports' 'M@25'
  lc_issue 15 mobile     4 viewer 'Push notification opt-in copy needs a rewrite for clarity and brevity' 'S:Backlog>Todo@44'
  lc_issue 16 checkout   2 viewer 'Flaky checkout e2e test on the payment step when the sandbox is slow' 'A@18' 'S:Todo>In Progress@17'
  lc_issue 17 mobile     1 viewer 'Sync conflicts resolve to the older edit on slow connections' 'S:Todo>In Progress@52' 'S:In Progress>In Review@15'
  lc_issue 18 platform   3 viewer 'SDK: surface retry-after in the error type' 'A@33'
  lc_issue 19 onboarding 4 viewer 'Welcome email links to the old docs' 'S:In Review>Done@22'
  lc_issue 20 billing    1 viewer 'Invoice totals round differently in the PDF and the portal for euro accounts' 'A@14' 'S:Todo>In Progress@13'
  lc_issue 21 search     3 other  'Ask: can we drop support for the v1 search API this quarter?' 'M@4'
  lc_issue 22 search     2 viewer 'Search suggestions flicker while typing quickly on slower laptops' 'A@8'
  lc_issue 23 platform   4 other  'Update the API reference for the new pagination parameters' 'M@30'
  lc_issue 24 checkout   1 viewer 'Handle expired sessions on the checkout confirmation page without losing the cart' 'A@64' 'S:Todo>In Progress@63'
  lc_issue 25 mobile     3 other  'Mobile: cache the last 50 shifts for offline viewing' 'M@7'
  lc_sql+="INSERT OR REPLACE INTO source_identity (source, user_ref, name, read_at) VALUES ('linear', 'linear:user/$PROJECTS_VIEWER', '$PROJECTS_VIEWER_NAME', $(projects_ts '+0 seconds'));
INSERT OR REPLACE INTO linear_team (id, key, name, synced_at) VALUES ('$LC_TEAM_ID', '$PROJECTS_TEAM', 'Spline', $(projects_ts '-3 minutes'));
INSERT OR REPLACE INTO linear_member (team_id, id, name, display_name, active) VALUES ('$LC_TEAM_ID', '$PROJECTS_VIEWER', '$PROJECTS_VIEWER_NAME', 'sam', 1), ('$LC_TEAM_ID', 'fx-dana', 'Dana Okafor', 'dana', 1);
INSERT OR REPLACE INTO source_cursor (source, repo, cursor, last_sync, last_error, phase, failures, next_try, last_error_text) VALUES ('linear', 'records', NULL, $(projects_ts '-3 minutes'), NULL, 'incremental', 0, NULL, NULL);
UPDATE store_epoch SET record_stamp = record_stamp + 1 WHERE id = 1;
"
  sql_on "$db" "$lc_sql" >/dev/null || die "linear-catchup: can't write the Linear issues to $db"

  now="$(date -u +%s)"
  case "$mode" in
    typical) lc_set_marker "" ;;
    # the marker is now: newer than every change, so the strip says nothing is new
    empty) lc_set_marker "$(lc_marker "$now")" ;;
    # the feed's last tick failed: Linear refused the key (SND-CONTEXT-071, a 401), three ticks in a row; the earlier rows stay, which is what the Reconnect card sits beside
    error) lc_set_marker ""
      sql_on "$db" "UPDATE source_cursor SET last_error = 'SND-CONTEXT-071', failures = 3, last_sync = $(projects_ts '-4 minutes'), next_try = $(projects_ts '+10 minutes'), last_error_text = 'Linear refused the key (401)' WHERE source = 'linear' AND repo = 'records'" >/dev/null || die "linear-catchup: can't record the failed sync" ;;
    # the stored read fails (a missing table), which the strip shows as "could not read", with Retry
    read-error) lc_set_marker ""; sql_on "$db" "ALTER TABLE linear_issue_change RENAME TO $LC_SQL_BAK" >/dev/null || die "linear-catchup: can't break the changes read" ;;
  esac
  printf 'linear-catchup %s: %s changes and mentions on %s issues in %s. Restart the app (it keeps its preferences in memory).\n' "$mode" \
    "$(sql_on "$db" "SELECT (SELECT COUNT(*) FROM $([ "$mode" = read-error ] && echo "$LC_SQL_BAK" || echo linear_issue_change) WHERE issue_key LIKE 'linear:issue/fxc-%') + (SELECT COUNT(*) FROM linear_comment WHERE issue_key LIKE 'linear:issue/fxc-%')")" \
    "$(sql_on "$db" "SELECT COUNT(*) FROM linear_issue WHERE key LIKE 'linear:issue/fxc-%'")" "$db"
}

# --- reviews: a review page on a fixture PR (PR review page spec 8.2) ---
REVIEWS_SLUG="acme/api"
REVIEWS_HEAD_SHA="1f2e3d4c5b6a79880011223344556677889900aa"
REVIEWS_BRANCH="feat/retry-budget"
REVIEWS_FILE="src/Sync.cs"
REVIEWS_LINE=42
REVIEWS_DIRNAME="ui-fixture-forge"
reviews_dir() { printf '%s\n' "$AW_SINDRI_HOME/$REVIEWS_DIRNAME"; }
reviews_state() { printf '%s\n' "$(reviews_dir)/state"; }

# Serve is restarted with AW_STATE_DIR inside the home, which sindri-prove.sh's guard accepts only for a home under $TMPDIR.
reviews_need_tmp_home() {
  local home temp
  home="$(cd "$AW_SINDRI_HOME" 2>/dev/null && pwd -P)" || die "reviews: can't resolve $AW_SINDRI_HOME"
  temp="$(cd "${TMPDIR:-/tmp}" 2>/dev/null && pwd -P)" || die "reviews: can't resolve \$TMPDIR"
  case "$home/" in "${temp%/}"/?*) ;; *) die "reviews: AW_SINDRI_HOME must be under \$TMPDIR (got $home): a changed AW_STATE_DIR fails the guard of a proof-folder home" ;; esac
}

# review.login and workflow.forge (the file adapter on github.com) in the live profile, once each; approved afterwards when it isn't.
# What 'reviews up' added (one word per line: review, forge, ghtoken, cursor), so 'reviews down' removes only that and never a block that was there before.
reviews_marker() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-reviews-added"; }
reviews_mark() { grep -qx "$1" "$(reviews_marker)" 2>/dev/null || printf '%s\n' "$1" >> "$(reviews_marker)"; }
reviews_marked() { grep -qx "$1" "$(reviews_marker)" 2>/dev/null; }

reviews_profile() {
  local profile
  profile="$(profile_path)"
  # why: a forge another verb set can't reach https://github.com/acme/api/pull/N, so the fix route would fail
  if grep -q '^  forge:' "$profile" && ! grep -q '^  forge:.*github\.com' "$profile"; then
    die "reviews: the profile already has a workflow.forge without github.com ($(grep -m1 '^  forge:' "$profile" | sed 's/^ *//')): run 'down' and 'up' for a fresh home first"
  fi
  if ! grep -q '^review:' "$profile"; then
    printf 'review:\n  login: %s\n' "$PROJECTS_VIEWER" >> "$profile" || die "reviews: can't add review.login to $profile"
    reviews_mark review
  fi
  if ! grep -q '^  forge:' "$profile"; then
    if grep -q '^workflow:' "$profile"; then
      awk '{print} /^workflow:/ {print "  forge: { adapter: file, hosts: [github.com] }"}' "$profile" > "$profile.new"
    else
      awk '{print} /^user:/ {print "workflow:\n  forge: { adapter: file, hosts: [github.com] }"}' "$profile" > "$profile.new"
    fi && mv "$profile.new" "$profile" || die "reviews: can't add workflow.forge to $profile"
    reviews_mark forge
  fi
  # Reviews shows "Connect GitHub" and no rows while there is no GitHub connection and no profile token (ReviewConnectRules.For, #2619), and this
  # fixture has no connection (it uses the file forge). A token pointer is the one input that lets the list read the seeded PR. It names an env var nothing
  # sets and no tick runs here (the only service is the heartbeat), so the github cursor's last_error stays NULL: no AW-CONNECTORS-006/-007 sign-in code.
  if grep -q 'github: { repos: \[' "$profile" && ! grep -q 'github: {[^}]*token:' "$profile"; then
    sed 's|\(github: { repos: \[[^]]*\]\) }|\1, token: "env:FIXTURE_GITHUB_TOKEN" }|' "$profile" > "$profile.new" && mv "$profile.new" "$profile" || die "reviews: can't add the github token pointer to $profile"
    grep -q 'token: "env:FIXTURE_GITHUB_TOKEN"' "$profile" || die "reviews: the profile's github line isn't 'github: { repos: [...] }'; add token: \"env:FIXTURE_GITHUB_TOKEN\" to it by hand"
    reviews_mark ghtoken
  fi
  approve >&2 || return
}

# The aw-forge-fake command line in REVIEWS_CMD: AW_FORGE_FAKE, else a dotnet build of the one project into <home>/ui-fixture-forge/bin.
reviews_ensure_bin() {
  if [ -n "${AW_FORGE_FAKE:-}" ]; then
    # shellcheck disable=SC2206
    REVIEWS_CMD=($AW_FORGE_FAKE)
    return
  fi
  need dotnet
  local bin dll
  bin="$(reviews_dir)/bin"
  dll="$bin/aw-forge-fake.dll"
  if [ ! -f "$dll" ] || [ -n "$(find "$ROOT/src/Forge" "$ROOT/src/Aw.Kernel" "$ROOT/src/Aw.Kernel.Telemetry" "$ROOT/src/Redaction/Aw.Redaction" -name '*.cs' -newer "$dll" -print -quit 2>/dev/null)" ]; then
    echo "sindri-ui-fixture: building aw-forge-fake into $bin" >&2
    mkdir -p "$bin" && dotnet build "$ROOT/src/Forge/Aw.Forge.Host" -c Release -o "$bin" -v q --nologo >&2 || die "reviews: dotnet build of aw-forge-fake failed"
  fi
  REVIEWS_CMD=(dotnet "$dll")
}

# aw-forge-fake on the fixture's own world (never the real $AW_STATE_DIR), acting as the viewer (or as REVIEWS_AS when set).
reviews_forge() { AW_STATE_DIR="$(reviews_state)" AW_FORGE_ADAPTER=file AW_FORGE_HOSTS=github.com AW_FORGE_LOGIN="${REVIEWS_AS:-$PROJECTS_VIEWER}" "${REVIEWS_CMD[@]}" "$@" </dev/null; }

# The 3 PRs by other authors that ask the viewer for a review, so the Reviews list has a queue to sort (one red, two green; #2980):
# author|branch|head sha|title|opened hours ago|file|added lines|checks
REVIEWS_OTHERS=(
  "dana-okafor|feat/rate-limit-headers|2a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d|Add rate limit headers to v3 list endpoints|5|src/RateLimit.cs|6|--state passing --passed 3 --runs build:success:github-actions,test:success:github-actions,lint:success:github-actions"
  "kai-reviewer|fix/diacritics-search|3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e|Fold diacritics in product search|26|src/Search/Normalize.cs|12|--state passing --passed 3 --runs build:success:github-actions,test:success:github-actions,lint:success:github-actions"
  "lee-park|chore/webhook-signing|4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f|Sign legacy webhook deliveries|50|src/Webhooks/Signer.cs|4|--state failing --failing test --passed 2 --runs build:success:github-actions,test:failure:github-actions,lint:success:github-actions"
)

# One of REVIEWS_OTHERS: opened as its author, a file with N lines added after line 20, its checks, and its Context record (the viewer requested).
reviews_other() {
  local db=$1 who br sha title age path n checks out number link files head="" base="" patch i
  IFS='|' read -r who br sha title age path n checks <<<"$2"
  out="$(REVIEWS_AS="$who" reviews_forge open --repo "github.com/$REVIEWS_SLUG" --head "$br" --base main --head-sha "$sha" --title "$title" --body "A fixture PR by $who.")" || die "reviews: aw-forge-fake open failed for $br"
  number="$(jq -r '.number // empty' <<<"$out")"
  [[ "$number" =~ ^[0-9]+$ ]] || die "reviews: aw-forge-fake open gave no pull number for $br: $out"
  link="https://github.com/$REVIEWS_SLUG/pull/$number"
  for ((i = 1; i <= 20; i++)); do head+="    Line($i);"$'\n'; done
  base="$head"; patch="@@ -20,0 +21,$n @@"
  for ((i = 1; i <= n; i++)); do head+="    Added$i();"$'\n'; patch+=$'\n'"+    Added$i();"; done
  files="$(reviews_dir)/files-$number.json"
  jq -n --arg p "$path" --arg t "$head" --arg b "$base" --arg patch "$patch" --argjson n "$n" \
    '[{file: {path: $p, previousPath: null, status: "Modified", additions: $n, deletions: 0, patch: $patch}, text: $t, baseText: $b}]' > "$files" || die "reviews: can't write $files"
  REVIEWS_AS="$who" reviews_forge seed files "$link" --from "$files" >/dev/null || die "reviews: seed files failed for $br"
  # shellcheck disable=SC2086 # the checks field is the seed's own flags, split on purpose
  REVIEWS_AS="$who" reviews_forge seed checks "$link" $checks >/dev/null || die "reviews: seed checks failed for $br"
  sql_on "$db" "INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at, pull_facts)
  VALUES ('github:pr/$REVIEWS_SLUG/$number', 'github', 'pr', NULL, '$PROJECTS_AUDIENCE', '$title', '$link', '$who', 'open', 'open', $(projects_ts "-$age hours"), $(projects_ts '-1 hours'), NULL, 'fixture', 'fixture-review-$number', 'fixture', 0, NULL, $(projects_ts '+0 seconds'),
    '{\"requestedReviewers\":[\"$PROJECTS_VIEWER\"],\"headSha\":\"$sha\",\"reviews\":[]}');" >/dev/null || die "reviews: can't write the record of $link to $db"
}

# The changed file's seed (docs/csharp/forge.md "seed files"): 3 lines added after line 40, so line 42 is on the new side.
reviews_files_json() {
  local n head="" base=""
  for ((n = 1; n <= 40; n++)); do head+="    Step($n);"$'\n'; done
  base="$head"
  head+="    var delay = budget.Next(attempt);"$'\n'
  head+="    if (delay is null) return Retry.Stop;"$'\n'
  head+="    await Task.Delay(delay.Value);"$'\n'
  jq -n --arg path "$REVIEWS_FILE" --arg text "$head" --arg baseText "$base" \
    '[{file: {path: $path, previousPath: null, status: "Modified", additions: 3, deletions: 0,
              patch: "@@ -40,0 +41,3 @@\n+    var delay = budget.Next(attempt);\n+    if (delay is null) return Retry.Stop;\n+    await Task.Delay(delay.Value);"},
       text: $text, baseText: $baseText}]'
}

reviews_up() {
  need jq
  need_sql
  reviews_need_tmp_home || return
  local db dir state real link out number files key
  db="$(context_db)"
  [ -f "$db" ] || die "no context store at $db: run 'context up' first"
  sql_on "$db" "SELECT pull_facts FROM source_record LIMIT 1" >/dev/null 2>&1 || die "the context store at $db has no pull_facts column (migration 0030): rebuild sindri and run 'context up' first"
  projects_profile || return
  reviews_profile || return

  dir="$(reviews_dir)"
  state="$(reviews_state)"
  # Serve finds its Temporal binary under $AW_STATE_DIR/cache/bin: link the real one in before serve's state dir is the home's.
  real="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
  mkdir -p "$state/cache" || return
  if [ "$real" != "$state" ] && [ ! -e "$state/cache/bin" ]; then
    ln -s "$real/cache/bin" "$state/cache/bin" || die "reviews: can't link $real/cache/bin"
  fi
  reviews_ensure_bin || return
  # A new world every run: the PR is always the first one, so the stored record never needs a second number.
  rm -rf -- "$state/forge"
  out="$(reviews_forge open --repo "github.com/$REVIEWS_SLUG" --head "$REVIEWS_BRANCH" --base main --head-sha "$REVIEWS_HEAD_SHA" \
    --title "Add a retry budget to sync" --body "Stops a failing sync from retrying forever. The fixture's PR for the review page.")" || die "reviews: aw-forge-fake open failed"
  number="$(jq -r '.number // empty' <<<"$out")"
  [[ "$number" =~ ^[0-9]+$ ]] || die "reviews: aw-forge-fake open gave no pull number: $out"
  link="https://github.com/$REVIEWS_SLUG/pull/$number"
  files="$dir/files.json"
  reviews_files_json > "$files" || die "reviews: can't write $files"
  reviews_forge seed files "$link" --from "$files" >/dev/null || die "reviews: seed files failed"
  reviews_forge seed checks "$link" --state failing --failing lint --passed 2 --runs "build:success:github-actions,test:success:github-actions,lint:failure:github-actions" >/dev/null || die "reviews: seed checks failed"
  reviews_forge seed thread "$link" --author reviewer-kai --association COLLABORATOR --kind thread --path "$REVIEWS_FILE" \
    --body "What stops this from waiting forever when budget.Next keeps returning a delay?" >/dev/null || die "reviews: seed thread failed"

  key="github:pr/$REVIEWS_SLUG/$number"
  [ -n "$(sql_on "$db" "SELECT 1 FROM source_cursor WHERE source = 'github' AND repo = 'records'")" ] || reviews_mark cursor
  sql_on "$db" "DELETE FROM source_record WHERE text_hash LIKE 'fixture-review-%';
INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at, pull_facts)
  VALUES ('$key', 'github', 'pr', NULL, '$PROJECTS_AUDIENCE', 'Add a retry budget to sync', '$link', '$PROJECTS_VIEWER', 'open', 'open', $(projects_ts '-2 days'), $(projects_ts '-3 hours'), NULL, 'fixture', 'fixture-review-$number', 'fixture', 0, NULL, $(projects_ts '+0 seconds'),
    '{\"requestedReviewers\":[\"reviewer-kai\"],\"headSha\":\"$REVIEWS_HEAD_SHA\",\"reviews\":[]}');
INSERT OR REPLACE INTO source_cursor (source, repo, cursor, last_sync, last_error, phase) VALUES ('github', 'records', NULL, $(projects_ts '-3 minutes'), NULL, 'incremental');
UPDATE store_epoch SET record_stamp = record_stamp + 1 WHERE id = 1;" >/dev/null || die "reviews: can't write the PR record to $db"
  local other
  for other in "${REVIEWS_OTHERS[@]}"; do reviews_other "$db" "$other" || return; done
  sql_on "$db" "UPDATE store_epoch SET record_stamp = record_stamp + 1 WHERE id = 1;" >/dev/null || die "reviews: can't move the store epoch in $db"

  sessions_restart >&2 || return
  printf '/sindri/reviews/%s/%s\n%s\n%s:%s\n' "$REVIEWS_SLUG" "$number" "$REVIEWS_HEAD_SHA" "$REVIEWS_FILE" "$REVIEWS_LINE"
}

reviews_down() {
  need_sql
  local db dir profile
  db="$(context_db)"
  if [ -f "$db" ]; then
    if reviews_marked cursor; then sql_on "$db" "DELETE FROM source_cursor WHERE source = 'github' AND repo = 'records';" >/dev/null || die "reviews: can't remove the github cursor row from $db"; fi
    sql_on "$db" "DELETE FROM source_record WHERE text_hash LIKE 'fixture-review-%'; UPDATE store_epoch SET record_stamp = record_stamp + 1 WHERE id = 1;" >/dev/null || die "reviews: can't remove the PR record from $db"
  fi
  dir="$(reviews_dir)"
  case "$dir" in */"$REVIEWS_DIRNAME") [ ! -e "$dir" ] || rm -rf -- "$dir" ;; *) die "reviews: refusing to delete $dir" ;; esac
  profile="$(profile_path)"
  local drop=""
  reviews_marked review && drop="^review:\$|^  login: $PROJECTS_VIEWER\$"
  reviews_marked forge && drop="${drop:+$drop|}^  forge: \\{ adapter: file, hosts: \\[github.com\\] \\}\$"
  if [ -f "$profile" ] && [ -n "$drop" ]; then
    grep -Ev "$drop" "$profile" \
      | awk '{l[NR]=$0} END {for (i=1;i<=NR;i++) if (!(l[i]=="workflow:" && (i==NR || l[i+1] !~ /^  /))) print l[i]}' > "$profile.new" && mv "$profile.new" "$profile" || die "reviews: can't edit $profile"
  fi
  if [ -f "$profile" ] && reviews_marked ghtoken; then
    sed 's|, token: "env:FIXTURE_GITHUB_TOKEN" }| }|' "$profile" > "$profile.new" && mv "$profile.new" "$profile" || die "reviews: can't remove the github token pointer from $profile"
  fi
  rm -f -- "$(reviews_marker)"
  # The dir is gone, so the restart no longer points AW_STATE_DIR at it.
  if [ -f "$AW_SINDRI_HOME/control.json" ]; then sessions_restart >&2 || return; fi
}

reviews() {
  case "$1" in up) reviews_up ;; down) reviews_down ;; esac
}

# --- context sources: the node panel's source links (#1702) ---
SOURCES_ORIGIN="https://github.com/acme/tabs.git"
SOURCES_DOC_NODE="feature:staffing.schema"
SOURCES_TICKET_UUID="0e5a1b00-0000-4000-8000-000000000702"
SOURCES_TICKET_NODE="record:linear:issue/$SOURCES_TICKET_UUID"
SOURCES_TICKET_URL="https://linear.app/acme/issue/SPL-142/roll-out-staffing"

context_sources() {
  need jq
  need_sql
  local dir repo db commit map
  dir="$(context_dir)"
  repo="$dir/$CONTEXT_REPO"
  [ -d "$repo/.git" ] || die "context sources: run 'context up' first"
  db="$(context_db)"
  [ -f "$db" ] || die "no context store at $db: run 'context up' first"
  projects_profile || return
  local g=(env -u GIT_DIR -u GIT_WORK_TREE -u GIT_INDEX_FILE -u GIT_COMMON_DIR GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1 git -C "$repo")
  if "${g[@]}" remote get-url origin >/dev/null 2>&1; then
    "${g[@]}" remote set-url origin "$SOURCES_ORIGIN" || die "context sources: can't set the origin of $repo"
  else
    "${g[@]}" remote add origin "$SOURCES_ORIGIN" || die "context sources: can't add the origin to $repo"
  fi
  commit="$("${g[@]}" rev-parse HEAD)" || die "context sources: can't read the HEAD of $repo"
  map="$dir/sources-map.json"
  jq --arg commit "$commit" '. + {commit: $commit}' "$ROOT/tests/Aw.Context.Testing/Fixtures/map-sources.json" > "$map" || die "context sources: can't write the sources map"
  context_import "$map" || return
  sql_on "$db" "INSERT OR REPLACE INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at)
      VALUES ('linear:issue/$SOURCES_TICKET_UUID', 'linear', 'issue', NULL, 'linear:$PROJECTS_TEAM', 'Roll out staffing', '$SOURCES_TICKET_URL', '$PROJECTS_VIEWER_NAME', 'open', 'In Progress', $(projects_ts '-10 days'), $(projects_ts '-1 days'), NULL, 'fixture', 'fixture-sources-1', 'fixture', 0, NULL, $(projects_ts '+0 seconds'));
    UPDATE store_epoch SET record_stamp = record_stamp + 1 WHERE id = 1;" >/dev/null || die "context sources: can't write the ticket record to $db"
  # A map built over the import (a refresh under the profile) has no $SOURCES_DOC_NODE: fail here, not later as a panel error.
  local shown
  shown="$("$PROVE" human context show "$SOURCES_DOC_NODE" 2>&1)" || die "context sources: the imported map was replaced, $SOURCES_DOC_NODE is gone: $shown"
  printf '%s\n%s\n%s\n' "$CONTEXT_REPO" "$SOURCES_DOC_NODE" "$SOURCES_TICKET_NODE"
}

SESSIONS_DIRNAME="ui-fixture-sessions"
NO_SESSIONS_DIRNAME="ui-fixture-no-sessions"
sessions_dir() { printf '%s\n' "$AW_SINDRI_HOME/$SESSIONS_DIRNAME"; }

# The collector's four overrides, pointed at the copy when 'sessions up' made one, else at an empty tree in the home
# (no process, no Claude record, an empty fleet, a bare launcher.json), so no control-plane start reads the real box.
# AW_LAUNCHER_CONFIG keeps the owner's launcher.json (under $AW_STATE_DIR) out too.
sessions_export() {
  local d
  d="$(sessions_dir)"
  if [ ! -d "$d/proc" ]; then
    d="$AW_SINDRI_HOME/$NO_SESSIONS_DIRNAME"
    mkdir -p "$d/proc/2" "$d/claude-sessions" && { [ -d "$d/fleet" ] || mkdir -m 700 "$d/fleet"; } && : > "$d/fleet/.aw-fleet" \
      || die "can't make the empty sessions tree at $d"
    # One plain shell of this uid and no agent: a table with no process of ours reads as SND-SESSION-002 proc-unreadable.
    printf 'Name:\tbash\nPid:\t2\nPPid:\t1\nUid:\t%s\t%s\t%s\t%s\n' "$(id -u)" "$(id -u)" "$(id -u)" "$(id -u)" > "$d/proc/2/status" \
      && printf '2 (bash) S 1 2 2 0 -1 4194304 0 0 0 0 0 0 0 0 20 0 1 0 100%s\n' "$(printf ' 0%.0s' {1..32})" > "$d/proc/2/stat" \
      && printf 'bash\0' > "$d/proc/2/cmdline" && : > "$d/proc/2/environ" || die "can't write the shell under $d/proc/2"
    [ -f "$d/launcher.json" ] || printf '{"schema":1}\n' > "$d/launcher.json" || die "can't write $d/launcher.json"
  fi
  export AW_SESSIONS_PROC_ROOT="$d/proc" AW_SESSIONS_CLAUDE_DIR="$d/claude-sessions" AW_FLEET_DIR="$d/fleet" AW_LAUNCHER_CONFIG="$d/launcher.json"
  # 'sandbox blocked|clear' keeps AW_SANDBOX_BWRAP across restarts.
  if [ -s "$(sandbox_dir)/bwrap" ]; then export AW_SANDBOX_BWRAP="$(cat "$(sandbox_dir)/bwrap")"; else unset AW_SANDBOX_BWRAP; fi
  # 'reviews up' keeps serve on its forge world (acting as the viewer, as the world's PR was opened) while <home>/ui-fixture-forge exists.
  if [ -d "$(reviews_state)" ]; then export AW_STATE_DIR="$(reviews_state)" AW_FORGE_LOGIN="$PROJECTS_VIEWER"; fi
}

sessions_restart() {
  sessions_export
  "$PROVE" human stop >/dev/null 2>&1 || true
  "$PROVE" human --headless
}

sessions_need_copy() { [ -d "$(sessions_dir)/proc" ] || die "no fixture sessions at $(sessions_dir): run 'sessions up' first"; }

sessions_pid() { [[ "$1" =~ ^[0-9]+$ ]] || die "sessions: '$1' is not a pid"; }

# Removes the copy; the path must end in the fixture's folder name.
sessions_remove() {
  local d="$1"
  case "$d" in */"$SESSIONS_DIRNAME") [ ! -e "$d" ] || rm -rf -- "$d" ;; *) die "sessions: refusing to delete $d" ;; esac
}

sessions_up() {
  need jq
  local src="$ROOT/tests/fixtures/sessions" d own
  own="$(jq -r '.ownUid' "$src/manifest.json")"
  [ "$(id -u)" = "$own" ] || die "sessions up: the fixture's processes belong to uid $own and this is uid $(id -u); the collector would find no session of its own"
  [ -f "$(profile_path)" ] || die "no live profile at $(profile_path): run 'up' first"
  d="$(sessions_dir)"
  sessions_remove "$d" || return
  mkdir -p "$d" || return
  cp -a "$src/proc" "$d/proc" && cp -a "$src/claude-sessions" "$d/claude-sessions" || die "sessions up: can't copy the fixture tree"
  mkdir -p "$d/ended" && mkdir -m 700 "$d/fleet" && : > "$d/fleet/.aw-fleet" || return
  printf '{"schema":1}\n' > "$d/launcher.json"
  sessions_restart
}

# $1 pid: a Claude session of the copy (its record names the session id).
sessions_ask() {
  sessions_pid "$1"
  sessions_need_copy
  need jq
  local d rec id cwd ticks dir now tmp
  d="$(sessions_dir)"
  rec="$d/claude-sessions/$1.json"
  [ -f "$rec" ] || die "sessions ask: no Claude record for pid $1 in the fixture"
  [ -f "$d/proc/$1/stat" ] || die "sessions ask: pid $1 is not in the fixture's process tree (ended?)"
  id="$(jq -r '.sessionId' "$rec")"; cwd="$(jq -r '.cwd' "$rec")"
  # stat field 22 (start time): the comm in field 2 may hold spaces, so count from the last ')' (field 3 is the first token after it).
  ticks="$(awk '{ sub(/^.*\) /, ""); print $20 }' "$d/proc/$1/stat")"
  [[ "$ticks" =~ ^[0-9]+$ ]] || die "sessions ask: no start time in $d/proc/$1/stat"
  dir="$d/fleet/claude-code"
  mkdir -p "$dir" && chmod 700 "$dir" && : > "$dir/.aw-fleet" || return
  now="$(date -u +%Y-%m-%dT%H:%M:%S.000Z)"
  tmp="$(mktemp "$dir/.ask.XXXXXX")" || return
  jq -n --arg id "$id" --arg at "$now" --arg cwd "$cwd" --argjson pid "$1" --argjson ticks "$ticks" \
    '{schema: 1, provider: "claude-code", sessionId: $id, event: "Notification", state: "asking", reason: "approval", tool: "Bash",
      at: $at, cwd: $cwd, transcript: null, agentPid: $pid, agentStartTicks: $ticks, env: {}}' > "$tmp" \
    && chmod 600 "$tmp" && mv -f "$tmp" "$dir/$id.json" || { rm -f -- "$tmp"; return 1; }
  printf '%s\n' "$dir/$id.json"
}

sessions_end() {
  sessions_pid "$1"
  sessions_need_copy
  local d
  d="$(sessions_dir)"
  [ -d "$d/proc/$1" ] || die "sessions end: pid $1 is not in the fixture's process tree"
  mv -- "$d/proc/$1" "$d/ended/$1"
}

sessions_back() {
  sessions_pid "$1"
  sessions_need_copy
  local d
  d="$(sessions_dir)"
  [ -d "$d/ended/$1" ] || die "sessions back: pid $1 was not ended by this fixture"
  mv -- "$d/ended/$1" "$d/proc/$1"
}

# false writes sessions.enabled=false; true removes the key. launcher.json is read once at start, so both restart.
sessions_toggle() {
  need jq
  sessions_need_copy
  local f tmp
  f="$(sessions_dir)/launcher.json"
  tmp="$f.new"
  if [ "$1" = false ]; then jq '.sessions.enabled = false' "$f" > "$tmp"
  else jq 'del(.sessions.enabled) | if .sessions == {} then del(.sessions) else . end' "$f" > "$tmp"; fi \
    && mv -f "$tmp" "$f" || die "sessions: can't update $f"
  sessions_restart
}

# Stops the control plane and removes the copy. It does not restart: a later start reads the empty tree, never the real box.
sessions_down() {
  "$PROVE" human stop >/dev/null 2>&1 || true
  sessions_remove "$(sessions_dir)"
}

sessions() {
  case "$1" in
    up) sessions_up ;; ask) sessions_ask "$2" ;; end) sessions_end "$2" ;; back) sessions_back "$2" ;;
    disable) sessions_toggle false ;; enable) sessions_toggle true ;; down) sessions_down ;;
  esac
}

# --- model-calls: a job's model calls on the Jobs page and the job sheet (context model routing spec 8.2, #1567 R-4) ---

# $1 key, $2 type, $3 payload JSON. A fixed id and idempotency key, so a rerun adds nothing (see publish_event).
calls_event() {
  local key="sindri-ui-fixture:model-calls:$1" id corr
  id="$(printf '%s' "$key" | cksum | awk '{printf "5c0f1e02-%04x-4d1e-9a55-3c2f6d8e1b05", $1 % 65536}')"
  corr="${id%?}6"
  ledger "INSERT OR IGNORE INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
          VALUES ('$id', '$2', 1, strftime('%Y-%m-%dT%H:%M:%S', 'now') || '.0000000Z', 'sindri-ui-fixture', 'local', 0, '$corr',
                  COALESCE((SELECT MAX(epoch) FROM events), 0), '$key', '$3');" >/dev/null
}

# $1 job, $2 call number, $3 seconds ago, $4 purpose, $5 kind, $6 tries JSON, $7 answeredBy JSON (or null), $8 input tokens, $9 output tokens, $10 items (or null), $11 how JSON (or null)
calls_call() {
  local at
  at="$(ledger "SELECT strftime('%Y-%m-%dT%H:%M:%S', 'now', '-$3 seconds') || '.0000000Z';")"
  calls_event "call:$1:$2" context.model.called "$(jq -cn --arg j "$1" --arg c "fixture-call-$2" --arg at "$at" --arg p "$4" --arg k "$5" --argjson tries "$6" --argjson by "$7" \
    --argjson i "$8" --argjson o "$9" --argjson items "${10}" --argjson how "${11}" \
    '{callId: $c, job: $j, purpose: $p, kind: $k, tries: $tries, answeredBy: $by, inputTokens: $i, outputTokens: $o, tokenBasis: "reported", items: $items, how: $how, startedAt: $at, ms: 2000}')"
}

# $1 harness, $2 model, $3 outcome, $4 reason (or null), $5 ms
calls_try() { jq -cn --arg h "$1" --arg m "$2" --arg o "$3" --arg r "$4" --argjson ms "$5" '{harness: $h, model: $m, outcome: $o, reason: (if $r == "null" then null else $r end), ms: $ms, source: "builtin"}'; }

model_calls() { # $1 state
  need jq
  need_sql
  local state="$1" n job cmd haiku luna sonnet gpt jev
  case "$state" in fell-back) n=1 ;; no-answer) n=2 ;; paused) n=3 ;; none) n=4 ;; esac
  job="$(printf 'JOB-%032x' "$((0x5c0f1e20 + n))")"
  cmd="5c0f1e02-0000-4d1e-9a55-3c2f6d8e1b0$n"
  calls_event "started:$job" job.started "$(jq -cn --arg c "$cmd" --arg j "$job" --arg k "map-$state" '{commandId: $c, job: $j, kind: "BuildContextMap", key: $k, item: null, repo: null, service: null}')"
  calls_event "progress:$job" job.progress "$(jq -cn --arg c "$cmd" --arg j "$job" '{commandId: $c, job: $j, round: 1, line: "labeling the map"}')"
  haiku="$(calls_try claude-code haiku failed exit-1 400)"
  luna="$(calls_try codex gpt-6-luna answered null 1200)"
  case "$state" in
    fell-back)
      calls_call "$job" 1 300 labeling labeling "[$(calls_try claude-code haiku limit limit 300), $luna]" '{"harness":"codex","model":"gpt-6-luna"}' 3000 1100 null null
      calls_call "$job" 2 240 labeling labeling "[$(calls_try claude-code haiku skipped limited 0), $luna]" '{"harness":"codex","model":"gpt-6-luna"}' 2800 1000 null null
      calls_call "$job" 3 180 concept-scan conceptNaming "[$haiku, $luna]" '{"harness":"codex","model":"gpt-6-luna"}' 2100 700 null null
      jev="$(calls_try jev jev-1.13.0 answered null 800)"
      calls_call "$job" 4 120 labeling labeling "[$jev]" '{"harness":"jev","model":"jev-1.13.0"}' 0 0 100 '{"jev":94,"firstCandidate":5,"fileName":1}'
      calls_call "$job" 5 60 product-surfaces productSurfaces "[$jev]" '{"harness":"jev","model":"jev-1.13.0"}' 0 0 80 '{"merged":12,"kept":58,"rejected":6,"undecided":4}'
      calls_call "$job" 6 30 labeling labeling "[$(calls_try jev jev-1.13.0 not-asked no-key 0)]" null 0 0 null null ;;
    no-answer)
      for i in 1 2 3; do calls_call "$job" "$i" "$((200 - i * 50))" labeling labeling "[$haiku]" null 0 0 null null; done ;;
    paused)
      calls_call "$job" 1 120 labeling labeling "[$luna]" '{"harness":"codex","model":"gpt-6-luna"}' 1900 600 null null
      calls_event "progress-paused:$job" job.progress "$(jq -cn --arg c "$cmd" --arg j "$job" '{commandId: $c, job: $j, round: 2, line: "Paused: claude-code at its usage limit; codex at its usage limit. Resume when one is back."}')"
      calls_event "waiting:$job" job.waiting "$(jq -cn --arg c "$cmd" --arg j "$job" '{commandId: $c, job: $j, signal: "model-limit"}')" ;;
    none) : ;;
  esac
  printf '%s\n' "$job"
}

# --- sandbox: the sandbox surfaces' states (sandbox spec section 8; issue #1113 SB-6) ---

sandbox_dir() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-sandbox"; }

# $1 key, $2 type, $3 payload JSON. A fixed id and idempotency key, so a rerun adds nothing (see publish_event).
sandbox_event() {
  local key="sindri-ui-fixture:sandbox:$1" id corr
  id="$(printf '%s' "$key" | cksum | awk '{printf "5b0f1e01-%04x-4d1e-9a55-3c2f6d8e1b05", $1 % 65536}')"
  corr="${id%?}6"
  ledger "INSERT OR IGNORE INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
          VALUES ('$id', '$2', 1, strftime('%Y-%m-%dT%H:%M:%S', 'now') || '.0000000Z', 'sindri-ui-fixture', 'local', 0, '$corr',
                  COALESCE((SELECT MAX(epoch) FROM events), 0), '$key', '$3');" >/dev/null
}

# $1 state, $2 item, $3 instance: launch.started with the golden's sandbox summary, on a fixed launch id per state.
sandbox_started() {
  local golden="$ROOT/tests/Aw.Sindri.Tests/Golden/Sandbox/events/launch-started-sandbox.json" mode=enforce attended=false
  case "$1" in learning) mode=learn ;; attended) attended=true ;; esac
  sandbox_event "started:$1" launch.started "$(jq -c --arg l "$(sandbox_launch "$1")" --arg i "$2" --arg a "$3" --arg m "$mode" --argjson at "$attended" '
    .launchId = $l | .item = $i | .sandbox.networkMode = $m
    | if $at then .mode = "open" | .instance = null | .state = null | .attempt = null
      else .mode = "step" | .instance = $a | .state = "build" | .attempt = 1 end' "$golden")"
}

# $1 state; prints the launch id (LCH- and 32 hex digits), one per state so reruns and states never collide.
sandbox_launch() { printf 'LCH-%032x\n' "$((0x5b0f1e00 + $(printf '%s' "$1" | cksum | awk '{print $1 % 4096}')))"; }

# $1 state, $2 file of events/, $3 key: a golden event payload with the launch id of the state.
sandbox_golden() { jq -c --arg l "$(sandbox_launch "$1")" '.launchId = $l' "$ROOT/tests/Aw.Sindri.Tests/Golden/Sandbox/events/$2"; }

sandbox_bwrap() { # $1 the AW_SANDBOX_BWRAP value, or empty for unset
  mkdir -p "$(sandbox_dir)" && printf '%s' "$1" > "$(sandbox_dir)/bwrap" || die "sandbox: can't write $(sandbox_dir)/bwrap"
  sessions_need_copy
  unset TMUX
  sessions_restart >&2
}

sandbox() { # $1 state, $2 item, $3 instance
  need jq
  local state="$1" item="${2:-}" instance="${3:-}" l
  case "$state" in
    blocked)
      sandbox_bwrap /nonexistent; return ;;
    clear)
      sandbox_bwrap ""; return ;;
    *)
      need_sql
      l="$(sandbox_launch "$state")"
      [ "$state" = waiting ] || sandbox_started "$state" "$item" "$instance"
      case "$state" in
        denials)
          sandbox_event "denied:$state" sandbox.denied "$(sandbox_golden "$state" sandbox-denied.json)"
          sandbox_event "denied-path:$state" sandbox.denied "$(sandbox_golden "$state" sandbox-denied.json | jq -c '.kind = "path" | .target = "/home/thor/.config/gh" | .rule = "not-bound" | .count = 3')" ;;
        denials-many)
          sandbox_event "denied:$state" sandbox.denied "$(sandbox_golden "$state" sandbox-denied.json)"
          sandbox_event "denied-path:$state" sandbox.denied "$(sandbox_golden "$state" sandbox-denied.json | jq -c '.kind = "path" | .target = "/home/thor/.config/gh" | .rule = "not-bound" | .count = 3')"
          sandbox_event "denied-never:$state" sandbox.denied "$(sandbox_golden "$state" sandbox-denied.json | jq -c '.kind = "path" | .target = "/home/thor/.ssh" | .rule = "deny-list" | .count = 2')"
          sandbox_event "denied-tool:$state" sandbox.denied "$(sandbox_golden "$state" sandbox-denied.json | jq -c '.kind = "policy" | .target = "git push" | .rule = "inner-policy" | .count = 4')"
          sandbox_event "denied-overflow:$state" sandbox.denied "$(sandbox_golden "$state" sandbox-denied-overflow.json)" ;;
        waiting)
          # the engine launch found the session cap full (SND-LAUNCH-020): no launch.started, since it ends the wait.
          sandbox_event "waiting:$state" launch.waiting "$(jq -cn --arg l "$l" --arg i "$item" --arg a "$instance" '{launchId: $l, item: $i, running: 3, cap: 3, instance: $a, state: "build", attempt: 1}')" ;;
        limit-memory)
          sandbox_event "limit-hit:$state" sandbox.limit-hit "$(sandbox_golden "$state" sandbox-limit-hit.json | jq -c '.kind = "memory"')"
          sandbox_event "ended:$state" session.ended "$(sandbox_golden "$state" session-ended-limit.json)" ;;
        limit-wall)
          sandbox_event "limit-hit:$state" sandbox.limit-hit "$(sandbox_golden "$state" sandbox-limit-hit.json | jq -c '.kind = "wall"')"
          sandbox_event "ended:$state" session.ended "$(sandbox_golden "$state" session-ended-limit.json | jq -c '.kind = "wall" | .limit = 2400 | .peaks.wallSeconds = 2400')" ;;
        takeover)
          sandbox_event "takeover:$state" launch.takeover-opened "$(jq -cn --arg l "$l" --arg i "$item" --arg a "$instance" '{launchId: $l, item: $i, instance: $a, viewer: {kind: "warp", label: "Warp", outcome: "opened", code: null, detail: null}}')" ;;
        learning | attended) : ;;
      esac ;;
  esac
  printf '%s\n' "$instance"
}

verb="${1:-}"
case "$verb" in
  -h | --help) usage; exit 0 ;;
  "") usage >&2; exit 2 ;;
  --large) die "--large belongs to 'items': sindri-ui-fixture.sh items --large" ;;
  up | stall | unstall | drift | down | fail | unfail) [ "$#" -eq 1 ] || die "'$verb' takes no arguments" ;;
  items) [ "$#" -eq 1 ] || { [ "$#" -eq 2 ] && [ "$2" = "--large" ]; } || die "usage: items [--large]" ;;
  close) [ "$#" -eq 2 ] || { [ "$#" -eq 3 ] && [ "$3" = "--cancelled" ]; } || die "usage: close <item> [--cancelled]" ;;
  projects) [ "$#" -eq 1 ] || die "'projects' takes no arguments" ;;
  linear-catchup) { [ "$#" -eq 1 ] || { [ "$#" -eq 2 ] && case "$2" in empty | typical | error | read-error) true ;; *) false ;; esac; }; } || die "usage: linear-catchup [empty|typical|error|read-error]" ;;
  todos) [ "$#" -eq 1 ] || die "'todos' takes no arguments" ;;
  resources) [ "$#" -eq 1 ] || die "'resources' takes no arguments" ;;
  connections) { [ "$#" -eq 2 ] && case "$2" in empty | typical | error | down) true ;; *) false ;; esac; } || die "usage: connections empty|typical|error|down" ;;
  reviews) { [ "$#" -eq 2 ] && { [ "$2" = up ] || [ "$2" = down ]; }; } || die "usage: reviews up|down" ;;
  request) [ "$#" -ge 2 ] || die "usage: request [--item ID] [--repo R] <text>" ;;
  context)
    case "${2:-}" in
      up | draft | down | sources) [ "$#" -eq 2 ] || die "usage: context up|draft|down|sources|publish <state>" ;;
      publish)
        [ "$#" -eq 3 ] || die "usage: context publish held|overdue|published|without-waiting|stale|by-hand|pause|resume"
        case "$3" in held | overdue | published | without-waiting | stale | by-hand | pause | resume) ;; *) die "context publish: '$3' is not a state (held, overdue, published, without-waiting, stale, by-hand, pause, resume)" ;; esac ;;
      whole) { [ "$#" -eq 3 ] && { [ "$3" = over ] || [ "$3" = under ] || [ "$3" = concepts ] || [ "$3" = pages ]; }; } || die "usage: context whole over|under|concepts|pages" ;;
      *) die "usage: context up|draft|down|sources|publish <state>|whole over|under|concepts|pages" ;;
    esac ;;
  sessions)
    case "${2:-}" in
      up | disable | enable | down) [ "$#" -eq 2 ] || die "usage: sessions up|ask <pid>|end <pid>|back <pid>|disable|enable|down" ;;
      ask | end | back) [ "$#" -eq 3 ] || die "usage: sessions $2 <pid>" ;;
      *) die "usage: sessions up|ask <pid>|end <pid>|back <pid>|disable|enable|down" ;;
    esac ;;
  hide-item) { [ "$#" -eq 2 ] && case "$2" in ITM-*[!0-9a-f]* | ITM-) false ;; ITM-*) true ;; *) false ;; esac; } || die "usage: hide-item <ITM-id>" ;;
  model-calls)
    [ "$#" -eq 2 ] || die "usage: model-calls fell-back|no-answer|paused|none"
    case "$2" in fell-back | no-answer | paused | none) ;; *) die "model-calls: '$2' is not a state (fell-back, no-answer, paused, none)" ;; esac ;;
  sandbox)
    case "${2:-}" in
      blocked | clear) [ "$#" -eq 2 ] || die "usage: sandbox blocked|clear" ;;
      denials | denials-many | waiting | limit-memory | limit-wall | learning | takeover | attended)
        [ "$#" -eq 4 ] || die "usage: sandbox $2 <ITM-id> <instance-address>"
        case "$3" in ITM-*[!0-9a-f]* | ITM-) die "sandbox: '$3' is not an item id" ;; ITM-*) ;; *) die "sandbox: '$3' is not an item id" ;; esac
        [ -n "$4" ] || die "sandbox: the instance address is empty" ;;
      *) die "usage: sandbox blocked|clear | sandbox denials|denials-many|waiting|limit-memory|limit-wall|learning|takeover|attended <ITM-id> <instance-address>" ;;
    esac ;;
  decide) [ "$#" -eq 3 ] || die "usage: decide <DEC-id> <choice>" ;;
  *) usage >&2; die "unknown verb: $verb" ;;
esac

guard
shift
"${verb//-/_}" "$@"
