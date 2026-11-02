#!/usr/bin/env bash
# scripts/sindri-ui-fixture.sh — the Sindri UI's proof fixture (UI spec §18.2, first part: U1). It builds the state a
# UI phase is proven against in a guarded temp home, through scripts/sindri-prove.sh only (its guard runs before
# anything is written, so the owner's real home is never touched).
#
#   sindri-ui-fixture.sh up        write the live profile (a heartbeat service, port 0), approve it as the owner and
#                                  start the control plane (sindri --headless, through sindri-prove.sh human)
#   sindri-ui-fixture.sh stall     insert one row of a registered event type with an unparseable payload into
#                                  <home>/ledger.db: the projection throws and the views stall (the stalled banner)
#   sindri-ui-fixture.sh unstall   delete that row again (then Rebuild views recovers)
#   sindri-ui-fixture.sh drift     edit the live profile so it no longer matches the approval (a profile row)
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
#   sindri-ui-fixture.sh decide <DEC-id> <choice>
#                                  decide at the CLI (sindri decide, as the owner): with the app open on the same
#                                  decision, the app's own Decide is then refused (SND-ITEM-409)
#   sindri-ui-fixture.sh fail      add a service the build cannot run to the live profile and approve it (a failure row)
#   sindri-ui-fixture.sh unfail    remove it again and approve (the row clears)
#                                  (items --large also files 12 requests, in FIXTURE_REPO when set, else with no repo)
#   sindri-ui-fixture.sh workflows [--large]
#                                  start the strip's instances through the CLI (sindri workflow start, as the owner, each on a
#                                  new item): one triage and two work instances; --large starts twelve roots across three recipes
#                                  (each dev recipe is approved as the owner first, which registers it: they aren't embedded)
#   sindri-ui-fixture.sh line-run  (run after 'context up')
#                                  start a workflow.line instance (the run page's recipe) on a new item in the context repo, as the
#                                  owner; prints the item. The item has no frozen scope: no scoping packet was approved for it
#   sindri-ui-fixture.sh hide-item <ITM-id>
#                                  delete the item's row from <home>/views.db (view_items): the control plane stays up and the run
#                                  still serves, but GET /sindri/items/<id> and /scope answer SND-ITEM-404 for it (gh-1046: the
#                                  Context page's "Could not read the context of <item>"). Derived state: 'views rebuild' brings it back
#   sindri-ui-fixture.sh park-timeout
#                                  start workflow.dev-timeout (a desk Step with a 1m timeout that parks) on a new item and print the item; after
#                                  about a minute the instance is parked by its timeout and offers answer kind resume (ApplyCommand:answer-resume).
#                                  Run 'up' with FIXTURE_DESK_TIMEOUT=1m first, or the desk floor (default 2d) holds the park back
#   sindri-ui-fixture.sh changed-recipe
#                                  Needs-you rows for workflows (run after 'up'): approves an older, smaller version of the shipped
#                                  workflow.hand-test as the owner (a desk step instead of the agent step, so the shipped version gains a
#                                  capability), which gives Home a "Approve a changed workflow" row for it, and, since no other shipped
#                                  workflow is approved, the "Approve all new workflows" row as well
#   sindri-ui-fixture.sh incident start|stop
#                                  start an incident-urgency instance (recipes/dev/incident-hold.yaml, approved first; it tops the strip) or cancel it
#   sindri-ui-fixture.sh context up|draft|down
#                                  the Context page's data (U9; run after 'up'): up writes a git repo, aw-split-panes, under
#                                  <home>/ui-fixture-context (the tabs repo of tests/Aw.Context.Testing/Fixtures/MapTabsRepo.cs),
#                                  adds it to the live profile's repos and approves that, then imports the tabs map
#                                  (map-tabs.json) as the owner, typing 'import map <hash6>', and prints the repo name
#                                  (the app's repo= argument); draft runs 'context map build' as the owner (a draft to
#                                  review; a missing graphify is a degraded line, not a failure); down removes the repo, the
#                                  map copy and the profile entry (and approves again). Idempotent: a repeat up imports the map
#                                  only when the store has no map or the map file changed (the imported version stays in
#                                  context.db through down, so the next draft's number depends on earlier runs); up and down
#                                  approve whenever the profile is not approved
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
#   sindri-ui-fixture.sh projects  (run after 'context up')
#                                  the Projects page's data (projects page spec 4.1, 5, 12 Ph5, 13.1): seeds six Linear projects into
#                                  <home>/context/context.db (source_record, record_link; source 'linear-projects', audience linear:SPL, a
#                                  'led-by' link each): Checkout rewrite (in progress, at risk) and Search relevance v2 (planned), both led
#                                  by the viewer, Onboarding revamp (completed 3 days ago, led by the viewer: the Finished fold), Billing
#                                  migration (paused, Dana Okafor), Mobile offline sync and Platform API v3 (in progress, Lee Tran and Mira
#                                  Costa); open and closed Linear issues with 'in-project' links (the cards' open counts); the viewer's
#                                  'source_identity' row (Sam Rivera: "Linear says you're Sam Rivera"); and both Linear sources' cursor rows
#                                  (phase incremental), so the page is past its first read and not counting. It adds
#                                  context.sources.linear.teams [SPL] to the live profile (a human reads only the profile's teams) and
#                                  approves that. Then, as the owner: attaches Billing migration, adds a hand-made project (Hiring loop) and
#                                  hides Search relevance v2 (a project the viewer leads: Hide acts only on a project on the page), each once (the markers <home>/ui-fixture-projects-owner-attach|add|hide; the store rows are rewritten
#                                  every run, so a repeat adds no row and no project). The store is read when the page asks, so no rebuild is
#                                  needed; the owner's writes are ledger events and refresh the view. Exit 2 without a context store (run
#                                  'context up' first) or when the profile already has a context: section without the SPL team.
#
#   sindri-ui-fixture.sh sessions up|ask <pid>|end <pid>|back <pid>|disable|enable|down
#                                  the Agents page's data (U11a; run after 'up', and run 'sessions up' right after every 'up': a
#                                  control plane started without it reads the real box). up copies tests/fixtures/sessions/
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
#                                  removes the copy. Every restart re-exports the four variables while the copy exists, and
#                                  down never restarts. Needs jq.
#
#   sindri-ui-fixture.sh agent-run
#                                  one workflow instance on a live agent Step (recipes/dev/workflow.dev-agent-hold.yaml; run after
#                                  'sessions up'), for the Attach control: Watch the agent, and Take over in its chevron menu. It adds
#                                  a git repo, agent-fixture, under <home>/ui-fixture-agent to the live profile with
#                                  workflow.agent.target claude-code and approves that, points launcher.json (the sessions copy's)
#                                  at a stub claude-code (a script that only sleeps, so no model and no network), registers and
#                                  approves the recipe as the owner, restarts the control plane (CLAUDE_CONFIG_DIR inside the home, so
#                                  the launcher's trust mark never touches the real ~/.claude.json) and starts the recipe on a new item.
#                                  The launch is real: a tmux session on Sindri's own socket, a worktree under the repo and a
#                                  'session wrap' heartbeat; it waits (FIXTURE_AGENT_WAIT s, default 120) for launch.started in the ledger, then prints the item
#                                  id on stdout (progress goes to stderr). The Step takes a slot of the box's heavy lock first, so a
#                                  busy lock delays the launch. Exit 1 when no launch started in time; needs jq and git.
#
#   sindri-ui-fixture.sh model-picks picked|escalated|unpriced|picking <ITM-id> <instance-address>
#                                  the model pick rows of one run (model-pick spec section 6.1) for the instance frame's Model picks
#                                  table, written as ledger rows the picks route reads (model.picking, model.picked, session.usage and,
#                                  for a retry, launch.failed): picked is one run on Haiku chosen by Jev with tokens and a price;
#                                  unpriced is one run on a model the price table lacks ("cost unknown"); picking is a model.picking
#                                  with no model.picked ("choosing model..."); escalated is a Haiku run that failed to start and a Sonnet
#                                  retry on the same state and attempt, chosen by rules. The ids are fixed per state, so a rerun
#                                  writes the same rows (INSERT OR IGNORE). The item and the instance must exist (see 'workflows'): the
#                                  frame opens the instance and reads its picks by address. Needs jq and sqlite3 or python3.
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
#   sindri-ui-fixture.sh model-picks-run picked|escalated|unpriced|picking
#                                  one workflow instance whose Model picks table (the instance frame; model-pick spec section 6.1,
#                                  issue #1452) shows that state, made by a real agent run with the pick on (run after 'sessions up'; each
#                                  call is its own item, so run it once per state). It does what agent-run does (the agent-fixture repo, a
#                                  stub claude-code, the recipe recipes/dev/workflow.dev-agent-pick.yaml, a restart) and also writes
#                                  workflow.agent.pick "on" and a models list into the profile, approves it as the owner (the approval
#                                  that shows the Jev notice is the pick's consent), and restarts the control plane with AW_SINDRI_JUDGE
#                                  on a stub judge that answers m1 as Jev. The stub claude writes a transcript under CLAUDE_CONFIG_DIR
#                                  (the usage reader reads it, so the run has tokens on the model it ran on) and reports the recipe's
#                                  payload fact. picked: models [claude-haiku-4-5, claude-sonnet-4-5], one good run on Haiku, chosen by Jev.
#                                  escalated: the same models, but the stub sends a report that is not JSON twice on Haiku (the second bad
#                                  report ends its session, SND-WF-008), so the engine retries once on Sonnet under the -e1 launch id
#                                  (M-6) and that run reports well. unpriced: as escalated with [claude-haiku-4-5, fixture-unpriced-model],
#                                  so the retry runs on a model the price table lacks ("cost unknown" on its row, "cost partial" on the
#                                  total). picking: the judge stub holds its answer, so the instance sits at "choosing model..." for the
#                                  length of the judge call, 40 s (the engine's timeout), and then the rules pick. Prints the item id on
#                                  stdout; waits (FIXTURE_AGENT_WAIT s, default 120) for the rows the state needs in the ledger
#                                  (model.picking for picking; model.picked and session.usage, twice for the retry, for the others).
#                                  Exit 1 when they do not come in time; needs jq and git.
#
#   sindri-ui-fixture.sh sandbox blocked|clear
#   sindri-ui-fixture.sh sandbox denials|denials-many|waiting|limit-memory|limit-wall|learning|takeover|attended <ITM-id> <instance-address>
#                                  the sandbox surfaces' states (sandbox spec section 8, States; goldens tests/Aw.Sindri.Tests/Golden/Sandbox/).
#                                  blocked and clear need a live answer from the control plane (/sindri/sandbox/status and /check), which
#                                  ledger rows can't fake, so they restart serve (as 'sessions up' does) with AW_SANDBOX_BWRAP=/nonexistent
#                                  (blocked: no bubblewrap, the Health row, the Settings section and the park card show the first blocker) or
#                                  with it unset (clear: needs a working bwrap, nothing blocks). The setting is remembered in
#                                  <home>/ui-fixture-sandbox/bwrap, so every later restart keeps it. blocked then starts the agent-run
#                                  recipe and waits (FIXTURE_AGENT_WAIT s) for its launch.failed: that Step is parked on SND-LAUNCH-014 no-bwrap;
#                                  run blocked twice for two parked Steps, then clear for the resume list (one Step may be closed first, so the
#                                  list shows "has moved on"). Run after 'sessions up'; needs jq and git.
#                                  The other states are ledger rows on a launch of an existing item (see 'workflows'), the same way
#                                  model-picks writes them (fixed ids, INSERT OR IGNORE, a rerun adds nothing): every one starts with the
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
# FIXTURE_DESK_TIMEOUT (up only): writes workflow.deskTimeout into the fixture profile, the floor of every desk Step timeout (park-timeout).
# FIXTURE_AGENT_WAIT is how many seconds agent-run waits for launch.started (default 120; a saturated box needs more).
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
  # The desk floor (workflow.deskTimeout, default 2d) raises every desk Step's timeout; park-timeout needs it low.
  if [ -n "${FIXTURE_DESK_TIMEOUT:-}" ]; then
    printf 'workflow:\n  deskTimeout: %s\n' "$FIXTURE_DESK_TIMEOUT" >> "$profile"
  fi
  approve || return
  "$PROVE" human --headless
}

