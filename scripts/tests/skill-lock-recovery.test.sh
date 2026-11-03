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

# --- a call that takes the lock and exits without releasing: the next documented acquire takes it over ---
bash -c 'LOCK_NAME=browser source "$1"; acquire_lock >/dev/null' _ "$LOCK"
dead="$(cat "$LF")"
out="$(bash -c 'LOCK_NAME=browser source "$1"; acquire_lock; echo "me=$$"' _ "$LOCK" 2>&1)" && rc=0 || rc=$?
check "a second acquire after an abandoned one exits 0" "$rc" "0"
check "the second acquire now holds the lock" "$(cat "$LF")" "$(sed -n 's/^me=//p' <<<"$out")"
check "the abandoned pid is gone from the lock" "$([ "$(cat "$LF")" = "$dead" ] && echo kept || echo replaced)" "replaced"
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
