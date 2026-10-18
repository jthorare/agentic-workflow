#!/usr/bin/env bash
# Tests for scripts/sindri-ui-fixture.sh. Run: bash scripts/tests/sindri-ui-fixture.test.sh
# No dotnet and no real sindri: the guard is sindri-prove.sh's own, stall and unstall run against a temp ledger.db made
# from the ledger's first migration, and up and down run a stub SINDRI that records its arguments.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { # $1 name, $2 haystack, $3 needle
  case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export TMPDIR="$WORK/tmp" AW_STATE_DIR="$WORK/state"
mkdir -p "$TMPDIR" "$AW_STATE_DIR" "$WORK/elsewhere" "$WORK/bin"
H="$TMPDIR/home"
mkdir -p "$H"

# --- usage ---
out="$(bash "$FIXTURE" 2>&1)"; check "no verb exits 2" "$?" "2"
contains "no verb prints usage" "$out" "sindri-ui-fixture.sh stall"
bash "$FIXTURE" --help >/dev/null 2>&1; check "--help exits 0" "$?" "0"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" sideways 2>&1)"; check "an unknown verb exits 2" "$?" "2"
contains "an unknown verb is named" "$out" "unknown verb: sideways"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" --large 2>&1)"; check "--large alone is a usage error (exit 2)" "$?" "2"
contains "--large points at items" "$out" "items --large"
AW_SINDRI_HOME="$H" bash "$FIXTURE" items --big >/dev/null 2>&1; check "items takes only --large (exit 2)" "$?" "2"
AW_SINDRI_HOME="$H" bash "$FIXTURE" close >/dev/null 2>&1; check "close needs an item (exit 2)" "$?" "2"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" stall extra 2>&1)"; check "extra arguments exit 2" "$?" "2"

# --- the guard (sindri-prove.sh's): nothing is written outside a temp or proof home ---
out="$(env -u AW_SINDRI_HOME bash "$FIXTURE" up 2>&1)"; check "an unset home is refused" "$?" "2"
contains "the refusal is the prover's" "$out" "sindri-prove:"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" up >/dev/null 2>&1; check "a home outside TMPDIR is refused" "$?" "2"
check "the refused up wrote no profile" "$(ls -A "$WORK/elsewhere")" ""
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" drift >/dev/null 2>&1; check "drift outside TMPDIR is refused" "$?" "2"

# --- stall / unstall against a ledger made from the first migration ---
# sqlite3 CLI when there is one, else python3's module (this box has no CLI); the fixture has the same fallback.
command -v python3 >/dev/null || command -v sqlite3 >/dev/null || { echo "not ok - sqlite3 or python3 is needed"; exit 1; }
sqlq() { # $1 db, $2 sql: statements in order, rows printed '|'-joined
  if command -v sqlite3 >/dev/null 2>&1; then sqlite3 "$1" "$2"; else
    python3 -I -c '
import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
for row in con.execute(sys.argv[2]):
    print("|".join("" if v is None else str(v) for v in row))' "$1" "$2"
  fi
}
migrate() { # $1 db, $2 migration file
  if command -v sqlite3 >/dev/null 2>&1; then sqlite3 "$1" < "$2"; else
    python3 -I -c '
import sqlite3, sys
con = sqlite3.connect(sys.argv[1], isolation_level=None)
con.executescript(open(sys.argv[2]).read())' "$1" "$2"
  fi
}
migrate "$H/ledger.db" "$ROOT/src/Sindri/Aw.Sindri.Infrastructure/Ledger/Migrations/0001_events.sql"
sqlq "$H/ledger.db" "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
  VALUES ('e1', 'process.started', 1, '2026-10-12T12:00:00Z', 'host:sindri', 'local', 0, 'c1', 3, 'process.started:3', '{}');"

