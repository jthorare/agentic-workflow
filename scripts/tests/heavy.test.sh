#!/usr/bin/env bash
# Tests for scripts/heavy.sh (the heavy-job wrapper), pinning what it does today, odd parts included (the odd ones are marked
# "pinned as is"; changing them is a separate issue). Everything runs against a temp AW_STATE_DIR (HOME is never changed;
# the wrapper only reads $HOME/.dotnet for PATH), a stub `dotnet` ahead of PATH through AW_HEAVY_TEST_PATH, and
# AW_HEAVY_SESSION_PID (this shell, or a dead pid, or a sleeping stand-in session). Nothing real is built or tested.
# Covers: usage (exit 2); the exit code of the job passes through; the lock files (heavy-session/<pid>.lock, heavy-job.lock
# and .k, the owner line, released on exit, a dead owner reclaimed, an ownerless dir never); the cap file heavy-slots-cap;
# the memory guard (AW_HEAVY_MIN_FREE_MB and _STEP_MB, Linux /proc/meminfo only); the meta reserve heavy-meta-reserve (capped at slots-1, #2060) and
# AW_HEAVY_PRIORITY=meta; the lock timeout (75, on a copy of the script with TIMEOUT=0: the real one is a constant 5400 s);
# the session dying (124); INT and TERM (130); the job launched through `systemd-run --user --scope -p OOMPolicy=continue` when
# that works, and the old direct launch when it doesn't (#2931, stub systemd-run); a nested run under AW_HEAVY_LOCK_HELD=1 taking no lock; --light, --expensive
# and the auto classification; the --timeout added to `dotnet test`.
# Run: bash scripts/tests/heavy.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$DIR/lib/test-portable.sh"   # timeout for macOS
HEAVY="$DIR/heavy.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
shown() { printf '%s\n' "$1" | sed 's/^/  in: /'; }   # every line prefixed: the CI runner keeps only "  in:" lines of a failing suite
has() { # has <name> <text> <fixed string>
  if printf '%s' "$2" | grep -qF -- "$3"; then echo "ok - $1"; else echo "not ok - $1"; echo "  missing: $3"; shown "$2"; fail=1; fi
}
lacks() {
  if printf '%s' "$2" | grep -qF -- "$3"; then echo "not ok - $1"; echo "  unexpected: $3"; shown "$2"; fail=1; else echo "ok - $1"; fi
}
WORK="$(mktemp -d)"
bgpids=()
cleanup() { for p in ${bgpids[@]+"${bgpids[@]}"}; do kill "$p" 2> /dev/null; done; find "$WORK" -delete 2> /dev/null; }   # by recorded pid only
trap cleanup EXIT

for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY)'); do unset "$v"; done   # this suite may itself run under a heavy job
unset DOTNET_PROCESSOR_COUNT MSBUILDDISABLENODEREUSE UseSharedCompilation
STATE="$WORK/state"; LOCKS="$STATE/locks"
export AW_STATE_DIR="$STATE" AW_HEAVY_SESSION_PID="$$"
STUB="$WORK/stub"; mkdir -p "$STUB"
export AW_HEAVY_TEST_PATH="$STUB"
# The caller's cgroup for the scope bind (#2931): a non-scope line by default, so a suite run inside a real tmux scope binds nothing.
echo '0::/test.slice' > "$WORK/cgroup"; export AW_HEAVY_TEST_CGROUP="$WORK/cgroup"
cat > "$STUB/dotnet" <<'EOF'
#!/usr/bin/env bash
echo "dotnet mode=${AW_HEAVY_MODE:-none} args=$*"
EOF
chmod +x "$STUB/dotnet"
# Stub systemd-run (#2931): logs one line per call to $WORK/systemd-run.log. HEAVY_STUB_SCOPE=ok behaves like a working
# `--scope` (the command runs in place, same pid); anything else (the default) fails like a box with no user bus, so every
# row that does not ask for a scope takes the fallback path and the suite never creates a real scope on a systemd box.
cat > "$STUB/systemd-run" <<'EOF'
#!/usr/bin/env bash
echo "systemd-run $*" >> "$WORK/systemd-run.log"
if [ "${HEAVY_STUB_SCOPE:-}" = ok ]; then
  while [ "$#" -gt 0 ] && [ "$1" != "--" ]; do shift; done
  shift
  exec "$@"
fi
echo "Failed to connect to user scope bus via local transport" >&2
exit 1
EOF
chmod +x "$STUB/systemd-run"
export WORK
# Copies of the wrapper with the 5400 s lock timeout cut (TIMEOUT is a constant there, no variable sets it): FAST gives up on the
# first pass (0 s), ONE after one counted second.
mkdir -p "$WORK/scripts"; ln -s "$DIR/../config" "$WORK/config"   # the copies find config/lib/heavy-config.sh beside their own folder, as the wrapper does
FAST="$WORK/scripts/heavy-fast.sh"; sed 's/^TIMEOUT=5400$/TIMEOUT=0/' "$HEAVY" > "$FAST"
ONE="$WORK/scripts/heavy-one.sh"; sed 's/^TIMEOUT=5400$/TIMEOUT=1/' "$HEAVY" > "$ONE"
check "the copies differ from the wrapper only in TIMEOUT" "$(diff "$HEAVY" "$FAST" | grep -c '^>')$(diff "$HEAVY" "$ONE" | grep -c '^>')" "11"

