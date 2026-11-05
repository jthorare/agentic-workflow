#!/usr/bin/env bash
# A meta reserve never shuts lane jobs out: every reader caps heavy-meta-reserve at the slot count minus one (#2060, owner
# 2026-11-05), so a non-meta taker always has a slot. Run: bash scripts/tests/heavy-full-reserve.test.sh
# config/lib/heavy-config.sh heavy_reserve and config/lib/locks.sh acquire_any_lock; the wrapper's rows are in heavy.test.sh.
# Scratch state and lock dirs only, never the box's live ones.
set -uo pipefail
unset TMUX
for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY|META_LANE)'); do unset "$v"; done   # this suite may itself run under a heavy job
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="$DIR/../../config/lib/heavy-config.sh"
LOCKS="$DIR/../../config/lib/locks.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf -- "$WORK"' EXIT

# scratch <case> <cap> <reserve>: a fresh state dir with those files; sets T and BASE.
scratch() {
  T="$WORK/$1"; BASE="$T/locks/heavy-job.lock"
  mkdir -p "$T/locks"
  echo "$2" > "$T/heavy-slots-cap"
  echo "$3" > "$T/heavy-meta-reserve"
}

# reserve_of <reserve file> <slots>: what heavy_reserve reads.
reserve_of() { scratch "r-$1-$2" "$2" "$1"; AW_STATE_DIR="$T" bash -c '. "$1"; heavy_reserve "$2"' _ "$CONFIG" "$2"; }

check "heavy_reserve: 1 of 3 stays 1" "$(reserve_of 1 3)" "1"
check "heavy_reserve: 2 of 3 stays 2" "$(reserve_of 2 3)" "2"
check "heavy_reserve: 3 of 3 is capped to 2" "$(reserve_of 3 3)" "2"
check "heavy_reserve: 9 of 3 is capped to 2" "$(reserve_of 9 3)" "2"
check "heavy_reserve: 1 of 1 is capped to 0" "$(reserve_of 1 1)" "0"
check "heavy_reserve: 0 of 1 stays 0" "$(reserve_of 0 1)" "0"

# lockssh [<env>...]: the slot a non-meta (or the given env's) acquire_any_lock takes with both slot args 2, timeout 2, or
# "none"; bounded from outside at 5 s, since a wait blocked only by the reserve never counts toward the timeout.
lockssh() {
  local got
  got="$(timeout 5 env AW_STATE_DIR="$T" AW_HEAVY_JOB_LOCK="$BASE" "$@" bash -c \
    '. "$1"; s="$(acquire_any_lock "$2" 2 2)" || exit 75; echo "${s#"$2"}"; release_lock "$s"' _ "$LOCKS" "$BASE" 2>/dev/null)" \
    && echo "slot[${got:-1}]" || echo none
}

scratch full 2 2
check "locks.sh: reserve 2 of 2 is capped to 1, a lane job takes slot 2" "$(lockssh)" "slot[.2]"
scratch over 2 9
check "locks.sh: reserve 9 of 2 is capped to 1, a lane job takes slot 2" "$(lockssh)" "slot[.2]"
scratch one 1 1
check "locks.sh: reserve 1 of 1 is capped to 0, a lane job takes slot 1" "$(lockssh)" "slot[1]"
scratch meta 2 2
check "locks.sh: reserve 2 of 2, a meta job still takes slot 1" "$(lockssh AW_HEAVY_PRIORITY=meta)" "slot[1]"

exit "$fail"
