#!/usr/bin/env bash
# Tests for scripts/sindri-sandbox-e2e.sh: argument parsing, the --sindri rewrite and the scratch guard. Run:
#   bash scripts/tests/sindri-sandbox-e2e.test.sh
# Nothing here makes a scratch home, starts a sindri or opens a socket: every case ends in parse_args, --print-config
# or guard_scratch.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
SCRIPT="$ROOT/scripts/sindri-sandbox-e2e.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
contains() { # $1 name, $2 haystack, $3 needle
  case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
FEATURE="$WORK/feature.md"
printf '# A feature\n' > "$FEATURE"

run() { bash "$SCRIPT" "$@" 2>&1; echo "rc=$?"; }

out="$(run)"
contains "no --feature is exit 2" "$out" "--feature <spec-file> is required"
contains "no --feature exit code" "$out" "rc=2"

out="$(run --feature "$WORK/missing.md")"
contains "a missing feature file is refused" "$out" "doesn't exist"

out="$(run --feature "$FEATURE" --bogus)"
contains "an unknown argument is refused" "$out" "unknown argument --bogus"
contains "an unknown argument is exit 2" "$out" "rc=2"

out="$(run --feature "$FEATURE" --stall ten)"
contains "--stall takes whole seconds" "$out" "--stall takes whole seconds"

out="$(run --feature "$FEATURE" --start line)"
contains "--start takes a recipe id" "$out" "--start takes a recipe id"

out="$(run --feature "$FEATURE" --repo "$WORK")"
contains "--repo must be a git checkout" "$out" "is not a git checkout"

out="$(run --feature "$FEATURE" --stall)"
contains "a flag with no value is refused" "$out" "--stall needs seconds"

out="$(run --feature "$FEATURE" --print-config)"
contains "defaults: start" "$out" "start=workflow.line"
contains "defaults: stall" "$out" "stall=300"
contains "defaults: publish" "$out" "sindri=publish"
contains "defaults: no auto-desk" "$out" "auto-desk=0"
contains "defaults: context on" "$out" "context=1"
contains "defaults: desk-max" "$out" "desk-max=25"
contains "defaults: no agent" "$out" "agent=none"
contains "--print-config is exit 0" "$out" "rc=0"

out="$(run --feature "$FEATURE" --print-config --auto-desk --no-context --keep --stall 5 --max-wait 9 --desk-max 3 --agent claude-code --start workflow.scoping)"
contains "flags: auto-desk" "$out" "auto-desk=1"
contains "flags: context off" "$out" "context=0"
contains "flags: keep" "$out" "keep=1"
contains "flags: stall" "$out" "stall=5"
contains "flags: max-wait" "$out" "max-wait=9"
contains "flags: start" "$out" "start=workflow.scoping"
contains "flags: desk-max" "$out" "desk-max=3"
contains "flags: agent" "$out" "agent=claude-code"

out="$(run --feature "$FEATURE" --desk-max many)"
contains "--desk-max takes a number" "$out" "--desk-max takes a whole number"

# --sindri: a dotnet <dll> is replaced by the apphost beside it, when there is one
mkdir -p "$WORK/bin"
: > "$WORK/bin/sindri.dll"; printf '#!/bin/sh\n' > "$WORK/bin/sindri"; chmod +x "$WORK/bin/sindri"
out="$(run --feature "$FEATURE" --print-config --sindri "dotnet $WORK/bin/sindri.dll")"
contains "--sindri dll becomes the apphost" "$out" "sindri=$WORK/bin/sindri"
contains "--sindri says why" "$out" "serve re-runs its own executable"
out="$(run --feature "$FEATURE" --print-config --sindri "dotnet $WORK/nowhere/sindri.dll")"
contains "--sindri dll without an apphost is kept" "$out" "sindri=dotnet $WORK/nowhere/sindri.dll"
out="$(run --feature "$FEATURE" --print-config --sindri "$WORK/bin/sindri --flag")"
contains "--sindri other command lines are kept" "$out" "sindri=$WORK/bin/sindri --flag"

# the guard: sourced, so guard_scratch runs without main
# shellcheck source=../sindri-sandbox-e2e.sh
source "$SCRIPT"
T="$WORK/tmproot"; mkdir -p "$T/scratch" "$WORK/elsewhere"
TMPDIR="$T" guard_scratch "$T/scratch" 2>/dev/null; check "a dir inside TMPDIR passes" "$?" "0"
TMPDIR="$T" guard_scratch "$T" 2>/dev/null; check "TMPDIR itself is refused" "$?" "2"
err="$(TMPDIR="$T" guard_scratch "$WORK/elsewhere" 2>&1)"; check "a dir outside TMPDIR is refused" "$?" "2"
contains "the refusal names the rule" "$err" "is not strictly inside"
ln -s "$WORK/elsewhere" "$T/link"
TMPDIR="$T" guard_scratch "$T/link" 2>/dev/null; check "a link out of TMPDIR is refused" "$?" "2"
TMPDIR="$T" guard_scratch "$T/missing" 2>/dev/null; check "a missing dir is refused" "$?" "2"

exit "$fail"
