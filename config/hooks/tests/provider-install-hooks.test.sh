#!/usr/bin/env bash
# Tests for providers/<name>/install-hooks.sh and the --provider flag on the
# scripts/install-*.sh lever installers and scripts/probe.sh.
# Run: bash config/hooks/tests/provider-install-hooks.test.sh
# Everything runs under a temp HOME — never the real ~/.claude, ~/.codex, ~/.cursor.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
unset AW_STATE_DIR AW_HOOKS_DIR CODEX_HOOKS_FILE CODEX_HOME CURSOR_HOOKS_FILE CLAUDE_SETTINGS_FILE CLAUDE_HOOKS_DIR CLAUDE_DIR SETTINGS_FILE TOOLKIT_DIR AW_DRY_RUN
BIN="$WORK/bin"; mkdir -p "$BIN"
printf '#!/usr/bin/env bash\nexit 0\n' > "$BIN/judge"; chmod +x "$BIN/judge"
export PATH="$BIN:$PATH"
q() { "$@" > /dev/null 2>&1; }

CODEX_FILE="$HOME/.codex/hooks.json"
CURSOR_FILE="$HOME/.cursor/hooks.json"
AWH="$HOME/.agentic-workflow/hooks"

# ---------------- codex base install ----------------
mkdir -p "$HOME/.codex"
echo '{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"/usr/local/bin/theirs.sh"}]}]}}' > "$CODEX_FILE"
q bash "$ROOT/providers/codex/install-hooks.sh"
check "codex: scripts staged in ~/.agentic-workflow/hooks" "$([ -x "$AWH/block-destructive.sh" ] && [ -x "$AWH/adapters/codex.sh" ] && [ -f "$AWH/lib/locks.sh" ] && [ -f "$AWH/lib/failopen.sh" ] && echo y)" "y"
check "codex: 4 owned PreToolUse commands" "$(jq '[.hooks.PreToolUse[].hooks[].command | select(test("# aw:"))] | length' "$CODEX_FILE")" "4"
check "codex: commands go through the adapter" \
  "$(jq -r '.hooks.PreToolUse[].hooks[].command | select(endswith("# aw:block-destructive"))' "$CODEX_FILE")" \
  "$AWH/adapters/codex.sh $AWH/block-destructive.sh # aw:block-destructive"
check "codex: SessionStart git-context + prism-context" "$(jq '[.hooks.SessionStart[].hooks[].command | select(test("# aw:(git|prism)-context$"))] | length' "$CODEX_FILE")" "2"
check "codex: foreign hook kept first" "$(jq -r '.hooks.PreToolUse[0].hooks[0].command' "$CODEX_FILE")" "/usr/local/bin/theirs.sh"
snap="$(jq -Sc . "$CODEX_FILE")"
q bash "$ROOT/providers/codex/install-hooks.sh"
check "codex: reinstall is idempotent" "$(jq -Sc . "$CODEX_FILE")" "$snap"
# The installed entry really works end to end.
cmd="$(jq -r '.hooks.PreToolUse[].hooks[].command | select(endswith("# aw:block-destructive"))' "$CODEX_FILE")"
echo '{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"git clean -fd"},"cwd":"/tmp"}' | sh -c "$cmd" > /dev/null 2>&1; rc=$?
check "codex: installed command blocks git clean -f (exit 2)" "$rc" "2"
q bash "$ROOT/providers/codex/install-hooks.sh" --uninstall
check "codex: uninstall leaves only the foreign hook" "$(jq -c . "$CODEX_FILE")" '{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"/usr/local/bin/theirs.sh"}]}]}}'

