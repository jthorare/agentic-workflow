#!/usr/bin/env bash
# Tests for scripts/owner-check.sh against a stub build (src/Sindri/aot-smoke/run.sh isn't on main until S1 merges)
# and a stub sindri binary, in a temp repo and a temp state root. Nothing here touches a real Sindri home, port or
# profile: the "real" home and profile are temp files too, and the port is a free one (OWNER_CHECK_PORT).
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PASS=0
FAIL=0
T="$(mktemp -d "${TMPDIR:-/tmp}/owner-check-test.XXXXXX")"
LISTENERS=()
cleanup() {
  local p
  for p in ${LISTENERS[@]+"${LISTENERS[@]}"}; do kill "$p" 2>/dev/null || true; done
  [ ! -f "$T/serve.pid" ] || kill "$(cat "$T/serve.pid")" 2>/dev/null || true
  rm -rf "$T"
}
trap cleanup EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
check() { if eval "$2"; then ok "$1"; else fail "$1"; printf '%s\n' "${OUT:-}" | sed 's/^/       | /'; fi; }
expect() { # expect <name> <code> <substring>...
  local name="$1" code="$2" good=1 s; shift 2
  [ "$CODE" = "$code" ] || good=0
  for s in "$@"; do case "$OUT" in *"$s"*) ;; *) good=0 ;; esac; done
  if [ "$good" = 1 ]; then ok "$name"; else fail "$name (exit $CODE, want $code)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
}

free_port() { python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()'; }
listening() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }
listen() { # listen <port> — a bare listener in the background, like an aborted check's process
  ( python3 -c 'import socket,sys,time
s=socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1); s.bind(("127.0.0.1", int(sys.argv[1]))); s.listen(); time.sleep(600)' "$1" >/dev/null 2>&1 &
    echo "$!" > "$T/listener.pid" )
  LISTENERS+=("$(cat "$T/listener.pid")")
  local i; for i in $(seq 1 50); do listening "$1" && return 0; sleep 0.1; done; return 1
}

# ---- the temp repo: the script under test, a stub S1 build and a stub "main" sindri ----
REPO="$T/repo"
mkdir -p "$REPO/scripts" "$REPO/src/Sindri/aot-smoke"
cp "$HERE/../owner-check.sh" "$REPO/scripts/owner-check.sh"
cat > "$T/stub-sindri" <<'STUB'
#!/usr/bin/env bash
# Stub of the PR's sindri: records each call with the home and port it ran with.
[ -z "${AW_SINDRI_PROFILE:-}" ] || echo "PROFILE-LEAK $AW_SINDRI_PROFILE" >> "$OWNER_CHECK_TEST_LOG"
echo "home=${AW_SINDRI_HOME:-unset} port=${AW_SINDRI_PORT:-unset} args=$*" >> "$OWNER_CHECK_TEST_LOG"
case "$*" in
  --headless)
    [ -z "${OWNER_CHECK_TEST_NOLISTEN:-}" ] || { echo "control plane went away"; exit 1; }
    python3 -c 'import socket,sys,time
s=socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1); s.bind(("127.0.0.1", int(sys.argv[1]))); s.listen(); time.sleep(600)' "$AW_SINDRI_PORT" >/dev/null 2>&1 &
    echo "$!" > "$OWNER_CHECK_TEST_PID"
    for _ in $(seq 1 50); do (exec 3<>"/dev/tcp/127.0.0.1/$AW_SINDRI_PORT") 2>/dev/null && break; sleep 0.1; done
    echo "profile needs approval"; exit 1 ;;
  "profile validate --json") printf '{"valid":true,"approved":false,"hash":"%s"}\n' "$(printf 'a%.0s' $(seq 1 64))"; exit 1 ;;
  "profile approve "*) exit 0 ;;
  "context map import "*) [ -f "${@: -1}" ] || exit 2; exit 0 ;;
  stop) [ ! -f "$OWNER_CHECK_TEST_PID" ] || { kill "$(cat "$OWNER_CHECK_TEST_PID")" 2>/dev/null; rm -f "$OWNER_CHECK_TEST_PID"; }; exit 0 ;;
  *) exit 2 ;;
