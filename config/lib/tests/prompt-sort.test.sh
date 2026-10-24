#!/usr/bin/env bash
# Tests for config/hooks/prompt-sort.sh. Run: bash config/lib/tests/prompt-sort.test.sh
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/prompt-sort.sh"
BASH_BIN="$(command -v bash)"
AW_STATE_DIR="$(mktemp -d)" || exit 2; export AW_STATE_DIR
trap 'find "$AW_STATE_DIR" -delete 2> /dev/null' EXIT

# A fake `judge` on PATH. FAKE_MODE: context | silent | slow | garbage. Logs argv + stdin.
make_fake_judge() {
  local bin log; bin="$(mktemp -d)"; log="$bin/calls.log"
  cat > "$bin/judge" <<'EOF'
#!/usr/bin/env bash
{ echo "ARGS: $*"; echo "ENV_SESSION: ${AW_SESSION_ID:-}"; echo "STDIN: $(cat)"; } >> "$FAKE_LOG"
case "${FAKE_MODE:-context}" in
  context) echo '{"id":"d1","fired":["brief"],"context":"Prompt sorter note: state Goal and Verify."}' ;;
  silent) echo '{"id":"d1","fired":[],"context":""}' ;;
  slow) sleep 5; echo '{"context":"too late"}' ;;
  crash) echo '{"context":"partial"}'; exit 3 ;;
  garbage) echo 'not json at all' ;;
esac
EOF
  chmod +x "$bin/judge"
  echo "$bin"
}

# The hook kills the judge after AW_PROMPT_SORT_BUDGET_MS. Only the slow-judge test measures
# that budget (RUN_BUDGET_MS=600); every other test asserts what the fake judge received or
# printed, so it gets a budget a loaded machine can't exhaust. With 600 ms, a fake that took
# longer than that to start (load average ~45) was killed before it logged its call, and the
# test failed as if the hook had skipped the judge.
run_hook() { # run_hook <bin> <mode> <stdin-json> [extra env...]
  local bin="$1" mode="$2" input="$3"; shift 3
  env PATH="$bin:$PATH" FAKE_LOG="$bin/calls.log" FAKE_MODE="$mode" AW_PROMPT_SORT_BUDGET_MS="${RUN_BUDGET_MS:-30000}" "$@" "$BASH_BIN" "$HOOK" <<< "$input"
}

test_prints_context_and_passes_prompt_and_session_to_judge() {
  local bin out; bin="$(make_fake_judge)"
  out="$(run_hook "$bin" context '{"session_id":"s1","prompt":"fix the login button"}')"
  [ "$out" = "Prompt sorter note: state Goal and Verify." ] || { echo "FAIL: expected the context on stdout, got: $out"; exit 1; }
  grep -q 'ARGS: prompt-sort' "$bin/calls.log" || { echo "FAIL: judge not called with prompt-sort"; exit 1; }
  grep -q 'ENV_SESSION: s1' "$bin/calls.log" || { echo "FAIL: AW_SESSION_ID not set"; exit 1; }
  [ "$(grep '^STDIN:' "$bin/calls.log" | sed 's/^STDIN: //' | jq -r '.prompt + "|" + .sessionId')" = "fix the login button|s1" ] || { echo "FAIL: payload wrong"; exit 1; }
  echo "PASS: test_prints_context_and_passes_prompt_and_session_to_judge"
}

test_silent_when_no_scaffold_fires() {
  local bin out; bin="$(make_fake_judge)"
  out="$(run_hook "$bin" silent '{"session_id":"s1","prompt":"fix the login button"}')"
  [ -z "$out" ] || { echo "FAIL: expected no output, got: $out"; exit 1; }
  echo "PASS: test_silent_when_no_scaffold_fires"
}

