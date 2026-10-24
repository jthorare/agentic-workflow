#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/send-gate.sh"
# The hook logs fail-open events under $AW_STATE_DIR (default ~/.agentic-workflow): this suite gets its own, never an inherited one.
AW_STATE_DIR="$(mktemp -d)" || exit 2; export AW_STATE_DIR
trap 'find "$AW_STATE_DIR" -delete 2> /dev/null' EXIT

# Every fake judge reads all of stdin before it answers, like the real one. The hook runs under pipefail, so a stub that
# exits without reading lets the hook's `printf | judge` die of SIGPIPE (141) when the stub wins the race, which a loaded
# box makes common: a spurious fail-open allow where the test expects deny or a queued item.
setup_fake_judge() {
  local bin_dir decision; bin_dir="$(mktemp -d)"; decision="$1"
  cat > "$bin_dir/judge" <<EOF
#!/usr/bin/env bash
cat > /dev/null
printf '%s' "\${AW_SESSION_ID:-}" > "$bin_dir/session-id"
echo '{"decision":"$decision","confidence":0.9,"model":"claude-cli","reason_code":"model","id":"fixed-id"}'
EOF
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

run_hook() {
  local input="$1" bin_dir="$2" outbox_dir="$3"
  PATH="$bin_dir:$PATH" AW_OUTBOX_DIR="$outbox_dir" bash "$HOOK" <<< "$input"
}

test_send_is_allowed_unmodified() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge send)"; outbox="$(mktemp -d)"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"done: PR #100 merged"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: expected allow, got $out"; exit 1; }
  [ "$(cat "$bin_dir/session-id")" = "s1" ] || { echo "FAIL: wake-gate must see AW_SESSION_ID=s1, got '$(cat "$bin_dir/session-id")'"; exit 1; }
  echo "PASS: test_send_is_allowed_unmodified"
}

test_drop_denies_with_no_ack_reason() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge drop)"; outbox="$(mktemp -d)"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"ok"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || { echo "FAIL: expected deny for drop, got $out"; exit 1; }
  echo "PASS: test_drop_denies_with_no_ack_reason"
}

test_batch_denies_and_queues_tagged_by_agent_type() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge batch)"; outbox="$(mktemp -d)"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"still working on task 2"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || { echo "FAIL: expected deny for batch, got $out"; exit 1; }
  [ -f "$outbox/s1.jsonl" ] || { echo "FAIL: expected an outbox file for session s1"; exit 1; }
  grep -q '"agentType":"builder-a"' "$outbox/s1.jsonl" || { echo "FAIL: queued item must be tagged by sender agent_type"; exit 1; }
  echo "PASS: test_batch_denies_and_queues_tagged_by_agent_type"
}

test_batch_from_main_session_queues_with_null_agent_type() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge batch)"; outbox="$(mktemp -d)"
  out="$(run_hook '{"session_id":"s1","tool_input":{"to":"main","message":"progress"}}' "$bin_dir" "$outbox")"
  grep -q '"agentType":null' "$outbox/s1.jsonl" || { echo "FAIL: expected a null agentType for a main-session sender"; exit 1; }
  echo "PASS: test_batch_from_main_session_queues_with_null_agent_type"
}

test_a_judge_failure_fails_open_to_allow_rf1() {
  local bin_dir out outbox; bin_dir="$(mktemp -d)"; outbox="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
cat > /dev/null
exit 1
EOF
  chmod +x "$bin_dir/judge"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"x"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: expected fail-open allow when judge fails (RF-1), got $out"; exit 1; }
  echo "PASS: test_a_judge_failure_fails_open_to_allow_rf1"
}

