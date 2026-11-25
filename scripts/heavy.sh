#!/usr/bin/env bash
# The whole script is one { } block, so bash parses all of it before running: editing this file while a job
# waits for a slot can't break that job. Edit it by writing a new file and mv-ing it over this one.
{
# heavy <cmd...> — run a build/test/install under the heavy-job locks (owner, 2026-10-14, after the box crashed under load):
#   1. Per session: one heavy job at a time. A session is the nearest `claude` ancestor process (a lane session and all
#      its subagents share it). Lock: <locks>/heavy-session/<pid>.lock (AW_SESSION_HEAVY_SLOTS, default 1).
#   2. Box-wide, for expensive jobs only: AW_HEAVY_SLOTS slots (default 1) of <locks>/heavy-job.lock (slot 1 is the
#      bare path, slot k is .k; mkdir mutexes, config/lib/locks.sh). ui-evidence's browser lock and sindri's TS lock
#      take slot 1 of the same lock, so they count against this cap too.
#   A job is expensive when it builds or tests the whole solution, publishes or smoke-tests a NativeAOT/framework
#   binary, or opens a browser (Playwright, ui-evidence, Aw.Evidence); `heavy --expensive <cmd>` forces it,
#   `heavy --light <cmd>` forces light (no box-wide slot).
#   3. Box-wide, for light jobs that compile (#3796): a `--light` command with ` dotnet build `, msbuild, publish, format, pack,
#      restore, run, or `dotnet test` without --no-build (dotnet matched by its basename, so `~/.dotnet/dotnet build` counts,
#      #3810), takes one of N light slots of <locks>/heavy-light.lock (slot 1 the bare path, slot k .k, the same mkdir mutexes
#      and dead-holder reclaim as rule 2). N is $AW_STATE_DIR/heavy-light-cap, else AW_HEAVY_LIGHT_SLOTS, else 4, read again on
#      every pass. Such a build also doesn't START while a live job waits for a box-wide slot and every free slot that job may
#      take is under its memory floor (slot 1 has no floor). A lane waiter may take only the slots above heavy-meta-reserve;
#      a meta waiter may take any (#3810). So builders can't keep a gate's slot
#      shut; nothing running is stopped, and that wait doesn't count toward the lock timeout. The hold has no limit of its own:
#      a held build prints a line when the hold starts or moves to another waiter, and every 5 minutes after. The line names
#      the waiter (pid, argv), the slot it waits for, that slot's floor and the MB available.
#      AW_HEAVY_PRIORITY=meta skips that hold, not the pool. A light job that compiles nothing (`dotnet test --no-build`,
#      `dotnet clean`, a script) takes the per-session slot only.
#      For a PR that touches one src project, a light build of that project alone is the compile check. Build the whole test
#      project only when the PR edits tests: a test project compiles its full dependency graph and holds a light slot for 3 to 8
#      minutes. On the owner's box, heavy-light-cap raised the cap to 6 on 2026-11-22; the default stays 4.
#   4. Arrival order (#3819): a waiter takes a free slot only when no older live waiter of the same pool is queued. Older means
#      an earlier wait-file `since`, then a lower pid. Only wait files with "fifo":1 count (older heavy.sh copies don't write it
#      and can't take every slot), and never one with "meta":1: AW_HEAVY_PRIORITY=meta jobs skip the queue both ways. The box-wide
#      and light pools are shared by every waiter; the session pool only by waiters for the same session. A waiter is live when its
#      pid is alive and that process's start time is the file's "start": a SIGKILLed waiter's file stays, and when another
#      process gets its pid later, the file holds no one back. A file without "start" is not ordered against. A waiter stops
#      standing back once AW_HEAVY_FIFO_MAX_DEFER seconds (default 1800; 0 turns the order off) have passed since its first
#      stand-back, in any pool.
#   The job runs in its own systemd user scope (OOMPolicy=continue) when systemd-run works, so an OOM kill ends the job only.
#   Test switches (scratch suites only): AW_HEAVY_TEST_PATH (a stub dir put first on PATH), AW_HEAVY_TEST_CGROUP (a file read for
#   /proc/self/cgroup) and AW_HEAVY_TEST_PS_START=1 (start tokens through `ps -o lstart=`, the path without /proc, #3859).
set -u
# The cap and reserve readers live next to locks.sh; this script may run through a link (~/.agentic-workflow/bin-r1b/heavy).
# In a build folder it sits beside this script; in the repo it is ../config/lib.
# Resolve links without readlink -f (macOS before 12.3 has none), at most 40 hops.
_hs="${BASH_SOURCE[0]}"; _hn=0
while [ -L "$_hs" ]; do
  _hn=$((_hn + 1)); [ "$_hn" -le 40 ] || { echo "heavy: too many links resolving $0" >&2; exit 2; }
  _hl="$(readlink "$_hs")"
  case "$_hl" in /*) _hs="$_hl" ;; *) _hs="$(cd "$(dirname "$_hs")" && pwd -P)/$_hl" ;; esac
done
_hd="$(cd "$(dirname "$_hs")" && pwd -P)"
unset _hs _hn _hl
if [ -f "$_hd/heavy-config.sh" ]; then . "$_hd/heavy-config.sh"
elif [ -f "$_hd/../config/lib/heavy-config.sh" ]; then . "$_hd/../config/lib/heavy-config.sh"
else echo "heavy: heavy-config.sh not found next to $_hd or in ../config/lib" >&2; exit 2; fi
export DOTNET_ROOT="$HOME/.dotnet" PATH="${AW_HEAVY_TEST_PATH:+$AW_HEAVY_TEST_PATH:}$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
LOCKS="${AW_STATE_DIR:-$HOME/.agentic-workflow}/locks"
LOCK="${AW_HEAVY_JOB_LOCK:-$LOCKS/heavy-job.lock}"
SLOTS="${AW_HEAVY_SLOTS:-1}"
SESSION_SLOTS="${AW_SESSION_HEAVY_SLOTS:-1}"
# A non-number, 0 or negative would take no slot and make every job wait out TIMEOUT; treat it as 1 (as config/lib/locks.sh does).
case $SLOTS in ""|*[!0-9]*|0) SLOTS=1 ;; esac
case $SESSION_SLOTS in ""|*[!0-9]*|0) SESSION_SLOTS=1 ;; esac
GIVEN_SLOTS="$SLOTS"   # the fallback each pass re-reads the cap against, so taking the cap file away restores it
# Owner cap (2026-10-14; 2026-10-15: the cap file is the box-wide slot count): ~/.agentic-workflow/heavy-slots-cap sets
# the box-wide slots, whatever a session's inherited AW_HEAVY_SLOTS says (sessions were launched with AW_HEAVY_SLOTS=1).
SLOTS="$(heavy_cap "$SLOTS")"
# Memory guard (owner, 2026-10-15): a box-wide slot above 1 is only taken while MemAvailable is at least
# its floor: AW_HEAVY_MIN_FREE_MB (default 10240) for slot 2; a slot whose floor is not met is skipped.
MIN_FREE_MB="${AW_HEAVY_MIN_FREE_MB:-10240}"
case $MIN_FREE_MB in ""|*[!0-9]*) MIN_FREE_MB=10240 ;; esac
# Slot 2 needs MIN_FREE_MB available. Slots 3 and up need AW_HEAVY_MIN_FREE_HIGH_MB (default 3072; owner, 2026-11-22, #3813:
# at 14 GB for slot 3 the upper slots stayed shut while gates queued). 3 GB is less than two compiles use (a csc takes 1 to
# 2 GB), so a job that starts on slot 3 or 4 near the floor can push the box into swap; each job runs in its own systemd scope
# with OOMPolicy=continue, so an OOM kill ends that job only.
HIGH_FREE_MB="${AW_HEAVY_MIN_FREE_HIGH_MB:-3072}"
case $HIGH_FREE_MB in ""|*[!0-9]*) HIGH_FREE_MB=3072 ;; esac
# Without /proc/meminfo (macOS), the available figure is vm_stat's free + inactive + speculative pages times its page size,
# the figure aw-meta reads there; an unreadable figure keeps the guard closed (#2584: macOS had one slot and no meta reserve).
avail_kb() {
  local kb; kb="$(awk '/^MemAvailable:/ {print $2; exit}' /proc/meminfo 2> /dev/null)"
  [ -n "$kb" ] || kb="$(vm_stat 2> /dev/null | awk '/page size of/ {for (i = 1; i <= NF; i++) if ($i == "of") ps = $(i + 1)}
    /^Pages (free|inactive|speculative):/ {gsub(/\./, "", $NF); n += $NF} END {if (ps > 0 && n > 0) printf "%d", n * ps / 1024}')"
  printf '%s' "$kb"
}
mem_ok() { local kb need; if [ "${1:-2}" -ge 3 ]; then need=$HIGH_FREE_MB; else need=$MIN_FREE_MB; fi; kb="$(avail_kb)"; [ -n "$kb" ] && [ "$((kb / 1024))" -ge "$need" ]; }
TIMEOUT=5400
# Meta gate reservation (owner, 2026-10-17): the meta orchestrator writes how many box-wide slots its gates reserve
# (0..SLOTS; every reader caps it at SLOTS-1, so a lane job always has a slot, #2060) to <state>/heavy-meta-reserve, scaled to its merge-queue depth. A job run with AW_HEAVY_PRIORITY=meta may
# take any slot; any other expensive job may take only slots above the reservation. Nothing running is pre-empted, and
# a job blocked only by the reservation (it skipped a free reserved slot) does not count that wait toward TIMEOUT. Such a wait
# prints a note when it starts and every HOLD_NOTE_EVERY seconds while it lasts: the reserve, the slot count and the MB available (#3907).
# The reserve is a slot count, held whatever memory is free (meta, 2026-11-22, #3885): the memory floors decide only whether a
# job may start in a slot, never how many slots meta keeps. So with slot 2 under its floor, a lane job still can't take slot 1.
RESERVE="$(heavy_reserve "$SLOTS")"
PRIORITY="${AW_HEAVY_PRIORITY:-}"
# The light pool (rule 3, #3796). A non-number or 0 in AW_HEAVY_LIGHT_SLOTS means the default 4.
LIGHT_LOCK="$LOCKS/heavy-light.lock"
LIGHT_GIVEN="${AW_HEAVY_LIGHT_SLOTS:-4}"
case $LIGHT_GIVEN in ""|*[!0-9]*|0) LIGHT_GIVEN=4 ;; esac
# Rule 4's safety valve, in seconds from the job's first stand-back (STOOD_AT, shared by the session, box-wide and light pools).
# The start check in live_waiter already ignores a reused pid; the valve bounds anything else that keeps an older waiter queued.
MAX_DEFER="${AW_HEAVY_FIFO_MAX_DEFER:-1800}"
case $MAX_DEFER in ""|*[!0-9]*) MAX_DEFER=1800 ;; esac
STOOD_AT=""
# A light build held back for a box-wide waiter (rule 3) says so when the hold starts and again every HOLD_NOTE_EVERY seconds of
# holding, so a long hold shows in the job's output (#3810). A hold that starts again, or switches to another waiter, says so at once. The hold has no time limit of its own.
# The box-wide wait beside a free reserved slot repeats its note on the same interval (#3907).
HOLD_NOTE_EVERY=300

mode=auto
case "${1:-}" in --expensive) mode=expensive; shift ;; --light) mode=light; shift ;; esac
[ "$#" -ge 1 ] || { echo "usage: heavy [--expensive|--light] <cmd...>" >&2; exit 2; }
# Nested call (a heavy job running heavy again): the outer job already holds this session's slot, so waiting on it
# again would deadlock. Run straight through.
if [ "${AW_HEAVY_LOCK_HELD:-}" = 1 ]; then exec "$@"; fi

# compiles: the command runs a dotnet step that compiles. In auto mode that makes the job expensive; under --light it takes
# a light slot (rule 3). The words are matched in CMDLINE, the args joined by spaces with every "/dotnet " turned into
# " dotnet ": a path-qualified dotnet (`~/.dotnet/dotnet build`) is matched by its basename, also inside a `bash -c` string,
# and every other word is left as it was. `dotnet msbuild` compiles like `dotnet build` (#3810). Sindri's sandbox shim table
# (SandboxShims.cs) lists the same dotnet subcommands, in the same order; SandboxShimsTests checks this row against it.
CMDLINE=" $* "; CMDLINE="${CMDLINE//\/dotnet / dotnet }"
compiles=""
case $CMDLINE in
  *" dotnet build "*|*" dotnet msbuild "*|*" dotnet publish "*|*" dotnet format "*|*" dotnet pack "*|*" dotnet restore "*|*" dotnet run "*) compiles=1 ;;
  *" dotnet test "*) case $CMDLINE in *" --no-build "*) ;; *) compiles=1 ;; esac ;;
esac
if [ "$mode" = auto ]; then
  # Owner, 2026-10-14 (second tightening, swap full at load 128): every build is expensive. Light is only what
  # compiles nothing: a `dotnet test --no-build`, a plain script. Everything that compiles, restores, publishes,
  # formats or opens a browser shares the box-wide slots.
  mode=light
  if [ -n "$compiles" ]; then mode=expensive
  else
    case $CMDLINE in
      *" dotnet test "*) ;;   # a --no-build test stays light, whatever else the line names
      *" npm ci "*|*" npm install "*|*" npm test "*|*" npx vitest "*|*" npx tsc "*|*vitest*) mode=expensive ;;
      *AgenticWorkflow.slnx*|*aot-smoke*|*playwright*|*ui-evidence*|*Aw.Evidence*|*" e2e.sh "*) mode=expensive ;;
    esac
  fi
fi

# The session: nearest ancestor whose command is `claude`; fall back to the parent shell's pid.
session=""; p=$PPID
for _ in $(seq 1 20); do
  [ -z "$p" ] || [ "$p" -le 1 ] && break
  c="$(ps -o comm= -p "$p" 2>/dev/null)"; c="${c##*/}"
  if [ "$c" = claude ]; then session="$p"; break; fi
  # Linux / npm installs: the process is `node …/claude …`
  if [ "$c" = node ] && ps -o args= -p "$p" 2>/dev/null | grep -qE '(^|/)claude( |$)|/claude-code/'; then session="$p"; break; fi
  p="$(ps -o ppid= -p "$p" 2>/dev/null | tr -d ' ')"
