#!/usr/bin/env bash
# Tests for main-worktree.sh against a throwaway repo with its own "origin".
set -u

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null; rm -rf "$p"; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; }
trap __aw_tmp_clean EXIT

SCRIPT="$(cd "$(dirname "$0")" && pwd)/main-worktree.sh"
LEASE="$(dirname "$SCRIPT")/lease.sh"
FAILURES=0

fail() {
  echo "FAIL: $1"
  FAILURES=$((FAILURES + 1))
}

run_script() {
  STATE="$(dirname "$1")/state"
  AW_WORKTREE_ROOT="$(dirname "$1")/wt" UI_BEFORE_AFTER_STATE_DIR="$STATE" bash "$SCRIPT" "$1" "${2:-a}"
}

take_lease() {
  UI_BEFORE_AFTER_STATE_DIR="$STATE" bash "$LEASE" take "$1"
}

make_repo() {
  local root origin repo
  root=$(mktemp -d)
  origin="$root/origin.git"
  repo="$root/repo"
  git init -q --bare -b main "$origin"
  git init -q -b main "$repo"
  git -C "$repo" commit -q --allow-empty -m first
  git -C "$repo" remote add origin "$origin"
  git -C "$repo" push -q origin main
  echo "$repo"
}

push_commit() {
  local upstream
  upstream=$(mktemp -d)/clone
  git clone -q "$(git -C "$1" remote get-url origin)" "$upstream"
  git -C "$upstream" commit -q --allow-empty -m second
  git -C "$upstream" push -q origin main
}

test_creates_a_locked_worktree_at_origin_main() {
  local repo path out
  repo=$(make_repo)
  path="$(dirname "$repo")/wt/proof-main"
  out=$(run_script "$repo" 2>/dev/null) || fail "create: exited non-zero: $out"
  [ "$(git -C "$path" rev-parse HEAD)" = "$(git -C "$repo" rev-parse origin/main)" ] || fail "create: not at origin/main"
  git -C "$repo" worktree list --porcelain | grep -A4 "worktree $(cd "$path" && pwd -P)" | grep -q "^locked" || fail "create: worktree is not locked"
  [ "$out" = "$path" ] || fail "create: should print the worktree path, printed: $out"
}

test_refreshes_an_existing_worktree_to_the_new_origin_main() {
  local repo path
  repo=$(make_repo)
  path="$(dirname "$repo")/wt/proof-main"
  run_script "$repo" >/dev/null 2>&1
  push_commit "$repo"
  run_script "$repo" >/dev/null 2>&1 || fail "refresh: exited non-zero"
  [ "$(git -C "$path" log -1 --format=%s)" = "second" ] || fail "refresh: still at the old main"
}

test_recreates_a_worktree_whose_folder_was_deleted() {
  local repo path
  repo=$(make_repo)
  path="$(dirname "$repo")/wt/proof-main"
  run_script "$repo" >/dev/null 2>&1
  git -C "$repo" worktree unlock "$path"
  rm -rf "$path"
  run_script "$repo" >/dev/null 2>&1 || fail "recreate: exited non-zero"
  [ -d "$path" ] || fail "recreate: folder still missing"
}

test_keeps_the_build_while_another_run_holds_a_lease() {
  local repo path out
  repo=$(make_repo)
  path="$(dirname "$repo")/wt/proof-main"
  run_script "$repo" a >/dev/null 2>&1
  take_lease b
  push_commit "$repo"
  out=$(run_script "$repo" a 2>&1 >/dev/null) || fail "shared: exited non-zero: $out"
  [ "$(git -C "$path" log -1 --format=%s)" = "first" ] || fail "shared: moved under another run"
  [[ "$out" == *"kept at"*"1 other"* ]] || fail "shared: no kept line: $out"
}

test_refreshes_when_the_only_other_lease_expired() {
  local repo path out
  repo=$(make_repo)
  path="$(dirname "$repo")/wt/proof-main"
  run_script "$repo" a >/dev/null 2>&1
  take_lease b
  echo 1 >"$STATE/proof-main.leases/b"
  push_commit "$repo"
  out=$(run_script "$repo" a 2>&1 >/dev/null) || fail "expired: exited non-zero: $out"
  [ "$(git -C "$path" log -1 --format=%s)" = "second" ] || fail "expired: not refreshed"
  [[ "$out" == *"refreshed"* ]] || fail "expired: no refreshed line: $out"
}

test_takes_the_lease_for_the_run() {
  local repo
  repo=$(make_repo)
  run_script "$repo" a >/dev/null 2>&1
  [ -f "$STATE/proof-main.leases/a" ] || fail "lease: not taken"
}

test_creates_a_locked_worktree_at_origin_main
test_refreshes_an_existing_worktree_to_the_new_origin_main
test_recreates_a_worktree_whose_folder_was_deleted
test_keeps_the_build_while_another_run_holds_a_lease
test_refreshes_when_the_only_other_lease_expired
test_takes_the_lease_for_the_run

if [ "$FAILURES" -eq 0 ]; then
  echo "6 passed"
else
  echo "$FAILURES failed"
  exit 1
fi
