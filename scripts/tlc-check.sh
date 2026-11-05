#!/usr/bin/env bash
# scripts/tlc-check.sh — run TLC on every TLA+ spec in the repo (CI job "TLA+ specs (TLC)").
#
# Usage: scripts/tlc-check.sh [--root <dir>] [--timeout <seconds>] [--witnesses] [<spec-dir>...]
#   A spec is a folder specs/tla/<Name>/ with <Name>.tla, one or more *.cfg, an optional MC<Name>.tla (TLC runs on it
#   instead of <Name>.tla when present) and a README.md. With no arguments every specs/tla/*/ folder runs, sorted; with
#   arguments only those folders do (relative ones are taken from the repo root).
#   For each folder and each *.cfg in it (sorted) this runs 'tlc.sh check <spec>.tla --config <cfg>' from
#   skills/formal-verify/scripts/tlc.sh (one worker). It prints one line per check,
#   'tlc-check: <Name>/<cfg>: <tlc.sh's last line without "tlc: ">', and tlc.sh's full output only when a check isn't PASS.
#   README.md line 1 of a spec folder names the code it models: 'models: <path>[:<line>] ...' (paths from the repo root, a
#   trailing / is a folder). A line 1 without 'models: ' or a path that doesn't exist prints
#   'tlc-check: <Name>: warning: ...'. Warnings never change the exit code.
#   --root <dir>: take the spec folders (specs/tla/*/), relative <spec-dir> arguments and README 'models:' paths from <dir>
#   instead of this script's own checkout (a toolkit copy of this script checking another repo). A missing <dir> exits 2.
#   --timeout <seconds>: a positive integer, per check. A check that runs longer gets its whole process group TERMed, then
#   KILLed after 5 s (a check can overrun by up to <s>+6 s: the 1 s poll plus the grace), prints 'tlc-check: <Name>/<cfg>: TIMEOUT after <s> s' and counts as exit 2. No flag, no timeout.
#   --witnesses: after a folder's own cfgs also run every <folder>/gaps/*.cfg, each of which must FAIL. Line 1 of a gap cfg
#   is '\* witness: <Name>', the invariant or property it is planted to break. Prints 'tlc-check: <Spec>/gaps/<cfg>:
#   WITNESS-CAUGHT <kind> <Name>' (exit 0), 'WITNESS-WRONG <kind> <actual> (expected <Name>)' or 'WITNESS-MISSED <Name>'
#   (the cfg passed) (both exit 1), or 'ERROR no witness line' (exit 2). Without the flag gaps/ is not run.
#   The per-cfg line 'tlc-check: <Name>/<cfg>: <result>' is parsed by a Sindri step: keep its format stable.
# Env: TLC_SH overrides the path to tlc.sh (the test's stub).
# Exit: 0 every check passed, 1 a violation, 2 an error (also a folder without <Name>.tla or without a .cfg, a bad
#       argument, no spec folders), 3 java or the jar is missing (tlc.sh's fix line is in the output above). The worst
#       code wins, ordered 3 > 2 > 1 > 0. With --timeout, a TERM, INT or HUP to this script stops the running check's
#       group first, then exits 143, 130 or 129 (128 + the signal).
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TLC_SH="${TLC_SH:-$ROOT/skills/formal-verify/scripts/tlc.sh}"

usage() { sed -n '4,5p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

worst=0
# note_rc <rc>: keeps the worst of the codes seen (3 > 2 > 1 > 0).
note_rc() {
  local rank_new rank_old
  case "$1" in 0) rank_new=0 ;; 1) rank_new=1 ;; 3) rank_new=3 ;; *) rank_new=2 ;; esac
  case "$worst" in 0) rank_old=0 ;; 1) rank_old=1 ;; 3) rank_old=3 ;; *) rank_old=2 ;; esac
  if [ "$rank_new" -gt "$rank_old" ]; then worst="$rank_new"; fi
  return 0
}

