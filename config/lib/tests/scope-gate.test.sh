#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/scope-gate.sh"
# The hook logs fail-open events under $AW_STATE_DIR (default ~/.agentic-workflow): this suite gets its own, never an inherited one.
AW_STATE_DIR="$(mktemp -d)" || exit 2; export AW_STATE_DIR
trap 'find "$AW_STATE_DIR" -delete 2> /dev/null' EXIT

setup_fake_judge() {
  local bin_dir decision; bin_dir="$(mktemp -d)"; decision="$1"
  cat > "$bin_dir/judge" <<EOF
#!/usr/bin/env bash
if [ "\$1" = "brief-scope" ]; then
  cat > /dev/null
  printf '%s' "\${AW_SESSION_ID:-}" > "$bin_dir/session-id"
  echo '{"decision":"$decision","confidence":0.9,"model":"claude-cli","reason_code":"model","id":"fixed-id"}'
elif [ "\$1" = "brief" ] && [ "\$2" = "save" ]; then
  cat > "$bin_dir/last-save.json"
  exit 0
fi
EOF
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

run_hook() {
  local input="$1" bin_dir="$2"
  PATH="$bin_dir:$PATH" bash "$HOOK" <<< "$input"
}

test_ready_allows_and_saves_the_brief() {
  local bin_dir out
  bin_dir="$(setup_fake_judge ready)"
  out="$(run_hook '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu1","tool_input":{"subagent_type":"lean-coder","description":"add X","prompt":"Goal: add X. Acceptance: tests pass. Proof: npm test"}}' "$bin_dir")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: expected allow for ready, got $out"; exit 1; }
  [ -f "$bin_dir/last-save.json" ] || { echo "FAIL: expected judge brief save to be called"; exit 1; }
  jq -e '.toolUseId == "tu1"' "$bin_dir/last-save.json" > /dev/null || { echo "FAIL: expected the saved brief to carry tool_use_id"; exit 1; }
  [ "$(cat "$bin_dir/session-id")" = "s1" ] || { echo "FAIL: brief-scope must see AW_SESSION_ID=s1, got '$(cat "$bin_dir/session-id")'"; exit 1; }
  echo "PASS: test_ready_allows_and_saves_the_brief"
}

test_missing_denies_with_field_reason() {
  local bin_dir out
  bin_dir="$(setup_fake_judge missing)"
  out="$(run_hook '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu2","tool_input":{"subagent_type":"lean-coder","description":"","prompt":""}}' "$bin_dir")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || { echo "FAIL: expected deny for missing, got $out"; exit 1; }
  echo "$out" | jq -r '.hookSpecificOutput.permissionDecisionReason' | has -i "goal or acceptance" || { echo "FAIL: expected the missing-field reason, got $out"; exit 1; }
  echo "PASS: test_missing_denies_with_field_reason"
}

test_needs_design_denies_with_user_wording() {
  local bin_dir out
  bin_dir="$(setup_fake_judge needs_design)"
  out="$(run_hook '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu3","tool_input":{"subagent_type":"lean-coder","description":"make it better","prompt":"make it nicer"}}' "$bin_dir")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || { echo "FAIL: expected deny for needs_design, got $out"; exit 1; }
  echo "$out" | jq -r '.hookSpecificOutput.permissionDecisionReason' | has -i "user" || { echo "FAIL: expected 'send this to the user' wording, got $out"; exit 1; }
  echo "PASS: test_needs_design_denies_with_user_wording"
}

test_skill_internal_dispatch_is_passed_through_and_reads_ready() {
  local bin_dir out
  bin_dir="$(setup_fake_judge ready)"
  out="$(run_hook '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu5","tool_input":{"subagent_type":"lean-coder","description":"","prompt":"","skill_internal":true}}' "$bin_dir")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: expected allow for a skill-internal dispatch (F13), got $out"; exit 1; }
  echo "PASS: test_skill_internal_dispatch_is_passed_through_and_reads_ready"
}

