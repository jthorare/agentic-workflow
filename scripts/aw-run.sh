#!/usr/bin/env bash
# scripts/aw-run.sh: run a command, keep its full stdout and stderr in private files, print a bounded summary (#4237).
#
#   aw-run [--timeout S] [--grace S] [--tail N] [--fail-lines N] [--json] -- <command> [args...]
#   aw-run show <run folder> [--stream stdout|stderr] [--lines A:B] [--grep RE] [--max N]
#   aw-run last
#
# The command runs as given (argv, never eval), with stdin from /dev/null, in its own process group. Its stdout and stderr go
# straight to files, so nothing is held in memory. Each run gets a folder $AW_STATE_DIR/runs/<YYYYMMDD>/<HHMMSS>-<pid>-<n>
# (UTC; n counts up until mkdir makes a new one) holding stdout.log, stderr.log and meta.json: folders 0700, files 0600.
# The command itself runs under the caller's umask.
# Run folders of days more than 7 days old are removed at start; only run-shaped real folders under runs/, never a symlink.
#
# The summary (text, or one JSON object with --json) names the outcome, the exit code, the bytes and lines of each stream,
# the first failure lines, the last lines of each stream, what was truncated and the run folder. It is at most 8 KB.
# The outcome comes from what the runner saw, never from the exit number alone:
#   ok            the command exited 0          failed       it exited non-zero (a child's own 124 is failed too)
#   timeout       --timeout passed (exit 124)   interrupted  aw-run got INT (exit 130), TERM or HUP (exit 143)
#   error         a group member was still there 5 s after the KILL (its pids are named; the exit stays as above),
#                 the command itself could not be reaped 5 s after that (its pid is named), or it could not be started (127)
# A command that printed nothing says "(no output)". Nothing here ever says a test passed.
# On a timeout or an interrupt the group gets TERM, then KILL after --grace seconds; what a finished command left in its
# group is stopped the same way and reported. The summary is printed only once the group is empty or the 5 s are up.
# Exit: the command's own code, 124 timeout, 130 or 143 interrupted, 125 a usage or runner error.
set -u
# The runner writes only under runs/ (day folders, run folders, their files), so it runs under 077. The command gets the
# caller's umask back (#4290): under 077 `aw-run -- dotnet build` wrote bin/ and obj/ as 0700/0600.
caller_umask="$(umask)"
umask 077

LINE_BYTES=300
CAP=8192
FAIL_RE='error|fail|fatal|panic|traceback|exception'
RUN_RE='^[0-9]{6}-[0-9]+-[0-9]+$'
DAY_RE='^[0-9]{8}$'
runs="${AW_STATE_DIR:-$HOME/.agentic-workflow}/runs"
usage="usage: aw-run [--timeout S] [--grace S] [--tail N] [--fail-lines N] [--json] -- <command...> | aw-run show <dir> [--stream stdout|stderr] [--lines A:B] [--grep RE] [--max N] | aw-run last"

die() { echo "aw-run: $*" >&2; exit 125; }
whole() { case "$1" in "" | *[!0-9]*) return 1 ;; esac; }
cut_lines() { LC_ALL=C cut -c "1-$LINE_BYTES"; }

# --- show and last ---------------------------------------------------------------------------------------------------
show() {
  local dir="${1:-}" stream=stdout range="" re="" max=200 f a b
  [ -n "$dir" ] || die "show needs a run folder ($usage)"
  shift
  while [ $# -gt 0 ]; do
    case "$1" in
      --stream) stream="${2:-}"; shift 2 || die "--stream needs a value" ;;
      --lines) range="${2:-}"; shift 2 || die "--lines needs A:B" ;;
      --grep) re="${2:-}"; shift 2 || die "--grep needs a pattern" ;;
      --max) max="${2:-}"; shift 2 || die "--max needs a number" ;;
      *) die "unknown option $1 ($usage)" ;;
    esac
  done
  case "$stream" in stdout | stderr) ;; *) die "--stream is stdout or stderr" ;; esac
  whole "$max" || die "--max is a whole number"
  [ -f "$dir/meta.json" ] && [ -f "$dir/$stream.log" ] || die "not an aw-run folder: $dir"
  f="$dir/$stream.log"
  if [ -n "$re" ]; then
    LC_ALL=C grep -n -E -- "$re" "$f" | head -n "$max" | cut_lines
  elif [ -n "$range" ]; then
    a="${range%%:*}" b="${range#*:}"
    whole "$a" && whole "$b" && [ "$a" -ge 1 ] && [ "$b" -ge "$a" ] || die "--lines is A:B with 1 <= A <= B"
    sed -n "${a},${b}p;${b}q" "$f" | head -n "$max" | cut_lines
  else
    head -n "$max" "$f" | cut_lines
    [ "$(wc -l < "$f" | tr -d ' ')" -le "$max" ] || echo "(aw-run: the first $max lines; use --lines A:B, --grep RE or --max N for others)"
  fi
  return 0
}

