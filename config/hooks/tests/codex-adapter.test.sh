#!/usr/bin/env bash
# Tests for config/hooks/adapters/codex.sh. Run: bash config/hooks/tests/codex-adapter.test.sh
# Fixture stdin -> expected exit code / stdout / stderr. Never touches $HOME:
# everything runs under a temp HOME and temp state dirs.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOKS="$(cd "$DIR/.." && pwd)"
ADAPTER="$HOOKS/adapters/codex.sh"
FIX="$DIR/fixtures/codex"
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
unset AW_JUDGE_CHILD

# Stubs on PATH: rtk (so rtk-rewrite fires) and judge (no briefs, ask-check = "ask").
BIN="$WORK/bin"; mkdir -p "$BIN"
printf '#!/usr/bin/env bash\nexit 0\n' > "$BIN/rtk"
cat > "$BIN/judge" <<'EOF'
#!/usr/bin/env bash
case "$1 ${2:-}" in
  "brief get") exit 0 ;;
  "ask-check "*) cat > /dev/null; exit 0 ;;
esac
exit 0
EOF
chmod +x "$BIN/rtk" "$BIN/judge"
export PATH="$BIN:$PATH"

run() { # run <fixture-file> <hook-script> [args...] — sets RC, OUT, ERR
  local fx="$1"; shift
  OUT="$(bash "$ADAPTER" "$@" < "$fx" 2> "$WORK/err")"; RC=$?
  ERR="$(cat "$WORK/err")"
}

# The permission decision Codex would read from stdout, or "none" when there is none.
decision() { if [ -n "$OUT" ]; then printf '%s' "$OUT" | jq -r '.hookSpecificOutput.permissionDecision // "none"'; else echo none; fi; }

# --- safety hooks: exit 2 moves the reason to stderr (Codex reads stderr) ---
run "$FIX/pretooluse-bash-rm.json" "$HOOKS/block-destructive.sh"
check "rm -rf: exit 2" "$RC" "2"
check "rm -rf: reason on stderr" "$(printf '%s' "$ERR" | grep -c 'BLOCKED: rm -rf')" "1"
check "rm -rf: nothing on stdout" "$OUT" ""

run "$FIX/pretooluse-bash-ls.json" "$HOOKS/block-destructive.sh"
check "ls: exit 0" "$RC" "0"
check "ls: no output" "$OUT" ""

run "$FIX/pretooluse-exec-argv.json" "$HOOKS/block-destructive.sh"
check "exec_command argv cmd is normalized and blocked" "$RC" "2"
check "exec_command: git reset --hard reason" "$(printf '%s' "$ERR" | grep -c 'git reset --hard')" "1"

run "$FIX/pretooluse-bash-rm.json" "block-destructive.sh" "#" "aw:block-destructive"
check "bare script name resolves next to adapters/, trailing tag argv ignored" "$RC" "2"

# --- rtk-rewrite: updatedInput passes through for {command: string} ---
run "$FIX/pretooluse-bash-git-status.json" "$HOOKS/rtk-rewrite.sh"
check "rtk: exit 0" "$RC" "0"
check "rtk: updatedInput rewritten" "$(printf '%s' "$OUT" | jq -r '.hookSpecificOutput.updatedInput.command')" "rtk git status --short"
run "$FIX/pretooluse-exec-git-status.json" "$HOOKS/rtk-rewrite.sh"
check "rtk: updatedInput dropped for exec_command {cmd} shape" "$(if [ -n "$OUT" ]; then printf '%s' "$OUT" | jq -c '.hookSpecificOutput // {} | has("updatedInput")'; else echo false; fi)" "false"
# Codex rejects permissionDecision "allow" without updatedInput ("PreToolUse hook returned unsupported
# permissionDecision:allow", codex-rs hooks output_parser.rs), so once the rewrite is dropped no bare allow is left (#1585).
check "rtk: exec_command without its rewrite carries no bare allow" "$(decision)" "none"

# --- bare allows from the canonical hooks never reach Codex (#1585) ---
run "$FIX/pretooluse-exec-git-status.json" "$HOOKS/external-write-guard.sh"
check "external-write-guard: exit 0" "$RC" "0"
check "external-write-guard: an ordinary call carries no bare allow" "$(decision)" "none"
run "$FIX/pretooluse-bash-git-status.json" "$HOOKS/rtk-rewrite.sh"
check "rtk: allow with its rewrite still reaches Codex" "$(decision)" "allow"