out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" stall 2>&1)"; check "stall exits 0" "$?" "0"
check "stall prints the row it inserted" "$out" "2 service.ran"
row="$(sqlq "$H/ledger.db" "SELECT type, epoch, json_valid(payload_json), actor FROM events WHERE seq = 2")"
check "the row is a registered type at the ledger's epoch with an unparseable payload" "$row" "service.ran|3|0|prover:fixture"
# K7(b): the ledger's reader takes GUIDs for id and correlation_id and the 7-digit round-trip timestamp, or it rejects the row.
shape="$(sqlq "$H/ledger.db" "SELECT id, correlation_id, occurred_at FROM events WHERE seq = 2")"
IFS='|' read -r row_id row_correlation row_at <<< "$shape"
guid='^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
[[ "$row_id" =~ $guid ]]; check "the stall row's id is a GUID" "$?" "0"
[[ "$row_correlation" =~ $guid ]]; check "the stall row's correlation_id is a GUID" "$?" "0"
[[ "$row_at" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{7}Z$ ]]; check "the stall row's occurred_at has seven fractional digits" "$?" "0"
AW_SINDRI_HOME="$H" bash "$FIXTURE" stall >/dev/null 2>&1
check "a second stall adds nothing" "$(sqlq "$H/ledger.db" "SELECT COUNT(*) FROM events")" "2"
out="$(AW_SINDRI_HOME="$H" STALL_TYPE="x'; DROP TABLE events; --" bash "$FIXTURE" stall 2>&1)"; check "a STALL_TYPE that isn't a type is refused" "$?" "2"
out="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" unstall 2>&1)"; check "unstall exits 0" "$?" "0"
check "unstall says what it removed" "$out" "1 row(s) removed"
check "unstall leaves the other rows" "$(sqlq "$H/ledger.db" "SELECT GROUP_CONCAT(type) FROM events")" "process.started"

# The python fallback (a box without the sqlite3 CLI) does the same.
out="$(AW_SINDRI_HOME="$H" SINDRI_UI_FIXTURE_SQL=python bash "$FIXTURE" stall 2>&1)"; check "stall through python3 exits 0" "$?" "0"
contains "stall through python3 prints the row it inserted" "$out" " service.ran"
check "the python3 row has the same shape" "$(sqlq "$H/ledger.db" "SELECT id, correlation_id, length(occurred_at) FROM events WHERE idempotency_key = 'sindri-ui-fixture:stall'")" "7f3c1a52-0b6e-4d1e-9a55-3c2f6d8e1b04|7f3c1a52-0b6e-4d1e-9a55-3c2f6d8e1b05|28"
out="$(AW_SINDRI_HOME="$H" SINDRI_UI_FIXTURE_SQL=python bash "$FIXTURE" unstall 2>&1)"; check "unstall through python3 says what it removed" "$out" "1 row(s) removed"

# A python3 without the sqlite3 module is a missing tool, not a traceback in the middle of a verb.
mkdir -p "$WORK/nosql"
printf '#!/bin/sh\ncase "$*" in *"import sqlite3"*) exit 1 ;; esac\nexit 0\n' > "$WORK/nosql/python3"
chmod +x "$WORK/nosql/python3"
out="$(PATH="$WORK/nosql:$PATH" AW_SINDRI_HOME="$H" SINDRI_UI_FIXTURE_SQL=python bash "$FIXTURE" stall 2>&1)"; check "python3 without sqlite3 exits 2" "$?" "2"
contains "it says which tool is missing" "$out" "sqlite3 (or python3) is needed for 'stall'"
out="$(AW_SINDRI_HOME="$TMPDIR/empty" bash "$FIXTURE" stall 2>&1)"; check "stall with no ledger exits 2" "$?" "2"
contains "stall with no ledger says to run up" "$out" "run 'up' first"

