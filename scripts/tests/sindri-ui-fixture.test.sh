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
# `timeout` is GNU coreutils: macOS has none (gtimeout only with Homebrew coreutils), so the stub checks used to exit 127 there.
# Same contract: runs "$@" with this shell's stdin, exits 124 when it is still running after $1 seconds.
with_timeout() {
  local secs=$1; shift
  if command -v timeout >/dev/null 2>&1; then timeout "$secs" "$@"; return; fi
  if command -v gtimeout >/dev/null 2>&1; then gtimeout "$secs" "$@"; return; fi
  # why: the watcher marks that it fired before it kills; asking whether it is still alive raced with its exit and returned the command's 143 (#1888)
  local fired; fired="$(mktemp -u)"
  exec 3<&0
  "$@" <&3 &
  local pid=$!
  ( sleep "$secs"; : > "$fired"; kill -TERM "$pid" 2>/dev/null ) &
  local watcher=$!
  wait "$pid"; local rc=$?
  exec 3<&-
  kill "$watcher" 2>/dev/null; wait "$watcher" 2>/dev/null
  if [ -e "$fired" ]; then rm -f "$fired"; return 124; fi
  return "$rc"
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
    echo "{\"valid\":true,\"hash\":\"abcdef0123456789\"}" ;;
  "--headless") echo "headless proc=${AW_SESSIONS_PROC_ROOT:-} claude=${AW_SESSIONS_CLAUDE_DIR:-} fleet=${AW_FLEET_DIR:-} launcher=${AW_LAUNCHER_CONFIG:-}" >> "$STUB_LOG.env"; echo "judge=${AW_SINDRI_JUDGE:-}" >> "$STUB_LOG.judge" ;;
  "context map status --json") [ -z "${STUB_NO_MAP:-}" ] || exit 1; echo '{"version":{"number":1}}' ;;
  "context map build") echo "build-env prover=${AW_SINDRI_PROVER:-} tty=$([ -t 0 ] && echo 1 || echo 0)" >> "$STUB_LOG.env" ;;
  "context map import "*) IFS= read -r line; echo "typed=$line" >> "$STUB_LOG"; echo "import-env prover=${AW_SINDRI_PROVER:-} tty=$([ -t 0 ] && echo 1 || echo 0)" >> "$STUB_LOG.env"; [ -z "${STUB_FAIL_IMPORT:-}" ] || { echo "SND-AUTH-002 refused" >&2; exit 1; } ;;
  "item add "*) n=$(($(cat "$STUB_LOG.n" 2>/dev/null || echo 0) + 1)); echo "$n" > "$STUB_LOG.n"; echo "added ITM-0$n" ;;
  "todo add "*)
    cid=""; prev=""; for a in "$@"; do [ "$prev" = "--client-id" ] && cid="$a"; prev="$a"; done
    grep -qx "td_$cid" "$STUB_LOG.todos" 2>/dev/null || echo "td_$cid" >> "$STUB_LOG.todos"
    echo "{\"accepted\":{},\"id\":\"td_$cid\",\"version\":1,\"due\":null}" ;;
  "todos --json --no-fence")
    printf '{"todos":['; sep=""; while IFS= read -r t; do grep -qx "$t" "$STUB_LOG.todos.done" 2>/dev/null || { printf '%s{"id":"%s"}' "$sep" "$t"; sep=","; }; done < "$STUB_LOG.todos"; echo ']}' ;;
  "todo done "*) echo "$3" >> "$STUB_LOG.todos.done" ;;
  "ledger "*) sleep "${STUB_LEDGER_SLEEP:-0}" ;;
esac
exit 0
EOF
chmod +x "$STUB"
export STUB_LOG="$WORK/calls"
U="$TMPDIR/up"
mkdir -p "$U"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" up >/dev/null 2>&1; check "up exits 0" "$?" "0"
contains "up writes a profile with a heartbeat" "$(cat "$U/profile/profile.yaml")" "heartbeat: { every: 5s }"
check "up validates the profile (no approval: edits apply on save), then starts headless" "$(tr '\n' ';' < "$STUB_LOG")" \
  "profile validate --json;--headless;"
# #2863: without `sessions up`, the control plane reads an empty tree in the temp home, never the box's /proc,
# ~/.claude/sessions or $AW_STATE_DIR/launcher.json (all four collector overrides set, each under the home).
E="$U/ui-fixture-no-sessions"
check "up starts the control plane with the four overrides on an empty tree" "$(tail -1 "$STUB_LOG.env")" \
  "headless proc=$E/proc claude=$E/claude-sessions fleet=$E/fleet launcher=$E/launcher.json"
check "the empty tree has no Claude record" "$(ls -A "$E/claude-sessions" 2>&1)" ""
# A table with no process of ours reads as SND-SESSION-002 proc-unreadable, so the tree holds one plain shell and no agent.
check "the empty tree's only process is one shell" "$(ls -A "$E/proc" 2>&1 | tr '\n' ';')" "2;"
check "the shell is bash" "$(cut -d' ' -f2 "$E/proc/2/stat" 2>&1)" "(bash)"
check "the shell is this uid's" "$(awk '/^Uid:/ {print $2}' "$E/proc/2/status" 2>&1)" "$(id -u)"
check "the shell's stat has the fields the collector reads" "$([ "$(wc -w < "$E/proc/2/stat" 2>/dev/null || echo 0)" -ge 22 ] && echo yes || echo no)" "yes"
check "the empty launcher.json names only the schema" "$(cat "$E/launcher.json" 2>&1)" '{"schema":1}'
printf '{"url":"http://127.0.0.1:7999"}\n' > "$U/control.json"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" reviews down >/dev/null 2>&1
check "a restart without sessions up keeps the four overrides on the empty tree" "$(tail -1 "$STUB_LOG.env")" \
  "headless proc=$E/proc claude=$E/claude-sessions fleet=$E/fleet launcher=$E/launcher.json"
rm -f "$U/control.json"
: > "$STUB_LOG"
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
check "fail validates the changed profile and approves nothing" "$(tr '\n' ';' < "$STUB_LOG")" "profile validate --json;"
: > "$STUB_LOG"
AW_SINDRI_HOME="$U" SINDRI="$STUB" bash "$FIXTURE" unfail >/dev/null 2>&1; check "unfail exits 0" "$?" "0"
absent "unfail_removes_it" "$(cat "$U/profile/profile.yaml")" "broken"
check "unfail validates the profile and approves nothing" "$(tr '\n' ';' < "$STUB_LOG")" "profile validate --json;"
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
check "the repo's sha is the same on every run (fixed author and date)" "$(git -C "$R" log -1 --format='%an|%at')" "Fixture Author|1782896400"
contains "the repo is one of the profile's repos" "$(cat "$C/profile/profile.yaml")" "name: aw-split-panes, path: $R"
contains "the profile keeps its other keys" "$(cat "$C/profile/profile.yaml")" "heartbeat: { every: 5s }"
calls="$(tr '\n' ';' < "$STUB_LOG")"
absent "the changed profile is not approved (edits apply on save)" "$calls" "profile approve"
want="import map $(sha256sum "$ROOT/tests/Aw.Context.Testing/Fixtures/map-tabs.json" | cut -c1-6)"
contains "the tabs map is imported by typing 'import map' and the file's hash6, under the human pty" "$calls" "context map import $C/ui-fixture-context/tabs-map.json;typed=$want;"
check "the imported file is the tabs map" "$(cmp "$C/ui-fixture-context/tabs-map.json" "$ROOT/tests/Aw.Context.Testing/Fixtures/map-tabs.json" && echo same)" "same"
envs="$(cat "$STUB_LOG.env")"
contains "the import runs as the owner under a pty (human mode, not a session)" "$envs" "import-env prover=1 tty=1"
check "context up's stdout is exactly the repo name" "$out" "aw-split-panes"
: > "$STUB_LOG"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1; check "a second context up exits 0" "$?" "0"
check "a second context up adds the repo to the profile once" "$(grep -c 'name: aw-split-panes' "$C/profile/profile.yaml")" "1"
check "a second context up keeps one commit" "$(git -C "$R" rev-list --count main)" "1"
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
absent "context down approves nothing (edits apply on save)" "$(cat "$STUB_LOG")" "profile approve"
before="$(cat "$C/profile/profile.yaml")"; : > "$STUB_LOG"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context down >/dev/null 2>&1; check "context down twice exits 0 (idempotent)" "$?" "0"
check "the second context down changes nothing" "$(cat "$C/profile/profile.yaml")" "$before"
# --- review run 1: re-import, AW_SINDRI_PROFILE ---
: > "$STUB_LOG"
# The marker names this very file, so only the status check can send it back to the import.
sha256sum "$ROOT/tests/Aw.Context.Testing/Fixtures/map-tabs.json" | cut -d' ' -f1 > "$C/ui-fixture-context-imported"
STUB_NO_MAP=1 AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1; check "context up imports again when the store has no map" "$?" "0"
contains "the import ran although the marker is current" "$(cat "$STUB_LOG")" "context map import"
rm -f "$C/ui-fixture-context-imported"
: > "$STUB_LOG"
AW_SINDRI_HOME="$C" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1
contains "a changed map file is imported again" "$(cat "$STUB_LOG")" "context map import"

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