last() {
  local d
  d="$(find "$runs" -mindepth 2 -maxdepth 2 -type d 2> /dev/null | LC_ALL=C grep -E '/[0-9]{8}/[0-9]{6}-[0-9]+-[0-9]+$' | LC_ALL=C sort | tail -1)"
  [ -n "$d" ] || { echo "aw-run: no run folder under $runs" >&2; return 1; }
  echo "$d"
}

case "${1:-}" in
  show) shift; show "$@"; exit $? ;;
  last) last; exit $? ;;
esac

# --- options ---------------------------------------------------------------------------------------------------------
timeout_s=1800 grace=10 tail_n=40 fail_n=20 json=0
while [ $# -gt 0 ]; do
  case "$1" in
    --timeout) timeout_s="${2:-}"; shift 2 || die "--timeout needs a value" ;;
    --grace) grace="${2:-}"; shift 2 || die "--grace needs a value" ;;
    --tail) tail_n="${2:-}"; shift 2 || die "--tail needs a value" ;;
    --fail-lines) fail_n="${2:-}"; shift 2 || die "--fail-lines needs a value" ;;
    --json) json=1; shift ;;
    --) shift; break ;;
    *) die "unknown option $1 ($usage)" ;;
  esac
done
[ $# -ge 1 ] || die "no command ($usage)"
for v in "$timeout_s" "$grace" "$tail_n" "$fail_n"; do whole "$v" || die "--timeout, --grace, --tail and --fail-lines are whole numbers"; done
[ "$timeout_s" -ge 1 ] || die "--timeout is at least 1 second"

# --- the run folder --------------------------------------------------------------------------------------------------
# Only run-shaped real folders of days before the cutoff go; a symlink (a day or a run) is never followed or removed.
prune() {
  local cutoff day d name
  [ -d "$runs" ] && [ ! -L "$runs" ] || return 0
  cutoff="$(date -u -d '7 days ago' +%Y%m%d 2> /dev/null || date -u -v-7d +%Y%m%d 2> /dev/null)" || return 0
  for day in "$runs"/*; do
    name="${day##*/}"
    [[ $name =~ $DAY_RE ]] && [ -d "$day" ] && [ ! -L "$day" ] && [[ $name < $cutoff ]] || continue
    for d in "$day"/*; do
      name="${d##*/}"
      [[ $name =~ $RUN_RE ]] && [ -d "$d" ] && [ ! -L "$d" ] || continue
      rm -rf -- "$d"
    done
    rmdir -- "$day" 2> /dev/null
  done
  return 0
}

make_dir() {
  local stamp day n=0
  stamp="$(date -u +%Y%m%d/%H%M%S)"; day="$runs/${stamp%/*}"
  [ ! -L "$runs" ] && [ ! -L "$day" ] || die "refusing to write through a symlink: $day"
  mkdir -p "$day" || die "could not make $day"
  until mkdir "$day/${stamp#*/}-$$-$n" 2> /dev/null; do
    n=$((n + 1)); [ "$n" -lt 1000 ] || die "could not make a run folder under $day"
  done
  dir="$day/${stamp#*/}-$$-$n"
}

