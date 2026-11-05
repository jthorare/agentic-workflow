#!/usr/bin/env bash
# Which leftover reclaim files each bash taker frees (#2065). Run: bash scripts/tests/heavy-reclaim-leftovers.test.sh
# A slot holding only a <owner|pid>.reclaim.<N>... file, from a reclaimer that stopped between its move and its delete:
# the wrapper (scripts/heavy.sh) and config/lib/locks.sh acquire_any_lock free it when N is dead, or whoever N is once the
# folder is over 6 h old, and leave a young one with a live N alone. Scratch lock dirs only, never the box's live ones.
set -uo pipefail
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HEAVY="$DIR/../heavy.sh"
LOCKS="$DIR/../../config/lib/locks.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
sleep 300 & live=$!
cleanup() { kill "$live" 2>/dev/null; rm -rf -- "$WORK"; }
trap cleanup EXIT

dead_pid() { bash -c 'exit 0' & local p=$!; wait "$p"; echo "$p"; }

# slot <case> <leftover name> [aged]: a scratch slot holding only that leftover; sets T and SLOT.
slot() {
  T="$WORK/$1"; SLOT="$T/state/locks/heavy-job.lock"
  mkdir -p "$SLOT"; echo "$(dead_pid) 2026-11-04T00:00:00Z session=1 dead job" > "$SLOT/$2"
  [ -z "${3:-}" ] || touch -d '7 hours ago' "$SLOT"
}

# wrapper / lockssh: try to take the slot within 6 s; prints "taken" (the job ran) or "kept" (still waiting).
wrapper() {
  timeout 6 env AW_STATE_DIR="$T/state" AW_HEAVY_JOB_LOCK="$SLOT" AW_HEAVY_SLOTS=1 AW_HEAVY_SESSION_PID="$live" \
    AW_HEAVY_RUN_MAX=0 bash "$HEAVY" --expensive true >/dev/null 2>&1 && echo taken || echo kept
}
lockssh() {
  timeout 6 bash -c '. "$1"; slot="$(acquire_any_lock "$2" 1 4)" || exit 75; release_lock "$slot"' _ "$LOCKS" "$SLOT" \
    >/dev/null 2>&1 && echo taken || echo kept
}

for taker in wrapper lockssh; do
  slot "$taker-owner" "owner.reclaim.$(dead_pid)"
  check "$taker frees a wrapper's owner.reclaim.<dead>" "$($taker) $(ls -A "$SLOT" 2>/dev/null)" "taken "
  slot "$taker-pid" "pid.reclaim.$(dead_pid)"
  check "$taker frees locks.sh's pid.reclaim.<dead>" "$($taker) $(ls -A "$SLOT" 2>/dev/null)" "taken "
  slot "$taker-cs" "owner.reclaim.$(dead_pid).abcdefgh.xyz"
  check "$taker frees Sindri's owner.reclaim.<dead>.<random>" "$($taker) $(ls -A "$SLOT" 2>/dev/null)" "taken "
  slot "$taker-live" "owner.reclaim.$live.abcdefgh.xyz"
  check "$taker keeps a young slot whose reclaimer is alive" "$($taker) $(ls -A "$SLOT")" "kept owner.reclaim.$live.abcdefgh.xyz"
  slot "$taker-aged" "owner.reclaim.$live.abcdefgh.xyz" aged
  check "$taker frees a 7 h old slot whatever its reclaimer" "$($taker) $(ls -A "$SLOT" 2>/dev/null)" "taken "
done

exit "$fail"
