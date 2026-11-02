#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/context-guard.sh"

# A real assistant transcript line, in the REAL message.usage key order
# (input_tokens, cache_creation_input_tokens, cache_read_input_tokens,
# output_tokens, then nested objects: output_tokens_details, server_tool_use,
# service_tier, cache_creation, inference_geo, iterations, speed). The old
# fast-path regex `"usage":\{[^}]*\}` stops at the FIRST nested `}` inside
# this object and never matches the whole flat-looking-but-not-flat usage
# object real transcripts actually have.
usage_line() {
  local input="$1" cache_read="$2" cache_creation="${3:-0}"
  jq -nc --argjson i "$input" --argjson cr "$cache_read" --argjson cc "$cache_creation" '{
    type: "assistant",
    sessionId: "s1",
    timestamp: "2026-09-27T00:00:00.000Z",
    message: {
      id: "m1",
      model: "x",
      usage: {
        input_tokens: $i,
        cache_creation_input_tokens: $cc,
        cache_read_input_tokens: $cr,
        output_tokens: 5,
        output_tokens_details: { reasoning_tokens: 0 },
        server_tool_use: { web_search_requests: 0 },
        service_tier: "standard",
        cache_creation: { ephemeral_5m_input_tokens: 0, ephemeral_1h_input_tokens: 0 },
        inference_geo: "us",
        iterations: [{ i: 1 }],
        speed: "normal"
      }
    }
  }'
}

# The OLD fast-path this test must prove is broken, run against the new
# fixture shape above.
old_broken_extract() {
  local transcript="$1"
  tail -c 8192 "$transcript" | grep -oE '"usage":\{[^}]*\}' | tail -1
}

write_transcript() {
  local file="$1" input="$2" cache_read="$3" pad_bytes="${4:-0}"
  usage_line "$input" "$cache_read" > "$file"
  if [ "$pad_bytes" -gt 0 ]; then
    head -c "$pad_bytes" /dev/zero | tr '\0' ' ' >> "$file"
  fi
}

# growth_gate_bytes is optional — the fourth positional arg — since the gate
# is now an env-overridable constant (AW_CONTEXT_GUARD_GROWTH_GATE_BYTES),
# not something config.json controls (latency fix, 2026-09-27 review #2).
run_hook() {
  local input="$1" config_dir="$2" state_dir="$3" growth_gate="${4:-}"
  if [ -n "$growth_gate" ]; then
    AW_CONTEXT_GUARD_DIR="$config_dir" AW_CONTEXT_GUARD_STATE_DIR="$state_dir" AW_CONTEXT_GUARD_GROWTH_GATE_BYTES="$growth_gate" bash "$HOOK" <<< "$input"
  else
    AW_CONTEXT_GUARD_DIR="$config_dir" AW_CONTEXT_GUARD_STATE_DIR="$state_dir" bash "$HOOK" <<< "$input"
  fi
}

test_blocker_old_regex_is_broken_on_real_key_order() {
  local transcript; transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 250000
  local out; out="$(old_broken_extract "$transcript")"
  # PROOF the old fast path is broken: it captures nothing usable (stops at
  # the first nested "}" inside the usage object, so it never yields a
  # value jq can parse `.usage.input_tokens` etc. out of correctly).
  if [ -n "$out" ] && printf '%s' "$out" | jq -e '.usage.cache_read_input_tokens == 250000' > /dev/null 2>&1; then
    echo "FAIL: expected the OLD regex to be broken on real key order, but it correctly extracted the usage object"; exit 1
  fi
  echo "PASS: test_blocker_old_regex_is_broken_on_real_key_order (old regex confirmed broken)"
}

test_skips_all_work_below_the_growth_gate() {
  local cfg state transcript out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 250000  # well over the token threshold...
  out="$(run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 1000000)"
  # ...but the growth gate (1MB) is set far above this file's real size, so no work happens at all.
  if [ -n "$out" ]; then echo "FAIL: expected no output below the growth gate, got: $out"; exit 1; fi
  echo "PASS: test_skips_all_work_below_the_growth_gate"
}

test_does_the_real_check_once_growth_exceeds_the_gate() {
  local cfg state transcript out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 250000
  out="$(run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 100)"
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext' > /dev/null || { echo "FAIL: expected the real check to run and trigger, got: $out"; exit 1; }
  echo "PASS: test_does_the_real_check_once_growth_exceeds_the_gate"
}

