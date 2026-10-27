#!/usr/bin/env bash
# Trait check: trait-check.sh on file edits (PostToolUse) and Stop. Opt-in:
# setup.sh never runs this, so only the people who run it get the hook. Its
# own approval gate, in the style of install-scope-gate.sh.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SETTINGS_FILE="${CLAUDE_SETTINGS_FILE:-$HOME/.claude/settings.json}"
HOOKS_DIR="${CLAUDE_HOOKS_DIR:-$HOME/.claude/hooks}"
JUDGE_BIN="${AW_JUDGE_BIN:-judge}"
# shellcheck source=../config/lib/merge-hook.sh
source "$ROOT/config/lib/merge-hook.sh"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
# Usage: scripts/install-trait-check.sh [--provider claude|codex|cursor] [--uninstall]
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

# Timeouts sit above `judge trait-check`'s own budgets (edit 15s, stop 40s), so
# the host never kills the hook before judge can fail open on a slow Jev call.
EDIT_TIMEOUT=20
STOP_TIMEOUT=45

if [ "$AW_PROVIDER" != "claude" ]; then
  aw_hooks_init "$AW_PROVIDER"
  if [ "$AW_PROVIDER" = "codex" ]; then
    EDIT_EVENT=PostToolUse; STOP_EVENT=Stop; EDIT_MATCHER='^(apply_patch|Edit|Write)$'
  else
    # Cursor: no matcher; trait-check.sh keeps only tools that change files.
    EDIT_EVENT=postToolUse; STOP_EVENT=stop; EDIT_MATCHER=""
  fi
  if [ "${1:-}" = "--uninstall" ]; then
    aw_hook_unset "$EDIT_EVENT" aw:trait-check-edit
    aw_hook_unset "$STOP_EVENT" aw:trait-check-stop
    echo "  trait-check: hook entries removed from $AW_HOOKS_CONFIG"
    exit 0
  fi
  if ! command -v "$JUDGE_BIN" > /dev/null 2>&1; then
    echo "  trait-check: refusing to install — judge is not installed (run ./setup.sh, or set AW_JUDGE_BIN)" >&2
    exit 1
  fi
  aw_hooks_stage
  aw_hook_set "$EDIT_EVENT" aw:trait-check-edit trait-check.sh "$EDIT_MATCHER" "$EDIT_TIMEOUT"
  aw_hook_set "$STOP_EVENT" aw:trait-check-stop trait-check.sh "" "$STOP_TIMEOUT"
  echo "  trait-check: trait-check ($EDIT_EVENT, $STOP_EVENT) hooks installed for $AW_PROVIDER in $AW_HOOKS_CONFIG"
  exit 0
fi

if [ "${1:-}" = "--uninstall" ]; then
  merge_hook "$SETTINGS_FILE" PostToolUse aw:trait-check-edit null
  merge_hook "$SETTINGS_FILE" Stop aw:trait-check-stop null
  echo "  trait-check: hook entries removed from $SETTINGS_FILE"
  exit 0
fi

if ! command -v "$JUDGE_BIN" > /dev/null 2>&1; then
  echo "  trait-check: refusing to install — judge is not installed (run ./setup.sh, or set AW_JUDGE_BIN)" >&2
  exit 1
fi

mkdir -p "$HOOKS_DIR"
aw_install_failopen_lib "$HOOKS_DIR"
cp "$ROOT/config/hooks/trait-check.sh" "$HOOKS_DIR/trait-check.sh"
chmod +x "$HOOKS_DIR/trait-check.sh"
EDIT_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/trait-check.sh # aw:trait-check-edit" --argjson t "$EDIT_TIMEOUT" \
  '{matcher:"Edit|Write|MultiEdit|NotebookEdit", hooks:[{type:"command",command:$c,timeout:$t}]}')
merge_hook "$SETTINGS_FILE" PostToolUse aw:trait-check-edit "$EDIT_ENTRY"
STOP_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/trait-check.sh # aw:trait-check-stop" --argjson t "$STOP_TIMEOUT" \
  '{hooks:[{type:"command",command:$c,timeout:$t}]}')
merge_hook "$SETTINGS_FILE" Stop aw:trait-check-stop "$STOP_ENTRY"

echo "  trait-check: trait-check (PostToolUse edits, Stop) hooks installed"
