#!/usr/bin/env bash
# Regression for #2799: scripts/heavy.sh's watchdog sends TERM to "-$job" (the job's process group) before setsid (Linux) or
# perl (macOS fallback) has made that group, so a kill decided on the first pass (a dead session) misses and the job runs its
# full time. Each case runs `heavy --light sleep 30.<n>` under a dead session pid and expects: exit 124, the reason line, a
# return within 10 s, no surviving job, locks released. Cases: a setsid that sleeps 1 s before it execs the real one, a perl
# that does the same with setsid, timeout and gtimeout off PATH, and a control with no stub (the kill works there today).
# Run: bash scripts/tests/heavy-pgid-race.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HEAVY="$DIR/heavy.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
# Jobs by pid: any sleep this suite started that is still alive is killed here, by recorded pid only.
alive_jobs() { ps -eo pid=,args= 2> /dev/null | awk -v a="sleep $1" '{ p=$1; sub(/^[ ]*[0-9]+[ ]+/, ""); if ($0 == a) print p }'; }
cleanup() { local n p; for n in 30.101 30.102 30.103; do for p in $(alive_jobs "$n"); do kill "$p" 2> /dev/null; done; done; find "$WORK" -delete 2> /dev/null; }
trap cleanup EXIT

for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY)'); do unset "$v"; done
unset DOTNET_PROCESSOR_COUNT MSBUILDDISABLENODEREUSE
DEAD=99999999
REAL_SETSID="$(command -v setsid || true)"
REAL_PERL="$(command -v perl || true)"
ORIG_PATH="$PATH"

# run_case <name> <sleep arg> <extra PATH dir or empty> <whole PATH or empty>
run_case() {
  local name="$1" n="$2" stubdir="$3" fullpath="$4" state="$WORK/state-$2" out rc t0 left
  mkdir -p "$state"
  t0=$SECONDS
  out="$WORK/out-$n"
  AW_STATE_DIR="$state" AW_HEAVY_SESSION_PID="$DEAD" AW_HEAVY_TEST_PATH="$stubdir" PATH="${fullpath:-$ORIG_PATH}" \
    bash "$HEAVY" --light sleep "$n" > "$out" 2>&1
  rc=$?
  check "$name: exit 124" "$rc" "124"
  check "$name: the reason line" "$(grep -c "^heavy: session $DEAD ended, killed: sleep $n\$" "$out" | tr -d ' ')" "1"
  check "$name: the wrapper returns within 10 s" "$([ $((SECONDS - t0)) -lt 10 ] && echo fast || echo slow)" "fast"
  left="$(alive_jobs "$n" | tr '\n' ' ')"
  check "$name: the job is not left running" "${left:+alive}" ""
  for p in $left; do kill "$p" 2> /dev/null; done
  check "$name: locks released" "$(find "$state" -name '*.lock*' -type d 2> /dev/null | wc -l | tr -d ' ')" "0"
}

# Case 3 first: the control, no stub.
run_case "control (no stub)" 30.103 "" ""

if [ -n "$REAL_SETSID" ]; then
  S1="$WORK/slow-setsid"; mkdir -p "$S1"
  printf '#!/bin/sh\nsleep 1\nexec "%s" "$@"\n' "$REAL_SETSID" > "$S1/setsid"; chmod +x "$S1/setsid"
  run_case "slow setsid" 30.101 "$S1" ""
else echo "skip - no setsid on this box"; fi

# Case 2: every tool but setsid, timeout and gtimeout, with perl replaced by a slow one (as ci-runner-parity.test.sh part 3).
if [ -n "$REAL_PERL" ]; then
  NOTOOLS="$WORK/notools"; mkdir -p "$NOTOOLS"
  IFS=: read -r -a path_dirs <<< "$ORIG_PATH"
  for d in "${path_dirs[@]}"; do
    [ -d "$d" ] || continue
    for t in "$d"/*; do
      nm="${t##*/}"
      case "$nm" in setsid|timeout|gtimeout|perl) continue ;; esac
      if [ -x "$t" ] && [ ! -d "$t" ] && [ ! -e "$NOTOOLS/$nm" ]; then ln -s "$t" "$NOTOOLS/$nm"; fi
    done
  done
  printf '#!/bin/sh\nsleep 1\nexec "%s" "$@"\n' "$REAL_PERL" > "$NOTOOLS/perl"; chmod +x "$NOTOOLS/perl"
  run_case "slow perl, no setsid" 30.102 "" "$NOTOOLS"
else echo "skip - no perl on this box"; fi

[ "$fail" = 0 ] && echo "all heavy-pgid-race tests passed" || { echo "heavy-pgid-race tests FAILED"; exit 1; }