# The wrapper's watchdog leaves a `sleep 5` holding its stdout after the job ends, so a $(...) around it would wait up to 5 s for
# the pipe to close (pinned as is). H and F (the wrapper, and the TIMEOUT=0 copy) write to a file and cat it: the sleep holds the file.
H() { local f; f="$(mktemp "$WORK/h.XXXXXX")"; bash "$HEAVY" "$@" > "$f" 2>&1; local rc=$?; cat "$f"; return $rc; }
F() { local f; f="$(mktemp "$WORK/f.XXXXXX")"; bash "$FAST" "$@" > "$f" 2>&1; local rc=$?; cat "$f"; return $rc; }
reset() { find "$STATE" -mindepth 1 -delete 2> /dev/null; mkdir -p "$STATE"; }
# A pid above any pid_max (Linux 4194304, macOS 99998): never a live process. A pid of an exited child can be reused by the
# time a row reads it on a busy runner ("a lock whose owner pid is dead is reclaimed" kept the slot on macos-15, #2757).
DEAD=99999999
# hold <lock dir> [pid]: a lock dir some live (or the given) pid owns, as another job would leave it.
hold() { mkdir -p "$1"; echo "${2:-$$} 2026-01-01T00:00:00Z session=x held" > "$1/owner"; }
wait_file() { local i; for i in $(seq 1 60); do [ -s "$1" ] && return 0; sleep 0.1; done; return 1; }
gone() { local i; for i in $(seq 1 40); do kill -0 "$1" 2> /dev/null || return 0; sleep 0.1; done; return 1; }
have_meminfo=0; [ -r /proc/meminfo ] && have_meminfo=1
SESSLOCK="$LOCKS/heavy-session/$$.lock"

# --- usage ---
out="$(H 2>&1)"; rc=$?
check "no command: exit 2" "$rc" "2"
has "no command: the usage line" "$out" "usage: heavy [--expensive|--light] <cmd...>"
H --light > /dev/null 2>&1; check "a flag and no command: exit 2" "$?" "2"
H --expensive > /dev/null 2>&1; check "--expensive and no command: exit 2" "$?" "2"
check "a usage error creates no lock dir" "$([ -e "$LOCKS" ] && echo yes || echo no)" "no"
AW_HEAVY_LOCK_HELD=1 H > /dev/null 2>&1; check "usage is checked before the nested shortcut" "$?" "2"

# --- the job's output and exit code pass through ---
check "stdout passes through" "$(H --light echo hello)" "hello"
H --light sh -c 'exit 7' > /dev/null 2>&1; check "the job's exit code is the wrapper's" "$?" "7"
H --light no-such-command-here > /dev/null 2>&1; check "a command that does not exist: 127" "$?" "127"
out="$(H --light --expensive echo x 2>&1)"; rc=$?
# setsid refuses the unknown option (1); without setsid (macOS) the perl fallback can't exec a `--expensive` command (127).
if command -v setsid > /dev/null 2>&1; then want=1; else want=127; fi
check "a second flag is taken as the command (pinned as is): setsid or perl rejects it, exit $want" "$rc" "$want"
check "a second flag is taken as the command: echo never runs" "$(printf '%s\n' "$out" | grep -cx 'x')" "0"
reset

