#!/usr/bin/env bash
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/external-write-guard.sh"
# The hook logs fail-open events under $AW_STATE_DIR (default ~/.agentic-workflow): this suite gets its own, never an inherited one.
AW_STATE_DIR="$(mktemp -d)" || exit 2; export AW_STATE_DIR
trap 'find "$AW_STATE_DIR" -delete 2> /dev/null' EXIT

write_session_with_auto_continue() {
  local dir="$1" session="$2"
  mkdir -p "$dir"
  echo '{"auto_continued_at":"2026-09-27T00:00:00Z"}' > "$dir/$session.json"
}

run_guard() { # $1 sessions_dir, $2 command
  AW_JUDGE_SESSIONS_DIR="$1" bash "$HOOK" <<< "$(jq -nc --arg c "$2" '{session_id:"s1",tool_name:"Bash",tool_input:{command:$c}}')"
}

test_auto_continued_plus_feature_branch_push_allows() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  out="$(run_guard "$sessions_dir" 'git push origin feat/x')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: expected allow for a branch push after auto-continue, got $out"; exit 1; }
  echo "PASS: test_auto_continued_plus_feature_branch_push_allows"
}

test_auto_continued_plus_gh_pr_create_allows() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  out="$(run_guard "$sessions_dir" 'gh pr create --title t --body b')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: expected allow for gh pr create after auto-continue, got $out"; exit 1; }
  echo "PASS: test_auto_continued_plus_gh_pr_create_allows"
}

test_no_auto_continue_plus_gh_pr_merge_allows() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  out="$(run_guard "$sessions_dir" 'gh pr merge 123')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: expected allow when auto_continued_at is unset, got $out"; exit 1; }
  echo "PASS: test_no_auto_continue_plus_gh_pr_merge_allows"
}

test_gh_pr_comment_and_review_are_gated() {
  local sessions_dir out c
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  for c in 'gh pr comment 1 -b hi' 'gh pr review 1 --approve'; do
    out="$(run_guard "$sessions_dir" "$c")"
    echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null \
      || { echo "FAIL: expected '$c' to be gated, got $out"; exit 1; }
  done
  echo "PASS: test_gh_pr_comment_and_review_are_gated"
}

test_deny_reason_does_not_mention_push() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  out="$(run_guard "$sessions_dir" 'gh pr merge 123')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecisionReason | test("push"; "i") | not' > /dev/null \
    || { echo "FAIL: deny reason must not mention push, got $out"; exit 1; }
  echo "PASS: test_deny_reason_does_not_mention_push"
}

test_auto_continued_but_unmatched_tool_always_allows_rf5() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  out="$(AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< '{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"npm test"}}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: RF-5 — a tool outside the matched set must always allow, got $out"; exit 1; }
  echo "PASS: test_auto_continued_but_unmatched_tool_always_allows_rf5"
}

test_linear_save_is_matched_and_gated() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  out="$(AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< '{"session_id":"s1","tool_name":"mcp__claude_ai_Linear__save_issue","tool_input":{}}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null \
    || { echo "FAIL: expected Linear save_* to be gated, got $out"; exit 1; }
  echo "PASS: test_linear_save_is_matched_and_gated"
}

test_slack_send_is_matched_and_gated() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  out="$(AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< '{"session_id":"s1","tool_name":"mcp__claude_ai_Slack__slack_send_message","tool_input":{}}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null \
    || { echo "FAIL: expected Slack send_* to be gated, got $out"; exit 1; }
  echo "PASS: test_slack_send_is_matched_and_gated"
}

test_gh_pr_merge_is_matched_and_gated() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  out="$(AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< '{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"gh pr merge 123"}}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null \
    || { echo "FAIL: expected gh pr merge to be gated, got $out"; exit 1; }
  echo "PASS: test_gh_pr_merge_is_matched_and_gated"
}

test_aw_judge_child_gets_explicit_allow() {
  local out
  out="$(AW_JUDGE_CHILD=1 bash "$HOOK" <<< '{}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: expected explicit allow under AW_JUDGE_CHILD, got $out"; exit 1; }
  echo "PASS: test_aw_judge_child_gets_explicit_allow"
}

# A matched (gated) command, so the fallback tests prove the state-file
# handling rather than a trivially allowed command.
risky_command_text() {
  printf 'gh pr merge 123'
}

test_corrupt_session_file_falls_back_to_allow() {
  local sessions_dir out cmd
  sessions_dir="$(mktemp -d)"
  echo 'not valid json {{{' > "$sessions_dir/s1.json"
  cmd="$(risky_command_text)"
  out="$(AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< "$(jq -nc --arg c "$cmd" '{session_id:"s1",tool_name:"Bash",tool_input:{command:$c}}')" 2>/dev/null)"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: a corrupt session state file must fall back to 'no auto-continue happened' (allow), got $out"; exit 1; }
  echo "PASS: test_corrupt_session_file_falls_back_to_allow"
}

test_unreadable_session_file_falls_back_to_allow() {
  local sessions_dir out cmd
  sessions_dir="$(mktemp -d)"
  echo '{"auto_continued_at":"2026-09-27T00:00:00Z"}' > "$sessions_dir/s1.json"
  chmod 000 "$sessions_dir/s1.json"
  cmd="$(risky_command_text)"
  out="$(AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< "$(jq -nc --arg c "$cmd" '{session_id:"s1",tool_name:"Bash",tool_input:{command:$c}}')" 2>/dev/null)"
  chmod 644 "$sessions_dir/s1.json"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: an unreadable session state file must fall back to allow, got $out"; exit 1; }
  echo "PASS: test_unreadable_session_file_falls_back_to_allow"
}

test_a_meta_lane_is_never_denied_after_an_auto_continue() {
  local sessions_dir out
  sessions_dir="$(mktemp -d)"
  write_session_with_auto_continue "$sessions_dir" "s1"
  out="$(AW_META_TEAM=t AW_META_LANE=lane-a run_guard "$sessions_dir" 'gh pr merge 123')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: a lane (AW_META_TEAM + AW_META_LANE) must be allowed gh pr merge after an auto-continue, got $out"; exit 1; }
  out="$(AW_META_LANE=lane-a run_guard "$sessions_dir" 'gh pr merge 123')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null \
    || { echo "FAIL: AW_META_LANE alone (no team) is not a lane: still denied, got $out"; exit 1; }
  echo "PASS: test_a_meta_lane_is_never_denied_after_an_auto_continue"
}

test_guard_never_invokes_judge() {
  grep -qE '(^|[^_a-zA-Z-])judge($| )' "$HOOK" && { echo "FAIL: external-write-guard.sh must be deterministic only — it must never call the judge binary"; exit 1; }
  echo "PASS: test_guard_never_invokes_judge"
}

test_auto_continued_plus_feature_branch_push_allows
test_auto_continued_plus_gh_pr_create_allows
test_no_auto_continue_plus_gh_pr_merge_allows
test_gh_pr_comment_and_review_are_gated
test_deny_reason_does_not_mention_push
test_auto_continued_but_unmatched_tool_always_allows_rf5
test_linear_save_is_matched_and_gated
test_slack_send_is_matched_and_gated
test_gh_pr_merge_is_matched_and_gated
test_aw_judge_child_gets_explicit_allow
test_corrupt_session_file_falls_back_to_allow
test_unreadable_session_file_falls_back_to_allow
test_guard_never_invokes_judge
test_a_meta_lane_is_never_denied_after_an_auto_continue
echo "All external-write-guard tests passed."
