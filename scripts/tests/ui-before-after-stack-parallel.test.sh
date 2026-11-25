#!/usr/bin/env bash
# Two copies of skills/ui-before-after/scripts/stack.test.sh at once must both pass (#2596): the suite pins PORT=39917
# and opens real listeners, so two runs on one box collide today.
# Run: bash scripts/tests/ui-before-after-stack-parallel.test.sh
set -u
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SUITE="$DIR/../../skills/ui-before-after/scripts/stack.test.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

run_copy() {
  env -u AW_META_LANE -u AW_META_LANE_ID -u AW_SINDRI_CHILD -u TMUX bash "$SUITE" >"$WORK/$1.log" 2>&1
  echo $? >"$WORK/$1.rc"
}

run_copy a &
run_copy b &
wait

status=0
for c in a b; do
  rc=$(cat "$WORK/$c.rc")
  echo "copy $c exit: $rc"
  if [ "$rc" != 0 ]; then
    status=1
    grep '^FAIL' "$WORK/$c.log" | sed "s/^/  [$c] /"
  fi
done
exit "$status"
