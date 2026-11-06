#!/usr/bin/env bash
# Tests for release.sh against a fake stack script.
set -u

DIR="$(cd "$(dirname "$0")" && pwd)"
FAILURES=0

fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

make_fixture() {
  local dir
  dir=$(mktemp -d)
  mkdir -p "$dir/state" "$dir/main"
  cat >"$dir/stack" <<'EOF'
#!/usr/bin/env bash
echo "stack $*" >>"$FAKE_DIR/calls.log"
EOF
  chmod +x "$dir/stack"
  echo "$dir"
}

release() {
  local dir="$1"
  shift
  FAKE_DIR="$dir" UI_BEFORE_AFTER_STATE_DIR="$dir/state" UI_BEFORE_AFTER_STACK="$dir/stack" \
    bash "$DIR/release.sh" "$@" 2>&1
}

lease() {
  UI_BEFORE_AFTER_STATE_DIR="$1/state" bash "$DIR/lease.sh" take "$2"
}

test_the_last_release_stops_main() {
  local dir out
  dir=$(make_fixture)
  lease "$dir" a
  out=$(release "$dir" a "$dir/main")
  grep -q "^stack down $dir/main\$" "$dir/calls.log" || fail "last: main not stopped"
  [[ "$out" == *"no other run holds proof-main; stopping it"* ]] || fail "last: message: $out"
  [ -e "$dir/state/proof-main.leases/a" ] && fail "last: lease remains"
}

test_a_release_with_another_live_lease_leaves_main_running() {
  local dir out
  dir=$(make_fixture)
  lease "$dir" a
  lease "$dir" b
  out=$(release "$dir" a "$dir/main")
  [ -e "$dir/calls.log" ] && fail "shared: main was stopped"
  [[ "$out" == *"still in use by 1 other"* ]] || fail "shared: message: $out"
  [ -e "$dir/state/proof-main.leases/b" ] || fail "shared: b's lease was removed"
}

test_a_missing_main_worktree_is_not_stopped() {
  local dir
  dir=$(make_fixture)
  lease "$dir" a
  release "$dir" a "$dir/missing" >/dev/null
  [ -e "$dir/calls.log" ] && fail "missing: stack called"
}

test_the_last_release_stops_main
test_a_release_with_another_live_lease_leaves_main_running
test_a_missing_main_worktree_is_not_stopped

if [ "$FAILURES" -eq 0 ]; then
  echo "3 passed"
else
  echo "$FAILURES failed"
  exit 1
fi
