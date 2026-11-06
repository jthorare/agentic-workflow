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
#   `heavy --light <cmd>` forces the per-session slot only.
set -u
# The cap and reserve readers live next to locks.sh; this script may run through a link (~/.agentic-workflow/bin-r1b/heavy).
. "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")/../config/lib/heavy-config.sh"
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
# AW_HEAVY_MIN_FREE_MB (default 10240); below that, jobs queue for slot 1 as before.
MIN_FREE_MB="${AW_HEAVY_MIN_FREE_MB:-10240}"
case $MIN_FREE_MB in ""|*[!0-9]*) MIN_FREE_MB=10240 ;; esac
# Slot k (k >= 2) needs MIN_FREE_MB + (k-2) * STEP_MB available (owner, 2026-10-15: slot 3 needs 14 GB by default).
STEP_MB="${AW_HEAVY_MIN_FREE_STEP_MB:-4096}"
case $STEP_MB in ""|*[!0-9]*) STEP_MB=4096 ;; esac
mem_ok() { local kb need; need=$((MIN_FREE_MB + (${1:-2} - 2) * STEP_MB)); kb="$(awk '/^MemAvailable:/ {print $2; exit}' /proc/meminfo 2>/dev/null)"; [ -n "$kb" ] && [ "$((kb / 1024))" -ge "$need" ]; }
TIMEOUT=5400
# Meta gate reservation (owner, 2026-10-17): the meta orchestrator writes how many box-wide slots its gates reserve
# (0..SLOTS; every reader caps it at SLOTS-1, so a lane job always has a slot, #2060) to <state>/heavy-meta-reserve, scaled to its merge-queue depth. A job run with AW_HEAVY_PRIORITY=meta may
# take any slot; any other expensive job may take only slots above the reservation. Nothing running is pre-empted, and
# a job blocked only by the reservation (it skipped a free reserved slot) does not count that wait toward TIMEOUT.
RESERVE="$(heavy_reserve "$SLOTS")"
PRIORITY="${AW_HEAVY_PRIORITY:-}"

mode=auto
case "${1:-}" in --expensive) mode=expensive; shift ;; --light) mode=light; shift ;; esac
[ "$#" -ge 1 ] || { echo "usage: heavy [--expensive|--light] <cmd...>" >&2; exit 2; }
# Nested call (a heavy job running heavy again): the outer job already holds this session's slot, so waiting on it
# again would deadlock. Run straight through.
if [ "${AW_HEAVY_LOCK_HELD:-}" = 1 ]; then exec "$@"; fi

if [ "$mode" = auto ]; then
  # Owner, 2026-10-14 (second tightening, swap full at load 128): every build is expensive. Light is only what
  # compiles nothing: a `dotnet test --no-build`, a plain script. Everything that compiles, restores, publishes,
  # formats or opens a browser shares the box-wide slots.
  mode=light
  case " $* " in
    *" dotnet build "*|*" dotnet publish "*|*" dotnet format "*|*" dotnet pack "*|*" dotnet restore "*|*" dotnet run "*) mode=expensive ;;
    *" dotnet test "*) case " $* " in *" --no-build "*) ;; *) mode=expensive ;; esac ;;
    *" npm ci "*|*" npm install "*|*" npm test "*|*" npx vitest "*|*" npx tsc "*|*vitest*) mode=expensive ;;
    *AgenticWorkflow.slnx*|*aot-smoke*|*playwright*|*ui-evidence*|*Aw.Evidence*|*" e2e.sh "*) mode=expensive ;;
  esac
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

mkdir -p "$LOCKS/heavy-session" "$(dirname "$LOCK")"
HELD=()
release() { local d; for d in "${HELD[@]+"${HELD[@]}"}"; do unlink "$d/owner" 2>/dev/null; rmdir "$d" 2>/dev/null || true; done; }
trap release EXIT
job=""; dog=""
stop_job() { [ -n "$job" ] && kill -TERM -- "-$job" 2>/dev/null; [ -n "$dog" ] && kill "$dog" 2>/dev/null; return 0; }
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

