#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/done-gate.sh"
# The hook logs fail-open events under $AW_STATE_DIR (default ~/.agentic-workflow): this suite gets its own, never an inherited one.
AW_STATE_DIR="$(mktemp -d)" || exit 2; export AW_STATE_DIR
trap 'find "$AW_STATE_DIR" -delete 2> /dev/null' EXIT

write_transcript_with_assistant_text() {
  local file="$1" text="$2"
  jq -nc --arg t "$text" '{type:"assistant", message:{content:[{type:"text", text:$t}]}}' > "$file"
}

write_transcript_with_string_content() {
  local file="$1" text="$2"
  jq -nc --arg t "$text" '{type:"assistant", message:{content: $t}}' > "$file"
}

setup_fake_judge() {
  local bin_dir brief_json; bin_dir="$(mktemp -d)"; brief_json="${1:-}"
  cat > "$bin_dir/judge" <<EOF
#!/usr/bin/env bash
if [ "\$1" = "ask-check" ]; then cat >/dev/null; exit 0; fi  # drain stdin like the real judge: else the hook's printf can die of SIGPIPE (141)
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

# RF-3, checked first, literally: stop_hook_active means exit 0 always,
# before even reading the transcript.
test_stop_hook_active_always_exits_0_rf3() {
  local out rc
  set +e
  out="$(bash "$HOOK" <<< '{"stop_hook_active": true, "transcript_path": "/nonexistent-should-never-be-read"}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: RF-3 expected exit 0 for stop_hook_active, got $rc"; exit 1; }
  echo "PASS: test_stop_hook_active_always_exits_0_rf3"
}

test_not_a_done_claim_exits_0() {
  local transcript rc
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "still working on the next step"
  set +e
  bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 for a non-done claim, got $rc"; exit 1; }
  echo "PASS: test_not_a_done_claim_exits_0"
}

test_no_brief_found_falls_back_to_any_evidence_check_rf2() {
  local bin_dir transcript rc
  bin_dir="$(setup_fake_judge)"
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "Done — ran npm test and everything passed."
  set +e
  PATH="$bin_dir:$PATH" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 (evidence present, no brief - RF-2), got $rc"; exit 1; }
  echo "PASS: test_no_brief_found_falls_back_to_any_evidence_check_rf2"
}

test_done_claim_with_no_evidence_at_all_exits_2() {
  local bin_dir transcript rc
  bin_dir="$(setup_fake_judge)"
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "Done, all good."
  set +e
  PATH="$bin_dir:$PATH" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 2 ] || { echo "FAIL: expected exit 2 for a done claim with zero evidence, got $rc"; exit 1; }
  echo "PASS: test_done_claim_with_no_evidence_at_all_exits_2"
}

test_done_claim_with_matching_brief_and_evidence_exits_0() {
  local bin_dir transcript rc
  bin_dir="$(setup_fake_judge '{"acceptanceCriteria":"tests pass"}')"
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "Done — tests pass, ran npm test."
  set +e
  PATH="$bin_dir:$PATH" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 for a claim matching the brief's acceptance criteria, got $rc"; exit 1; }
  echo "PASS: test_done_claim_with_matching_brief_and_evidence_exits_0"
}

test_a_paraphrased_claim_with_evidence_passes_whatever_the_brief_says() {
  local bin_dir transcript rc
  bin_dir="$(setup_fake_judge '{"acceptanceCriteria":"the migration runs cleanly on staging"}')"
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "Done, ran npm test."
  set +e
  PATH="$bin_dir:$PATH" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: JG-05: a claim need not repeat the brief's criteria, got $rc"; exit 1; }
  echo "PASS: test_a_paraphrased_claim_with_evidence_passes_whatever_the_brief_says"
}

test_dotnet_cargo_and_go_runs_count_as_evidence() {
  local c
  for c in 'Done: dotnet test passed, 12 tests.' 'Done. cargo test is green.' 'Done: go test ./... ok.' 'Done: Passed! - Failed: 0, Passed: 40.'; do
    [ "$(claim_rc "$c")" -eq 0 ] || { echo "FAIL: JG-04: '$c' should count as evidence"; exit 1; }
  done
  echo "PASS: test_dotnet_cargo_and_go_runs_count_as_evidence"
}