done
# claude_found: a real claude ancestor (or AW_HEAVY_SESSION_PID, for tests); only then does the orphan watchdog run.
claude_found=""; [ -n "$session" ] && claude_found=1
if [ -n "${AW_HEAVY_SESSION_PID:-}" ]; then session="$AW_HEAVY_SESSION_PID"; claude_found=1; fi
[ -n "$session" ] || session="$PPID"

mkdir -p "$LOCKS/heavy-session" "$LOCKS/heavy-wait" "$(dirname "$LOCK")"
# <locks>/heavy-wait/<pid>: one file per job that is waiting for a slot (pid = this wrapper's $$), one JSON line:
# {"pid","session" (number),"lock":"session"|"box-wide"|"light","since" (UTC, when the first wait began),"start" (this wrapper's
# process start token, pid_start),"argv":[...] (raw),"cwd",
# "fifo":1 (this copy orders waiters by arrival, rule 4), and "meta":1 for an AW_HEAVY_PRIORITY=meta job}.
# "light" is a compiling light job waiting for the light pool or held back for a box-wide waiter (rule 3).
# A waiter reads the other files to find older waiters (rule 4) and box-wide waiters (rule 3).
# The reader is `aw-meta heavy --json` (#3142); it redacts argv and skips a file whose pid is dead (a SIGKILLed wrapper
# leaves one; the dead-holder reclaim below does not touch these). Written atomically on the first pass without a slot,
# rewritten when the wait moves from the session lock to the box-wide or light one, removed once a slot is taken, on the lock
# timeout and in both traps. A job that gets its slots at once writes none. A write failure never delays the job.
WAITFILE="$LOCKS/heavy-wait/$$"; WAIT_SINCE=""; MY_START=""
# pid_start <pid>: set START to the process start token of <pid>, empty when it can't be read. Linux: field 22 of
# /proc/<pid>/stat (clock ticks since boot), read with builtins after the last ")" because the command name may hold spaces or
# brackets. Without /proc (macOS): `ps -o lstart=`, its words joined by _. A pid that a new process took has another token.
# A pid that dies between kill -0 and ps gives no words; "${f[*]-}" keeps that an empty START on bash before 4.4 (macOS
# /bin/bash 3.2), where "${f[*]}" of an empty array is an unbound-variable exit under set -u. AW_HEAVY_TEST_PS_START=1 takes the
# ps branch on a box with /proc too, so the suite runs it on Linux (#3859).
PS_START="${AW_HEAVY_TEST_PS_START:-}"
pid_start() {
  local l f; START=""
  if [ -r /proc/self/stat ] && [ "$PS_START" != 1 ]; then
    { IFS= read -r l < "/proc/$1/stat"; } 2>/dev/null || return 0
    read -r -a f <<< "${l##*) }"; START="${f[19]:-}"
  else
    l="$(ps -o lstart= -p "$1" 2>/dev/null)"; read -r -a f <<< "$l"; local IFS=_; START="${f[*]-}"
  fi
  return 0
}
json_str() { local s="$1"; s="${s//\\/\\\\}"; s="${s//\"/\\\"}"; s="${s//$'\n'/\\n}"; s="${s//$'\r'/\\r}"; s="${s//$'\t'/\\t}"; printf '"%s"' "$s"; }
rm_wait() { rm -f "$WAITFILE" 2>/dev/null; return 0; }
# write_wait <label> <argv...>
write_wait() {
  local label="$1" a first=1 argv="" meta=""; shift
  [ -n "$WAIT_SINCE" ] || WAIT_SINCE="$(date -u +%FT%TZ)"
  [ -n "$MY_START" ] || { pid_start "$$"; MY_START="$START"; }
  [ "$PRIORITY" = meta ] && meta=',"meta":1'
  for a in "$@"; do if [ "$first" = 1 ]; then first=0; else argv="$argv,"; fi; argv="$argv$(json_str "$a")"; done
  { printf '{"pid":%s,"session":%s,"lock":"%s","since":"%s","start":"%s","argv":[%s],"cwd":%s,"fifo":1%s}\n' \
      "$$" "$session" "$label" "$WAIT_SINCE" "$MY_START" "$argv" "$(json_str "$PWD")" "$meta" > "$WAITFILE.tmp.$$" && mv -f "$WAITFILE.tmp.$$" "$WAITFILE"; } 2>/dev/null || true
}
HELD=(); reason=""
release() { [ -z "$reason" ] || rm -f "$reason" 2>/dev/null; rm_wait; rm -f "$WAITFILE.tmp.$$" 2>/dev/null; local d; for d in "${HELD[@]+"${HELD[@]}"}"; do unlink "$d/owner" 2>/dev/null; rmdir "$d" 2>/dev/null || true; done; }
trap release EXIT
job=""; dog=""
# signal_job <SIG>: the job's process group, or its pid while the group doesn't exist yet (setsid or perl hasn't made it, #2799).
signal_job() { kill "-$1" -- "-$job" 2>/dev/null || kill "-$1" "$job" 2>/dev/null; }
# job_alive: the group (test hosts outlive the job's own pid) or the pid (no group yet) is still there.
job_alive() { kill -0 -- "-$job" 2>/dev/null || kill -0 "$job" 2>/dev/null; }
stop_job() { [ -n "$job" ] && signal_job TERM; [ -n "$dog" ] && kill "$dog" 2>/dev/null; return 0; }
trap 'stop_job; release; exit 130' INT TERM