# Validates the live profile and approves its hash as the owner, typing the first six characters.
approve() {
  local hash
  hash="$("$PROVE" profile validate --json | jq -r '.hash // empty')"
  [ -n "$hash" ] || die "profile validate gave no hash"
  printf 'approve profile %s\n' "${hash:0:6}" | "$PROVE" human profile approve "$hash"
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

# A changed cadence changes the profile's hash (comments and key order don't), so the approval no longer matches.
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
  # 12 requests (U3's large Needs-you list), filed as a session. The API refuses a repo the approved profile lacks and the
  # fixture's profile has none, so they name FIXTURE_REPO when it is set (a repo the profile has) and no repo otherwise.
  for n in $(seq 1 12); do
    if [ -n "${FIXTURE_REPO:-}" ]; then
      request --repo "$FIXTURE_REPO" "Large fixture request $n" || return
    else
      request "Large fixture request $n" || return
    fi
  done
}

close() {
  [[ "$1" =~ ^[A-Za-z]+-[A-Za-z0-9]+$ ]] || die "close: '$1' is not an item id"
  if [ "${2:-}" = "--cancelled" ]; then "$PROVE" human item close "$1" --cancelled; else "$PROVE" human item close "$1"; fi
}

# A new item, its id printed (the id is the first ITM- token sindri item add prints).
new_item() {
  local out id
  out="$(add "$1")" || return
  id="$(printf '%s\n' "$out" | grep -o 'ITM-[A-Za-z0-9]*' | head -1)"
  [ -n "$id" ] || die "item add printed no item id: $out"
  printf '%s\n' "$id"
}

