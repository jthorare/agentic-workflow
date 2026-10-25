#!/usr/bin/env bash
# Done gate (Plan 5 Task 2): done-gate.sh (Stop, main sessions) +
# done-gate-annotate.sh (PostToolUse/Agent, subagents/teammates). Its own
# approval gate, in the style of install-wake-gating.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SETTINGS_FILE="${CLAUDE_SETTINGS_FILE:-$HOME/.claude/settings.json}"
HOOKS_DIR="${CLAUDE_HOOKS_DIR:-$HOME/.claude/hooks}"
JUDGE_BIN="${AW_JUDGE_BIN:-judge}"
# shellcheck source=../config/lib/merge-hook.sh
source "$ROOT/config/lib/merge-hook.sh"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
# Usage: scripts/install-done-gate.sh [--provider claude|codex|cursor] [--uninstall]
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

# Codex / Cursor: done-gate on Stop / stop through the provider adapter.
# done-gate-annotate has no equivalent (see config/hooks/adapters/README.md).
if [ "$AW_PROVIDER" != "claude" ]; then
  aw_hooks_init "$AW_PROVIDER"
  STOP_EVENT=Stop; [ "$AW_PROVIDER" = "cursor" ] && STOP_EVENT=stop
  if [ "${1:-}" = "--uninstall" ]; then
    aw_hook_unset "$STOP_EVENT" aw:done-gate
    echo "  done-gate: hook entry removed from $AW_HOOKS_CONFIG"
    exit 0
  fi
  if ! command -v "$JUDGE_BIN" > /dev/null 2>&1; then
    echo "  done-gate: refusing to install — judge is not installed (run ./setup.sh, or set AW_JUDGE_BIN)" >&2
    exit 1
  fi
  aw_hooks_stage
  aw_hook_set "$STOP_EVENT" aw:done-gate done-gate.sh "" 12
  aw_unsupported done-gate-annotate "no hook sees a finished subagent's result alongside its dispatch id"
  echo "  done-gate: done-gate ($STOP_EVENT) hook installed for $AW_PROVIDER in $AW_HOOKS_CONFIG"
  exit 0
fi

if [ "${1:-}" = "--uninstall" ]; then
  merge_hook "$SETTINGS_FILE" Stop aw:done-gate null
  merge_hook "$SETTINGS_FILE" PostToolUse aw:done-gate-annotate null
  echo "  done-gate: hook entries removed from $SETTINGS_FILE"
  exit 0
fi

if ! command -v "$JUDGE_BIN" > /dev/null 2>&1; then
  echo "  done-gate: refusing to install — judge is not installed (run ./setup.sh, or set AW_JUDGE_BIN)" >&2
  exit 1
fi

mkdir -p "$HOOKS_DIR"
aw_install_failopen_lib "$HOOKS_DIR"

# timeout: 12s = ask-check's own timeBudgetMs (10s, project rule for a
# claude-cli-routed question) + 2s margin, so Claude Code never kills this
# hook before `judge` itself can fail open on a slow/timed-out model call.
cp "$ROOT/config/hooks/done-gate.sh" "$HOOKS_DIR/done-gate.sh"
chmod +x "$HOOKS_DIR/done-gate.sh"
STOP_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/done-gate.sh # aw:done-gate" '{hooks:[{type:"command",command:$c,timeout:12}]}')
merge_hook "$SETTINGS_FILE" Stop aw:done-gate "$STOP_ENTRY"

# done-gate-annotate.sh only ever calls `judge brief get` (a plain sqlite
# lookup, no model call) — no wide timeout needed, but set an explicit,
# generous one anyway so a slow disk/db doesn't get killed mid-annotation.
cp "$ROOT/config/hooks/done-gate-annotate.sh" "$HOOKS_DIR/done-gate-annotate.sh"
chmod +x "$HOOKS_DIR/done-gate-annotate.sh"
ANNOTATE_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/done-gate-annotate.sh # aw:done-gate-annotate" '{matcher:"Agent", hooks:[{type:"command",command:$c,timeout:5}]}')
merge_hook "$SETTINGS_FILE" PostToolUse aw:done-gate-annotate "$ANNOTATE_ENTRY"

echo "  done-gate: done-gate (Stop), done-gate-annotate (PostToolUse/Agent) hooks installed"