# reclaim_slot <dir>: free a slot whose holder pid is dead. The holder file is <dir>/owner (this wrapper, ui-evidence), else
# <dir>/pid (config/lib/locks.sh); the first word of the line is the pid in both. The file is moved aside first (only one
# waiter's mv wins); if what moved is not the dead line read, the slot changed hands and it goes back. With neither file, leftover
# <owner|pid>.reclaim.<N>* files are freed when every reclaimer pid N is dead (a live N is mid-step), or whoever N is once the
# folder is over 6 h old (a reused pid, or a reclaimer that failed without dying). A folder with no entries at all (a holder killed
# between its mkdir and its record write) is freed once over two minutes old (GNU find -mmin +1 truncates; Sindri waits one); younger is a holder mid-write, and rmdir fails if one
# writes meanwhile.
reclaim_slot() {
  local d="$1" line pid f n hf m left=() aged=""
  hf="$d/owner"; [ -f "$hf" ] || hf="$d/pid"
  if [ -f "$hf" ]; then
    m="$hf.reclaim.$$"
    line="$(cat "$hf" 2>/dev/null)" || return 1; pid="${line%% *}"
    case $pid in ""|*[!0-9]*) return 1 ;; esac
    kill -0 "$pid" 2>/dev/null && return 1
    mv "$hf" "$m" 2>/dev/null || return 1
    if [ "$(cat "$m" 2>/dev/null)" != "$line" ]; then mv "$m" "$hf" 2>/dev/null; return 1; fi
    rm -f "$m"; rmdir "$d" 2>/dev/null; return 0
  fi
  m="$d/owner.reclaim.$$"
  if [ -z "$(ls -A "$d" 2>/dev/null)" ]; then
    [ -n "$(find "$d" -maxdepth 0 -mmin +1 2>/dev/null)" ] && rmdir "$d" 2>/dev/null
    return
  fi
  [ -n "$(find "$d" -maxdepth 0 -mmin +360 2>/dev/null)" ] && aged=1
  for f in "$d"/owner.reclaim.* "$d"/pid.reclaim.*; do
    [ -e "$f" ] || continue
    n="${f##*.reclaim.}"; n="${n%%[!0-9]*}"
    [ -n "$aged" ] || { [ -n "$n" ] && ! kill -0 "$n" 2>/dev/null; } || return 1
    left+=("$f")
  done
  [ "${#left[@]}" -gt 0 ] || return 1
  for f in "${left[@]}"; do mv "$f" "$m" 2>/dev/null || return 1; rm -f "$m"; done
  rmdir "$d" 2>/dev/null; return 0
}

