#!/usr/bin/env bash
# scripts/tlc-check.sh — run TLC on every TLA+ spec in the repo (CI job "TLA+ specs (TLC)").
#
# Usage: scripts/tlc-check.sh [<spec-dir>...]
#   A spec is a folder specs/tla/<Name>/ with <Name>.tla, one or more *.cfg, an optional MC<Name>.tla (TLC runs on it
#   instead of <Name>.tla when present) and a README.md. With no arguments every specs/tla/*/ folder runs, sorted; with
#   arguments only those folders do (relative ones are taken from the repo root).
#   For each folder and each *.cfg in it (sorted) this runs 'tlc.sh check <spec>.tla --config <cfg>' from
#   skills/formal-verify/scripts/tlc.sh (one worker). It prints one line per check,
#   'tlc-check: <Name>/<cfg>: <tlc.sh's last line without "tlc: ">', and tlc.sh's full output only when a check isn't PASS.
# Env: TLC_SH overrides the path to tlc.sh (the test's stub).
# Exit: 0 every check passed, 1 a violation, 2 an error (also a folder without <Name>.tla or without a .cfg, a bad
#       argument, no spec folders), 3 java or the jar is missing (tlc.sh's fix line is in the output above). The worst
#       code wins, ordered 3 > 2 > 1 > 0.
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

dirs=()
while [ $# -gt 0 ]; do
  case "$1" in
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
  for cfg in "${cfgs[@]}"; do
    label="$name/$(basename "$cfg")"
    set +e
    out="$(bash "$TLC_SH" check "$spec" --config "$cfg" 2>&1)"
    rc=$?
    set -e
    last="$(printf '%s\n' "$out" | grep '^tlc: ' | tail -n 1 || true)"
    last="${last#tlc: }"
    if [ -z "$last" ]; then last="ERROR no result line from tlc.sh (exit $rc)"; fi
    if [ "$rc" -eq 0 ]; then
      case "$last" in PASS*) ;; *) rc=2 ;; esac
    fi
    echo "tlc-check: $label: $last"
    if [ "$rc" -ne 0 ]; then
      printf '%s\n' "$out" | sed 's/^/  | /'
    fi
    note_rc "$rc"
  done
done
exit "$worst"
