#!/usr/bin/env bash
# Tests for scripts/aw-pr-watch.sh (the read-only PR watcher, issue 4221) with a stub `gh` first on PATH. The stub serves
# fixture JSON from a scenario folder: for the k-th call of a read (pull, checks, reviews) it serves <read>.<k>.json, else
# the highest <read>.<n>.json below k. <read>.<k>.fail prints its text on stderr and exits 1; <read>.<k>.partial prints
# <read>.<k>.json and then fails like .fail (a page of a paginated read failed). Every call is logged to calls.log.
# Covers: pending checks turning green then a merge (changes only, exit 0), a closed PR, no checks, two pages, a failed
# page labelled incomplete, the API failing 3 times (exit 5), the deadline (exit 4), a second watcher (exit 3), a dead
# holder's lock taken over, INT and TERM releasing the lock (130, 143), silence while nothing changes, a restart on the
# same snapshot, file modes, usage (exit 2) and that every gh call is a GET.
# Runs against a temp AW_STATE_DIR; HOME is never changed. Run: bash scripts/tests/aw-pr-watch.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="$DIR/aw-pr-watch.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() {
  case "$2" in *"$3"*) echo "ok - $1" ;; *)
    echo "not ok - $1"; echo "  expected to contain: $3"; echo "  actual: $2"; fail=1 ;;
  esac
}
hasnt() {
  case "$2" in *"$3"*) echo "not ok - $1"; echo "  expected not to contain: $3"; echo "  actual: $2"; fail=1 ;;
    *) echo "ok - $1" ;;
  esac
}
WORK="$(mktemp -d)"
cleanup() {
  local rc=$?
  [ -n "${bgpid:-}" ] && kill "$bgpid" 2> /dev/null
  rm -rf "$WORK"
  exit "$rc"
}
trap cleanup EXIT
mkdir -p "$WORK/bin"
export AW_STATE_DIR="$WORK/state"
export AW_PR_WATCH_BACKOFF=0
# The poll floor (#4293) is 10 s; the suite polls back to back. Section 17 checks the floor with this unset.
export AW_PR_WATCH_MIN_INTERVAL=0
unset GH_TOKEN GITHUB_TOKEN GH_HOST

cat > "$WORK/bin/gh" <<'STUB'
#!/usr/bin/env bash
s="${AW_TEST_SCEN:?}"
printf '%s\n' "$*" >> "$s/calls.log"
[ "${1:-}" = api ] || { echo "stub gh: only api" >&2; exit 9; }
shift
path=""
for a in "$@"; do case "$a" in -*) ;; *) [ -z "$path" ] && path="$a" ;; esac; done
case "$path" in
  */check-runs*) key=checks ;;
  */reviews*) key=reviews ;;
  */pulls/[0-9]*) key=pull ;;
  *) echo "stub gh: no fixture for $path" >&2; exit 9 ;;
esac
n=$(( $(cat "$s/.count.$key" 2> /dev/null || echo 0) + 1 ))
echo "$n" > "$s/.count.$key"
if [ -f "$s/$key.$n.fail" ]; then cat "$s/$key.$n.fail" >&2; exit 1; fi
k=$n
while [ "$k" -gt 0 ] && [ ! -f "$s/$key.$k.json" ]; do k=$((k - 1)); done
[ "$k" -gt 0 ] || { echo "stub gh: no $key fixture" >&2; exit 9; }
cat "$s/$key.$k.json"
if [ -f "$s/$key.$n.partial" ]; then cat "$s/$key.$n.partial" >&2; exit 1; fi
exit 0
STUB
chmod 755 "$WORK/bin/gh"
export PATH="$WORK/bin:$PATH"