# --- lock files while a job runs, and after ---
child='echo "ppid=$PPID"; echo "mode=${AW_HEAVY_MODE-}"; echo "held=${AW_HEAVY_LOCK_HELD-}"; echo "slot=${AW_HEAVY_SLOT-unset}"; echo "cpus=${DOTNET_PROCESSOR_COUNT-}"; echo "nodereuse=${MSBUILDDISABLENODEREUSE-}"; echo "pgid=$(ps -o pgid= -p $$ | tr -d " ")/$$"; ls "$1"; for f in "$2"/owner "$3"/owner; do [ -f "$f" ] && echo "owner $(cat "$f")"; done; true'
out="$(H --light bash -c "$child" _ "$LOCKS" "$SESSLOCK" "$LOCKS/heavy-job.lock" 2>&1)"
has "light: the session lock is heavy-session/<pid>.lock" "$out" "heavy-session"
check "light: no box-wide lock is taken" "$(printf '%s\n' "$out" | grep -c '^heavy-job')" "0"
has "light: mode is exported" "$out" "mode=light"
has "light: AW_HEAVY_LOCK_HELD=1 is exported" "$out" "held=1"
has "light: AW_HEAVY_SLOT is not set" "$out" "slot=unset"
has "default DOTNET_PROCESSOR_COUNT is 4" "$out" "cpus=4"
has "MSBuild node reuse is off" "$out" "nodereuse=1"
# #3178: no shared Roslyn server; a caller's own value is kept
ucmd='echo "usc=${UseSharedCompilation-unset}"'
check "UseSharedCompilation=false for a light job (#3178)" "$(unset UseSharedCompilation; H --light bash -c "$ucmd" 2>&1)" "usc=false"
check "UseSharedCompilation=false for an expensive job (#3178)" "$(unset UseSharedCompilation; H --expensive bash -c "$ucmd" 2>&1)" "usc=false"
check "a caller's UseSharedCompilation=true is kept (#3178)" "$(UseSharedCompilation=true H --light bash -c "$ucmd" 2>&1)" "usc=true"
pg="$(printf '%s\n' "$out" | sed -n 's/^pgid=//p')"
check "the job is its own process group leader" "${pg%%/*}" "${pg##*/}"
wp="$(printf '%s\n' "$out" | sed -n 's/^ppid=//p')"
own="$(printf '%s\n' "$out" | sed -n 's/^owner //p')"
check "owner line: first word is the wrapper's pid (the job's parent)" "${own%% *}" "$wp"
check "owner line: pid, UTC time, session=<pid>, then the acquire label, lock path and slot count, then the command (pinned as is)" \
  "$(printf '%s' "$own" | sed -E "s/^[0-9]+ [0-9T:-]+Z session=$$ session $(printf '%s' "$SESSLOCK" | sed 's/[.\/]/\\&/g') 1 bash -c .*/OK/")" "OK"
check "released on exit: no session lock dir left" "$(ls "$LOCKS/heavy-session" | wc -l | tr -d ' ')" "0"
check "released on exit: no box-wide lock either" "$(ls "$LOCKS" | grep -c 'heavy-job')" "0"

out="$(H --expensive bash -c "$child" _ "$LOCKS" "$SESSLOCK" "$LOCKS/heavy-job.lock" 2>&1)"
has "expensive: mode" "$out" "mode=expensive"
has "expensive: heavy-job.lock is slot 1" "$out" "heavy-job.lock"
has "expensive: AW_HEAVY_SLOT names the slot dir" "$out" "slot=$LOCKS/heavy-job.lock"
has "expensive: the box-wide owner line carries the box-wide label (pinned as is)" "$out" "session=$$ box-wide $LOCKS/heavy-job.lock 1 bash -c"
check "expensive: both locks released" "$(find "$LOCKS" -name '*.lock*' -type d | wc -l | tr -d ' ')" "0"
out="$(AW_HEAVY_CPUS=2 H --light bash -c 'echo $DOTNET_PROCESSOR_COUNT')"; check "AW_HEAVY_CPUS sets DOTNET_PROCESSOR_COUNT" "$out" "2"
JL="$WORK/other/job.lock"
out="$(AW_HEAVY_JOB_LOCK="$JL" H --expensive bash -c 'ls "$1"' _ "$WORK/other")"
check "AW_HEAVY_JOB_LOCK moves the box-wide lock" "$out" "job.lock"
check "...and the default one is not created" "$(ls "$LOCKS" | grep -c 'heavy-job')" "0"

# --- the session: which pid names the lock ---
cat > "$STUB/ps" <<'EOF'
#!/usr/bin/env bash
case "$*" in
  *"comm="*) echo "${PS_COMM:-bash}" ;;
  *"args="*) echo "${PS_ARGS:-}" ;;
  *"ppid="*) echo 1 ;;
esac
EOF
chmod +x "$STUB/ps"
# heavy's parent is the bash -c below (not last in its list, so it does not exec): me=<its pid>
sess_of() { env -u AW_HEAVY_SESSION_PID "$@" bash -c 'bash "$1" --light bash -c "ls \"\$1/heavy-session\"" _ "$2"; echo me=$$' _ "$HEAVY" "$LOCKS" > "$WORK/sess.out" 2>&1; cat "$WORK/sess.out"; }
out="$(sess_of PS_COMM=claude)"; me="$(printf '%s\n' "$out" | sed -n 's/^me=//p')"
has "a claude ancestor names the session" "$out" "$me.lock"
out="$(sess_of PS_COMM=node PS_ARGS="node /usr/lib/claude-code/cli.js")"; me="$(printf '%s\n' "$out" | sed -n 's/^me=//p')"
has "a node process running claude-code counts too" "$out" "$me.lock"
out="$(sess_of PS_COMM=bash)"; me="$(printf '%s\n' "$out" | sed -n 's/^me=//p')"
has "no claude ancestor: falls back to the parent pid" "$out" "$me.lock"
sleep 300 & livep=$!; bgpids+=("$livep")
out="$(AW_HEAVY_SESSION_PID="$livep" H --light bash -c 'ls "$1/heavy-session"' _ "$LOCKS")"
check "AW_HEAVY_SESSION_PID wins" "$out" "$livep.lock"
rm -f "$STUB/ps"
reset