prune
make_dir
out="$dir/stdout.log" err="$dir/stderr.log"
: > "$out"; : > "$err"
argv_text="$(printf '%q ' "$@")"; argv_text="${argv_text% }"
[ "${#argv_text}" -le 200 ] || argv_text="${argv_text:0:200}..."
start_s="$(date -u +%s)" start_iso="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# --- the group -------------------------------------------------------------------------------------------------------
child="" pg="" why="" stopped=0 leftover=""
# Every member of the command's group but the command itself once it is a zombie (this shell reaps it with wait).
group_pids() {
  [ -n "$pg" ] || return 0
  ps -A -o pid= -o pgid= -o stat= 2> /dev/null | awk -v g="$pg" -v c="$child" '$2 == g && !($1 == c && $3 ~ /^Z/) { print $1 }'
}
wait_empty() { # wait_empty <seconds>: 0 once the group is empty
  local n=$(($1 * 5))
  while [ -n "$(group_pids)" ]; do
    [ "$n" -gt 0 ] || return 1
    sleep 0.2; n=$((n - 1))
  done
  return 0
}
# TERM the group, KILL it after --grace, then wait 5 s more: a member still there (a zombie whose parent left the group, a
# process stuck in the kernel) is named in $leftover instead of hanging the runner.
stop_group() {
  local members
  members="$(group_pids)"
  [ -n "$members" ] || return 0
  stopped="$(printf '%s\n' "$members" | wc -l | tr -d ' ')"
  kill -TERM -- "-$pg" 2> /dev/null || kill -TERM "$pg" 2> /dev/null
  wait_empty "$grace" && return 0
  kill -KILL -- "-$pg" 2> /dev/null
  wait_empty 5 && return 0
  leftover="$(group_pids | tr '\n' ' ')"; leftover="${leftover% }"
}

# Reap the command with a deadline (#4290): wait blocks on a child the kernel holds (D state after the KILL), so the zombie
# is waited for only once ps shows it as one. A child still not reapable 5 s after the group was stopped is named in
# $unreaped instead of hanging the runner; one stop_group already named in $leftover is not waited for again.
unreaped=""
reap_child() {
  local n=25 st
  case " $leftover " in *" $child "*) kill -0 "$child" 2> /dev/null && { unreaped="$child"; return 0; } ;; esac
  while kill -0 "$child" 2> /dev/null; do
    st="$(ps -o stat= -p "$child" 2> /dev/null | tr -d ' ')"
    case "$st" in Z* | "") wait "$child"; rc=$?; continue ;; esac
    [ "$n" -gt 0 ] || { unreaped="$child"; return 0; }
    sleep 0.2; n=$((n - 1))
  done
  return 0
}

trap 'why=${why:-INT}' INT
trap 'why=${why:-TERM}' TERM
trap 'why=${why:-HUP}' HUP
trap 'why=${why:-timeout}' USR1

rc=127
if command -v "$1" > /dev/null 2>&1; then
  # Job control is off here, so the background subshell is not a group leader: it sets the caller's umask and execs setsid,
  # which execs in place, so $! is the command's pid and its group id. macOS has no setsid; perl does the same (as in heavy.sh).
  # The command starts with SIGINT (and SIGQUIT) ignored: non-interactive bash does that for every async command, and the
  # command inherits it. That is fine: aw-run traps INT itself and stops the group with TERM, then KILL (stop_group).
  if command -v setsid > /dev/null 2>&1; then
    ( umask "$caller_umask"; exec setsid "$@" ) > "$out" 2> "$err" < /dev/null &
  else
    ( umask "$caller_umask"; exec perl -e 'setpgrp(0, 0); exec @ARGV or exit 127' -- "$@" ) > "$out" 2> "$err" < /dev/null &
  fi
  child=$! pg=$!
  # The watcher wakes this shell with USR1 at the timeout; it kills its own sleep when it is stopped.
  # The trap is set before the sleep starts, so a TERM in between can't leave the sleep behind (#4290).
  ( sp=""; trap '[ -n "$sp" ] && kill "$sp" 2> /dev/null; exit 0' TERM; sleep "$timeout_s" & sp=$!; wait "$sp" && kill -USR1 $$ 2> /dev/null ) > /dev/null 2>&1 < /dev/null &
  watcher=$!
  # wait returns early when a trapped signal arrives; the command is reaped once kill -0 no longer finds it.
  while :; do
    wait "$child"; rc=$?
    kill -0 "$child" 2> /dev/null || break
    [ -z "$why" ] || break
  done
  # Not waited for: bash's wait on the watcher after its USR1 blocked until the command itself ended. Stopped, it stops its sleep.
  kill -TERM "$watcher" 2> /dev/null
  stop_group
  reap_child