# --- up, drift and down with a stub SINDRI ---
STUB="$WORK/bin/sindri-stub"
cat > "$STUB" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$STUB_LOG"
case "$*" in
  "profile validate --json")
    ok=false; [ "$(cat "$STUB_LOG.approved" 2>/dev/null)" = "$(cksum < "$AW_SINDRI_HOME/profile/profile.yaml")" ] && ok=true
    echo "{\"valid\":true,\"hash\":\"abcdef0123456789\",\"approved\":$ok}" ;;
  "profile approve abcdef0123456789")
    IFS= read -r line; echo "typed=$line" >> "$STUB_LOG"; echo "approval text from the CLI"
    echo "approve-env prover=${AW_SINDRI_PROVER:-} tty=$([ -t 0 ] && echo 1 || echo 0)" >> "$STUB_LOG.env"
    [ -n "${STUB_FAIL_APPROVE:-}" ] && { echo "approve refused" >&2; exit 1; }
    cksum < "$AW_SINDRI_HOME/profile/profile.yaml" > "$STUB_LOG.approved" ;;
  "context map status --json") [ -z "${STUB_NO_MAP:-}" ] || exit 1; echo '{"version":{"number":1}}' ;;
  "context map build") echo "build-env prover=${AW_SINDRI_PROVER:-} tty=$([ -t 0 ] && echo 1 || echo 0)" >> "$STUB_LOG.env" ;;
  "context map import "*) IFS= read -r line; echo "typed=$line" >> "$STUB_LOG"; echo "import-env prover=${AW_SINDRI_PROVER:-} tty=$([ -t 0 ] && echo 1 || echo 0)" >> "$STUB_LOG.env"; [ -z "${STUB_FAIL_IMPORT:-}" ] || { echo "SND-AUTH-002 refused" >&2; exit 1; } ;;
  "item add "*) n=$(($(cat "$STUB_LOG.n" 2>/dev/null || echo 0) + 1)); echo "$n" > "$STUB_LOG.n"; echo "added ITM-0$n" ;;
  "workflow start "*)
    for a in "$@"; do case "$a" in fact:*=*) echo "fact ${a%%=*} $(cat "${a#*=}")" >> "$STUB_LOG" ;; esac; done
    if [ -n "${STUB_FAIL_START:-}" ]; then echo "SND-CLI-003 refused" >&2; exit 2; fi ;;
esac
exit 0
EOF
chmod +x "$STUB"
export STUB_LOG="$WORK/calls"
U="$TMPDIR/up"
mkdir -p "$U"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" up >/dev/null 2>&1; check "up exits 0" "$?" "0"
contains "up writes a profile with a heartbeat" "$(cat "$U/profile/profile.yaml")" "heartbeat: { every: 5s }"
check "up validates, approves by typing 'approve profile' and the hash's first six characters (K7a), then starts headless" "$(tr '\n' ';' < "$STUB_LOG")" \
  "profile validate --json;profile approve abcdef0123456789;typed=approve profile abcdef;--headless;"
AW_SINDRI_HOME="$U" bash "$FIXTURE" drift >/dev/null 2>&1; check "drift exits 0" "$?" "0"
contains "drift changes the cadence" "$(cat "$U/profile/profile.yaml")" "every: 7s"
AW_SINDRI_HOME="$U" bash "$FIXTURE" drift >/dev/null 2>&1
contains "drift again changes it back" "$(cat "$U/profile/profile.yaml")" "every: 5s"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" down >/dev/null 2>&1; check "down exits 0" "$?" "0"
check "down stops it" "$(cat "$STUB_LOG")" "stop"

# --- items and close (U2): human item add / item close through sindri-prove.sh ---
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" items >/dev/null 2>&1; check "items exits 0" "$?" "0"
check "items adds the fixture's four items, oldest first" "$(tr '\n' ';' < "$STUB_LOG")" \
  "item add Stream limits 429 fallback;item add Retry storm in outbox dispatcher;item add Context map review for tabs.layout;item add Try the queue;"
: > "$STUB_LOG"
printf '{"url":"http://127.0.0.1:7999"}\n' > "$U/control.json"
mkdir -p "$WORK/nocurl"; printf '#!/bin/sh\nexit 0\n' > "$WORK/nocurl/curl"; chmod +x "$WORK/nocurl/curl"
PATH="$WORK/nocurl:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" FIXTURE_REPO=demo bash "$FIXTURE" items --large >/dev/null 2>&1; check "items --large exits 0" "$?" "0"
check "items --large adds 34 items" "$(grep -c '^item add' "$STUB_LOG")" "34"
contains "FIXTURE_REPO names the repo" "$(head -1 "$STUB_LOG")" "--repo demo"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" close ITM-0192a1b2 --cancelled >/dev/null 2>&1; check "close exits 0" "$?" "0"
check "close sends item close --cancelled" "$(cat "$STUB_LOG")" "item close ITM-0192a1b2 --cancelled"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" close 'ITM-1;rm' >/dev/null 2>&1; check "close refuses a bad id (exit 2)" "$?" "2"

# --- U3': request, decide, fail / unfail, items --large's requests ---
# A fake curl records its arguments; control.json gives the prover's `url` a base.
cat > "$WORK/bin/curl" <<'EOF'
#!/usr/bin/env bash
echo "curl $*" >> "$STUB_LOG"
exit 0
EOF
chmod +x "$WORK/bin/curl"
printf '{"url":"http://127.0.0.1:7999"}\n' > "$U/control.json"
absent() { case "$2" in *"$3"*) check "$1" "present" "absent" ;; *) check "$1" "absent" "absent" ;; esac; }
: > "$STUB_LOG"
PATH="$WORK/bin:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" request --item ITM-01 --repo demo "Split it" >/dev/null 2>&1; check "request exits 0" "$?" "0"
call="$(cat "$STUB_LOG")"
contains "request_sends_a_session_curl: a POST to /sindri/requests" "$call" "http://127.0.0.1:7999/sindri/requests"
contains "request names the repo scope" "$call" "X-Sindri-Repo: demo"
contains "request sends the item as the API's member, item" "$call" '"item":"ITM-01"'
absent "request does not send itemId" "$call" "itemId"
contains "request sends the text" "$call" '"text":"Split it"'
absent "request_sends_no_caller_header" "$call" "X-Sindri-Caller"
absent "request sends no token" "$call" "X-Sindri-Token"
: > "$STUB_LOG"
PATH="$WORK/bin:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" request "Plain" >/dev/null 2>&1
absent "request without flags sends no repo" "$(cat "$STUB_LOG")" "X-Sindri-Repo"
absent "request without flags sends no item" "$(cat "$STUB_LOG")" '"item"'
AW_SINDRI_HOME="$U" bash "$FIXTURE" request >/dev/null 2>&1; check "request needs text (exit 2)" "$?" "2"
AW_SINDRI_HOME="$U" bash "$FIXTURE" request --item 'x;y' hi >/dev/null 2>&1; check "request refuses a bad item id (exit 2)" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" request hi >/dev/null 2>&1; check "every_verb_refuses_an_unguarded_home: request" "$?" "2"

