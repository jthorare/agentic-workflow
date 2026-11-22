#!/usr/bin/env bash
# Tests for scripts/heavy.sh's light pool (#3796): a `--light` job that compiles takes one of N box-wide light slots
# (<locks>/heavy-light.lock, N from $AW_STATE_DIR/heavy-light-cap, else AW_HEAVY_LIGHT_SLOTS, else 4), and doesn't start
# while a live job waits for a box-wide slot and MemAvailable is under that slot's floor. Covers: N run and the N+1th waits
# until one ends; the cap file is read again while a job waits; the env fallback and the default 4; the hold, and its end
# when the waiter goes or memory is enough; the hold counts heavy-meta-reserve and the slots the waiter can take (#3810); its
# note repeats (on a copy with HOLD_NOTE_EVERY=1) and names the waiter and the memory figure; slot 1 free and a dead waiter
# hold nothing; a path-qualified dotnet build and a dotnet msbuild take a light slot; a job that compiles nothing never
# waits on the pool; AW_HEAVY_PRIORITY=meta skips the hold but not the pool; a held wait doesn't count toward the lock
# timeout; a dead holder's light slot is reclaimed.
# A fake dotnet on PATH waits for a go file per job; an awk shim answers MemAvailable from a file. Scratch state and lock
# dirs only; every pid killed is one this suite started and recorded. Every wait is bounded at ~10 s.
# Run: bash scripts/tests/heavy-light-pool.test.sh
set -uo pipefail
unset TMUX
for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY|META_LANE)'); do unset "$v"; done   # this suite may itself run under a heavy job
unset DOTNET_PROCESSOR_COUNT MSBUILDDISABLENODEREUSE
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HEAVY="$DIR/heavy.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
yn() { if eval "$1"; then echo yes; else echo no; fi; }

WORK="$(mktemp -d)"
bgpids=()
STATE="$WORK/state"; LOCKS="$STATE/locks"; RUN="$WORK/run"; mkdir -p "$STATE" "$RUN"
cleanup() {
  touch "$RUN/go.all" 2> /dev/null
  for p in ${bgpids[@]+"${bgpids[@]}"}; do kill "$p" 2> /dev/null; done   # by recorded pid only
  wait 2> /dev/null
  rm -rf -- "$WORK"
}
trap cleanup EXIT

export AW_STATE_DIR="$STATE" AW_HEAVY_SESSION_PID="$$" AW_SESSION_HEAVY_SLOTS=50 AW_HEAVY_RUN_MAX=0 \
  AW_HEAVY_MIN_FREE_MB=10240 AW_HEAVY_MIN_FREE_HIGH_MB=14336 RUN