test_string_shaped_message_content_is_read_too() {
  local bin_dir transcript rc
  bin_dir="$(setup_fake_judge)"
  transcript="$(mktemp)"
  write_transcript_with_string_content "$transcript" "Done, all good."
  set +e
  PATH="$bin_dir:$PATH" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 2 ] || { echo "FAIL: expected exit 2 — string-shaped message.content must be gated too, got $rc"; exit 1; }
  echo "PASS: test_string_shaped_message_content_is_read_too"
}

test_done_claim_survives_over_1mb_of_trailing_hook_attachments() {
  # 2026-09-27 real-session finding: a headless claude -p session's Stop
  # event can carry well over 1MB of trailing hook-attachment JSON
  # (deferred-tool listings, full skill text, etc.) appended AFTER the last
  # assistant line — the old fixed 64KB tail window missed the assistant
  # line entirely, so the hook silently allowed a bare "Done." through with
  # no evidence, unevaluated. Reproduce with >1MB of padding.
  local transcript rc
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "Done."
  head -c 1200000 /dev/zero | tr '\0' 'x' | jq -Rsc '{type:"attachment", attachment:{type:"padding", content:.}}' >> "$transcript"
  set +e
  bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 2 ] || { echo "FAIL: expected exit 2 (no evidence) past >1MB of trailing padding, got $rc"; exit 1; }
  echo "PASS: test_done_claim_survives_over_1mb_of_trailing_hook_attachments"
}

claim_rc() {
  # Runs the hook on one assistant message with a fake judge (no brief), prints the exit code.
  local text="$1" bin transcript rc
  bin="$(setup_fake_judge)"; transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "$text"
  set +e
  PATH="$bin:$PATH" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')" > /dev/null 2>&1
  rc=$?
  set -e
  echo "$rc"
}

test_question_with_claim_word_is_not_a_claim() {
  [ "$(claim_rc 'Should I mark it ready for review now?')" -eq 0 ] || { echo "FAIL: question treated as claim"; exit 1; }
  echo "PASS: test_question_with_claim_word_is_not_a_claim"
}

