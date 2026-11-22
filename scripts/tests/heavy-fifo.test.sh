#!/usr/bin/env bash
# Tests for scripts/heavy.sh's arrival order (#3819): a waiter takes a free slot only when no older live waiter of the same
# pool is queued (wait-file since, then pid). Covers: three box-wide waiters get one slot in arrival order, and three light
# ones; a live older wait file holds a free slot back and its removal lets the job through; a dead one, one whose pid now
# belongs to another process (its "start" differs) or that has no "start", one without "fifo":1, one of another pool and one of
# another session hold nothing; the session pool is ordered too; a meta job skips
# older waiters and nobody waits for a meta waiter; AW_HEAVY_FIFO_MAX_DEFER lets a waiter through; standing back counts toward
# the lock timeout; an empty queue adds no wait; the wait file carries "fifo":1, and "meta":1 for a meta job.
# A fake dotnet on PATH waits for a go file per job. Scratch state and lock dirs only; every pid killed is one this suite
# started and recorded. Every wait is bounded at ~20 s.
# Run: bash scripts/tests/heavy-fifo.test.sh
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

export AW_STATE_DIR="$STATE" AW_HEAVY_SESSION_PID="$$" AW_SESSION_HEAVY_SLOTS=50 AW_HEAVY_RUN_MAX=0 RUN
echo '0::/test.slice' > "$WORK/cgroup"; export AW_HEAVY_TEST_CGROUP="$WORK/cgroup"
STUB="$WORK/stub"; mkdir -p "$STUB"
printf '#!/usr/bin/env bash\nexit 1\n' > "$STUB/systemd-run"   # no scope: the suite never creates a real systemd scope
# The fake dotnet: marks its job started, then waits (<= 30 s) for go.<job> or go.all.
cat > "$STUB/dotnet" <<'EOF'
#!/usr/bin/env bash
touch "$RUN/started.$JOB"
for _ in $(seq 1 600); do [ -e "$RUN/go.$JOB" ] || [ -e "$RUN/go.all" ] && break; sleep 0.05; done
EOF
chmod +x "$STUB/systemd-run" "$STUB/dotnet"
export AW_HEAVY_TEST_PATH="$STUB"
# A copy with a 1 s lock timeout, laid out so it finds ../config/lib/heavy-config.sh.
mkdir -p "$WORK/scripts"; ln -s "$DIR/../config" "$WORK/config"
ONE="$WORK/scripts/heavy-one.sh"; sed 's/^TIMEOUT=5400$/TIMEOUT=1/' "$HEAVY" > "$ONE"
sleep 300 & live=$!; bgpids+=("$live")
true & dead=$!; wait "$dead"

