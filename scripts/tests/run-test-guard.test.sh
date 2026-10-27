#!/usr/bin/env bash
# Tests for scripts/run-test-guard.sh. Run: bash scripts/tests/run-test-guard.test.sh
# Linux only (the guard reads /proc/<pid>/environ). Signals only the PIDs it finds by its own token, never a pattern.
set -uo pipefail
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GUARD="$DIR/../run-test-guard.sh"
fail=0

if [ "$(uname -s)" != "Linux" ]; then echo "skip - run-test-guard reads /proc (Linux only)"; exit 0; fi

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
token="rtgtest-$$-$(date +%s%N)"

with_token() {  # every PID of this user whose command line carries the token (the daemonized sleep is renamed to it)
  local p
  for p in /proc/[0-9]*; do
    p=${p#/proc/}
    [ -O "/proc/$p" ] || continue
    { tr '\0' ' ' < "/proc/$p/cmdline"; } 2>/dev/null | grep -q -- "$token" && echo "$p"
  done
}
cleanup() {
  local left; left="$(with_token)"
  [ -z "$left" ] || kill -KILL $left 2>/dev/null
  rm -rf -- "$WORK"
}
trap cleanup EXIT

# A double fork into its own session, named after the token (the way a fixture's serve outlives its check).
daemonize="( setsid bash -c 'exec -a \"\$0\" sleep 300' $token </dev/null >/dev/null 2>&1 & ); sleep 0.5"

# --- (a) the command's exit code passes through, and a daemonized child does not outlive it ---
bash "$GUARD" --timeout 30 --grace 3 -- bash -c "$daemonize; exit 7"
check "a command's exit code passes through" "$?" "7"
check "nothing tagged is left after a normal exit" "$(with_token | wc -l | tr -d ' ')" "0"

bash "$GUARD" -- true
check "exit 0 passes through" "$?" "0"

# --- (b) a timeout exits 124, the check's EXIT trap runs and its daemonized child is gone ---
start=$SECONDS
bash "$GUARD" --timeout 2 --grace 3 -- bash -c "trap 'touch \"$WORK/trap-ran\"' EXIT; trap 'exit 143' TERM; $daemonize; sleep 120" 2>"$WORK/b.err"
rc=$?
took=$((SECONDS - start))
check "a timeout exits 124" "$rc" "124"
check "the timeout returns promptly" "$([ "$took" -lt 20 ] && echo yes || echo "no (${took}s)")" "yes"
check "the check's EXIT trap ran" "$([ -e "$WORK/trap-ran" ] && echo yes || echo no)" "yes"
check "nothing tagged is left after a timeout" "$(with_token | wc -l | tr -d ' ')" "0"
check "the timeout is said on stderr" "$(grep -c 'timed out after 2s' "$WORK/b.err")" "1"

# --- (c) usage errors exit 2 ---
bash "$GUARD" >/dev/null 2>&1;                          check "no arguments exit 2" "$?" "2"
bash "$GUARD" --timeout 5 >/dev/null 2>&1;              check "no command exits 2" "$?" "2"
bash "$GUARD" --bogus -- true >/dev/null 2>&1;          check "an unknown option exits 2" "$?" "2"
bash "$GUARD" --timeout abc -- true >/dev/null 2>&1;    check "a non-numeric timeout exits 2" "$?" "2"
bash "$GUARD" --timeout >/dev/null 2>&1;                check "a missing value exits 2" "$?" "2"

exit "$fail"