esac
STUB
cat > "$REPO/src/Sindri/aot-smoke/run.sh" <<STUB
#!/usr/bin/env bash
# Stub of S1's AOT smoke: "publishes" the stub binary and its SQLite library.
set -e
[ -z "\${AW_SINDRI_PROFILE:-}" ] || echo "PROFILE-LEAK smoke" >> "\$OWNER_CHECK_TEST_LOG"
root="\$(cd "\$(dirname "\$0")/../../.." && pwd)"
mkdir -p "\$root/artifacts/aot-sindri-smoke/bin"
cp "$T/stub-sindri" "\$root/artifacts/aot-sindri-smoke/bin/sindri"
echo lib > "\$root/artifacts/aot-sindri-smoke/bin/libe_sqlite3.dylib"
echo "aot-sindri-smoke: ok"
STUB
chmod +x "$T/stub-sindri" "$REPO/src/Sindri/aot-smoke/run.sh"
cat > "$T/main-sindri" <<'STUB'
#!/usr/bin/env bash
# Stub of the merged build: context map export <file> from the real home.
echo "main home=${AW_SINDRI_HOME:-unset} port=${AW_SINDRI_PORT:-unset} args=$*" >> "$OWNER_CHECK_TEST_LOG"
[ "$1 $2 $3" = "context map export" ] || exit 2
echo '{"schemaVersion":1}' > "$4"
STUB
chmod +x "$T/main-sindri"

STATE="$T/state"
REAL_HOME="$STATE/sindri-app"
mkdir -p "$REAL_HOME/profile"
cat > "$REAL_HOME/profile/profile.yaml" <<'YAML'
schemaVersion: 1
user: owner
services:
  heartbeat: { every: 5s }
  context-sync:
    every: 15m
  context-purge: { every: 1d }
  context-docs-refresh: { every: 10m }
context:
  labeling:
    enabled: true
    maxTokens: 20000
  sessions:
    enabled: false
api: { port: 7410 }
YAML
cp "$REAL_HOME/profile/profile.yaml" "$T/profile.orig"

PORT="$(free_port)"
export AW_STATE_DIR="$STATE" OWNER_CHECK_PORT="$PORT" OWNER_CHECK_TEST_LOG="$T/calls.log" OWNER_CHECK_TEST_PID="$T/serve.pid"
unset AW_SINDRI_HOME AW_SINDRI_PORT AW_SINDRI_PROFILE SINDRI_MAIN
OC_HOME="$STATE/sindri-app-ownercheck-s-x"
OC_SCRATCH="$STATE/sindri-ownercheck-s-x"
run() { set +e; OUT="$(cd "$T" && bash "$REPO/scripts/owner-check.sh" "$@" </dev/null 2>&1)"; CODE=$?; set -e; }

echo "owner-check.sh"

run; expect "no arguments is a usage error" 2 "usage:"
run s-x sideways; expect "an unknown action is a usage error" 2 "usage:"
run ../x up; expect "a stage that isn't a plain id is refused" 2 "stage"
run s-x up --bogus; expect "an unknown option is a usage error" 2 "--bogus"
run s-x up --seed; expect "--seed needs SINDRI_MAIN (the merged build)" 2 "SINDRI_MAIN"
run s-x up --services context-nope; expect "only the engine's services can be named" 2 "context-nope"

mv "$REPO/src/Sindri/aot-smoke/run.sh" "$T/run.sh.away"; run s-x up
expect "without S1's AOT smoke the build step fails with what it needs" 2 "src/Sindri/aot-smoke/run.sh"
mv "$T/run.sh.away" "$REPO/src/Sindri/aot-smoke/run.sh"

AW_SINDRI_HOME="$OC_HOME" run s-x up
expect "the owner-check home is never the real home" 2 "AW_SINDRI_HOME"

