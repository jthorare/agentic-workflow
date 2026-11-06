#!/usr/bin/env bash
# Tests for config/hooks/adapters/cursor.sh. Run: bash config/hooks/tests/cursor-adapter.test.sh
# Fixture stdin -> expected Cursor JSON output. The adapter must ALWAYS exit 0
# with valid JSON (Cursor blocks a permission hook's action on invalid JSON).
# Never touches $HOME: everything runs under a temp HOME and temp state dirs.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOKS="$(cd "$DIR/.." && pwd)"
ADAPTER="$HOOKS/adapters/cursor.sh"
FIX="$DIR/fixtures/cursor"
CAPTURE_HOOK="$DIR/fixtures/capture-hook.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
export AW_STATE_DIR="$WORK/state"
unset AW_JUDGE_CHILD CURSOR_TRANSCRIPT_PATH CURSOR_PROJECT_DIR

BIN="$WORK/bin"; mkdir -p "$BIN"
printf '#!/usr/bin/env bash\nexit 0\n' > "$BIN/rtk"
cat > "$BIN/judge" <<'EOF'
#!/usr/bin/env bash
case "$1 ${2:-}" in
  "brief get") exit 0 ;;
  "brief-scope "*) cat > /dev/null; echo '{"decision":"missing"}'; exit 0 ;;
  "ask-check "*) cat > /dev/null; exit 0 ;;
esac
exit 0
EOF
chmod +x "$BIN/rtk" "$BIN/judge"
export PATH="$BIN:$PATH"

run() { # run <fixture-file> <adapter-args...> — sets RC, OUT
  local fx="$1"; shift
  OUT="$(bash "$ADAPTER" "$@" < "$fx" 2> "$WORK/err")"; RC=$?
}

# --- beforeShellExecution: safety hooks ---
run "$FIX/before-shell-rm.json" "$HOOKS/block-destructive.sh"
check "rm -rf: exit 0 (decision is in JSON)" "$RC" "0"
check "rm -rf: permission deny" "$(printf '%s' "$OUT" | jq -r '.permission')" "deny"
check "rm -rf: agent_message carries the reason" "$(printf '%s' "$OUT" | jq -r '.agent_message | test("BLOCKED: rm -rf")')" "true"
check "rm -rf: user_message carries the reason" "$(printf '%s' "$OUT" | jq -r '.user_message | test("BLOCKED: rm -rf")')" "true"
run "$FIX/before-shell-ls.json" "$HOOKS/block-destructive.sh"
check "ls: permission allow" "$OUT" '{"permission":"allow"}'
run "$FIX/before-shell-rm.json" "block-destructive.sh" "#" "aw:block-destructive"
check "bare script name + trailing tag argv" "$(printf '%s' "$OUT" | jq -r '.permission')" "deny"

# --- preToolUse(Shell): rtk rewrite -> updated_input merged over the original ---
run "$FIX/pretooluse-shell-git-status.json" "$HOOKS/rtk-rewrite.sh"
check "rtk: allow" "$(printf '%s' "$OUT" | jq -r '.permission')" "allow"
check "rtk: updated_input keeps other fields, rewrites command" \
  "$(printf '%s' "$OUT" | jq -c '.updated_input')" '{"command":"rtk git status","working_directory":"/tmp"}'