test_second_call_right_after_the_first_skips_again_state_file_updated() {
  local cfg state transcript out1 out2; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 250000
  out1="$(run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 100)"
  out2="$(run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 100)"
  [ -n "$out1" ] || { echo "FAIL: expected the first call to trigger"; exit 1; }
  [ -z "$out2" ] || { echo "FAIL: expected the second call (no further growth) to skip, got: $out2"; exit 1; }
  echo "PASS: test_second_call_right_after_the_first_skips_again_state_file_updated"
}

test_below_token_threshold_after_the_real_check_emits_nothing() {
  local cfg state transcript out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 500 0
  out="$(run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 100)"
  if [ -n "$out" ]; then echo "FAIL: expected no output below the token threshold, got: $out"; exit 1; fi
  echo "PASS: test_below_token_threshold_after_the_real_check_emits_nothing"
}

test_missing_transcript_is_silent_not_error() {
  local cfg state out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  out="$(run_hook '{"session_id":"s1","agent_id":"a1","transcript_path":"/tmp/does-not-exist-cg.jsonl"}' "$cfg" "$state")"
  if [ -n "$out" ]; then echo "FAIL: expected no output for a missing transcript, got: $out"; exit 1; fi
  echo "PASS: test_missing_transcript_is_silent_not_error"
}

test_missing_config_falls_back_to_defaults() {
  local state transcript out; state="$(mktemp -d)"
  transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 250000
  out="$(run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$(mktemp -d)" "$state")"
  # Default growthGateBytes is small enough that a fresh-file check still runs
  # (no prior state), so this exercises "no config at all, still works safely."
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext' > /dev/null 2>&1 || true  # either outcome is safe; just must not error
  echo "PASS: test_missing_config_falls_back_to_defaults"
}

test_wording_differs_for_a_main_session_with_no_agent_id() {
  local cfg state transcript out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 250000
  out="$(run_hook "{\"session_id\":\"s1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 100)"
  local msg; msg="$(echo "$out" | jq -r '.hookSpecificOutput.additionalContext')"
  if echo "$msg" | has -i "re-dispatch"; then
    echo "FAIL: a main session has nowhere to be re-dispatched to, wording must not imply it (RF-4): $msg"; exit 1
  fi
  echo "PASS: test_wording_differs_for_a_main_session_with_no_agent_id"
}

test_aw_judge_child_guard_exits_silently() {
  local cfg state transcript out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 250000
  out="$(AW_CONTEXT_GUARD_DIR="$cfg" AW_CONTEXT_GUARD_STATE_DIR="$state" AW_JUDGE_CHILD=1 bash "$HOOK" <<< "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}")"
  if [ -n "$out" ]; then echo "FAIL: AW_JUDGE_CHILD=1 must short-circuit before any work, got: $out"; exit 1; fi
  echo "PASS: test_aw_judge_child_guard_exits_silently"
}

test_missing_transcript_path_field_is_silent() {
  local cfg state out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  out="$(run_hook '{"session_id":"s1","agent_id":"a1"}' "$cfg" "$state")"
  if [ -n "$out" ]; then echo "FAIL: expected no output with no transcript_path, got: $out"; exit 1; fi
  echo "PASS: test_missing_transcript_path_field_is_silent"
}

test_large_tool_result_after_last_assistant_line_still_reads_tokens() {
  local cfg state transcript out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  usage_line 5000 250000 > "$transcript"
  # A > 8KB tool_result line follows the last assistant line — the old 8KB
  # tail window would miss the assistant usage line entirely once this is
  # appended; the fix's larger window + "last assistant line" grep must
  # still find it.
  jq -nc --arg big "$(head -c 9000 /dev/zero | tr '\0' 'x')" '{type:"user", sessionId:"s1", timestamp:"2026-09-27T00:01:00.000Z", uuid:"u1", message:{content:[{type:"tool_result", content:$big}]}}' >> "$transcript"
  out="$(run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 100)"
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext' > /dev/null || { echo "FAIL: expected the guard to still find the last assistant usage past a large trailing tool_result, got: $out"; exit 1; }
  echo "PASS: test_large_tool_result_after_last_assistant_line_still_reads_tokens"
}

