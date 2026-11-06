#!/usr/bin/env bash
# Leases on the shared proof-main build. Each proof.sh run holds one, named by its <n> folder and
# holding the time it was last renewed. A lease is live until released or older than the TTL.
#
#   lease.sh take <n>      Creates or renews the lease.
#   lease.sh release <n>   Removes the lease; prints how many live leases remain.
#   lease.sh others <n>    Prints how many live leases belong to other runs.
# Sourced, it gives main-worktree.sh the same functions to use while holding the lock.
set -u

LEASE_STATE_DIR="${UI_BEFORE_AFTER_STATE_DIR:-$HOME/.agentic-workflow/vitalizecare-web-app/ui-before-after/.stacks}"
LEASE_DIR="$LEASE_STATE_DIR/proof-main.leases"
LEASE_LOCK="$LEASE_DIR.lock"
LEASE_TTL_S="${UI_BEFORE_AFTER_LEASE_TTL_S:-7200}"
LEASE_LOCK_WAIT_S="${UI_BEFORE_AFTER_LEASE_LOCK_WAIT_S:-30}"

# A lock whose holder died is taken over.
lease_lock() {
  local waited=0
  mkdir -p "$LEASE_STATE_DIR"
  while ! mkdir "$LEASE_LOCK" 2>/dev/null; do
    if ! kill -0 "$(cat "$LEASE_LOCK/pid" 2>/dev/null)" 2>/dev/null; then
      rm -rf "$LEASE_LOCK"
      continue
    fi
    if [ "$waited" -ge "$LEASE_LOCK_WAIT_S" ]; then
      echo "proof-main lease lock still held after ${LEASE_LOCK_WAIT_S}s" >&2
      exit 5
    fi
    sleep 1
    waited=$((waited + 1))
  done
  echo "$$" >"$LEASE_LOCK/pid"
}

lease_unlock() {
  rm -rf "$LEASE_LOCK"
}

lease_take() {
  mkdir -p "$LEASE_DIR"
  date +%s >"$LEASE_DIR/$1"
}

lease_release() {
  rm -f "$LEASE_DIR/$1"
}

# Counts live leases other than <n>'s and deletes expired ones.
lease_others() {
  local lease stamp count=0 now
  now=$(date +%s)
  for lease in "$LEASE_DIR"/*; do
    [ -f "$lease" ] || continue
    stamp=$(cat "$lease")
    if [ $((now - ${stamp:-0})) -ge "$LEASE_TTL_S" ]; then
      rm -f "$lease"
    elif [ "$(basename "$lease")" != "$1" ]; then
      count=$((count + 1))
    fi
  done
  echo "$count"
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  usage="usage: lease.sh take|release|others <n>"
  [ $# -eq 2 ] || { echo "$usage" >&2; exit 64; }
  case "$1" in take | release | others) ;; *) echo "$usage" >&2; exit 64 ;; esac
  lease_lock
  case "$1" in
    take) lease_take "$2" ;;
    release) lease_release "$2"; lease_others "$2" ;;
    others) lease_others "$2" ;;
  esac
  lease_unlock
fi
