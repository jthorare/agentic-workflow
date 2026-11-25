#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../locks.sh"

# Hermetic: the cases below assume the default two heavy-job slots (the "every slot is held"
# case holds slots 1 and 2), whatever the caller's shell exports (a box may set
# AW_HEAVY_SLOTS=3). Each case passes its own lock paths; the rest of the lock env is cleared
# and the state dir is a temp dir, so nothing falls back to the real ~/.agentic-workflow.
export AW_HEAVY_SLOTS=2
unset AW_HEAVY_JOB_LOCK AW_HEAVY_LOCK_HELD AW_QA_STACK_LOCK
AW_STATE_DIR="$(mktemp -d)"; export AW_STATE_DIR

test_with_stack_lock_releases_both_locks_even_on_failure() {
  local stack_lock heavy_lock
  stack_lock="$(mktemp -d)/qa-stack.lock"
  heavy_lock="$(mktemp -d)/heavy-job.lock"
  AW_QA_STACK_LOCK="$stack_lock" AW_HEAVY_JOB_LOCK="$heavy_lock" \
    with_stack_lock_and_heavy_job_lock 2 bash -c 'exit 1' || true
  [ -d "$stack_lock" ] && { echo "FAIL: qa-stack.lock not released after a failing command"; exit 1; }
  [ -d "$heavy_lock" ] && { echo "FAIL: heavy-job.lock not released after a failing command"; exit 1; }
  echo "PASS: test_with_stack_lock_releases_both_locks_even_on_failure"
}

test_with_stack_lock_runs_the_command_and_returns_its_exit_code() {
  local stack_lock heavy_lock
  stack_lock="$(mktemp -d)/qa-stack.lock"
  heavy_lock="$(mktemp -d)/heavy-job.lock"
  local rc=0
  AW_QA_STACK_LOCK="$stack_lock" AW_HEAVY_JOB_LOCK="$heavy_lock" \
    with_stack_lock_and_heavy_job_lock 2 bash -c 'exit 0' || rc=$?
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0, got $rc"; exit 1; }
  echo "PASS: test_with_stack_lock_runs_the_command_and_returns_its_exit_code"
}

test_with_stack_lock_fails_when_the_stack_lock_is_already_held() {
  local stack_lock heavy_lock
  stack_lock="$(mktemp -d)/qa-stack.lock"
  heavy_lock="$(mktemp -d)/heavy-job.lock"
  mkdir "$stack_lock"
  local rc=0
  AW_QA_STACK_LOCK="$stack_lock" AW_HEAVY_JOB_LOCK="$heavy_lock" \
    with_stack_lock_and_heavy_job_lock 1 bash -c 'exit 0' || rc=$?
  [ "$rc" -eq 0 ] && { echo "FAIL: expected non-zero when the stack lock can't be acquired"; exit 1; }
  rmdir "$stack_lock"
  echo "PASS: test_with_stack_lock_fails_when_the_stack_lock_is_already_held"
}

test_with_stack_lock_takes_slot_2_when_slot_1_is_held() {
  local stack_lock heavy_lock
  stack_lock="$(mktemp -d)/qa-stack.lock"
  heavy_lock="$(mktemp -d)/heavy-job.lock"
  mkdir "$heavy_lock"   # another heavy job (a plain locks.sh holder) has slot 1
  local seen
  seen="$(AW_QA_STACK_LOCK="$stack_lock" AW_HEAVY_JOB_LOCK="$heavy_lock" \
    with_stack_lock_and_heavy_job_lock 1 bash -c "[ -d '$heavy_lock.2' ] && echo slot2")"
  [ "$seen" = "slot2" ] || { echo "FAIL: expected the command to run holding slot 2, got '$seen'"; exit 1; }
  [ -d "$heavy_lock.2" ] && { echo "FAIL: slot 2 not released"; exit 1; }
  [ -d "$heavy_lock" ] || { echo "FAIL: the other job's slot 1 was released"; exit 1; }
  [ -d "$stack_lock" ] && { echo "FAIL: qa-stack.lock not released"; exit 1; }
  rmdir "$heavy_lock"
  echo "PASS: test_with_stack_lock_takes_slot_2_when_slot_1_is_held"
}

test_with_stack_lock_fails_and_frees_the_stack_lock_when_every_heavy_slot_is_held() {
  local stack_lock heavy_lock
  stack_lock="$(mktemp -d)/qa-stack.lock"
  heavy_lock="$(mktemp -d)/heavy-job.lock"
  mkdir "$heavy_lock" "$heavy_lock.2"
  local rc=0
  AW_QA_STACK_LOCK="$stack_lock" AW_HEAVY_JOB_LOCK="$heavy_lock" \
    with_stack_lock_and_heavy_job_lock 1 bash -c 'echo ran' 2>/dev/null || rc=$?
  [ "$rc" -eq 0 ] && { echo "FAIL: expected non-zero with both heavy slots held"; exit 1; }
  [ -d "$stack_lock" ] && { echo "FAIL: qa-stack.lock not released after the heavy-slot timeout"; exit 1; }
  rmdir "$heavy_lock" "$heavy_lock.2"
  echo "PASS: test_with_stack_lock_fails_and_frees_the_stack_lock_when_every_heavy_slot_is_held"
}

test_with_stack_lock_releases_both_locks_even_on_failure
test_with_stack_lock_takes_slot_2_when_slot_1_is_held
test_with_stack_lock_fails_and_frees_the_stack_lock_when_every_heavy_slot_is_held
test_with_stack_lock_runs_the_command_and_returns_its_exit_code
test_with_stack_lock_fails_when_the_stack_lock_is_already_held
echo "All ui-evidence locks tests passed."
