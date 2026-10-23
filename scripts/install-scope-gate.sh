#!/usr/bin/env bash
# Scope gate (lever 3, Plan 5 Task 1): scope-gate.sh (PreToolUse/Agent) +
# subagent-start-map.sh (SubagentStart). Its own approval gate, in the style
# of install-wake-gating.sh — never folded into install-judge-hooks.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SETTINGS_FILE="${CLAUDE_SETTINGS_FILE:-$HOME/.claude/settings.json}"
HOOKS_DIR="${CLAUDE_HOOKS_DIR:-$HOME/.claude/hooks}"
JUDGE_BIN="${AW_JUDGE_BIN:-judge}"
# shellcheck source=../config/lib/merge-hook.sh
source "$ROOT/config/lib/merge-hook.sh"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
# Usage: scripts/install-scope-gate.sh [--provider claude|codex|cursor] [--uninstall]
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

# Codex: PreToolUse(Agent = spawn_agent) + SubagentStart. Cursor: scope-gate
# on subagentStart (it can block, and carries the task text + tool_call_id);
# subagent-start-map is not needed there (see config/hooks/adapters/README.md).
if [ "$AW_PROVIDER" != "claude" ]; then
  aw_hooks_init "$AW_PROVIDER"
  if [ "${1:-}" = "--uninstall" ]; then
    if [ "$AW_PROVIDER" = "codex" ]; then
      aw_hook_unset PreToolUse aw:scope-gate
      aw_hook_unset SubagentStart aw:subagent-start-map
    else
      aw_hook_unset subagentStart aw:scope-gate
    fi
    echo "  scope-gate: hook entries removed from $AW_HOOKS_CONFIG"
    exit 0
  fi
  if ! command -v "$JUDGE_BIN" > /dev/null 2>&1; then
    echo "  scope-gate: refusing to install — judge is not installed (run ./setup.sh, or set AW_JUDGE_BIN)" >&2
    exit 1
  fi
  aw_hooks_stage
  if [ "$AW_PROVIDER" = "codex" ]; then
    aw_hook_set PreToolUse aw:scope-gate scope-gate.sh '^(Agent|spawn_agent)$' 12
    aw_hook_set SubagentStart aw:subagent-start-map subagent-start-map.sh
    echo "  scope-gate: scope-gate (PreToolUse/Agent), subagent-start-map (SubagentStart) hooks installed for codex in $AW_HOOKS_CONFIG"
  else
    aw_hook_set subagentStart aw:scope-gate scope-gate.sh "" 12
    aw_unsupported subagent-start-map "Cursor's subagentStart already carries the dispatch's tool_call_id, which scope-gate saves the brief under"
    echo "  scope-gate: scope-gate (subagentStart) hook installed for cursor in $AW_HOOKS_CONFIG"
  fi
  exit 0
fi

if [ "${1:-}" = "--uninstall" ]; then
  merge_hook "$SETTINGS_FILE" PreToolUse aw:scope-gate null
  merge_hook "$SETTINGS_FILE" SubagentStart aw:subagent-start-map null
  echo "  scope-gate: hook entries removed from $SETTINGS_FILE"
  exit 0
fi

if ! command -v "$JUDGE_BIN" > /dev/null 2>&1; then
  echo "  scope-gate: refusing to install — judge is not installed (run ./setup.sh, or set AW_JUDGE_BIN)" >&2
  exit 1
fi

mkdir -p "$HOOKS_DIR"
aw_install_failopen_lib "$HOOKS_DIR"

# timeout: 12s = brief-scope's own timeBudgetMs (10s, project rule for a
# claude-cli-routed question) + 2s margin, so Claude Code never kills this
# hook before `judge` itself can fail open on a slow/timed-out model call.
cp "$ROOT/config/hooks/scope-gate.sh" "$HOOKS_DIR/scope-gate.sh"
chmod +x "$HOOKS_DIR/scope-gate.sh"
SCOPE_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/scope-gate.sh # aw:scope-gate" '{matcher:"Agent", hooks:[{type:"command",command:$c,timeout:12}]}')
merge_hook "$SETTINGS_FILE" PreToolUse aw:scope-gate "$SCOPE_ENTRY"

cp "$ROOT/config/hooks/subagent-start-map.sh" "$HOOKS_DIR/subagent-start-map.sh"
chmod +x "$HOOKS_DIR/subagent-start-map.sh"
MAP_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/subagent-start-map.sh # aw:subagent-start-map" '{hooks:[{type:"command",command:$c}]}')
merge_hook "$SETTINGS_FILE" SubagentStart aw:subagent-start-map "$MAP_ENTRY"

echo "  scope-gate: scope-gate, subagent-start-map hooks installed (lever 3)"
