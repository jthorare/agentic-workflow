#!/usr/bin/env bash
# Install judge's hooks: the SessionStart health hook and the prompt-sort UserPromptSubmit hook.
#
#   install-judge-hooks.sh [--hook-only] [--provider claude|codex|cursor]
#   --provider claude|codex|cursor  which host gets the hooks (default claude)
#
# The judge binary itself is installed by ./setup.sh (the C# build); this script only wires the
# hooks. --hook-only is accepted and does nothing extra: it is the only mode.
#
# Also installs the prompt-sort UserPromptSubmit hook (Claude and Codex; Cursor's
# beforeSubmitPrompt cannot inject context). Opt out with AW_NO_PROMPT_SORT=1.
#
# providers/<X>/install.sh runs this with --provider X, so each hook lands only for
# selected providers (Claude: after ~/.claude/settings.json has been seeded). Codex and
# Cursor entries run through config/hooks/adapters/<provider>.sh. AW_DRY_RUN=1
# prints what the Codex/Cursor hook install would do and writes nothing.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=../config/hooks/adapters/install-lib.sh
source "$SCRIPT_DIR/config/hooks/adapters/install-lib.sh"
aw_parse_provider_args "$@" || exit 1
set -- ${AW_ARGS[@]+"${AW_ARGS[@]}"}

case "${1:-}" in
  --hook-only|"") ;;
  *) echo "usage: install-judge-hooks.sh [--hook-only] [--provider claude|codex|cursor]" >&2; exit 1 ;;
esac

echo ""
echo "Installing judge hooks..."

if [ "$AW_PROVIDER" != "claude" ]; then
  aw_hooks_init "$AW_PROVIDER"
  case "$AW_PROVIDER" in
    codex) EVENT=SessionStart ;;
    cursor) EVENT=sessionStart ;;
  esac
  if [ "${AW_DRY_RUN:-0}" = "1" ]; then
    echo "  [dry-run] would install judge-health ($EVENT) for $AW_PROVIDER in $AW_HOOKS_CONFIG"
    if [ "$AW_PROVIDER" = "codex" ] && [ "${AW_NO_PROMPT_SORT:-0}" != "1" ]; then
      echo "  [dry-run] would install prompt-sort (UserPromptSubmit) for codex"
    fi
  else
    aw_hooks_stage
    aw_hook_set "$EVENT" aw:judge-health judge-health.sh
    echo "  judge: $EVENT health hook installed for $AW_PROVIDER in $AW_HOOKS_CONFIG"
    if [ "${AW_NO_PROMPT_SORT:-0}" != "1" ]; then
      case "$AW_PROVIDER" in
        codex)
          aw_hook_set UserPromptSubmit aw:prompt-sort prompt-sort.sh "" 6
          echo "  judge: UserPromptSubmit prompt-sort hook installed for codex"
          ;;
        cursor)
          aw_unsupported prompt-sort "beforeSubmitPrompt cannot inject context"
          ;;
      esac
    fi
  fi
else
  SETTINGS_FILE="${CLAUDE_SETTINGS_FILE:-$HOME/.claude/settings.json}"
  HOOKS_DIR="${CLAUDE_HOOKS_DIR:-$HOME/.claude/hooks}"
  # shellcheck source=config/lib/merge-hook.sh
  source "$SCRIPT_DIR/config/lib/merge-hook.sh"
  mkdir -p "$HOOKS_DIR"
  cp "$SCRIPT_DIR/config/hooks/judge-health.sh" "$HOOKS_DIR/judge-health.sh"
  chmod +x "$HOOKS_DIR/judge-health.sh"
  ENTRY=$(jq -nc --arg c "$HOOKS_DIR/judge-health.sh # aw:judge-health" '{hooks:[{type:"command",command:$c}]}')
  merge_hook "$SETTINGS_FILE" SessionStart aw:judge-health "$ENTRY"
  echo "  judge: SessionStart health hook installed"
  if [ "${AW_NO_PROMPT_SORT:-0}" != "1" ]; then
    cp "$SCRIPT_DIR/config/hooks/prompt-sort.sh" "$HOOKS_DIR/prompt-sort.sh"
    chmod +x "$HOOKS_DIR/prompt-sort.sh"
    SORT_ENTRY=$(jq -nc --arg c "$HOOKS_DIR/prompt-sort.sh # aw:prompt-sort" '{hooks:[{type:"command",command:$c,timeout:6}]}')
    merge_hook "$SETTINGS_FILE" UserPromptSubmit aw:prompt-sort "$SORT_ENTRY"
    echo "  judge: UserPromptSubmit prompt-sort hook installed (shadow mode: records, injects nothing until a scaffold switch is on)"
  fi
  # Wake gating (lever 1A: send-gate, record-teammate-name, outbox-flush) is
  # a separate, explicitly-approved install — scripts/install-wake-gating.sh
  # — never turned on as a side effect of installing the judge hooks (the user
  # approves live steps one at a time; "install judge" silently activating
  # the send gate on every future SendMessage would violate that).
fi
