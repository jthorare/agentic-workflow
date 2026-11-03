#!/usr/bin/env bash
# Tests for skills/_shared/skill-lock.sh, called the way the skills document it:
#   LOCK_NAME=browser source skill-lock.sh; acquire_lock
# Run: bash scripts/tests/skill-lock.test.sh
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
trap 'rm -rf "$WORK"' EXIT
# The lock lives under AW_STATE_DIR, never the real ~/.agentic-workflow.
export AW_STATE_DIR="$WORK/state"
export SKILL_LOCK_TIMEOUT=2

# --- the documented acquire: exit 0, names the lock, leaves this shell's pid in the file ---
out="$(bash -c 'LOCK_NAME=browser source "$1"; acquire_lock; echo "pid=$$"' _ "$LOCK" 2>&1)" && rc=0 || rc=$?
check "documented acquire exits 0" "$rc" "0"
check "acquire names the browser lock" "$(grep -c '^skill-lock\[browser\]: acquired' <<<"$out")" "1"
rm -f "$AW_STATE_DIR/.browser.lock"

# --- the documented acquire then release in one shell: exit 0, lock file gone ---
out="$(bash -c 'LOCK_NAME=browser source "$1"; acquire_lock; LOCK_NAME=browser source "$1"; release_lock' _ "$LOCK" 2>&1)" && rc=0 || rc=$?
check "documented acquire + release exits 0" "$rc" "0"
check "release names the browser lock" "$(grep -c '^skill-lock\[browser\]: released' <<<"$out")" "1"
check "release removes the lock file" "$([ -e "$AW_STATE_DIR/.browser.lock" ] && echo present || echo gone)" "gone"

# --- the default name, no LOCK_NAME at all, still works ---
out="$(bash -c 'unset LOCK_NAME; source "$1"; acquire_lock; release_lock' _ "$LOCK" 2>&1)" && rc=0 || rc=$?
check "acquire + release with the default name exits 0" "$rc" "0"
check "default name is skill" "$(grep -c '^skill-lock\[skill\]: acquired' <<<"$out")" "1"

exit "$fail"