echo 4 > "$STATE/heavy-slots-cap"
echo '0::/test.slice' > "$WORK/cgroup"; export AW_HEAVY_TEST_CGROUP="$WORK/cgroup"
STUB="$WORK/stub"; mkdir -p "$STUB"
printf '#!/usr/bin/env bash\nexit 1\n' > "$STUB/systemd-run"   # no scope: the suite never creates a real systemd scope
# The fake dotnet: marks its job started, then waits (<= 15 s) for go.<job> or go.all.
cat > "$STUB/dotnet" <<'EOF'
#!/usr/bin/env bash
touch "$RUN/started.$JOB"
for _ in $(seq 1 300); do [ -e "$RUN/go.$JOB" ] || [ -e "$RUN/go.all" ] && break; sleep 0.05; done
EOF
# The awk shim: the memory guard's MemAvailable read answers from $FAKE_MEM_KB's file; anything else is the real awk.
REAL_AWK="$(command -v awk)"
export FAKE_MEM="$WORK/mem.kb"
cat > "$STUB/awk" <<SHIM
#!/usr/bin/env bash
for a in "\$@"; do case "\$a" in *MemAvailable*) echo x >> "\$FAKE_MEM.reads"; [ -f "\$FAKE_MEM" ] && { cat "\$FAKE_MEM"; exit 0; } ;; esac; done
exec "$REAL_AWK" "\$@"
SHIM
chmod +x "$STUB/systemd-run" "$STUB/dotnet" "$STUB/awk"
export AW_HEAVY_TEST_PATH="$STUB"
# A copy with a 1 s lock timeout, laid out so it finds ../config/lib/heavy-config.sh.
mkdir -p "$WORK/scripts"; ln -s "$DIR/../config" "$WORK/config"
ONE="$WORK/scripts/heavy-one.sh"; sed 's/^TIMEOUT=5400$/TIMEOUT=1/' "$HEAVY" > "$ONE"
# A copy that repeats the hold note every second instead of every 300.
NOTE="$WORK/scripts/heavy-note.sh"; sed 's/^HOLD_NOTE_EVERY=300$/HOLD_NOTE_EVERY=1/' "$HEAVY" > "$NOTE"
LIGHT="$LOCKS/heavy-light.lock"; BOX="$LOCKS/heavy-job.lock"
# The stand-in live process lives as long as this suite (#3810: a `sleep 300` could end mid-suite on a loaded box and turn rows
# red for the wrong reason). cleanup kills it by its recorded pid; if the suite is SIGKILLed it ends within a second on its own.
(while kill -0 "$$" 2> /dev/null; do sleep 1; done) & live=$!; bgpids+=("$live")
(while kill -0 "$$" 2> /dev/null; do sleep 1; done) & live2=$!; bgpids+=("$live2")   # a second live waiter
true & dead=$!; wait "$dead"

wait_for() { local i; for i in $(seq 1 100); do eval "$1" && return 0; sleep 0.1; done; return 1; }   # <= 10 s
mem_mb() { echo "$(($1 * 1024))" > "$FAKE_MEM"; }
# job <id> [<heavy args>...] (default --light dotnet build <id>), extra env in JOB_ENV; the wrapper's pid in $RUN/pid.<id>.
job() {
  local id="$1" script="${SCRIPT:-$HEAVY}"; shift
  [ "$#" -gt 0 ] || set -- --light dotnet build "$id"
  env JOB="$id" ${JOB_ENV:-} bash "$script" "$@" > "$RUN/out.$id" 2>&1 &
  local p=$!; bgpids+=("$p"); echo "$p" > "$RUN/pid.$id"
}
pid_of() { cat "$RUN/pid.$1"; }
started() { [ -e "$RUN/started.$1" ]; }
waiting() { [ -s "$LOCKS/heavy-wait/$(pid_of "$1")" ]; }
go() { touch "$RUN/go.$1"; }
rc_of() { wait "$(pid_of "$1")" 2> /dev/null; RC=$?; }   # in this shell: a $(...) subshell can't wait for our jobs
# reset: end every job, empty the locks and the run dir, no light cap or reserve file, box-wide cap 4; memory back to plenty.
reset() {
  touch "$RUN/go.all"
  local f id; for f in "$RUN"/pid.*; do
    [ -e "$f" ] || continue; id="${f##*/pid.}"
    started "$id" || kill "$(cat "$f")" 2> /dev/null   # a job still waiting for a slot: TERM its wrapper (recorded pid)
    wait "$(cat "$f")" 2> /dev/null
  done
  find "$RUN" "$LOCKS" -mindepth 1 -delete 2> /dev/null
  rm -f "$STATE/heavy-light-cap" "$STATE/heavy-meta-reserve"; echo 4 > "$STATE/heavy-slots-cap"; mem_mb 64000; JOB_ENV=""; SCRIPT=""
}
hold_box() { mkdir -p "$BOX"; echo "$live 2026-01-01T00:00:00Z session=x held" > "$BOX/owner"; }
# box_waiter <pid> [<extra fields>]
box_waiter() { mkdir -p "$LOCKS/heavy-wait"; printf '{"pid":%s,"session":1,"lock":"box-wide","since":"2026-01-01T00:00:00Z","argv":["x"],"cwd":"/"%s}\n' "$1" "${2:-}" > "$LOCKS/heavy-wait/$1"; }
fill_light() { mkdir -p "$LIGHT"; echo "$live 2026-01-01T00:00:00Z session=x held" > "$LIGHT/owner"; }