wait_for() { local i; for i in $(seq 1 200); do eval "$1" && return 0; sleep 0.1; done; return 1; }   # <= 20 s
# job <id> [<heavy args>...] (default --expensive dotnet build <id>), extra env in JOB_ENV; the wrapper's pid in $RUN/pid.<id>.
job() {
  local id="$1" script="${SCRIPT:-$HEAVY}"; shift
  [ "$#" -gt 0 ] || set -- --expensive dotnet build "$id"
  env JOB="$id" ${JOB_ENV:-} bash "$script" "$@" > "$RUN/out.$id" 2>&1 &
  local p=$!; bgpids+=("$p"); echo "$p" > "$RUN/pid.$id"
}
pid_of() { cat "$RUN/pid.$1"; }
started() { [ -e "$RUN/started.$1" ]; }
waiting() { [ -s "$LOCKS/heavy-wait/$(pid_of "$1")" ]; }
go() { touch "$RUN/go.$1"; }
rc_of() { wait "$(pid_of "$1")" 2> /dev/null; RC=$?; }   # in this shell: a $(...) subshell can't wait for our jobs
# next_second <id>: wait until the clock has passed the second of <id>'s wait-file since, so the next waiter is strictly younger.
next_second() {
  local s; s="$(sed 's/.*"since":"\([^"]*\)".*/\1/' "$LOCKS/heavy-wait/$(pid_of "$1")")"
  wait_for '[ "$(date -u +%FT%TZ)" != "$s" ]'
}
reset() {
  touch "$RUN/go.all"
  local f id; for f in "$RUN"/pid.*; do
    [ -e "$f" ] || continue; id="${f##*/pid.}"
    started "$id" || kill "$(cat "$f")" 2> /dev/null   # a job still waiting for a slot: TERM its wrapper (recorded pid)
    wait "$(cat "$f")" 2> /dev/null
  done
  find "$RUN" "$LOCKS" -mindepth 1 -delete 2> /dev/null
  echo 1 > "$STATE/heavy-slots-cap"; echo 1 > "$STATE/heavy-light-cap"; JOB_ENV=""; SCRIPT=""
}
# start_of <pid> [ps]: the process start token heavy.sh writes as "start" (Linux: field 22 of /proc/<pid>/stat, read after the
# last ")"; macOS, or with "ps" as heavy.sh's AW_HEAVY_TEST_PS_START=1 does: ps lstart, spaces as _). Empty when the pid is gone.
start_of() {
  local l r f
  if [ -r "/proc/$1/stat" ] && [ "${2:-}" != ps ]; then
    IFS= read -r l < "/proc/$1/stat" 2> /dev/null || return 0
    r="${l##*) }"; read -r -a f <<< "$r"; printf '%s' "${f[19]:-}"
  else
    r="$(ps -o lstart= -p "$1" 2> /dev/null)"; read -r -a f <<< "$r"; (IFS=_; printf '%s' "${f[*]-}")
  fi
}
# A second stub dir whose ps prints nothing for `-o lstart=`, as when the pid dies between kill -0 and ps; any other call is the
# real ps (heavy.sh's session lookup).
STUB_NOPS="$WORK/stub-nops"; mkdir -p "$STUB_NOPS"; REAL_PS="$(command -v ps)"
cat > "$STUB_NOPS/ps" <<EOF2
#!/usr/bin/env bash
for a in "\$@"; do [ "\$a" = lstart= ] && exit 1; done
exec "$REAL_PS" "\$@"
EOF2
chmod +x "$STUB_NOPS/ps"
# fake <pid> <lock> [<extra fields>] [<session>] [<start>]: an older wait file (since 2026-01-01) for <pid>. <start> defaults
# to <pid>'s real start token (a genuine waiter); "-" leaves the field out; anything else is written as given.
fake() {
  local st="${5:-auto}" sf=""
  [ "$st" = auto ] && st="$(start_of "$1")"
  [ "$st" = - ] || sf=",\"start\":\"$st\""
  mkdir -p "$LOCKS/heavy-wait"
  printf '{"pid":%s,"session":%s,"lock":"%s","since":"2026-01-01T00:00:00Z"%s,"argv":["x"],"cwd":"/"%s}\n' \
    "$1" "${4:-$$}" "$2" "$sf" "${3-,\"fifo\":1}" > "$LOCKS/heavy-wait/$1"
}

# --- three box-wide waiters get the one slot in arrival order ---
reset
job h; wait_for 'started h'
job a; wait_for 'waiting a'; next_second a
job b; wait_for 'waiting b'; next_second b
job c; wait_for 'waiting c'
check "box-wide: the wait file carries fifo:1" "$(grep -c '"fifo":1' "$LOCKS/heavy-wait/$(pid_of a)")" "1"
go h; wait_for 'started a || started b || started c'
check "box-wide: the first waiter takes the slot first" "$(yn 'started a && ! started b && ! started c')" "yes"
go a; wait_for 'started b || started c'
check "box-wide: the second waiter next" "$(yn 'started b && ! started c')" "yes"
go b; wait_for 'started c'
check "box-wide: then the third" "$(yn 'started c')" "yes"

# --- the same for the light pool ---
reset
job h --light dotnet build h; wait_for 'started h'
job a --light dotnet build a; wait_for 'waiting a'; next_second a
job b --light dotnet build b; wait_for 'waiting b'; next_second b
job c --light dotnet build c; wait_for 'waiting c'
go h; wait_for 'started a || started b || started c'
check "light: the first waiter takes the slot first" "$(yn 'started a && ! started b && ! started c')" "yes"
go a; wait_for 'started b || started c'
check "light: the second waiter next" "$(yn 'started b && ! started c')" "yes"
go b; wait_for 'started c'
check "light: then the third" "$(yn 'started c')" "yes"

# --- a live older waiter holds a free slot back; the job takes it once that waiter is gone ---
reset; fake "$live" box-wide
job j; wait_for 'waiting j'; sleep 1.5
check "a live older box-wide waiter: the free slot stays free" "$(yn '! started j && [ ! -d "$LOCKS/heavy-job.lock" ]')" "yes"
rm -f "$LOCKS/heavy-wait/$live"; wait_for 'started j'
check "the older waiter's file goes: the job starts" "$(yn 'started j')" "yes"

reset; fake "$live" light
job j --light dotnet build j; wait_for 'waiting j'; sleep 1.5
check "a live older light waiter holds a light build back" "$(yn '! started j')" "yes"

# --- waiters that don't count ---
reset; fake "$dead" box-wide; job j; wait_for 'started j'
check "a dead older waiter's file is ignored" "$(yn 'started j')" "yes"
# A SIGKILLed waiter's file stays; when another process gets its pid, the file must hold no job back, the first one or the next.
reset; [ -n "$(start_of "$live")" ] && [ "$(start_of "$live")" != 1 ]; check "the live pid has a start token" "$?" "0"
fake "$live" box-wide ',"fifo":1' "$$" 1
job r1; wait_for 'started r1'
check "a reused pid's wait file (start differs): the job takes the free slot at once" "$(yn 'started r1 && ! grep -q waiting "$RUN/out.r1"')" "yes"
go r1; started r1 || kill "$(pid_of r1)" 2> /dev/null; wait "$(pid_of r1)" 2> /dev/null; job r2; wait_for 'started r2'
check "a reused pid's wait file: the next job takes the free slot at once too" "$(yn 'started r2 && ! grep -q waiting "$RUN/out.r2"')" "yes"
# The same through pid_start's ps branch (the path without /proc, macOS), forced on any box by AW_HEAVY_TEST_PS_START=1 (#3859).
reset; pst="$(start_of "$live" ps)"; case $pst in *_*) r=0 ;; *) r=1 ;; esac
check "ps branch: the live pid has an lstart token with words joined by _" "$r" "0"
fake "$live" box-wide ',"fifo":1' "$$" "$pst"; JOB_ENV="AW_HEAVY_TEST_PS_START=1"
job p1; wait_for 'waiting p1'; sleep 1.5
check "ps branch: a genuine older waiter (its lstart matches) holds the free slot back" "$(yn '! started p1 && [ ! -d "$LOCKS/heavy-job.lock" ]')" "yes"
check "ps branch: the job's own wait file carries an lstart token" "$(yn 'grep -q "\"start\":\"[A-Z][a-z]*_" "$LOCKS/heavy-wait/$(pid_of p1)" 2> /dev/null')" "yes"
rm -f "$LOCKS/heavy-wait/$live"; wait_for 'started p1'
check "ps branch: the older waiter's file goes, the job starts" "$(yn 'started p1')" "yes"
reset; fake "$live" box-wide ',"fifo":1' "$$" "Mon_Jan_1_00:00:00_2001"; JOB_ENV="AW_HEAVY_TEST_PS_START=1"
job p2; wait_for 'started p2'
check "ps branch: a reused pid's wait file (lstart differs): the job takes the free slot at once" "$(yn 'started p2 && ! grep -q waiting "$RUN/out.p2"')" "yes"
# The pid dies between kill -0 and ps: ps prints nothing, the start is empty, the file holds no one back and the job doesn't die
# (bash before 4.4 exits on "${f[*]}" of an empty array under set -u).
reset; fake "$live" box-wide ',"fifo":1' "$$" "$(start_of "$live" ps)"; JOB_ENV="AW_HEAVY_TEST_PS_START=1 AW_HEAVY_TEST_PATH=$STUB_NOPS:$STUB"
job p3; wait_for 'started p3'; go p3; rc_of p3
check "ps branch, ps prints nothing (the pid just died): the job runs at once and exits 0" "$(yn 'started p3 && ! grep -q waiting "$RUN/out.p3"') $RC" "yes 0"
check "ps branch, ps prints nothing: no unbound-variable error" "$(grep -c 'unbound variable' "$RUN/out.p3")" "0"
reset; fake "$live" box-wide ',"fifo":1' "$$" -; job n; wait_for 'started n'
check "a fifo:1 wait file with no start is ignored" "$(yn 'started n && ! grep -q waiting "$RUN/out.n"')" "yes"
reset; fake "$live" box-wide ""; job j; wait_for 'started j'
check "an older file without fifo:1 (an older heavy.sh) is ignored" "$(yn 'started j')" "yes"
reset; fake "$live" light; job j; wait_for 'started j'
check "a box-wide job ignores an older light waiter" "$(yn 'started j')" "yes"
reset; fake "$live" session ',"fifo":1' 999999; job j; wait_for 'started j'
check "a box-wide job ignores an older session waiter (of another session)" "$(yn 'started j')" "yes"
reset; fake "$live" box-wide; job j --light dotnet build j; wait_for 'started j'
check "a light build ignores an older box-wide waiter while slot 1 is free" "$(yn 'started j')" "yes"

