#!/usr/bin/env bash
# Tests for scripts/aw-run.sh (#4237): the runner that saves a command's full stdout and stderr to private files and prints a
# bounded summary. Real child processes and files in a temp AW_STATE_DIR; every process it signals is one it recorded.
# Covers: a 10 MB log leaves a complete artifact and a summary under 8 KB; a failing exit is kept with its failure lines; a
# child's own exit 124 is "failed", not "timeout"; a silent command is "ok (no output)", never "passed"; a timeout stops the
# command's process group (grandchild too) with exit 124; a group member that outlives the KILL is "error" with its pid; an
# interrupt stops the group with exit 130; leftovers of a finished command are stopped; argv is never eval'd; run folders are
# private and distinct; the 7-day prune removes only run-shaped folders and keeps symlinks; `show` returns only the slice.
# Run: bash scripts/tests/aw-run.test.sh
set -uo pipefail
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN="$DIR/aw-run.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { # has <name> <text> <fixed string>
  case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in: $2"; fail=1 ;; esac
}
lacks() { # lacks <name> <text> <fixed string>
  case "$2" in *"$3"*) echo "not ok - $1"; echo "  unexpected: $3"; echo "  in: $2"; fail=1 ;; *) echo "ok - $1" ;; esac
}
alive() { if [ -z "$1" ]; then echo "no pid recorded"; elif kill -0 "$1" 2> /dev/null; then echo alive; else echo gone; fi; }
perm() { stat -c %a "$1" 2> /dev/null || stat -f %Lp "$1"; }
size() { wc -c < "$1" | tr -d ' '; }
artifacts() { sed -n 's/^artifacts: \([^ ]*\).*$/\1/p' <<< "$1"; }

WORK="$(cd "$(mktemp -d)" && pwd -P)"
pids=()   # the escape case's perl parent and sleep, stopped by recorded pid at exit
cleanup() { local p; for p in "${pids[@]+"${pids[@]}"}"; do kill -KILL "$p" 2> /dev/null; done; find "$WORK" -delete 2> /dev/null; }
trap cleanup EXIT
export AW_STATE_DIR="$WORK/state"

# --- usage ---
bash "$RUN" > /dev/null 2>&1; check "no command is exit 125" "$?" "125"
bash "$RUN" --bogus -- true > /dev/null 2>&1; check "an unknown flag is exit 125" "$?" "125"

# --- a large log: complete artifact, bounded summary ---
out="$(bash "$RUN" --json -- sh -c 'yes abcdefghij0123456789 | head -c 10485760')"; rc=$?
check "a 10 MB log: exit 0" "$rc" "0"
check "a 10 MB log: outcome ok" "$(jq -r .outcome <<< "$out")" "ok"
check "a 10 MB log: stdout bytes counted" "$(jq -r .stdout.bytes <<< "$out")" "10485760"
check "a 10 MB log: stdout marked truncated" "$(jq -r .stdout.truncated <<< "$out")" "true"
big="$(jq -r .artifactDir <<< "$out")"
check "a 10 MB log: the artifact is complete" "$(size "$big/stdout.log")" "10485760"
check "a 10 MB log: the JSON summary is at most 8 KB" "$([ "$(printf '%s' "$out" | wc -c)" -le 8192 ] && echo yes || echo no)" "yes"
text="$(bash "$RUN" -- sh -c 'yes abcdefghij0123456789 | head -c 10485760')"
check "a 10 MB log: the text summary is at most 8 KB" "$([ "$(printf '%s' "$text" | wc -c)" -le 8192 ] && echo yes || echo no)" "yes"
has "a 10 MB log: the text summary names the artifacts" "$text" "artifacts: $AW_STATE_DIR/runs/"

# --- a failure keeps its exit, its failure lines, and the two streams apart ---
text="$(bash "$RUN" -- sh -c 'echo building; echo "error: boom" >&2; exit 3')"; rc=$?
check "a failure: exit 3 is kept" "$rc" "3"
check "a failure: line 1 says failed with the exit" "$(head -1 <<< "$text" | cut -d' ' -f1-3)" "aw-run: failed exit=3"
has "a failure: the failure line is shown, tagged with its stream" "$text" "[stderr] error: boom"
fdir="$(artifacts "$text")"
check "a failure: stdout.log holds stdout only" "$(cat "$fdir/stdout.log")" "building"
check "a failure: stderr.log holds stderr only" "$(cat "$fdir/stderr.log")" "error: boom"
check "a failure: meta.json records the exit" "$(jq -r .exit "$fdir/meta.json")" "3"