# slot_still_allowed <k>: box-wide slot k is within the cap and, for a non-meta job, outside the reserve, as read now.
# Light slot k: within the light cap.
slot_still_allowed() {
  local k="$1" cap res
  if [ "$label" = light ]; then [ "$k" -le "$(heavy_light_cap "$LIGHT_GIVEN")" ]; return; fi
  cap="$(heavy_cap "$GIVEN_SLOTS")"; [ "$k" -le "$cap" ] || return 1
  [ "$PRIORITY" = meta ] && return 0
  res="$(heavy_reserve "$cap")"
  [ "$k" -gt "$res" ]
}

# live_waiter <match>: some other job's wait file, whose line <match> accepts (called with the pid and the line), has a live pid,
# and, when the line has a "start", that pid's process start token is still the one written there. The case skips
# <pid>.tmp.<pid> files mid-write; a dead waiter's file (SIGKILL leaves one) and one whose pid another process took count for
# nothing. Such files are left in place: the pid's next heavy wrapper overwrites its own, and aw-meta skips dead pids. `read`, not
# cat, and pid_start reads /proc with builtins: no fork per file on Linux, since every waiter runs this each pass. On a match,
# WAITER_LINE is that file's line.
WAITER_LINE=""
live_waiter() {
  local f pid line want
  for f in "$LOCKS/heavy-wait/"*; do
    pid="${f##*/}"; case $pid in ""|*[!0-9]*) continue ;; esac
    [ "$pid" != "$$" ] && [ -f "$f" ] || continue
    line=""; { IFS= read -r line || [ -n "$line" ]; } 2>/dev/null < "$f" || continue
    "$1" "$pid" "$line" || continue
    kill -0 "$pid" 2>/dev/null || continue
    case $line in
      *'"start":"'*) want="${line#*\"start\":\"}"; want="${want%%\"*}"; pid_start "$pid"
        [ -n "$want" ] && [ "$START" = "$want" ] || continue ;;
    esac
    WAITER_LINE="$line"; return 0
  done
  return 1
}
# boxwide_lane_line / boxwide_meta_line <pid> <line>: a box-wide waiter without / with "meta":1 (older copies never write it).
boxwide_lane_line() { case $2 in *'"meta":1'*) return 1 ;; *'"lock":"box-wide"'*) return 0 ;; esac; return 1; }
boxwide_meta_line() { case $2 in *'"meta":1'*) case $2 in *'"lock":"box-wide"'*) return 0 ;; esac ;; esac; return 1; }
# older_peer <pid> <line>: the line is a rule-4 waiter of this pool (the caller's $label; for the session pool, this session)
# that came first: an earlier since, or the same since and a lower pid. Before this job has a since, every such waiter came first.
# json_str escapes every quote in argv and cwd, so none of these patterns can match inside them.
older_peer() {
  local pid="$1" line="$2" since
  case $line in *'"fifo":1'*) ;; *) return 1 ;; esac
  case $line in *'"meta":1'*) return 1 ;; esac
  case $line in *'"start":"'[!\"]*) ;; *) return 1 ;; esac   # no start token: live_waiter can't tell a reused pid
  case $line in *"\"lock\":\"$label\""*) ;; *) return 1 ;; esac
  if [ "$label" = session ]; then case $line in *"\"session\":$session,"*) ;; *) return 1 ;; esac; fi
  [ -n "$WAIT_SINCE" ] || return 0
  since="${line#*\"since\":\"}"; since="${since%%\"*}"
  [[ "$since" < "$WAIT_SINCE" ]] || { [ "$since" = "$WAIT_SINCE" ] && [ "$pid" -lt "$$" ]; }
}

