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
#   sindri-ui-fixture.sh incident start|stop
#                                  start an incident-urgency instance (recipes/dev/incident-hold.yaml; it tops the strip) or cancel it
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
#   sindri-ui-fixture.sh context whole over|under
#                                  the whole-map fixture (whole-map spec 8; run after 'context up'): over imports a generated map of 12
#                                  features with 50 code nodes each (612 nodes, over the 500-node budget: the Context page opens by feature),
#                                  under imports the tabs map again (10 nodes: the page draws every node). Each switch is a new map version
#                                  and a repeat with the same map imports nothing
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
# FIXTURE_REPO names the items' repo (default: none, so the API's default applies).
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
    *dev-agent-hold)
      # ItemFact is positional and AwJson requires every constructor parameter, so the item names its spec too (#1134).
      jq -nc --arg id "$2" --arg repo "$AGENT_REPO" --arg spec "Fixture spec for $3" '{id: $id, repo: $repo, spec: $spec}' > "$dir/$2.item.json" || return
      printf '%s\n%s\n' --fact "fact:item=$dir/$2.item.json" ;;
    *desk-round-trip)
      printf 'Fixture request for %s\n' "$3" > "$dir/$2.request.txt" || return
      printf '%s\n%s\n' --fact "fact:request=$dir/$2.request.txt" ;;
  esac
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
    for n in 1 2 3 4; do
      for r in workflow.dev-triage-three-state workflow.dev-desk-round-trip workflow.dev-notify; do
        start_on_new_item "$r" "Large fixture workflow $r $n" >/dev/null || return
      done
    done
    return 0
  fi
  start_on_new_item workflow.dev-triage-three-state "Fixture triage" >/dev/null || return
  start_on_new_item workflow.dev-desk-round-trip "Fixture work one" >/dev/null || return
  start_on_new_item workflow.dev-desk-round-trip "Fixture work two" >/dev/null
}

incident_file() { printf '%s\n' "$AW_SINDRI_HOME/ui-fixture-incident-item"; }

incident() {
  local item file
  file="$(incident_file)"
  case "$1" in
    start)
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
  case "$1" in up) context_up ;; draft) context_draft ;; down) context_down ;; publish) context_publish "$2" ;; whole) context_whole "$2" ;; esac
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
  local dir profile launcher recipe item out
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
  recipe="$ROOT/recipes/dev/$AGENT_RECIPE.yaml"
  out="$(printf 'approve %s %s\n' "$AGENT_RECIPE" "$(sed -n 's/^version: *//p' "$recipe" | head -1)" | "$PROVE" human recipe approve "$recipe" 2>&1)" \
    || die "agent-run: the recipe was not approved: $out"
  item="$(FIXTURE_REPO="$AGENT_REPO" start_on_new_item "$AGENT_RECIPE" "Fixture agent run")" || return
  local wait="${FIXTURE_AGENT_WAIT:-120}"
  [[ "$wait" =~ ^[0-9]+$ ]] || die "agent-run: FIXTURE_AGENT_WAIT '$wait' is not a number of seconds"
  local deadline=$((SECONDS + wait))
  while [ "$SECONDS" -lt "$deadline" ]; do
    if "$PROVE" human ledger --item "$item" --max 1000 2>&1 | tr -d '\r' | grep -q 'launch.started'; then
      printf '%s\n' "$item"
      return 0
    fi
    sleep 1
  done
  echo "sindri-ui-fixture: agent-run: no launch.started for $item in $wait s; the ledger says:" >&2
  "$PROVE" human ledger --item "$item" --max 1000 2>&1 | tr -d '\r' | tail -15 >&2
  return 1
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
  incident) { [ "$#" -eq 2 ] && { [ "$2" = start ] || [ "$2" = stop ]; }; } || die "usage: incident start|stop" ;;
  request) [ "$#" -ge 2 ] || die "usage: request [--item ID] [--repo R] <text>" ;;
  context)
    case "${2:-}" in
      up | draft | down) [ "$#" -eq 2 ] || die "usage: context up|draft|down|publish <state>" ;;
      publish)
        [ "$#" -eq 3 ] || die "usage: context publish held|overdue|published|without-waiting|stale|by-hand|pause|resume"
        case "$3" in held | overdue | published | without-waiting | stale | by-hand | pause | resume) ;; *) die "context publish: '$3' is not a state (held, overdue, published, without-waiting, stale, by-hand, pause, resume)" ;; esac ;;
      whole) { [ "$#" -eq 3 ] && { [ "$3" = over ] || [ "$3" = under ]; }; } || die "usage: context whole over|under" ;;
      *) die "usage: context up|draft|down|publish <state>|whole over|under" ;;
    esac ;;
  sessions)
    case "${2:-}" in
      up | disable | enable | down) [ "$#" -eq 2 ] || die "usage: sessions up|ask <pid>|end <pid>|back <pid>|disable|enable|down" ;;
      ask | end | back) [ "$#" -eq 3 ] || die "usage: sessions $2 <pid>" ;;
      *) die "usage: sessions up|ask <pid>|end <pid>|back <pid>|disable|enable|down" ;;
    esac ;;
  agent-run) [ "$#" -eq 1 ] || die "'agent-run' takes no arguments" ;;
  decide) [ "$#" -eq 3 ] || die "usage: decide <DEC-id> <choice>" ;;
  *) usage >&2; die "unknown verb: $verb" ;;
esac

guard
shift
"${verb//-/_}" "$@"
