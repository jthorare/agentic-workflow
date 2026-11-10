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

# --- the reclaim guard (#2569): one left by a waiter that died mid-reclaim is cleared; a live or young one is waited on ---
G="$LF.reclaim"
dead_pid() { bash -c 'exit 0' & local p=$!; wait "$p"; echo "$p"; }
plain() { SKILL_LOCK_TIMEOUT="$1" bash -c 'LOCK_NAME=browser source "$1"; acquire_lock' _ "$LOCK" >/dev/null 2>&1; }
rm -rf "$G" "$LF"; dead_pid >"$LF"; mkdir "$G"; : >"$G/$(dead_pid)"
plain 3 && rc=0 || rc=$?
check "a guard whose holder died is cleared and the dead holder's lock reclaimed" "$rc:$(cat "$LF" 2>/dev/null):$([ -e "$G" ] && echo guard || echo none)" "0:$$:none"
rm -rf "$G" "$LF"; dead_pid >"$LF"; sleep 30 & holder=$!; mkdir "$G"; : >"$G/$holder"
dead="$(cat "$LF")"; plain 1 && rc=0 || rc=$?
check "a guard held by a live reclaimer is waited on: the acquire times out, the lock and the guard stay" "$rc:$(cat "$LF"):$(ls "$G")" "1:$dead:$holder"
kill "$holder" 2>/dev/null || true; wait "$holder" 2>/dev/null || true; holder=""
rm -rf "$G" "$LF"; dead_pid >"$LF"; mkdir "$G"
plain 1 && rc=0 || rc=$?
check "a young empty guard (a reclaimer between its mkdir and its pid file) is not cleared" "$rc:$([ -d "$G" ] && echo guard)" "1:guard"
rm -rf "$G" "$LF"

# --- a live holder's pid file in the guard (#2616): a dead pid file that sorts first must not let the lock be removed ---
# 04194399: the leading 0 sorts it before any real pid, and it is above the default pid_max, so it is never live.
reclaim_once() { bash -c 'LOCK_NAME=browser source "$1"; set +e; _lock_reclaim; echo $?' _ "$LOCK" 2>/dev/null | tail -n 1; }
rm -rf "$G" "$LF"; mkdir -p "$AW_STATE_DIR"; dead_pid >"$LF"; mkdir "$G"
sleep 30 & holder=$!
: >"$G/04194399"; : >"$G/$holder"
check "precondition: the dead pid file sorts before the live holder's in the guard" "$(printf '%s\n' "$G"/* | head -n 1 | sed 's|.*/||')" "04194399"
reclaim_once >/dev/null
check "a dead pid file ahead of a live holder's does not let the waiter remove the lock" "$([ -e "$LF" ] && echo kept || echo removed)" "kept"
check "the live holder's pid file is still in the guard" "$([ -e "$G/$holder" ] && echo present || echo gone)" "present"
kill "$holder" 2>/dev/null || true; wait "$holder" 2>/dev/null || true; holder=""

# --- an old empty guard and a dead lock owner: within two reclaims the lock is gone (a fix must not write into an empty guard) ---
rm -rf "$G" "$LF"; dead_pid >"$LF"; mkdir "$G"
touch -d '5 minutes ago' "$G" 2>/dev/null || touch -t "$(date -v-5M +%Y%m%d%H%M 2>/dev/null)" "$G"
reclaim_once >/dev/null; reclaim_once >/dev/null
check "an old empty guard is cleared and the dead owner's lock removed within two reclaims" "$([ -e "$LF" ] && echo kept || echo removed)" "removed"
rm -rf "$G" "$LF"

exit "$fail"
