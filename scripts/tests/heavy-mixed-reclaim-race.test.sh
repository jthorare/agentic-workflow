#!/usr/bin/env bash
# The wrapper vs locks.sh reclaim race on one heavy slot (#2058; TLC MixedReclaimRace). Run:
# bash scripts/tests/heavy-mixed-reclaim-race.test.sh
# Scratch AW_STATE_DIR and lock paths only, never the box's live lock dir. The real scripts/heavy.sh and the real
# config/lib/locks.sh acquire_any_lock (what bugfix-state's `locked` runs) race for a slot whose holder died, with PATH
# stubs that hold each at a step.
set -uo pipefail
unset TMUX
for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY)'); do unset "$v"; done   # this suite may itself run under a heavy job
unset DOTNET_PROCESSOR_COUNT MSBUILDDISABLENODEREUSE
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HEAVY="$DIR/../heavy.sh"
LOCKS="$DIR/../../config/lib/locks.sh"
fail=0
pids=()

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
cleanup() { [ "${#pids[@]}" -eq 0 ] || kill "${pids[@]}" 2>/dev/null; rm -rf -- "$WORK"; }
trap cleanup EXIT

# dead_pid: a pid whose process has exited.
dead_pid() { bash -c 'exit 0' & local p=$!; wait "$p"; echo "$p"; }

T="$WORK/mixed"; SLOT="$T/state/locks/heavy-job.lock"
mkdir -p "$T/state/locks" "$T/stub" "$T/sig" "$T/running"
# The external commands both takers may use. STUB_ROLE picks the gate; every gate gives up after 5 s, so a taker that
# never reaches a gate just runs on.
for c in mkdir rmdir unlink awk cat mv rm; do
  real="$(command -v "$c")"
  cat > "$T/stub/$c" <<EOF
#!/usr/bin/env bash
gate() { for _ in \$(seq 1 50); do [ -e "$T/sig/\$1" ] && return 0; sleep 0.1; done; return 0; }
self=$c; last="\${@: -1}"
# l1 (locks.sh): hold its move of the dead owner aside until the wrapper has read that owner too.
if [ "\${STUB_ROLE:-}" = l1 ] && [ \$self = mv ] && [ "\${1:-}" = "$SLOT/owner" ]; then gate h2-read; fi
"$real" "\$@"; rc=\$?
# h2 (wrapper): after reading the dead owner, wait until locks.sh holds a fresh, empty slot.
if [ "\${STUB_ROLE:-}" = h2 ] && { [ \$self = awk ] || [ \$self = cat ]; } && [ "\$last" = "$SLOT/owner" ]; then : > "$T/sig/h2-read"; gate l1-paused; fi
# l1: right after its reclaiming mkdir of the slot succeeds, hold before the pid write until the wrapper has run.
if [ "\${STUB_ROLE:-}" = l1 ] && [ \$self = mkdir ] && [ \$rc = 0 ] && [ "\$last" = "$SLOT" ]; then : > "$T/sig/l1-paused"; gate h2-ran; fi
exit \$rc
EOF
  chmod +x "$T/stub/$c"
done
# The job: count the jobs running at once and record OVERLAP when another one is already in.
cat > "$T/job.sh" <<EOF
#!/usr/bin/env bash
mkdir "$T/running/\$1"; [ "\$(ls "$T/running" | wc -l)" -gt 1 ] && : > "$T/sig/OVERLAP"
: > "$T/sig/\$1-ran"
sleep 2; rmdir "$T/running/\$1"
EOF
chmod +x "$T/job.sh"

sleep 300 & s2=$!; pids+=("$s2")

# h1 (a wrapper job) held slot 1 and died. The wrapper job h2 and the locks.sh holder l1 both read its dead owner.
# l1 moves it aside, removes the folder and mkdirs it, and is held before its pid write; h2 then reclaims too.
mkdir "$SLOT"; echo "$(dead_pid) 2026-11-04T00:00:00Z session=1 dead job" > "$SLOT/owner"
env AW_STATE_DIR="$T/state" AW_HEAVY_JOB_LOCK="$SLOT" AW_HEAVY_SLOTS=1 AW_HEAVY_TEST_PATH="$T/stub" \
  AW_HEAVY_SESSION_PID="$s2" AW_HEAVY_RUN_MAX=0 STUB_ROLE=h2 bash "$HEAVY" --expensive "$T/job.sh" h2 2>/dev/null & p2=$!
env PATH="$T/stub:$PATH" STUB_ROLE=l1 bash -c '. "$1"; slot="$(acquire_any_lock "$2" 1 20)" || exit 75; "$3" l1; release_lock "$slot"' \
  _ "$LOCKS" "$SLOT" "$T/job.sh" 2>/dev/null & p1=$!
wait "$p2"; r2=$?; wait "$p1"; r1=$?
check "mixed race: both jobs finish" "$r2 $r1" "0 0"
check "mixed race: both jobs ran" "$([ -e "$T/sig/h2-ran" ] && [ -e "$T/sig/l1-ran" ] && echo yes)" "yes"
check "mixed race: never two jobs on one slot" "$([ -e "$T/sig/OVERLAP" ] && echo overlap || echo one)" "one"

exit "$fail"