: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" decide DEC-0192a1 accept >/dev/null 2>&1; check "decide exits 0" "$?" "0"
check "decide_runs_sindri_decide_as_the_owner" "$(cat "$STUB_LOG")" "decide DEC-0192a1 accept"
AW_SINDRI_HOME="$U" bash "$FIXTURE" decide 'DEC-1;rm' accept >/dev/null 2>&1; check "decide refuses a bad id (exit 2)" "$?" "2"
AW_SINDRI_HOME="$U" bash "$FIXTURE" decide DEC-1 >/dev/null 2>&1; check "decide needs a choice (exit 2)" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" decide DEC-1 accept >/dev/null 2>&1; check "every_verb_refuses_an_unguarded_home: decide" "$?" "2"

: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" fail >/dev/null 2>&1; check "fail exits 0" "$?" "0"
contains "fail_adds_a_service_the_build_cannot_run" "$(cat "$U/profile/profile.yaml")" "broken: { every: 5s }"
contains "fail approves the changed profile" "$(tr '\n' ';' < "$STUB_LOG")" "profile approve abcdef0123456789;typed=approve profile abcdef;"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" unfail >/dev/null 2>&1; check "unfail exits 0" "$?" "0"
absent "unfail_removes_it" "$(cat "$U/profile/profile.yaml")" "broken"
contains "unfail approves the profile again" "$(tr '\n' ';' < "$STUB_LOG")" "typed=approve profile abcdef;"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" fail >/dev/null 2>&1; check "every_verb_refuses_an_unguarded_home: fail" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" unfail >/dev/null 2>&1; check "every_verb_refuses_an_unguarded_home: unfail" "$?" "2"

