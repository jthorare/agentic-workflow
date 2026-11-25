#!/usr/bin/env bash
# aw-pr-watch: watch one pull request and print what changes (issue 4221). Read-only: every call is a `gh api` GET.
# It never comments, merges or edits, and it never says a PR is ready to merge. Bash 3.2, jq and gh.
#
# Each poll reads the PR (state, head sha, mergeable state, comment counts), the check runs of its head sha and its
# reviews, and turns them into a snapshot of "key: value" rows. The first snapshot is printed whole (under 30 lines:
# at most 20 check rows); after that only a changed row prints, one line each. The last snapshot is kept under
# $AW_STATE_DIR/pr-watch/<owner>/<name>/<N>/ (every folder 0700, files 0600), so a restart that sees the same snapshot prints
# "unchanged since <time>" once.
#
# One watcher per PR: the lock folder <state>/lock is made in one rename(2) with the pid inside (the make_lock pattern
# of scripts/install-cs.sh). A live holder makes a second watcher exit 3; a dead holder's lock is taken over.
#
# Run `aw-pr-watch --help` for the options and the exit codes.
set -uo pipefail

usage() {
  cat << 'EOF'
Usage: aw-pr-watch --repo OWNER/NAME --pr N [--interval S] [--deadline D] [--once] [--json]

Watch one pull request and print what changes. Read-only: it only runs `gh api` GETs, and never comments, merges,
edits or says the PR is ready to merge.

  --repo OWNER/NAME  the repository
  --pr N             the pull request number
  --interval S       seconds between polls (default 60; at least AW_PR_WATCH_MIN_INTERVAL, default 10)
  --deadline D       stop after D minutes (default 120), or D seconds when written Ns (90s)
  --once             poll once, print, exit 0
  --json             one JSON object per line: snapshot, change, unchanged, end

Output: the first snapshot whole, then one line per changed row. Zero check runs prints "no checks reported", never a
pass. A read that lost a page prints "incomplete". A restart on the same snapshot prints "unchanged since <time>".

State: $AW_STATE_DIR/pr-watch/<owner>/<name>/<N>/ (default AW_STATE_DIR is ~/.agentic-workflow).
AW_PR_WATCH_BACKOFF: base seconds between the 3 tries of a failed read (default 2: waits 2 s, then 4 s).
AW_PR_WATCH_MIN_INTERVAL: the lowest --interval accepted (default 10; a smaller one is usage, exit 2).

Exit codes:
  0    the PR merged or closed (the last line says which), or --once finished
  1    a local error: jq or gh missing, the state folder or the lock could not be made
  2    usage
  3    another watcher holds this PR's lock (its pid is named)
  4    the deadline passed while the PR was still open
  5    a GitHub read failed 3 times (gh's error text follows, cut to 5 lines)
  130  interrupted (INT); the lock is released
  143  terminated (TERM); the lock is released
EOF
}

bad_usage() { echo "aw-pr-watch: $1" >&2; echo "run aw-pr-watch --help for usage" >&2; exit 2; }

repo="" pr="" interval=60 deadline=120 once=0 json=0
while [ $# -gt 0 ]; do
  case "$1" in
    -h | --help) usage; exit 0 ;;
    --repo | --pr | --interval | --deadline)
      [ $# -ge 2 ] || bad_usage "$1 needs a value"
      case "$1" in
        --repo) repo="$2" ;; --pr) pr="$2" ;; --interval) interval="$2" ;; --deadline) deadline="$2" ;;
      esac
      shift 2 ;;
    --once) once=1; shift ;;
    --json) json=1; shift ;;
    *) bad_usage "unknown argument: $1" ;;
  esac