# --- auto classification, --light and --expensive ---
mode_of() { H "$@" 2>&1 | sed -n 's/.*mode=\([a-z]*\).*/\1/p'; }
sh_mode='echo mode=$AW_HEAVY_MODE'
for sub in build publish format run; do check "dotnet $sub is expensive" "$(mode_of dotnet "$sub" x)" "expensive"; done
check "dotnet test is expensive" "$(mode_of dotnet test x)" "expensive"
check "dotnet test --no-build is light" "$(mode_of dotnet test x --no-build)" "light"
check "dotnet --version is light" "$(mode_of dotnet --version)" "light"
check "a path-qualified dotnet is not matched (pinned as is): light" "$(mode_of "$STUB/dotnet" build x)" "light"
for w in "npm ci" "npx vitest"; do
  # shellcheck disable=SC2086
  check "$w anywhere in the args is expensive" "$(mode_of sh -c "$sh_mode" x $w)" "expensive"
done
for w in AgenticWorkflow.slnx run-aot-smoke.sh playwright; do
  check "$w anywhere in the args is expensive" "$(mode_of sh -c "$sh_mode" x "$w")" "expensive"
done
check "e2e.sh as its own word is expensive" "$(mode_of sh -c "$sh_mode" x e2e.sh y)" "expensive"
check "a plain script is light" "$(mode_of sh -c "$sh_mode")" "light"
check "the words are matched over all args, not the command (pinned as is): an echo of dotnet build is expensive" "$(mode_of sh -c "$sh_mode" x dotnet build)" "expensive"
check "--light forces light on a build" "$(mode_of --light dotnet build x)" "light"
check "--expensive forces expensive on a plain script" "$(mode_of --expensive sh -c "$sh_mode")" "expensive"
check "a light job takes no box-wide slot" "$(H --light bash -c 'ls "$1" | grep -c heavy-job' _ "$LOCKS")" "0"

# --- the --timeout added to `dotnet test` ---
args_of() { H "$@" 2>&1 | sed -n 's/.*args=//p'; }
check "dotnet test gets --timeout 40m (45 - 5)" "$(args_of dotnet test x)" "test x --timeout 40m"
check "--no-build gets it too" "$(args_of dotnet test --no-build)" "test --no-build --timeout 40m"
check "AW_HEAVY_RUN_MAX=30: 25m" "$(AW_HEAVY_RUN_MAX=30 args_of dotnet test x)" "test x --timeout 25m"
check "AW_HEAVY_RUN_MAX=6: 1m" "$(AW_HEAVY_RUN_MAX=6 args_of dotnet test x)" "test x --timeout 1m"
check "AW_HEAVY_RUN_MAX=5: 5m (not 0m)" "$(AW_HEAVY_RUN_MAX=5 args_of dotnet test x)" "test x --timeout 5m"
check "AW_HEAVY_RUN_MAX=0: no limit, nothing added" "$(AW_HEAVY_RUN_MAX=0 args_of dotnet test x)" "test x"
check "AW_HEAVY_RUN_MAX=abc: the default 45" "$(AW_HEAVY_RUN_MAX=abc args_of dotnet test x)" "test x --timeout 40m"
check "the caller's --timeout stays alone" "$(args_of dotnet test --timeout 10m)" "test --timeout 10m"
check "the caller's --timeout=... stays alone" "$(args_of dotnet test --timeout=10m)" "test --timeout=10m"
check "dotnet build gets no --timeout" "$(args_of dotnet build x)" "build x"
check "a path-qualified dotnet test gets it, though it is classified light (pinned as is)" "$(args_of "$STUB/dotnet" test x)" "test x --timeout 40m"

