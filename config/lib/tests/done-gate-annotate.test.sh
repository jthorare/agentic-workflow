#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/done-gate-annotate.sh"

setup_fake_judge() {
  local bin_dir brief_json; bin_dir="$(mktemp -d)"; brief_json="${1:-}"
  cat > "$bin_dir/judge" <<EOF
#!/usr/bin/env bash
if [ "\$1" = "brief" ] && [ "\$2" = "get" ]; then
  if [ -n '$brief_json' ]; then
    printf '%s' '$brief_json'
    exit 0
  fi
  exit 1
fi
EOF
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

test_matching_final_message_annotates_satisfied() {
  local bin_dir out
  bin_dir="$(setup_fake_judge '{"acceptanceCriteria":"tests pass and PR opened"}')"
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu1","tool_response":{"result":"Done: tests pass and PR opened, see #123."}}')"
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext | test("plausibly met"; "i")' > /dev/null \
    || { echo "FAIL: expected a 'plausibly met' annotation, got $out"; exit 1; }
  echo "PASS: test_matching_final_message_annotates_satisfied"
}

test_mismatched_final_message_annotates_flagged_but_still_allows() {
  local bin_dir out rc
  bin_dir="$(setup_fake_judge '{"acceptanceCriteria":"the migration runs cleanly on staging"}')"
  set +e
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu2","tool_response":{"result":"Done, ran the unit tests."}}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: this hook only annotates, must never exit nonzero, got $rc"; exit 1; }
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext | test("NOT clearly addressed")' > /dev/null \
    || { echo "FAIL: expected a mismatch annotation, got $out"; exit 1; }
  echo "PASS: test_mismatched_final_message_annotates_flagged_but_still_allows"
}

test_no_brief_found_adds_no_annotation() {
  local bin_dir out
  bin_dir="$(setup_fake_judge)"
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu3","tool_response":{"result":"done"}}')"
  [ -z "$out" ] || { echo "FAIL: expected no output when no brief is found (exempt dispatch, or mapping never landed), got: $out"; exit 1; }
  echo "PASS: test_no_brief_found_adds_no_annotation"
}

# #3543: a background spawn returns before the builder has said anything, so there is no final message to check; echoing
# the whole brief back then doubled every spawn in the lead's context.
test_background_spawn_adds_no_annotation() {
  local bin_dir out
  bin_dir="$(setup_fake_judge '{"acceptanceCriteria":"tests pass and PR opened"}')"
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu6","tool_response":{"status":"teammate_spawned","prompt":"long brief","agent_id":"a1"}}')"
  [ -z "$out" ] || { echo "FAIL: expected no annotation for a spawn with no final message yet, got: $out"; exit 1; }
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu7","tool_response":{"status":"async_launched"}}')"
  [ -z "$out" ] || { echo "FAIL: expected no annotation for an async launch, got: $out"; exit 1; }
  echo "PASS: test_background_spawn_adds_no_annotation"
}

test_long_criteria_are_quoted_short() {
  local bin_dir out crit len
  crit="$(printf 'x%.0s' $(seq 1 3000))"
  bin_dir="$(setup_fake_judge "{\"acceptanceCriteria\":\"$crit\"}")"
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu8","tool_response":{"result":"Done, ran the unit tests."}}')"
  len="$(echo "$out" | jq -r '.hookSpecificOutput.additionalContext | length')"
  [ "$len" -le 300 ] || { echo "FAIL: the annotation quoted $len chars; it must quote the criteria short (<= 300 chars total)"; exit 1; }
  echo "PASS: test_long_criteria_are_quoted_short"
}

# A real finished Agent result has no .result: its text is in .content[] blocks (status "completed").
test_real_completed_shape_that_matches_annotates_satisfied() {
  local bin_dir out
  bin_dir="$(setup_fake_judge '{"acceptanceCriteria":"tests pass and PR opened"}')"
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu9","tool_response":{"status":"completed","content":[{"type":"text","text":"Done: tests pass and PR opened, see #123."}],"agentId":"a1"}}')"
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext | test("plausibly met"; "i")' > /dev/null \
    || { echo "FAIL: expected 'plausibly met' for a real completed result, got: $out"; exit 1; }
  echo "PASS: test_real_completed_shape_that_matches_annotates_satisfied"
}

test_real_completed_shape_that_misses_annotates_flagged() {
  local bin_dir out
  bin_dir="$(setup_fake_judge '{"acceptanceCriteria":"the migration runs cleanly on staging"}')"
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu10","tool_response":{"status":"completed","content":[{"type":"text","text":"Done, ran the unit tests."}],"agentId":"a1"}}')"
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext | test("NOT clearly addressed")' > /dev/null \
    || { echo "FAIL: expected 'NOT clearly addressed' for a real completed result that misses, got: $out"; exit 1; }
  echo "PASS: test_real_completed_shape_that_misses_annotates_flagged"
}

test_real_completed_shape_that_matches_annotates_satisfied
test_real_completed_shape_that_misses_annotates_flagged
test_background_spawn_adds_no_annotation
test_long_criteria_are_quoted_short
test_matching_final_message_annotates_satisfied
test_mismatched_final_message_annotates_flagged_but_still_allows
test_no_brief_found_adds_no_annotation
echo "All done-gate-annotate tests passed."

test_judge_missing_from_path_still_exits_0_no_annotation() {
  local out rc
  set +e
  out="$(PATH="/usr/bin:/bin" bash "$HOOK" <<< '{"tool_use_id":"tu4","tool_response":{"result":"done"}}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 when judge is missing from PATH entirely, got $rc"; exit 1; }
  [ -z "$out" ] || { echo "FAIL: expected no annotation when judge is unreachable, got: $out"; exit 1; }
  echo "PASS: test_judge_missing_from_path_still_exits_0_no_annotation"
}

test_judge_nonzero_garbage_stdout_still_exits_0() {
  local bin_dir out rc
  bin_dir="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
echo 'not valid json {{{'
exit 1
EOF
  chmod +x "$bin_dir/judge"
  set +e
  out="$(PATH="$bin_dir:$PATH" bash "$HOOK" <<< '{"tool_use_id":"tu5","tool_response":{"result":"done"}}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: garbage/nonzero judge output must never make this hook exit nonzero, got $rc"; exit 1; }
  [ -z "$out" ] || { echo "FAIL: expected no annotation when the brief lookup returns unparsable garbage, got: $out"; exit 1; }
  echo "PASS: test_judge_nonzero_garbage_stdout_still_exits_0"
}

test_judge_missing_from_path_still_exits_0_no_annotation
test_judge_nonzero_garbage_stdout_still_exits_0
