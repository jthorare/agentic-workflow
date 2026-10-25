#!/usr/bin/env bash
# scripts/lib/sindri-open-runs.sh — sourced by scripts/sindri-app.sh: warns, or waits, before a publish when Sindri has agent
# runs open (#1000: a reinstall republished Sindri while an agent Step was running, serve died during the publish and the
# agent's result was stranded).
#
#   sindri_open_runs_guard <sindri-binary>
#
# Reads `<sindri-binary> agents --json` (read-only; it never starts the control plane). An open agent run is a session with a
# non-null `launch.instance`. Returns 0 to go on, 1 to stop (nothing was changed). Never aborts a `set -euo pipefail` caller.
#   - plane not running (exit 1, SND-API-001), or no open runs: return 0, silent.
#   - the read fails or does not parse: one warning line on stderr, return 0.
#   - open runs: list them on stderr, then
#       AW_SINDRI_STOP_OK=1   go on (says so).
#       a terminal (stdin and stderr): 'go' goes on, 'wait' polls every ${AW_SINDRI_OPEN_RUNS_POLL:-30} s until none are open
#                             (or the plane is down), anything else returns 1.
#       no terminal           return 1.
# Bash 3.2 compatible (macOS /bin/bash).

# Prints the open runs as lines "<ref>\t<instance>\t<launch state>\t<state>" and returns 0; returns 2 when the plane is down
# (nothing printed) and 3 on any other failure (the reason on stdout).
_sindri_open_runs_read() { # $1 = sindri binary
  local out rc=0 rows
  out="$("$1" agents --json </dev/null 2>&1)" || rc=$?
  if [ "$rc" -ne 0 ]; then
    case "$out" in *SND-API-001*) return 2 ;; esac
    printf 'exit %s' "$rc"; return 3
  fi
  rows="$(printf '%s' "$out" | jq -r '.sessions[] | select(.launch.instance != null) | [.ref, .launch.instance, (.launch.state // "?"), .state] | @tsv' 2>/dev/null)" \
    || { printf 'not JSON'; return 3; }
  printf '%s' "$rows"
}

sindri_open_runs_guard() { # $1 = the sindri binary
  local rows rc=0 n answer poll
  rows="$(_sindri_open_runs_read "$1")" || rc=$?
  case "$rc" in
    0) ;;
    2) return 0 ;;
    *) echo "sindri-app: couldn't read the open agent runs ($rows); going on" >&2; return 0 ;;
  esac
  [ -n "$rows" ] || return 0
  n="$(printf '%s\n' "$rows" | wc -l | tr -d ' ')"
  {
    echo "sindri-app: $n agent run(s) are open in Sindri; publishing now can stop the control plane mid-Step (#1000):"
    printf '%s\n' "$rows" | while IFS="$(printf '\t')" read -r ref inst lstate state; do
      echo "  $ref  $inst · $lstate  $state"
    done
  } >&2
  if [ "${AW_SINDRI_STOP_OK:-0}" = 1 ]; then
    echo "sindri-app: AW_SINDRI_STOP_OK=1: going on" >&2
    return 0
  fi
  if [ -t 0 ] && [ -t 2 ]; then
    printf "type 'go' to go on now, 'wait' to wait until they finish, anything else stops: " >&2
    answer=""
    read -r answer || true
    case "$answer" in
      go) return 0 ;;
      wait)
        poll="${AW_SINDRI_OPEN_RUNS_POLL:-30}"
        local last="$n"
        while :; do
          sleep "$poll"
          rc=0
          rows="$(_sindri_open_runs_read "$1")" || rc=$?
          if [ "$rc" -eq 2 ]; then return 0; fi
          if [ "$rc" -ne 0 ]; then echo "sindri-app: couldn't read the open agent runs ($rows); going on" >&2; return 0; fi
          [ -n "$rows" ] || return 0
          n="$(printf '%s\n' "$rows" | wc -l | tr -d ' ')"
          if [ "$n" != "$last" ]; then echo "sindri-app: $n agent run(s) still open" >&2; last="$n"; fi
        done
        ;;
      *) echo "stopped; nothing was changed" >&2; return 1 ;;
    esac
  fi
  echo "not a terminal: stopping. Let the runs finish, or set AW_SINDRI_STOP_OK=1 to go on anyway" >&2
  return 1
}