# --- fixture helpers -------------------------------------------------------------------------------------------------
# scen <name>: a fresh scenario folder, exported as AW_TEST_SCEN
scen() { S="$WORK/scen/$1"; rm -rf "$S"; mkdir -p "$S"; : > "$S/calls.log"; export AW_TEST_SCEN="$S"; }
# pull <k> <state open|closed> <merged true|false> [sha] [mergeable_state]
pull() {
  printf '{"number":7,"state":"%s","merged":%s,"draft":false,"head":{"sha":"%s"},"mergeable_state":"%s","comments":1,"review_comments":2}\n' \
    "$2" "$3" "${4:-aaaaaaaaaaaa1111}" "${5:-clean}" > "$S/pull.$1.json"
}
run_json() { printf '{"name":"%s","status":"%s","conclusion":%s}' "$1" "$2" "$3"; }
# checks <k> <total> <run json>...: one page
checks() {
  local k="$1" total="$2"; shift 2
  local IFS=,
  printf '{"total_count":%s,"check_runs":[%s]}\n' "$total" "$*" > "$S/checks.$k.json"
}
reviews() { printf '%s\n' "${2:-[]}" > "$S/reviews.$1.json"; }
watch() { bash "$SCRIPT" --repo acme/app --pr 7 "$@"; }
LOCK="$AW_STATE_DIR/pr-watch/acme/app/7/lock"
mode() { stat -c %a "$1" 2> /dev/null || stat -f %Lp "$1"; }

# --- 1. pending -> success -> merged: the snapshot once, then only the changes ---------------------------------------
scen green
pull 1 open false; pull 4 closed true
checks 1 2 "$(run_json build in_progress null)" "$(run_json lint completed '"success"')"
checks 3 2 "$(run_json build completed '"success"')" "$(run_json lint completed '"success"')"
reviews 1 '[{"user":{"login":"r1"},"state":"COMMENTED"},{"user":{"login":"r1"},"state":"APPROVED"}]'
out="$(watch --interval 0 --deadline 1 2>&1)"; rc=$?
check "green: exit 0 on merge" "$rc" "0"
has "green: the snapshot names the PR" "$out" "acme/app#7: open"
has "green: the snapshot shows the pending check" "$out" "check build: in_progress"
has "green: the snapshot counts reviews" "$out" "reviews: 1 approved, 0 changes requested"
has "green: the build check's change is one line" "$out" "check build: in_progress -> success"
has "green: the merge is a change line" "$out" "state: open -> merged"
check "green: the last line says merged" "$(printf '%s\n' "$out" | tail -1)" "merged: acme/app#7"
check "green: poll 2 (nothing changed) printed nothing: one build line in the whole output" \
  "$(printf '%s\n' "$out" | grep -c 'check build')" "2"
check "green: 4 polls read the PR 4 times" "$(cat "$S/.count.pull")" "4"
hasnt "green: never claims ready to merge" "$out" "ready to merge"
check "green: the snapshot is under 30 lines" "$([ "$(printf '%s\n' "$out" | wc -l)" -lt 30 ] && echo yes || echo no)" "yes"
check "green: the lock is released" "$([ -e "$LOCK" ] && echo held || echo gone)" "gone"
check "green: every gh call is api, read-only" "$(grep -cvE '^api ' "$S/calls.log")" "0"
check "green: no write flags (-X, --method, -f, -F, --field, --input)" \
  "$(grep -cE '(^| )(-X|--method|-f|-F|--field|--raw-field|--input)( |=|$)' "$S/calls.log")" "0"
has "green: the check-runs read paginates" "$(grep check-runs "$S/calls.log")" "--paginate"
has "green: the reviews read paginates" "$(grep reviews "$S/calls.log")" "--paginate"

# --- 2. a closed PR -------------------------------------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen closed
pull 1 closed false; checks 1 0; reviews 1
out="$(watch --interval 0 2>&1)"; rc=$?
check "closed: exit 0" "$rc" "0"
check "closed: the last line says closed" "$(printf '%s\n' "$out" | tail -1)" "closed: acme/app#7"
check "closed: one poll" "$(cat "$S/.count.pull")" "1"

# --- 3. no checks: reported as none, never green -----------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen nochecks
pull 1 open false; checks 1 0; reviews 1
out="$(watch --once 2>&1)"; rc=$?
check "no checks: --once exits 0" "$rc" "0"
has "no checks: says no checks reported" "$out" "checks: no checks reported"
hasnt "no checks: never says success" "$out" "success"

# --- 4. two pages are merged ------------------------------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen pages
pull 1 open false; reviews 1
{ printf '{"total_count":2,"check_runs":[%s]}\n' "$(run_json build completed '"success"')"
  printf '{"total_count":2,"check_runs":[%s]}\n' "$(run_json test completed '"failure"')"; } > "$S/checks.1.json"