# boxwide_starved: some live job waits for a box-wide slot (a heavy-wait file with "lock":"box-wide" whose pid is alive), and
# every free box-wide slot that job may take is shut by its memory floor. A meta waiter may take any slot within the cap. Any other
# waiter may take only the slots above the reserve, a slot count whatever memory is free, as in the box-wide pool (#3885). So a
# free slot 1 it may take (no floor), a free slot whose floor is met, or no free slot it may take all mean no: the waiter takes
# the slot or waits for a holder, not for memory. A dead waiter's file (SIGKILL leaves one) counts for nothing.
# The waiter lookup comes first and uses builtins only: the cap file (cat) and MemAvailable (awk, or vm_stat) are read only once
# a waiter is found, and then once per call, so a compiling light job with no box-wide waiter forks nothing here. On a yes,
# HOLD_LINE is the waiter's wait-file line, HOLD_SLOT the lowest slot it can't take for memory, HOLD_NEED that slot's floor and HOLD_MB the MB available (empty when unreadable), for the hold note.
boxwide_starved() {
  local cap="" kb=""
  if live_waiter boxwide_lane_line; then
    cap="$(heavy_cap "$GIVEN_SLOTS")"; kb="$(avail_kb)"
    starved_above "$(heavy_reserve "$cap")" "$cap" "$kb" && return 0
  fi
  live_waiter boxwide_meta_line || return 1
  [ -n "$cap" ] || { cap="$(heavy_cap "$GIVEN_SLOTS")"; kb="$(avail_kb)"; }
  starved_above 0 "$cap" "$kb"
}
# floor_met <k> <kb>: box-wide slot k's memory floor is met by <kb> KB available; slot 1 has none, an empty <kb> meets none above.
floor_met() {
  [ "$1" -le 1 ] && return 0
  if [ "$1" -ge 3 ]; then FLOOR=$HIGH_FREE_MB; else FLOOR=$MIN_FREE_MB; fi
  [ -n "$2" ] && [ "$(($2 / 1024))" -ge "$FLOOR" ]
}
# starved_above <res> <cap> <kb>: of the box-wide slots res+1..cap, at least one is free and every free one is short of memory.
starved_above() {
  local k d short=""
  for ((k = $1 + 1; k <= $2; k++)); do
    if [ "$k" -eq 1 ]; then d="$LOCK"; else d="$LOCK.$k"; fi
    [ -d "$d" ] && continue
    floor_met "$k" "$3" && return 1
    [ -n "$short" ] || { short=$k; HOLD_NEED=$FLOOR; }
  done
  [ -n "$short" ] || return 1
  HOLD_LINE="$WAITER_LINE"; HOLD_PID="${WAITER_LINE#*\"pid\":}"; HOLD_PID="${HOLD_PID%%,*}"; HOLD_SLOT=$short; HOLD_MB=""; [ -z "$3" ] || HOLD_MB=$(($3 / 1024))
}

