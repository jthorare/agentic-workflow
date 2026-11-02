#!/usr/bin/env bash
# Codex base hooks: safety PreToolUse(Bash) chain + SessionStart context,
# the Codex equivalent of providers/claude/install-hooks.sh.
#
# Scripts are copied to a provider-neutral dir (~/.agentic-workflow/hooks/)
# and every entry in ~/.codex/hooks.json runs through
# config/hooks/adapters/codex.sh. Each entry's command ends in "# aw:<id>";
# reinstall/uninstall only ever touch those. Lever hooks (context-guard,
# done-gate, ...) stay in scripts/install-*.sh --provider codex.
#
# Codex only runs non-managed hooks after you trust them: open `codex`, run
# /hooks, and trust the aw:* entries (re-trust after each reinstall that
# changes a command).
#
# Usage: providers/codex/install-hooks.sh [--uninstall] [--target-dir DIR]
#        --target-dir DIR  install into DIR as if it were the agent's config home (a separate-login
#                          CODEX_HOME): DIR/hooks.json, scripts and adapter under DIR/hooks, and
#                          commands that name DIR/hooks; the owner's ~/.codex and state dir are untouched
# Env:   CODEX_HOOKS_FILE (default ${CODEX_HOME:-~/.codex}/hooks.json)
#        AW_HOOKS_DIR     (default ${AW_STATE_DIR:-~/.agentic-workflow}/hooks)
set -euo pipefail
USAGE="Usage: providers/codex/install-hooks.sh [--uninstall] [--target-dir DIR]"
UNINSTALL=0
TARGET_DIR=""
while [ $# -gt 0 ]; do
  case "$1" in
    --uninstall) UNINSTALL=1; shift ;;
    --target-dir)
      [ $# -ge 2 ] && [ -n "$2" ] || { echo "$USAGE" >&2; exit 2; }
      TARGET_DIR="$2"; shift 2 ;;
    --target-dir=*)
      TARGET_DIR="${1#--target-dir=}"
      [ -n "$TARGET_DIR" ] || { echo "$USAGE" >&2; exit 2; }
      shift ;;
    *) shift ;;
  esac
done
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../config/hooks/adapters/install-lib.sh
source "$ROOT/config/hooks/adapters/install-lib.sh"
if [ -n "$TARGET_DIR" ]; then
  case "$TARGET_DIR" in /*) ;; *) TARGET_DIR="$PWD/$TARGET_DIR" ;; esac
  TARGET_DIR="${TARGET_DIR%/}"
  export CODEX_HOOKS_FILE="$TARGET_DIR/hooks.json" AW_HOOKS_DIR="$TARGET_DIR/hooks"
fi
aw_hooks_init codex

SAFETY_IDS="aw:block-destructive aw:block-push-main aw:detect-secrets aw:rtk-rewrite"
CONTEXT_IDS="aw:git-context aw:prism-context"

if [ "$UNINSTALL" = 1 ]; then
  for id in $SAFETY_IDS; do aw_hook_unset PreToolUse "$id"; done
  for id in $CONTEXT_IDS; do aw_hook_unset SessionStart "$id"; done
  echo "  codex hooks: safety + SessionStart entries removed from $AW_HOOKS_CONFIG"
  exit 0
fi

if [ "${AW_DRY_RUN:-0}" = "1" ]; then
  echo "  [dry-run] would copy hooks to $AW_HOOKS_INSTALL_DIR and merge aw:* entries into $AW_HOOKS_CONFIG"
  exit 0
fi

echo ""
echo "Installing Codex hooks..."
aw_hooks_stage
echo "  hooks: copied to $AW_HOOKS_INSTALL_DIR (adapter: adapters/codex.sh)"

# Codex matches both "Bash" and unified exec ("exec_command"); the adapter
# normalizes either to the Claude {tool_name: Bash, tool_input.command} shape.
# rtk-rewrite last: its updatedInput rewrite is honored by Codex PreToolUse.
BASH_MATCHER='^(Bash|exec_command)$'
aw_hook_set PreToolUse aw:block-destructive block-destructive.sh "$BASH_MATCHER"
aw_hook_set PreToolUse aw:block-push-main block-push-main.sh "$BASH_MATCHER"
aw_hook_set PreToolUse aw:detect-secrets detect-secrets.sh "$BASH_MATCHER"
aw_hook_set PreToolUse aw:rtk-rewrite rtk-rewrite.sh "$BASH_MATCHER"
echo "  PreToolUse: block-destructive, block-push-main, detect-secrets, rtk-rewrite"

aw_hook_set SessionStart aw:git-context git-context.sh
aw_hook_set SessionStart aw:prism-context prism-context.sh
echo "  SessionStart: git-context, prism-context"

echo "  hooks by owner:"
hook_owners "$AW_HOOKS_CONFIG" | awk -F'\t' '{printf "    %-18s %-22s %s\n", $1, $3, $2}'
echo "  NOTE: run /hooks inside codex to trust the new aw:* hooks (Codex skips untrusted hooks)."