mkdir -p "$OC_HOME"; echo stale > "$OC_HOME/leftover"
: > "$T/calls.log"
SINDRI_MAIN="$T/main-sindri" run s-x up --seed --session --services context-sync --repo work-repo
expect "up builds, makes a fresh home, seeds, edits the profile, starts and approves" 0 "$OC_SCRATCH/bin/sindri" "$OC_HOME" "AW_SINDRI_PORT=$PORT" "--strict-mcp-config" "owner-check.sh s-x down"
check "the build output is copied whole (binary and SQLite library)" '[ -x "$OC_SCRATCH/bin/sindri" ] && [ -f "$OC_SCRATCH/bin/libe_sqlite3.dylib" ]'
check "the home is created fresh (a leftover file is gone) and is 0700" '[ ! -e "$OC_HOME/leftover" ] && [ "$(stat -c %a "$OC_HOME" 2>/dev/null || stat -f %Lp "$OC_HOME")" = 700 ]'
check "the seed is exported by the merged build from the real home (the profile's), with no port" 'grep -qx "main home=$REAL_HOME port=unset args=context map export $OC_HOME/seed.json" "$T/calls.log" && [ -f "$OC_HOME/seed.json" ]'
P="$OC_HOME/profile/profile.yaml"
check "the real profile is copied in and left unchanged" '[ -f "$P" ] && cmp -s "$T/profile.orig" "$REAL_HOME/profile/profile.yaml"'
check "the named engine service is kept, the others removed, other services kept" 'grep -q "^  context-sync:" "$P" && grep -q "^    every: 15m" "$P" && ! grep -q "context-purge\|context-docs-refresh" "$P" && grep -q "^  heartbeat:" "$P"'
check "labeling is off unless the check names it; its other keys stay" 'awk "/^  labeling:/{f=1;next} f&&/^    enabled:/{print \$2; exit}" "$P" | grep -qx false && grep -q "^    maxTokens: 20000" "$P"'
check "sessions are on when the check uses a session" 'awk "/^  sessions:/{f=1;next} f&&/^    enabled:/{print \$2; exit}" "$P" | grep -qx true'
check "start, approve and import run against the scratch home and port, in order" '
  [ "$(grep -v "^main" "$T/calls.log" | sed "s/ args=.*//" | sort -u)" = "home=$OC_HOME port=$PORT" ] &&
  [ "$(grep -v "^main" "$T/calls.log" | sed "s/.* args=//" | tr "\n" "|")" = "--headless|profile validate --json|profile approve $(printf "a%.0s" $(seq 1 64))|context map import $OC_HOME/seed.json|" ]'
check "the MCP config names only sindri-ownercheck on the check's port, with the repo header" '
  grep -q "\"sindri-ownercheck\"" "$OC_SCRATCH/mcp.json" && grep -q "127.0.0.1:$PORT/sindri/mcp" "$OC_SCRATCH/mcp.json" &&
  grep -q "\"X-Sindri-Repo\": \"work-repo\"" "$OC_SCRATCH/mcp.json" && [ "$(grep -c "\"sindri" "$OC_SCRATCH/mcp.json")" = 1 ]'
check "the scratch instance is listening on the check's port" 'listening "$PORT"'

run s-x up; expect "a second check while one runs is refused (one at a time)" 2 "$PORT" "down"

: > "$T/calls.log"
run s-x down
expect "down stops the instance and deletes the home, the build and the MCP config" 0 "down"
check "stop ran against the scratch home" 'grep -qx "home=$OC_HOME port=$PORT args=stop" "$T/calls.log"'
check "nothing listens on the port and the files are gone" '! listening "$PORT" && [ ! -e "$OC_HOME" ] && [ ! -e "$OC_SCRATCH" ] && [ -f "$REAL_HOME/profile/profile.yaml" ]'

run s-x down; expect "down with nothing to tear down still succeeds" 0 "down"

listen "$PORT"; run s-x down
check "down also stops a leftover listener on the port from an aborted check" '[ "$CODE" = 0 ] && ! listening "$PORT"'

: > "$T/calls.log"
run s-x up --labeling
expect "up without --seed or --session" 0 "$OC_HOME"
check "without --seed nothing is exported or imported" '! grep -q "export\|import" "$T/calls.log" && [ ! -e "$OC_HOME/seed.json" ]'
check "--labeling turns labeling on; no session leaves sessions off; no MCP config" '
  awk "/^  labeling:/{f=1;next} f&&/^    enabled:/{print \$2; exit}" "$P" | grep -qx true &&
  awk "/^  sessions:/{f=1;next} f&&/^    enabled:/{print \$2; exit}" "$P" | grep -qx false && [ ! -e "$OC_SCRATCH/mcp.json" ]'
check "no service named: every engine service is removed" '! grep -q "context-sync\|context-purge\|context-docs-refresh" "$P" && grep -q "^  heartbeat:" "$P"'
run s-x down

cp "$T/profile.orig" "$T/flow.yaml"; perl -0pi -e 's/context:\n  labeling:\n    enabled: true\n    maxTokens: 20000\n  sessions:\n    enabled: false\n/context: { sessions: { enabled: false } }\n/' "$T/flow.yaml"
AW_SINDRI_PROFILE="$T/flow.yaml" run s-x up --session --repo work-repo
expect "a profile key it can't edit in place stops with the key to set by hand" 2 "context.sessions.enabled"
run s-x down

cp "$REAL_HOME/profile/profile.yaml" "$T/elsewhere.yaml"
: > "$T/calls.log"
AW_SINDRI_PROFILE="$T/elsewhere.yaml" run s-x up --session --repo work-repo
expect "up with AW_SINDRI_PROFILE pointing at the real profile" 0 "unset AW_SINDRI_PROFILE; export SINDRI="
check "the PR's build (smoke included) never sees AW_SINDRI_PROFILE, so it loads the edited copy" '! grep -q PROFILE-LEAK "$T/calls.log" && awk "/^  sessions:/{f=1;next} f&&/^    enabled:/{print \$2; exit}" "$P" | grep -qx true'
AW_SINDRI_HOME="$OC_HOME" run s-x down
expect "down runs from a shell that exported the owner-check home" 0 "unset SINDRI AW_SINDRI_HOME AW_SINDRI_PORT"
check "  ... and tears it down" '! listening "$PORT" && [ ! -e "$OC_HOME" ]'

printf 'schemaVersion: 1\nuser: owner\nservices: { context-sync: { every: 1m } }\n' > "$T/flowsvc.yaml"
: > "$T/calls.log"
AW_SINDRI_PROFILE="$T/flowsvc.yaml" run s-x up --services context-sync
expect "a flow-style services: is named as the reason, even with --services" 2 "flow mapping"
check "  ... before anything is built, exported or started" '[ ! -s "$T/calls.log" ] && [ ! -e "$OC_SCRATCH" ] && [ ! -e "$OC_HOME" ]'
run s-x down

OWNER_CHECK_TEST_NOLISTEN=1 run s-x up
expect "a start that exits 1 without a listener is a failed start, not a pending approval" 1 "isn't running on port $PORT" "owner-check.sh s-x down"
run s-x down

git init -q "$T/work-repo" && git -C "$T/work-repo" -c user.email=t@t -c user.name=t commit -q --allow-empty -m init
git -C "$T/work-repo" worktree add -q "$T/wt-feature" 2>/dev/null
set +e; OUT="$(cd "$T/wt-feature" && bash "$REPO/scripts/owner-check.sh" s-x up --session </dev/null 2>&1)"; CODE=$?; set -e
check "without --repo the header is the main checkout's name, also from a linked worktree" '[ "$CODE" = 0 ] && grep -q "\"X-Sindri-Repo\": \"work-repo\"" "$OC_SCRATCH/mcp.json"'
run s-x down

tr -d '\r' < "$T/profile.orig" | sed 's/$/\r/' > "$T/crlf.yaml"
AW_SINDRI_PROFILE="$T/crlf.yaml" run s-x up
check "a CRLF profile is edited too (labeling turned off)" '[ "$CODE" = 0 ] && ! grep -q $'"'"'\r'"'"' "$P" && awk "/^  labeling:/{f=1;next} f&&/^    enabled:/{print \$2; exit}" "$P" | grep -qx false'
run s-x down

AW_SINDRI_HOME="$STATE/sindri-app-ownercheck-s-other" run s-x up
expect "another stage's owner-check home is never taken for the real one" 2 "an owner-check home"

printf 'schemaVersion: 1\nuser: owner\n' > "$T/bare.yaml"
AW_SINDRI_PROFILE="$T/bare.yaml" run s-x up --session --services context-sync --repo work-repo
expect "a service the check names but the profile lacks is refused" 2 "context-sync"
run s-x down
AW_SINDRI_PROFILE="$T/bare.yaml" run s-x up --session --repo work-repo
check "missing context keys are added when the check needs them" '[ "$CODE" = 0 ] && grep -q "^context:" "$P" && awk "/^  sessions:/{f=1;next} f&&/^    enabled:/{print \$2; exit}" "$P" | grep -qx true'
run s-x down

echo
echo "owner-check: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
