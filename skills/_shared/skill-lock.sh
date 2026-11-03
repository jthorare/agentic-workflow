#!/usr/bin/env bash
# NOTE: sourcing this file enables set -euo pipefail in the caller's shell.
# Callers must tolerate nounset (-u) and pipefail after sourcing.
# In particular, all referenced variables must be set or provide a default.
#
# NOTE: No trap is registered for automatic lock release on unexpected exit.
# Traps in sourced files can interfere with the caller's own trap handlers.
# Recovery from unexpected exits relies on stale-lock detection (kill -0 check).
set -euo pipefail

# Generic skill lockfile — prevents concurrent skill sessions for a given resource.
#
# Usage:
#   LOCK_NAME=browser  source skill-lock.sh   # for browser/Playwright sessions
#   LOCK_NAME=ios-sim  source skill-lock.sh   # for iOS simulator sessions
#   acquire_lock    # blocks until lock acquired or timeout
#   # ... use resource ...
#   release_lock    # release when done
#
# Environment:
#   LOCK_NAME             — lock identifier written into the lock file path (default: "skill")
#   SKILL_LOCK_TIMEOUT    — max seconds to wait (default: 120)
#   SKILL_LOCK_OWNER_PID  — pid written as the lock owner (default: the caller's parent, $PPID)
#
# Owner: agents run every Bash call in a fresh shell that exits when the call ends, so $$
# is dead by the next call. The owner is the caller's parent process (the agent session,
# which outlives each call), so a lock taken in one call holds in later calls of that
# session, excludes other sessions, and is released by a later call of the same session.
# A script that sources this file directly from a long-lived shell should set
# SKILL_LOCK_OWNER_PID=$$.
# Limits: a lead and its subagents run in one agent process, so they are one session
# here and are not kept apart. $PPID is the agent only when the agent starts each Bash
# call's shell itself; a per-call wrapper that forks in between (a sandbox helper) makes
# the wrapper the owner again, so such a host must set SKILL_LOCK_OWNER_PID.

LOCK_NAME="${LOCK_NAME:-skill}"

# sec-2: Validate LOCK_NAME to prevent path traversal and shell metacharacter injection.
# Only alphanumeric characters, hyphens, and underscores are allowed.
if ! [[ "$LOCK_NAME" =~ ^[a-zA-Z0-9_-]+$ ]]; then
  echo "skill-lock: invalid LOCK_NAME '$LOCK_NAME' — must match ^[a-zA-Z0-9_-]+$" >&2
  return 1 2>/dev/null || exit 1
fi

# A `LOCK_NAME=x source` prefix only lasts during the source, so functions read this copy.
SKILL_LOCK_NAME="$LOCK_NAME"

SKILL_LOCK_OWNER="${SKILL_LOCK_OWNER_PID:-$PPID}"
if ! [[ "$SKILL_LOCK_OWNER" =~ ^[0-9]+$ ]]; then
  echo "skill-lock: invalid SKILL_LOCK_OWNER_PID '$SKILL_LOCK_OWNER' — must be digits" >&2
  return 1 2>/dev/null || exit 1
fi

# sec-1: Require bash >= 4 for atomic noclobber (O_CREAT|O_EXCL semantics).
# macOS ships bash 3.2 where noclobber uses stat+open, not open(O_CREAT|O_EXCL),
# making the atomic write claim false on that platform.
if [ "${BASH_VERSINFO[0]:-0}" -lt 4 ]; then
  echo "skill-lock: requires bash >= 4 for atomic noclobber (found $BASH_VERSION)" >&2
  return 1 2>/dev/null || exit 1
fi

LOCK_FILE="${AW_STATE_DIR:-$HOME/.agentic-workflow}/.${SKILL_LOCK_NAME}.lock"
SKILL_LOCK_TIMEOUT="${SKILL_LOCK_TIMEOUT:-120}"