out="$(watch --once 2>&1)"; rc=$?
check "pages: exit 0" "$rc" "0"
has "pages: page 1's check" "$out" "check build: success"
has "pages: page 2's check" "$out" "check test: failure"
has "pages: the summary counts both" "$out" "checks: 2 (1 failure, 1 success)"
hasnt "pages: not incomplete" "$out" "incomplete"

# --- 5. a page fails on every try: incomplete, never "no checks" ------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen partial
pull 1 open false; reviews 1
printf '{"total_count":2,"check_runs":[%s]}\n' "$(run_json build completed '"success"')" > "$S/checks.1.json"
for k in 1 2 3; do echo "HTTP 502: Bad Gateway (page 2)" > "$S/checks.$k.partial"; done
out="$(watch --once 2>&1)"; rc=$?
check "partial: exit 0 (the PR read worked)" "$rc" "0"
has "partial: the checks line says incomplete" "$out" "checks: incomplete"
hasnt "partial: never no checks" "$out" "no checks reported"
check "partial: retried 3 times" "$(cat "$S/.count.checks")" "3"

# --- 5b. fewer runs than total_count is incomplete too ----------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen short
pull 1 open false; reviews 1
checks 1 3 "$(run_json build completed '"success"')"
out="$(watch --once 2>&1)"
has "short read: 1 of 3 runs is incomplete" "$out" "checks: incomplete"

# --- 6. the API fails 3 times: exit 5 with gh's text --------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen apifail
for k in 1 2 3; do echo "HTTP 503: Service Unavailable (https://api.github.com/repos/acme/app/pulls/7)" > "$S/pull.$k.fail"; done
pull 4 open false
out="$(watch --interval 0 2>&1)"; rc=$?
check "api error: exit 5" "$rc" "5"
has "api error: gh's message is shown" "$out" "HTTP 503: Service Unavailable"
check "api error: exactly 3 tries" "$(cat "$S/.count.pull")" "3"
check "api error: the lock is released" "$([ -e "$LOCK" ] && echo held || echo gone)" "gone"
# gh's text is bounded
rm -rf "$AW_STATE_DIR"
scen apifail-long
for k in 1 2 3; do for i in $(seq 1 50); do echo "line $i of a very long error from gh"; done > "$S/pull.$k.fail"; done
out="$(watch --interval 0 2>&1)"; rc=$?
check "api error, long text: exit 5" "$rc" "5"
check "api error, long text: bounded under 15 lines" "$([ "$(printf '%s\n' "$out" | wc -l)" -lt 15 ] && echo yes || echo no)" "yes"
# one failure then success is not an error
rm -rf "$AW_STATE_DIR"
scen apiflaky
echo "HTTP 502: Bad Gateway" > "$S/pull.1.fail"; pull 2 open false; checks 1 0; reviews 1
out="$(watch --once 2>&1)"; rc=$?
check "api flaky: one failure then success exits 0" "$rc" "0"
has "api flaky: the snapshot is printed" "$out" "acme/app#7: open"

# --- 7. the deadline: exit 4 ------------------------------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen deadline
pull 1 open false; checks 1 1 "$(run_json build in_progress null)"; reviews 1
start=$(date +%s)
out="$(watch --interval 1 --deadline 2s 2>&1)"; rc=$?
took=$(( $(date +%s) - start ))
check "deadline: exit 4" "$rc" "4"
has "deadline: the last line names it" "$(printf '%s\n' "$out" | tail -1)" "deadline"
check "deadline: stops within 6 s" "$([ "$took" -le 6 ] && echo yes || echo no)" "yes"
check "deadline: nothing printed between polls (snapshot only + final line)" \
  "$(printf '%s\n' "$out" | grep -c 'check build')" "1"
check "deadline: the lock is released" "$([ -e "$LOCK" ] && echo held || echo gone)" "gone"

# --- 8. state files: 0600, folder 0700; a restart on the same snapshot -------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen restart
pull 1 open false; checks 1 1 "$(run_json build completed '"success"')"; reviews 1
watch --once > /dev/null 2>&1
SD="$AW_STATE_DIR/pr-watch/acme/app/7"
check "state: the folder is 0700" "$(mode "$SD")" "700"
check "state: every level is 0700 (pr-watch, owner, name, N)" \
  "$(mode "$AW_STATE_DIR/pr-watch") $(mode "$AW_STATE_DIR/pr-watch/acme") $(mode "$AW_STATE_DIR/pr-watch/acme/app") $(mode "$SD")" "700 700 700 700"