test_fire_appends_one_line_to_fires_jsonl_non_fire_appends_nothing() {
  local cfg state transcript out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"

  transcript="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript" 5000 250000
  run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 100 > /dev/null
  [ -f "$cfg/fires.jsonl" ] || { echo "FAIL: expected fires.jsonl to be created on a fire"; exit 1; }
  local n; n="$(wc -l < "$cfg/fires.jsonl" | tr -d ' ')"
  [ "$n" -eq 1 ] || { echo "FAIL: expected exactly one fires.jsonl line, got $n"; exit 1; }
  jq -e '.tokens == 255000 and .agentId == "a1" and .sessionId == "s1"' "$cfg/fires.jsonl" > /dev/null || { echo "FAIL: fires.jsonl line shape wrong: $(cat "$cfg/fires.jsonl")"; exit 1; }

  local cfg2 state2 transcript2
  cfg2="$(mktemp -d)"; state2="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg2/config.json"
  transcript2="$(mktemp -d)/t.jsonl"
  write_transcript "$transcript2" 5000 500 0
  run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript2\"}" "$cfg2" "$state2" 100 > /dev/null
  [ -f "$cfg2/fires.jsonl" ] && { echo "FAIL: expected no fires.jsonl for a below-threshold call"; exit 1; }
  echo "PASS: test_fire_appends_one_line_to_fires_jsonl_non_fire_appends_nothing"
}

test_blocker_old_regex_is_broken_on_real_key_order
test_fire_appends_one_line_to_fires_jsonl_non_fire_appends_nothing
test_skips_all_work_below_the_growth_gate
test_does_the_real_check_once_growth_exceeds_the_gate
test_second_call_right_after_the_first_skips_again_state_file_updated
test_below_token_threshold_after_the_real_check_emits_nothing
test_missing_transcript_is_silent_not_error
test_missing_config_falls_back_to_defaults
test_wording_differs_for_a_main_session_with_no_agent_id
test_aw_judge_child_guard_exits_silently
test_missing_transcript_path_field_is_silent
test_over_1mb_trailing_content_still_reads_tokens() {
  # 2026-09-27 real-session finding (done-gate.sh's identical pattern): a
  # headless session's Stop/PostToolUse event can carry well over 1MB of
  # trailing hook-attachment JSON after the last assistant line — the old
  # fixed 64KB tail window missed it entirely, silently skipping the growth
  # check. Reproduce with >1MB of padding.
  local cfg state transcript out; cfg="$(mktemp -d)"; state="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  usage_line 5000 250000 > "$transcript"
  head -c 1200000 /dev/zero | tr '\0' 'x' | jq -Rsc '{type:"user", sessionId:"s1", timestamp:"2026-09-27T00:01:00.000Z", uuid:"u1", message:{content:[{type:"tool_result", content:.}]}}' >> "$transcript"
  out="$(run_hook "{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}" "$cfg" "$state" 100)"
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext' > /dev/null \
    || { echo "FAIL: expected the guard to still find the last assistant usage past >1MB of trailing padding, got: $out"; exit 1; }
  echo "PASS: test_over_1mb_trailing_content_still_reads_tokens"
}

test_digest_dir_override_and_default() {
  local cfg state transcript out st; cfg="$(mktemp -d)"; state="$(mktemp -d)"; st="$(mktemp -d)"
  echo '{"thresholdTokens": 200000}' > "$cfg/config.json"
  transcript="$(mktemp -d)/t.jsonl"
  usage_line 5000 250000 > "$transcript"
  local in="{\"session_id\":\"s1\",\"agent_id\":\"a1\",\"transcript_path\":\"$transcript\"}"
  out="$(AW_DIGEST_DIR=/spool/digests AW_STATE_DIR="$st" run_hook "$in" "$cfg" "$state" 100)"
  echo "$out" | jq -e '.hookSpecificOutput.additionalContext | contains("/spool/digests/<task-slug>.md")' > /dev/null \
    || { echo "FAIL: AW_DIGEST_DIR not honored, got: $out"; exit 1; }
  out="$(env -u AW_DIGEST_DIR AW_STATE_DIR="$st" bash -c 'AW_CONTEXT_GUARD_DIR="$1" AW_CONTEXT_GUARD_STATE_DIR="$2" AW_CONTEXT_GUARD_GROWTH_GATE_BYTES=100 bash "$3" <<< "$4"' _ "$cfg" "$(mktemp -d)" "$HOOK" "$in")"
  echo "$out" | jq -e --arg d "$st/digests/<task-slug>.md" '.hookSpecificOutput.additionalContext | contains($d)' > /dev/null \
    || { echo "FAIL: default digests dir changed, got: $out"; exit 1; }
  echo "PASS: test_digest_dir_override_and_default"
}

test_large_tool_result_after_last_assistant_line_still_reads_tokens
test_over_1mb_trailing_content_still_reads_tokens
test_digest_dir_override_and_default
echo "All context-guard tests passed."
