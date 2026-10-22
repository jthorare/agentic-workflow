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

# generate_map: the shape the context import accepts (keys follow the grammar, every implements/tests edge has evidence for a
# file that exists), over a throwaway git repo
G="$WORK/gitrepo"; mkdir -p "$G/docs/sindri" "$G/src/A" "$G/tests/T"
printf '# readme\n' > "$G/docs/sindri/README.md"; printf 'class A {}\n' > "$G/src/A/A.cs"; printf 'class B {}\n' > "$G/src/A/B.cs"
printf 'class ATests {}\n' > "$G/tests/T/ATests.cs"
git -C "$G" init -q && git -C "$G" add -A && git -C "$G" -c user.name=t -c user.email=t@t commit -qm init
printf '# Feature\nTouches `src/A/A.cs` and `src/A/B.cs`, tested by `tests/T/ATests.cs`; not `src/none.cs`.\n' > "$G/docs/f.md"
REPO="$G" FEATURE_REL="docs/f.md" CODE_FILE=""
feature_title() { echo "A feature"; }
generate_map "$WORK/map.yaml"
map="$(cat "$WORK/map.yaml")"
contains "map: feature node" "$map" 'key: "feature:sandbox"'
contains "map: names the code files" "$map" 'key: "code:src/A/B.cs"'
contains "map: names the test file" "$map" 'key: "code:tests/T/ATests.cs"'
contains "map: tests edge" "$map" 'type: tests, evidence: [{key: "repo:repo/tests/T/ATests.cs"}]'
contains "map: mentions edge" "$map" 'type: mentions'
check "map: a file that does not exist is not used" "$(grep -c 'none.cs' "$WORK/map.yaml")" "0"
check "map: two surfaces part-of the feature" "$(grep -c 'type: part-of' "$WORK/map.yaml")" "2"
bad=0
for k in $(grep -oE 'key: "(surface|feature|code|record):[^"]*"' "$WORK/map.yaml" | sed 's/key: "//; s/"$//'); do
  case "$k" in
    surface:api/* | surface:command/* | surface:dialog/* | surface:flag/* | surface:job/* | surface:keybinding/* | surface:menu/* | surface:notification/* | surface:pane/* | surface:permission/* | surface:report/* | surface:route/* | surface:screen/* | surface:setting/*) ;;
    surface:*) echo "  bad surface kind: $k"; bad=1 ;;
  esac
done
check "map: every surface uses a valid kind" "$bad" "0"
bad=0; impl=0
while IFS= read -r line; do
  case "$line" in *"type: implements"* | *"type: tests"*) ;; *) continue ;; esac
  impl=$((impl + 1))
  ev="$(printf '%s\n' "$line" | sed -n 's/.*evidence: \[{key: "repo:repo\/\([^"]*\)"}\].*/\1/p')"
  from="$(printf '%s\n' "$line" | sed -n 's/.*from: "code:\([^"]*\)".*/\1/p')"
  { [ -n "$ev" ] && [ -f "$G/$ev" ] && [ "$ev" = "$from" ]; } || { echo "  bad edge: $line"; bad=1; }
done < "$WORK/map.yaml"
check "map: every implements/tests edge has evidence for an existing file" "$bad" "0"
check "map: at least two implements edges and one tests edge" "$([ "$impl" -ge 3 ] && echo yes)" "yes"
CODE_FILE="docs/sindri/README.md"; generate_map "$WORK/map2.yaml"
contains "map: --code leads" "$(cat "$WORK/map2.yaml")" 'key: "code:docs/sindri/README.md"'
check "map: --code pads to two implements edges" "$(grep -c 'type: implements' "$WORK/map2.yaml")" "2"
CODE_FILE=""
printf '# Feature with nothing named\n' > "$G/docs/g.md"; FEATURE_REL="docs/g.md"; generate_map "$WORK/map3.yaml"
check "map: fallback gives two implements edges" "$(grep -c 'type: implements' "$WORK/map3.yaml")" "2"

# kill_launched_sessions: only the sessions whose pane process tree has this home's AW_SINDRI_HOME
KILLED="$WORK/killed"; : > "$KILLED"
launch_tmux() {
  case "$1" in
    list-sessions) printf 'snd-LCH-mine\nsnd-LCH-theirs\nsnd-LCH-deep\nother-session\n' ;;
    list-panes) case "$3" in =snd-LCH-mine) echo 100 ;; =snd-LCH-theirs) echo 200 ;; =snd-LCH-deep) echo 300 ;; esac ;;
    kill-session) echo "$3" >> "$KILLED" ;;
  esac
}
proc_children() { case "$1" in 300) echo 301 ;; 301) echo 302 ;; esac; }
proc_environ() {
  case "$1" in
    100) printf 'PATH=/bin\nAW_SINDRI_HOME=/scratch/mine\n' ;;
    200) printf 'AW_SINDRI_HOME=/scratch/mine-not\nAW_SINDRI_HOME2=/scratch/mine\n' ;;
    302) printf 'AW_SINDRI_HOME=/scratch/mine\n' ;;
    *) printf 'PATH=/bin\n' ;;
  esac
}
kill_launched_sessions /scratch/mine 2>/dev/null
check "cleanup: ends this home's sessions, a child's too, and no other" "$(sort "$KILLED" | tr '\n' ' ')" "=snd-LCH-deep =snd-LCH-mine "
: > "$KILLED"; kill_launched_sessions "" 2>/dev/null
check "cleanup: no home kills nothing" "$(wc -c < "$KILLED" | tr -d ' ')" "0"