# ---------------- cursor base install ----------------
mkdir -p "$HOME/.cursor"
echo '{"version":1,"hooks":{"beforeShellExecution":[{"command":"./theirs.sh","matcher":"curl"}]}}' > "$CURSOR_FILE"
q bash "$ROOT/providers/cursor/install-hooks.sh"
check "cursor: 3 owned beforeShellExecution entries + foreign" "$(jq '.hooks.beforeShellExecution | length' "$CURSOR_FILE")" "4"
check "cursor: flat entry shape" \
  "$(jq -c '.hooks.beforeShellExecution[] | select(.command | endswith("# aw:detect-secrets"))' "$CURSOR_FILE")" \
  "$(jq -nc --arg c "$AWH/adapters/cursor.sh $AWH/detect-secrets.sh # aw:detect-secrets" '{command:$c}')"
check "cursor: rtk on preToolUse(Shell)" "$(jq -r '.hooks.preToolUse[] | select(.command | endswith("# aw:rtk-rewrite")) | .matcher' "$CURSOR_FILE")" "Shell"
check "cursor: sessionStart context hooks" "$(jq '.hooks.sessionStart | length' "$CURSOR_FILE")" "2"
check "cursor: version kept at 1" "$(jq '.version' "$CURSOR_FILE")" "1"
snap="$(jq -Sc . "$CURSOR_FILE")"
q bash "$ROOT/providers/cursor/install-hooks.sh"
check "cursor: reinstall is idempotent" "$(jq -Sc . "$CURSOR_FILE")" "$snap"
cmd="$(jq -r '.hooks.beforeShellExecution[] | select(.command | endswith("# aw:block-push-main")) | .command' "$CURSOR_FILE")"
out="$(echo '{"hook_event_name":"beforeShellExecution","command":"git push origin main","cwd":"/tmp"}' | sh -c "$cmd" 2>/dev/null)"
check "cursor: installed command denies push to main" "$(printf '%s' "$out" | jq -r '.permission')" "deny"
q bash "$ROOT/providers/cursor/install-hooks.sh" --uninstall
check "cursor: uninstall leaves only the foreign hook" "$(jq -c . "$CURSOR_FILE")" '{"version":1,"hooks":{"beforeShellExecution":[{"command":"./theirs.sh","matcher":"curl"}]}}'

# ---------------- claude base install (moved from setup.sh) ----------------
CD="$WORK/claude"; mkdir -p "$CD"; echo '{}' > "$CD/settings.json"
q env CLAUDE_DIR="$CD" bash "$ROOT/providers/claude/install-hooks.sh"
check "claude: Bash safety group" "$(jq -c '[.hooks.PreToolUse[] | select(.matcher=="Bash") | .hooks[].command]' "$CD/settings.json")" \
  '["~/.claude/hooks/block-destructive.sh","~/.claude/hooks/block-push-main.sh","~/.claude/hooks/detect-secrets.sh","~/.claude/hooks/rtk-rewrite.sh"]'
check "claude: SessionStart git-context then prism-context" "$(jq -c '[.hooks.SessionStart[].hooks[].command]' "$CD/settings.json")" \
  '["~/.claude/hooks/git-context.sh","~/.claude/hooks/prism-context.sh"]'
check "claude: scripts copied to CLAUDE_DIR/hooks" "$([ -x "$CD/hooks/done-gate.sh" ] && echo y)" "y"
check "claude: HOOK-FAILOPEN logger installed next to the hooks" "$([ -f "$CD/hooks/lib/failopen.sh" ] && echo y)" "y"
snap="$(jq -Sc . "$CD/settings.json")"
q env CLAUDE_DIR="$CD" bash "$ROOT/providers/claude/install-hooks.sh"
check "claude: reinstall is idempotent" "$(jq -Sc . "$CD/settings.json")" "$snap"
check "claude: bridge-context.sh is not installed" "$([ -e "$CD/hooks/bridge-context.sh" ] && echo present || echo absent)" "absent"
q env CLAUDE_DIR="$CD" bash "$ROOT/providers/claude/install-hooks.sh" --uninstall
check "claude: uninstall removes the entries it wrote" "$(jq -c . "$CD/settings.json")" '{"hooks":{}}'