# --- the lock timeout (75), on the TIMEOUT=0 copy ---
reset
hold "$SESSLOCK"
out="$(F --light echo ran 2>&1)"; rc=$?
check "a held session lock: timeout exits 75" "$rc" "75"
has "the wait is announced once" "$out" "heavy: waiting for the session lock (1 slot(s), $SESSLOCK)"
has "the timeout sentence" "$out" "heavy: session lock timeout ($SESSLOCK)"
lacks "the job never ran" "$out" "ran"
check "the other session's lock is left alone" "$([ -f "$SESSLOCK/owner" ] && echo kept || echo gone)" "kept"
out="$(AW_SESSION_HEAVY_SLOTS=2 F --light bash -c 'ls "$1"' _ "$LOCKS/heavy-session" 2>&1)"
has "AW_SESSION_HEAVY_SLOTS=2: a second job takes <pid>.lock.2" "$out" "$$.lock.2"
out="$(AW_SESSION_HEAVY_SLOTS=two F --light echo ran 2>&1)"; check "AW_SESSION_HEAVY_SLOTS=two means 1" "$?" "75"
reset
mkdir -p "$SESSLOCK"   # a dir with no owner file: never reclaimed (pinned as is)
F --light echo ran > /dev/null 2>&1; check "a lock dir with no owner file is never reclaimed: 75" "$?" "75"
reset
hold "$SESSLOCK" "$DEAD"
check "a lock whose owner pid is dead is reclaimed" "$(F --light echo ran 2>&1)" "ran"
check "...and released after" "$([ -e "$SESSLOCK" ] && echo left || echo clean)" "clean"
reset
hold "$LOCKS/heavy-job.lock" "$DEAD"
check "a dead box-wide owner is reclaimed too" "$(F --expensive echo ran 2>&1)" "ran"
reset
hold "$LOCKS/heavy-job.lock"
out="$(F --expensive echo ran 2>&1)"; rc=$?
check "a held box-wide lock: timeout exits 75" "$rc" "75"
has "the box-wide wait is announced with its base path" "$out" "heavy: waiting for the box-wide lock (1 slot(s), $LOCKS/heavy-job.lock)"
has "the box-wide timeout sentence" "$out" "heavy: box-wide lock timeout ($LOCKS/heavy-job.lock)"
check "the session lock taken first is released on the timeout" "$([ -e "$SESSLOCK" ] && echo left || echo clean)" "clean"
out="$(AW_HEAVY_SLOTS=two F --expensive echo ran 2>&1)"; has "AW_HEAVY_SLOTS=two means 1" "$out" "(1 slot(s)"
out="$(AW_HEAVY_SLOTS=0 F --expensive echo ran 2>&1)"; has "AW_HEAVY_SLOTS=0 means 1" "$out" "(1 slot(s)"

