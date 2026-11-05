#!/usr/bin/env bash
# Tests for the dead-holder reclaim in scripts/heavy.sh (#2058). Run: bash scripts/tests/heavy-reclaim-race.test.sh
# Scratch AW_STATE_DIR and lock paths only, never the box's live lock dir. Each case is a TLC trace from
# specs/tla/HeavySlots, replayed on the real wrapper with PATH stubs (AW_HEAVY_TEST_PATH) that hold a job at a step.
set -uo pipefail
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HEAVY="$DIR/../heavy.sh"
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

# setup <case>: a scratch state dir, the stubs and the job script; sets T and SLOT.
setup() {
  T="$WORK/$1"; SLOT="$T/state/locks/heavy-job.lock"
  mkdir -p "$T/state/locks" "$T/stub" "$T/sig" "$T/running"
  # The external commands acquire() may use. STUB_ROLE picks the gate; every gate gives up after 5 s, so a
  # wrapper that never reaches a gate just runs on.
  local c real
  for c in mkdir rmdir unlink awk cat mv rm; do
    real="$(command -v "$c")"
    cat > "$T/stub/$c" <<EOF
#!/usr/bin/env bash
gate() { for _ in \$(seq 1 50); do [ -e "$T/sig/\$1" ] && return 0; sleep 0.1; done; return 0; }
self=$c; last="\${@: -1}"
# h2: hold its first touch of the dead owner file until h3 has read it.
if [ "\${STUB_ROLE:-}" = h2 ] && { [ \$self = unlink ] || [ \$self = mv ] || [ \$self = rm ]; } && [ "\${1:-}" = "$SLOT/owner" -o "\${2:-}" = "$SLOT/owner" -o "\$last" = "$SLOT/owner" ]; then gate h3-read; fi
"$real" "\$@"; rc=\$?
# h3: after reading the owner file, wait until h2 holds a fresh, empty slot.
if [ "\${STUB_ROLE:-}" = h3 ] && { [ \$self = awk ] || [ \$self = cat ]; } && [ "\$last" = "$SLOT/owner" ]; then : > "$T/sig/h3-read"; gate h2-paused; fi
# h2: right after its mkdir of the slot succeeds (the reclaim), hold before the owner write until h3 has run.
if [ "\${STUB_ROLE:-}" = h2 ] && [ \$self = mkdir ] && [ \$rc = 0 ] && [ "\$last" = "$SLOT" ]; then : > "$T/sig/h2-paused"; gate h3-ran; fi
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
}

# run <role> <session pid>: one expensive wrapper job on the scratch lock, one slot.
run() {
  env AW_STATE_DIR="$T/state" AW_HEAVY_JOB_LOCK="$SLOT" AW_HEAVY_SLOTS=1 AW_HEAVY_TEST_PATH="$T/stub" \
    AW_HEAVY_SESSION_PID="$2" AW_HEAVY_RUN_MAX=0 STUB_ROLE="$1" bash "$HEAVY" --expensive "$T/job.sh" "$1"
}

# Each job its own live session, so the per-session slot doesn't serialize them.
sleep 300 & s2=$!; sleep 300 & s3=$!; pids+=("$s2" "$s3")

# --- (a) gaps/WrapperReclaimRace.cfg: h1 held slot 1 and died; h2 and h3 both read its dead owner. h2 reclaims and
# is held after its mkdir, before its owner write; h3 then reclaims too. One holder at a time, whoever wins. ---
setup race
mkdir "$SLOT"; echo "$(dead_pid) 2026-11-04T00:00:00Z session=1 dead job" > "$SLOT/owner"
run h2 "$s2" 2>/dev/null & p2=$!
run h3 "$s3" 2>/dev/null & p3=$!
wait "$p2"; r2=$?; wait "$p3"; r3=$?
check "race: both jobs finish" "$r2 $r3" "0 0"
check "race: both jobs ran" "$([ -e "$T/sig/h2-ran" ] && [ -e "$T/sig/h3-ran" ] && echo yes)" "yes"
check "race: never two jobs on one slot" "$([ -e "$T/sig/OVERLAP" ] && echo overlap || echo one)" "one"

# --- (b) a reclaimer SIGKILLed between moving the dead owner aside and removing the folder leaves only
# owner.reclaim.<its pid>. Its pid is dead too, so the next waiter frees the slot instead of waiting forever. ---
setup orphan
mkdir "$SLOT"; echo "$(dead_pid) 2026-11-04T00:00:00Z session=1 dead job" > "$SLOT/owner.reclaim.$(dead_pid)"
timeout 20 bash -c "$(declare -f run); T='$T' SLOT='$SLOT' HEAVY='$HEAVY'; run solo $s3" 2>/dev/null; r=$?
check "orphaned reclaim: the next job gets the slot" "$r $([ -e "$T/sig/solo-ran" ] && echo ran || echo waiting)" "0 ran"

exit "$fail"