# The inputs the dev recipes declare: triage-three-state takes fact:item (JSON naming the item) and fact:scope (text, its name), desk-round-trip takes
# fact:request (text). The files live in the guarded home, one folder for all of them.
fact_args() { # $1 recipe, $2 item, $3 title; prints the --fact arguments, one for each line
  local dir="$AW_SINDRI_HOME/ui-fixture-facts"
  mkdir -p "$dir" || return
  case "$1" in
    *triage-three-state)
      jq -nc --arg id "$2" '{id: $id}' > "$dir/$2.item.json" || return
      printf 'Fixture scope for %s\n' "$3" > "$dir/$2.scope.txt" || return
      printf '%s\n%s\n%s\n%s\n' --fact "fact:item=$dir/$2.item.json" --fact "fact:scope=$dir/$2.scope.txt" ;;
    *dev-agent-hold | *dev-agent-pick)
      # ItemFact is positional and AwJson requires every constructor parameter, so the item names its spec too (#1134).
      jq -nc --arg id "$2" --arg repo "$AGENT_REPO" --arg spec "Fixture spec for $3" '{id: $id, repo: $repo, spec: $spec}' > "$dir/$2.item.json" || return
      printf '%s\n%s\n' --fact "fact:item=$dir/$2.item.json" ;;
    *desk-round-trip)
      printf 'Fixture request for %s\n' "$3" > "$dir/$2.request.txt" || return
      printf '%s\n%s\n' --fact "fact:request=$dir/$2.request.txt" ;;
  esac
}

# The dev recipes aren't embedded (the host ships recipes/sindri only), so a start needs its recipe approved first, which registers it (#1343).
# Approves recipes/dev's recipe with id $1 as the owner, typing "approve <id> <version>"; a recipe outside recipes/dev is left alone.
approve_dev_recipe() { # $1 recipe id
  local file out
  file="$(grep -lx "id: $1" "$ROOT"/recipes/dev/*.yaml 2>/dev/null | head -1)"
  [ -n "$file" ] || return 0
  out="$(printf 'approve %s %s\n' "$1" "$(sed -n 's/^version: *//p' "$file" | head -1)" | "$PROVE" human recipe approve "$file" 2>&1)" \
    || die "the recipe $1 was not approved: $out"
}

start_on_new_item() { # $1 recipe, $2 item title; prints the item
  local item facts=() line out
  need jq
  item="$(new_item "$2")" || return
  while IFS= read -r line; do facts+=("$line"); done < <(fact_args "$1" "$item" "$2")
  # The pty the owner's verbs run under joins stderr into stdout, so the output is kept to show on a refusal.
  if ! out="$("$PROVE" human workflow start "$1" --item "$item" ${facts[@]+"${facts[@]}"} 2>&1)"; then
    die "workflow start $1 on $item was refused: $out"
  fi
  printf 'started %s on %s\n' "$1" "$item" >&2
  printf '%s\n' "$item"
}

# One triage and two work instances (screens §8.2); --large: twelve roots, four on each of three recipes.
workflows() {
  local n r
  if [ "${1:-}" = "--large" ]; then
    for r in workflow.dev-triage-three-state workflow.dev-desk-round-trip workflow.dev-notify; do approve_dev_recipe "$r" || return; done
    for n in 1 2 3 4; do
      for r in workflow.dev-triage-three-state workflow.dev-desk-round-trip workflow.dev-notify; do
        start_on_new_item "$r" "Large fixture workflow $r $n" >/dev/null || return
      done
    done
    return 0
  fi
  approve_dev_recipe workflow.dev-triage-three-state || return
  approve_dev_recipe workflow.dev-desk-round-trip || return
  start_on_new_item workflow.dev-triage-three-state "Fixture triage" >/dev/null || return
  start_on_new_item workflow.dev-desk-round-trip "Fixture work one" >/dev/null || return
  start_on_new_item workflow.dev-desk-round-trip "Fixture work two" >/dev/null
}

