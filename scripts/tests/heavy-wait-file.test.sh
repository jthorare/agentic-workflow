#!/usr/bin/env bash
# Tests for the wait file scripts/heavy.sh writes: <locks>/heavy-wait/<wrapper pid>, one JSON line per job waiting for a slot
# (read later by `aw-meta heavy --json`, #3142). Covers: a waiter writes it (pid, session, lock, UTC since, exact argv, cwd);
# it is gone before the job's command runs; a job that gets a slot at once writes none; a TERMed waiter and a lock timeout
# (exit 75) leave none. Everything runs on a temp AW_STATE_DIR with a sleeper as the box-wide holder; every pid killed is one
# this suite started and recorded. Every wait is bounded at ~10 s.
# Run: bash scripts/tests/heavy-wait-file.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HEAVY="$DIR/heavy.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
bgpids=()
cleanup() { for p in ${bgpids[@]+"${bgpids[@]}"}; do kill "$p" 2> /dev/null; done; find "$WORK" -delete 2> /dev/null; }   # by recorded pid only
trap cleanup EXIT
for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY)'); do unset "$v"; done   # this suite may itself run under a heavy job
unset DOTNET_PROCESSOR_COUNT MSBUILDDISABLENODEREUSE
STATE="$WORK/state"; LOCKS="$STATE/locks"; RUN="$WORK/run"; mkdir -p "$STATE" "$RUN"
export AW_STATE_DIR="$STATE" AW_HEAVY_SESSION_PID="$$" AW_HEAVY_SLOTS=1
echo '0::/test.slice' > "$WORK/cgroup"; export AW_HEAVY_TEST_CGROUP="$WORK/cgroup"
STUB="$WORK/stub"; mkdir -p "$STUB"; printf '#!/usr/bin/env bash\nexit 1\n' > "$STUB/systemd-run"; chmod +x "$STUB/systemd-run"
export AW_HEAVY_TEST_PATH="$STUB"   # no scope: the suite never creates a real systemd scope
mkdir -p "$WORK/scripts"; ln -s "$DIR/../config" "$WORK/config"
ONE="$WORK/scripts/heavy-one.sh"; sed 's/^TIMEOUT=5400$/TIMEOUT=1/' "$HEAVY" > "$ONE"
LOCK="$LOCKS/heavy-job.lock"

wait_for() { local i; for i in $(seq 1 100); do eval "$1" && return 0; sleep 0.1; done; return 1; }   # <= 10 s
# take_slot: the box-wide slot held by a sleeper of ours (pid in $HOLDER).
take_slot() {
  sleep 60 & HOLDER=$!; bgpids+=("$HOLDER")
  mkdir -p "$LOCK"; echo "$HOLDER 2026-01-01T00:00:00Z session=x held" > "$LOCK/owner"
}
free_slot() { unlink "$LOCK/owner" 2> /dev/null; rmdir "$LOCK" 2> /dev/null; kill "$HOLDER" 2> /dev/null; }
reset() { find "$LOCKS" -mindepth 1 -delete 2> /dev/null; }

ARG1='two words'; ARG2='q"x\y'

# --- 1 and 2: a waiter writes the file; it is gone before the job's command runs ---
reset; take_slot
# The job reads its parent's pid ($PPID, the wrapper) to name the file it must not find.
(cd "$RUN" && exec bash "$HEAVY" --expensive bash -c 'test ! -e "'"$LOCKS"'/heavy-wait/$PPID"; echo $? > "$1"' _ "$WORK/rc" "$ARG1" "$ARG2") > "$WORK/w1.out" 2>&1 &
W=$!; bgpids+=("$W")
WF="$LOCKS/heavy-wait/$W"
wait_for '[ -s "$WF" ]'; check "a waiting job creates heavy-wait/<its pid>" "$([ -s "$WF" ] && echo yes || echo no)" "yes"
echo "  line: $(cat "$WF" 2> /dev/null)"
if command -v python3 > /dev/null 2>&1; then
  got="$(python3 -I - "$WF" "$W" "$$" "$RUN" <<'PY'
import json, sys, datetime
f, pid, sess, cwd = sys.argv[1:5]
try:
    d = json.loads(open(f).read())
    datetime.datetime.strptime(d["since"], "%Y-%m-%dT%H:%M:%SZ")
    ok = (d["pid"] == int(pid) and d["session"] == int(sess) and d["lock"] == "box-wide" and d["cwd"] == cwd
          and d["argv"][0] == "bash")
    print("ok" if ok else "bad " + repr(d))
except Exception as e:
    print("bad " + repr(e))
PY
)"
  check "valid JSON: pid, session, lock box-wide, UTC since, cwd" "$got" "ok"
  got="$(python3 -I - "$WF" "$ARG1" "$ARG2" <<'PY'
import json, sys
d = json.loads(open(sys.argv[1]).read())
print("ok" if d["argv"][-2:] == [sys.argv[2], sys.argv[3]] and d["argv"][0] == "bash" and d["argv"][1] == "-c" else "bad " + repr(d["argv"]))
PY
)"
  check "argv is exact, an arg with a space and one with a quote and backslash" "$got" "ok"
else
  echo "skip - no python3: JSON content checks"
fi
free_slot
wait_for '[ -s "$WORK/rc" ]'
check "the waiter took the slot and ran its command" "$([ -s "$WORK/rc" ] && echo yes || echo no)" "yes"
check "the file was gone before the command ran (test ! -e exit)" "$(cat "$WORK/rc" 2> /dev/null)" "0"
wait "$W" 2> /dev/null
check "no file left after the job" "$(ls "$LOCKS/heavy-wait" | wc -l | tr -d ' ')" "0"

# --- 3: a job that gets a slot at once writes no file ---
reset; rm -f "$WORK/seen"
bash "$HEAVY" --expensive bash -c 'ls "$1" | wc -l > "$2"' _ "$LOCKS/heavy-wait" "$WORK/seen" > /dev/null 2>&1
check "no waiting: no wait file while the job runs" "$(tr -d ' ' < "$WORK/seen")" "0"

# --- 4: a TERMed waiter leaves no file ---
reset; take_slot
bash "$HEAVY" --expensive true > /dev/null 2>&1 &
W=$!; bgpids+=("$W"); WF="$LOCKS/heavy-wait/$W"
wait_for '[ -s "$WF" ]'; check "TERM case: the waiter wrote its file" "$([ -s "$WF" ] && echo yes || echo no)" "yes"
kill -TERM "$W" 2> /dev/null; wait "$W" 2> /dev/null; rc=$?
check "TERM case: exit 130" "$rc" "130"
check "TERM case: the file is gone" "$([ -e "$WF" ] && echo yes || echo no)" "no"
free_slot

# --- 5: a lock timeout exits 75 and leaves no file ---
reset; take_slot
bash "$ONE" --expensive true > /dev/null 2>&1 &
W=$!; bgpids+=("$W"); WF="$LOCKS/heavy-wait/$W"
wait_for '[ -s "$WF" ] || ! kill -0 "$W" 2> /dev/null'
wait "$W" 2> /dev/null; rc=$?
check "timeout case: exit 75" "$rc" "75"
check "timeout case: no file left" "$(ls "$LOCKS/heavy-wait" | wc -l | tr -d ' ')" "0"
free_slot

[ "$fail" -eq 0 ] && echo "all heavy-wait-file tests passed"
exit "$fail"
