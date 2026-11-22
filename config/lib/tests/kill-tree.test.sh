#!/usr/bin/env bash
# Tests for config/lib/kill-tree.sh (_aw_kill_tree): the one place the watchdog hooks (done-gate, prompt-sort,
# sindri-tasks) kill a timed-out script and everything under it, by pid only.
# Run: bash config/lib/tests/kill-tree.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIB="$DIR/../kill-tree.sh"
HOOKS="$DIR/../../hooks"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
PIDS=""
# Kill only the pids this suite recorded, then remove the temp dir.
cleanup() {
  local p
  for p in $PIDS; do kill -9 "$p" 2> /dev/null; done
  rm -rf "$WORK"
}
trap cleanup EXIT

# A pid counts as gone when it no longer exists or is only a zombie waiting to be reaped.
gone() {
  local st
  st="$(ps -o stat= -p "$1" 2> /dev/null | tr -d ' ')"
  case "$st" in '' | Z*) return 0 ;; *) return 1 ;; esac
}
# Wait up to 3 s for a pid to be gone; prints "gone" or "alive".
wait_gone() {
  local i
  for i in $(seq 1 30); do gone "$1" && { echo gone; return; }; sleep 0.1; done
  echo alive
}
# Wait up to 3 s for a file to hold a pid.
wait_file() {
  local i
  for i in $(seq 1 30); do [ -s "$1" ] && return 0; sleep 0.1; done
  return 1
}

check "the lib file exists" "$([ -f "$LIB" ] && echo y)" "y"

# A parent with a child and a grandchild, each a bash that waits on the one below it, like a hook script
# whose $(judge …) runs a judge.
bash -c '
  bash -c "sleep 60 & echo \$! > \"$1/grandchild\"; wait" _ "$1" &
  echo $! > "$1/child"
  wait
' _ "$WORK" &
parent=$!
disown "$parent" 2> /dev/null  # no "Killed" job line; a zombie counts as gone
PIDS="$parent"
wait_file "$WORK/child" && wait_file "$WORK/grandchild"
child="$(cat "$WORK/child" 2> /dev/null)"; grandchild="$(cat "$WORK/grandchild" 2> /dev/null)"
PIDS="$PIDS $child $grandchild"
check "the tree is up: parent, child and grandchild" \
  "$([ -n "$child" ] && [ -n "$grandchild" ] && ! gone "$parent" && ! gone "$child" && ! gone "$grandchild" && echo y)" "y"

( . "$LIB" 2> /dev/null && _aw_kill_tree "$parent" )
check "the parent is gone" "$(wait_gone "$parent")" "gone"
check "the child is gone" "$(wait_gone "$child")" "gone"
check "the grandchild is gone" "$(wait_gone "$grandchild")" "gone"

# A pid that is not running is a no-op: no output, exit 0.
out="$( ( . "$LIB" 2> /dev/null && _aw_kill_tree "$parent" ) 2>&1)"; rc=$?
check "a pid that is gone already: no output" "$out" ""
check "a pid that is gone already: exit 0" "$rc" "0"

# Sourcing the lib prints nothing and defines only the helper.
check "sourcing prints nothing" "$( (. "$LIB") 2>&1)" ""

# Each watchdog hook keeps a fallback for a missing lib, and it is the lib's own definition word for word, so a fix
# to the lib that misses a hook's copy fails here.
def="$(grep -E '^_aw_kill_tree\(\)' "$LIB" 2> /dev/null)"
check "the lib holds a one-line definition" "$([ -n "$def" ] && echo y)" "y"
for h in done-gate prompt-sort sindri-tasks; do
  check "$h: sources lib/kill-tree.sh" "$(grep -c 'lib/kill-tree\.sh' "$HOOKS/$h.sh" | awk '$1 > 0 { print "y" }')" "y"
  check "$h: its fallback is the lib's definition" \
    "$([ -n "$def" ] && grep -F -- "$def" "$HOOKS/$h.sh" > /dev/null && echo y)" "y"
done

# A hook installed without the lib (an older installer) still kills its tree: run the fallback alone.
mkdir -p "$WORK/bare"
cp "$HOOKS/done-gate.sh" "$WORK/bare/done-gate.sh"
bash -c 'bash -c "sleep 60 & echo \$! > \"$1/gc2\"; wait" _ "$1" & echo $! > "$1/c2"; wait' _ "$WORK" &
parent2=$!
disown "$parent2" 2> /dev/null
PIDS="$PIDS $parent2"
wait_file "$WORK/c2" && wait_file "$WORK/gc2"
c2="$(cat "$WORK/c2" 2> /dev/null)"; gc2="$(cat "$WORK/gc2" 2> /dev/null)"
PIDS="$PIDS $c2 $gc2"
fallback="$(grep -E '_aw_kill_tree\(\) \{' "$WORK/bare/done-gate.sh" | grep -v '^\s*#' | tail -1 | sed -E 's/^.*(_aw_kill_tree\(\) \{.*\}).*$/\1/')"
( eval "$fallback" 2> /dev/null && _aw_kill_tree "$parent2" )
check "fallback: parent, child and grandchild are gone" \
  "$(wait_gone "$parent2") $(wait_gone "$c2") $(wait_gone "$gc2")" "gone gone gone"

exit "$fail"