test_send_with_a_pending_digest_rewrites_updated_input_and_clears_the_queue() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge send)"; outbox="$(mktemp -d)"
  # deviation from plan draft (2026-09-27, this build): the plan's literal
  # fixture timestamp "2026-09-27T00:00:00Z" is more than 2h before this
  # machine's real wall clock on the same date, so it fails the send-gate's
  # own expiry filter and this test spuriously fails. Use "now" instead so the
  # item is always fresh regardless of when this test runs.
  jq -nc --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" '{ts:$ts, agentType:"builder-a", text:"still on task 2"}' > "$outbox/s1.jsonl"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"done: task 3 too"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.updatedInput.message | contains("still on task 2")' > /dev/null \
    || { echo "FAIL: expected the queued digest to ride along in updatedInput.message, got $out"; exit 1; }
  [ -s "$outbox/s1.jsonl" ] && { echo "FAIL: expected the outbox to be cleared after riding along"; exit 1; }
  echo "PASS: test_send_with_a_pending_digest_rewrites_updated_input_and_clears_the_queue"
}

test_the_rewrite_keeps_to_and_the_other_fields() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge send)"; outbox="$(mktemp -d)"
  jq -nc --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" '{ts:$ts, agentType:"builder-a", to:"main", text:"queued one"}' > "$outbox/s1.jsonl"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","summary":"s","message":"done"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.updatedInput | .to == "main" and .summary == "s" and (.message | contains("queued one"))' > /dev/null \
    || { echo "FAIL: same recipient: expected updatedInput with to, summary and the merged message, got $out"; exit 1; }
  echo "PASS: test_the_rewrite_keeps_to_and_the_other_fields"
}

test_a_queued_item_for_another_recipient_is_not_merged_and_is_kept() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge send)"; outbox="$(mktemp -d)"
  jq -nc --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" '{ts:$ts, agentType:"builder-a", to:"alice", text:"for alice"}' > "$outbox/s1.jsonl"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"bob","message":"hello bob"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow" and (.hookSpecificOutput.updatedInput == null)' > /dev/null \
    || { echo "FAIL: different recipient: expected a plain allow with no rewrite, got $out"; exit 1; }
  jq -e 'select(.to == "alice" and .text == "for alice")' "$outbox/s1.jsonl" > /dev/null \
    || { echo "FAIL: the alice item must stay queued for alice"; exit 1; }
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"alice","message":"hello alice"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.updatedInput | .to == "alice" and (.message | contains("for alice"))' > /dev/null \
    || { echo "FAIL: the next send to alice should carry the item, got $out"; exit 1; }
  [ -s "$outbox/s1.jsonl" ] && { echo "FAIL: the outbox should be empty after delivery"; exit 1; }
  echo "PASS: test_a_queued_item_for_another_recipient_is_not_merged_and_is_kept"
}

test_batch_records_the_recipient() {
  local bin_dir outbox; bin_dir="$(setup_fake_judge batch)"; outbox="$(mktemp -d)"
  run_hook '{"session_id":"s1","tool_input":{"to":"alice","message":"p"}}' "$bin_dir" "$outbox" > /dev/null
  jq -e 'select(.to == "alice")' "$outbox/s1.jsonl" > /dev/null || { echo "FAIL: the queued item must record its recipient"; exit 1; }
  echo "PASS: test_batch_records_the_recipient"
}

test_a_malformed_queue_fails_open_and_is_left_intact() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge send)"; outbox="$(mktemp -d)"
  printf 'not json\n' > "$outbox/s1.jsonl"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"done"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow" and (.hookSpecificOutput.updatedInput == null)' > /dev/null \
    || { echo "FAIL: malformed queue: expected the original send allowed unchanged, got $out"; exit 1; }
  [ "$(cat "$outbox/s1.jsonl")" = "not json" ] || { echo "FAIL: a malformed queue must not be truncated"; exit 1; }
  echo "PASS: test_a_malformed_queue_fails_open_and_is_left_intact"
}

test_concurrent_batch_appends_dont_lose_an_item_rf4() {
  local bin_dir outbox; bin_dir="$(setup_fake_judge batch)"; outbox="$(mktemp -d)"
  run_hook '{"session_id":"s1","agent_type":"a","tool_input":{"to":"main","message":"first"}}' "$bin_dir" "$outbox" > /dev/null &
  run_hook '{"session_id":"s1","agent_type":"b","tool_input":{"to":"main","message":"second"}}' "$bin_dir" "$outbox" > /dev/null &
  wait
  local n; n="$(wc -l < "$outbox/s1.jsonl" | tr -d ' ')"
  [ "$n" -eq 2 ] || { echo "FAIL: expected 2 queued items after concurrent batches, got $n (RF-4)"; exit 1; }
  echo "PASS: test_concurrent_batch_appends_dont_lose_an_item_rf4"
}

