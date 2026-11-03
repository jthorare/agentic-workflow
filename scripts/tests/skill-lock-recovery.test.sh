#!/usr/bin/env bash
# Tests for skills/_shared/skill-lock.sh after a failed or abandoned call (#1989): a failed acquire leaves no lock
# of its own, and the next acquire gets the lock. Run: bash scripts/tests/skill-lock-recovery.test.sh
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
LOCK="$ROOT/skills/_shared/skill-lock.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
holder=""
trap '[ -z "$holder" ] || kill "$holder" 2>/dev/null || true; rm -rf "$WORK"' EXIT
export AW_STATE_DIR="$WORK/state"
export SKILL_LOCK_TIMEOUT=1
LF="$AW_STATE_DIR/.browser.lock"

# --- a session that takes the lock and ends without releasing: the next session's acquire takes it over ---
# The lock's owner is the calling shell's parent (#1991), so the abandoning session is a shell of its own that exits.
bash -c 'bash -c "LOCK_NAME=browser source \"\$1\"; acquire_lock >/dev/null" _ "$1"; exit $?' _ "$LOCK"
dead="$(cat "$LF")"
bash -c 'LOCK_NAME=browser source "$1"; acquire_lock' _ "$LOCK" >"$WORK/out" 2>&1 && rc=0 || rc=$?
check "a second acquire after an abandoned one exits 0" "$rc" "0"
check "the second acquire now holds the lock (this script is its session)" "$(cat "$LF")" "$$"
check "the abandoned owner is gone from the lock" "$([ "$(cat "$LF")" = "$dead" ] && echo kept || echo replaced)" "replaced"
rm -f "$LF"

# --- an acquire that fails (the lock is held by a live process) leaves the holder's lock, not one of its own ---
sleep 30 & holder=$!
mkdir -p "$AW_STATE_DIR"; echo "$holder" >"$LF"
out="$(bash -c 'LOCK_NAME=browser source "$1"; acquire_lock' _ "$LOCK" 2>&1)" && rc=0 || rc=$?
check "an acquire against a live holder fails" "$rc" "1"
check "it says it timed out on the browser lock" "$(grep -c '^skill-lock\[browser\]: TIMEOUT' <<<"$out")" "1"
check "the failed acquire leaves the holder's pid, not its own" "$(cat "$LF")" "$holder"

# --- once the holder is gone, the next documented acquire gets the lock ---
kill "$holder" 2>/dev/null || true; wait "$holder" 2>/dev/null || true; holder=""
out="$(bash -c 'LOCK_NAME=browser source "$1"; acquire_lock; LOCK_NAME=browser source "$1"; release_lock' _ "$LOCK" 2>&1)" && rc=0 || rc=$?
check "a second acquire after a failed one exits 0" "$rc" "0"
check "and its release leaves no lock behind" "$([ -e "$LF" ] && echo present || echo gone)" "gone"

exit "$fail"