# --- the pool: N run, the N+1th waits and starts when one ends ---
reset; echo 2 > "$STATE/heavy-light-cap"
job j1; job j2; wait_for 'started j1 && started j2'
job j3; wait_for 'waiting j3'
check "cap 2: two light builds run" "$(yn 'started j1 && started j2')" "yes"
check "cap 2: the third waits" "$(yn 'started j3')" "no"
check "cap 2: slots 1 and 2 of heavy-light.lock are held" "$(yn '[ -d "$LIGHT" ] && [ -d "$LIGHT.2" ]')" "yes"
check "the waiter's file says lock light" "$(grep -c '"lock":"light"' "$LOCKS/heavy-wait/$(pid_of j3)")" "1"
go j1; wait_for 'started j3'
check "the third starts when one ends" "$(yn 'started j3')" "yes"
rc_of j1; check "the first ended with exit 0" "$RC" "0"

# --- the cap file is read again while a job waits ---
reset; echo 1 > "$STATE/heavy-light-cap"
job j1; wait_for 'started j1'; job j2; wait_for 'waiting j2'
check "cap 1: the second waits" "$(yn 'started j2')" "no"
echo 2 > "$STATE/heavy-light-cap"; wait_for 'started j2'
check "cap raised to 2 while waiting: the second starts, the first still running" "$(yn 'started j2 && kill -0 "$(pid_of j1)" 2> /dev/null')" "yes"

# --- the env fallback, and the default 4 ---
reset; JOB_ENV="AW_HEAVY_LIGHT_SLOTS=1"
job j1; wait_for 'started j1'; job j2; wait_for 'waiting j2'
check "no cap file, AW_HEAVY_LIGHT_SLOTS=1: the second waits" "$(yn 'started j2')" "no"
reset
for i in 1 2 3 4; do job "d$i"; done; wait_for 'started d1 && started d2 && started d3 && started d4'
job d5; wait_for 'waiting d5'
check "no cap file, no env: four run and the fifth waits" "$(yn 'started d4 && ! started d5')" "yes"

# --- the hold: a live box-wide waiter and memory under the floor of the next free slot ---
reset; hold_box; box_waiter "$live"; mem_mb 5000   # slot 1 held, slot 2 free, its floor 10240 MB
job j1; wait_for 'waiting j1'
check "held: a live box-wide waiter, 5000 MB under slot 2's 10240: the build waits" "$(yn 'started j1')" "no"
check "held: its wait file says lock light" "$(grep -c '"lock":"light"' "$LOCKS/heavy-wait/$(pid_of j1)")" "1"
check "held: it says why on stderr" "$(grep -c 'holding this build back' "$RUN/out.j1")" "1"
rm -f "$LOCKS/heavy-wait/$live"; wait_for 'started j1'
check "held: the waiter's file goes, the build starts" "$(yn 'started j1')" "yes"

# --- the hold note repeats while the hold lasts and names the waiter and the memory figure (#3810) ---
reset; hold_box; box_waiter "$live"; mem_mb 5000; SCRIPT="$NOTE"
job n1; wait_for 'waiting n1'; sleep 3.5
check "held 3.5 s, a note every 1 s: the note is printed again" "$(yn '[ "$(grep -c "holding this build back" "$RUN/out.n1")" -ge 2 ]')" "yes"
check "the note names the waiter's pid and argv" "$(grep -c "box-wide job $live (\"x\")" "$RUN/out.n1" | sed 's/^[1-9][0-9]*$/some/')" "some"
check "the note names slot 2, its floor and the MB available" "$(grep -c 'slot 2, which needs 10240 MB free; 5000 MB available' "$RUN/out.n1" | sed 's/^[1-9][0-9]*$/some/')" "some"