# workflow.line is not in the fixture's registry: its effects go into the live profile (as sindri-sandbox-e2e.sh writes them), the profile
# is approved again, and the owner's approve-all approves every shipped recipe, children first. Idempotent.
line_recipes_approved() {
  local profile out i
  profile="$(profile_path)"
  [ -f "$profile" ] || die "line-run: no live profile at $profile: run 'up' first"
  if "$PROVE" human recipe list 2>&1 | grep -Eq '^workflow\.line@[0-9a-f]+ approved'; then return 0; fi
  if ! grep -q 'effect.line.start-phase' "$profile"; then
    grep -q '^workflow:' "$profile" && die "line-run: the live profile already has a workflow: section; add the line's effects to it"
    {
      echo "workflow:"
      echo "  effects: [effect.line.report, effect.claim.ref, effect.pr.open, effect.notify.desk, effect.pr.push, effect.pr.reply, effect.pr.record, effect.pr.rerun-checks, effect.pr.restack, effect.pr.create-stacked, effect.pr.merge, effect.pr.merge-docs, effect.pr.purge-marks, effect.line.start-phase]"
      echo "  forge: { adapter: file, hosts: [forge.test] }"
      echo "  build:"
      echo "    commands:"
      echo "      - 'true'"
    } >> "$profile" || return
  fi
  if needs_approval; then approve >&2 || return; fi
  out="$("$PROVE" human recipe approve-all 2>&1)" || die "line-run: recipe approve-all was refused: $out"
  for i in $(seq 1 120); do
    if "$PROVE" human recipe list 2>&1 | grep -Eq '^workflow\.line@[0-9a-f]+ approved'; then return 0; fi
    sleep 1
  done
  die "line-run: workflow.line is not approved 120 s after approve-all: $("$PROVE" human recipe list 2>&1 | tail -5 | tr '\n' ' ')"
}

# A line run on a new item of the context repo (gh-1046: the run page's See its context needs a run with an item). The item has no
# frozen scope, as a fresh item never has.
line_run() {
  local item dir out facts="$AW_SINDRI_HOME/ui-fixture-facts"
  need jq
  dir="$(context_dir)/$CONTEXT_REPO"
  [ -d "$dir/.git" ] || die "line-run: run 'context up' first"
  mkdir -p "$facts" || return
  line_recipes_approved || return
  item="$(FIXTURE_REPO="$CONTEXT_REPO" new_item "Fixture line run")" || return
  printf '{"kind":"brief","repo":"%s","path":"README.md"}\n' "$CONTEXT_REPO" > "$facts/scopeInput.json"
  printf '{"line":"line","phase":"1","attempt":1,"base":"%s","runArea":{}}\n' "$(git -C "$dir" rev-parse HEAD)" > "$facts/lineContext.json"
  if ! out="$("$PROVE" human workflow start workflow.line --item "$item" --fact "fact:scopeInput=$facts/scopeInput.json" --fact "fact:lineContext=$facts/lineContext.json" 2>&1)"; then
    die "workflow start workflow.line on $item was refused: $out"
  fi
  printf '%s\n' "$item"
}

hide_item() {
  need_sql
  local db="$AW_SINDRI_HOME/views.db"
  [ -f "$db" ] || die "no views at $db: run 'up' first"
  sql_on "$db" "DELETE FROM view_items WHERE key = '$1'; SELECT changes() || ' row(s) removed';"
}

park_timeout() {
  approve_dev_recipe workflow.dev-timeout || return
  start_on_new_item workflow.dev-timeout "Fixture timeout park"
}

# The older version of a shipped workflow, approved by the owner so the shipped one waits for its own approval (it can do more): a "workflow . changed" row.
changed_recipe() {
  local dir="$AW_SINDRI_HOME/ui-fixture-recipes" file out
  mkdir -p "$dir" || return
  file="$dir/workflow.hand-test.yaml"
  cat > "$file" <<'YAML' || return
schema: 1
id: workflow.hand-test
version: 2
description: "Block recipe: an earlier, smaller version of the hand test for the fixture (a desk step, no agent)."
displayName: "Try the change by hand in the running app"
inputs:
  - { key: fact:phasePacket, label: "Phase packet", type: packet, kind: packet }
  - { key: fact:workspace, label: "Workspace", type: text, kind: text }
outputs:
  - { key: fact:output, label: "Output", type: text, from: handtest }
initial: handtest
states:
  handtest: { kind: active, step: step.produce, timeout: { after: 4h, then: park },
              next: "Try the change for {item} by hand, then complete this Step." }
  works:    { kind: terminal, outcome: resolved }
transitions:
  - { from: handtest, to: works, on: step.completed, when: { outcome: done }, produces: [fact:output] }
evals: [evals/workflow.hand-test/happy.jsonl]
YAML
  # The registry refuses an approval without eval cases (SND-RECIPE-008): the file the yaml names sits next to it.
  mkdir -p "$dir/evals/workflow.hand-test" || return
  cat > "$dir/evals/workflow.hand-test/happy.jsonl" <<'JSONL' || return
{"start":{"inputs":{"fact:phasePacket":{"phase":"1","goal":"Export works"},"fact:workspace":"/work/tree"}}}
{"trigger":{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"Clicked export; a CSV downloaded."}},"expect":{"state":"works","outcome":"resolved"}}
JSONL
  out="$(printf 'approve workflow.hand-test 2\n' | "$PROVE" human recipe approve "$file" 2>&1)"
  printf '%s\n' "$out" | grep -qE '^(approved|already approved)' || die "recipe approve of the older workflow.hand-test was refused: $out"
}

incident_file() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-incident-item"; }

incident() {
  local item file
  file="$(incident_file)"
  case "$1" in
    start)
      approve_dev_recipe workflow.dev-incident-hold || return
      item="$(start_on_new_item workflow.dev-incident-hold "Fixture incident")" || return
      printf '%s\n' "$item" > "$file"
      printf '%s\n' "$item" ;;
    stop)
      [ -f "$file" ] || die "no incident started by this fixture: run 'incident start' first"
      item="$(cat "$file")"
      "$PROVE" human workflow cancel "$item" && rm -f -- "$file" && printf 'cancelled %s\n' "$item" ;;
  esac
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
  # Approve whenever the profile isn't approved (not only after this run's edit): a failed approve is retried here.
  if needs_approval; then approve >&2 || return; fi
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