# hold_note: the hold's line on stderr, from what boxwide_starved found: the waiter's pid and argv (as JSON, cut at 200
# characters), the slot it can't take, that slot's floor and the MB available.
hold_note() {
  local pid argv mem
  pid="${HOLD_LINE#*\"pid\":}"; pid="${pid%%,*}"
  argv="${HOLD_LINE#*\"argv\":[}"; argv="${argv%%],\"cwd\":*}"; [ "${#argv}" -le 200 ] || argv="${argv:0:200}..."
  if [ -n "$HOLD_MB" ]; then mem="$HOLD_MB MB available"; else mem="MemAvailable unreadable"; fi
  echo "heavy: holding this build back: box-wide job $pid ($argv) waits for slot $HOLD_SLOT, which needs $HOLD_NEED MB free; $mem" >&2
}

# reserve_note <slots>: the reserve wait's line on stderr (#3907): the reserve, the slot count and the MB available. MemAvailable is
# read here, only when the note is due, not on every pass.
reserve_note() {
  local kb mem; kb="$(avail_kb)"
  if [ -n "$kb" ]; then mem="$((kb / 1024)) MB available"; else mem="MemAvailable unreadable"; fi
  echo "heavy: waiting although a box-wide slot is free: meta reserves $RESERVE of $1 slot(s) (heavy-meta-reserve); every slot above those is held or under its memory floor; $mem" >&2
}

