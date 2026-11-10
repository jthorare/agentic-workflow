#!/usr/bin/env bash
# Tests for run_model_lane in meta-launch.test.sh (#2643): when the lane script hangs on its own and never runs the stub,
# nothing ends the lane script, so the helper's final `wait` blocked the whole suite forever. The real function is extracted
# from the suite and run against a lane script that only sleeps.
# Run: bash scripts/tests/meta-launch-run-model-lane.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
WORK="$(mktemp -d)"
hp=""
stop_pids() {
  local lp=""
  [ -s "$WORK/lane.pid" ] && lp="$(cat "$WORK/lane.pid")"
  [ -n "$lp" ] && kill -0 "$lp" 2> /dev/null && kill "$lp" 2> /dev/null
  [ -n "$hp" ] && kill -0 "$hp" 2> /dev/null && kill "$hp" 2> /dev/null
  [ -n "$hp" ] && wait "$hp" 2> /dev/null
  return 0
}
cleanup() { stop_pids; rm -r "$WORK"; }
trap cleanup EXIT

sed -n '/^run_model_lane() {/,/^}/p' "$DIR/tests/meta-launch.test.sh" > "$WORK/fn.sh"
check "run_model_lane is found in meta-launch.test.sh" "$([ -s "$WORK/fn.sh" ] && echo found || echo empty)" "found"

# A lane script that hangs by itself: it records its pid and never runs the stub.
mkdir "$WORK/scripts"
cat > "$WORK/scripts/meta-lane.sh" <<'LANE'
#!/usr/bin/env bash
echo $$ > "$(dirname "$0")/../lane.pid"
exec sleep 60
LANE

(
  DIR="$WORK/scripts"
  CALLS="$WORK/calls"
  waitfor() { for _ in $(seq 100); do eval "$1" && return 0; sleep 0.1; done; return 1; }
  check() { echo "$1: $2" >> "$WORK/inner.log"; }
  # shellcheck disable=SC1091
  . "$WORK/fn.sh"
  run_model_lane api
  : > "$WORK/returned"
) > /dev/null 2>&1 &
hp=$!

for _ in $(seq 300); do [ -e "$WORK/returned" ] && break; sleep 0.1; done
check "run_model_lane returns when the lane script hangs on its own (#2643)" "$([ -e "$WORK/returned" ] && echo returned || echo hung)" "returned"
lp=""
[ -s "$WORK/lane.pid" ] && lp="$(cat "$WORK/lane.pid")"
check "run_model_lane leaves no lane script running" "$([ -n "$lp" ] && kill -0 "$lp" 2> /dev/null && echo running || echo gone)" "gone"
stop_pids
exit "$fail"