done
[ -n "$repo" ] || bad_usage "--repo is required"
[ -n "$pr" ] || bad_usage "--pr is required"
case "$repo" in
  */*/* | /* | */) bad_usage "--repo must be OWNER/NAME, got: $repo" ;;
  */*) ;;
  *) bad_usage "--repo must be OWNER/NAME, got: $repo" ;;
esac
case "$repo" in *[!A-Za-z0-9._/-]*) bad_usage "--repo must be OWNER/NAME, got: $repo" ;; esac
owner="${repo%%/*}" name="${repo#*/}"
# Each part is a folder under pr-watch/, so "", "." and ".." are refused: they would name another folder.
for part in "$owner" "$name"; do
  case "$part" in "" | . | ..) bad_usage "--repo parts must not be empty, . or .., got: $repo" ;; esac
done
case "$pr" in "" | *[!0-9]* | 0) bad_usage "--pr must be a positive number, got: $pr" ;; esac
case "$interval" in "" | *[!0-9]*) bad_usage "--interval must be whole seconds, got: $interval" ;; esac
min_interval="${AW_PR_WATCH_MIN_INTERVAL:-10}"
case "$min_interval" in "" | *[!0-9]*) min_interval=10 ;; esac
interval=$((10#$interval)); min_interval=$((10#$min_interval))
[ "$interval" -ge "$min_interval" ] || bad_usage "--interval must be at least $min_interval s, got: $interval"
case "$deadline" in
  *s) deadline_s="${deadline%s}"; deadline_label="$deadline" ;;
  *) deadline_s="$deadline"; deadline_label="${deadline}m" ;;
esac
case "$deadline_s" in "" | *[!0-9]*) bad_usage "--deadline must be minutes or Ns, got: $deadline" ;; esac
deadline_s=$((10#$deadline_s))
case "$deadline" in *s) ;; *) deadline_s=$((deadline_s * 60)) ;; esac
backoff="${AW_PR_WATCH_BACKOFF:-2}"
case "$backoff" in "" | *[!0-9]*) backoff=2 ;; esac

command -v jq > /dev/null 2>&1 || { echo "aw-pr-watch: jq is not installed" >&2; exit 1; }
command -v gh > /dev/null 2>&1 || { echo "aw-pr-watch: gh is not installed" >&2; exit 1; }

label="$repo#$pr"
umask 077
root="${AW_STATE_DIR:-$HOME/.agentic-workflow}/pr-watch"
dir="$root/$owner/$name/$pr"
mkdir -p "$dir" 2> /dev/null && chmod 700 "$root" "$root/$owner" "$root/$owner/$name" "$dir" 2> /dev/null \
  || { echo "aw-pr-watch: could not make the state folder $dir" >&2; exit 1; }

# --- the lock ------------------------------------------------------------------------------------------------------------
lock="" lock_new="" work="" sleeper=""
release() {
  if [ -n "$sleeper" ]; then kill "$sleeper" 2> /dev/null; sleeper=""; fi
  if [ -n "$work" ]; then rm -rf "$work"; work=""; fi
  if [ -n "$lock_new" ]; then rm -rf "$lock_new"; lock_new=""; fi
  if [ -n "$lock" ]; then rm -rf "$lock"; lock=""; fi
}
trap release EXIT
trap 'release; exit 130' INT
trap 'release; exit 143' TERM