# acquire <label> <base> <slots>: take one slot of a mkdir-mutex pool; a slot whose owner pid is gone is reclaimed.
acquire() {
  local label="$1" base="$2" slots="$3" waited=0 printed=0 told=0 k d got="" free_wait held
  local fifo=1 older stood told_at=0 told_pid="" rtold=0 rtold_at=0
  [ "$PRIORITY" = meta ] && fifo=0
  while [ -z "$got" ]; do
    free_wait=0; held=0; older=""; stood=0
    # Re-read the cap and the reservation each pass: a drained queue releases waiting lane jobs promptly, and a lowered cap
    # keeps them off the slots above it (nothing running is pre-empted). The memory guard below is slow (two vm_stat forks
    # without /proc/meminfo), so a slot taken is checked again against the cap and reserve read after the take (#2718).
    if [ "$label" = box-wide ]; then
      # heavy_reserve keeps the reserve at most slots - 1, so a lane job always has a slot to wait for (#2060). There is no memory
      # clamp: the reserve is a slot count (#3885), and each slot's floor below decides only whether a job may start there.
      slots="$(heavy_cap "$GIVEN_SLOTS")"; RESERVE="$(heavy_reserve "$slots")"
    fi
    # The light pool: its cap, read now, and the hold for a box-wide waiter short of memory (rule 3; a meta job skips the hold).
    if [ "$label" = light ]; then
      slots="$(heavy_light_cap "$LIGHT_GIVEN")"
      if [ "$PRIORITY" != meta ] && boxwide_starved; then
        held=1; free_wait=1
        # A new hold (told was reset on a pass that wasn't held) or a hold for another waiter says so at once; a hold that goes
        # on repeats its note every HOLD_NOTE_EVERY seconds.
        if [ "$told" -eq 0 ] || [ "$HOLD_PID" != "$told_pid" ] || [ "$((SECONDS - told_at))" -ge "$HOLD_NOTE_EVERY" ]; then
          hold_note; told=1; told_at=$SECONDS; told_pid="$HOLD_PID"
        fi
      else
        told=0
      fi
    fi
    [ "$held" = 1 ] || for k in $(seq 1 "$slots"); do
      if [ "$k" -eq 1 ]; then d="$base"; else d="$base.$k"; fi
      # Box-wide slots above 1 need the memory guard; the per-session and light pools don't.
      if [ "$label" = box-wide ] && [ "$PRIORITY" != meta ] && [ "$k" -le "$RESERVE" ]; then [ -d "$d" ] || free_wait=1; continue; fi
      if [ "$k" -gt 1 ] && [ "$label" = box-wide ] && ! mem_ok "$k"; then continue; fi
      # Rule 4: stand back while an older waiter of this pool is queued (looked up once per pass, at the first slot we could
      # try). No head-of-line deadlock while both jobs run with the same AW_HEAVY_MIN_FREE_MB and AW_HEAVY_MIN_FREE_HIGH_MB, and
      # the same AW_HEAVY_SLOTS and AW_HEAVY_LIGHT_SLOTS when there is no cap file: the older waiter runs this version, is not
      # meta and waits for the same pool, so the reserve, the floors, the light cap and the light hold bind it as they bind us;
      # whatever it can't take, we can't either. Those variables are per process (no tool sets them per job today); an older
      # waiter with a higher floor leaves a slot we could take, and only the valve below lets us through. A session slot is no
      # difference: a box-wide or light waiter already holds its own. A dead holder's slot is still freed; the older waiter takes it.
      if [ "$fifo" = 1 ]; then
        [ -n "$older" ] || { if live_waiter older_peer; then older=y; else older=n; fi; }
        if [ "$older" = y ]; then { [ ! -d "$d" ] || reclaim_slot "$d"; } && stood=1; continue; fi
      fi
      if mkdir "$d" 2>/dev/null || { reclaim_slot "$d" && mkdir "$d" 2>/dev/null; }; then
        # Take-then-check (#2718): read the cap and reserve again now the slot is ours. Over the cap (box-wide or light), or
        # inside the reserve for a non-meta job: give the empty slot back (before the owner write) and keep waiting.
        if [ "$label" != session ] && ! slot_still_allowed "$k"; then rmdir "$d" 2>/dev/null; continue; fi
        got="$d"; break
      fi
    done
    [ -n "$got" ] && break
    # The safety valve: the clock starts at the job's first stand-back in any pool (a pass takes 1 s of sleep plus the memory
    # checks, so counting passes ran long under load). Standing back also counts toward TIMEOUT, like any wait.
    if [ "$stood" = 1 ]; then
      [ -n "$STOOD_AT" ] || STOOD_AT=$SECONDS
      if [ "$((SECONDS - STOOD_AT))" -ge "$MAX_DEFER" ]; then
        fifo=0; echo "heavy: no longer waiting for older jobs: stood back from a free $label slot for ${MAX_DEFER}s (AW_HEAVY_FIFO_MAX_DEFER)" >&2
      fi
    fi
    [ "$printed" -eq 0 ] && { echo "heavy: waiting for the $label lock ($slots slot(s), $base)" >&2; printed=1; write_wait "$label" "${@:4}"; }
    # A box-wide pass that skipped a free reserved slot and stood back for no older waiter waits only for meta's reserve (#3907):
    # say so when such a wait starts and every HOLD_NOTE_EVERY seconds while it lasts; any other pass ends it.
    if [ "$label" = box-wide ] && [ "$free_wait" = 1 ] && [ "$stood" = 0 ]; then
      if [ "$rtold" -eq 0 ] || [ "$((SECONDS - rtold_at))" -ge "$HOLD_NOTE_EVERY" ]; then reserve_note "$slots"; rtold=1; rtold_at=$SECONDS; fi
    else
      rtold=0
    fi
    [ "$waited" -ge "$TIMEOUT" ] && { echo "heavy: $label lock timeout ($base)" >&2; rm_wait; exit 75; }
    # A pass that skipped a free reserved slot was blocked only by the reservation, and a light pass held back for a box-wide
    # waiter only by that hold: neither counts. Any other pass does.
    sleep 1; [ "$free_wait" = 1 ] || waited=$((waited + 1))
  done
  rm_wait
  echo "$$ $(date -u +%FT%TZ) session=$session $*" > "$got/owner" 2>/dev/null || true
  HELD+=("$got")
}

# Order: the session slot first, then the box-wide one, so a session never holds a box-wide slot while queued behind itself.
acquire session "$LOCKS/heavy-session/$session.lock" "$SESSION_SLOTS" "$@"
if [ "$mode" = expensive ]; then acquire box-wide "$LOCK" "$SLOTS" "$@"; export AW_HEAVY_SLOT="${HELD[1]}"
elif [ -n "$compiles" ]; then acquire light "$LIGHT_LOCK" "$(heavy_light_cap "$LIGHT_GIVEN")" "$@"; fi
export AW_HEAVY_LOCK_HELD=1 AW_HEAVY_MODE="$mode"
# Keep one job from taking the whole box: fewer cores per .NET process (MSBuild -m, test parallelism), no MSBuild
# worker reuse (nodes exit with the build instead of staying resident between builds).
export DOTNET_PROCESSOR_COUNT="${AW_HEAVY_CPUS:-4}" MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0
# why: one shared Roslyn server (VBCSCompiler) grew to 10 GB in one build and caused MEMORY LOW events (#3178). MSBuild reads
# the variable as a property, so every wrapped job, light or expensive, compiles in process. A caller's own value wins.
export UseSharedCompilation="${UseSharedCompilation:-false}"
# Hang limit (owner, 2026-10-22, after an orphaned test run held a slot for 2.5 h). AW_HEAVY_RUN_MAX is minutes:
# 45 for `dotnet test`, 120 otherwise, 0 = no limit. The clock starts here, after the slots are held. The job runs in
# its own process group, so test hosts started under `dotnet test` go with it: TERM to the group, KILL 30 s later,
# exit 124. A watchdog also kills the job when its claude session is gone (never for a fallback, non-claude pid).
# A `dotnet test` with no --timeout of its own gets the test platform's --timeout, 5 minutes inside the limit.
# It is appended last, so a command that ends in `-- <args>` would pass it to the test app instead (none of ours do).
is_dotnet_test=""; [ "${1##*/}" = dotnet ] && [ "${2:-}" = test ] && is_dotnet_test=1
run_max="${AW_HEAVY_RUN_MAX:-}"
case $run_max in ""|*[!0-9]*) if [ -n "$is_dotnet_test" ]; then run_max=45; else run_max=120; fi ;; esac
if [ -n "$is_dotnet_test" ] && [ "$run_max" -gt 0 ]; then
  case " $* " in
    *" --timeout "*|*" --timeout="*) ;;
    *) if [ "$run_max" -gt 5 ]; then set -- "$@" --timeout "$((run_max - 5))m"; else set -- "$@" --timeout "${run_max}m"; fi ;;
  esac