# --- the memory guard and the slot count (Linux: it reads /proc/meminfo) ---
if [ "$have_meminfo" = 1 ]; then
  reset
  hold "$LOCKS/heavy-job.lock"
  slot_of() { H --expensive bash -c 'echo slot=$AW_HEAVY_SLOT' 2>&1 | sed -n 's/^slot=//p'; }
  check "AW_HEAVY_SLOTS=2 with enough free memory: slot 2 is taken" "$(AW_HEAVY_SLOTS=2 AW_HEAVY_MIN_FREE_MB=0 slot_of)" "$LOCKS/heavy-job.lock.2"
  out="$(AW_HEAVY_SLOTS=2 AW_HEAVY_MIN_FREE_MB=999999999 F --expensive echo ran 2>&1)"; rc=$?
  check "not enough free memory: slot 2 is refused, the job queues for slot 1 and times out" "$rc" "75"
  has "...the wait names both slots" "$out" "(2 slot(s), $LOCKS/heavy-job.lock)"
  check "free memory is not asked for slot 1" "$(reset; AW_HEAVY_MIN_FREE_MB=999999999 H --expensive echo ran)" "ran"
  reset; hold "$LOCKS/heavy-job.lock"; hold "$LOCKS/heavy-job.lock.2"
  out="$(AW_HEAVY_SLOTS=3 AW_HEAVY_MIN_FREE_MB=0 AW_HEAVY_MIN_FREE_STEP_MB=999999999 F --expensive echo ran 2>&1)"; rc=$?
  check "slot 3 needs MIN + STEP free: refused when STEP is huge" "$rc" "75"
  check "...taken when STEP is 0" "$(AW_HEAVY_SLOTS=3 AW_HEAVY_MIN_FREE_MB=0 AW_HEAVY_MIN_FREE_STEP_MB=0 slot_of)" "$LOCKS/heavy-job.lock.3"
  check "a garbage STEP falls back to 4096 (slot 3 refused when MIN is huge)" \
    "$(AW_HEAVY_SLOTS=3 AW_HEAVY_MIN_FREE_MB=999999999 AW_HEAVY_MIN_FREE_STEP_MB=x F --expensive echo ran > /dev/null 2>&1; echo $?)" "75"

  # the cap file: the box-wide slot count, whatever AW_HEAVY_SLOTS says
  reset; hold "$LOCKS/heavy-job.lock"
  echo 2 > "$STATE/heavy-slots-cap"
  check "heavy-slots-cap=2 beats AW_HEAVY_SLOTS=1" "$(AW_HEAVY_SLOTS=1 AW_HEAVY_MIN_FREE_MB=0 slot_of)" "$LOCKS/heavy-job.lock.2"
  check "...with no env at all" "$(AW_HEAVY_MIN_FREE_MB=0 slot_of)" "$LOCKS/heavy-job.lock.2"
  for bad in 0 abc ""; do
    printf '%s' "$bad" > "$STATE/heavy-slots-cap"
    out="$(AW_HEAVY_SLOTS=1 AW_HEAVY_MIN_FREE_MB=0 F --expensive echo ran 2>&1)"; rc=$?
    check "heavy-slots-cap='$bad' is ignored (env says 1 slot): 75" "$rc" "75"
  done
  echo 1 > "$STATE/heavy-slots-cap"
  out="$(AW_HEAVY_SLOTS=3 F --expensive echo ran 2>&1)"; has "heavy-slots-cap=1 beats AW_HEAVY_SLOTS=3" "$out" "(1 slot(s)"
  # the cap file with a newline, as `echo` writes it, is read whole
  echo 3 > "$STATE/heavy-slots-cap"
  out="$(AW_HEAVY_MIN_FREE_MB=999999999 F --expensive echo ran 2>&1)"; has "heavy-slots-cap=3 (with its newline) sets 3 slots" "$out" "(3 slot(s)"

  # --- the meta reserve ---
  reset
  echo 2 > "$STATE/heavy-slots-cap"
  echo 1 > "$STATE/heavy-meta-reserve"
  check "reserve 1 of 2: a lane job takes slot 2" "$(AW_HEAVY_MIN_FREE_MB=0 slot_of)" "$LOCKS/heavy-job.lock.2"
  check "reserve 1 of 2: a meta job takes slot 1" "$(AW_HEAVY_PRIORITY=meta AW_HEAVY_MIN_FREE_MB=0 slot_of)" "$LOCKS/heavy-job.lock"
  echo 2 > "$STATE/heavy-meta-reserve"
  out="$(AW_HEAVY_MIN_FREE_MB=0 H --light echo light-ok 2>&1)"; check "a light job ignores the reserve" "$out" "light-ok"
  check "reserve 2 of 2: a meta job still runs" "$(AW_HEAVY_PRIORITY=meta AW_HEAVY_MIN_FREE_MB=0 slot_of)" "$LOCKS/heavy-job.lock"
  # the reserve is capped at the slot count minus one (#2060, owner 2026-11-05), so a lane job always has a slot: bounded
  # from outside, since a job blocked only by the reserve never times out
  out="$(AW_HEAVY_PRIORITY=other AW_HEAVY_MIN_FREE_MB=0 timeout 5 bash "$HEAVY" --expensive bash -c 'echo slot=$AW_HEAVY_SLOT' 2>&1 | sed -n 's/^slot=//p')"
  check "reserve 2 of 2 is capped to 1: a lane job (AW_HEAVY_PRIORITY=other is not meta) takes slot 2" "$out" "$LOCKS/heavy-job.lock.2"
  # the reserve is also capped at the slots free memory allows minus one: memory for slot 1 only means no reserve
  echo 1 > "$STATE/heavy-meta-reserve"
  out="$(AW_HEAVY_MIN_FREE_MB=999999999 timeout 5 bash "$HEAVY" --expensive bash -c 'echo slot=$AW_HEAVY_SLOT' 2>&1 | sed -n 's/^slot=//p')"
  check "reserve 1 of 2, memory for slot 1 only: a lane job takes slot 1" "$out" "$LOCKS/heavy-job.lock"
  out="$(AW_HEAVY_MIN_FREE_MB=0 timeout 5 bash "$HEAVY" --expensive bash -c 'echo slot=$AW_HEAVY_SLOT' 2>&1 | sed -n 's/^slot=//p')"
  check "reserve 1 of 2, memory for both: a lane job still takes slot 2" "$out" "$LOCKS/heavy-job.lock.2"
  # blocked only by the reserve (slot 1 free, slot 2 busy): the wait is not counted; the file is re-read each second
  echo 1 > "$STATE/heavy-meta-reserve"; hold "$LOCKS/heavy-job.lock.2"
  AW_HEAVY_MIN_FREE_MB=0 bash "$ONE" --expensive echo ran > "$WORK/res.out" 2> "$WORK/res.err" &
  rp=$!; bgpids+=("$rp")
  sleep 2.3
  check "reserved: a lane job is still waiting after 2 s although TIMEOUT is 1 s" "$(kill -0 "$rp" 2> /dev/null && echo waiting || echo exited)" "waiting"
  has "reserved: it announced the wait" "$(cat "$WORK/res.err")" "heavy: waiting for the box-wide lock"
  lacks "reserved: no timeout sentence" "$(cat "$WORK/res.err")" "timeout"
  echo 0 > "$STATE/heavy-meta-reserve"
  wait "$rp"; rc=$?
  check "reserve drained: the waiting job runs on slot 1 and exits 0" "$rc" "0"
  check "...and printed its output" "$(cat "$WORK/res.out")" "ran"
else
  echo "ok - (skipped: no /proc/meminfo; the memory guard fails closed there, so slots above 1 are never taken - pinned as is)"
fi

# --- the meta reserve with one slot: capped at the slot count minus one (#2060, owner 2026-11-05), so it is 0 ---
reset
echo 1 > "$STATE/heavy-meta-reserve"
check "reserve 1 of 1: a meta job runs" "$(AW_HEAVY_PRIORITY=meta H --expensive echo ran 2>&1)" "ran"
check "reserve 1 of 1 is capped to 0: a lane job runs (bounded from outside)" "$(timeout 5 bash "$HEAVY" --expensive echo ran 2>&1)" "ran"
echo 9 > "$STATE/heavy-meta-reserve"
check "a reserve above the slot count: a meta job runs" "$(AW_HEAVY_PRIORITY=meta H --expensive echo ran 2>&1)" "ran"
check "a reserve above the slot count is capped to 0: a lane job runs (bounded from outside)" "$(timeout 5 bash "$HEAVY" --expensive echo ran 2>&1)" "ran"
echo abc > "$STATE/heavy-meta-reserve"
check "a garbage reserve is 0" "$(H --expensive echo ran 2>&1)" "ran"

