#!/usr/bin/env bash
# NOTE: sourcing this file enables set -euo pipefail in the caller's shell.
# Callers must tolerate nounset (-u) and pipefail after sourcing.
# In particular, all referenced variables must be set or provide a default.
#
# Works on bash 3.2 (macOS /bin/bash): no bash 4 features, no GNU-only tools.
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

# Atomic create that works on bash 3.2 and BSD/macOS (noclobber is not O_EXCL there): write the pid to a private
# temp file, then ln it to LOCK_FILE. ln fails when LOCK_FILE exists, and the lock is never visible half-written.
_lock_try_create() {
  local tmp="${LOCK_FILE}.new.$$"
  echo "$SKILL_LOCK_OWNER" > "$tmp" 2>/dev/null || return 1
  if ln "$tmp" "$LOCK_FILE" 2>/dev/null; then rm -f "$tmp"; return 0; fi
  rm -f "$tmp"
  return 1
}

# Remove a dead holder's lock, one waiter at a time (#2569). Two waiters can both read the dead pid; without a guard,
# the late one's remove deletes the lock the other has just taken, and both hold it. The guard is a mkdir mutex next to
# the lock (only one mkdir wins): under it the lock is read again and removed only when its holder is still dead, so a
# fresh lock (a live pid) is never removed. Returns 1 when another waiter holds the guard.
# Every waiter, the mkdir winner too, announces first: it creates an empty file named by its pid ($$, the shell running
# acquire_lock) in the guard, then reads EVERY entry. Any other pid file whose process is live means someone else holds or
# is taking the guard: it removes its own file and leaves. Each announces before it scans, so of two waiters that run at
# once the later scanner always sees the earlier one's file. It enters only when every other pid file is dead (or there is
# none and it won the mkdir); it removes one dead pid file with `rm` (only the waiter whose `rm` succeeds enters) and the
# other dead ones too. A waiter with no other file leaves: it can't tell that from a winner that has not announced yet.
# Deciding from the first entry in glob order missed a live holder's file behind a dead one (#2616, as #2613).
# Every exit removes the waiter's own pid file and then tries `rmdir` on the guard: the last waiter out removes it, and
# `rmdir` of a guard that still holds a file fails harmlessly. So an empty guard means nobody is announced in it.
# An empty guard is a holder between its mkdir and its pid file, and is cleared only once over two minutes old
# (`find -mmin +1`; a reclaim takes milliseconds). A waiter that finds the guard empty checks its age BEFORE it writes its
# pid file: the write would touch the guard and keep it young forever. Every cleanup rmdir goes through
# _lock_guard_rmdir_if_old, so a fresh guard another waiter just made is never removed (#2581).
_lock_guard_rmdir_if_old() {
  [ -n "$(find "$1" -maxdepth 0 -mmin +1 2>/dev/null)" ] && rmdir "$1" 2>/dev/null
  return 0
}

# A random 0.05-0.30 s sleep, so waiters that announce together do not back off in lockstep.
_lock_guard_nap() {
  local n=$((RANDOM % 26 + 5))
  sleep "0.$(printf '%02d' "$n")"
}

_lock_reclaim() {
  local guard="${LOCK_FILE}.reclaim" f p dead="" winner=1
  if ! mkdir "$guard" 2>/dev/null; then
    winner=0; _lock_guard_nap
    # An empty guard: announcing would touch it and keep it young. Check its age, don't write.
    if [ -z "$(find "$guard" -mindepth 1 -maxdepth 1 2>/dev/null | head -n 1)" ]; then
      _lock_guard_rmdir_if_old "$guard"; return 1
    fi
  fi
  : > "$guard/$$" 2>/dev/null || return 1
  for f in "$guard"/*; do
    [ -e "$f" ] || continue
    p="${f##*/}"
    case "$p" in
      ""|*[!0-9]*) rm -f "$f" ;;
      "$$") ;;
      *)
        if kill -0 "$p" 2>/dev/null; then rm -f "$guard/$$"; rmdir "$guard" 2>/dev/null || true; return 1; fi
        if [ -z "$dead" ]; then dead="$f"; else rm -f "$f"; fi
        ;;
    esac
  done
  if [ -n "$dead" ]; then
    rm "$dead" 2>/dev/null || { rm -f "$guard/$$"; rmdir "$guard" 2>/dev/null || true; return 1; }
  elif [ "$winner" = 0 ]; then
    rm -f "$guard/$$"; rmdir "$guard" 2>/dev/null || true
    return 1
  fi
  if _lock_is_stale; then rm -f "$LOCK_FILE"; fi
  rm -f "$guard/$$"; rmdir "$guard" 2>/dev/null || true
  return 0
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
    # sec-1: Attempt an atomic create (hard link, see _lock_try_create) — avoids the TOCTOU race
    # where two processes could both see a stale lock and both write their PID.
    if _lock_try_create; then
      echo "skill-lock[$SKILL_LOCK_NAME]: acquired (owner pid $SKILL_LOCK_OWNER)"
      return 0
    fi

    # Re-acquire by the same session is not a wait.
    if [ "$(cat "$LOCK_FILE" 2>/dev/null || echo "")" = "$SKILL_LOCK_OWNER" ]; then
      echo "skill-lock[$SKILL_LOCK_NAME]: already held (owner pid $SKILL_LOCK_OWNER)"
      return 0
    fi

    # File exists — check if the holder is still alive. A dead holder's lock is removed
    # under the reclaim guard only; a waiter that loses the guard waits like any other.
    if _lock_is_stale && _lock_reclaim; then
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

# Release only a lock we own, and never write the lock path as a non-owner (#2581). The holder is read first: a
# non-owner touches nothing. An owner moves the lock aside to a private name and reads it again; if it is still ours it is
# removed. If it changed in between, it goes back with ln, which fails when a newer lock exists (then the private copy is
# dropped). mv is never used to put it back, because mv would replace a lock a waiter just took.
release_lock() {
  local holder tmp="${LOCK_FILE}.releasing.$$"
  holder=$(cat "$LOCK_FILE" 2>/dev/null || echo "")
  if [ "$holder" != "$SKILL_LOCK_OWNER" ]; then
    [ -n "$holder" ] && echo "skill-lock[$SKILL_LOCK_NAME]: not releasing — held by pid $holder, we are owner pid $SKILL_LOCK_OWNER" >&2
    return 0
  fi
  mv "$LOCK_FILE" "$tmp" 2>/dev/null || return 0
  holder=$(cat "$tmp" 2>/dev/null || echo "")
  if [ "$holder" = "$SKILL_LOCK_OWNER" ]; then
    rm -f "$tmp"
    echo "skill-lock[$SKILL_LOCK_NAME]: released (owner pid $SKILL_LOCK_OWNER)"
  else
    ln "$tmp" "$LOCK_FILE" 2>/dev/null || true
    rm -f "$tmp"
    echo "skill-lock[$SKILL_LOCK_NAME]: not releasing — held by pid $holder, we are owner pid $SKILL_LOCK_OWNER" >&2
  fi
}