# --- normalization seen by the canonical script ---
export CAPTURE_FILE="$WORK/capture.json"
run "$FIX/before-shell-ls.json" "$CAPTURE_HOOK"
check "beforeShellExecution -> PreToolUse" "$(jq -r '.hook_event_name' "$CAPTURE_FILE")" "PreToolUse"
check "beforeShellExecution -> tool_name Bash" "$(jq -r '.tool_name' "$CAPTURE_FILE")" "Bash"
check "command -> tool_input.command" "$(jq -r '.tool_input.command' "$CAPTURE_FILE")" "ls"
check "conversation_id -> session_id" "$(jq -r '.session_id' "$CAPTURE_FILE")" "cu-1"
check "generation_id -> prompt_id" "$(jq -r '.prompt_id' "$CAPTURE_FILE")" "g-1"
check "AW_HOOK_PROVIDER=cursor" "$(cat "$CAPTURE_FILE.provider")" "cursor"
run "$FIX/pretooluse-shell-git-status.json" "$CAPTURE_HOOK"
check "preToolUse Shell -> Bash" "$(jq -r '.tool_name' "$CAPTURE_FILE")" "Bash"
run "$FIX/before-mcp-linear.json" "$CAPTURE_HOOK"
check "beforeMCPExecution -> mcp__<server>__<tool>" "$(jq -r '.tool_name' "$CAPTURE_FILE")" "mcp__linear__save_issue"
check "MCP tool_input JSON string is parsed" "$(jq -r '.tool_input.title' "$CAPTURE_FILE")" "x"
run "$FIX/subagent-start.json" "$CAPTURE_HOOK"
check "subagentStart -> SubagentStart" "$(jq -r '.hook_event_name' "$CAPTURE_FILE")" "SubagentStart"
check "subagentStart: tool_call_id -> tool_use_id" "$(jq -r '.tool_use_id' "$CAPTURE_FILE")" "tc-1"
check "subagentStart: task -> tool_input.prompt" "$(jq -r '.tool_input.prompt' "$CAPTURE_FILE")" "Fix the flaky test
Acceptance: vitest passes"
check "subagentStart: description is the task's first line" "$(jq -r '.tool_input.description' "$CAPTURE_FILE")" "Fix the flaky test"
check "subagentStart: subagent_id -> agent_id" "$(jq -r '.agent_id' "$CAPTURE_FILE")" "sa-1"
check "workspace_roots[0] -> cwd (user hooks run from ~/.cursor)" "$(cat "$CAPTURE_FILE.cwd")" "$(cd /tmp && pwd -P)"

# A canonical JSON deny (scope-gate style) becomes permission deny.
CAPTURE_OUT='{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"no brief"}}' \
  run "$FIX/subagent-start.json" "$CAPTURE_HOOK"
check "JSON deny -> permission deny with reason" "$(printf '%s' "$OUT" | jq -c '{permission, agent_message}')" '{"permission":"deny","agent_message":"no brief"}'
unset CAPTURE_FILE

# --- scope-gate on subagentStart (judge says the brief is missing) ---
run "$FIX/subagent-start.json" "$HOOKS/scope-gate.sh"
check "scope-gate: missing brief denies the subagent" "$(printf '%s' "$OUT" | jq -r '.permission')" "deny"

# --- sessionStart: stdout text -> additional_context ---
REPO="$WORK/repo"; mkdir -p "$REPO"; git -C "$REPO" init -q -b feature-y; git -C "$REPO" -c user.email=t@t -c user.name=t commit -q --allow-empty -m init
sed "s|__CWD__|$REPO|" "$FIX/session-start.json" > "$WORK/ss.json"
run "$WORK/ss.json" "$HOOKS/git-context.sh"
check "sessionStart: additional_context from git-context" \
  "$(printf '%s' "$OUT" | jq -r '.additional_context' | grep -c '^Branch: feature-y$')" "1"
CAPTURE_FILE="$WORK/c2.json" run "$WORK/ss.json" "$CAPTURE_HOOK"
check "sessionStart: no output -> {}" "$OUT" "{}"

# --- stop: done-gate via transcript's last assistant text -> followup_message ---
cp "$FIX/transcript.jsonl" "$WORK/transcript.jsonl"
sed "s|__TRANSCRIPT__|$WORK/transcript.jsonl|" "$FIX/stop.json" > "$WORK/stop.json"
sed "s|__TRANSCRIPT__|$WORK/transcript.jsonl|" "$FIX/stop-looped.json" > "$WORK/stop-looped.json"
CAPTURE_FILE="$WORK/c3.json" run "$WORK/stop.json" "$CAPTURE_HOOK"
check "stop: last assistant text extracted" "$(jq -r '.last_assistant_message' "$WORK/c3.json")" "All done."
check "stop: loop_count 0 -> stop_hook_active false" "$(jq -r '.stop_hook_active' "$WORK/c3.json")" "false"
run "$WORK/stop.json" "$HOOKS/done-gate.sh"
check "done-gate: followup_message asks for proof" "$(printf '%s' "$OUT" | jq -r '.followup_message | test("Show the proof")')" "true"
run "$WORK/stop-looped.json" "$HOOKS/done-gate.sh"
check "done-gate: loop_count > 0 -> no follow-up (never loops)" "$OUT" "{}"

# --- beforeSubmitPrompt: turn-origin ---
run "$FIX/before-submit-prompt.json" "$HOOKS/turn-origin.sh"
check "beforeSubmitPrompt: continue true" "$OUT" '{"continue":true}'