# Legacy bridge-context entry from an older install is removed on reinstall,
# including when it shares a group with a foreign command.
CL="$WORK/claude-legacy"; mkdir -p "$CL/hooks"
printf '#!/usr/bin/env bash\n' > "$CL/hooks/bridge-context.sh"
echo '{"hooks":{"SessionStart":[{"hooks":[{"type":"command","command":"~/.claude/hooks/bridge-context.sh"}]},{"hooks":[{"type":"command","command":"/opt/theirs.sh"},{"type":"command","command":"~/.claude/hooks/bridge-context.sh"}]}]}}' > "$CL/settings.json"
q env CLAUDE_DIR="$CL" bash "$ROOT/providers/claude/install-hooks.sh"
check "claude: legacy bridge-context entries removed, foreign kept" "$(jq -c '[.hooks.SessionStart[].hooks[].command]' "$CL/settings.json")" \
  '["/opt/theirs.sh","~/.claude/hooks/git-context.sh","~/.claude/hooks/prism-context.sh"]'
check "claude: legacy bridge-context.sh copy removed" "$([ -e "$CL/hooks/bridge-context.sh" ] && echo present || echo absent)" "absent"
snap="$(jq -Sc . "$CL/settings.json")"
q env CLAUDE_DIR="$CL" bash "$ROOT/providers/claude/install-hooks.sh"
check "claude: legacy cleanup is idempotent" "$(jq -Sc . "$CL/settings.json")" "$snap"

# ---------------- lever installers --provider ----------------
rm -f "$CODEX_FILE" "$CURSOR_FILE"
q bash "$ROOT/scripts/install-context-guard.sh" --provider codex
check "context-guard codex: PostToolUse .*" "$(jq -r '.hooks.PostToolUse[0].matcher' "$CODEX_FILE")" ".*"
q bash "$ROOT/scripts/install-context-guard.sh" --provider cursor
check "context-guard cursor: unsupported, nothing written" "$([ -f "$CURSOR_FILE" ] && echo written || echo none)" "none"

q bash "$ROOT/scripts/install-done-gate.sh" --provider codex
check "done-gate codex: Stop, timeout 12" "$(jq -c '.hooks.Stop[0].hooks[0].timeout' "$CODEX_FILE")" "12"
q bash "$ROOT/scripts/install-done-gate.sh" --provider cursor
check "done-gate cursor: stop" "$(jq -r '.hooks.stop[0].command | endswith("done-gate.sh # aw:done-gate")' "$CURSOR_FILE")" "true"

q bash "$ROOT/scripts/install-scope-gate.sh" --provider codex
check "scope-gate codex: PreToolUse Agent matcher" "$(jq -r '.hooks.PreToolUse[] | select(.hooks[0].command | endswith("# aw:scope-gate")) | .matcher' "$CODEX_FILE")" '^(Agent|spawn_agent)$'
check "scope-gate codex: SubagentStart map" "$(jq '.hooks.SubagentStart | length' "$CODEX_FILE")" "1"
q bash "$ROOT/scripts/install-scope-gate.sh" --provider cursor
check "scope-gate cursor: subagentStart" "$(jq '.hooks.subagentStart | length' "$CURSOR_FILE")" "1"

q bash "$ROOT/scripts/install-external-write-guard.sh" --provider codex
check "external-write-guard codex: UserPromptSubmit turn-origin" "$(jq -r '.hooks.UserPromptSubmit[0].hooks[0].command | endswith("# aw:turn-origin")' "$CODEX_FILE")" "true"
q bash "$ROOT/scripts/install-external-write-guard.sh" --provider cursor
check "external-write-guard cursor: shell + MCP + prompt" \
  "$(jq -c '[.hooks.beforeShellExecution, .hooks.beforeMCPExecution, .hooks.beforeSubmitPrompt | length]' "$CURSOR_FILE")" "[1,1,1]"