fi
# Own systemd scope (#2931, #2856): setsid changes the session and process group but not the cgroup, so the job would stay in the
# caller's pane scope (tmux-spawn-*.scope). With the default OOMPolicy=stop, one OOM kill of a job process then stops that whole
# scope and takes the agent session with it. `systemd-run --user --scope -p OOMPolicy=continue` puts the job in its own unit, so
# an OOM kill ends that job only. --scope execs in place (no fork), so $! below is still the job's pid and its group id.
# The job also binds to the caller's pane scope (BindsTo+After): without it the job outlives its pane (OOM kill of the agent,
# tmux kill-session), and the lock's dead-holder reclaim frees its slot while it still eats memory, so a 4th job could start.
# BindsTo is one-way, so an OOM kill inside the job leaves the pane alone. Only scopes of the user manager are bound: a login
# session-N.scope belongs to the system manager, and `systemd-run --user` fails on it.
# Fallback, today's path: no systemd-run (macOS), no user bus, or the probe below fails.
scope=()
if command -v systemd-run >/dev/null 2>&1; then
  unit="$(awk -F: '$1=="0" && $2=="" {print $3; exit}' "${AW_HEAVY_TEST_CGROUP:-/proc/self/cgroup}" 2>/dev/null)"
  bind=()
  case "$unit" in
    */user@[0-9]*.service/*.scope) unit="${unit##*/}"; bind=(-p "BindsTo=$unit" -p "After=$unit") ;;
  esac
  cand=(systemd-run --user --scope --quiet --collect -p OOMPolicy=continue "${bind[@]+"${bind[@]}"}" --)
  # A wedged user bus under memory pressure would hang every heavy call while it holds a slot, so the probe gets a timeout.
  probe=()
  if command -v timeout >/dev/null 2>&1; then probe=(timeout 10); fi
  if "${probe[@]+"${probe[@]}"}" "${cand[@]}" true >/dev/null 2>&1; then scope=("${cand[@]}"); fi
fi
# This relies on job control being off (non-interactive bash): the background child is not a group leader, so setsid
# execs in place, $! is the job's pid and its group id. Under job control setsid would fork and $! would exit at once.
# macOS has no setsid: perl does the same. Its `--` stops perl from reading a command word that starts with - as a perl switch.
if command -v setsid >/dev/null 2>&1; then setsid "${scope[@]+"${scope[@]}"}" "$@" & else perl -e 'setpgrp(0, 0); exec @ARGV or exit 127' -- "${scope[@]+"${scope[@]}"}" "$@" & fi
job=$!
reason="$(mktemp "${TMPDIR:-/tmp}/heavy-reason.XXXXXX")"
(
  start=$SECONDS
  # Arm only once the job's group exists (#2799): setsid or perl makes it some time after the fork, and a kill sent to "-$job"
  # before then reaches nothing. Wait up to ~5 s in 0.05 s steps, or until the job is gone; past that, signal_job falls back to
  # the pid. This waits here, not in the wrapper: the wrapper must sit in `wait "$job"`, which reaps a quick job at once.
  for _ in $(seq 1 100); do kill -0 -- "-$job" 2>/dev/null && break; kill -0 "$job" 2>/dev/null || break; sleep 0.05; done
  while kill -0 "$job" 2>/dev/null; do
    why=""
    [ "$run_max" -gt 0 ] && [ $((SECONDS - start)) -ge $((run_max * 60)) ] && why="ran past AW_HEAVY_RUN_MAX=${run_max}m"
    [ -z "$why" ] && [ -n "$claude_found" ] && ! kill -0 "$session" 2>/dev/null && why="session $session ended"
    if [ -n "$why" ]; then
      echo "$why" > "$reason"
      signal_job TERM
      for _ in $(seq 1 30); do job_alive || exit 0; sleep 1; done
      signal_job KILL
      exit 0
    fi
    sleep 5
  done
# The watchdog has no message of its own (its reason goes to the reason file), so its stderr goes nowhere: killed right after
# the fork it can make bash print a run_pending_traps warning, and `seq` a broken-pipe error, into the job's output (#2884).
) 2>/dev/null &
dog=$!
wait "$job"; rc=$?
kill "$dog" 2>/dev/null; wait "$dog" 2>/dev/null
if [ -s "$reason" ]; then echo "heavy: $(cat "$reason"), killed: $*" >&2; rc=124; fi
rm -f "$reason"
exit $rc
}
