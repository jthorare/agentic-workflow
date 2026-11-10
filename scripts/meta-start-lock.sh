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
# guard, newcomers only `ln -s`, which fails for as long as the lock exists.
# The guard holds one empty file named by its holder's pid ($lane_pid). A guard whose holder died (a taker killed inside it) is
# taken over by the one waiter whose `rm` of the dead pid file succeeds; that waiter's own pid file is already in the guard, so
# the guard is never empty and no one can rmdir it and mkdir a fresh one under it. An empty guard is a holder between its mkdir
# and its pid file, and is cleared only once over a minute old (_start_guard_rmdir_if_old), never because it looked empty
# twice: only a holder removes its own guard, after its own pid file (#2598).
_start_guard_rmdir_if_old() {
  [ -n "$(find "$1" -maxdepth 0 -mmin +1 2> /dev/null)" ] && rmdir "$1" 2> /dev/null
  return 0
}

# _start_guard_body <guard>: the work of a guard holder (its pid file is in the guard): unlink the lock if its owner is still
# dead, then remove the pid file and the guard.
_start_guard_body() {
  local now
  now="$(readlink "$start_lock" 2> /dev/null)"
  if [ -n "$now" ] && ! live_pid "$now"; then
    unlink "$start_lock" 2> /dev/null
    start_lock_hook gap
  fi
  rm -f "$1/$lane_pid"; rmdir "$1" 2> /dev/null
  return 0
}

take_over_dead() { # <owner seen dead>
  local guard="$start_lock.takeover" f p
  if mkdir "$guard" 2> /dev/null; then
    : > "$guard/$lane_pid" 2> /dev/null
    _start_guard_body "$guard"
    return 0
  fi
  # the guard is held: its holder finishes in microseconds, so wait, unless its holder is dead
  sleep 0.1
  for f in "$guard"/*; do
    [ -e "$f" ] || { _start_guard_rmdir_if_old "$guard"; return 0; }
    p="${f##*/}"
    case "$p" in
      '' | *[!0-9]*) rm -f "$f" ;;   # not a pid file (an older install's `owner`): nobody holds the guard through it
      *)
        live_pid "$p" && return 0
        : > "$guard/$lane_pid" 2> /dev/null || return 0
        if rm "$f" 2> /dev/null; then
          _start_guard_body "$guard"
        else
          rm -f "$guard/$lane_pid"
        fi
        return 0
        ;;
    esac
  done
  _start_guard_rmdir_if_old "$guard"
  return 0
}

acquire_start_lock() { # returns 1 if the holder did not finish in AW_META_ID_WAIT x 10 s
  local deadline=$((SECONDS + ${AW_META_ID_WAIT:-30} * 10)) owner
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