# --- the outcome comes from what the runner did, never from the exit number ---
out="$(bash "$RUN" --json -- sh -c 'exit 124')"; rc=$?
check "a child's own exit 124: exit 124 is kept" "$rc" "124"
check "a child's own exit 124: outcome failed" "$(jq -r .outcome <<< "$out")" "failed"
check "a child's own exit 124: not timed out" "$(jq -r .timedOut <<< "$out")" "false"
out="$(bash "$RUN" --json -- sh -c 'exit 125')"
check "a child's own exit 125: outcome failed" "$(jq -r .outcome <<< "$out")" "failed"

# --- a silent command is not a pass ---
text="$(bash "$RUN" -- true)"; rc=$?
check "a silent command: exit 0" "$rc" "0"
check "a silent command: line 1 says ok" "$(head -1 <<< "$text" | cut -d' ' -f1-3)" "aw-run: ok exit=0"
has "a silent command: says it printed nothing" "$text" "(no output)"
lacks "a silent command: never says passed" "$(tr '[:upper:]' '[:lower:]' <<< "$text")" "pass"
text="$(bash "$RUN" -- false)"; rc=$?
check "a silent failure: exit 1" "$rc" "1"
check "a silent failure: line 1 says failed" "$(head -1 <<< "$text" | cut -d' ' -f1-2)" "aw-run: failed"

# --- argv runs as given, never through eval ---
text="$(bash "$RUN" -- printf '%s\n' '$(echo pwned)')"
has "argv is not eval'd" "$text" '$(echo pwned)'

# --- a timeout stops the whole group ---
start=$SECONDS
out="$(bash "$RUN" --json --timeout 2 --grace 1 -- sh -c 'sleep 60 & echo $! > "$1"; sleep 60' sh "$WORK/gc.pid")"; rc=$?
check "a timeout: exit 124" "$rc" "124"
check "a timeout: outcome timeout" "$(jq -r .outcome <<< "$out")" "timeout"
check "a timeout: timedOut true" "$(jq -r .timedOut <<< "$out")" "true"
check "a timeout: it returns within 15 s" "$([ $((SECONDS - start)) -lt 15 ] && echo yes || echo no)" "yes"
gc="$(cat "$WORK/gc.pid" 2> /dev/null)"
check "a timeout: the grandchild is gone" "$(alive "$gc")" "gone"

# --- a group member that outlives the KILL: error, with its pid, and exit 124 kept ---
# The perl parent forks a sleep (it stays in the group), then leaves the group with setsid and never reaps it: after the
# KILL the sleep is a zombie the runner can't remove.
cat > "$WORK/escape.pl" <<'PL'
use POSIX ();
my $c = fork();
if ($c == 0) { exec "sleep", "60"; }
open(my $f, ">", $ARGV[0]) or die; print $f "$$ $c\n"; close $f;
POSIX::setsid();
sleep 60;
PL
out="$(bash "$RUN" --json --timeout 1 --grace 1 -- sh -c 'perl "$1" "$2" & wait' sh "$WORK/escape.pl" "$WORK/escape.pids")"; rc=$?
read -r esc_parent esc_child < "$WORK/escape.pids" 2> /dev/null; pids+=("${esc_parent:-0}" "${esc_child:-0}")
check "a member that outlives the KILL: exit 124 is kept" "$rc" "124"
check "a member that outlives the KILL: outcome error" "$(jq -r .outcome <<< "$out")" "error"
check "a member that outlives the KILL: its pid is named" "$(jq -r --arg p "${esc_child:-x}" '[.leftoverPids[] | tostring] | index($p) != null' <<< "$out")" "true"
kill -KILL "${esc_parent:-0}" 2> /dev/null

# --- an interrupt stops the group: exit 130 ---
# A background job of a non-interactive shell starts with SIGINT ignored, which bash can't trap; perl resets it first.
perl -e '$SIG{INT} = "DEFAULT"; exec @ARGV or exit 127' bash "$RUN" -- sh -c 'sleep 60 & echo $! > "$1"; wait' sh "$WORK/int.pid" > "$WORK/int.out" 2>&1 &
runner=$!
for _ in $(seq 1 50); do [ -s "$WORK/int.pid" ] && break; sleep 0.1; done
kill -INT "$runner" 2> /dev/null; wait "$runner"; rc=$?
ic="$(cat "$WORK/int.pid" 2> /dev/null)"
check "an interrupt: exit 130" "$rc" "130"
check "an interrupt: line 1 says interrupted" "$(head -1 "$WORK/int.out" | cut -d' ' -f1-2)" "aw-run: interrupted"
check "an interrupt: the grandchild is gone" "$(alive "$ic")" "gone"