else
  echo "aw-run: command not found: $1" > "$err"
fi
end_s="$(date -u +%s)"
secs=$((end_s - start_s))

timed=false interrupted=false signal=""
case "$why" in
  timeout) outcome=timeout code=124 timed=true ;;
  INT) outcome=interrupted code=130 interrupted=true signal=INT ;;
  TERM | HUP) outcome=interrupted code=143 interrupted=true signal="$why" ;;
  *) code=$rc; if [ "$rc" = 0 ]; then outcome=ok; else outcome=failed; fi ;;
esac
if [ -z "$child" ] || [ -n "$leftover" ] || [ -n "$unreaped" ]; then outcome=error; fi
trap - INT TERM HUP USR1

jq -n --arg cwd "$PWD" --arg start "$start_iso" --arg end "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --argjson exit "$code" \
  --arg outcome "$outcome" --argjson timedOut "$timed" --argjson interrupted "$interrupted" --arg signal "$signal" \
  '{argv: $ARGS.positional, cwd: $cwd, start: $start, end: $end, exit: $exit, outcome: $outcome, timedOut: $timedOut,
    interrupted: $interrupted, signal: (if $signal == "" then null else $signal end)}' --args "$@" > "$dir/meta.json"

# --- the summary -----------------------------------------------------------------------------------------------------
# Built from the files with wc, tail and grep -m, so a log of any size costs only the lines shown.
stats() { # stats <file>: "bytes lines"; a last line without a newline counts too
  local b l
  b="$(wc -c < "$1" | tr -d ' ')" l="$(wc -l < "$1" | tr -d ' ')"
  if [ "$b" -gt 0 ] && [ -n "$(tail -c 1 "$1")" ]; then l=$((l + 1)); fi
  echo "$b $l"
}
truncated() { # truncated <file> <lines>: whether the tail leaves lines out or cuts one
  [ "$2" -gt "$tail_n" ] && { echo true; return; }
  tail -n "$tail_n" "$1" | LC_ALL=C awk -v m="$LINE_BYTES" 'length($0) > m { x = 1 } END { exit !x }' && echo true || echo false
}
failures() { # failures <stream> <file> <count>
  [ "$3" -gt 0 ] || return 0
  LC_ALL=C grep -i -E -m "$3" -- "$FAIL_RE" "$2" | cut_lines | LC_ALL=C awk -v s="$1" '{ print s "\t" $0 }'
}
collect() { # collect <tail lines> <failure lines>: the shown parts, into dot files of the run folder
  local left
  tail -n "$1" "$out" | cut_lines > "$dir/.tail.stdout"
  tail -n "$1" "$err" | cut_lines > "$dir/.tail.stderr"
  failures stderr "$err" "$2" > "$dir/.failures"
  left=$(($2 - $(wc -l < "$dir/.failures")))
  failures stdout "$out" "$left" >> "$dir/.failures"
}
read -r ob ol <<< "$(stats "$out")"
read -r eb el <<< "$(stats "$err")"
ot="$(truncated "$out" "$ol")" et="$(truncated "$err" "$el")"