# --- the watchdog writes nothing to heavy's stderr (#2884) ---
# Killed right after its fork, the watchdog subshell can make bash print "run_pending_traps: signal handler is SIG_DFL" on its
# stderr, and every $(H ... 2>&1) check read "ran" plus that line (macOS main, 978acd5 and 796333c). The race is too narrow to
# hit on purpose, so a `sleep` on PATH that writes to stderr stands in for it: the watchdog calls sleep while the job runs.
reset
mkdir -p "$WORK/dogbin"
printf '#!/bin/sh\necho "watchdog-stderr" >&2\nexec %s "$@"\n' "$(command -v sleep)" > "$WORK/dogbin/sleep"
chmod +x "$WORK/dogbin/sleep"
check "nothing the watchdog writes to stderr reaches the job's output" \
  "$(PATH="$WORK/dogbin:$PATH" H --expensive perl -e 'select(undef, undef, undef, 0.5); print "ran\n"' 2>&1)" "ran"

# --- nested: a run under AW_HEAVY_LOCK_HELD=1 takes no lock ---
reset
out="$(AW_HEAVY_LOCK_HELD=1 H --expensive bash -c 'echo "mode=${AW_HEAVY_MODE-none}"')"
check "nested: the command runs straight through (the flag is still consumed)" "$out" "mode=none"
check "nested: no lock dir is even created" "$([ -e "$LOCKS" ] && echo yes || echo no)" "no"
AW_HEAVY_LOCK_HELD=1 H sh -c 'exit 9' > /dev/null 2>&1; check "nested: the exit code passes through" "$?" "9"
hold "$SESSLOCK"; hold "$LOCKS/heavy-job.lock"
check "nested: held locks are not waited on" "$(AW_HEAVY_LOCK_HELD=1 F --expensive echo ran 2>&1)" "ran"
reset
out="$(H --light bash "$HEAVY" --expensive bash -c 'ls "$1" "$1/heavy-session"' _ "$LOCKS" 2>&1)"
lacks "heavy run inside heavy: the inner one takes no box-wide slot" "$out" "heavy-job"
check "heavy run inside heavy: and one session lock only" "$(printf '%s\n' "$out" | grep -c '\.lock')" "1"
check "AW_HEAVY_LOCK_HELD=2 is not nested (pinned as is): it takes the locks" \
  "$(AW_HEAVY_LOCK_HELD=2 H --expensive bash -c 'echo mode=$AW_HEAVY_MODE')" "mode=expensive"

# --- the session ending: the watchdog kills the job, exit 124 ---
reset
t0=$SECONDS
out="$(AW_HEAVY_SESSION_PID="$DEAD" H --light sleep 30 2>&1)"; rc=$?
check "a dead session: the job is killed and the wrapper exits 124" "$rc" "124"
has "...with the reason and the command" "$out" "heavy: session $DEAD ended, killed: sleep 30"
check "...within seconds" "$([ $((SECONDS - t0)) -lt 15 ] && echo fast || echo slow)" "fast"
check "...locks released" "$(find "$LOCKS" -name '*.lock*' -type d | wc -l | tr -d ' ')" "0"

# --- INT and TERM: the job's group is stopped, locks released, exit 130 ---
cat > "$WORK/job.sh" <<'EOF'
echo $$ > "$1"; exec sleep 60
EOF
for sig in INT TERM; do
  reset
  # perl resets SIGINT and SIGTERM: a background job of a non-interactive shell would otherwise start with INT ignored,
  # and a suite started with TERM ignored would hand that on too (#2377)
  perl -e '$SIG{INT}="DEFAULT"; $SIG{TERM}="DEFAULT"; exec @ARGV' bash "$HEAVY" --expensive bash "$WORK/job.sh" "$WORK/job.pid" > /dev/null 2> "$WORK/sig.err" &
  hp=$!; bgpids+=("$hp")
  wait_file "$WORK/job.pid" || echo "  (job did not start)"
  jp="$(cat "$WORK/job.pid" 2> /dev/null)"
  check "$sig: both locks are held while the job runs" "$(find "$LOCKS" -name '*.lock*' -type d | wc -l | tr -d ' ')" "2"
  kill -"$sig" "$hp"; wait "$hp"; rc=$?
  check "$sig: exit 130" "$rc" "130"
  check "$sig: the job is gone" "$(gone "${jp:-0}" && echo gone || echo alive)" "gone"
  check "$sig: locks released" "$(find "$LOCKS" -name '*.lock*' -type d | wc -l | tr -d ' ')" "0"
  rm -f "$WORK/job.pid"