# --- the session pool: same session only ---
reset; fake "$live" session
job s --light bash -c 'touch "$RUN/started.s"'; wait_for 'waiting s'; sleep 1.5
check "session: an older waiter of this session holds the session slot back" "$(yn '! started s')" "yes"
rm -f "$LOCKS/heavy-wait/$live"; wait_for 'started s'
check "session: it goes, the job runs" "$(yn 'started s')" "yes"
reset; fake "$live" session ',"fifo":1' 999999
job s --light bash -c 'touch "$RUN/started.s"'; wait_for 'started s'
check "session: an older waiter of another session holds nothing" "$(yn 'started s')" "yes"

# --- meta skips the queue both ways ---
reset; fake "$live" box-wide; JOB_ENV="AW_HEAVY_PRIORITY=meta"
job m; wait_for 'started m'
check "meta: takes the free slot ahead of an older waiter" "$(yn 'started m')" "yes"
reset; fake "$live" box-wide ',"fifo":1,"meta":1'; job j; wait_for 'started j'
check "nobody waits for an older meta waiter" "$(yn 'started j')" "yes"
reset; job h; wait_for 'started h'; JOB_ENV="AW_HEAVY_PRIORITY=meta"; job m; wait_for 'waiting m'
check "a meta job's wait file carries meta:1" "$(grep -c '"fifo":1,"meta":1}' "$LOCKS/heavy-wait/$(pid_of m)")" "1"

# --- the safety valve and the lock timeout ---
reset; fake "$live" box-wide; JOB_ENV="AW_HEAVY_FIFO_MAX_DEFER=2"
job v; wait_for 'started v'
check "AW_HEAVY_FIFO_MAX_DEFER=2: the waiter stops standing back and runs" "$(yn 'started v')" "yes"
check "it says so once" "$(grep -c 'no longer waiting for older jobs' "$RUN/out.v")" "1"
reset; fake "$live" box-wide; SCRIPT="$ONE"; job o; rc_of o
check "timeout 1 s: standing back counts toward the lock timeout, exit 75" "$RC" "75"

# --- an empty queue adds no wait ---
reset; job e; wait_for 'started e'
check "empty queue: the job starts without waiting" "$(yn 'started e && ! grep -q waiting "$RUN/out.e"')" "yes"

reset
[ "$fail" -eq 0 ] && echo "all heavy-fifo tests passed"
exit "$fail"