_lock_is_stale() {
  [ ! -f "$LOCK_FILE" ] && return 0
  local pid
  pid=$(cat "$LOCK_FILE" 2>/dev/null || echo "")
  [ -z "$pid" ] && return 0  # empty/truncated lock file — treat as stale
  # Check if PID is still running.
  # Known limitation (sec-4): kill -0 checks process existence, but PIDs can be
  # reused by the OS after a process exits. In theory, a new unrelated process
  # could have the same PID, causing us to treat a stale lock as still held.
  # In practice, PID reuse is rare enough on modern systems (PID space is large
  # and wraps infrequently) that this is acceptable for skill-level locking.
  if kill -0 "$pid" 2>/dev/null; then
    return 1  # not stale — process is alive
  fi
  return 0  # stale — process is gone
}

acquire_lock() {
  # sec-5: Explicitly check that lock directory creation succeeds.
  # When sourced, mkdir failure could be silently ignored without this guard.
  mkdir -p "$(dirname "$LOCK_FILE")" || {
    echo "skill-lock[$SKILL_LOCK_NAME]: failed to create lock directory $(dirname "$LOCK_FILE")" >&2
    return 1
  }

  local elapsed=0
  local backoff=1

  while true; do
    # sec-1: Attempt atomic O_EXCL write via noclobber — avoids TOCTOU race
    # where two processes could both see a stale lock and both write their PID.
    # The subshell ensures noclobber is scoped and does not leak into the caller.
    if ( set -o noclobber; echo "$SKILL_LOCK_OWNER" > "$LOCK_FILE" ) 2>/dev/null; then
      echo "skill-lock[$SKILL_LOCK_NAME]: acquired (owner pid $SKILL_LOCK_OWNER)"
      return 0
    fi

    # Re-acquire by the same session is not a wait.
    if [ "$(cat "$LOCK_FILE" 2>/dev/null || echo "")" = "$SKILL_LOCK_OWNER" ]; then
      echo "skill-lock[$SKILL_LOCK_NAME]: already held (owner pid $SKILL_LOCK_OWNER)"
      return 0
    fi

    # File exists — check if the holder is still alive
    if _lock_is_stale; then
      # Stale lock: remove and retry. Safe for concurrent waiters — if multiple
      # processes remove it simultaneously, the noclobber write below arbitrates;
      # only one winner proceeds, others loop back and wait.
      rm -f "$LOCK_FILE"
      continue
    fi

    # Check timeout
    if [ "$elapsed" -ge "$SKILL_LOCK_TIMEOUT" ]; then
      local holder
      holder=$(cat "$LOCK_FILE" 2>/dev/null || echo "unknown")
      echo "skill-lock[$SKILL_LOCK_NAME]: TIMEOUT after ${elapsed}s waiting for pid $holder"
      return 1
    fi

    local remaining=$((SKILL_LOCK_TIMEOUT - elapsed))
    local actual_sleep=$((backoff < remaining ? backoff : remaining))
    [ "$actual_sleep" -lt 1 ] && actual_sleep=1

    echo "skill-lock[$SKILL_LOCK_NAME]: waiting (held by pid $(cat "$LOCK_FILE" 2>/dev/null)), retry in ${actual_sleep}s..."
    sleep "$actual_sleep"
    elapsed=$((elapsed + actual_sleep))

    # Exponential backoff: 1, 2, 4, 8, 16, 30 (cap)
    backoff=$((backoff * 2))
    [ "$backoff" -gt 30 ] && backoff=30
  done
}

release_lock() {
  local tmp="${LOCK_FILE}.releasing.$$"
  if mv "$LOCK_FILE" "$tmp" 2>/dev/null; then
    local holder
    holder=$(cat "$tmp" 2>/dev/null || echo "")
    if [ "$holder" = "$SKILL_LOCK_OWNER" ]; then
      rm "$tmp"
      echo "skill-lock[$SKILL_LOCK_NAME]: released (owner pid $SKILL_LOCK_OWNER)"
    else
      mv "$tmp" "$LOCK_FILE" 2>/dev/null || true
      echo "skill-lock[$SKILL_LOCK_NAME]: not releasing — held by pid $holder, we are owner pid $SKILL_LOCK_OWNER" >&2
    fi
  fi
}