: > "$STUB_LOG"
PATH="$WORK/bin:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" items --large >/dev/null 2>&1; check "items --large exits 0" "$?" "0"
check "items_large_adds_30_items_and_12_requests: 34 items" "$(grep -c '^item add' "$STUB_LOG")" "34"
check "items --large files 12 requests" "$(grep -c '^curl .*sindri/requests' "$STUB_LOG")" "12"
# F1: the fixture profile has no repos, so a request names none unless FIXTURE_REPO names one the profile has.
absent "items --large without FIXTURE_REPO names no repo (the API refuses one the profile lacks)" "$(cat "$STUB_LOG")" "X-Sindri-Repo"
: > "$STUB_LOG"
PATH="$WORK/bin:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" FIXTURE_REPO=agentic-fixture bash "$FIXTURE" items --large >/dev/null 2>&1
check "items --large with FIXTURE_REPO files its 12 requests in that repo only" "$(grep '^curl .*sindri/requests' "$STUB_LOG" | grep -c 'X-Sindri-Repo: agentic-fixture -d')" "12"
absent "no request names a repo of its own invention" "$(cat "$STUB_LOG")" "demo"

# A refused request (review run 1, test-4): a curl that behaves as curl does answers a 4xx problem body with exit 22 only when told
# to fail on HTTP errors (--fail or --fail-with-body), else prints it and exits 0. The fixture must ask, or a refusal looks like success.
mkdir -p "$WORK/refuse"
cat > "$WORK/refuse/curl" <<'EOF'
#!/usr/bin/env bash
echo "curl $*" >> "$STUB_LOG"
case "$*" in
  *--fail-with-body*|*--fail\ *) echo '{"code":"SND-API-409","detail":"repo demo is not in the approved profile"}'; exit 22 ;;
  *) echo '{"code":"SND-API-409","detail":"repo demo is not in the approved profile"}'; exit 0 ;;
esac
EOF
chmod +x "$WORK/refuse/curl"
: > "$STUB_LOG"
out="$(PATH="$WORK/refuse:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" request --repo demo "Refused" 2>&1)"; rc=$?
check "a refused request exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
contains "a refused request shows the problem detail" "$out" "repo demo is not in the approved profile"
: > "$STUB_LOG"
out="$(PATH="$WORK/refuse:$PATH" AW_SINDRI_HOME="$U" SINDRI="$STUB" FIXTURE_REPO=demo bash "$FIXTURE" items --large 2>&1)"; rc=$?
check "items --large with every request refused exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
contains "items --large shows the problem detail" "$out" "repo demo is not in the approved profile"
check "items --large stops at the first refused request" "$(grep -c '^curl .*sindri/requests' "$STUB_LOG")" "1"