test_aw_judge_child_guard() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge send)"; outbox="$(mktemp -d)"
  out="$(PATH="$bin_dir:$PATH" AW_OUTBOX_DIR="$outbox" AW_JUDGE_CHILD=1 bash "$HOOK" <<< '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"x"}}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: guard must still produce an explicit allow, got $out"; exit 1; }
  echo "PASS: test_aw_judge_child_guard"
}

test_control_messages_bypass_the_classifier_entirely_f6() {
  local bin_dir out outbox; bin_dir="$(mktemp -d)"; outbox="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
cat > /dev/null
echo "FAIL: judge must never be called for a control message" >&2
exit 1
EOF
  chmod +x "$bin_dir/judge"
  out="$(PATH="$bin_dir:$PATH" AW_OUTBOX_DIR="$outbox" bash "$HOOK" <<< '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","type":"shutdown_request","message":{"type":"shutdown_request","reason":"context full"}}}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: expected an unconditional allow for a shutdown_request, got $out"; exit 1; }
  echo "PASS: test_control_messages_bypass_the_classifier_entirely_f6"
}

test_an_old_queued_item_is_never_expired() {
  local bin_dir out outbox; bin_dir="$(setup_fake_judge send)"; outbox="$(mktemp -d)"
  jq -nc '{ts:"2026-01-01T00:00:00Z", agentType:"builder-a", text:"ancient progress"}' > "$outbox/s1.jsonl"
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"done"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.updatedInput.message | contains("ancient progress")' > /dev/null \
    || { echo "FAIL: a queued item older than 2 h must still ride along, got $out"; exit 1; }
  [ ! -e "$outbox/expired.jsonl" ] || { echo "FAIL: nothing expires any more"; exit 1; }
  echo "PASS: test_an_old_queued_item_is_never_expired"
}

test_a_lane_message_to_meta_or_the_lead_skips_wake_gating() {
  local bin_dir out outbox to; bin_dir="$(setup_fake_judge drop)"; outbox="$(mktemp -d)"
  for to in main team-lead meta; do
    out="$(AW_META_LANE=lane-a run_hook "{\"session_id\":\"s1\",\"agent_type\":\"builder-a\",\"tool_input\":{\"to\":\"$to\",\"message\":\"ok\"}}" "$bin_dir" "$outbox")"
    echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
      || { echo "FAIL: a lane's message to $to must skip the wake gate (judge says drop), got $out"; exit 1; }
  done
  out="$(AW_META_LANE=lane-a run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"lane-b","message":"ok"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null \
    || { echo "FAIL: a lane's message to another lane is still gated, got $out"; exit 1; }
  out="$(run_hook '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"meta","message":"ok"}}' "$bin_dir" "$outbox")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null \
    || { echo "FAIL: an ordinary session is still gated, got $out"; exit 1; }
  echo "PASS: test_a_lane_message_to_meta_or_the_lead_skips_wake_gating"
}

test_send_is_allowed_unmodified
test_drop_denies_with_no_ack_reason
test_batch_denies_and_queues_tagged_by_agent_type
test_batch_from_main_session_queues_with_null_agent_type
test_a_judge_failure_fails_open_to_allow_rf1
test_send_with_a_pending_digest_rewrites_updated_input_and_clears_the_queue
test_the_rewrite_keeps_to_and_the_other_fields
test_a_queued_item_for_another_recipient_is_not_merged_and_is_kept
test_batch_records_the_recipient
test_a_malformed_queue_fails_open_and_is_left_intact
test_concurrent_batch_appends_dont_lose_an_item_rf4
test_aw_judge_child_guard
test_control_messages_bypass_the_classifier_entirely_f6
test_an_old_queued_item_is_never_expired
test_a_lane_message_to_meta_or_the_lead_skips_wake_gating
echo "All send-gate tests passed."