# --- what a finished command left in its group is stopped and reported ---
text="$(bash "$RUN" -- sh -c 'sleep 60 & echo $! > "$1"' sh "$WORK/left.pid")"; rc=$?
lp="$(cat "$WORK/left.pid" 2> /dev/null)"
check "a finished command's leftover: exit 0" "$rc" "0"
has "a finished command's leftover: the summary says it stopped it" "$text" "stopped 1 process"
check "a finished command's leftover: it is gone" "$(alive "$lp")" "gone"

# --- run folders: private, run-shaped, never shared ---
d="$fdir"
check "a run folder is 0700" "$(perm "$d")" "700"
check "stdout.log is 0600" "$(perm "$d/stdout.log")" "600"
check "stderr.log is 0600" "$(perm "$d/stderr.log")" "600"
check "meta.json is 0600" "$(perm "$d/meta.json")" "600"
check "a run folder is runs/<YYYYMMDD>/<HHMMSS>-<pid>-<n>" "$(sed -E "s#^$AW_STATE_DIR/runs/[0-9]{8}/[0-9]{6}-[0-9]+-[0-9]+\$#shape#" <<< "$d")" "shape"
for i in 1 2 3 4; do bash "$RUN" -- echo "$i" > "$WORK/par.$i" & done; wait
check "four runs at once get four folders" "$(for i in 1 2 3 4; do artifacts "$(cat "$WORK/par.$i")"; done | sort -u | wc -l | tr -d ' ')" "4"

# --- the prune: only run-shaped folders older than 7 days, never through a symlink ---
R="$AW_STATE_DIR/runs"
mkdir -p "$R/20200101/000000-1-0" "$R/20200101/notarun" "$WORK/outside/000000-2-0" "$WORK/outside2"
echo old > "$R/20200101/000000-1-0/stdout.log"; echo keep > "$WORK/outside/000000-2-0/stdout.log"
ln -s "$WORK/outside" "$R/20200102"
ln -s "$WORK/outside2" "$R/20200101/000001-1-0"
bash "$RUN" -- true > /dev/null
check "the prune removes an old run folder" "$([ -e "$R/20200101/000000-1-0" ] && echo kept || echo removed)" "removed"
check "the prune keeps a folder that is not run-shaped" "$([ -d "$R/20200101/notarun" ] && echo kept || echo removed)" "kept"
check "the prune keeps a symlinked day folder" "$([ -L "$R/20200102" ] && echo kept || echo removed)" "kept"
check "the prune never deletes through a symlinked day" "$(cat "$WORK/outside/000000-2-0/stdout.log" 2> /dev/null)" "keep"
check "the prune keeps a symlinked run folder" "$([ -L "$R/20200101/000001-1-0" ] && echo kept || echo removed)" "kept"
check "the prune leaves the symlink's target" "$([ -d "$WORK/outside2" ] && echo kept || echo removed)" "kept"

# --- show: only the slice ---
check "show --stream stderr --lines 1:1" "$(bash "$RUN" show "$fdir" --stream stderr --lines 1:1)" "error: boom"
check "show --lines 5:7 gives three lines" "$(bash "$RUN" show "$big" --lines 5:7 | grep -c abcdefghij)" "3"
check "show --grep finds the match" "$(bash "$RUN" show "$fdir" --stream stderr --grep boom | grep -c boom)" "1"
check "show with no range is capped at 200 lines" "$(bash "$RUN" show "$big" | grep -c abcdefghij)" "200"
check "show --max 10 caps at 10" "$(bash "$RUN" show "$big" --max 10 | grep -c abcdefghij)" "10"
bash "$RUN" show "$WORK/nope" > /dev/null 2>&1; check "show of a folder that is not a run is exit 125" "$?" "125"
check "last names the newest run folder" "$(bash "$RUN" last | sed -E "s#^$R/[0-9]{8}/[0-9]{6}-[0-9]+-[0-9]+\$#shape#")" "shape"

[ "$fail" = 0 ] && echo "all aw-run tests passed" || { echo "aw-run tests FAILED"; exit 1; }
