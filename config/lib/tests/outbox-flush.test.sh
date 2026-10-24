#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/outbox-flush.sh"

test_exits_2_when_this_teammates_own_item_is_queued() {
  local outbox; outbox="$(mktemp -d)"
  echo '{"ts":"2026-09-27T00:00:00Z","agentType":"builder-a","text":"still on task 2"}' > "$outbox/s1.jsonl"
  set +e
  AW_OUTBOX_DIR="$outbox" bash "$HOOK" <<< '{"session_id":"s1","teammate_name":"builder-a"}' > /dev/null 2>&1
  local rc=$?
  set -e
  [ "$rc" -eq 2 ] || { echo "FAIL: expected exit 2 for this teammate's own queued item, got $rc"; exit 1; }
  echo "PASS: test_exits_2_when_this_teammates_own_item_is_queued"
}

test_exits_0_when_only_another_teammates_item_is_queued() {
  local outbox; outbox="$(mktemp -d)"
  echo '{"ts":"2026-09-27T00:00:00Z","agentType":"builder-b","text":"still on task 5"}' > "$outbox/s1.jsonl"
  set +e
  AW_OUTBOX_DIR="$outbox" bash "$HOOK" <<< '{"session_id":"s1","teammate_name":"builder-a"}' > /dev/null 2>&1
  local rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 when the only queued item belongs to a different teammate, got $rc"; exit 1; }
  echo "PASS: test_exits_0_when_only_another_teammates_item_is_queued"
}

test_exits_0_when_nothing_is_queued_rf2() {
  local outbox; outbox="$(mktemp -d)"
  set +e
  bash "$HOOK" <<< '{"session_id":"s1","teammate_name":"builder-a"}' > /dev/null 2>&1
  local rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 with no outbox file (RF-2), got $rc"; exit 1; }
  echo "PASS: test_exits_0_when_nothing_is_queued_rf2"
}

test_aw_judge_child_guard_exits_0() {
  local outbox; outbox="$(mktemp -d)"
  echo '{"ts":"x","agentType":"builder-a","text":"x"}' > "$outbox/s1.jsonl"
  set +e
  AW_OUTBOX_DIR="$outbox" AW_JUDGE_CHILD=1 bash "$HOOK" <<< '{"session_id":"s1","teammate_name":"builder-a"}' > /dev/null 2>&1
  local rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: recursion guard must exit 0, got $rc"; exit 1; }
  echo "PASS: test_aw_judge_child_guard_exits_0"
}

test_a_null_agent_type_item_is_the_leads_and_does_not_block_a_teammate() {
  local outbox; outbox="$(mktemp -d)"
  echo '{"ts":"2026-09-27T00:00:00Z","agentType":null,"text":"lead progress"}' > "$outbox/s1.jsonl"
  set +e
  AW_OUTBOX_DIR="$outbox" bash "$HOOK" <<< '{"session_id":"s1","teammate_name":"builder-a"}' > /dev/null 2>&1
  local rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: a null-agentType item must not block a teammate, got $rc"; exit 1; }
  set +e
  AW_OUTBOX_DIR="$outbox" bash "$HOOK" <<< '{"session_id":"s1"}' > /dev/null 2>&1
  rc=$?
  set -e
  [ "$rc" -eq 2 ] || { echo "FAIL: the lead (no teammate_name) must still be told to flush, got $rc"; exit 1; }
  echo "PASS: test_a_null_agent_type_item_is_the_leads_and_does_not_block_a_teammate"
}

test_exits_2_when_this_teammates_own_item_is_queued
test_a_null_agent_type_item_is_the_leads_and_does_not_block_a_teammate
test_exits_0_when_only_another_teammates_item_is_queued
test_exits_0_when_nothing_is_queued_rf2
test_aw_judge_child_guard_exits_0
echo "All outbox-flush tests passed."
