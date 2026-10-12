#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DIR/../locks.sh"

test_acquire_then_release_allows_a_second_acquire() {
  local lock; lock="$(mktemp -d)/lock"
  acquire_lock "$lock" 2 || { echo "FAIL: first acquire failed"; exit 1; }
  release_lock "$lock"
  acquire_lock "$lock" 2 || { echo "FAIL: second acquire failed after release"; exit 1; }
  release_lock "$lock"
  echo "PASS: test_acquire_then_release_allows_a_second_acquire"
}

test_acquire_times_out_when_held() {
  local lock; lock="$(mktemp -d)/lock"
  mkdir "$lock"
  if acquire_lock "$lock" 1; then
    echo "FAIL: expected timeout, but acquire succeeded"; rmdir "$lock"; exit 1
  fi
  rmdir "$lock"
  echo "PASS: test_acquire_times_out_when_held"
}

test_release_on_an_already_gone_lock_is_a_no_op() {
  local lock; lock="$(mktemp -d)/lock-never-created"
  release_lock "$lock" || { echo "FAIL: release must tolerate a missing lock dir"; exit 1; }
  echo "PASS: test_release_on_an_already_gone_lock_is_a_no_op"
}

test_acquire_prints_waiting_message_once_not_per_poll() {
  local lock; lock="$(mktemp -d)/lock"
  mkdir "$lock"
  local out
  out="$(acquire_lock "$lock" 2 2>&1 1>/dev/null || true)"
  rmdir "$lock"
  local count
  count="$(echo "$out" | grep -c "waiting on lock" || true)"
  [ "$count" -eq 1 ] || { echo "FAIL: expected exactly one waiting message, got $count"; exit 1; }
  echo "PASS: test_acquire_prints_waiting_message_once_not_per_poll"
}

test_any_lock_lets_two_holders_in_at_once() {
  local base; base="$(mktemp -d)/heavy.lock"
  local a b
  a="$(acquire_any_lock "$base" 2 0)" || { echo "FAIL: first holder did not get a slot"; exit 1; }
  b="$(acquire_any_lock "$base" 2 0)" || { echo "FAIL: second holder did not get a slot"; exit 1; }
  [ "$a" = "$base" ] || { echo "FAIL: first holder should take slot 1 ($base), got $a"; exit 1; }
  [ "$b" = "$base.2" ] || { echo "FAIL: second holder should take slot 2 ($base.2), got $b"; exit 1; }
  release_lock "$a"; release_lock "$b"
  echo "PASS: test_any_lock_lets_two_holders_in_at_once"
}

test_any_lock_third_holder_waits_then_times_out() {
  local base; base="$(mktemp -d)/heavy.lock"
  local a b err
  a="$(acquire_any_lock "$base" 2 0)"; b="$(acquire_any_lock "$base" 2 0)"
  err="$(mktemp)"
  if acquire_any_lock "$base" 2 1 >/dev/null 2>"$err"; then
    echo "FAIL: a third holder got in with two slots"; exit 1
  fi
  [ "$(grep -c "waiting on lock" "$err")" -eq 1 ] || { echo "FAIL: expected exactly one waiting line"; cat "$err"; exit 1; }
  [ ! -e "$base.3" ] || { echo "FAIL: a third slot dir was made"; exit 1; }
  release_lock "$b"
  local c; c="$(acquire_any_lock "$base" 2 0)" || { echo "FAIL: a freed slot was not taken"; exit 1; }
  [ "$c" = "$base.2" ] || { echo "FAIL: expected the freed slot $base.2, got $c"; exit 1; }
  release_lock "$a"; release_lock "$c"
  echo "PASS: test_any_lock_third_holder_waits_then_times_out"
}

test_any_lock_waiter_gets_a_slot_freed_while_it_waits() {
  local base; base="$(mktemp -d)/heavy.lock"
  local a b
  a="$(acquire_any_lock "$base" 2 0)"; b="$(acquire_any_lock "$base" 2 0)"
  ( sleep 1; release_lock "$a" ) &
  local c; c="$(acquire_any_lock "$base" 2 5 2>/dev/null)" || { echo "FAIL: waiter timed out though a slot was freed"; exit 1; }
  wait
  [ "$c" = "$base" ] || { echo "FAIL: expected slot 1, got $c"; exit 1; }
  release_lock "$b"; release_lock "$c"
  echo "PASS: test_any_lock_waiter_gets_a_slot_freed_while_it_waits"
}

test_any_lock_shares_slot_1_with_plain_acquire_lock() {
  local base; base="$(mktemp -d)/heavy.lock"
  acquire_lock "$base" 0 || { echo "FAIL: plain acquire_lock failed"; exit 1; }
  local a; a="$(acquire_any_lock "$base" 2 0)" || { echo "FAIL: slot 2 should be free"; exit 1; }
  [ "$a" = "$base.2" ] || { echo "FAIL: a plain holder of $base must count as slot 1; got $a"; exit 1; }
  if acquire_any_lock "$base" 2 0 >/dev/null 2>&1; then
    echo "FAIL: a plain holder plus one slot holder must fill two slots"; exit 1
  fi
  release_lock "$a"
  if acquire_lock "$base" 0 2>/dev/null; then
    echo "FAIL: plain acquire_lock got slot 1 while its plain holder still has it"; exit 1
  fi
  release_lock "$base"
  local b; b="$(acquire_any_lock "$base" 2 0)"
  if acquire_lock "$base" 0 2>/dev/null; then
    echo "FAIL: plain acquire_lock got $base while a slot holder has slot 1"; exit 1
  fi
  release_lock "$b"
  echo "PASS: test_any_lock_shares_slot_1_with_plain_acquire_lock"
}

test_any_lock_release_leaves_nothing() {
  local parent; parent="$(mktemp -d)"
  local base="$parent/heavy.lock" a b
  a="$(acquire_any_lock "$base" 2 0)"; b="$(acquire_any_lock "$base" 2 0)"
  release_lock "$a"; release_lock "$b"
  [ -z "$(ls -A "$parent")" ] || { echo "FAIL: release left $(ls -A "$parent")"; exit 1; }
  echo "PASS: test_any_lock_release_leaves_nothing"
}

test_any_lock_with_one_slot_is_the_plain_lock() {
  local base; base="$(mktemp -d)/heavy.lock"
  local a; a="$(acquire_any_lock "$base" 1 0)"
  [ "$a" = "$base" ] || { echo "FAIL: one slot should be $base, got $a"; exit 1; }
  if acquire_any_lock "$base" 1 0 >/dev/null 2>&1; then echo "FAIL: one slot let two in"; exit 1; fi
  release_lock "$a"
  echo "PASS: test_any_lock_with_one_slot_is_the_plain_lock"
}

test_acquire_then_release_allows_a_second_acquire
test_acquire_times_out_when_held
test_release_on_an_already_gone_lock_is_a_no_op
test_acquire_prints_waiting_message_once_not_per_poll
test_any_lock_lets_two_holders_in_at_once
test_any_lock_third_holder_waits_then_times_out
test_any_lock_waiter_gets_a_slot_freed_while_it_waits
test_any_lock_shares_slot_1_with_plain_acquire_lock
test_any_lock_release_leaves_nothing
test_any_lock_with_one_slot_is_the_plain_lock
echo "All locks tests passed."
