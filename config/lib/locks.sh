#!/usr/bin/env bash
# config/lib/locks.sh — canonical mkdir-based mutexes for this repo. Owned by
# Plan 6 (UI evidence); Plan 4 sources this same file rather than defining its
# own lock primitives. Two callers care about this: a box-wide lock (a shared
# resource like the local web-app stack, used by many sessions on one box) and
# a per-session heavy-job lock (a real Playwright/build/test run).
#
# acquire_lock <dir> <timeout_s> / release_lock <dir>: one mutex (mkdir to take,
# rmdir to release). The QA-stack lock is one of these.
#
# acquire_any_lock <base_dir> <slots> <timeout_s>: a lock with <slots> slots.
# Slot 1 is <base_dir> itself, slot k is <base_dir>.k (k = 2..slots), each its own
# mkdir mutex. It takes the first free slot and prints that slot's dir on stdout:
#   slot="$(acquire_any_lock "$lock" "${AW_HEAVY_SLOTS:-2}" 600)" || exit 75
#   ...; release_lock "$slot"
# A holder that knows only <base_dir> (acquire_lock, sindri's TS lock on an older
# build) holds slot 1, so the holders never outnumber the slots. The box-wide
# heavy-job lock (AW_HEAVY_JOB_LOCK, AW_HEAVY_SLOTS, default 2) is one of these.

acquire_lock() {
  local lock_dir="$1" timeout_s="$2" waited=0 printed=0
  while ! mkdir "$lock_dir" 2>/dev/null; do
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
      if mkdir "$dir" 2>/dev/null; then
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