bad=""; for f in "$SD"/*; do [ -f "$f" ] && [ "$(mode "$f")" != 600 ] && bad="$bad $(basename "$f"):$(mode "$f")"; done
check "state: every file is 0600" "${bad:-none}" "none"
check "state: the snapshot file exists" "$([ -s "$SD/snapshot" ] && echo yes || echo no)" "yes"
out="$(watch --once 2>&1)"; rc=$?
check "restart: exit 0" "$rc" "0"
has "restart: says unchanged since" "$out" "unchanged since"
check "restart: one line only" "$(printf '%s\n' "$out" | wc -l | tr -d ' ')" "1"
pull 3 open false bbbbbbbbbbbb2222
out="$(watch --once 2>&1)"
has "restart, new head: the snapshot is printed again" "$out" "head: bbbbbbbbbbbb"
hasnt "restart, new head: not unchanged" "$out" "unchanged since"

# --- 9. a second watcher on the same PR: exit 3 naming the holder -------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen second
pull 1 open false; checks 1 0; reviews 1
perl -e '$SIG{INT}="DEFAULT"; exec @ARGV' bash "$SCRIPT" --repo acme/app --pr 7 --interval 5 --deadline 20s > "$WORK/bg.out" 2>&1 &
bgpid=$!
for _ in $(seq 1 50); do [ -s "$LOCK/pid" ] && break; sleep 0.1; done
check "second: the first watcher holds the lock with its pid" "$(cat "$LOCK/pid" 2> /dev/null)" "$bgpid"
out="$(watch --once 2>&1)"; rc=$?
check "second: exit 3" "$rc" "3"
has "second: names the holder's pid" "$out" "pid $bgpid"
check "second: the holder's lock is untouched" "$(cat "$LOCK/pid" 2> /dev/null)" "$bgpid"

# --- 10. INT releases the lock, exit 130 ------------------------------------------------------------------------------
# in_sleep <pid>: wait (5 s at most) until the watcher sits in its sleep between polls. bash 5.2 can drop a trapped INT or
# TERM that lands while it waits on a foreground child or a $(...) (about 1 in 25 kills mid-poll, #4498), so the signal
# goes in while only the sleep runs: the `wait` builtin then returns at once and the trap fires.
in_sleep() {
  local _
  for _ in $(seq 1 100); do ps -A -o ppid= -o comm= | awk -v p="$1" '$1 == p && $2 == "sleep" { f = 1 } END { exit !f }' && return 0; sleep 0.05; done
  return 1
}
in_sleep "$bgpid"
kill -INT "$bgpid"
rc=0; wait "$bgpid" || rc=$?
bgpid=""
check "INT: exit 130" "$rc" "130"
check "INT: the lock is released" "$([ -e "$LOCK" ] && echo held || echo gone)" "gone"

# --- 11. TERM releases the lock, exit 143 -----------------------------------------------------------------------------
bash "$SCRIPT" --repo acme/app --pr 7 --interval 5 --deadline 20s > "$WORK/bg.out" 2>&1 &
bgpid=$!
for _ in $(seq 1 50); do [ -s "$LOCK/pid" ] && break; sleep 0.1; done
in_sleep "$bgpid"
kill -TERM "$bgpid"
rc=0; wait "$bgpid" || rc=$?
bgpid=""
check "TERM: exit 143" "$rc" "143"
check "TERM: the lock is released" "$([ -e "$LOCK" ] && echo held || echo gone)" "gone"

# --- 12. a dead holder's lock is taken over -----------------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen dead
pull 1 open false; checks 1 0; reviews 1
sh -c 'exit 0' & dead=$!; wait "$dead"
mkdir -p "$LOCK"; echo "$dead" > "$LOCK/pid"
out="$(watch --once 2>&1)"; rc=$?
check "dead holder: exit 0" "$rc" "0"
has "dead holder: the snapshot is printed" "$out" "acme/app#7: open"
check "dead holder: the lock is gone after" "$([ -e "$LOCK" ] && echo held || echo gone)" "gone"

# --- 13. usage and --help -----------------------------------------------------------------------------------------------
for args in "" "--repo acme/app" "--pr 7" "--repo acme --pr 7" "--repo acme/app --pr x" "--repo acme/app --pr 7 --interval -1" \
  "--repo acme/app --pr 7 --deadline soon" "--repo acme/app --pr 7 --bogus" \
  "--repo ../app --pr 7" "--repo acme/.. --pr 7" "--repo ./app --pr 7" "--repo acme/. --pr 7" "--repo /app --pr 7" \
  "--repo acme/ --pr 7"; do
  # shellcheck disable=SC2086
  rc=0; bash "$SCRIPT" $args > /dev/null 2>&1 || rc=$?
  check "usage: '$args' exits 2" "$rc" "2"
done
out="$(bash "$SCRIPT" --help 2>&1)"; rc=$?
check "--help exits 0" "$rc" "0"
for code in "0 " "2 " "3 " "4 " "5 " "130" "143"; do has "--help documents exit $code" "$out" "  $code"; done

rm -rf "$AW_STATE_DIR"
out="$(bash "$SCRIPT" --repo acme/.. --pr 7 2>&1)"
has "usage: a .. part is named in the refusal" "$out" "must not be empty, . or .."
check "usage: a .. part writes no state" "$([ -e "$AW_STATE_DIR" ] && echo yes || echo no)" "no"

# --- 13b. repos whose old flat folder names collided get their own locks -----------------------------------------------
# acme-x/app#7 and acme/x-app#7 were both pr-watch/acme-x-app-7 before the folder was nested.
rm -rf "$AW_STATE_DIR"
scen collide
pull 1 open false; checks 1 0; reviews 1
bash "$SCRIPT" --repo acme-x/app --pr 7 --interval 1 --deadline 20s > "$WORK/bg.out" 2>&1 &
bgpid=$!
L1="$AW_STATE_DIR/pr-watch/acme-x/app/7/lock"
for _ in $(seq 1 50); do [ -s "$L1/pid" ] && break; sleep 0.1; done
check "collide: acme-x/app#7 holds its own lock" "$(cat "$L1/pid" 2> /dev/null)" "$bgpid"
out="$(bash "$SCRIPT" --repo acme/x-app --pr 7 --once 2>&1)"; rc=$?
check "collide: acme/x-app#7 runs beside it (exit 0, not 3)" "$rc" "0"
has "collide: acme/x-app#7 prints its own snapshot" "$out" "acme/x-app#7: open"
check "collide: acme/x-app#7 has its own folder" "$([ -s "$AW_STATE_DIR/pr-watch/acme/x-app/7/snapshot" ] && echo yes || echo no)" "yes"
check "collide: acme-x/app#7's lock is untouched" "$(cat "$L1/pid" 2> /dev/null)" "$bgpid"
kill -TERM "$bgpid"; wait "$bgpid" 2> /dev/null; bgpid=""

# --- 14. --json -------------------------------------------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen json
pull 1 open false; pull 2 closed true; checks 1 0; reviews 1
out="$(watch --json --interval 0 2> /dev/null)"; rc=$?
check "json: exit 0" "$rc" "0"
check "json: every line is a JSON object" "$(printf '%s\n' "$out" | jq -c 'type' 2> /dev/null | sort -u)" '"object"'
check "json: the first line is the snapshot" "$(printf '%s\n' "$out" | head -1 | jq -r .type)" "snapshot"
check "json: a change object" "$(printf '%s\n' "$out" | jq -r 'select(.type=="change") | "\(.key) \(.from) \(.to)"')" "state open merged"
check "json: the end object" "$(printf '%s\n' "$out" | tail -1 | jq -r '"\(.type) \(.reason)"')" "end merged"

# --- 15. a read that 403s on every try is exit 5, not "incomplete" (#4293) ---------------------------------------------
# gh prints the HTTP error body on stdout, so the failed read leaves JSON that parses; it must not pass as a partial page.
rm -rf "$AW_STATE_DIR"
scen forbidden
pull 1 open false; reviews 1
printf '{"message":"Resource not accessible by integration","documentation_url":"https://docs.github.com/rest","status":"403"}\n' > "$S/checks.1.json"
for k in 1 2 3; do echo "HTTP 403: Resource not accessible by integration (https://api.github.com/repos/acme/app/commits/x/check-runs)" > "$S/checks.$k.partial"; done
out="$(watch --once 2>&1)"; rc=$?
check "403: exit 5" "$rc" "5"
has "403: gh's text is named" "$out" "HTTP 403"
hasnt "403: never reported as incomplete" "$out" "incomplete"

# --- 16. two watchers reclaiming one dead lock leave one holder (#4293) ------------------------------------------------
# A stub cat slows the lock pid reads of the watcher started with AW_TEST_CAT_PAUSE, so it reads the dead pid, the other
# watcher takes the lock over meanwhile, and the slow one must not then remove the new holder's lock.
rm -rf "$AW_STATE_DIR"
scen race
pull 1 open false; checks 1 0; reviews 1
real_cat="$(command -v cat)"
cat > "$WORK/bin/cat" << STUB
#!/usr/bin/env bash
# Reads first, then pauses: the caller acts on what the file held before the pause.
if [ -n "\${AW_TEST_CAT_PAUSE:-}" ]; then
  for a in "\$@"; do case "\$a" in */lock/pid | */lock/pid.*)
    out="\$("$real_cat" "\$@")" || exit 1; sleep "\$AW_TEST_CAT_PAUSE"; printf '%s\\n' "\$out"; exit 0 ;; esac; done