# True when the live profile isn't the approved one (or validate can't say).
needs_approval() { [ "$("$PROVE" profile validate --json 2>/dev/null | jq -r '.approved // false')" != true ]; }

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
  # Whether or not this run edited the profile: an approve that failed on an earlier down is tried again.
  if [ -f "$profile" ] && needs_approval; then
    (approve >&2) || echo "context down: the profile is not approved; run 'context down' again or approve it (sindri profile approve)" >&2
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
    awk -v e="context:\n  sources:\n    linear: { teams: [$PROJECTS_TEAM] }" '{print} /^user:/ {print e}' "$profile" > "$profile.new" && mv "$profile.new" "$profile" \
      || die "projects: can't add the Linear team to $profile"
  fi
  # Approve whenever the profile isn't approved (a failed approve is retried here).
  if needs_approval; then approve >&2 || return; fi
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

projects() {
  need_sql
  local db marker target
  db="$(context_db)"
  [ -f "$db" ] || die "no context store at $db: run 'context up' first"
  sql_on "$db" "SELECT 1 FROM source_identity LIMIT 1" >/dev/null 2>&1 || die "the context store at $db has no source_identity table (migration 0020): rebuild sindri and run 'context up' first"
  projects_profile || return

  local projects_sql="DELETE FROM record_link WHERE from_key LIKE 'linear:project/fx-%' OR from_key LIKE 'linear:issue/fx-%';
DELETE FROM source_record WHERE key LIKE 'linear:project/fx-%' OR key LIKE 'linear:issue/fx-%';
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
  # A map built over the import (a refresh under the approved profile) has no $SOURCES_DOC_NODE: fail here, not later as a panel error.
  local shown
  shown="$("$PROVE" human context show "$SOURCES_DOC_NODE" 2>&1)" || die "context sources: the imported map was replaced, $SOURCES_DOC_NODE is gone: $shown"
  printf '%s\n%s\n%s\n' "$CONTEXT_REPO" "$SOURCES_DOC_NODE" "$SOURCES_TICKET_NODE"
}

SESSIONS_DIRNAME="ui-fixture-sessions"
sessions_dir() { printf '%s\n' "$AW_SINDRI_HOME/$SESSIONS_DIRNAME"; }

