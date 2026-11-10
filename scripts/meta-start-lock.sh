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
# Every taker (the mkdir winner too) announces first: it creates an empty file named by its pid ($lane_pid) in the guard, then
# reads EVERY entry. Any other pid file whose process is live means someone else holds or is taking the guard: the taker
# removes its own file and leaves. Because each taker announces before it scans, of two takers that run at once the later scanner
# always sees the earlier one's file, so they never both enter. A taker enters the body only when every other pid file is dead
# (or there is none and it won the mkdir); it removes one dead pid file with `rm` (only the taker whose `rm` succeeds
# enters) and the other dead ones too. A waiter with no other file leaves: it can't tell that from a winner that has not
# announced yet. Deciding from the first entry in glob order missed a live holder's file behind a dead one (#2613, after #2598). The winner
# of the mkdir has no file for a moment, so a waiter can scan the guard without seeing it; the winner's own scan after its
# announce sees that waiter and backs off. An empty guard is a holder between its mkdir and its pid file, and is cleared only
# once over a minute old (_start_guard_rmdir_if_old), never because it looked empty twice.
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

# _start_guard_nap: a random 0.05-0.30 s sleep, so two waiters that announce together do not back off in lockstep.
_start_guard_nap() {
  local n=$((RANDOM % 26 + 5))
  sleep "0.$(printf '%02d' "$n")"
}

take_over_dead() { # <owner seen dead>
  local guard="$start_lock.takeover" f p dead="" winner=1
  # a held guard is held by a taker that finishes in microseconds, or by a dead one: back off a little, then announce and scan
  mkdir "$guard" 2> /dev/null || { winner=0; _start_guard_nap; }
  : > "$guard/$lane_pid" 2> /dev/null || return 0
  for f in "$guard"/*; do
    p="${f##*/}"
    case "$p" in
      '' | *[!0-9]*) rm -f "$f" ;;   # not a pid file (an older install's `owner`): nobody holds the guard through it
      "$lane_pid") ;;
      *)
        if live_pid "$p"; then rm -f "$guard/$lane_pid"; return 0; fi
        if [ -z "$dead" ]; then dead="$f"; else rm -f "$f"; fi
        ;;
    esac
  done
  if [ -n "$dead" ]; then
    rm "$dead" 2> /dev/null || { rm -f "$guard/$lane_pid"; return 0; }   # another taker took that dead file
  elif [ "$winner" = 0 ]; then
    # only our own file: indistinguishable from the empty guard of a winner that has not announced yet, which scans after us
    # and would see us live. Leave; the guard is cleared once it is over a minute old.
    rm -f "$guard/$lane_pid"
    _start_guard_rmdir_if_old "$guard"
    return 0
  fi
  _start_guard_body "$guard"
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