# acquire <label> <base> <slots>: take one slot of a mkdir-mutex pool; a slot whose owner pid is gone is reclaimed.
acquire() {
  local label="$1" base="$2" slots="$3" waited=0 printed=0 k d got="" reserve_blocked usable
  while [ -z "$got" ]; do
    reserve_blocked=0
    # Re-read the cap and the reservation each pass: a drained queue releases waiting lane jobs promptly, and a lowered cap
    # keeps them off the slots above it (nothing running is pre-empted).
    if [ "$label" = box-wide ]; then
      slots="$(heavy_cap "$GIVEN_SLOTS")"; RESERVE="$(heavy_reserve "$slots")"
      # The reserve also stays below the slots free memory allows this pass, so a lane job keeps one (#2060).
      usable=1; while [ "$usable" -lt "$slots" ] && mem_ok "$((usable + 1))"; do usable=$((usable + 1)); done
      [ "$RESERVE" -gt "$((usable - 1))" ] && RESERVE=$((usable - 1))
    fi
    for k in $(seq 1 "$slots"); do
      if [ "$k" -eq 1 ]; then d="$base"; else d="$base.$k"; fi
      # Box-wide slots above 1 need the memory guard; the per-session pool doesn't.
      if [ "$label" = box-wide ] && [ "$PRIORITY" != meta ] && [ "$k" -le "$RESERVE" ]; then [ -d "$d" ] || reserve_blocked=1; continue; fi
      if [ "$k" -gt 1 ] && [ "$label" = box-wide ] && ! mem_ok "$k"; then continue; fi
      if mkdir "$d" 2>/dev/null; then got="$d"; break; fi
      if reclaim_slot "$d" && mkdir "$d" 2>/dev/null; then got="$d"; break; fi
    done
    [ -n "$got" ] && break
    [ "$printed" -eq 0 ] && { echo "heavy: waiting for the $label lock ($slots slot(s), $base)" >&2; printed=1; }
    [ "$waited" -ge "$TIMEOUT" ] && { echo "heavy: $label lock timeout ($base)" >&2; exit 75; }
    # A pass that skipped a free reserved slot was blocked only by the reservation and doesn't count; any other pass does.
    sleep 1; [ "$reserve_blocked" = 1 ] || waited=$((waited + 1))
  done
  echo "$$ $(date -u +%FT%TZ) session=$session $*" > "$got/owner" 2>/dev/null || true
  HELD+=("$got")
}

# Order: the session slot first, then the box-wide one, so a session never holds a box-wide slot while queued behind itself.
acquire session "$LOCKS/heavy-session/$session.lock" "$SESSION_SLOTS" "$@"
if [ "$mode" = expensive ]; then acquire box-wide "$LOCK" "$SLOTS" "$@"; export AW_HEAVY_SLOT="${HELD[1]}"; fi
export AW_HEAVY_LOCK_HELD=1 AW_HEAVY_MODE="$mode"
# Keep one job from taking the whole box: fewer cores per .NET process (MSBuild -m, test parallelism), no MSBuild
# worker reuse (nodes exit with the build instead of staying resident between builds).
export DOTNET_PROCESSOR_COUNT="${AW_HEAVY_CPUS:-4}" MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0
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
# This relies on job control being off (non-interactive bash): the background child is not a group leader, so setsid
# execs in place, $! is the job's pid and its group id. Under job control setsid would fork and $! would exit at once.
if command -v setsid >/dev/null 2>&1; then setsid "$@" & else perl -e 'setpgrp(0, 0); exec @ARGV or exit 127' "$@" & fi
job=$!
reason="$(mktemp "${TMPDIR:-/tmp}/heavy-reason.XXXXXX")"
(
  start=$SECONDS
  while kill -0 "$job" 2>/dev/null; do
    why=""
    [ "$run_max" -gt 0 ] && [ $((SECONDS - start)) -ge $((run_max * 60)) ] && why="ran past AW_HEAVY_RUN_MAX=${run_max}m"
    [ -z "$why" ] && [ -n "$claude_found" ] && ! kill -0 "$session" 2>/dev/null && why="session $session ended"
    if [ -n "$why" ]; then
      echo "$why" > "$reason"
      kill -TERM -- "-$job" 2>/dev/null
      for _ in $(seq 1 30); do kill -0 -- "-$job" 2>/dev/null || exit 0; sleep 1; done
      kill -KILL -- "-$job" 2>/dev/null
      exit 0
    fi
    sleep 5
  done
) &
dog=$!
wait "$job"; rc=$?
kill "$dog" 2>/dev/null; wait "$dog" 2>/dev/null
if [ -s "$reason" ]; then echo "heavy: $(cat "$reason"), killed: $*" >&2; rc=124; fi
rm -f "$reason"
exit $rc
}
