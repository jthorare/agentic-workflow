#!/usr/bin/env bash
# Tests for skills/_shared/skill-lock.sh called the way skills call it (#1991): acquire in one Bash call, release in a
# later one, each call a fresh shell under the same agent process. Here this script plays the agent: every `bash -c`
# it starts is one Bash call. A "second session" is a shell under a different parent. Run: bash scripts/tests/skill-lock-sessions.test.sh
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
other=""
trap '[ -z "$other" ] || kill "$other" 2>/dev/null || true; rm -rf "$WORK"' EXIT
export AW_STATE_DIR="$WORK/state"
export SKILL_LOCK_TIMEOUT=1
LF="$AW_STATE_DIR/.browser.lock"
call() { bash -c "LOCK_NAME=browser source \"\$1\"; $1" _ "$LOCK" 2>&1; }      # one Bash call of this "session"
other_call() { bash -c 'bash -c "LOCK_NAME=browser source \"\$1\"; $2" _ "$1" "$2"' _ "$LOCK" "$1" 2>&1; } # another session's call

# --- acquire in one call; the lock still holds in the next ---
out="$(call acquire_lock)" && rc=0 || rc=$?
check "acquire in one call exits 0" "$rc" "0"
out="$(other_call acquire_lock)" && rc=0 || rc=$?
check "another session's acquire waits and times out while the lock is held" "$rc" "1"
check "it names the timeout" "$(grep -c 'TIMEOUT' <<<"$out")" "1"

# --- release in a later call removes the lock ---
out="$(call release_lock)" && rc=0 || rc=$?
check "release in a later call exits 0" "$rc" "0"
check "release in a later call says released" "$(grep -c 'released' <<<"$out")" "1"
check "release in a later call removes the lock file" "$([ -e "$LF" ] && echo present || echo gone)" "gone"

# --- a session that ends while holding the lock: the next acquire takes it over ---
bash -c 'bash -c "LOCK_NAME=browser source \"\$1\"; acquire_lock >/dev/null" _ "$1"; sleep 30' _ "$LOCK" & other=$!
for _ in $(seq 50); do [ -s "$LF" ] && break; sleep 0.1; done
out="$(call acquire_lock)" && rc=0 || rc=$?
check "acquire while another live session holds the lock times out" "$rc" "1"
kill "$other" 2>/dev/null || true; wait "$other" 2>/dev/null || true; other=""
out="$(call acquire_lock)" && rc=0 || rc=$?
check "once that session is gone, the next acquire takes the lock" "$rc" "0"
out="$(call release_lock)" && rc=0 || rc=$?
check "and releases it in a later call" "$([ -e "$LF" ] && echo present || echo gone)" "gone"

exit "$fail"