# --- sessions up | ask | end | back | disable | enable | down (the Agents page's data, U11a) ---
S="$TMPDIR/sess"
mkdir -p "$S"
AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" up >/dev/null 2>&1
for v in "up" "ask 211" "end 211" "back 211" "disable" "enable" "down"; do
  # shellcheck disable=SC2086
  AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" sessions $v >/dev/null 2>&1; check "sessions $v refuses an unguarded home" "$?" "2"
done
check "the refused sessions verbs wrote nothing" "$(ls -A "$WORK/elsewhere")" ""
AW_SINDRI_HOME="$S" bash "$FIXTURE" sessions >/dev/null 2>&1; check "sessions needs a subverb (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sessions sideways >/dev/null 2>&1; check "sessions refuses another word (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sessions ask >/dev/null 2>&1; check "sessions ask needs a pid (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sessions up 1 >/dev/null 2>&1; check "sessions up takes no argument (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions end 211 >/dev/null 2>&1; check "sessions end before up exits 2" "$?" "2"
AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions ask x >/dev/null 2>&1; check "sessions ask refuses a non-pid (exit 2)" "$?" "2"

own="$(jq -r '.ownUid' "$ROOT/tests/fixtures/sessions/manifest.json")"
if [ "$(id -u)" != "$own" ]; then
  out="$(AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions up 2>&1)"; check "sessions up refuses another uid" "$?" "2"
  contains "the refusal names the fixture's uid" "$out" "belong to uid $own"
  check "the refused sessions up copied nothing" "$([ -e "$S/ui-fixture-sessions" ] && echo yes || echo no)" "no"
  echo "ok - skipped: the other sessions cases need uid $own (this is $(id -u))"
else
  # shellcheck source=scripts/lib/ensure-scripts-deps.sh
  . "$ROOT/scripts/lib/ensure-scripts-deps.sh"
  ensure_scripts_deps "$ROOT/scripts" || { echo "not ok - Ajv is needed"; fail=1; }
  : > "$STUB_LOG"; : > "$STUB_LOG.env"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions up >/dev/null 2>&1; check "sessions up exits 0" "$?" "0"
  D="$S/ui-fixture-sessions"
  check "the copy is the fixture's tree" "$(diff -r --no-dereference "$ROOT/tests/fixtures/sessions/proc" "$D/proc" && diff -r "$ROOT/tests/fixtures/sessions/claude-sessions" "$D/claude-sessions" && echo same)" "same"
  check "the copy's proc keeps a symlink" "$([ -L "$D/proc/211/exe" ] && echo link)" "link"
  check "the fleet dir is 0700 with the marker" "$(stat -c %a "$D/fleet" 2>/dev/null || stat -f %Lp "$D/fleet"):$([ -f "$D/fleet/.aw-fleet" ] && echo marker)" "700:marker"
  check "the copy's launcher.json is bare" "$(jq -c . "$D/launcher.json")" '{"schema":1}'
  check "sessions up stops, then starts headless" "$(tr '\n' ';' < "$STUB_LOG")" "stop;--headless;"
  check "the control plane starts with the four overrides on the copy" "$(tail -1 "$STUB_LOG.env")" "headless proc=$D/proc claude=$D/claude-sessions fleet=$D/fleet launcher=$D/launcher.json"
  before="$(cat "$STUB_LOG")"
  # ask: a schema-valid asking file for pid 211's session, ticks from its stat
  f="$(AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions ask 211 2>&1)"; check "sessions ask exits 0" "$?" "0"
  check "ask prints the file under fleet/claude-code/<session id>.json" "$f" "$D/fleet/claude-code/aaaaaaaa-0000-4000-8000-000000000211.json"
  check "ask: state, reason, tool, pid, ticks, provider" "$(jq -r '[.state, .reason, .tool, .agentPid, .agentStartTicks, .provider] | join(",")' "$f")" "asking,approval,Bash,211,1100,claude-code"
  check "ask: mode 0600, dir 0700, marker" "$(stat -c %a "$f" 2>/dev/null || stat -f %Lp "$f"):$(stat -c %a "$D/fleet/claude-code" 2>/dev/null || stat -f %Lp "$D/fleet/claude-code"):$([ -f "$D/fleet/claude-code/.aw-fleet" ] && echo marker)" "600:700:marker"
  check "ask leaves no temp file" "$(ls -A "$D/fleet/claude-code" | grep -c '^\.ask\.')" "0"
  check "ask does not restart the control plane" "$(cat "$STUB_LOG")" "$before"
  node -e '
const fs = require("fs"), { createRequire } = require("module");
const Ajv = createRequire(process.argv[1] + "/package.json")("ajv/dist/2020").default;
const v = new Ajv({ strict: false }).compile(JSON.parse(fs.readFileSync(process.argv[2], "utf8")));
process.exit(v(JSON.parse(fs.readFileSync(process.argv[3], "utf8"))) ? 0 : 1);' "$ROOT/scripts" "$ROOT/schemas/fleet-state.schema.json" "$f" 2>/dev/null
  check "the fleet file validates against fleet-state.schema.json" "$?" "0"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions ask 212 >/dev/null 2>&1; check "ask refuses a pid with no Claude record (exit 2)" "$?" "2"
  # end / back
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions end 311 >/dev/null 2>&1; check "sessions end exits 0" "$?" "0"
  check "end moves proc/311 out of the tree" "$([ -e "$D/proc/311" ] && echo in || echo out):$([ -d "$D/ended/311" ] && echo kept)" "out:kept"
  check "end leaves the source fixture alone" "$([ -d "$ROOT/tests/fixtures/sessions/proc/311" ] && echo present)" "present"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions end 311 >/dev/null 2>&1; check "ending an ended pid exits 2" "$?" "2"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions back 311 >/dev/null 2>&1; check "sessions back exits 0" "$?" "0"
  check "back restores proc/311" "$(diff -r --no-dereference "$ROOT/tests/fixtures/sessions/proc/311" "$D/proc/311" && echo same)" "same"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions back 311 >/dev/null 2>&1; check "back of a pid not ended exits 2" "$?" "2"
  # disable / enable
  : > "$STUB_LOG.env"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions disable >/dev/null 2>&1; check "sessions disable exits 0" "$?" "0"
  check "disable writes sessions.enabled=false into the copy's launcher.json" "$(jq -c . "$D/launcher.json")" '{"schema":1,"sessions":{"enabled":false}}'
  check "disable restarts with the overrides" "$(cat "$STUB_LOG.env")" "headless proc=$D/proc claude=$D/claude-sessions fleet=$D/fleet launcher=$D/launcher.json"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions enable >/dev/null 2>&1; check "sessions enable exits 0" "$?" "0"
  check "enable removes the key again" "$(jq -c . "$D/launcher.json")" '{"schema":1}'
  # down
  : > "$STUB_LOG"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions down >/dev/null 2>&1; check "sessions down exits 0" "$?" "0"
  check "down removes the copy" "$([ -e "$D" ] && echo present || echo gone)" "gone"
  check "down stops the control plane and never restarts it" "$(cat "$STUB_LOG")" "stop"
  AW_SINDRI_HOME="$S" SINDRI="$STUB" bash "$FIXTURE" sessions down >/dev/null 2>&1; check "sessions down twice exits 0" "$?" "0"
  check "the fixture tree in the repo is untouched" "$(git -C "$ROOT" status --porcelain -- tests/fixtures/sessions)" ""
fi

# --- context publish: the Context map card's six states (F1P-c, code index spec 6.4) ---
P="$TMPDIR/pub"
mkdir -p "$P/context"
migrate "$P/ledger.db" "$ROOT/src/Sindri/Aw.Sindri.Infrastructure/Ledger/Migrations/0001_events.sql"
migrate "$P/context/context.db" "$ROOT/src/Context/Aw.Context.Infrastructure/Store/Migrations/0002_s3a_graph.sql"
migrate "$P/context/context.db" "$ROOT/src/Context/Aw.Context.Infrastructure/Store/Migrations/0010_s4p_map_publish.sql"
CDB="$P/context/context.db"
sqlq "$P/ledger.db" "INSERT INTO events (id, type, schema_version, occurred_at, actor, tenant, ring, correlation_id, epoch, idempotency_key, payload_json)
  VALUES ('e1', 'process.started', 1, '2026-10-12T12:00:00Z', 'host:sindri', 'local', 0, 'c1', 3, 'process.started:3', '{}');"
pub() { AW_SINDRI_HOME="$P" SINDRI="$STUB" bash "$FIXTURE" context publish "$@"; }

out="$(AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" context publish held 2>&1)"; check "context publish refuses outside a guarded home" "$?" "2"
contains "the refusal is the prover's" "$out" "sindri-prove:"
AW_SINDRI_HOME="$P" bash "$FIXTURE" context publish >/dev/null 2>&1; check "context publish needs a state (exit 2)" "$?" "2"
out="$(AW_SINDRI_HOME="$P" bash "$FIXTURE" context publish sideways 2>&1)"; check "context publish refuses another state (exit 2)" "$?" "2"
contains "the refusal names the states" "$out" "held, overdue, published, without-waiting, stale, by-hand"
out="$(pub held 2>&1)"; check "context publish with no store exits 2" "$?" "2"
contains "it says to run context up" "$out" "run 'context up' first"

sqlq "$CDB" "INSERT INTO map_version (repo, commit_sha, key_version, created, status) VALUES ('aw-split-panes', NULL, 1, '2026-10-01T00:00:00.0000000Z', 'current');"
sqlq "$CDB" "INSERT INTO map_version_head (version, repo, commit_sha, on_default) VALUES (1, 'aw-split-panes', 'abcdef0', 1);"
: > "$STUB_LOG"
out="$(pub held 2>&1)"; check "publish held exits 0" "$?" "0"
check "held prints its state id" "$out" "held"
check "held writes a draft headed at the repo's live commit (a stand-in outside a git repo) and a hold on it" "$(sqlq "$CDB" "SELECT d.id, d.status, h.repo, h.commit_sha FROM map_hold m JOIN map_version d ON d.id = m.version JOIN map_version_head h ON h.version = d.id")" "2|draft|aw-split-panes|9999999999999999999999999999999999999999"
check "held writes the code index at another commit, so the repo waits" "$(ls "$P/code-index" | wc -l | tr -d ' ')" "2"
check "held starts the wait now" "$(sqlq "$CDB" "SELECT since >= strftime('%Y-%m-%dT%H:%M:%S', 'now', '-30 seconds') FROM map_publish_chain")" "1"
check "held pauses the map service so no tick publishes it" "$(cat "$STUB_LOG")" "services pause context-map-refresh"
check "held leaves the current version" "$(sqlq "$CDB" "SELECT id, status FROM map_version WHERE status = 'current'")" "1|current"
pub held >/dev/null 2>&1
check "a second held writes one draft and one hold" "$(sqlq "$CDB" "SELECT COUNT(*) FROM map_hold") $(sqlq "$CDB" "SELECT COUNT(*) FROM map_version WHERE status = 'draft'") $(sqlq "$CDB" "SELECT COUNT(*) FROM map_version_head")" "1 1 2"

out="$(pub overdue 2>&1)"; check "publish overdue exits 0" "$?" "0"
check "overdue prints its state id" "$out" "overdue"
check "overdue starts the wait an hour ago" "$(sqlq "$CDB" "SELECT since <= strftime('%Y-%m-%dT%H:%M:%S', 'now', '-3500 seconds') FROM map_publish_chain")" "1"

: > "$STUB_LOG"
out="$(pub published 2>&1)"; check "publish published exits 0" "$?" "0"
check "published prints its state id" "$out" "published"
check "published drops the hold, the chain and the draft with its heads" "$(sqlq "$CDB" "SELECT (SELECT COUNT(*) FROM map_hold), (SELECT COUNT(*) FROM map_publish_chain), (SELECT COUNT(*) FROM map_version WHERE status = 'draft'), (SELECT COUNT(*) FROM map_version_head)")" "0|0|0|1"
check "published resumes the map service" "$(cat "$STUB_LOG")" "services resume context-map-refresh"

out="$(pub without-waiting 2>&1)"; check "publish without-waiting exits 0" "$?" "0"
check "without-waiting prints its state id" "$out" "published-without-waiting"
check "without-waiting records a FirstMap published event for the current version" "$(sqlq "$P/ledger.db" "SELECT type, actor, json_extract(payload_json, '$.version'), json_extract(payload_json, '$.reason') FROM events WHERE idempotency_key LIKE 'sindri-ui-fixture:publish:firstmap:%'")" "context.map.published|service:context-map-refresh|1|FirstMap"
shape="$(sqlq "$P/ledger.db" "SELECT id, correlation_id, occurred_at FROM events WHERE idempotency_key LIKE 'sindri-ui-fixture:publish:firstmap:%'")"
IFS='|' read -r row_id row_correlation row_at <<< "$shape"
[[ "$row_id" =~ $guid ]]; check "the published event's id is a GUID" "$?" "0"
[[ "$row_correlation" =~ $guid ]]; check "the published event's correlation_id is a GUID" "$?" "0"
[[ "$row_at" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{7}Z$ ]]; check "the published event's occurred_at has seven fractional digits" "$?" "0"
pub without-waiting >/dev/null 2>&1
check "a second without-waiting adds nothing" "$(sqlq "$P/ledger.db" "SELECT COUNT(*) FROM events WHERE idempotency_key LIKE 'sindri-ui-fixture:publish:firstmap:%'")" "1"

out="$(pub by-hand 2>&1)"; check "publish by-hand exits 0" "$?" "0"
check "by-hand prints its state id" "$out" "published-by-hand"
check "by-hand records a promoted event by a human for the current version" "$(sqlq "$P/ledger.db" "SELECT type, actor, json_extract(payload_json, '$.version') FROM events WHERE idempotency_key LIKE 'sindri-ui-fixture:publish:byhand:%'")" "context.map.promoted|human:fixture|1"

out="$(pub stale 2>&1)"; check "publish stale exits 0" "$?" "0"
check "stale prints its state id" "$out" "published-stale"
check "stale moves the current version's recorded head off the index's commit" "$(sqlq "$CDB" "SELECT commit_sha FROM map_version_head WHERE version = 1")" "1234567"
check "stale records a TimedOut published event" "$(sqlq "$P/ledger.db" "SELECT json_extract(payload_json, '$.reason') FROM events WHERE idempotency_key LIKE 'sindri-ui-fixture:publish:timedout:%'")" "TimedOut"
IXDB="$P/code-index/aw-split-panes.0123456789ABCDEFGHJKMNPQRS.db"
check "stale writes the code index file beside the pointer" "$(ls "$P/code-index" | sort | tr '\n' ' ')" "aw-split-panes.0123456789ABCDEFGHJKMNPQRS.db aw-split-panes.current "
check "the pointer names the file and is private" "$(cat "$P/code-index/aw-split-panes.current") $(stat -c %a "$P/code-index/aw-split-panes.current" 2>/dev/null || stat -f %Lp "$P/code-index/aw-split-panes.current")" "0123456789ABCDEFGHJKMNPQRS 600"
check "the index header reads back at abcdef0 under schema 1" "$(sqlq "$IXDB" "SELECT value FROM meta WHERE key = 'commit'") $(sqlq "$IXDB" "PRAGMA user_version")" "abcdef0000000000000000000000000000000000 1"
pub by-hand >/dev/null 2>&1
pub published >/dev/null 2>&1
check "published after by-hand leaves no map event, so the newest state is published" "$(sqlq "$P/ledger.db" "SELECT COUNT(*) FROM events WHERE idempotency_key LIKE 'sindri-ui-fixture:publish:%'")" "0"
pub stale >/dev/null 2>&1
check "stale after published records its TimedOut event again" "$(sqlq "$P/ledger.db" "SELECT COUNT(*) FROM events WHERE idempotency_key LIKE 'sindri-ui-fixture:publish:timedout:%'")" "1"
pub published >/dev/null 2>&1
check "any other state removes the index file and its pointer" "$(ls "$P/code-index" | wc -l | tr -d ' ')" "0"
pub stale >/dev/null 2>&1
printf '%s\n' "OTHERULID" > "$P/code-index/other.current"
pub by-hand >/dev/null 2>&1
check "clearing leaves a pointer that is not the fixture's" "$(ls "$P/code-index")" "other.current"

: > "$STUB_LOG"
pub pause >/dev/null 2>&1; pub resume >/dev/null 2>&1
check "pause and resume only toggle the service" "$(tr '\n' ';' < "$STUB_LOG")" "services pause context-map-refresh;services resume context-map-refresh;"

# --- context whole over | under: the whole-map fixture (whole-map spec 8) ---
W="$TMPDIR/ctxw"; mkdir -p "$W"
AW_SINDRI_HOME="$W" SINDRI="$STUB" bash "$FIXTURE" up >/dev/null 2>&1
AW_SINDRI_HOME="$W" bash "$FIXTURE" context whole over >/dev/null 2>&1; check "context whole refuses before context up (exit 2)" "$?" "2"
AW_SINDRI_HOME="$W" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1
AW_SINDRI_HOME="$W" bash "$FIXTURE" context whole sideways >/dev/null 2>&1; check "context whole refuses another word (exit 2)" "$?" "2"
AW_SINDRI_HOME="$W" bash "$FIXTURE" context whole >/dev/null 2>&1; check "context whole needs over or under (exit 2)" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" context whole over >/dev/null 2>&1; check "context whole refuses outside a guarded home" "$?" "2"
: > "$STUB_LOG"
out="$(AW_SINDRI_HOME="$W" SINDRI="$STUB" bash "$FIXTURE" context whole over 2>/dev/null)"; check "context whole over exits 0" "$?" "0"
check "context whole over prints its word" "$out" "over"
contains "context whole over imports the map as the owner" "$(cat "$STUB_LOG")" "context map import"
M="$W/ui-fixture-context/over-map.json"
check "the over map has 612 nodes (over the 500-node budget)" "$(jq '.nodes | length' "$M")" "612"
check "the over map has 12 features" "$(jq '[.nodes[] | select(.key | startswith("feature:"))] | length' "$M")" "12"
check "every edge of the over map joins two of its nodes" "$(jq '[.nodes[].key] as $k | [.edges[] | select((.from as $f | $k | index($f)) == null or (.to as $t | $k | index($t)) == null)] | length' "$M")" "0"
check "the over map is for the fixture repo" "$(jq -r .repo "$M")" "aw-split-panes"
: > "$STUB_LOG"
AW_SINDRI_HOME="$W" SINDRI="$STUB" bash "$FIXTURE" context whole over >/dev/null 2>&1
absent "a repeat of context whole over imports nothing" "$(cat "$STUB_LOG")" "context map import"
: > "$STUB_LOG"
out="$(AW_SINDRI_HOME="$W" SINDRI="$STUB" bash "$FIXTURE" context whole under 2>/dev/null)"; check "context whole under exits 0 and prints its word" "$out" "under"
contains "context whole under imports the tabs map again" "$(cat "$STUB_LOG")" "context map import"
check "the under map is the tabs map (10 nodes)" "$(jq '.nodes | length' "$W/ui-fixture-context/tabs-map.json")" "10"
: > "$STUB_LOG"
out="$(AW_SINDRI_HOME="$W" SINDRI="$STUB" bash "$FIXTURE" context whole concepts 2>/dev/null)"; check "context whole concepts exits 0 and prints its word" "$out" "concepts"
contains "context whole concepts imports the map as the owner" "$(cat "$STUB_LOG")" "context map import"
M="$W/ui-fixture-context/concepts-map.json"
check "the concepts map has two concepts" "$(jq '[.nodes[] | select(.key | startswith("concept:"))] | length' "$M")" "2"
check "each concept of the concepts map carries its glossary terms" "$(jq -c '[.nodes[] | select(.key | startswith("concept:")) | .attributes.terms]' "$M")" '[["shift","swap","roster","rota"],["invoice"]]'
DKEY="decision:$(printf '%s\n%s' "$(jq -r '.decisions[0].record' "$M")" "$(jq -r '.decisions[0].quote' "$M")" | sha256sum | cut -c1-64)"
check "every edge of the concepts map joins two of its nodes (a decision row implies its node)" "$(jq --arg d "$DKEY" '([.nodes[].key] + [$d]) as $k | [.edges[] | select((.from as $f | $k | index($f)) == null or (.to as $t | $k | index($t)) == null)] | length' "$M")" "0"
check "the concepts map has one decision, on a Self scheduling feature, keyed as context keys it (the Why band, #1281)" "$(jq -r '[.edges[] | select(.type == "decides") | .from + ">" + .to] | join(",")' "$M")" "$DKEY>feature:swaps"
check "the decision belongs to Self scheduling, so the concept opens it (ConceptScope)" "$(jq -r --arg d "$DKEY" '[.edges[] | select(.from == $d and .type == "belongs-to") | .to] | join(",")' "$M")" "concept:scheduling"

# model-calls (#1567 R-4): the usage, the refusals, and the ledger rows each state writes (fixed ids, so a rerun adds nothing).
contains "the usage lists model-calls" "$(bash "$FIXTURE" --help 2>&1)" "sindri-ui-fixture.sh model-calls fell-back|no-answer|paused|none"
AW_SINDRI_HOME="$S" bash "$FIXTURE" model-calls >/dev/null 2>&1; check "model-calls needs a state (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" model-calls nope >/dev/null 2>&1; check "model-calls refuses an unknown state (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" model-calls paused extra >/dev/null 2>&1; check "model-calls takes one argument (exit 2)" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" model-calls paused >/dev/null 2>&1; check "model-calls refuses an unguarded home" "$?" "2"
calls_rows() { sqlq "$H/ledger.db" "SELECT COUNT(*) FROM events WHERE type = '$1' AND json_extract(payload_json, '$.job') = '$2'"; }
job="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" model-calls fell-back 2>/dev/null)"; check "model-calls fell-back exits 0 and prints the job id" "$?" "0"
[[ "$job" =~ ^JOB-[0-9a-f]{32}$ ]]; check "the printed job id is a JOB- id" "$?" "0"
check "fell-back starts the job once" "$(calls_rows job.started "$job")" "1"
check "fell-back writes six calls" "$(calls_rows context.model.called "$job")" "6"
check "fell-back's calls hold a limit, a skipped and a failed try" "$(sqlq "$H/ledger.db" "SELECT COUNT(DISTINCT json_extract(t.value, '$.outcome')) FROM events e, json_each(e.payload_json, '$.tries') t WHERE e.type = 'context.model.called' AND json_extract(e.payload_json, '$.job') = '$job' AND json_extract(t.value, '$.outcome') IN ('limit', 'skipped', 'failed')")" "3"
check "fell-back's calls hold a Jev label pick, a surface pick and a pick not asked" "$(sqlq "$H/ledger.db" "SELECT COUNT(*) FROM events WHERE type = 'context.model.called' AND json_extract(payload_json, '$.job') = '$job' AND json_extract(payload_json, '$.tries[0].harness') = 'jev'")" "3"
check "fell-back never waits" "$(calls_rows job.waiting "$job")" "0"
AW_SINDRI_HOME="$H" bash "$FIXTURE" model-calls fell-back >/dev/null 2>&1
check "a second fell-back adds nothing" "$(calls_rows context.model.called "$job")" "6"
job="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" model-calls no-answer 2>/dev/null)"
check "no-answer writes three calls and none has an answerer" "$(sqlq "$H/ledger.db" "SELECT COUNT(*) FROM events WHERE type = 'context.model.called' AND json_extract(payload_json, '$.job') = '$job' AND json_extract(payload_json, '$.answeredBy') IS NULL")" "3"
job="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" model-calls paused 2>/dev/null)"
check "paused waits on model-limit" "$(sqlq "$H/ledger.db" "SELECT json_extract(payload_json, '$.signal') FROM events WHERE type = 'job.waiting' AND json_extract(payload_json, '$.job') = '$job'")" "model-limit"
check "paused says why in its last progress line" "$(sqlq "$H/ledger.db" "SELECT json_extract(payload_json, '$.line') FROM events WHERE type = 'job.progress' AND json_extract(payload_json, '$.job') = '$job' ORDER BY seq DESC LIMIT 1")" "Paused: claude-code at its usage limit; codex at its usage limit. Resume when one is back."
job="$(AW_SINDRI_HOME="$H" bash "$FIXTURE" model-calls none 2>/dev/null)"
check "none writes a job and no call" "$(calls_rows job.started "$job")$(calls_rows context.model.called "$job")" "10"

