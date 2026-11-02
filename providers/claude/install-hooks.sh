#!/usr/bin/env bash
# Claude Code base hooks: safety PreToolUse(Bash) chain + SessionStart context.
# Moved verbatim from setup.sh's "Safety Hooks" section — identical behavior:
# copies config/hooks/*.sh to ~/.claude/hooks/ and merges the entries into
# ~/.claude/settings.json. Lever hooks (context-guard, done-gate, ...) stay in
# scripts/install-*.sh behind their own approval step.
#
# Usage: providers/claude/install-hooks.sh [--uninstall] [--target-dir DIR]
#        --target-dir DIR  install into DIR as if it were the agent's config home: DIR/settings.json,
#                          DIR/hooks/*.sh, and hook commands in settings.json that name DIR/hooks
#                          (absolute), so ~/.claude is never read or written (a separate-login home)
# Env:   TOOLKIT_DIR   (default: repo root)
#        CLAUDE_DIR    (default: ~/.claude)
#        SETTINGS_FILE (default: $CLAUDE_DIR/settings.json)
set -euo pipefail
USAGE="Usage: providers/claude/install-hooks.sh [--uninstall] [--target-dir DIR]"
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
SCRIPT_DIR="${TOOLKIT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
CLAUDE_DIR="${CLAUDE_DIR:-$HOME/.claude}"
SETTINGS_FILE="${SETTINGS_FILE:-$CLAUDE_DIR/settings.json}"
HOOKS_DIR="$CLAUDE_DIR/hooks"
# Hook commands in settings.json: the literal ~/.claude path by default, DIR/hooks under --target-dir.
CMD_DIR="~/.claude/hooks"
HOOKS_PATH_RE='\.claude/hooks'
if [ -n "$TARGET_DIR" ]; then
  case "$TARGET_DIR" in /*) ;; *) TARGET_DIR="$PWD/$TARGET_DIR" ;; esac
  TARGET_DIR="${TARGET_DIR%/}"
  CLAUDE_DIR="$TARGET_DIR"
  SETTINGS_FILE="$TARGET_DIR/settings.json"
  HOOKS_DIR="$TARGET_DIR/hooks"
  CMD_DIR="$HOOKS_DIR"
  HOOKS_PATH_RE='hooks'
fi

if [ "${AW_DRY_RUN:-0}" = "1" ]; then
  echo "  [dry-run] would $([ "$UNINSTALL" = 1 ] && echo 'run --uninstall on ')copy config/hooks/*.sh → $HOOKS_DIR and merge hooks into $SETTINGS_FILE"
  exit 0
fi

if [ "$UNINSTALL" = 1 ]; then
  # Remove exactly the entries this installer writes (the canonical Bash
  # safety group and the git-context/prism-context SessionStart commands).
  # Script files in ~/.claude/hooks/ are left in place: lever installers
  # reference the same copies.
  if [ -f "$SETTINGS_FILE" ] && command -v jq &>/dev/null; then
    jq --arg re "$HOOKS_PATH_RE" '
      def ours: test("/" + $re + "/(block-destructive|block-push-main|detect-secrets|rtk-rewrite|git-context|prism-context)\\.sh$");
      if .hooks then
        .hooks.PreToolUse = [ .hooks.PreToolUse[]? | select((.matcher == "Bash" and ([.hooks[]? | (.command // "") | ours] | all)) | not) ]
        | .hooks.SessionStart = [ .hooks.SessionStart[]? | .hooks = [ .hooks[]? | select((.command // "") | ours | not) ] | select(.hooks | length > 0) ]
        | if (.hooks.PreToolUse | length) == 0 then .hooks |= del(.PreToolUse) else . end
        | if (.hooks.SessionStart | length) == 0 then .hooks |= del(.SessionStart) else . end
      else . end' "$SETTINGS_FILE" > "$SETTINGS_FILE.tmp" && mv "$SETTINGS_FILE.tmp" "$SETTINGS_FILE"
    echo "  claude hooks: safety + SessionStart entries removed from $SETTINGS_FILE"
  fi
  exit 0
fi

# --- Safety Hooks ---
echo ""
echo "Installing safety hooks..."

mkdir -p "$HOOKS_DIR"
# A fresh agent home has no settings.json yet; the default install leaves a missing one alone.
if [ -n "$TARGET_DIR" ] && [ ! -f "$SETTINGS_FILE" ]; then echo '{}' > "$SETTINGS_FILE"; fi

for hook_file in "$SCRIPT_DIR/config/hooks/"*.sh; do
  [ -f "$hook_file" ] || continue
  hook_name="$(basename "$hook_file")"
  cp "$hook_file" "$HOOKS_DIR/$hook_name"
  chmod +x "$HOOKS_DIR/$hook_name"
  echo "  $hook_name: installed"
done
# The safety hooks log HOOK-FAILOPEN through lib/failopen.sh next to them.
mkdir -p "$HOOKS_DIR/lib"
cp "$SCRIPT_DIR/config/lib/failopen.sh" "$HOOKS_DIR/lib/failopen.sh"

# Merge safety hooks into existing settings.json
if [ -f "$SETTINGS_FILE" ] && command -v jq &>/dev/null; then
  # Replace any existing Bash matcher entry with the canonical one (fully idempotent, handles version drift)
  HOOK_BASH_ENTRY='{"matcher":"Bash","hooks":[{"type":"command","command":"'"$CMD_DIR"'/block-destructive.sh"},{"type":"command","command":"'"$CMD_DIR"'/block-push-main.sh"},{"type":"command","command":"'"$CMD_DIR"'/detect-secrets.sh"},{"type":"command","command":"'"$CMD_DIR"'/rtk-rewrite.sh"}]}'
  jq --argjson entry "$HOOK_BASH_ENTRY" \
    '.hooks.PreToolUse = ([.hooks.PreToolUse[]? | select(.matcher != "Bash")] + [$entry])' \
    "$SETTINGS_FILE" > "$SETTINGS_FILE.tmp" \
    && mv "$SETTINGS_FILE.tmp" "$SETTINGS_FILE"
  echo "  hooks.PreToolUse: Bash safety hooks installed (idempotent replace)"

  # Add git-context SessionStart hook if not already present
  if ! jq -e '.hooks.SessionStart[]? | select(.hooks[]?.command | test("git-context"))' "$SETTINGS_FILE" &>/dev/null; then
    HOOK_ENTRY='[{"hooks":[{"type":"command","command":"'"$CMD_DIR"'/git-context.sh"}]}]'
    if jq -e 'has("hooks") and (.hooks | has("SessionStart"))' "$SETTINGS_FILE" &>/dev/null; then
      jq --argjson entry '{"hooks":[{"type":"command","command":"'"$CMD_DIR"'/git-context.sh"}]}' '.hooks.SessionStart += [$entry]' \
        "$SETTINGS_FILE" > "$SETTINGS_FILE.tmp" \
        && mv "$SETTINGS_FILE.tmp" "$SETTINGS_FILE"
    else
      jq --argjson entries "$HOOK_ENTRY" '.hooks.SessionStart = $entries' \
        "$SETTINGS_FILE" > "$SETTINGS_FILE.tmp" \
        && mv "$SETTINGS_FILE.tmp" "$SETTINGS_FILE"
    fi
    echo "  hooks.SessionStart: git-context added"
  fi

  # Add prism-context SessionStart hook if not already present
  if ! jq -e '.hooks.SessionStart[]? | select(.hooks[]?.command | test("prism-context"))' "$SETTINGS_FILE" &>/dev/null; then
    HOOK_ENTRY='[{"hooks":[{"type":"command","command":"'"$CMD_DIR"'/prism-context.sh"}]}]'
    if jq -e 'has("hooks") and (.hooks | has("SessionStart"))' "$SETTINGS_FILE" &>/dev/null; then
      jq --argjson entry '{"hooks":[{"type":"command","command":"'"$CMD_DIR"'/prism-context.sh"}]}' '.hooks.SessionStart += [$entry]' \
        "$SETTINGS_FILE" > "$SETTINGS_FILE.tmp" \
        && mv "$SETTINGS_FILE.tmp" "$SETTINGS_FILE"
    else
      jq --argjson entries "$HOOK_ENTRY" '.hooks.SessionStart = $entries' \
        "$SETTINGS_FILE" > "$SETTINGS_FILE.tmp" \
        && mv "$SETTINGS_FILE.tmp" "$SETTINGS_FILE"
    fi
    echo "  hooks.SessionStart: prism-context added"
  fi

  # Remove the legacy bridge-context SessionStart hook left by older installs
  # (it queried the removed /memory/context endpoint). Drops only that command
  # from each group, then any group left empty. No-op when absent.
  if jq -e '[.hooks.SessionStart[]?.hooks[]? | (.command // "") | select(test("bridge-context\\.sh"))] | length > 0' "$SETTINGS_FILE" &>/dev/null; then
    jq '.hooks.SessionStart = [ .hooks.SessionStart[]?
          | .hooks = [ .hooks[]? | select((.command // "") | test("bridge-context\\.sh") | not) ]
          | select(.hooks | length > 0) ]' \
      "$SETTINGS_FILE" > "$SETTINGS_FILE.tmp" && mv "$SETTINGS_FILE.tmp" "$SETTINGS_FILE"
    echo "  hooks.SessionStart: removed legacy bridge-context hook"
  fi
  if [ -f "$HOOKS_DIR/bridge-context.sh" ]; then
    rm -f "$HOOKS_DIR/bridge-context.sh"
    echo "  bridge-context.sh: removed legacy copy from $HOOKS_DIR"
  fi

  # Print installed hooks by owner (aw:* = agentic-workflow, prism = prism connect)
  # shellcheck source=../../config/lib/merge-hook.sh
  source "$SCRIPT_DIR/config/lib/merge-hook.sh"
  echo "  hooks by owner:"
  hook_owners "$SETTINGS_FILE" | awk -F'\t' '{printf "    %-18s %-8s %s\n", $1, $3, $4}'
fi
