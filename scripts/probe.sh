#!/usr/bin/env bash
# Turns the hook-input probe (cheap-agent-harness rollout step 0.5) on or off.
# Usage: scripts/probe.sh [--provider claude|codex|cursor] on|off|status
#
# claude (default): ~/.claude/settings.json, logs to ~/.agentic-workflow/probe/
# codex:  ~/.codex/hooks.json,  logs to ~/.agentic-workflow/probe/codex/
# cursor: ~/.cursor/hooks.json, logs to ~/.agentic-workflow/probe/cursor/
#         (through adapters/cursor.sh --raw: raw stdin is logged, and the
#         adapter answers Cursor's permission hooks with "allow")
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SETTINGS_FILE="${CLAUDE_SETTINGS_FILE:-$HOME/.claude/settings.json}"
HOOKS_DIR="${CLAUDE_HOOKS_DIR:-$HOME/.claude/hooks}"
# shellcheck source=../config/lib/merge-hook.sh
source "$ROOT/config/lib/merge-hook.sh"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

case "$AW_PROVIDER" in
  claude)
    # PermissionRequest, Notification, SessionStart and SessionEnd are the aw:fleet hook's events (session collector spec 5.2)
    PLAIN_EVENTS=(UserPromptSubmit Stop SubagentStop TeammateIdle SubagentStart PermissionRequest Notification SessionStart SessionEnd)
    TOOL_EVENTS=(PreToolUse PostToolUse)
    TOOL_MATCHER='Agent|SendMessage'
    ;;
  codex)
    # No TeammateIdle in Codex. spawn_agent matches as Agent.
    aw_hooks_init codex
    SETTINGS_FILE="$AW_HOOKS_CONFIG"; HOOKS_DIR="$AW_HOOKS_INSTALL_DIR"
    PLAIN_EVENTS=(UserPromptSubmit Stop SubagentStop SubagentStart)
    TOOL_EVENTS=(PreToolUse PostToolUse)
    TOOL_MATCHER='Agent|spawn_agent|send_message'
    ;;
  cursor)
    aw_hooks_init cursor
    SETTINGS_FILE="$AW_HOOKS_CONFIG"; HOOKS_DIR="$AW_HOOKS_INSTALL_DIR"
    PLAIN_EVENTS=(beforeSubmitPrompt stop subagentStop subagentStart)
    TOOL_EVENTS=(preToolUse postToolUse)
    TOOL_MATCHER='Task'
    ;;
esac

entry() {
  local cmd
  case "$AW_PROVIDER" in
    claude)
      local hook_path="~/.claude/hooks/probe-log.sh"
      if [ -n "${CLAUDE_HOOKS_DIR:-}" ]; then
        hook_path="$HOOKS_DIR/probe-log.sh"
      fi
      cmd="$hook_path $1 # aw:probe"
      ;;
    codex) cmd="$HOOKS_DIR/probe-log.sh $1 codex # aw:probe" ;;
    cursor) cmd="$HOOKS_DIR/adapters/cursor.sh --raw --event $1 $HOOKS_DIR/probe-log.sh $1 cursor # aw:probe" ;;
  esac
  if [ "$AW_PROVIDER" = "cursor" ]; then
    if [ -n "${2:-}" ]; then
      jq -nc --arg c "$cmd" --arg m "$2" '{command: $c, matcher: $m}'
    else
      jq -nc --arg c "$cmd" '{command: $c}'
    fi
  elif [ -n "${2:-}" ]; then
    jq -nc --arg c "$cmd" --arg m "$2" '{matcher: $m, hooks: [{type: "command", command: $c}]}'
  else
    jq -nc --arg c "$cmd" '{hooks: [{type: "command", command: $c}]}'
  fi
}

merge() {
  if [ "$AW_PROVIDER" = "cursor" ]; then merge_hook_flat "$@"; else merge_hook "$@"; fi
}

# backup_settings / restore_settings: make on/off transactional across their
# several merge_hook calls, so a failure partway through (e.g. jq breaking)
# never leaves settings.json half-installed.
BACKUP=""
backup_settings() {
  BACKUP=""
  if [ -f "$SETTINGS_FILE" ]; then
    BACKUP="$(mktemp "$(dirname "$SETTINGS_FILE")/.$(basename "$SETTINGS_FILE").probe-backup.XXXXXX")"
    cp "$SETTINGS_FILE" "$BACKUP"
  fi
}
restore_settings() {
  if [ -n "$BACKUP" ]; then
    mv "$BACKUP" "$SETTINGS_FILE"
  else
    rm -f "$SETTINGS_FILE"
  fi
}
drop_backup() {
  [ -n "$BACKUP" ] && rm -f "$BACKUP"
  BACKUP=""
}

LOG_DIR="~/.agentic-workflow/probe/"
[ "$AW_PROVIDER" = "claude" ] || LOG_DIR="~/.agentic-workflow/probe/$AW_PROVIDER/"
PROVIDER_FLAG=""
[ "$AW_PROVIDER" = "claude" ] || PROVIDER_FLAG=" --provider $AW_PROVIDER"

case "${1:-}" in
  on)
    if [ "$AW_PROVIDER" = "claude" ]; then
      mkdir -p "$HOOKS_DIR"
      cp "$ROOT/config/hooks/probe-log.sh" "$HOOKS_DIR/probe-log.sh"
      chmod +x "$HOOKS_DIR/probe-log.sh"
    else
      aw_hooks_stage
    fi
    backup_settings
    ok=1
    for e in "${PLAIN_EVENTS[@]}"; do merge "$SETTINGS_FILE" "$e" aw:probe "$(entry "$e")" || { ok=0; break; }; done
    if [ "$ok" -eq 1 ]; then
      for e in "${TOOL_EVENTS[@]}"; do merge "$SETTINGS_FILE" "$e" aw:probe "$(entry "$e" "$TOOL_MATCHER")" || { ok=0; break; }; done
    fi
    if [ "$ok" -ne 1 ]; then
      restore_settings
      echo "probe: failed, settings.json restored" >&2
      exit 1
    fi
    drop_backup
    echo "probe: on. Logs go to $LOG_DIR."
    echo "Leave it on for one working day, then run: scripts/probe.sh$PROVIDER_FLAG off"
    ;;
  off)
    backup_settings
    ok=1
    for e in "${PLAIN_EVENTS[@]}" "${TOOL_EVENTS[@]}"; do merge "$SETTINGS_FILE" "$e" aw:probe null || { ok=0; break; }; done
    if [ "$ok" -ne 1 ]; then
      restore_settings
      echo "probe: failed, settings.json restored" >&2
      exit 1
    fi
    drop_backup
    echo "probe: off. Logs are kept in $LOG_DIR."
    ;;
  status)
    if [ -f "$SETTINGS_FILE" ] && hook_owners "$SETTINGS_FILE" | awk -F'\t' '$3=="aw:probe"{found=1; print $1} END{exit !found}'; then :; else echo "probe: off"; fi
    ;;
  *)
    echo "usage: scripts/probe.sh [--provider claude|codex|cursor] on|off|status" >&2
    exit 1
    ;;
esac