q bash "$ROOT/scripts/install-judge-hooks.sh" --hook-only --provider codex
check "judge codex: UserPromptSubmit prompt-sort, timeout 6" "$(jq -c '[.hooks.UserPromptSubmit[].hooks[] | select(.command | endswith("# aw:prompt-sort")) | .timeout]' "$CODEX_FILE")" "[6]"
check "judge codex: prompt-sort runs through the adapter" "$(jq -r '.hooks.UserPromptSubmit[].hooks[] | select(.command | endswith("# aw:prompt-sort")) | .command | contains("adapters/codex.sh")' "$CODEX_FILE")" "true"
q bash "$ROOT/scripts/install-judge-hooks.sh" --hook-only --provider cursor
check "judge cursor: no prompt-sort (beforeSubmitPrompt cannot inject context)" "$(jq -r '[.hooks.beforeSubmitPrompt[]?.command | select(endswith("# aw:prompt-sort"))] | length' "$CURSOR_FILE")" "0"

# judge has no uninstaller: drop its entries so the lever-uninstall check below stays exact.
for f in "$CODEX_FILE" "$CURSOR_FILE"; do
  jq 'def keep: (.command // .hooks[0].command // "") | (endswith("# aw:judge-health") or endswith("# aw:prompt-sort")) | not;
      .hooks |= (with_entries(.value |= map(select(keep))) | with_entries(select(.value | length > 0)))' "$f" > "$f.tmp" && mv "$f.tmp" "$f"
done

snap_codex="$(jq -Sc . "$CODEX_FILE")"; snap_cursor="$(jq -Sc . "$CURSOR_FILE")"
q bash "$ROOT/scripts/install-wake-gating.sh" --provider codex
q bash "$ROOT/scripts/install-wake-gating.sh" --provider cursor
check "wake-gating: unsupported on codex/cursor, nothing written" "$(jq -Sc . "$CODEX_FILE")$(jq -Sc . "$CURSOR_FILE")" "$snap_codex$snap_cursor"

for s in context-guard done-gate scope-gate external-write-guard; do
  q bash "$ROOT/scripts/install-$s.sh" --provider codex --uninstall
  q bash "$ROOT/scripts/install-$s.sh" --uninstall --provider cursor
done
check "lever uninstall (codex) removes every owned entry" "$(jq -c . "$CODEX_FILE")" '{"hooks":{}}'
check "lever uninstall (cursor) removes every owned entry" "$(jq -c . "$CURSOR_FILE")" '{"hooks":{},"version":1}'

bash "$ROOT/scripts/install-done-gate.sh" --provider bogus > /dev/null 2>&1; rc=$?
check "unknown provider is rejected" "$rc" "1"

# ---------------- probe --provider ----------------
rm -f "$CODEX_FILE" "$CURSOR_FILE"
q bash "$ROOT/scripts/probe.sh" --provider codex on
check "probe codex: no TeammateIdle" "$(jq '.hooks | has("TeammateIdle")' "$CODEX_FILE")" "false"
check "probe codex: logs under probe/codex" "$(jq -r '.hooks.Stop[0].hooks[0].command' "$CODEX_FILE")" "$AWH/probe-log.sh Stop codex # aw:probe"
check "probe codex: status lists events" "$(bash "$ROOT/scripts/probe.sh" --provider codex status | wc -l | tr -d ' ')" "6"
q bash "$ROOT/scripts/probe.sh" --provider codex off
check "probe codex: off removes everything" "$(jq -c . "$CODEX_FILE")" '{"hooks":{}}'
q bash "$ROOT/scripts/probe.sh" --provider cursor on
check "probe cursor: raw adapter entry" "$(jq -r '.hooks.stop[0].command' "$CURSOR_FILE")" "$AWH/adapters/cursor.sh --raw --event stop $AWH/probe-log.sh stop cursor # aw:probe"
check "probe cursor: preToolUse matcher Task" "$(jq -r '.hooks.preToolUse[0].matcher' "$CURSOR_FILE")" "Task"
q bash "$ROOT/scripts/probe.sh" --provider cursor off
check "probe cursor: off removes everything" "$(jq -c '.hooks' "$CURSOR_FILE")" '{}'
check "probe claude (default) untouched by provider runs" "$([ -e "$HOME/.claude/settings.json" ] && echo touched || echo untouched)" "untouched"

exit $fail
