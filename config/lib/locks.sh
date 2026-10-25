#!/usr/bin/env bash
# config/lib/locks.sh — canonical mkdir-based mutexes for this repo. Owned by
# Plan 6 (UI evidence); Plan 4 sources this same file rather than defining its
# own lock primitives. Two callers care about this: a box-wide lock (a shared
# resource like the local web-app stack, used by many sessions on one box) and
# a per-session heavy-job lock (a real Playwright/build/test run).
#
# acquire_lock <dir> <timeout_s> / release_lock <dir>: one mutex (mkdir to take,
# rmdir to release, after removing its pid file). The QA-stack lock is one of these.
#
# acquire_any_lock <base_dir> <slots> <timeout_s>: a lock with <slots> slots.
# Slot 1 is <base_dir> itself, slot k is <base_dir>.k (k = 2..slots), each its own
# mkdir mutex. It takes the first free slot and prints that slot's dir on stdout:
#   slot="$(acquire_any_lock "$lock" "${AW_HEAVY_SLOTS:-2}" 600)" || exit 75
#   ...; release_lock "$slot"
# A holder that knows only <base_dir> (acquire_lock, sindri's TS lock on an older
# build) holds slot 1, so the holders never outnumber the slots. The box-wide
# heavy-job lock (AW_HEAVY_JOB_LOCK, AW_HEAVY_SLOTS, default 2) is one of these.
#
# A holder records its pid in <dir>/pid: $$, the shell that called acquire, which is
# the one that releases (BASHPID would be the short-lived subshell of
# slot="$(acquire_any_lock ...)"). A waiter that finds a pid file naming a dead process
# (SIGKILL, a cleared session) reclaims the lock. A dir with no pid file (an older
# holder, sindri's TS lock) is never reclaimed, so a waiter that sees the dir between
# the holder's mkdir and its pid write leaves it alone.

# _lock_pid_alive <pid>: 0 (alive) unless the pid is a number whose process is gone.
_lock_pid_alive() {
  case "$1" in ''|*[!0-9]*) return 0 ;; esac
  kill -0 "$1" 2>/dev/null && return 0
  [ -d "/proc/$1" ] && return 0
  # No /proc (macOS): kill -0 also fails for another user's live process.
  if [ ! -d /proc/self ] && ps -p "$1" >/dev/null 2>&1; then return 0; fi
  return 1
}

# _lock_take <dir>: mkdir the mutex and record this shell's pid in it.
_lock_take() {
  mkdir "$1" 2>/dev/null || return 1
  printf '%s\n' "$$" > "$1/pid" 2>/dev/null || true
  return 0
}

# _lock_reclaim_dead <dir>: free <dir> when its pid file names a dead process. The pid
# file is moved aside first (only one waiter's mv wins); if what was moved is not the
# dead pid read, the dir changed hands in between and the file goes back.
_lock_reclaim_dead() {
  local dir="$1" pid moved
  [ -f "$dir/pid" ] || return 1
  pid="$(cat "$dir/pid" 2>/dev/null)" || return 1
  _lock_pid_alive "$pid" && return 1
  moved="$dir/pid.reclaim.$$"
  mv "$dir/pid" "$moved" 2>/dev/null || return 1
  if [ "$(cat "$moved" 2>/dev/null)" != "$pid" ]; then
    mv "$moved" "$dir/pid" 2>/dev/null || true
    return 1
  fi
  rm -f "$moved"
  rmdir "$dir" 2>/dev/null
}

acquire_lock() {
  local lock_dir="$1" timeout_s="$2" waited=0 printed=0
  while ! _lock_take "$lock_dir"; do
    _lock_reclaim_dead "$lock_dir" && continue
    if [ "$printed" -eq 0 ]; then
      echo "waiting on lock: $lock_dir" >&2
      printed=1
    fi
    [ "$waited" -ge "$timeout_s" ] && return 1
    sleep 1
    waited=$((waited + 1))
  done
  return 0
}

release_lock() {
  local lock_dir="$1"
  rm -f "$lock_dir/pid" 2>/dev/null
  rmdir "$lock_dir" 2>/dev/null || true
}

acquire_any_lock() {
  local base="$1" slots="$2" timeout_s="$3" waited=0 printed=0 k dir
  case "$slots" in ''|*[!0-9]*) slots=1 ;; esac
  [ "$slots" -ge 1 ] || slots=1
  while :; do
    k=1
    while [ "$k" -le "$slots" ]; do
      if [ "$k" -eq 1 ]; then dir="$base"; else dir="$base.$k"; fi
      if _lock_take "$dir" || { _lock_reclaim_dead "$dir" && _lock_take "$dir"; }; then
        printf '%s\n' "$dir"
        return 0
      fi
      k=$((k + 1))
    done
    if [ "$printed" -eq 0 ]; then
      echo "waiting on lock: $base (all $slots slots busy)" >&2
      printed=1
    fi
    [ "$waited" -ge "$timeout_s" ] && return 1
    sleep 1
    waited=$((waited + 1))
  done
}