# The collector's overrides, pointed at the copy. AW_LAUNCHER_CONFIG keeps the owner's launcher.json (under $AW_STATE_DIR) out too.
sessions_export() {
  local d
  d="$(sessions_dir)"
  export AW_SESSIONS_PROC_ROOT="$d/proc" AW_SESSIONS_CLAUDE_DIR="$d/claude-sessions" AW_FLEET_DIR="$d/fleet" AW_LAUNCHER_CONFIG="$d/launcher.json"
  # agent-run's launches mark their worktree trusted in Claude Code's config: keep that file inside the home.
  if [ -d "$(agent_dir)/claude-config" ]; then export CLAUDE_CONFIG_DIR="$(agent_dir)/claude-config"; fi
  # 'sandbox blocked|clear' keeps AW_SANDBOX_BWRAP across restarts.
  if [ -s "$(sandbox_dir)/bwrap" ]; then export AW_SANDBOX_BWRAP="$(cat "$(sandbox_dir)/bwrap")"; else unset AW_SANDBOX_BWRAP; fi
  # model-picks-run's judge stub stays the judge of every later restart (the real judge would call a model provider).
  if [ -x "$(agent_dir)/stub-judge/judge" ]; then export AW_SINDRI_JUDGE="$(agent_dir)/stub-judge/judge"; fi
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

# Stops the control plane and removes the copy. It does not restart: a later 'up' without 'sessions up' would read the real box.
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

# --- agent-run: an instance on a live agent Step (the Attach control; issue #1004) ---
AGENT_REPO="agent-fixture"
AGENT_RECIPE="workflow.dev-agent-hold"

agent_dir() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-agent"; }

# A git repo with one commit, the stub agent (sleeps, ignores the flags the launcher adds) and Claude Code's config folder.
agent_files() {
  local dir="$1" repo="$1/repo" g
  mkdir -p "$dir/claude-config" "$repo" || return
  if [ ! -d "$repo/.git" ]; then
    g=(env -u GIT_DIR -u GIT_WORK_TREE -u GIT_INDEX_FILE -u GIT_COMMON_DIR GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1 GIT_AUTHOR_NAME="Fixture Author" GIT_AUTHOR_EMAIL=fixture@example.invalid
       GIT_COMMITTER_NAME="Fixture Author" GIT_COMMITTER_EMAIL=fixture@example.invalid git -C "$repo")
    printf '# Agent fixture\n' > "$repo/README.md"
    "${g[@]}" init -q -b main && "${g[@]}" config core.hooksPath "$repo/.no-hooks" && "${g[@]}" add -A \
      && "${g[@]}" commit -q --no-gpg-sign -m "Agent fixture" || die "agent-run: can't build the repo in $repo"
  fi
  # Named claude: the argv hash keeps only bin's base name, so a stub with another name is not a bin-only override (#1134).
  mkdir -p "$dir/stub-bin" || return
  printf '#!/bin/sh\nexec sleep 86400\n' > "$dir/stub-bin/claude" && chmod 755 "$dir/stub-bin/claude"
}

# The live profile gets the repo and workflow.agent.target (a workflow: block after user: when there is none).
agent_profile_add() {
  local profile="$1" repo="$2"
  if ! grep -q "name: $AGENT_REPO" "$profile"; then
    if grep -q '^repos:' "$profile"; then
      awk -v e="  - { name: $AGENT_REPO, path: $repo }" '{print} /^repos:/ {print e}' "$profile" > "$profile.new"
    else
      awk -v e="repos:\n  - { name: $AGENT_REPO, path: $repo }" '{print} /^user:/ {print e}' "$profile" > "$profile.new"
    fi && mv "$profile.new" "$profile" || return
  fi
  if ! grep -q '^  agent:' "$profile"; then
    if grep -q '^workflow:' "$profile"; then
      awk '{print} /^workflow:/ {print "  agent: { target: claude-code }"}' "$profile" > "$profile.new"
    else
      awk '{print} /^user:/ {print "workflow:\n  agent: { target: claude-code }"}' "$profile" > "$profile.new"
    fi && mv "$profile.new" "$profile" || return
  fi
}

agent_run() {
  need jq
  need git
  sessions_need_copy
  local dir profile launcher item out
  dir="$(agent_dir)"
  profile="$(profile_path)"
  launcher="$(sessions_dir)/launcher.json"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  agent_files "$dir" || return
  agent_profile_add "$profile" "$dir/repo" || die "agent-run: can't edit $profile"
  if needs_approval; then approve >&2 || return; fi
  # A bin-only override of the built-in claude-code needs no stepArgvHash; the stub ignores the --settings and the other flags a Step adds.
  jq --arg bin "$dir/stub-bin/claude" '.agents["claude-code"] = {override: true, bin: $bin}' "$launcher" > "$launcher.new" && mv -f "$launcher.new" "$launcher" \
    || die "agent-run: can't update $launcher"
  unset TMUX   # the control plane starts its own tmux server on its own socket; it must not inherit the caller's
  sessions_restart >&2 || return
  approve_dev_recipe "$AGENT_RECIPE" || return
  item="$(FIXTURE_REPO="$AGENT_REPO" start_on_new_item "$AGENT_RECIPE" "Fixture agent run")" || return
  local wait="${FIXTURE_AGENT_WAIT:-120}"
  [[ "$wait" =~ ^[0-9]+$ ]] || die "agent-run: FIXTURE_AGENT_WAIT '$wait' is not a number of seconds"
  local deadline=$((SECONDS + wait))
  while [ "$SECONDS" -lt "$deadline" ]; do
    if "$PROVE" human ledger --item "$item" --max 1000 2>&1 | tr -d '\r' | grep -q "${FIXTURE_AGENT_WANT:-launch.started}"; then
      printf '%s\n' "$item"
      return 0
    fi
    sleep 1
  done
  echo "sindri-ui-fixture: agent-run: no ${FIXTURE_AGENT_WANT:-launch.started} for $item in $wait s; the ledger says:" >&2
  "$PROVE" human ledger --item "$item" --max 1000 2>&1 | tr -d '\r' | tail -15 >&2
  return 1
}

# --- model-picks: the Model picks table's states (model-pick spec 6.1, issue #1206 M-8) ---

# $1 key, $2 type, $3 payload JSON. A fixed id and idempotency key, so a rerun adds nothing (see publish_event).
pick_event() {
  local key="sindri-ui-fixture:model-picks:$1" id corr
  id="$(printf '%s' "$key" | cksum | awk '{printf "5c0f1e01-%04x-4d1e-9a55-3c2f6d8e1b05", $1 % 65536}')"
  corr="${id%?}6"
  ledger "INSERT OR IGNORE INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
          VALUES ('$id', '$2', 1, strftime('%Y-%m-%dT%H:%M:%S', 'now') || '.0000000Z', 'sindri-ui-fixture', 'local', 0, '$corr',
                  COALESCE((SELECT MAX(epoch) FROM events), 0), '$key', '$3');" >/dev/null
}

# $1 launch number (1 or 2), $2 item, $3 instance; prints the launch id (LCH- and 32 hex digits).
pick_launch() { printf 'LCH-%032x\n' "$((0x5c0f1e00 + $1))"; }

# $1 launch, $2 item, $3 instance, $4 model (or null), $5 by, $6 reason (or null), $7 escalatedFrom (or null), $8 escalatedFromKind (or null)
pick_payload() {
  jq -cn --arg l "$1" --arg i "$2" --arg a "$3" --arg m "$4" --arg by "$5" --arg r "$6" --arg f "$7" --arg k "$8" '
    def nul: if . == "null" then null else . end;
    {launchId: $l, item: $i, instance: $a, state: "questions", attempt: 1, target: "claude-code", model: ($m | nul), by: $by, reason: ($r | nul),
     decision: (if $by == "jev" then "JD-fixture" else null end), candidates: ["haiku", "sonnet"], briefHash: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
     files: [{path: "src/App.cs", chars: 1200}], omitted: [{path: "big.log", chars: 500000, why: "cap"}], jevMs: 2100,
     jevCalls: (if $by == "jev" then 1 else 0 end), contentSent: ($by == "jev"), unusual: false, escalatedFrom: ($f | nul), escalatedFromKind: ($k | nul)}'
}

# $1 launch, $2 model, $3 input tokens, $4 output tokens
pick_usage() {
  jq -cn --arg l "$1" --arg m "$2" --argjson i "$3" --argjson o "$4" '{launchId: $l, sample: 1, inputTokens: $i, outputTokens: $o, target: "claude-code", model: $m}'
}

model_picks() { # $1 state, $2 item, $3 instance
  need jq
  need_sql
  local state="$1" item="$2" instance="$3" l1 l2
  l1="$(pick_launch 1)"
  l2="$(pick_launch 2)"
  case "$state" in
    picking)
      pick_event "picking:$l1" model.picking "$(jq -cn --arg l "$l1" --arg i "$item" --arg a "$instance" '{launchId: $l, item: $i, instance: $a, state: "questions", attempt: 1, startedAt: "2026-01-01T00:00:00.0000000Z"}')" ;;
    picked)
      pick_event "picked:$l1" model.picked "$(pick_payload "$l1" "$item" "$instance" claude-haiku-4-5 jev null null null)"
      pick_event "usage:$l1" session.usage "$(pick_usage "$l1" claude-haiku-4-5 30000 11000)" ;;
    unpriced)
      pick_event "unpriced:$l1" model.picked "$(pick_payload "$l1" "$item" "$instance" some-new-model rules timeout null null)"
      pick_event "unpriced-usage:$l1" session.usage "$(pick_usage "$l1" some-new-model 12000 3000)" ;;
    escalated)
      pick_event "escalated:$l1" model.picked "$(pick_payload "$l1" "$item" "$instance" claude-haiku-4-5 jev null null null)"
      pick_event "escalated-usage:$l1" session.usage "$(pick_usage "$l1" claude-haiku-4-5 6000 2000)"
      pick_event "escalated-failed:$l1" launch.failed "$(jq -cn --arg l "$l1" --arg i "$item" --arg a "$instance" '{launchId: $l, item: $i, code: "SND-LAUNCH-001", message: "the model refused", instance: $a, state: "questions", attempt: 1}')"
      pick_event "escalated:$l2" model.picked "$(pick_payload "$l2" "$item" "$instance" claude-sonnet-4-5 rules timeout claude-haiku-4-5 refused)"
      pick_event "escalated-usage:$l2" session.usage "$(pick_usage "$l2" claude-sonnet-4-5 24000 7000)" ;;
  esac
  printf '%s\n' "$instance"
}