# --- U3' T20: the Workflows strip's instances (sindri workflow start / cancel as the owner) ---
: > "$STUB_LOG"; echo 0 > "$STUB_LOG.n"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" workflows >/dev/null 2>&1; check "workflows exits 0" "$?" "0"
starts="$(grep '^workflow start' "$STUB_LOG")"
check "workflows_starts_one_triage_and_two_work_instances_through_the_cli: three starts" "$(printf '%s\n' "$starts" | grep -c .)" "3"
check "one triage" "$(printf '%s\n' "$starts" | grep -c 'workflow start workflow.dev-triage-three-state --item ITM-')" "1"
check "two work" "$(printf '%s\n' "$starts" | grep -c 'workflow start workflow.dev-desk-round-trip --item ITM-')" "2"
check "each start has an item of its own" "$(printf '%s\n' "$starts" | grep -o 'ITM-[A-Za-z0-9]*' | sort -u | wc -l | tr -d ' ')" "3"
# F3: the recipes' required inputs go with the start: fact:item as JSON naming the item, fact:request as text.
check "the triage start passes fact:item" "$(printf '%s\n' "$starts" | grep 'dev-triage-three-state' | grep -c -- '--fact fact:item=')" "1"
check "each work start passes fact:request" "$(printf '%s\n' "$starts" | grep 'dev-desk-round-trip' | grep -c -- '--fact fact:request=')" "2"
facts="$(grep '^fact ' "$STUB_LOG")"
check "fact:item is JSON with the item's id" "$(printf '%s\n' "$facts" | grep '^fact fact:item ' | sed 's/^fact fact:item //' | jq -r '.id')" "ITM-01"
check "the triage start passes fact:scope" "$(printf '%s\n' "$starts" | grep 'dev-triage-three-state' | grep -c -- '--fact fact:scope=')" "1"
check "fact:scope is text" "$(printf '%s\n' "$facts" | grep -c '^fact fact:scope Fixture')" "1"
check "fact:request is text" "$(printf '%s\n' "$facts" | grep -c '^fact fact:request Fixture')" "2"
out="$(AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" workflows 2>&1)"
contains "workflows prints what it started" "$out" "started workflow.dev-triage-three-state on ITM-"
check "workflows prints a line for each start" "$(printf '%s\n' "$out" | grep -c '^started ')" "3"
out="$(STUB_FAIL_START=1 AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" workflows 2>&1)"; rc=$?
check "a refused start exits non-zero" "$([ "$rc" -ne 0 ] && echo yes || echo no)" "yes"
contains "a refused start says which one and why" "$out" "workflow.dev-triage-three-state"
contains "a refused start carries the CLI's refusal" "$out" "SND-CLI-003 refused"
: > "$STUB_LOG"; echo 0 > "$STUB_LOG.n"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" workflows --large >/dev/null 2>&1; check "workflows --large exits 0" "$?" "0"
starts="$(grep '^workflow start' "$STUB_LOG")"
check "workflows_large_starts_twelve_roots_across_three_recipes: twelve starts" "$(printf '%s\n' "$starts" | grep -c .)" "12"
check "three recipes" "$(printf '%s\n' "$starts" | awk '{print $3}' | sort -u | wc -l | tr -d ' ')" "3"
: > "$STUB_LOG"; echo 0 > "$STUB_LOG.n"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" incident start >/dev/null 2>&1; check "incident start exits 0" "$?" "0"
contains "incident_start_starts_an_incident_urgency_instance: the incident recipe" "$(grep '^workflow start' "$STUB_LOG")" "workflow start workflow.dev-incident-hold --item ITM-01"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" incident stop >/dev/null 2>&1; check "incident stop exits 0" "$?" "0"
check "incident_stop_cancels_it" "$(cat "$STUB_LOG")" "workflow cancel ITM-01"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" incident stop >/dev/null 2>&1; check "incident stop with none started exits 2" "$?" "2"
AW_SINDRI_HOME="$U" bash "$FIXTURE" incident sideways >/dev/null 2>&1; check "incident takes start or stop (exit 2)" "$?" "2"
AW_SINDRI_HOME="$U" bash "$FIXTURE" workflows --big >/dev/null 2>&1; check "workflows takes only --large (exit 2)" "$?" "2"
for v in "workflows" "workflows --large" "incident start" "incident stop"; do
  # shellcheck disable=SC2086
  AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" $v >/dev/null 2>&1; check "every_workflow_verb_refuses_an_unguarded_home: $v" "$?" "2"
done

# --- T-F: context up | draft | down (the Context page's data, U9) ---
C="$TMPDIR/ctx"
mkdir -p "$C"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" up >/dev/null 2>&1
: > "$STUB_LOG"
out="$(AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" context up 2>&1)"; check "context up refuses outside a guarded home" "$?" "2"
contains "the refusal is the prover's" "$out" "sindri-prove:"
check "the refused context up wrote nothing" "$(ls -A "$WORK/elsewhere")" ""
for v in draft down; do AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" context $v >/dev/null 2>&1; check "context $v refuses outside a guarded home" "$?" "2"; done
AW_SINDRI_HOME="$C" bash "$FIXTURE" context >/dev/null 2>&1; check "context needs up, draft or down (exit 2)" "$?" "2"
AW_SINDRI_HOME="$C" bash "$FIXTURE" context sideways >/dev/null 2>&1; check "context refuses another word (exit 2)" "$?" "2"

: > "$STUB_LOG"
rm -f "$STUB_LOG.env"   # per case: the assertions below read only this context up
out="$(AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up 2>/dev/null)"; check "context up exits 0" "$?" "0"
check "context up prints the repo name" "$out" "aw-split-panes"
R="$C/ui-fixture-context/aw-split-panes"
for f in src/tabs/TabManager.ts src/tabs/TabGroups.ts src/tabs/Pinned.ts src/layout/Grid.ts README.md .env; do
  check "context up writes $f" "$([ -f "$R/$f" ] && echo yes || echo no)" "yes"
