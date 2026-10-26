#!/usr/bin/env bash
# scripts/run-test-guard.sh — run a check with a wall-clock limit and leave nothing behind (#1130). The same idea as the
# C# bugfix-state's run-test (ProcessCheckRunner stops what a check started), for the old TS bugfix-state the lanes still
# run in-flight items with, whose run-test only kills the shell at its limit.
#
#   run-test-guard.sh [--timeout SECS] [--grace SECS] -- <command...>
#
# Put it INSIDE bugfix-state's run-test, around the check itself:
#   node "$BFS_JS" run-test --state S --check C --cwd W -- scripts/run-test-guard.sh --timeout 1200 -- bash C
#
# What it does:
#  1. Tags the command with a unique AW_RUN_GUARD=<token> in its environment and starts it in its own process group (setsid).
#  2. After --timeout seconds (default 1200), TERMs the group, waits --grace seconds (default 30), then KILLs it. Exit 124.
#  3. When the command ends (any way, or the guard itself gets TERM/INT/HUP), it stops the group, then every process of this
#     user that still carries the token in its environment. Environment survives setsid and daemonizing, so a detached
#     `sindri serve` or `temporal server start-dev` the check started is found and stopped too.
# Signals only PIDs it found by its own token (never a name pattern). Linux only (it reads /proc/<pid>/environ).
# Exit: the command's own code, 124 on a timeout, 143 when the guard itself was stopped, 2 on a usage error.
set -u
timeout_s=1200; grace=30
usage="usage: run-test-guard.sh [--timeout SECS] [--grace SECS] -- <command...>"
while [ $# -gt 0 ]; do
  case "$1" in
    --timeout) [ $# -ge 2 ] || { echo "run-test-guard: --timeout needs a value ($usage)" >&2; exit 2; }; timeout_s=$2; shift 2 ;;
    --grace) [ $# -ge 2 ] || { echo "run-test-guard: --grace needs a value ($usage)" >&2; exit 2; }; grace=$2; shift 2 ;;
    --) shift; break ;;
    *) echo "run-test-guard: unknown option $1 ($usage)" >&2; exit 2 ;;
  esac
done
[ $# -ge 1 ] || { echo "run-test-guard: no command ($usage)" >&2; exit 2; }
case "$timeout_s$grace" in *[!0-9]*|"") echo "run-test-guard: --timeout and --grace are whole seconds ($usage)" >&2; exit 2 ;; esac

token="rtg-$$-$(date +%s%N)"

tagged() {  # every PID of this user (but us) whose environment holds the token
  local p
  for p in /proc/[0-9]*; do
    p=${p#/proc/}
    { [ "$p" = "$$" ] || [ "$p" = "$BASHPID" ]; } && continue
    [ -O "/proc/$p" ] || continue
    { tr '\0' '\n' < "/proc/$p/environ"; } 2>/dev/null | grep -qx "AW_RUN_GUARD=$token" && echo "$p"
  done
}

sweep() {
  [ -n "${pgid:-}" ] && kill -TERM -- "-$pgid" 2>/dev/null
  local left; left=$(tagged)
  [ -n "$left" ] && kill -TERM $left 2>/dev/null
  local i; for i in $(seq 1 "$grace"); do left=$(tagged); [ -z "$left" ] && break; sleep 1; done
  if [ -n "$left" ]; then
    echo "run-test-guard: killing what outlived TERM: $(echo $left)" >&2
    kill -KILL $left 2>/dev/null
  fi
  [ -n "${pgid:-}" ] && kill -KILL -- "-$pgid" 2>/dev/null
  return 0
}

AW_RUN_GUARD="$token" setsid "$@" &
pgid=$!
trap 'sweep; exit 143' TERM INT HUP

( trap 'kill "$s" 2>/dev/null; exit 0' TERM; sleep "$timeout_s" & s=$!; wait "$s"
  kill -0 "$pgid" 2>/dev/null && { echo "run-test-guard: timed out after ${timeout_s}s; stopping the check" >&2; kill -TERM -- "-$pgid" 2>/dev/null; } ) &
watchdog=$!

wait "$pgid"; rc=$?
timed_out=0
if ! kill -0 "$watchdog" 2>/dev/null; then timed_out=1; fi
kill "$watchdog" 2>/dev/null; wait "$watchdog" 2>/dev/null
sweep
[ "$timed_out" = 1 ] && [ "$rc" -ne 0 ] && rc=124
exit "$rc"