# --- model-calls: a job's model calls on the Jobs page and the job sheet (context model routing spec 8.2, #1567 R-4) ---

# $1 key, $2 type, $3 payload JSON. A fixed id and idempotency key, so a rerun adds nothing (see pick_event).
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

# --- model-picks-run: the Model picks table's states from a real agent run with the pick on (issue #1452) ---
PICK_RECIPE="workflow.dev-agent-pick"
PICK_CHEAP="claude-haiku-4-5"
PICK_STRONG="claude-sonnet-4-5"
PICK_UNPRICED="fixture-unpriced-model"   # no prefix of Budgets' price table covers it

# workflow.agent gets pick "on" (quoted, so YAML keeps a word) and the models the state needs; $2 is the list, cheapest first.
pick_profile_set() {
  local profile="$1" list="$2" first="${2%%,*}"
  awk -v e="  agent: { target: claude-code, pick: \"on\", models: { claude-code: { list: [$list], default: $first } } }" \
    '/^  agent:/ { print e; next } { print }' "$profile" > "$profile.new" && mv "$profile.new" "$profile"
}

# $1 dir, $2 the model the claude stub refuses (empty: none), $3 state. The stubs: the claude one reports and writes its transcript, the judge one answers m1 or holds.
pick_stubs() {
  local dir="$1" refuse="$2" state="$3"
  mkdir -p "$dir/stub-bin-pick" "$dir/stub-judge" || return
  {
    printf '#!/bin/sh\nSINDRI_BIN=%q\nREFUSE=%q\nCHEAP=%q\n' "${SINDRI:-sindri}" "$refuse" "$PICK_CHEAP"
    cat <<'EOF'
# The fixture's claude-code: finds its model and session id in its argv, writes a transcript the usage reader counts, and reports the
# recipe's payload fact. On the model it is told to refuse it sends a report that is not JSON twice; the second one ends the session.
model="" sid="" seed="" prev=""
for a in "$@"; do
  case "$prev" in --model) model=$a ;; --session-id) sid=$a ;; esac
  case "$a" in *"workflow complete"*) seed=$a ;; esac
  prev=$a
done
if [ -z "$sid" ]; then
  sid=$(cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen)
  mkdir -p "$AW_SINDRI_HOME/launches" && printf '%s\n' "$sid" > "$AW_SINDRI_HOME/launches/$AW_SINDRI_LAUNCH.session-id"
fi
if [ -n "$REFUSE" ] && [ "$model" = "$REFUSE" ]; then in=6000; out=2000
elif [ "$model" = "$CHEAP" ]; then in=30000; out=11000
else in=24000; out=7000; fi
projects="${CLAUDE_CONFIG_DIR:-$HOME/.claude}/projects/fixture"
mkdir -p "$projects" && printf '{"type":"assistant","timestamp":"%s","message":{"id":"msg_%s","model":"%s","usage":{"input_tokens":%s,"output_tokens":%s,"cache_read_input_tokens":0,"cache_creation_input_tokens":0}}}\n' \
  "$(date -u +%Y-%m-%dT%H:%M:%S.000Z)" "${AW_SINDRI_LAUNCH:-launch}" "$model" "$in" "$out" > "$projects/$sid.jsonl"
run=$(printf '%s' "$seed" | sed -n 's/.*workflow complete [^ ]* --run \([^ ]*\) .*/\1/p')
folder="$AW_SINDRI_RUN_DIR/.sindri-out"
mkdir -p "$folder"
# Every call's output goes to stub.log in the run folder, so a proof can show what serve answered.
report() { echo "--- workflow complete $1" >> "$AW_SINDRI_RUN_DIR/stub.log"; $SINDRI_BIN workflow complete "$AW_SINDRI_INSTANCE" --run "$run" --outcome done --output "fact:resolution=$1" >> "$AW_SINDRI_RUN_DIR/stub.log" 2>&1; echo "exit $?" >> "$AW_SINDRI_RUN_DIR/stub.log"; }
if [ -n "$REFUSE" ] && [ "$model" = "$REFUSE" ]; then
  # The bad report lives outside .sindri-out: a file there would let the engine's recovery prove the outcome when the session ends, and the refusal would never count.
  printf 'not json\n' > "$AW_SINDRI_RUN_DIR/bad-report"; report "$AW_SINDRI_RUN_DIR/bad-report"; report "$AW_SINDRI_RUN_DIR/bad-report"
else
  printf '{"summary":"Fixture report"}\n' > "$folder/resolution"; report "$folder/resolution"