test_judge_failure_fails_open_to_allow() {
  local bin_dir out; bin_dir="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
cat >/dev/null  # drain stdin like the real judge, so the logged cause is judge-exit-1, never a SIGPIPE 141
exit 1
EOF
  chmod +x "$bin_dir/judge"
  out="$(run_hook '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu4","tool_input":{"subagent_type":"lean-coder","description":"x","prompt":"x"}}' "$bin_dir")"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: expected fail-open allow when judge fails, got $out"; exit 1; }
  echo "PASS: test_judge_failure_fails_open_to_allow"
}

test_aw_judge_child_gets_explicit_allow() {
  local out
  out="$(AW_JUDGE_CHILD=1 bash "$HOOK" <<< '{}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: expected explicit allow under AW_JUDGE_CHILD, got $out"; exit 1; }
  echo "PASS: test_aw_judge_child_gets_explicit_allow"
}

test_ready_allows_and_saves_the_brief
test_missing_denies_with_field_reason
test_needs_design_denies_with_user_wording
test_skill_internal_dispatch_is_passed_through_and_reads_ready
test_judge_failure_fails_open_to_allow
test_aw_judge_child_gets_explicit_allow
# A throwaway git checkout with a screenshot in it (the hooks only attach images under the checkout they run in).
make_checkout_with_png() {
  local d; d="$(mktemp -d)"; git -C "$d" init -q; printf '\211PNG\r\n\032\n' > "$d/shot.png"; echo "$d"
}

# --- judge images (spec 5.2): an existing screenshot named in the brief goes to brief-scope as `images` ---
setup_recording_scope_judge() {
  local bin_dir; bin_dir="$(mktemp -d)"
  cat > "$bin_dir/judge" <<EOF
#!/usr/bin/env bash
if [ "\$1" = "brief-scope" ]; then
  cat > "$bin_dir/scope-input.json"
  echo '{"decision":"ready","confidence":0.9,"model":"claude-cli","reason_code":"model","id":"fixed-id"}'
elif [ "\$1" = "brief" ] && [ "\$2" = "save" ]; then
  cat > /dev/null
fi
EOF
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

test_a_screenshot_named_in_the_goal_is_sent_to_brief_scope_as_an_image() {
  local bin_dir repo input
  bin_dir="$(setup_recording_scope_judge)"; repo="$(make_checkout_with_png)"
  input="$(jq -nc --arg g "match $repo/shot.png" '{session_id:"s1",prompt_id:"p1",tool_use_id:"tu9",tool_input:{subagent_type:"lean-coder",description:$g,prompt:"Acceptance: it looks like the screenshot. Proof: npm test"}}')"
  ( cd "$repo" && PATH="$bin_dir:$PATH" bash "$HOOK" <<< "$input" > /dev/null )
  jq -e --arg p "$repo/shot.png" '.images == [$p] and .imagesBestEffort == true' "$bin_dir/scope-input.json" > /dev/null \
    || { echo "FAIL: expected brief-scope to be sent images [$repo/shot.png] with imagesBestEffort, got $(cat "$bin_dir/scope-input.json")"; exit 1; }
  echo "PASS: test_a_screenshot_named_in_the_goal_is_sent_to_brief_scope_as_an_image"
}

# Green today and must stay so: a brief that names no image is today's input, byte for byte.
test_a_brief_naming_no_image_keeps_the_brief_scope_input_byte_identical() {
  local bin_dir
  bin_dir="$(setup_recording_scope_judge)"
  PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu8","tool_input":{"subagent_type":"lean-coder","description":"add X","prompt":"Acceptance: tests pass"}}' > /dev/null
  [ "$(cat "$bin_dir/scope-input.json")" = '{"agentType":"lean-coder","goal":"add X","acceptanceCriteria":"Acceptance: tests pass","proofCommand":"Acceptance: tests pass","skillInternal":false}' ] \
    || { echo "FAIL: a call without images must be today's input, got $(cat "$bin_dir/scope-input.json")"; exit 1; }
  echo "PASS: test_a_brief_naming_no_image_keeps_the_brief_scope_input_byte_identical"
}

test_a_brief_naming_no_image_keeps_the_brief_scope_input_byte_identical
test_a_screenshot_named_in_the_goal_is_sent_to_brief_scope_as_an_image
echo "All scope-gate tests passed."
