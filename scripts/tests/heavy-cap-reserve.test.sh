#!/usr/bin/env bash
# Every heavy-slot taker counts slots by heavy-slots-cap as it reads NOW, and every non-meta taker honours
# heavy-meta-reserve (#2061). Run: bash scripts/tests/heavy-cap-reserve.test.sh
# config/lib/locks.sh acquire_any_lock counts slots from its argument and never reads either file; scripts/heavy.sh reads
# the cap once per job. Scratch state and lock dirs only, never the box's live ones.
set -uo pipefail
unset TMUX
for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY|META_LANE)'); do unset "$v"; done   # this suite may itself run under a heavy job
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

# scratch <case> <cap> <reserve>: a fresh state dir with those files ("" = no file); sets T and BASE.
scratch() {
  T="$WORK/$1"; BASE="$T/locks/heavy-job.lock"
  mkdir -p "$T/locks"
  [ -z "$2" ] || echo "$2" > "$T/heavy-slots-cap"
  [ -z "$3" ] || echo "$3" > "$T/heavy-meta-reserve"
}
hold() { mkdir -p "$1"; echo "$live" > "$1/pid"; }

# lockssh <base> <slots-arg> <timeout> [<extra env>...]: the slot acquire_any_lock hands out ("slot[1]", "slot[.2]"), or "none".
lockssh() {
  local base="$1" slots="$2" tmo="$3"; shift 3
  local got
  got="$(timeout 10 env AW_STATE_DIR="$T" AW_HEAVY_JOB_LOCK="$BASE" "$@" bash -c \
    '. "$1"; s="$(acquire_any_lock "$2" "$3" "$4")" || exit 75; echo "${s#"$2"}"; release_lock "$s"' _ "$LOCKS" "$base" "$slots" "$tmo" 2>/dev/null)" \
    && echo "slot[${got:-1}]" || echo none
}

scratch cap 1 ""; hold "$BASE"
check "locks.sh: cap 1 with slot 1 held takes no slot 2 though the argument says 2" "$(lockssh "$BASE" 2 2)" "none"

scratch reserve 2 1
check "locks.sh: reserve 1, not meta, skips slot 1 and takes slot 2" "$(lockssh "$BASE" 2 2)" "slot[.2]"

scratch meta 2 1
check "locks.sh: reserve 1, AW_HEAVY_PRIORITY=meta, takes slot 1" "$(lockssh "$BASE" 2 2 AW_HEAVY_PRIORITY=meta)" "slot[1]"

scratch other 1 1; QA="$T/locks/qa-stack.lock"; hold "$QA"
check "locks.sh: another lock ignores the cap and the reserve" "$(lockssh "$QA" 2 2)" "slot[.2]"

# Wrapper: cap 2 with both slots held; while it waits the cap drops to 1 and slot 2 is freed. It must keep waiting.
scratch wrapper 2 ""; hold "$BASE"; hold "$BASE.2"
( sleep 1; echo 1 > "$T/heavy-slots-cap"; rm -r "$BASE.2" ) & freer=$!
rc=0
timeout 5 env AW_STATE_DIR="$T" AW_HEAVY_JOB_LOCK="$BASE" AW_HEAVY_SESSION_PID="$live" AW_HEAVY_RUN_MAX=0 AW_HEAVY_MIN_FREE_MB=0 \
  bash "$HEAVY" --expensive true >/dev/null 2>&1 || rc=$?
wait "$freer"
check "wrapper: a cap lowered to 1 while waiting keeps a job off slot 2 (exit 124 = still waiting)" "$rc" "124"

# Box case: a real wrapper job holds slot 1 (cap 2); the cap is then lowered to 1; a locks.sh taker must not get slot 2.
scratch box 2 ""
timeout 12 env AW_STATE_DIR="$T" AW_HEAVY_JOB_LOCK="$BASE" AW_HEAVY_SESSION_PID="$live" AW_HEAVY_RUN_MAX=0 AW_HEAVY_MIN_FREE_MB=0 \
  bash "$HEAVY" --expensive sleep 5 >/dev/null 2>&1 & wrap=$!
for _ in $(seq 1 40); do [ -f "$BASE/owner" ] && break; sleep 0.1; done
echo 1 > "$T/heavy-slots-cap"
check "box: wrapper holds slot 1, cap lowered to 1, a locks.sh taker gets no slot 2" "$(lockssh "$BASE" 2 2)" "none"
wait "$wrap"

exit "$fail"