done

# --- the job runs in its own systemd scope (#2931) ---
# An OOM kill inside the pane's cgroup scope makes systemd stop the whole pane (the agent session). With a working
# systemd-run the job goes through `systemd-run --user --scope -p OOMPolicy=continue`; without one, the direct launch stays.
SCOPE_LOG="$WORK/systemd-run.log"
reset; : > "$SCOPE_LOG"
out="$(HEAVY_STUB_SCOPE=ok H --light dotnet build x)"; rc=$?
slog="$(cat "$SCOPE_LOG")"
has "scope: the job goes through systemd-run --user --scope" "$slog" "--user --scope"
has "scope: OOMPolicy=continue is set" "$slog" "-p OOMPolicy=continue"
check "scope: a call ends with the job after --" "$(printf '%s\n' "$slog" | grep -c -- '-- dotnet build x$')" "1"
has "scope: the job's output still appears" "$out" "dotnet mode=light args=build x"
check "scope: exit 0" "$rc" "0"
check "scope: no bind on a non-scope cgroup" "$(grep -c 'BindsTo' "$SCOPE_LOG")" "0"

# Bind to the caller's pane scope, only when it belongs to the user manager.
for row in "bind: a user-manager scope|0::/user.slice/user-1000.slice/user@1000.service/app.slice/tmux-spawn-abc.scope|1" \
           "bind: a login session scope|0::/user.slice/user-1000.slice/session-3.scope|0" \
           "bind: a user-manager service|0::/user.slice/user-1000.slice/user@1000.service/app.slice/app.service|0"; do
  IFS='|' read -r name line want <<< "$row"
  reset; : > "$SCOPE_LOG"; echo "$line" > "$WORK/cgroup.row"
  HEAVY_STUB_SCOPE=ok AW_HEAVY_TEST_CGROUP="$WORK/cgroup.row" H --light dotnet build x > /dev/null 2>&1
  job="$(grep -- '-- dotnet build x$' "$SCOPE_LOG")"
  check "$name: BindsTo on the job line" "$(printf '%s\n' "$job" | grep -c -- '-p BindsTo=tmux-spawn-abc.scope')" "$want"
  check "$name: After on the job line" "$(printf '%s\n' "$job" | grep -c -- '-p After=tmux-spawn-abc.scope')" "$want"
done

reset; : > "$SCOPE_LOG"
cat > "$STUB/exit3" <<'EOF'
#!/usr/bin/env bash
echo "exit3 ran"; exit 3
EOF
chmod +x "$STUB/exit3"
HEAVY_STUB_SCOPE=ok H --light exit3 > /dev/null 2>&1; rc=$?
check "scope: the job's exit code (3) passes through" "$rc" "3"
check "scope: ...and it ran through systemd-run" "$(grep -c -- '-- exit3$' "$SCOPE_LOG")" "1"

reset; : > "$SCOPE_LOG"
rm -f "$WORK/scope-job.pid"
cat > "$WORK/scope-job.sh" <<'EOF'
echo $$ > "$1"; exec sleep 30
EOF
# A stand-in session that dies once the job has started: with a session dead from the start, the watchdog can kill the job
# under load before it writes its pid file, and `gone 0` then reads as alive.
sleep 600 & sp=$!; bgpids+=("$sp")
t0=$SECONDS
HEAVY_STUB_SCOPE=ok AW_HEAVY_SESSION_PID="$sp" bash "$HEAVY" --light bash "$WORK/scope-job.sh" "$WORK/scope-job.pid" > "$WORK/scope.out" 2>&1 &
hp=$!; bgpids+=("$hp")
wait_file "$WORK/scope-job.pid" || echo "  (job did not start)"
jp="$(cat "$WORK/scope-job.pid" 2> /dev/null)"
kill "$sp"; wait "$sp" 2> /dev/null
wait "$hp"; rc=$?
check "scope: a dead session still gets the job killed, exit 124" "$rc" "124"
check "scope: ...the job is gone" "$(gone "${jp:-0}" && echo gone || echo alive)" "gone"
check "scope: ...within seconds" "$([ $((SECONDS - t0)) -lt 15 ] && echo fast || echo slow)" "fast"
check "scope: ...and it ran through systemd-run" "$(grep -c -- '-- bash .*scope-job.sh' "$SCOPE_LOG")" "1"

reset; : > "$SCOPE_LOG"
out="$(H --light dotnet build x)"; rc=$?
has "no scope (systemd-run fails): the job still runs" "$out" "dotnet mode=light args=build x"
check "no scope: exit 0" "$rc" "0"
check "no scope: no systemd-run call ends with the job" "$(grep -c -- '-- dotnet build x$' "$SCOPE_LOG")" "0"
reset

[ "$fail" = 0 ] && echo "all heavy tests passed" || { echo "heavy tests FAILED"; exit 1; }