# --- beforeMCPExecution: external-write-guard after an auto-continue ---
mkdir -p "$AW_STATE_DIR/judge/sessions"
echo '{"auto_continued_at":"2026-09-28T00:00:00Z"}' > "$AW_STATE_DIR/judge/sessions/cu-auto.json"
run "$FIX/before-mcp-linear.json" "$HOOKS/external-write-guard.sh"
check "external-write-guard: Linear save after auto-continue is denied" "$(printf '%s' "$OUT" | jq -r '.permission')" "deny"
rm "$AW_STATE_DIR/judge/sessions/cu-auto.json"
run "$FIX/before-mcp-linear.json" "$HOOKS/external-write-guard.sh"
check "external-write-guard: allowed on a user turn" "$(printf '%s' "$OUT" | jq -r '.permission')" "allow"

# --- --raw probe logging ---
export AW_PROBE_DIR="$WORK/probe"
run "$WORK/stop.json" --raw --event stop "$HOOKS/probe-log.sh" stop cursor
check "probe --raw: stop output is {}" "$OUT" "{}"
check "probe --raw: raw stdin logged under probe/cursor/" "$(jq -r '.input.conversation_id' "$AW_PROBE_DIR/cursor/stop.jsonl")" "cu-stop"
run "$FIX/subagent-start.json" --raw --event subagentStart "$HOOKS/probe-log.sh" subagentStart cursor
check "probe --raw: permission hook answered allow" "$OUT" '{"permission":"allow"}'
unset AW_PROBE_DIR

# --- fail-open: always valid JSON ---
run "$FIX/before-shell-rm.json" "$WORK/does-not-exist.sh"
check "missing hook script -> allow" "$OUT" '{"permission":"allow"}'
OUT="$(printf 'not json' | bash "$ADAPTER" --event beforeShellExecution "$HOOKS/block-destructive.sh" 2>/dev/null)"; RC=$?
check "garbage stdin -> exit 0" "$RC" "0"
check "garbage stdin -> allow" "$OUT" '{"permission":"allow"}'
OUT="$(bash "$ADAPTER" --event beforeSubmitPrompt < /dev/null 2>/dev/null)"; RC=$?
check "no hook script argument -> default output" "$OUT" '{"continue":true}'

# --- #2123: a Cursor lane's reply comes from afterAgentResponse (its stop payload carries no text) ---
# The real adapter and the real meta-lane-gate.sh behind it, against a stub aw-meta (as config/hooks/tests/meta-hooks.test.sh).
CALLS="$WORK/calls"; STDIN_SEEN="$WORK/stdin"; export CALLS STDIN_SEEN
cat > "$WORK/aw-meta" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$CALLS"
[ "$1" = lane-reply ] && cat > "$STDIN_SEEN"
exit 0
EOF
chmod +x "$WORK/aw-meta"
GATE="$HOOKS/meta-lane-gate.sh"
LS="$AW_STATE_DIR/meta/lane-sessions"
reset_lane() { find "$LS" -mindepth 1 -delete 2> /dev/null; : > "$CALLS"; : > "$STDIN_SEEN"; }
# this shell plays scripts/meta-lane.sh: the gate acts only for the lane's own agent
lane_cursor() { # <payload-file> — through cursor.sh and meta-lane-gate.sh as lane api of team t1
  OUT="$(AW_META_BIN="$WORK/aw-meta" AW_META_LANE_PID=$$ AW_META_LANE_AGENT=cursor AW_META_LANE_PROC=cursor-agent \
    AW_META_TEAM=t1 AW_META_LANE=api bash "$ADAPTER" "$GATE" < "$1" 2> "$WORK/err")"; RC=$?
}
AAR_TEXT=$'Rebased.\nREPLY TO META: cursor reply\nsecond line'
jq -nc --arg t "$AAR_TEXT" '{hook_event_name:"afterAgentResponse",conversation_id:"c-9",generation_id:"g-9",text:$t}' > "$WORK/aar.json"
reset_lane
lane_cursor "$WORK/aar.json"
check "afterAgentResponse in a lane: exit 0 and the adapter prints {}" "$RC:$OUT" "0:{}"
check "afterAgentResponse: lane-reply gets the reply text on stdin, lane-stop-check is not called" "$(paste -sd'|' "$CALLS"):$(cat "$STDIN_SEEN")" "lane-reply --stdin:cursor reply
second line"
CAPTURE_FILE="$WORK/c-aar.json" run "$WORK/aar.json" "$CAPTURE_HOOK"
check "afterAgentResponse -> AfterAgentResponse" "$(jq -r '.hook_event_name' "$WORK/c-aar.json")" "AfterAgentResponse"
check "afterAgentResponse: last_assistant_message is .text" "$(jq -r '.last_assistant_message' "$WORK/c-aar.json")" "$AAR_TEXT"
check "afterAgentResponse: conversation_id -> session_id" "$(jq -r '.session_id' "$WORK/c-aar.json")" "c-9"
check "afterAgentResponse: the adapter prints {}" "$OUT" "{}"
# a transcript, even one with another reply, is not read for this event
echo '{"role":"assistant","message":{"content":[{"type":"text","text":"REPLY TO META: from the transcript"}]}}' > "$WORK/aar-tr.jsonl"
jq -nc --arg t "no marker" --arg tp "$WORK/aar-tr.jsonl" '{hook_event_name:"afterAgentResponse",conversation_id:"c-9",text:$t,transcript_path:$tp}' > "$WORK/aar2.json"
CAPTURE_FILE="$WORK/c-aar2.json" run "$WORK/aar2.json" "$CAPTURE_HOOK"
check "afterAgentResponse: no transcript read (last_assistant_message stays .text)" "$(jq -r '.last_assistant_message' "$WORK/c-aar2.json")" "no marker"