done
check "the repo is a git repo on main with one commit" "$(git -C "$R" rev-list --count main 2>/dev/null)" "1"
check "the repo is clean" "$(git -C "$R" status --porcelain)" ""
check "the repo's sha is the same on every run (fixed author and date)" "$(git -C "$R" log -1 --format='%an|%aI')" "Fixture Author|2026-07-01T09:00:00+00:00"
contains "the repo is one of the profile's repos" "$(cat "$C/profile/profile.yaml")" "name: aw-split-panes, path: $R"
contains "the profile keeps its other keys" "$(cat "$C/profile/profile.yaml")" "heartbeat: { every: 5s }"
calls="$(tr '\n' ';' < "$STUB_LOG")"
contains "the changed profile is approved by typing the hash's first six characters" "$calls" "profile approve abcdef0123456789;typed=approve profile abcdef;"
want="import map $(sha256sum "$ROOT/tests/Aw.Context.Testing/Fixtures/map-tabs.json" | cut -c1-6)"
contains "the tabs map is imported by typing 'import map' and the file's hash6, under the human pty" "$calls" "context map import $C/ui-fixture-context/tabs-map.json;typed=$want;"
check "the imported file is the tabs map" "$(cmp "$C/ui-fixture-context/tabs-map.json" "$ROOT/tests/Aw.Context.Testing/Fixtures/map-tabs.json" && echo same)" "same"
envs="$(cat "$STUB_LOG.env")"
contains "the approve runs as the owner under a pty (human mode, not a session)" "$envs" "approve-env prover=1 tty=1"
contains "the import runs as the owner under a pty (human mode, not a session)" "$envs" "import-env prover=1 tty=1"
check "context up's stdout is exactly the repo name even when approve prints text" "$out" "aw-split-panes"
: > "$STUB_LOG"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1; check "a second context up exits 0" "$?" "0"
check "a second context up adds the repo to the profile once" "$(grep -c 'name: aw-split-panes' "$C/profile/profile.yaml")" "1"
check "a second context up keeps one commit" "$(git -C "$R" rev-list --count main)" "1"
absent "a second context up does not approve again" "$(cat "$STUB_LOG")" "profile approve"
absent "a second context up does not import the map again (the current map is this file's)" "$(cat "$STUB_LOG")" "context map import"
rm -f "$C/ui-fixture-context-imported"
out="$(STUB_FAIL_IMPORT=1 AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up 2>&1)"; check "a refused import exits non-zero" "$([ "$?" -ne 0 ] && echo yes || echo no)" "yes"
contains "a refused import carries the CLI's refusal" "$out" "SND-AUTH-002 refused"

: > "$STUB_LOG"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context draft >/dev/null 2>&1; check "context draft exits 0" "$?" "0"
check "context draft builds the draft: map build as the owner" "$(cat "$STUB_LOG")" "context map build"
contains "context draft runs as the owner under a pty" "$(cat "$STUB_LOG.env")" "build-env prover=1 tty=1"

: > "$STUB_LOG"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context down >/dev/null 2>&1; check "context down exits 0" "$?" "0"
check "context down removes the repo and the map copy" "$([ -e "$C/ui-fixture-context" ] && echo present || echo gone)" "gone"
absent "context down removes the profile entry" "$(cat "$C/profile/profile.yaml")" "aw-split-panes"
absent "context down leaves no empty repos: key" "$(cat "$C/profile/profile.yaml")" "repos:"
contains "context down keeps the rest of the profile" "$(cat "$C/profile/profile.yaml")" "heartbeat: { every: 5s }"
contains "context down approves the changed profile" "$(cat "$STUB_LOG")" "typed=approve profile abcdef"
before="$(cat "$C/profile/profile.yaml")"; : > "$STUB_LOG"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context down >/dev/null 2>&1; check "context down twice exits 0 (idempotent)" "$?" "0"
check "the second context down changes nothing" "$(cat "$C/profile/profile.yaml")" "$before"
absent "the second context down approves nothing" "$(cat "$STUB_LOG")" "profile approve"
# --- review run 1: re-import, approval retry, AW_SINDRI_PROFILE ---
: > "$STUB_LOG"
# The marker names this very file, so only the status check can send it back to the import.
sha256sum "$ROOT/tests/Aw.Context.Testing/Fixtures/map-tabs.json" | cut -d' ' -f1 > "$C/ui-fixture-context-imported"
STUB_NO_MAP=1 AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1; check "context up imports again when the store has no map" "$?" "0"
contains "the import ran although the marker is current" "$(cat "$STUB_LOG")" "context map import"
rm -f "$C/ui-fixture-context-imported"
: > "$STUB_LOG"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1
contains "a changed map file is imported again" "$(cat "$STUB_LOG")" "context map import"