fi
exec "$real_cat" "\$@"
STUB
chmod 755 "$WORK/bin/cat"
sh -c 'exit 0' & dead=$!; wait "$dead"
mkdir -p "$LOCK"; echo "$dead" > "$LOCK/pid"
AW_TEST_CAT_PAUSE=2 bash "$SCRIPT" --repo acme/app --pr 7 --interval 1 --deadline 8s > "$WORK/slow.out" 2>&1 &
slowpid=$!
sleep 0.5
bash "$SCRIPT" --repo acme/app --pr 7 --interval 1 --deadline 30s > "$WORK/bg.out" 2>&1 &
bgpid=$!
for _ in $(seq 1 50); do [ "$(cat "$LOCK/pid" 2> /dev/null)" = "$bgpid" ] && break; sleep 0.1; done
check "race: the fast watcher took the dead lock over" "$(cat "$LOCK/pid" 2> /dev/null)" "$bgpid"
rc=0; wait "$slowpid" || rc=$?
check "race: the slow watcher finds a live holder (exit 3)" "$rc" "3"
check "race: the fast watcher still holds the lock" "$(cat "$LOCK/pid" 2> /dev/null)" "$bgpid"
check "race: the fast watcher still runs" "$(kill -0 "$bgpid" 2> /dev/null && echo yes || echo no)" "yes"
kill -TERM "$bgpid"; wait "$bgpid" 2> /dev/null; bgpid=""
rm -f "$WORK/bin/cat"

# --- 17. --interval has a 10 s floor (#4293) ---------------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen floor
pull 1 open false; checks 1 0; reviews 1
for v in 0 9; do
  rc=0; out="$(env -u AW_PR_WATCH_MIN_INTERVAL bash "$SCRIPT" --repo acme/app --pr 7 --interval "$v" --once 2>&1)" || rc=$?
  check "floor: --interval $v exits 2" "$rc" "2"
  has "floor: --interval $v names the 10 s floor" "$out" "at least 10"
done
rc=0; env -u AW_PR_WATCH_MIN_INTERVAL bash "$SCRIPT" --repo acme/app --pr 7 --interval 10 --once > /dev/null 2>&1 || rc=$?
check "floor: --interval 10 is accepted" "$rc" "0"

# --- 18. a leading zero is base 10 (#4293) ------------------------------------------------------------------------------
rm -rf "$AW_STATE_DIR"
scen octal
pull 1 open false; checks 1 0; reviews 1
for v in 08 09s; do
  rc=0; out="$(watch --deadline "$v" --once 2>&1)" || rc=$?
  check "octal: --deadline $v exits 0" "$rc" "0"
  hasnt "octal: --deadline $v is not an arithmetic error" "$out" "value too great"
done

exit "$fail"