text_summary() {
  local body="$dir/.summary" s n
  collect "$tail_n" "$fail_n"
  {
    printf 'aw-run: %s exit=%s %ss: %s\n' "$outcome" "$code" "$secs" "$argv_text"
    case "$why" in
      timeout) echo "timed out after ${timeout_s}s; stopped the command's process group" ;;
      INT | TERM | HUP) echo "aw-run got SIG$why; stopped the command's process group" ;;
      *) if [ "$stopped" = 1 ]; then echo "stopped 1 process the command left in its group"; elif [ "$stopped" -gt 1 ]; then echo "stopped $stopped processes the command left in its group"; fi ;;
    esac
    [ -z "$leftover" ] || echo "still in the group 5 s after the KILL: pids $leftover"
    [ -z "$unreaped" ] || echo "the command (pid $unreaped) could not be reaped 5 s after the KILL; its exit code is unknown"
    if [ "$ob" = 0 ] && [ "$eb" = 0 ]; then
      echo "(no output)"
    else
      for s in stdout stderr; do
        if [ "$s" = stdout ]; then n="$ob bytes, $ol lines"; [ "$ot" = false ] || n="$n, truncated"; else n="$eb bytes, $el lines"; [ "$et" = false ] || n="$n, truncated"; fi
        echo "$s: $n"
      done
      if [ -s "$dir/.failures" ]; then
        echo "failure lines (first $fail_n):"
        LC_ALL=C awk -F '\t' '{ print "  [" $1 "] " substr($0, length($1) + 2) }' "$dir/.failures"
      fi
      for s in stdout stderr; do
        [ -s "$dir/.tail.$s" ] || continue
        echo "tail $s (last $tail_n lines):"
        sed 's/^/  /' "$dir/.tail.$s"
      done
    fi
  } > "$body"
  if [ "$(wc -c < "$body")" -gt $((CAP - 400)) ]; then
    head -c $((CAP - 500)) "$body"; echo
    echo "(summary cut at $CAP bytes; aw-run show $dir --lines A:B for more)"
  else
    cat "$body"
  fi
  echo "artifacts: $dir (stdout.log stderr.log meta.json)"
}

json_summary() { # json_summary <tail lines> <failure lines> <cut>
  collect "$1" "$2"
  jq -n --slurpfile meta "$dir/meta.json" --arg dir "$dir" --argjson secs "$secs" --arg why "$why" \
    --argjson ob "$ob" --argjson ol "$ol" --argjson ot "$ot" --argjson eb "$eb" --argjson el "$el" --argjson et "$et" \
    --rawfile fails "$dir/.failures" --rawfile tso "$dir/.tail.stdout" --rawfile tse "$dir/.tail.stderr" \
    --arg left "$leftover" --arg unreaped "$unreaped" --argjson stopped "$stopped" --argjson cut "$3" \
    'def tolines: if . == "" then [] else rtrimstr("\n") | split("\n") end;
     # argv cut as the text summary cuts it: 200 characters of the joined words, then "..." (meta.json keeps it whole).
     def cutargv: reduce .[] as $a ({out: [], n: 0, cut: false};
       if .cut then .
       elif .n + ($a | length) > 200 then .out += [($a[0:(200 - .n)] + "...")] | .cut = true
       else .out += [$a] | .n += ($a | length) + 1 end);
     ($meta[0].argv | cutargv) as $av
     | $meta[0] + {argv: $av.out, argvCut: $av.cut, seconds: $secs,
       stdout: {bytes: $ob, lines: $ol, truncated: $ot}, stderr: {bytes: $eb, lines: $el, truncated: $et},
       failureLines: ($fails | tolines | map(split("\t") | {stream: .[0], line: (.[1:] | join("\t"))})),
       tail: {stdout: ($tso | tolines), stderr: ($tse | tolines)},
       leftoverPids: ($left | split(" ") | map(select(length > 0) | tonumber)),
       unreapedPid: (if $unreaped == "" then null else ($unreaped | tonumber) end),
       stoppedLeftovers: (if $why == "" then $stopped else 0 end),
       artifactDir: $dir, summaryCut: $cut}'
}

if [ "$json" = 1 ]; then
  # Halve the tails, then the failure lines, until the object fits in CAP bytes.
  t="$tail_n" f="$fail_n"
  s="$(json_summary "$t" "$f" false)"
  while [ "$(printf '%s' "$s" | wc -c)" -gt "$CAP" ]; do
    if [ "$t" -gt 1 ]; then t=$((t / 2)); elif [ "$f" -gt 0 ]; then f=$((f / 2)); else break; fi
    s="$(json_summary "$t" "$f" true)"
  done
  printf '%s\n' "$s"
else
  text_summary
fi
rm -f "$dir/.tail.stdout" "$dir/.tail.stderr" "$dir/.failures" "$dir/.summary"
exit "$code"