fi
exec sleep 86400
EOF
  } > "$dir/stub-bin-pick/claude" && chmod 755 "$dir/stub-bin-pick/claude" || return
  # The judge: Jev picks the first candidate. While judge-hold says hold it does not answer in time (the picking state).
  {
    printf '#!/bin/sh\nHOLD=%q\n' "$dir/stub-judge/judge-hold"
    cat <<'EOF'
cat > /dev/null
[ "$(cat "$HOLD" 2>/dev/null)" != hold ] || exec sleep 60
printf '{"decision":"m1","model":"jev","confidence":0.9,"id":"JD-fixture-pick","reasonCode":"decided"}\n'
EOF
  } > "$dir/stub-judge/judge" && chmod 755 "$dir/stub-judge/judge" || return
  if [ "$state" = picking ]; then echo hold; else echo off; fi > "$dir/stub-judge/judge-hold"
}

# How many lines of the item's ledger name the event type $2.
ledger_count() { "$PROVE" human ledger --item "$1" --max 1000 2>&1 | tr -d '\r' | grep -c -- "$2"; }

model_picks_run() { # $1 state
  need jq
  need git
  sessions_need_copy
  local state="$1" dir profile launcher list refuse="" item wait picks=1 usages=1 want
  dir="$(agent_dir)"
  profile="$(profile_path)"
  launcher="$(sessions_dir)/launcher.json"
  [ -f "$profile" ] || die "no live profile at $profile: run 'up' first"
  case "$state" in
    picked | picking) list="$PICK_CHEAP, $PICK_STRONG" ;;
    escalated) list="$PICK_CHEAP, $PICK_STRONG"; refuse="$PICK_CHEAP"; picks=2; usages=2 ;;
    unpriced) list="$PICK_CHEAP, $PICK_UNPRICED"; refuse="$PICK_CHEAP"; picks=2; usages=2 ;;
  esac
  agent_files "$dir" || return
  agent_profile_add "$profile" "$dir/repo" || die "model-picks-run: can't edit $profile"
  pick_profile_set "$profile" "$list" || die "model-picks-run: can't edit $profile"
  pick_stubs "$dir" "$refuse" "$state" || die "model-picks-run: can't write the stubs in $dir"
  if needs_approval; then approve >&2 || return; fi
  jq --arg bin "$dir/stub-bin-pick/claude" '.agents["claude-code"] = {override: true, bin: $bin}' "$launcher" > "$launcher.new" && mv -f "$launcher.new" "$launcher" \
    || die "model-picks-run: can't update $launcher"
  unset TMUX
  sessions_restart >&2 || return
  approve_dev_recipe "$PICK_RECIPE" || return
  item="$(FIXTURE_REPO="$AGENT_REPO" start_on_new_item "$PICK_RECIPE" "Fixture model picks ($state)")" || return
  wait="${FIXTURE_AGENT_WAIT:-120}"
  [[ "$wait" =~ ^[0-9]+$ ]] || die "model-picks-run: FIXTURE_AGENT_WAIT '$wait' is not a number of seconds"
  local deadline=$((SECONDS + wait))
  while [ "$SECONDS" -lt "$deadline" ]; do
    if [ "$state" = picking ]; then
      [ "$(ledger_count "$item" 'model.picking')" -ge 1 ] && { echo "sindri-ui-fixture: the judge call holds for 40 s; open the instance now" >&2; printf '%s\n' "$item"; return 0; }
    elif [ "$(ledger_count "$item" 'model.picked')" -ge "$picks" ] && [ "$(ledger_count "$item" 'session.usage')" -ge "$usages" ]; then
      printf '%s\n' "$item"
      return 0
    fi
    sleep 1
  done
  want="model.picked"; [ "$state" != picking ] || want="model.picking"
  echo "sindri-ui-fixture: model-picks-run: no $want for $item in $wait s; the ledger says:" >&2
  "$PROVE" human ledger --item "$item" --max 1000 2>&1 | tr -d '\r' | tail -15 >&2
  return 1
}

# --- sandbox: the sandbox surfaces' states (sandbox spec section 8; issue #1113 SB-6) ---

sandbox_dir() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-sandbox"; }

# $1 key, $2 type, $3 payload JSON. A fixed id and idempotency key, so a rerun adds nothing (see pick_event).
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
      sandbox_bwrap /nonexistent || return
      FIXTURE_AGENT_WANT=launch.failed agent_run; return ;;
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
  workflows) [ "$#" -eq 1 ] || { [ "$#" -eq 2 ] && [ "$2" = "--large" ]; } || die "usage: workflows [--large]" ;;
  line-run) [ "$#" -eq 1 ] || die "'line-run' takes no arguments" ;;
  projects) [ "$#" -eq 1 ] || die "'projects' takes no arguments" ;;
  park-timeout | changed-recipe) [ "$#" -eq 1 ] || die "'$verb' takes no arguments" ;;
  incident) { [ "$#" -eq 2 ] && { [ "$2" = start ] || [ "$2" = stop ]; }; } || die "usage: incident start|stop" ;;
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
  agent-run) [ "$#" -eq 1 ] || die "'agent-run' takes no arguments" ;;
  model-calls)
    [ "$#" -eq 2 ] || die "usage: model-calls fell-back|no-answer|paused|none"
    case "$2" in fell-back | no-answer | paused | none) ;; *) die "model-calls: '$2' is not a state (fell-back, no-answer, paused, none)" ;; esac ;;
  model-picks-run)
    [ "$#" -eq 2 ] || die "usage: model-picks-run picked|escalated|unpriced|picking"
    case "$2" in picked | escalated | unpriced | picking) ;; *) die "model-picks-run: '$2' is not a state (picked, escalated, unpriced, picking)" ;; esac ;;
  model-picks)
    [ "$#" -eq 4 ] || die "usage: model-picks picked|escalated|unpriced|picking <ITM-id> <instance-address>"
    case "$2" in picked | escalated | unpriced | picking) ;; *) die "model-picks: '$2' is not a state (picked, escalated, unpriced, picking)" ;; esac
    case "$3" in ITM-*[!0-9a-f]* | ITM-) die "model-picks: '$3' is not an item id" ;; ITM-*) ;; *) die "model-picks: '$3' is not an item id" ;; esac
    [ -n "$4" ] || die "model-picks: the instance address is empty" ;;
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