test_slow_judge_is_killed_within_budget_with_no_output_rf2() {
  local bin out rc start end; bin="$(make_fake_judge)"
  start=$(date +%s)
  set +e; out="$(RUN_BUDGET_MS=600 run_hook "$bin" slow '{"session_id":"s1","prompt":"fix the login button"}')"; rc=$?; set -e
  end=$(date +%s)
  [ "$rc" -eq 0 ] || { echo "FAIL: RF-2 must exit 0, got $rc"; exit 1; }
  [ -z "$out" ] || { echo "FAIL: RF-2 must print nothing, got: $out"; exit 1; }
  grep -q '"hook":"prompt-sort","cause":"timeout"' "$AW_STATE_DIR/logs/failopen.log" 2>/dev/null || { echo "FAIL: JG-18: a timeout must be logged to failopen.log"; exit 1; }
  [ $((end - start)) -le 3 ] || { echo "FAIL: RF-2 hook took $((end - start))s, budget is 0.6s"; exit 1; }
  echo "PASS: test_slow_judge_is_killed_within_budget_with_no_output_rf2"
}

test_garbage_output_and_missing_judge_fail_open_rf2() {
  local bin out rc; bin="$(make_fake_judge)"
  set +e; out="$(run_hook "$bin" garbage '{"session_id":"s1","prompt":"fix the login button"}')"; rc=$?; set -e
  { [ "$rc" -eq 0 ] && [ -z "$out" ]; } || { echo "FAIL: garbage judge output must be silent, exit 0"; exit 1; }
  local nojudge; nojudge="$(mktemp -d)"
  local tool; for tool in jq grep cat mktemp; do ln -s "$(command -v "$tool")" "$nojudge/$tool"; done
  set +e; out="$(env PATH="$nojudge" "$BASH_BIN" "$HOOK" <<< '{"session_id":"s1","prompt":"fix the login button"}')"; rc=$?; set -e
  { [ "$rc" -eq 0 ] && [ -z "$out" ]; } || { echo "FAIL: a missing judge must be silent, exit 0"; exit 1; }
  echo "PASS: test_garbage_output_and_missing_judge_fail_open_rf2"
}

test_nonzero_exit_of_judge_is_swallowed() {
  local bin out rc; bin="$(make_fake_judge)"
  set +e; out="$(run_hook "$bin" crash '{"session_id":"s1","prompt":"fix the login button"}')"; rc=$?; set -e
  { [ "$rc" -eq 0 ] && [ -z "$out" ]; } || { echo "FAIL: a crashing judge must be silent, exit 0, got rc=$rc out=$out"; exit 1; }
  echo "PASS: test_nonzero_exit_of_judge_is_swallowed"
}

test_machine_text_slash_commands_and_judge_child_never_reach_judge_rf3() {
  local bin p; bin="$(make_fake_judge)"
  for p in '<system-reminder>x</system-reminder>' '<teammate-message teammate_id="a">hi</teammate-message>' \
           'Another Claude session sent a message: done' '[Request interrupted by user]' '/bugFixOrchestrator FRN-1' '/clear'; do
    run_hook "$bin" context "$(jq -nc --arg p "$p" '{session_id:"s1",prompt:$p}')" > /dev/null
  done
  run_hook "$bin" context '{"session_id":"s1","prompt":"fix the login button"}' AW_JUDGE_CHILD=1 > /dev/null
  run_hook "$bin" context '{}' > /dev/null
  run_hook "$bin" context '' > /dev/null
  [ ! -s "$bin/calls.log" ] 2>/dev/null || { echo "FAIL: RF-3 judge was called for machine text: $(cat "$bin/calls.log")"; exit 1; }
  echo "PASS: test_machine_text_slash_commands_and_judge_child_never_reach_judge_rf3"
}

test_pasted_absolute_path_is_still_sorted() {
  local bin; bin="$(make_fake_judge)"
  run_hook "$bin" silent '{"session_id":"s1","prompt":"/Users/joi/app/src/a.ts throws on save"}' > /dev/null
  grep -q 'ARGS: prompt-sort' "$bin/calls.log" || { echo "FAIL: a pasted absolute path was treated as a slash command"; exit 1; }
  echo "PASS: test_pasted_absolute_path_is_still_sorted"
}

test_prints_context_and_passes_prompt_and_session_to_judge
test_silent_when_no_scaffold_fires
test_slow_judge_is_killed_within_budget_with_no_output_rf2
test_garbage_output_and_missing_judge_fail_open_rf2
test_nonzero_exit_of_judge_is_swallowed
test_machine_text_slash_commands_and_judge_child_never_reach_judge_rf3
test_pasted_absolute_path_is_still_sorted
echo "All prompt-sort hook tests passed."
