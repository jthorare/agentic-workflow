#!/usr/bin/env bash
# A slot whose holder was SIGKILLed is freed by the next waiter, whichever taker wrote it (#2059).
# Run: bash scripts/tests/heavy-dead-holder.test.sh
# Two leftovers: a dead config/lib/locks.sh holder's <slot>/pid (the wrapper read only <slot>/owner), and an empty folder
# from a holder killed between its mkdir and its record write (nobody freed it). The wrapper (scripts/heavy.sh) and
# locks.sh acquire_any_lock free both; an empty folder only once it is over a minute old (younger is a holder mid-write),
# and a record naming a live pid never. Scratch lock dirs only, never the box's live ones.
set -uo pipefail
unset TMUX
for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY)'); do unset "$v"; done   # this suite may itself run under a heavy job
unset DOTNET_PROCESSOR_COUNT MSBUILDDISABLENODEREUSE
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

# slot <case> [<file> <text>] [<age>]: a scratch slot holding that record file (none: an empty folder), its folder
# <age> old (touch -d); sets T and SLOT.
slot() {
  T="$WORK/$1"; SLOT="$T/state/locks/heavy-job.lock"
  mkdir -p "$SLOT"
  [ -z "$2" ] || printf '%s\n' "$3" > "$SLOT/$2"
  [ -z "${4:-}" ] || touch -d "$4" "$SLOT"
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
  slot "$taker-pid" pid "$(dead_pid)"
  check "$taker frees a dead locks.sh holder's pid file" "$($taker) $(ls -A "$SLOT" 2>/dev/null)" "taken "
  slot "$taker-owner" owner "$(dead_pid) 2026-11-04T00:00:00Z session=1 dead job"
  check "$taker frees a dead wrapper holder's owner file" "$($taker) $(ls -A "$SLOT" 2>/dev/null)" "taken "
  slot "$taker-empty" "" "" '2 minutes ago'
  check "$taker frees an empty folder over a minute old" "$($taker) $(ls -A "$SLOT" 2>/dev/null)" "taken "
  slot "$taker-young" ""
  check "$taker keeps a young empty folder (a holder mid-write)" "$($taker) $([ -d "$SLOT" ] && echo there)" "kept there"
  slot "$taker-livepid" pid "$live" '2 minutes ago'
  check "$taker keeps a slot whose pid file names a live process" "$($taker) $(ls -A "$SLOT")" "kept pid"
done

exit "$fail"
