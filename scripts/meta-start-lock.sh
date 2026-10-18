#!/usr/bin/env bash
# The team's start lock for lanes whose agent reports its own session id (sourced by meta-lane.sh; tested by
# scripts/tests/meta-launch.test.sh). The lock is a symlink, $start_lock, whose target is the owner's pid: `ln -s` is atomic and
# records the owner in one step, on macOS (no flock there) and Linux alike. A lock whose owner pid is dead is taken over, so a
# crashed start never leaves a stale one.
# Needs: start_lock (the symlink path), lane_pid (the owner), lane (a name for messages) and AW_META_ID_WAIT (optional).
# start_lock_hook <point> is a test seam, a no-op here: "checked" runs once a dead owner was seen, "gap" while a taker holds the
# lock's slot empty.
start_lock_hook() { :; }
release_start_lock() { [ "$(readlink "$start_lock" 2> /dev/null)" = "$lane_pid" ] && unlink "$start_lock" 2> /dev/null; return 0; }

# live_pid <value>: whether the lock's owner is a running process. Only a positive integer is a pid: 0 would signal the process
# group and -1 every process (kill -0 succeeds for both), and anything else names nobody, so those owners are stale.
live_pid() {
  case "$1" in
    '' | *[!0-9]* | 0*) return 1 ;;
  esac
  kill -0 "$1" 2> /dev/null
}

# take_over_dead <owner>: remove the lock whose owner was seen dead. Takers go through a second atomic guard (a directory,
# $start_lock.takeover) and re-read the owner inside it, so a lane that lost the race to another taker finds that taker's live
# lock and leaves it alone, and the lock is never moved aside (no gap where a live lock is absent). While a taker holds the
# guard, newcomers only `ln -s`, which fails for as long as the lock exists. The guard itself is held for a few commands; one
# whose owner is dead (a taker killed in them) is cleared after it was seen for a second.
take_over_dead() { # <owner seen dead>
  local guard="$start_lock.takeover" now
  if mkdir "$guard" 2> /dev/null; then
    printf '%s\n' "$lane_pid" > "$guard/owner" 2> /dev/null
    now="$(readlink "$start_lock" 2> /dev/null)"
    if [ -n "$now" ] && ! live_pid "$now"; then
      unlink "$start_lock" 2> /dev/null
      start_lock_hook gap
    fi
    rm -f "$guard/owner"; rmdir "$guard" 2> /dev/null
    return 0
  fi
  # the guard is held: its holder finishes in microseconds, so wait; clear it only when its owner is gone
  sleep 0.1
  local holder; holder="$(cat "$guard/owner" 2> /dev/null)"
  if [ "${guard_seen:-}" = "seen:$holder" ] && ! live_pid "$holder"; then
    rm -f "$guard/owner"; rmdir "$guard" 2> /dev/null
    guard_seen=""
  else
    guard_seen="seen:$holder"
  fi
  return 0
}

acquire_start_lock() { # returns 1 if the holder did not finish in AW_META_ID_WAIT x 10 s
  local deadline=$((SECONDS + ${AW_META_ID_WAIT:-30} * 10)) owner guard_seen=""
  while ! ln -s "$lane_pid" "$start_lock" 2> /dev/null; do
    owner="$(readlink "$start_lock" 2> /dev/null)"
    if [ -n "$owner" ] && ! live_pid "$owner"; then   # the holder died: take the lock over, one taker at a time
      start_lock_hook checked
      take_over_dead "$owner"
      continue
    fi
    if [ "$SECONDS" -ge "$deadline" ]; then
      echo "meta-lane: another lane's start did not finish; starting $lane anyway" >&2
      return 1
    fi
    sleep 0.2
  done
  return 0
}