# Renames folder $1 to $2 in one rename(2), which fails when $2 is a folder with anything in it (GNU mv -T; BSD mv has
# no -T and would move $1 into $2, so perl's rename does it there).
rename_dir() {
  if mv --version > /dev/null 2>&1; then
    mv -T "$1" "$2" 2> /dev/null
  else
    perl -e 'rename($ARGV[0], $ARGV[1]) or exit 1' "$1" "$2" 2> /dev/null
  fi
}
# make_lock <dir>: a temp folder holding this pid is renamed to <dir>, so no lock is ever seen without its pid.
# Exit 1 when <dir> exists, 2 when the lock could not be made at all.
make_lock() {
  local lk="$1"
  [ ! -e "$lk" ] || return 1
  lock_new="$(mktemp -d "$dir/.lock-new.XXXXXX")" || return 2
  echo $$ > "$lock_new/pid" || { rm -rf "$lock_new"; lock_new=""; return 2; }
  if rename_dir "$lock_new" "$lk"; then lock_new=""; return 0; fi
  rm -rf "$lock_new"; lock_new=""
  if [ -e "$lk" ]; then return 1; fi
  return 2
}
# lock_stale <dir>: its pid is dead, or it has no pid and is older than 60 s.
lock_stale() {
  local lk="$1" owner
  owner="$(cat "$lk/pid" 2> /dev/null || true)"
  if [ -n "$owner" ]; then
    ! kill -0 "$owner" 2> /dev/null
    return
  fi
  [ -d "$lk" ] && [ -n "$(find "$lk" -maxdepth 0 -mmin +1 2> /dev/null)" ]
}
end_json() { [ "$json" = 1 ] && jq -cn --arg r "$1" --arg m "${2:-}" --arg at "$(date '+%Y-%m-%dT%H:%M:%S%z')" \
  '{type:"end", reason:$r, at:$at} + (if $m == "" then {} else {message:$m} end)'; return 0; }

# reclaim_dead <dir>: free a stale lock. A pid-less one older than 60 s goes at once. For a dead pid, move the pid file
# aside first (one mover wins, as in config/lib/locks.sh _lock_reclaim_dead) and remove the folder only when the moved
# file still holds the dead pid; otherwise the lock changed hands, so the file goes back.
reclaim_dead() {
  local lk="$1" owner moved
  owner="$(cat "$lk/pid" 2> /dev/null || true)"
  if [ -z "$owner" ]; then
    lock_stale "$lk" && { rm -rf "$lk"; return 0; }
    return 1
  fi
  kill -0 "$owner" 2> /dev/null && return 1
  moved="$lk/pid.reclaim.$$"
  mv "$lk/pid" "$moved" 2> /dev/null || return 1
  if [ "$(cat "$moved" 2> /dev/null || true)" != "$owner" ]; then
    mv "$moved" "$lk/pid" 2> /dev/null || true
    return 1
  fi
  rm -rf "$lk"
}

take_lock() {
  local lk="$dir/lock" rc holder tries=0
  while :; do
    rc=0; make_lock "$lk" || rc=$?
    [ "$rc" = 0 ] && { lock="$lk"; return 0; }
    [ "$rc" = 2 ] && { echo "aw-pr-watch: could not make the lock $lk" >&2; exit 1; }
    tries=$((tries + 1))
    if [ "$tries" -le 3 ] && reclaim_dead "$lk"; then continue; fi
    holder="$(cat "$lk/pid" 2> /dev/null || true)"
    echo "aw-pr-watch: another watcher holds $label (pid ${holder:-unknown}, lock $lk)" >&2
    end_json lock_held "pid ${holder:-unknown}"
    exit 3
  done
}
take_lock
# Leftovers of a killed watcher: only the lock holder gets here, so nothing else is using them.
rm -rf "$dir"/.poll.* "$dir"/.snapshot.* 2> /dev/null
work="$(mktemp -d "$dir/.poll.XXXXXX")" || { echo "aw-pr-watch: could not make a temp folder in $dir" >&2; exit 1; }

# sleep_for <seconds>: a sleep the INT and TERM traps cut short.
sleep_for() {
  [ "$1" -gt 0 ] || return 0
  sleep "$1" & sleeper=$!
  wait "$sleeper" 2> /dev/null
  sleeper=""
}