# --- a new hold, or a hold for another waiter, prints its note at once (the real 300 s cadence; review of #3882) ---
# The light pool is full, so the build keeps waiting for a slot after the hold ends.
reset; echo 1 > "$STATE/heavy-light-cap"; fill_light; hold_box; box_waiter "$live"; mem_mb 5000
job h1; wait_for 'grep -q "holding this build back" "$RUN/out.h1"'
mem_mb 64000; sleep 2.5; mem_mb 5000; wait_for '[ "$(grep -c "holding this build back" "$RUN/out.h1")" -ge 2 ]'
check "a hold that ends and starts again within 300 s prints a second note" "$(grep -c 'holding this build back' "$RUN/out.h1")" "2"
reset; echo 1 > "$STATE/heavy-light-cap"; fill_light; hold_box; box_waiter "$live"; mem_mb 5000
job h2; wait_for 'grep -q "box-wide job $live " "$RUN/out.h2"'
box_waiter "$live2"; rm -f "$LOCKS/heavy-wait/$live"; wait_for 'grep -q "box-wide job $live2 " "$RUN/out.h2"'
check "a hold for another waiter names that waiter at once" "$(yn 'grep -q "box-wide job $live2 " "$RUN/out.h2"')" "yes"

reset; hold_box; box_waiter "$live"; mem_mb 5000
job j1; wait_for 'waiting j1'
check "held again before memory frees" "$(yn 'started j1')" "no"
mem_mb 12000; wait_for 'started j1'
check "held: memory reaches slot 2's floor (12000 MB), the build starts" "$(yn 'started j1')" "yes"

reset; hold_box; mkdir -p "$BOX.2"; echo "$live x" > "$BOX.2/owner"; box_waiter "$live"; mem_mb 12000   # slot 3 next: 14336 MB
job j1; wait_for 'waiting j1'
check "held: slots 1 and 2 held, 12000 MB under slot 3's 14336" "$(yn 'started j1')" "no"

reset; box_waiter "$live"; mem_mb 5000
job j1; wait_for 'started j1'
check "not held: box-wide slot 1 is free (no floor), low memory" "$(yn 'started j1')" "yes"

# --- the hold counts the meta reserve as the box-wide pool does (#3810) ---
# Cap 3, reserve 1, slot 1 free, slot 2 held, 12000 MB: the pool keeps the reserve at 1 (slot 2's floor is met), so a lane
# waiter can't take slot 1 and slot 3 needs 14336 MB. A meta waiter takes slot 1.
reset; echo 3 > "$STATE/heavy-slots-cap"; echo 1 > "$STATE/heavy-meta-reserve"
mkdir -p "$BOX.2"; echo "$live x" > "$BOX.2/owner"; box_waiter "$live"; mem_mb 12000
job j1; wait_for 'waiting j1'
check "held: slot 1 free but reserved, a lane waiter needs slot 3 (12000 MB under 14336)" "$(yn 'started j1')" "no"
reset; echo 3 > "$STATE/heavy-slots-cap"; echo 1 > "$STATE/heavy-meta-reserve"
mkdir -p "$BOX.2"; echo "$live x" > "$BOX.2/owner"; box_waiter "$live" ',"meta":1'; mem_mb 12000
job j1; wait_for 'started j1'
check "not held: the same, but the waiter is meta and may take reserved slot 1" "$(yn 'started j1')" "yes"
# Memory too low for slot 2 (the pool then drops the reserve to 0): a lane waiter takes slot 1.
reset; echo 3 > "$STATE/heavy-slots-cap"; echo 1 > "$STATE/heavy-meta-reserve"
mkdir -p "$BOX.2"; echo "$live x" > "$BOX.2/owner"; box_waiter "$live"; mem_mb 5000
job j1; wait_for 'started j1'
check "not held: 5000 MB, the reserve drops to 0, so a lane waiter may take slot 1" "$(yn 'started j1')" "yes"
# Slot 2 free but short of memory while slot 3's lower floor is met: the waiter takes slot 3, so nothing holds.
reset; hold_box; box_waiter "$live"; mem_mb 5000; JOB_ENV="AW_HEAVY_MIN_FREE_HIGH_MB=3072"
job j1; wait_for 'started j1'
check "not held: slot 2 under its floor, but free slot 3 (floor 3072) is one the waiter can take" "$(yn 'started j1')" "yes"