# --- normalization seen by the canonical script ---
export CAPTURE_FILE="$WORK/capture.json"
run "$FIX/pretooluse-spawn-agent.json" "$CAPTURE_HOOK" "arg1"
check "spawn_agent -> Agent" "$(jq -r '.tool_name' "$CAPTURE_FILE")" "Agent"
check "message -> tool_input.prompt" "$(jq -r '.tool_input.prompt' "$CAPTURE_FILE")" "Goal: fix tests. Acceptance: npm test passes."
check "agent_type -> tool_input.subagent_type" "$(jq -r '.tool_input.subagent_type' "$CAPTURE_FILE")" "worker"
check "task_name -> tool_input.name" "$(jq -r '.tool_input.name' "$CAPTURE_FILE")" "fix_tests"
check "turn_id -> prompt_id" "$(jq -r '.prompt_id' "$CAPTURE_FILE")" "t-9"
check "script args pass through" "$(cat "$CAPTURE_FILE.argv")" "arg1"
check "AW_HOOK_PROVIDER=codex" "$(cat "$CAPTURE_FILE.provider")" "codex"
check "runs from the session cwd" "$(cat "$CAPTURE_FILE.cwd")" "$(cd /tmp && pwd -P)"

run "$FIX/pretooluse-exec-argv.json" "$CAPTURE_HOOK"
check "exec_command -> Bash" "$(jq -r '.tool_name' "$CAPTURE_FILE")" "Bash"
check "argv joined into tool_input.command" "$(jq -r '.tool_input.command' "$CAPTURE_FILE")" "git reset --hard"

run "$FIX/stop-done.json" "$CAPTURE_HOOK"
check "Stop: transcript_path dropped" "$(jq -r '.transcript_path' "$CAPTURE_FILE")" "null"

# Non-JSON stdout on Stop is dropped (Codex rejects plain text there);
# on SessionStart it's kept (becomes developer context).
CAPTURE_OUT="just text" run "$FIX/stop-done.json" "$CAPTURE_HOOK"
check "Stop: plain-text stdout dropped" "$OUT" ""
REPO="$WORK/repo"; mkdir -p "$REPO"; git -C "$REPO" init -q -b feature-x; git -C "$REPO" -c user.email=t@t -c user.name=t commit -q --allow-empty -m init
sed "s|__CWD__|$REPO|" "$FIX/sessionstart.json" > "$WORK/ss.json"
run "$WORK/ss.json" "$HOOKS/git-context.sh"
check "SessionStart git-context: exit 0" "$RC" "0"
check "SessionStart git-context: text passes through, run in session cwd" "$(printf '%s' "$OUT" | grep -c '^Branch: feature-x$')" "1"

# Other exit codes are non-blocking errors -> exit 0.
CAPTURE_RC=1 CAPTURE_ERR="boom" run "$FIX/pretooluse-bash-ls.json" "$CAPTURE_HOOK"
check "exit 1 from the hook is non-blocking" "$RC" "0"
CAPTURE_RC=2 run "$FIX/stop-done.json" "$CAPTURE_HOOK"
check "Stop exit 2 with no reason gets a default continuation" "$ERR" "Continue working on the task."
unset CAPTURE_FILE

# --- done-gate on Stop: reads last_assistant_message ---
run "$FIX/stop-done.json" "$HOOKS/done-gate.sh"
check "done-gate: done claim with no evidence -> exit 2" "$RC" "2"
check "done-gate: asks for proof on stderr" "$(printf '%s' "$ERR" | grep -c 'Show the proof')" "1"
run "$FIX/stop-active.json" "$HOOKS/done-gate.sh"
check "done-gate: stop_hook_active -> exit 0" "$RC" "0"

# --- context-guard on a Codex rollout: token_count usage ---
ROLL="$WORK/rollout.jsonl"; cp "$FIX/rollout.jsonl" "$ROLL"
jq -nc --arg t "$ROLL" '{session_id:"cx-cg",turn_id:"t",transcript_path:$t,cwd:"/tmp",hook_event_name:"PostToolUse",tool_name:"Bash",tool_input:{command:"ls"},tool_response:{}}' > "$WORK/ptu.json"
AW_CONTEXT_GUARD_GROWTH_GATE_BYTES=1 run "$WORK/ptu.json" "$HOOKS/context-guard.sh"
check "context-guard: exit 0" "$RC" "0"
check "context-guard: nudges past the threshold from token_count" \
  "$(printf '%s' "$OUT" | jq -r '.hookSpecificOutput.additionalContext | test("~250000 tokens")')" "true"

# --- fail-open ---
run "$FIX/pretooluse-bash-rm.json" "$WORK/does-not-exist.sh"
check "missing hook script fails open" "$RC" "0"
OUT="$(printf 'not json' | bash "$ADAPTER" "$HOOKS/block-destructive.sh" 2>/dev/null)"; RC=$?
check "garbage stdin fails open" "$RC" "0"
OUT="$(bash "$ADAPTER" < /dev/null 2>/dev/null)"; RC=$?
check "no hook script argument fails open" "$RC" "0"

check "never wrote into the real home" "$(ls -A "$HOME" | wc -l | tr -d ' ')" "0"
exit $fail