# --- reads ---------------------------------------------------------------------------------------------------------------
# gh_read <out> <gh api args...>: 3 tries with backoff. 0 read, 1 partial (pages printed, then a failure: the output
# parses), 2 failed. gh's stderr of the last try is in $work/err.
gh_read() {
  local out="$1" try=1
  shift
  while :; do
    if gh api "$@" > "$out" 2> "$work/err" < /dev/null; then return 0; fi
    [ "$try" -ge 3 ] && break
    sleep_for $((backoff * try))
    try=$((try + 1))
  done
  # Partial only when a parsed document is not a GitHub error body (gh prints the HTTP error body on stdout).
  if [ -s "$out" ] && jq -e -s 'any(.[]; type != "object" or (has("message") | not) or has("check_runs"))' "$out" > /dev/null 2>&1; then return 1; fi
  return 2
}
api_fail() {
  local msg
  msg="$(head -5 "$work/err" | cut -c1-300)"
  echo "aw-pr-watch: reading $1 of $label failed 3 times: ${msg:-gh printed no error}" >&2
  end_json api_error "$msg"
  exit 5
}

# poll: reads the PR, its checks and reviews into $work/snap (key<TAB>value rows); sets $state.
poll() {
  local rc sha checks_partial=false reviews_partial=false
  rc=0; gh_read "$work/pull" "repos/$repo/pulls/$pr" || rc=$?
  [ "$rc" = 0 ] || api_fail "the PR"
  jq -e 'type == "object" and has("state")' "$work/pull" > /dev/null 2>&1 || { echo "not a PR object" > "$work/err"; api_fail "the PR"; }
  state="$(jq -r 'if .merged then "merged" elif .state == "closed" then "closed" else "open" end' "$work/pull")"
  sha="$(jq -r '.head.sha // ""' "$work/pull")"
  case "$sha" in "" | *[!0-9a-fA-F]*) echo "the PR has no head sha" > "$work/err"; api_fail "the PR" ;; esac

  rc=0; gh_read "$work/checks" --paginate "repos/$repo/commits/$sha/check-runs?per_page=100" || rc=$?
  case "$rc" in 1) checks_partial=true ;; 2) api_fail "the check runs" ;; esac
  rc=0; gh_read "$work/reviews" --paginate "repos/$repo/pulls/$pr/reviews?per_page=100" || rc=$?
  case "$rc" in 1) reviews_partial=true ;; 2) api_fail "the reviews" ;; esac

  {
    jq -r --arg state "$state" '
      ["state", $state],
      (if .draft then ["draft", "yes"] else empty end),
      ["head", (.head.sha[0:12])],
      ["mergeable", (.mergeable_state // "unknown")],
      ["comments", "\(.comments // 0) on the PR, \(.review_comments // 0) on the code"]
      | @tsv' "$work/pull"
    jq -r -s --argjson partial "$reviews_partial" '
      (map(if type == "array" then . else [] end) | add // []) as $all
      | ($all | map(select(.state == "APPROVED" or .state == "CHANGES_REQUESTED" or .state == "DISMISSED"))
          | group_by(.user.login // "") | map(last.state)) as $latest
      | ["reviews",
          ((if ($all | length) == 0 then "none"
            else "\($latest | map(select(. == "APPROVED")) | length) approved, \($latest | map(select(. == "CHANGES_REQUESTED")) | length) changes requested"
            end) + (if $partial then " (incomplete: a page failed)" else "" end))]
      | @tsv' "$work/reviews"
    jq -r -s --argjson partial "$checks_partial" '
      (map(.check_runs // []) | add // []) as $runs
      | (map(.total_count // 0) | max // 0) as $total
      | ($runs
          | map({n: ((.name // "?") | gsub("[\t\r\n]"; " ")),
                 s: (if .status == "completed" then (.conclusion // "completed") else (.status // "unknown") end)})
          | group_by(.n)
          | map(if length == 1 then . else to_entries | map(.value + {n: "\(.value.n) (\(.key + 1))"}) end)
          | flatten) as $rows
      | ($rows | length) as $n
      | ["checks",
          (if $partial then "incomplete (a page failed, \($n) read)"
           elif $n < $total then "incomplete (\($n) of \($total) read)"
           elif $n == 0 then "no checks reported"
           else "\($n) (\($rows | group_by(.s) | map("\(length) \(.[0].s)") | join(", ")))"
           end)],
        ($rows[0:20][] | ["check \(.n)", .s]),
        (if $n > 20 then ["checks not shown", "\($n - 20)"] else empty end)
      | @tsv' "$work/checks"
  } > "$work/snap.new" || { echo "a read returned JSON of the wrong shape" > "$work/err"; api_fail "the PR data"; }
  mv -f "$work/snap.new" "$work/snap"
}

# --- output --------------------------------------------------------------------------------------------------------------
now_hms() { date '+%H:%M:%S'; }
print_snapshot() {
  if [ "$json" = 1 ]; then
    jq -c -R -s --arg pr "$label" --arg at "$(date '+%Y-%m-%dT%H:%M:%S%z')" '
      split("\n") | map(select(length > 0) | split("\t") | {(.[0]): (.[1:] | join("\t"))}) | add // {}
      | {type: "snapshot", at: $at, pr: $pr, items: .}' "$work/snap"
  else
    echo "$label: $state (read at $(now_hms))"
    awk -F'\t' '$1 != "state" { print $1 ": " $2 }' "$work/snap"
  fi
}
# print_changes <old> <new>: one line per row that changed, appeared or went away.
print_changes() {
  awk -F'\t' -v t="$(now_hms)" -v json="$json" '
    function q(s) { gsub(/\\/, "\\\\", s); gsub(/"/, "\\\"", s); return "\"" s "\"" }
    function out(k, a, b) {
      if (json == 1) print "{\"type\":\"change\",\"at\":" q(t) ",\"key\":" q(k) ",\"from\":" q(a) ",\"to\":" q(b) "}"
      else print t " " k ": " a " -> " b
    }
    NR == FNR { old[$1] = $2; seen[$1] = 0; order[++n] = $1; next }
    { if (!($1 in old)) out($1, "none", $2); else { seen[$1] = 1; if (old[$1] != $2) out($1, old[$1], $2) } }
    END { for (i = 1; i <= n; i++) if (!seen[order[i]]) out(order[i], old[order[i]], "gone") }
  ' "$1" "$2"
}
store() { # store <seen-at text>: the snapshot and when it was first seen, each written whole then renamed
  cp "$work/snap" "$dir/.snapshot.new" && mv -f "$dir/.snapshot.new" "$dir/snapshot"
  printf '%s\n' "$1" > "$dir/.snapshot.at.new" && mv -f "$dir/.snapshot.at.new" "$dir/snapshot.at"
}
finish_if_ended() {
  case "$state" in
    merged | closed)
      if [ "$json" = 1 ]; then end_json "$state"; else echo "$state: $label"; fi
      exit 0 ;;
  esac
}

# --- the loop ------------------------------------------------------------------------------------------------------------
start=$(date +%s)
deadline_at=$((start + deadline_s))
poll
if [ -f "$dir/snapshot" ] && cmp -s "$dir/snapshot" "$work/snap"; then
  since="$(cat "$dir/snapshot.at" 2> /dev/null || echo "an earlier run")"
  if [ "$json" = 1 ]; then jq -cn --arg s "$since" '{type:"unchanged", since:$s}'; else echo "unchanged since $since"; fi
else
  print_snapshot
  store "$(date '+%Y-%m-%d %H:%M:%S')"
fi
finish_if_ended
if [ "$once" = 1 ]; then end_json once; exit 0; fi
while :; do
  now=$(date +%s)
  if [ "$now" -ge "$deadline_at" ]; then
    if [ "$json" = 1 ]; then end_json deadline "still open after $deadline_label"; else echo "deadline: $label still open after $deadline_label"; fi
    exit 4
  fi
  wait_s=$interval
  [ $((deadline_at - now)) -lt "$wait_s" ] && wait_s=$((deadline_at - now))
  sleep_for "$wait_s"
  poll
  if ! cmp -s "$dir/snapshot" "$work/snap"; then
    print_changes "$dir/snapshot" "$work/snap"
    store "$(date '+%Y-%m-%d %H:%M:%S')"
  fi
  finish_if_ended
done
