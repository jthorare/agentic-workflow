#!/usr/bin/env bash
# aw:fleet — the session state hook for the Sindri session collector (spec 2026-10-16-sindri-session-collector-design.md 5.4).
# Usage: scripts/install-fleet.sh [--provider claude|codex|cursor] [--uninstall]
# Opt-in (./setup.sh --with-fleet). The hook (config/hooks/fleet-state.sh) writes one small state file per session under
# $AW_FLEET_DIR (default $AW_STATE_DIR/fleet): the state (running, asking, idle, ended), never a prompt, tool input or message.
# Off switch: AW_FLEET_DISABLE=1 in a session, or this script with --uninstall (the state files stay; the collector prunes them).
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

# Claude Code's events (spec 5.2). PreToolUse and PostToolUse run on every tool call (they record the session's last tool), so the hook
# rewrites a running file in bash and runs jq only on a change of state.
EVENTS="SessionStart UserPromptSubmit PermissionRequest Notification PreToolUse PostToolUse Stop SessionEnd"

if [ "$AW_PROVIDER" != "claude" ]; then
  aw_hooks_init "$AW_PROVIDER"
  aw_unsupported fleet "the Codex and Cursor hooks arrive with CO-1b"
  exit 0
fi

if [ "${1:-}" = "--uninstall" ]; then
  if [ -f "$SETTINGS_FILE" ]; then
    for e in $EVENTS; do merge_hook "$SETTINGS_FILE" "$e" aw:fleet null; done
  fi
  rm -f "$HOOKS_DIR/fleet-state.sh"
  echo "  fleet: hook entries removed from $SETTINGS_FILE"
  exit 0
fi

mkdir -p "$HOOKS_DIR"
cp "$ROOT/config/hooks/fleet-state.sh" "$HOOKS_DIR/fleet-state.sh"
chmod +x "$HOOKS_DIR/fleet-state.sh"
for e in $EVENTS; do
  matcher=""
  case "$e" in PreToolUse|PostToolUse) matcher=".*" ;; esac
  entry="$(jq -nc --arg cmd "$HOOKS_DIR/fleet-state.sh # aw:fleet" --arg m "$matcher" \
    '(if $m == "" then {} else {matcher: $m} end) + {hooks: [{type: "command", command: $cmd}]}')"
  merge_hook "$SETTINGS_FILE" "$e" aw:fleet "$entry"
done
echo "  fleet: hook installed for claude (${EVENTS// /, }) in $SETTINGS_FILE"
echo "  fleet: state files go to ${AW_FLEET_DIR:-${AW_STATE_DIR:-~/.agentic-workflow}/fleet}; off: AW_FLEET_DISABLE=1 per session, or $0 --uninstall"