# auto-desk: a choice outcome takes doneAsking where offered, else its first; fact:answers gets [] and any other {}
SCRATCH="$WORK/desk-scratch"; DESK_MAX=5; DESK_LOG=(); COMPLETED="$WORK/completed"; mkdir -p "$SCRATCH"
say() { :; }
human() { printf '%s\n' "$*" >> "$COMPLETED"; }
DESK_NEXT='Answer the questions, then run: sindri workflow complete A/1 --run 7.1 --outcome
<answered|doneAsking> --output fact:answers=<file>'
status_json() { jq -n --arg n "$DESK_NEXT" '{next: $n}'; }
auto_desk A/1 2>/dev/null
check "auto-desk: an interview's choice closes it" "$(cat "$COMPLETED")" "workflow complete A/1 --run 7.1 --outcome doneAsking --output fact:answers=$SCRATCH/desk/0-answers.json"
check "auto-desk: fact:answers is an empty list" "$(cat "$SCRATCH/desk/0-answers.json")" "[]"
: > "$COMPLETED"; DESK_NEXT='run: sindri workflow complete B/1 --run 2.1 --outcome <pass|fail> --output fact:verdict=<file>'
auto_desk B/1 2>/dev/null
check "auto-desk: another choice takes its first" "$(cat "$COMPLETED")" "workflow complete B/1 --run 2.1 --outcome pass --output fact:verdict=$SCRATCH/desk/1-verdict.json"
check "auto-desk: another fact is an empty object" "$(cat "$SCRATCH/desk/1-verdict.json")" "{}"
# auto-desk: a refusal that names a required output gets that output's placeholder and one more try
: > "$COMPLETED"; DESK_LOG=()
human() {
  printf '%s\n' "$*" >> "$COMPLETED"
  case "$*" in
    *interviewClosed*) echo "completed" ;;
    *) echo "SND-WF-008 step.interview.answer reported 'doneAsking' without fact:interviewClosed (fix: include the required output fact:interviewClosed)" ;;
  esac
}
DESK_NEXT='run: sindri workflow complete C/1 --run 7.1 --outcome <answered|doneAsking> --output fact:answers=<file>'
auto_desk C/1 2>/dev/null
check "auto-desk: the retry adds the required output" "$(tail -1 "$COMPLETED")" "workflow complete C/1 --run 7.1 --outcome doneAsking --output fact:answers=$SCRATCH/desk/0-answers.json --output fact:interviewClosed=$SCRATCH/desk/0-interviewClosed.json"
check "auto-desk: two calls in all" "$(wc -l < "$COMPLETED" | tr -d ' ')" "2"
check "auto-desk: the log shows the outputs sent last" "${DESK_LOG[0]}" "C/1 run 7.1 -> doneAsking --output fact:answers=$SCRATCH/desk/0-answers.json --output fact:interviewClosed=$SCRATCH/desk/0-interviewClosed.json"
# auto-grant: a budget park gets a token grant, logged with the desk answers, and stops at --desk-max. The first call
# types an empty line and reads the challenge from the preview (it names the parked child); the second types it back
: > "$COMPLETED"; DESK_LOG=(); DESK_MAX=2
human() {
  local typed; typed="$(cat)"
  printf '%s\n' "$*" >> "$COMPLETED"
  if [ -z "$typed" ]; then echo "    type 'grant-tokens 200000 P/1/child.1' to confirm: "; else printf '%s\n' "$typed" > "$WORK/typed"; fi
}
auto_grant P/1 2>/dev/null
check "auto-grant: types the challenge the preview names" "$(cat "$WORK/typed")" "grant-tokens 200000 P/1/child.1"
check "auto-grant: answers with a token grant, preview first" "$(sort -u "$COMPLETED")" "workflow answer P/1 --grant-tokens 200000"
check "auto-grant: two calls per grant" "$(wc -l < "$COMPLETED" | tr -d ' ')" "2"
check "auto-grant: logged" "${DESK_LOG[0]}" "P/1 grant-tokens 200000"
auto_grant P/1 2>/dev/null
auto_grant P/1 2>/dev/null; rc=$?
check "auto-grant: stops at --desk-max" "$rc $(wc -l < "$COMPLETED" | tr -d ' ')" "1 4"
exit "$fail"