# the stop that follows: its transcript's last assistant line carries text AND thinking parts; only the text is the reply
printf '%s\n' '{"role":"assistant","message":{"content":[{"type":"text","text":"Done.\nREPLY TO META: only the text"},{"type":"thinking","thinking":"I considered Y"}]}}' > "$WORK/tr-think.jsonl"
jq -nc --arg tp "$WORK/tr-think.jsonl" '{hook_event_name:"stop",conversation_id:"c-9",loop_count:0,transcript_path:$tp}' > "$WORK/stop-think.json"
CAPTURE_FILE="$WORK/c-think.json" run "$WORK/stop-think.json" "$CAPTURE_HOOK"
check "stop: a transcript line with text and thinking parts yields only the text" "$(jq -r '.last_assistant_message' "$WORK/c-think.json")" $'Done.\nREPLY TO META: only the text'
printf '%s\n' '{"role":"assistant","message":{"content":[{"type":"text","text":"Done.\nREPLY TO META: only the text"},{"type":"thinking","thinking":"REPLY TO META: not this one"}]}}' > "$WORK/tr-think2.jsonl"
jq -nc --arg tp "$WORK/tr-think2.jsonl" '{hook_event_name:"stop",conversation_id:"c-9",loop_count:0,transcript_path:$tp}' > "$WORK/stop-think2.json"
reset_lane
lane_cursor "$WORK/stop-think2.json"
check "stop in a lane: only the text part is recorded, never the thinking" "$(paste -sd'|' "$CALLS"):$(cat "$STDIN_SEEN")" "lane-reply --stdin|lane-stop-check:only the text"

# the existing path stays: a stop whose transcript's last assistant line has a REPLY TO META line still records
printf '%s\n' '{"role":"assistant","message":{"content":[{"type":"text","text":"Done.\nREPLY TO META: via the transcript"}]}}' > "$WORK/tr-plain.jsonl"
jq -nc --arg tp "$WORK/tr-plain.jsonl" '{hook_event_name:"stop",conversation_id:"c-9",loop_count:0,transcript_path:$tp}' > "$WORK/stop-plain.json"
reset_lane
lane_cursor "$WORK/stop-plain.json"
check "stop with a REPLY TO META line in the transcript still records it" "$(paste -sd'|' "$CALLS"):$(cat "$STDIN_SEEN")" "lane-reply --stdin|lane-stop-check:via the transcript"
# afterAgentResponse then stop with the same text: recorded once
reset_lane
jq -nc --arg t $'Done.\nREPLY TO META: via the transcript' '{hook_event_name:"afterAgentResponse",conversation_id:"c-9",text:$t}' > "$WORK/aar3.json"
lane_cursor "$WORK/aar3.json"; lane_cursor "$WORK/stop-plain.json"
check "afterAgentResponse then stop with the same text: the reply is recorded once" "$(grep -c '^lane-reply' "$CALLS")" "1"
reset_lane

check "never wrote into the real home" "$(ls -A "$HOME" | wc -l | tr -d ' ')" "0"
exit $fail