test_negated_claim_is_not_a_claim() {
  [ "$(claim_rc "I'm not claiming anything is finished; four reviews are still running.")" -eq 0 ] || { echo "FAIL: negation treated as claim"; exit 1; }
  [ "$(claim_rc 'Nothing is done yet.')" -eq 0 ] || { echo "FAIL: 'Nothing is done yet' treated as claim"; exit 1; }
  [ "$(claim_rc "It isn't complete.")" -eq 0 ] || { echo "FAIL: isn't complete treated as claim"; exit 1; }
  echo "PASS: test_negated_claim_is_not_a_claim"
}

test_claim_word_in_table_or_code_is_not_a_claim() {
  local table code
  table=$'| Step | Status |\n|---|---|\n| undraft | ready for review |'
  code=$'Example:\n```\necho done\n```\nWhich option do you want?'
  [ "$(claim_rc "$table")" -eq 0 ] || { echo "FAIL: table cell treated as claim"; exit 1; }
  [ "$(claim_rc "$code")" -eq 0 ] || { echo "FAIL: code fence treated as claim"; exit 1; }
  echo "PASS: test_claim_word_in_table_or_code_is_not_a_claim"
}

test_real_claims_without_evidence_still_block() {
  [ "$(claim_rc 'Done. The refactor is complete.')" -eq 2 ] || { echo "FAIL: real claim not blocked"; exit 1; }
  [ "$(claim_rc "I've finished the migration.")" -eq 2 ] || { echo "FAIL: I've finished not blocked"; exit 1; }
  [ "$(claim_rc 'The PR is ready for review.')" -eq 2 ] || { echo "FAIL: 'is ready for review' not blocked"; exit 1; }
  echo "PASS: test_real_claims_without_evidence_still_block"
}

test_real_claim_with_evidence_passes() {
  [ "$(claim_rc 'Done — ran npm test and all 42 tests passed.')" -eq 0 ] || { echo "FAIL: evidenced claim blocked"; exit 1; }
  echo "PASS: test_real_claim_with_evidence_passes"
}

test_claim_before_a_question_still_blocks() {
  [ "$(claim_rc 'All done. Should I open the PR?')" -eq 2 ] || { echo "FAIL: claim followed by a question not blocked"; exit 1; }
  echo "PASS: test_claim_before_a_question_still_blocks"
}

test_negation_after_claim_word_still_blocks() {
  [ "$(claim_rc 'Done, no issues found.')" -eq 2 ] || { echo "FAIL: 'Done, no issues found.' not blocked"; exit 1; }
  [ "$(claim_rc 'Done — all tests pass, no failures.')" -eq 2 ] || { echo "FAIL: trailing 'no failures' not blocked"; exit 1; }
  [ "$(claim_rc 'The work is finished, not merged.')" -eq 2 ] || { echo "FAIL: 'finished, not merged' not blocked"; exit 1; }
  echo "PASS: test_negation_after_claim_word_still_blocks"
}

test_noun_plus_complete_and_bullet_forms_still_block() {
  [ "$(claim_rc 'Task complete.')" -eq 2 ] || { echo "FAIL: 'Task complete.' not blocked"; exit 1; }
  [ "$(claim_rc 'Implementation complete; tests pass.')" -eq 2 ] || { echo "FAIL: 'Implementation complete; tests pass.' not blocked"; exit 1; }
  [ "$(claim_rc '- Ready for review')" -eq 2 ] || { echo "FAIL: bullet 'Ready for review' not blocked"; exit 1; }
  echo "PASS: test_noun_plus_complete_and_bullet_forms_still_block"
}

test_question_plus_trailing_negation_is_not_a_claim() {
  [ "$(claim_rc 'Should I mark it done? Nothing is finished yet.')" -eq 0 ] || { echo "FAIL: question + negated claim treated as claim"; exit 1; }
  echo "PASS: test_question_plus_trailing_negation_is_not_a_claim"
}

test_negated_predicate_is_not_a_claim() {
  local s
  for s in 'It is not finished.' 'The migration is not complete.' 'This is not complete.' \
           'The task was never finished.' 'Still not finished.' 'No, it is not finished.' \
           'Nothing, in short, is done.' "It's not finished."; do
    [ "$(claim_rc "$s")" -eq 0 ] || { echo "FAIL: negated predicate treated as claim: $s"; exit 1; }
  done
  echo "PASS: test_negated_predicate_is_not_a_claim"
}

test_terse_noun_complete_with_tail_is_a_claim() {
  local s
  for s in 'Refactor complete, tests pass.' 'Task complete, all tests pass.' \
           'Implementation complete, tests pass' 'Refactor finished, 12 tests pass.' \
           'Migration complete, not merged.'; do
    [ "$(claim_rc "$s")" -eq 2 ] || { echo "FAIL: terse noun-complete claim not blocked: $s"; exit 1; }
  done
  echo "PASS: test_terse_noun_complete_with_tail_is_a_claim"
}

test_markdown_decorated_claims_still_block() {
  local c
  while IFS= read -r c; do
    [ "$(claim_rc "$c")" -eq 2 ] || { echo "FAIL: markdown-decorated claim not blocked: $c"; exit 1; }
  done <<'CLAIMS'
**Done.** Tests pass.
**Done** — all three tasks landed.
- **Done**: migration
**Finished.**
**Shipped.**
✅ Done
## Done
> Done.
PR merged.
The fix has been merged.
The migration has been completed.
CLAIMS
  echo "PASS: test_markdown_decorated_claims_still_block"
}

test_decorated_non_claims_stay_inert() {
  [ "$(claim_rc '**Not done yet.**')" -eq 0 ] || { echo "FAIL: '**Not done yet.**' treated as claim"; exit 1; }
  [ "$(claim_rc '`done`')" -eq 0 ] || { echo "FAIL: inline-code done treated as claim"; exit 1; }
  [ "$(claim_rc 'The fix has not been merged.')" -eq 0 ] || { echo "FAIL: 'has not been merged' treated as claim"; exit 1; }
  echo "PASS: test_decorated_non_claims_stay_inert"
}

# Guard tests: each text below is a real claim the moment its guard is removed
# (verified by deleting the guard), so it fails if the guard goes.
test_question_guard_is_load_bearing() {
  local s
  for s in 'Is everything complete?' 'Is it all done?' 'Are we all finished? Tell me.'; do
    [ "$(claim_rc "$s")" -eq 0 ] || { echo "FAIL: question treated as claim: $s"; exit 1; }
  done
  echo "PASS: test_question_guard_is_load_bearing"
}

test_table_guard_is_load_bearing() {
  local table
  table=$'| Step | Status |\n|---|---|\n| lint | all done |\n| tests | everything finished |'
  [ "$(claim_rc "$table")" -eq 0 ] || { echo "FAIL: table row claim word treated as claim"; exit 1; }
  [ "$(claim_rc $'| status | all done |\nThe refactor is complete.')" -eq 2 ] || { echo "FAIL: claim after a table not blocked"; exit 1; }
  echo "PASS: test_table_guard_is_load_bearing"
}

test_fence_guard_is_load_bearing() {
  local s
  s=$'Sample output:\n```\nThe refactor is complete.\n```\nWhich option do you want?'
  [ "$(claim_rc "$s")" -eq 0 ] || { echo "FAIL: claim inside a code fence treated as claim"; exit 1; }
  s=$'Still working on it.\n```\nDone.\n```'
  [ "$(claim_rc "$s")" -eq 0 ] || { echo "FAIL: 'Done.' inside a code fence treated as claim"; exit 1; }
  s=$'```\necho hi\n```\nThe refactor is complete.'
  [ "$(claim_rc "$s")" -eq 2 ] || { echo "FAIL: claim after a closed code fence not blocked"; exit 1; }
  echo "PASS: test_fence_guard_is_load_bearing"
}

test_mid_sentence_markdown_decoration_is_stripped() {
  local s
  for s in 'The fix is _done_.' 'The fix is **done**.' 'The migration is *finished*.'; do
    [ "$(claim_rc "$s")" -eq 2 ] || { echo "FAIL: decorated mid-sentence claim not blocked: $s"; exit 1; }
  done
  [ "$(claim_rc 'It is **not** finished.')" -eq 0 ] || { echo "FAIL: decorated negation treated as claim"; exit 1; }
  echo "PASS: test_mid_sentence_markdown_decoration_is_stripped"
}

test_negation_is_scoped_to_the_claims_clause() {
  local s
  for s in 'I did not finish the tests, but the refactor is complete.' \
           'All tests pass, no failures, and the work is done.' \
           'The old API is not used anymore, and the migration is complete.' \
           'There are no blockers, and everything is finished.' \
           'Nothing is broken, and it is done.' \
           'I did not touch the schema - the refactor is complete.' \
           $'I did not touch the schema \xe2\x80\x94 the refactor is complete.' \
           'Nothing failed, so the work is done.'; do
    [ "$(claim_rc "$s")" -eq 2 ] || { echo "FAIL: independent claim after a negated clause not blocked: $s"; exit 1; }
  done
  echo "PASS: test_negation_is_scoped_to_the_claims_clause"
}

test_negation_in_the_claims_own_clause_still_cancels() {
  local s
  for s in 'Nothing, in short, is done.' 'No, it is not finished.' \
           "I'm not claiming anything is finished; four reviews are still running." \
           'Tests pass, but the refactor is not complete.' \
           'The tests pass, and the migration is not finished.' \
           'Reviews are running - nothing is done.'; do
    [ "$(claim_rc "$s")" -eq 0 ] || { echo "FAIL: negated claim treated as claim: $s"; exit 1; }
  done
  echo "PASS: test_negation_in_the_claims_own_clause_still_cancels"
}

test_first_person_done_forms_are_claims() {
  local s
  for s in "I'm done." "I am done." "We're done." "We are done" "I'm finished." "I am finished with the migration." "We're all done."; do
    [ "$(claim_rc "$s")" -eq 2 ] || { echo "FAIL: first-person completion not blocked: $s"; exit 1; }
  done
  for s in "I'm not done." "I'm done?" "I'm not done yet." "We're not finished." "I am not finished yet."; do
    [ "$(claim_rc "$s")" -eq 0 ] || { echo "FAIL: first-person non-claim treated as claim: $s"; exit 1; }
  done
  echo "PASS: test_first_person_done_forms_are_claims"
}

test_stop_hook_active_always_exits_0_rf3
test_not_a_done_claim_exits_0
test_no_brief_found_falls_back_to_any_evidence_check_rf2
test_done_claim_with_no_evidence_at_all_exits_2
test_done_claim_with_matching_brief_and_evidence_exits_0
test_a_paraphrased_claim_with_evidence_passes_whatever_the_brief_says
test_dotnet_cargo_and_go_runs_count_as_evidence
test_string_shaped_message_content_is_read_too
test_done_claim_survives_over_1mb_of_trailing_hook_attachments
test_question_with_claim_word_is_not_a_claim
test_negated_claim_is_not_a_claim
test_claim_word_in_table_or_code_is_not_a_claim
test_real_claims_without_evidence_still_block
test_real_claim_with_evidence_passes
test_claim_before_a_question_still_blocks
test_negation_after_claim_word_still_blocks
test_noun_plus_complete_and_bullet_forms_still_block
test_question_plus_trailing_negation_is_not_a_claim
test_negated_predicate_is_not_a_claim
test_terse_noun_complete_with_tail_is_a_claim
test_question_guard_is_load_bearing
test_table_guard_is_load_bearing
test_fence_guard_is_load_bearing
test_mid_sentence_markdown_decoration_is_stripped
test_negation_is_scoped_to_the_claims_clause
test_negation_in_the_claims_own_clause_still_cancels
test_first_person_done_forms_are_claims
echo "All done-gate tests passed."

setup_fake_judge_ask_check() {
  local bin_dir rc; bin_dir="$(mktemp -d)"; rc="$1"
  cat > "$bin_dir/judge" <<EOF
#!/usr/bin/env bash
if [ "\$1" = "ask-check" ]; then
  cat > /dev/null
  printf '%s' "\${AW_SESSION_ID:-}" > "$bin_dir/session-id"
  exit $rc
fi
if [ "\$1" = "brief" ] && [ "\$2" = "get" ]; then
  exit 1
fi
EOF
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

test_ask_check_continue_exits_2_and_writes_auto_continued_at() {
  local bin_dir transcript rc sessions_dir
  bin_dir="$(setup_fake_judge_ask_check 2)"
  sessions_dir="$(mktemp -d)"
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "still working on the next step"
  set +e
  local err_file
  err_file="$(mktemp)"
  PATH="$bin_dir:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')" 2> "$err_file"
  rc=$?
  set -e
  [ "$rc" -eq 2 ] || { echo "FAIL: expected exit 2 when ask-check says continue, got $rc"; exit 1; }
  [ "$(cat "$err_file")" = "Next step already authorized by the brief or plan — continuing without asking." ] || { echo "FAIL: expected continue reason on stderr, got '$(cat "$err_file")'"; exit 1; }
  jq -e '.auto_continued_at' "$sessions_dir/s1.json" > /dev/null || { echo "FAIL: expected auto_continued_at to be written"; exit 1; }
  [ "$(cat "$bin_dir/session-id")" = "s1" ] || { echo "FAIL: ask-check must see AW_SESSION_ID=s1, got '$(cat "$bin_dir/session-id")'"; exit 1; }
  echo "PASS: test_ask_check_continue_exits_2_and_writes_auto_continued_at"
}

test_ask_check_ask_exits_0() {
  local bin_dir transcript rc sessions_dir
  bin_dir="$(setup_fake_judge_ask_check 0)"
  sessions_dir="$(mktemp -d)"
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "still working on the next step"
  set +e
  PATH="$bin_dir:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 when ask-check says ask, got $rc"; exit 1; }
  echo "PASS: test_ask_check_ask_exits_0"
}

test_ask_check_continue_exits_2_and_writes_auto_continued_at
test_ask_check_ask_exits_0

test_judge_missing_from_path_on_done_claim_still_fails_open() {
  local transcript rc empty_path
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "Done, all good."
  empty_path="/usr/bin:/bin"  # keeps jq/coreutils, excludes judge
  set +e
  PATH="$empty_path" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 2 ] || { echo "FAIL: expected exit 2 (RF-2, no evidence) — must never crash or exit >2 when judge is missing entirely, got $rc"; exit 1; }
  echo "PASS: test_judge_missing_from_path_on_done_claim_still_fails_open"
}

test_judge_nonzero_garbage_stdout_on_done_claim_still_fails_open() {
  local bin_dir transcript rc
  bin_dir="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
echo 'not valid json {{{'
exit 1
EOF
  chmod +x "$bin_dir/judge"
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "Done — tests pass, ran npm test."
  set +e
  PATH="$bin_dir:$PATH" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: garbage/nonzero judge output must not block the stop (evidence is present in the claim itself), got $rc"; exit 1; }
  echo "PASS: test_judge_nonzero_garbage_stdout_on_done_claim_still_fails_open"
}

test_judge_missing_from_path_on_non_done_claim_still_fails_open() {
  local transcript rc empty_path
  transcript="$(mktemp)"
  write_transcript_with_assistant_text "$transcript" "still working on the next step"
  empty_path="/usr/bin:/bin"
  set +e
  PATH="$empty_path" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')"
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 (ask-check unreachable -> fail safe to a real stop, never auto-continue), got $rc"; exit 1; }
  echo "PASS: test_judge_missing_from_path_on_non_done_claim_still_fails_open"
}

write_ui_requirement() { # write_ui_requirement <sessions-dir> <sid> <json>
  mkdir -p "$1"; printf '%s' "$3" > "$1/$2.sort.json"
}

test_ui_requirement_blocks_a_done_claim_without_ui_evidence() {
  local bin transcript sessions rc err
  bin="$(setup_fake_judge)"; transcript="$(mktemp)"; sessions="$(mktemp -d)"
  write_transcript_with_assistant_text "$transcript" "Done — ran npm test, everything passed."
  write_ui_requirement "$sessions" s1 '{"prompts":1,"lastFired":{},"requirements":{"uiEvidence":true}}'
  set +e
  err="$(PATH="$bin:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')" 2>&1 >/dev/null)"
  rc=$?
  set -e
  [ "$rc" -eq 2 ] || { echo "FAIL: expected exit 2 without UI evidence, got $rc"; exit 1; }
  echo "$err" | has -i "UI evidence" || { echo "FAIL: stderr must say UI evidence is required, got: $err"; exit 1; }
  jq -e '.requirements.uiEvidence' "$sessions/s1.sort.json" > /dev/null || { echo "FAIL: the requirement must stay until evidence is shown"; exit 1; }
  echo "PASS: test_ui_requirement_blocks_a_done_claim_without_ui_evidence"
}

test_ui_requirement_is_satisfied_once_and_then_cleared() {
  local bin transcript sessions rc
  bin="$(setup_fake_judge)"; transcript="$(mktemp)"; sessions="$(mktemp -d)"
  write_transcript_with_assistant_text "$transcript" "Done — screenshot saved at /tmp/after.png, and npm test passed."
  write_ui_requirement "$sessions" s1 '{"prompts":1,"lastFired":{},"requirements":{"uiEvidence":true}}'
  set +e
  PATH="$bin:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')" > /dev/null 2>&1
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: expected exit 0 when a screenshot is mentioned, got $rc"; exit 1; }
  [ "$(jq -r '.requirements.uiEvidence // "gone"' "$sessions/s1.sort.json")" = "gone" ] || { echo "FAIL: a satisfied requirement must be cleared"; exit 1; }
  echo "PASS: test_ui_requirement_is_satisfied_once_and_then_cleared"
}

test_ui_requirement_never_affects_a_non_done_claim_or_a_stop_hook_active_rerun() {
  local bin transcript sessions rc
  bin="$(setup_fake_judge)"; transcript="$(mktemp)"; sessions="$(mktemp -d)"
  write_ui_requirement "$sessions" s1 '{"prompts":1,"lastFired":{},"requirements":{"uiEvidence":true}}'
  write_transcript_with_assistant_text "$transcript" "still working on the layout"
  set +e
  PATH="$bin:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')" > /dev/null 2>&1
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: a non-done claim must not be blocked by the UI requirement, got $rc"; exit 1; }
  write_transcript_with_assistant_text "$transcript" "Done — all green."
  set +e
  PATH="$bin:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1", stop_hook_active:true}')" > /dev/null 2>&1
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: stop_hook_active must always exit 0 (no loop), got $rc"; exit 1; }
  echo "PASS: test_ui_requirement_never_affects_a_non_done_claim_or_a_stop_hook_active_rerun"
}

test_hostile_session_id_never_reads_outside_the_sessions_dir_rf4() {
  local bin transcript sessions rc outer
  bin="$(setup_fake_judge)"; transcript="$(mktemp)"; outer="$(mktemp -d)"; sessions="$outer/sessions"
  mkdir -p "$sessions"
  printf '%s' '{"requirements":{"uiEvidence":true}}' > "$outer/evil.sort.json"
  write_transcript_with_assistant_text "$transcript" "Done — ran npm test, everything passed."
  set +e
  PATH="$bin:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"../evil"}')" > /dev/null 2>&1
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: RF-4 a path-traversal session id must be ignored by the UI check, got exit $rc"; exit 1; }
  echo "PASS: test_hostile_session_id_never_reads_outside_the_sessions_dir_rf4"
}

test_absent_or_corrupt_sort_file_changes_nothing() {
  local bin transcript sessions rc
  bin="$(setup_fake_judge)"; transcript="$(mktemp)"; sessions="$(mktemp -d)"
  write_transcript_with_assistant_text "$transcript" "Done — ran npm test, everything passed."
  set +e
  PATH="$bin:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')" > /dev/null 2>&1
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: no sort file must leave done-gate unchanged, got $rc"; exit 1; }
  write_ui_requirement "$sessions" s1 'not json'
  set +e
  PATH="$bin:$PATH" AW_JUDGE_SESSIONS_DIR="$sessions" bash "$HOOK" <<< "$(jq -nc --arg t "$transcript" '{transcript_path:$t, session_id:"s1"}')" > /dev/null 2>&1
  rc=$?
  set -e
  [ "$rc" -eq 0 ] || { echo "FAIL: a corrupt sort file must be ignored, got $rc"; exit 1; }
  echo "PASS: test_absent_or_corrupt_sort_file_changes_nothing"
}

test_markdown_decorated_claims_still_block
test_decorated_non_claims_stay_inert
test_judge_missing_from_path_on_done_claim_still_fails_open
test_judge_nonzero_garbage_stdout_on_done_claim_still_fails_open
test_judge_missing_from_path_on_non_done_claim_still_fails_open
test_ui_requirement_blocks_a_done_claim_without_ui_evidence
test_ui_requirement_is_satisfied_once_and_then_cleared
test_ui_requirement_never_affects_a_non_done_claim_or_a_stop_hook_active_rerun
test_hostile_session_id_never_reads_outside_the_sessions_dir_rf4
test_absent_or_corrupt_sort_file_changes_nothing