# check_models <dir> <name>: warns when README.md line 1 isn't 'models: <paths>' or names a path that doesn't exist.
check_models() {
  local first="" entry path
  if [ -f "$1/README.md" ]; then IFS= read -r first <"$1/README.md" || true; fi
  case "$first" in
    "models: "*) ;;
    *) echo "tlc-check: $2: warning: README line 1 is not models:"; return 0 ;;
  esac
  for entry in ${first#models: }; do
    path="${entry%%:[0-9]*}"
    case "$path" in /*) ;; *) path="$ROOT/$path" ;; esac
    if [ ! -e "$path" ]; then echo "tlc-check: $2: warning: models path missing: ${entry%%:[0-9]*}"; fi
  done
  return 0
}

# run_timed <seconds>: runs tlc.sh for $spec/$cfg in its own process group (bash job control, 'set -m': portable to macOS,
# which has no setsid binary), output to $tout. Sets rc; 124 marks the timeout.
run_timed() {
  local limit="$1" pid waited=0 grace=0
  # The check's group is not ours, so a signal that kills us would leave TLC running: stop its group first. The traps are
  # set before the check starts; a signal after the '&' but before CHILD_PID is set finds the check through $! (it differs
  # from PREV_BG once the check exists).
  CHILD_PID=""
  PREV_BG="${!:-}"
  trap 'abort_timed 143' TERM
  trap 'abort_timed 130' INT
  trap 'abort_timed 129' HUP
  set -m
  bash "$TLC_SH" check "$spec" --config "$cfg" >"$tout" 2>&1 &
  pid=$!
  set +m
  CHILD_PID="$pid"
  while kill -0 "$pid" 2>/dev/null && [ "$waited" -lt "$limit" ]; do sleep 1; waited=$((waited + 1)); done
  if kill -0 "$pid" 2>/dev/null; then
    kill -TERM -- "-$pid" 2>/dev/null || true
    while kill -0 "$pid" 2>/dev/null && [ "$grace" -lt 5 ]; do sleep 1; grace=$((grace + 1)); done
    kill -KILL -- "-$pid" 2>/dev/null || true
    { wait "$pid"; } 2>/dev/null || true
    rc=124
  else
    set +e; { wait "$pid"; } 2>/dev/null; rc=$?; set -e
  fi
  trap - TERM INT HUP
  CHILD_PID=""
}

# abort_timed <exit code>: TERM, 5 s grace, KILL for the running check's group, drop its temp file, exit 128+signal.
abort_timed() {
  local grace=0
  trap - TERM INT HUP
  if [ -z "$CHILD_PID" ] && [ -n "${!:-}" ] && [ "${!:-}" != "$PREV_BG" ]; then CHILD_PID="$!"; fi
  if [ -n "$CHILD_PID" ]; then
    kill -TERM -- "-$CHILD_PID" 2>/dev/null || true
    while kill -0 "$CHILD_PID" 2>/dev/null && [ "$grace" -lt 5 ]; do sleep 1; grace=$((grace + 1)); done
    kill -KILL -- "-$CHILD_PID" 2>/dev/null || true
    { wait "$CHILD_PID"; } 2>/dev/null || true
  fi
  rm -f "$tout"
  exit "$1"
}
CHILD_PID=""
PREV_BG=""

TIMEOUT=""
WITNESSES=""
dirs=()
while [ $# -gt 0 ]; do
  case "$1" in
    --root)
      if [ $# -lt 2 ] || [ ! -d "$2" ]; then echo "tlc-check: --root needs an existing directory" >&2; exit 2; fi
      ROOT="$(cd "$2" && pwd)"; shift 2 ;;
    --timeout)
      case "${2:-}" in ''|*[!0-9]*|0*) echo "tlc-check: --timeout needs a positive integer (seconds)" >&2; exit 2 ;; esac
      TIMEOUT="$2"; shift 2 ;;
    --witnesses) WITNESSES=1; shift ;;
    -*) usage ;;
    *) dirs+=("$1"); shift ;;
  esac
done
if [ ${#dirs[@]} -eq 0 ]; then
  for d in "$ROOT"/specs/tla/*/; do
    if [ -d "$d" ]; then dirs+=("${d%/}"); fi
  done
  if [ ${#dirs[@]} -eq 0 ]; then
    echo "tlc-check: no spec folders under specs/tla/" >&2
    exit 2
  fi
fi

for dir in "${dirs[@]}"; do
  case "$dir" in /*) ;; *) dir="$ROOT/${dir#./}" ;; esac
  dir="${dir%/}"
  name="$(basename "$dir")"
  if [ ! -d "$dir" ]; then
    echo "tlc-check: $name: ERROR no such folder $dir" >&2; note_rc 2; continue
  fi
  check_models "$dir" "$name"
  if [ -f "$dir/MC$name.tla" ]; then spec="$dir/MC$name.tla"
  elif [ -f "$dir/$name.tla" ]; then spec="$dir/$name.tla"
  else
    echo "tlc-check: $name: ERROR no $name.tla in $dir" >&2; note_rc 2; continue
  fi
  cfgs=()
  for c in "$dir"/*.cfg; do
    if [ -f "$c" ]; then cfgs+=("$c"); fi
  done
  if [ ${#cfgs[@]} -eq 0 ]; then
    echo "tlc-check: $name: ERROR no .cfg in $dir" >&2; note_rc 2; continue
  fi
  if [ -n "$WITNESSES" ]; then
    for c in "$dir"/gaps/*.cfg; do
      if [ -f "$c" ]; then cfgs+=("$c"); fi
    done
  fi
  for cfg in "${cfgs[@]}"; do
    gap=""
    case "$cfg" in "$dir"/gaps/*) gap=1; label="$name/gaps/$(basename "$cfg")" ;; *) label="$name/$(basename "$cfg")" ;; esac
    if [ -n "$gap" ]; then
      witness=""
      IFS= read -r first <"$cfg" || true
      case "$first" in '\* witness: '*) witness="${first#'\* witness: '}"; witness="${witness%% *}" ;; esac
      if [ -z "$witness" ]; then
        echo "tlc-check: $label: ERROR no witness line"; note_rc 2; continue
      fi
    fi
    if [ -n "$TIMEOUT" ]; then
      tout="$(mktemp "${TMPDIR:-/tmp}/tlc-check.XXXXXX")"
      run_timed "$TIMEOUT"
      out="$(cat "$tout")"; rm -f "$tout"
      if [ "$rc" -eq 124 ]; then
        echo "tlc-check: $label: TIMEOUT after $TIMEOUT s"
        printf '%s\n' "$out" | sed 's/^/  | /'
        note_rc 2
        continue
      fi
    else
      set +e
      out="$(bash "$TLC_SH" check "$spec" --config "$cfg" 2>&1)"
      rc=$?
      set -e
    fi
    last="$(printf '%s\n' "$out" | grep '^tlc: ' | tail -n 1 || true)"
    last="${last#tlc: }"
    if [ -z "$last" ]; then last="ERROR no result line from tlc.sh (exit $rc)"; fi
    if [ "$rc" -eq 0 ]; then
      case "$last" in PASS*) ;; *) rc=2 ;; esac
    fi
    if [ -n "$gap" ]; then
      case "$last" in
        PASS*) last="WITNESS-MISSED $witness"; rc=1 ;;
        "VIOLATION "*)
          set -- $last
          kind="${2:-}"; actual="${3:-}"
          if [ "$actual" = "$witness" ]; then last="WITNESS-CAUGHT $kind $witness"; rc=0
          else last="WITNESS-WRONG $kind ${actual:-unnamed} (expected $witness)"; rc=1; fi ;;
      esac
    fi
    echo "tlc-check: $label: $last"
    if [ "$rc" -ne 0 ]; then
      printf '%s\n' "$out" | sed 's/^/  | /'
    fi
    note_rc "$rc"
  done
done
exit "$worst"