reset; hold_box; box_waiter "$dead"; mem_mb 5000
job j1; wait_for 'started j1'
check "not held: the box-wide waiter's pid is dead" "$(yn 'started j1')" "yes"

# --- a light job that compiles nothing never waits on the pool or the hold ---
reset; echo 1 > "$STATE/heavy-light-cap"; fill_light; hold_box; box_waiter "$live"; mem_mb 5000; touch "$RUN/go.all"
job t1 --light dotnet test --no-build x; job c1 --light dotnet clean; job s1 --light bash -c 'touch "$RUN/started.s1"'
wait_for 'started t1 && started c1 && started s1'
check "pool full and held: test --no-build, clean and a script run" "$(yn 'started t1 && started c1 && started s1')" "yes"
rc_of t1; rcs="$RC"; rc_of c1; rcs="$rcs $RC"; rc_of s1; check "they exit 0" "$rcs $RC" "0 0 0"

# --- a path-qualified dotnet build and a dotnet msbuild compile too: they take a light slot (#3810) ---
reset; echo 1 > "$STATE/heavy-light-cap"; fill_light
job p1 --light "$STUB/dotnet" build p1; job q1 --light dotnet msbuild q1; wait_for 'waiting p1 && waiting q1'
check "pool full: a path-qualified dotnet build waits for a light slot" "$(yn '! started p1')" "yes"
check "pool full: a dotnet msbuild waits for a light slot (#3810 item 3)" "$(yn '! started q1')" "yes"

# --- AW_HEAVY_PRIORITY=meta skips the hold, not the pool ---
reset; hold_box; box_waiter "$live"; mem_mb 5000; JOB_ENV="AW_HEAVY_PRIORITY=meta"
job m1; wait_for 'started m1'
check "meta: a held light build starts" "$(yn 'started m1')" "yes"
reset; echo 1 > "$STATE/heavy-light-cap"; fill_light; JOB_ENV="AW_HEAVY_PRIORITY=meta"
job m2; wait_for 'waiting m2'
check "meta: the light pool still binds it" "$(yn 'started m2')" "no"

# --- no box-wide waiter: the hold check reads no memory (review of #3882: no fork per pass without a waiter) ---
reset; rm -f "$FAKE_MEM.reads"; job f1; wait_for 'started f1'
check "a light build with no box-wide waiter reads MemAvailable 0 times" "$(cat "$FAKE_MEM.reads" 2> /dev/null | wc -l | tr -d ' ')" "0"

# --- a held wait doesn't count toward the lock timeout; a pool wait does ---
reset; hold_box; box_waiter "$live"; mem_mb 5000; SCRIPT="$ONE"
job o1; wait_for 'waiting o1'; sleep 2.5
check "timeout 1 s: held 2.5 s, still waiting" "$(yn 'kill -0 "$(pid_of o1)" 2> /dev/null && ! started o1')" "yes"
rm -f "$LOCKS/heavy-wait/$live"; wait_for 'started o1'; go o1
rc_of o1; check "timeout 1 s: the hold ends, the build runs and exits 0" "$RC" "0"
reset; echo 1 > "$STATE/heavy-light-cap"; fill_light; SCRIPT="$ONE"
job o2
rc_of o2; check "timeout 1 s: a full pool times out, exit 75" "$RC" "75"

# --- a dead holder's light slot is reclaimed ---
reset; mkdir -p "$LIGHT"; echo "$dead 2026-01-01T00:00:00Z session=x held" > "$LIGHT/owner"; echo 1 > "$STATE/heavy-light-cap"
job r1; wait_for 'started r1'
check "a light slot whose holder is dead is reclaimed" "$(yn 'started r1')" "yes"
check "the slot's owner is now this job" "$(cut -d' ' -f1 "$LIGHT/owner" 2> /dev/null)" "$(pid_of r1)"

reset
[ "$fail" -eq 0 ] && echo "all heavy-light-pool tests passed"
exit "$fail"