# A failed approve is retried by the next up, which approves because the profile says it isn't approved.
D="$TMPDIR/ctx2"; mkdir -p "$D"
AW_SINDRI_HOME="$D" SINDRI="$STUB" bash "$FIXTURE" up >/dev/null 2>&1
rm -f "$STUB_LOG.approved"; : > "$STUB_LOG"
STUB_FAIL_APPROVE=1 AW_SINDRI_HOME="$D" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1
check "a refused approve fails context up" "$([ "$?" -ne 0 ] && echo yes || echo no)" "yes"
contains "the profile already lists the repo" "$(cat "$D/profile/profile.yaml")" "name: aw-split-panes"
absent "no import ran against the unapproved profile" "$(cat "$STUB_LOG")" "context map import"
: > "$STUB_LOG"
AW_SINDRI_HOME="$D" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1; check "the next context up exits 0" "$?" "0"
contains "the next context up approves although the entry is already there" "$(cat "$STUB_LOG")" "profile approve"
contains "and then imports" "$(cat "$STUB_LOG")" "context map import"
# down: a failed approve is warned about and the next down tries again
STUB_FAIL_APPROVE=1 AW_SINDRI_HOME="$D" SINDRI="$STUB" bash "$FIXTURE" context down >/dev/null 2>&1
: > "$STUB_LOG"
AW_SINDRI_HOME="$D" SINDRI="$STUB" bash "$FIXTURE" context down >/dev/null 2>&1; check "the next context down exits 0" "$?" "0"
contains "the next context down approves the profile it left unapproved" "$(cat "$STUB_LOG")" "profile approve"

# AW_SINDRI_PROFILE outside the home: the real file is untouched, the home's profile is used
G="$TMPDIR/ctx3"; mkdir -p "$G" "$WORK/realprofile"
AW_SINDRI_HOME="$G" SINDRI="$STUB" bash "$FIXTURE" up >/dev/null 2>&1
printf 'schemaVersion: 1\nuser: owner\n' > "$WORK/realprofile/profile.yaml"; cp "$WORK/realprofile/profile.yaml" "$WORK/realprofile/orig"
AW_SINDRI_PROFILE="$WORK/realprofile/profile.yaml" AW_SINDRI_HOME="$G" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1
AW_SINDRI_PROFILE="$WORK/realprofile/profile.yaml" AW_SINDRI_HOME="$G" SINDRI="$STUB" bash "$FIXTURE" context down >/dev/null 2>&1
AW_SINDRI_PROFILE="$WORK/realprofile/profile.yaml" AW_SINDRI_HOME="$G" SINDRI="$STUB" bash "$FIXTURE" drift >/dev/null 2>&1
check "an AW_SINDRI_PROFILE outside the home is never edited" "$(cmp "$WORK/realprofile/profile.yaml" "$WORK/realprofile/orig" && echo same)" "same"
contains "the fixture edited the home's own profile instead" "$(cat "$G/profile/profile.yaml")" "every: 7s"

# --help keeps the FIXTURE_REPO and Environment lines
contains "--help shows FIXTURE_REPO" "$(bash "$FIXTURE" --help)" "FIXTURE_REPO names"
contains "--help shows the environment" "$(bash "$FIXTURE" --help)" "Environment: AW_SINDRI_HOME"

AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1; check "context up works again after down" "$?" "0"

exit "$fail"