# sandbox (SB-6): the usage and the refusals that need no control plane.
contains "the usage lists sandbox" "$(bash "$FIXTURE" --help 2>&1)" "sindri-ui-fixture.sh sandbox blocked|clear"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sandbox >/dev/null 2>&1; check "sandbox needs a state (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sandbox nope >/dev/null 2>&1; check "sandbox refuses an unknown state (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sandbox blocked extra >/dev/null 2>&1; check "sandbox blocked takes no arguments (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sandbox denials >/dev/null 2>&1; check "sandbox denials needs an item and an instance (exit 2)" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sandbox denials not-an-item ITM-0f/step/1 >/dev/null 2>&1; check "sandbox refuses a bad item id (exit 2)" "$?" "2"
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" sandbox denials ITM-0f ITM-0f/step/1 >/dev/null 2>&1; check "sandbox refuses an unguarded home" "$?" "2"
AW_SINDRI_HOME="$S" bash "$FIXTURE" sandbox clear >/dev/null 2>&1; check "sandbox clear before sessions up exits 2" "$?" "2"

# --- projects: the Projects page's data (projects page spec 4.1, 5, 12 Ph5, 13.1) ---
contains "the usage lists projects" "$(bash "$FIXTURE" --help 2>&1)" "sindri-ui-fixture.sh projects"
PJ="$TMPDIR/proj"
mkdir -p "$PJ/profile" "$PJ/context"
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\n' > "$PJ/profile/profile.yaml"
PJDB="$PJ/context/context.db"
proj() { AW_SINDRI_HOME="$PJ" SINDRI="$STUB" bash "$FIXTURE" projects "$@"; }
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" projects >/dev/null 2>&1; check "projects refuses outside a guarded home" "$?" "2"
proj extra >/dev/null 2>&1; check "projects takes no arguments (exit 2)" "$?" "2"
out="$(proj 2>&1)"; check "projects with no context store exits 2" "$?" "2"
contains "projects with no context store says to run context up" "$out" "run 'context up' first"
for m in 0001 0018 0019 0020 0022 0023; do migrate "$PJDB" "$(ls "$ROOT"/src/Context/Aw.Context.Infrastructure/Store/Migrations/${m}_*.sql)"; done
: > "$STUB_LOG"
out="$(proj 2>&1)"; check "projects exits 0" "$?" "0"
check "projects stores six Linear projects" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record WHERE source = 'linear-projects' AND kind = 'project'")" "6"
check "three are led by the viewer: in progress at risk, planned, and completed 3 days ago" "$(sqlq "$PJDB" "SELECT GROUP_CONCAT(t, ',') FROM (SELECT p.title || ':' || p.state_raw || ':' || COALESCE(p.health, '-') AS t FROM source_record p JOIN record_link l ON l.from_key = p.key AND l.kind = 'led-by' WHERE l.to_ref = (SELECT user_ref FROM source_identity WHERE source = 'linear') ORDER BY p.key)")" "Checkout rewrite:started:atRisk,Onboarding revamp:completed:onTrack,Search relevance v2:planned:-"
check "the finished one closed about 3 days ago" "$(sqlq "$PJDB" "SELECT closed_at < strftime('%Y-%m-%dT%H:%M:%S', 'now', '-2 days') AND closed_at > strftime('%Y-%m-%dT%H:%M:%S', 'now', '-4 days') FROM source_record WHERE key = 'linear:project/fx-onboarding'")" "1"
check "the others' projects: one paused, two in progress" "$(sqlq "$PJDB" "SELECT GROUP_CONCAT(state_raw) FROM (SELECT state_raw FROM source_record p JOIN record_link l ON l.from_key = p.key AND l.kind = 'led-by' WHERE l.to_ref <> 'linear:user/fx-viewer' ORDER BY p.key)")" "paused,started,started"
check "every project is in the audience of the profile's team" "$(sqlq "$PJDB" "SELECT DISTINCT audience_key FROM source_record")" "linear:SPL"
check "issues: 15 open and 6 closed, each linked to its project by key" "$(sqlq "$PJDB" "SELECT SUM(i.state = 'open'), SUM(i.state = 'done') FROM source_record i JOIN record_link l ON l.from_key = i.key AND l.kind = 'in-project' AND l.to_key IN (SELECT key FROM source_record WHERE kind = 'project') WHERE i.kind = 'issue'")" "15|6"
check "Checkout rewrite has 5 open issues" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM record_link l JOIN source_record i ON i.key = l.from_key WHERE l.to_key = 'linear:project/fx-checkout' AND l.kind = 'in-project' AND i.state = 'open'")" "5"
check "the viewer's identity row is stored" "$(sqlq "$PJDB" "SELECT user_ref, name FROM source_identity WHERE source = 'linear'")" "linear:user/fx-viewer|Sam Rivera"
check "both Linear sources are past their first read" "$(sqlq "$PJDB" "SELECT GROUP_CONCAT(source || ':' || phase) FROM (SELECT source, phase FROM source_cursor WHERE repo = 'records' ORDER BY source)")" "linear:incremental,linear-projects:incremental"
contains "projects puts the Linear team in the live profile" "$(cat "$PJ/profile/profile.yaml")" "linear: { teams: [SPL] }"
absent "projects runs no profile approve (edits apply on save)" "$(cat "$STUB_LOG")" "profile approve"
contains "projects attaches the paused project as the owner" "$(cat "$STUB_LOG")" "project add --linear linear:project/fx-billing"
contains "projects adds the hand-made project" "$(cat "$STUB_LOG")" "project add --name Hiring loop --state in-progress --notes Two onsite slots left this month --target "
contains "projects hides a project the viewer leads" "$(cat "$STUB_LOG")" "project hide linear:project/fx-search"
check "projects leaves one marker per owner write" "$(ls "$PJ" | grep -c '^ui-fixture-projects-owner-')" "3"
: > "$STUB_LOG"
proj >/dev/null 2>&1; check "a second projects exits 0" "$?" "0"
check "a second projects adds no row" "$(sqlq "$PJDB" "SELECT COUNT(*) FROM source_record") $(sqlq "$PJDB" "SELECT COUNT(*) FROM record_link")" "30 30"
check "a second projects adds the team to the profile once" "$(grep -c 'teams: \[SPL\]' "$PJ/profile/profile.yaml")" "1"
absent "a second projects attaches, adds and hides nothing" "$(cat "$STUB_LOG")" "project "
# A run that stopped half way resumes: with only the attach marker left, only the add and the hide run.
rm -f "$PJ/ui-fixture-projects-owner-add" "$PJ/ui-fixture-projects-owner-hide"
: > "$STUB_LOG"
proj >/dev/null 2>&1; check "a resumed projects exits 0" "$?" "0"
absent "a resumed projects does not attach again" "$(cat "$STUB_LOG")" "project add --linear"
contains "a resumed projects adds the hand-made project" "$(cat "$STUB_LOG")" "project add --name Hiring loop"
contains "a resumed projects hides" "$(cat "$STUB_LOG")" "project hide linear:project/fx-search"
printf 'schemaVersion: 1\nuser: fixture\ncontext:\n  docs: { globs: [x] }\n' > "$PJ/profile/profile.yaml"
rm -f "$PJ"/ui-fixture-projects-owner-*
out="$(proj 2>&1)"; check "projects refuses a profile with its own context: section (exit 2)" "$?" "2"
contains "that refusal names the key to add" "$out" "teams: [SPL]"
# --- linear-catchup: the Work > Linear "Since you last looked" strip (#2536) ---
contains "the usage lists linear-catchup" "$(bash "$FIXTURE" --help 2>&1)" "sindri-ui-fixture.sh linear-catchup"
LC="$TMPDIR/lcatch"
mkdir -p "$LC/profile" "$LC/context"
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\n' > "$LC/profile/profile.yaml"
LCDB="$LC/context/context.db"
lcatch() { AW_SINDRI_HOME="$LC" SINDRI="$STUB" bash "$FIXTURE" linear-catchup "$@"; }
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" linear-catchup >/dev/null 2>&1; check "linear-catchup refuses outside a guarded home" "$?" "2"
lcatch nope >/dev/null 2>&1; check "linear-catchup refuses an unknown state (exit 2)" "$?" "2"
lcatch typical extra >/dev/null 2>&1; check "linear-catchup takes one state at most (exit 2)" "$?" "2"
out="$(lcatch 2>&1)"; check "linear-catchup with no context store exits 2" "$?" "2"
contains "linear-catchup with no context store says to run context up" "$out" "run 'context up' first"
for m in "$ROOT"/src/Context/Aw.Context.Infrastructure/Store/Migrations/00*.sql; do migrate "$LCDB" "$m"; done
LCN="SELECT (SELECT COUNT(*) FROM linear_issue_change) + (SELECT COUNT(*) FROM linear_comment)"
out="$(lcatch 2>&1)"; check "linear-catchup (typical) exits 0" "$?" "0"
check "typical: 25 issues" "$(sqlq "$LCDB" "SELECT COUNT(*) FROM linear_issue")" "25"
check "typical: 37 strip rows (changes and mentions)" "$(sqlq "$LCDB" "$LCN")" "37"
check "typical: 8 or more issues have 2 or more rows" "$(sqlq "$LCDB" "SELECT COUNT(*) >= 8 FROM (SELECT i.key, (SELECT COUNT(*) FROM linear_issue_change c WHERE c.issue_key = i.key) + (SELECT COUNT(*) FROM linear_comment m WHERE m.issue_key = i.key) AS n FROM linear_issue i) WHERE n >= 2")" "1"
check "typical: a third of the titles are over 60 characters" "$(sqlq "$LCDB" "SELECT COUNT(*) * 3 >= (SELECT COUNT(*) FROM source_record WHERE key LIKE 'linear:issue/fxc-%') FROM source_record WHERE key LIKE 'linear:issue/fxc-%' AND length(title) > 60")" "1"
check "typical: three or more projects and four priorities" "$(sqlq "$LCDB" "SELECT COUNT(DISTINCT project_id) >= 3, COUNT(DISTINCT priority) >= 4 FROM linear_issue")" "1|1"
check "typical: every change is within the last 3 days" "$(sqlq "$LCDB" "SELECT MIN(at) > strftime('%Y-%m-%dT%H:%M:%S', 'now', '-3 days') AND MAX(at) < strftime('%Y-%m-%dT%H:%M:%S', 'now') FROM linear_issue_change")" "1"
check "typical: state moves keep their names" "$(sqlq "$LCDB" "SELECT old_name || ' > ' || new_name FROM linear_issue_change WHERE issue_key = 'linear:issue/fxc-1' AND field = 'state' ORDER BY at DESC LIMIT 1")" "In Progress > In Review"
check "typical: assignments are to the viewer, state moves are on the viewer's issues" "$(sqlq "$LCDB" "SELECT (SELECT COUNT(*) FROM linear_issue_change WHERE field = 'assignee' AND \"new\" <> 'fx-viewer') + (SELECT COUNT(*) FROM linear_issue_change c JOIN linear_issue i ON i.key = c.issue_key WHERE c.field = 'state' AND i.assignee_id <> 'fx-viewer')")" "0"
check "typical: every issue is in the profile team's audience" "$(sqlq "$LCDB" "SELECT DISTINCT audience_key FROM source_record WHERE key LIKE 'linear:issue/fxc-%'")" "linear:SPL"
check "typical: the mentions name a viewer who is a stored member" "$(sqlq "$LCDB" "SELECT COUNT(*) FROM linear_comment WHERE body LIKE '@' || (SELECT name FROM linear_member WHERE id = 'fx-viewer') || '%'")" "5"
contains "linear-catchup gives Linear a token pointer and a dead loopback url, so the page reads connected" "$(cat "$LC/profile/profile.yaml")" 'linear: { teams: [SPL], token: "env:FIXTURE_LINEAR_KEY", url: "http://127.0.0.1:9/graphql" }'
check "typical: no marker is stored" "$(test -f "$LC/ui/preferences.json" && jq -r '.linearSince // "none"' "$LC/ui/preferences.json" || echo none)" "none"
lcatch typical >/dev/null 2>&1; check "a second typical exits 0" "$?" "0"
check "a second typical replaces the rows" "$(sqlq "$LCDB" "$LCN") $(sqlq "$LCDB" "SELECT COUNT(*) FROM linear_issue")" "37 25"
lcatch empty >/dev/null 2>&1; check "linear-catchup empty exits 0" "$?" "0"
check "empty: the marker is a lin_ marker newer than every change" "$(jq -r '.linearSince' "$LC/ui/preferences.json" | grep -Ec '^lin_[0-9a-f]{16}$')" "1"
check "empty: the preferences file keeps its version" "$(jq -r .version "$LC/ui/preferences.json")" "1"
check "empty: the marker's instant is within a minute of now" "$(( $(date -u +%s) - ( $(printf '%d' "0x$(jq -r '.linearSince' "$LC/ui/preferences.json" | cut -c5-)") - 621355968000000000 ) / 10000000 < 60 ))" "1"
lcatch error >/dev/null 2>&1; check "linear-catchup error exits 0" "$?" "0"
check "error: the feed's cursor row records the refused key" "$(sqlq "$LCDB" "SELECT last_error || '|' || failures || '|' || last_error_text || '|' || (last_sync > strftime('%Y-%m-%dT%H:%M:%S', 'now', '-10 minutes')) FROM source_cursor WHERE source = 'linear' AND repo = 'records'")" "SND-CONTEXT-071|3|Linear refused the key (401)|1"
check "error: the earlier rows stay" "$(sqlq "$LCDB" "$LCN")" "37"
lcatch typical >/dev/null 2>&1
check "typical after error clears the failed sync" "$(sqlq "$LCDB" "SELECT ifnull(last_error, 'none') || '|' || failures || '|' || ifnull(last_error_text, 'none') FROM source_cursor WHERE source = 'linear' AND repo = 'records'")" "none|0|none"
lcatch read-error >/dev/null 2>&1; check "linear-catchup read-error exits 0" "$?" "0"
check "read-error: the cursor row is healthy" "$(sqlq "$LCDB" "SELECT ifnull(last_error, 'none') FROM source_cursor WHERE source = 'linear' AND repo = 'records'")" "none"
check "read-error: the changes table is gone for the strip's read" "$(sqlq "$LCDB" "SELECT COUNT(*) FROM sqlite_master WHERE name = 'linear_issue_change'")" "0"
check "read-error: the marker is cleared" "$(jq -r '.linearSince // "none"' "$LC/ui/preferences.json")" "none"
lcatch typical >/dev/null 2>&1; check "typical after error exits 0" "$?" "0"
check "typical after error has put the table back with its rows" "$(sqlq "$LCDB" "$LCN")" "37"

# --- context sources: the node panel's source links (#1702) ---
contains "the usage lists context sources" "$(bash "$FIXTURE" --help 2>&1)" "sindri-ui-fixture.sh context sources"
SX="$TMPDIR/srcs"
mkdir -p "$SX/profile" "$SX/context"
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\n' > "$SX/profile/profile.yaml"
SXDB="$SX/context/context.db"
srcs() { AW_SINDRI_HOME="$SX" SINDRI="$STUB" bash "$FIXTURE" context sources "$@"; }
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" context sources >/dev/null 2>&1; check "context sources refuses outside a guarded home" "$?" "2"
srcs extra >/dev/null 2>&1; check "context sources takes no arguments (exit 2)" "$?" "2"
out="$(srcs 2>&1)"; check "context sources before context up exits 2" "$?" "2"
contains "context sources before context up says to run context up" "$out" "run 'context up' first"
AW_SINDRI_HOME="$SX" SINDRI="$STUB" bash "$FIXTURE" context up >/dev/null 2>&1
for m in 0001 0018 0019 0020 0022 0023; do migrate "$SXDB" "$(ls "$ROOT"/src/Context/Aw.Context.Infrastructure/Store/Migrations/${m}_*.sql)"; done
: > "$STUB_LOG"
out="$(srcs 2>/dev/null)"; check "context sources exits 0" "$?" "0"
check "context sources prints the repo, the doc node and the ticket node" "$(printf '%s' "$out" | tr '\n' ' ')" "aw-split-panes feature:staffing.schema record:linear:issue/0e5a1b00-0000-4000-8000-000000000702"
SXR="$SX/ui-fixture-context/aw-split-panes"
check "the tabs repo's origin is the GitHub remote" "$(git -C "$SXR" remote get-url origin)" "https://github.com/acme/tabs.git"
SXM="$SX/ui-fixture-context/sources-map.json"
check "the imported map names the tabs repo's HEAD as its commit" "$(jq -r .commit "$SXM")" "$(git -C "$SXR" rev-parse HEAD)"
check "the doc node rests on a migrations/0001_staffing/migration.sql path of the repo" "$(jq -r '.nodes[] | select(.key == "feature:staffing.schema") | .evidence[0].key' "$SXM")" "repo:aw-split-panes/migrations/0001_staffing/migration.sql"
contains "context sources imports the sources map as the owner" "$(cat "$STUB_LOG")" "context map import $SXM"
check "the ticket's record is stored with its Linear URL, in the profile team's audience" "$(sqlq "$SXDB" "SELECT url, audience_key, title FROM source_record WHERE key = 'linear:issue/0e5a1b00-0000-4000-8000-000000000702'")" "https://linear.app/acme/issue/SPL-142/roll-out-staffing|linear:SPL|Roll out staffing"
contains "context sources puts the Linear team in the live profile" "$(cat "$SX/profile/profile.yaml")" "linear: { teams: [SPL] }"
: > "$STUB_LOG"
srcs >/dev/null 2>&1; check "a second context sources exits 0" "$?" "0"
check "a second context sources keeps one record row and one origin" "$(sqlq "$SXDB" "SELECT COUNT(*) FROM source_record") $(git -C "$SXR" remote | wc -l | tr -d ' ')" "1 1"
check "a second context sources adds the team to the profile once" "$(grep -c 'teams: \[SPL\]' "$SX/profile/profile.yaml")" "1"
absent "a second context sources imports nothing" "$(cat "$STUB_LOG")" "context map import"

# --- todos: the todo panel's data (todo spec §10 Ph2), added through the CLI with fixed client ids ---
contains "the usage lists todos" "$(bash "$FIXTURE" --help 2>&1)" "sindri-ui-fixture.sh todos"
TD="$TMPDIR/todos"
mkdir -p "$TD/profile"
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\n' > "$TD/profile/profile.yaml"
tds() { AW_SINDRI_HOME="$TD" SINDRI="$STUB" bash "$FIXTURE" todos "$@"; }
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" todos >/dev/null 2>&1; check "todos refuses outside a guarded home" "$?" "2"
tds extra >/dev/null 2>&1; check "todos takes no arguments (exit 2)" "$?" "2"
: > "$STUB_LOG"; : > "$STUB_LOG.todos"; : > "$STUB_LOG.todos.done"
tds >/dev/null 2>&1; check "todos exits 0" "$?" "0"
check "todos adds 11 todos" "$(grep -c '^todo add' "$STUB_LOG")" "11"
check "every add carries a fixed client id" "$(grep -c -- '--client-id ui-fixture-todo-' "$STUB_LOG")" "11"
contains "todos adds an overdue one (a past date)" "$(cat "$STUB_LOG")" "todo add renew the staging cert --client-id ui-fixture-todo-01 --json --due 2020-01-01"
check "todos adds two due today" "$(grep -c -- '--due today' "$STUB_LOG")" "2"
contains "todos gives one todo two links" "$(cat "$STUB_LOG")" "--link acme/api#12 --link ENG-12"
check "todos adds three with no date" "$(grep '^todo add' "$STUB_LOG" | grep -vc -- '--due')" "3"
check "todos marks exactly one done" "$(grep -c '^todo done' "$STUB_LOG")" "1"
: > "$STUB_LOG"
tds >/dev/null 2>&1; check "a second todos exits 0" "$?" "0"
check "a second todos leaves 11 todos (same client ids)" "$(wc -l < "$STUB_LOG.todos" | tr -d ' ')" "11"
absent "a second todos marks nothing done again" "$(cat "$STUB_LOG")" "todo done"
# todos read-error / read-ok (#2908): only the todo read fails
tds nope >/dev/null 2>&1; check "todos refuses an unknown state (exit 2)" "$?" "2"
tds read-error >/dev/null 2>&1; check "todos read-error with no views exits 2" "$?" "2"
TDDB="$TD/views.db"
# one statement per call: sqlq's python fallback (no sqlite3 CLI) runs a single statement
sqlq "$TDDB" "CREATE TABLE view_todo (key TEXT)" >/dev/null
sqlq "$TDDB" "INSERT INTO view_todo VALUES ('a')" >/dev/null
sqlq "$TDDB" "CREATE TABLE view_items (key TEXT)" >/dev/null
tds read-error >/dev/null 2>&1; check "todos read-error exits 0" "$?" "0"
check "read-error: view_todo is gone for the route's read" "$(sqlq "$TDDB" "SELECT COUNT(*) FROM sqlite_master WHERE name = 'view_todo'")" "0"
check "read-error: the other views stay" "$(sqlq "$TDDB" "SELECT COUNT(*) FROM sqlite_master WHERE name = 'view_items'")" "1"
tds read-error >/dev/null 2>&1; check "a second read-error exits 0" "$?" "0"
tds read-ok >/dev/null 2>&1; check "todos read-ok exits 0" "$?" "0"
check "read-ok: view_todo is back with its row" "$(sqlq "$TDDB" "SELECT COUNT(*) FROM view_todo")" "1"
tds read-ok >/dev/null 2>&1; check "a second read-ok exits 0" "$?" "0"

# --- resources: the Resources page's data (#2258 PR18b, #2494), files under <home>/resources ---
contains "the usage lists resources" "$(bash "$FIXTURE" --help 2>&1)" "sindri-ui-fixture.sh resources"
RS="$TMPDIR/resources"
mkdir -p "$RS/profile"
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\n' > "$RS/profile/profile.yaml"
rss() { AW_SINDRI_HOME="$RS" SINDRI="$STUB" bash "$FIXTURE" resources "$@"; }
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" resources >/dev/null 2>&1; check "resources refuses outside a guarded home" "$?" "2"
[ ! -e "$WORK/elsewhere/resources" ]; check "a refused resources writes nothing" "$?" "0"
rss extra >/dev/null 2>&1; check "resources takes no arguments (exit 2)" "$?" "2"
rss >/dev/null 2>&1; check "resources exits 0" "$?" "0"
check "the workflow's database is 1.2 MB" "$(wc -c < "$RS/resources/workflow/morning-check/state.db" | tr -d ' ')" "1258291"
check "the workflow's disk holds 300 KB" "$(wc -c < "$RS/resources/workflow/morning-check/scratch/notes.txt" | tr -d ' ')" "307200"
check "the toolkit's disk holds 40 KB" "$(wc -c < "$RS/resources/toolkit/notes/cache/index.bin" | tr -d ' ')" "40960"
rss >/dev/null 2>&1; check "a second resources exits 0" "$?" "0"
check "a second resources keeps the same three files" "$(find "$RS/resources" -type f | wc -l | tr -d ' ') $(wc -c < "$RS/resources/workflow/morning-check/state.db" | tr -d ' ')" "3 1258291"

# --- connections: the Connections page's data (#2537), rows of the connection table ---
contains "the usage lists connections" "$(bash "$FIXTURE" --help 2>&1)" "sindri-ui-fixture.sh connections"
CN="$TMPDIR/connections"
mkdir -p "$CN/profile"
printf 'schemaVersion: 1\nuser: fixture\nservices:\n  heartbeat: { every: 5s }\n' > "$CN/profile/profile.yaml"
cns() { AW_SINDRI_HOME="$CN" SINDRI="$STUB" bash "$FIXTURE" connections "$@"; }
AW_SINDRI_HOME="$WORK/elsewhere" bash "$FIXTURE" connections typical >/dev/null 2>&1; check "connections refuses outside a guarded home" "$?" "2"
cns bogus >/dev/null 2>&1; check "connections refuses an unknown state (exit 2)" "$?" "2"
cns typical >/dev/null 2>&1; check "connections without a ledger exits 2" "$?" "2"
migrate "$CN/ledger.db" "$ROOT/src/Sindri/Aw.Sindri.Infrastructure/Ledger/Migrations/0010_connections.sql"
cns typical >/dev/null 2>&1; check "connections typical exits 0" "$?" "0"
check "typical writes four rows" "$(sqlq "$CN/ledger.db" "SELECT COUNT(*) FROM connection")" "4"
check "typical: three connected oauth presets and one custom rest row" "$(sqlq "$CN/ledger.db" "SELECT group_concat(name || ':' || status, ',') FROM (SELECT * FROM connection ORDER BY name)")" "billing-api:prod:connected,github:sam-rivera:connected,google:sam-acme:connected,linear:acme:connected"
check "the custom row has no preset and no refresh time" "$(sqlq "$CN/ledger.db" "SELECT ifnull(preset, 'none') || ' ' || ifnull(last_refreshed_at, 'never') FROM connection WHERE name = 'billing-api:prod'")" "none never"
check "last used is about 30 minutes for GitHub" "$(sqlq "$CN/ledger.db" "SELECT CAST((strftime('%s','now') - strftime('%s', last_refreshed_at)) / 60 AS INTEGER) FROM connection WHERE name = 'github:sam-rivera'")" "30"
check "every timestamp is ISO UTC text the store reads" "$(sqlq "$CN/ledger.db" "SELECT COUNT(*) FROM connection WHERE created_at NOT GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]T[0-9][0-9]:[0-9][0-9]:[0-9][0-9]Z'")" "0"
check "the secret pointers name keychain entries and hold no token" "$(sqlq "$CN/ledger.db" "SELECT secret_pointer FROM connection WHERE name = 'linear:acme'")" "keychain:sindri-connection/linear.acme"
cns error >/dev/null 2>&1; check "connections error exits 0" "$?" "0"
check "error keeps four rows (no duplicates)" "$(sqlq "$CN/ledger.db" "SELECT COUNT(*) FROM connection")" "4"
check "error: Linear needs sign-in with the provider's reason" "$(sqlq "$CN/ledger.db" "SELECT status || '|' || status_reason FROM connection WHERE name = 'linear:acme'")" "needs-sign-in|the provider refused the refresh (invalid_grant)"
cns typical >/dev/null 2>&1
check "typical after error clears the reason" "$(sqlq "$CN/ledger.db" "SELECT status || '|' || ifnull(status_reason, 'none') FROM connection WHERE name = 'linear:acme'")" "connected|none"
sqlq "$CN/ledger.db" "INSERT INTO connection VALUES ('slack:hand', 'rest', NULL, '{}', NULL, 'connected', NULL, 'human', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', NULL)" >/dev/null
cns down >/dev/null 2>&1; check "connections down exits 0" "$?" "0"
check "down removes only the fixture's rows" "$(sqlq "$CN/ledger.db" "SELECT group_concat(name) FROM connection")" "slack:hand"
cns empty >/dev/null 2>&1; check "connections empty exits 0" "$?" "0"
check "empty removes every row" "$(sqlq "$CN/ledger.db" "SELECT COUNT(*) FROM connection")" "0"

exit "$fail"
