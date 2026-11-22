#!/usr/bin/env bash
set -euo pipefail

# PreToolUse hook: Block git push to main/master
# Exit 2 = deny, Exit 0 = allow.

INPUT=$(cat)
COMMAND=$(printf '%s\n' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null) || true
[ -z "$COMMAND" ] && exit 0

# Only check git push commands
printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+push\b' || exit 0

# Check if pushing to main or master explicitly (with optional flags before remote, and refspec support)
# Blocks: git push origin main, git push -u origin main, git push --set-upstream origin main, git push origin HEAD:main
# Allows: git push origin main:feature (pushing main TO a non-main refspec)
if printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+push\b.*\b(main|master)\b' && \
   ! printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+push\b.*\b(main|master)\s*:'; then
  echo "BLOCKED: Pushing directly to main/master is not allowed."
  echo "Suggestion: Create a feature branch and open a pull request instead."
  exit 2
fi

# Check if pushing with current branch being main/master (bare push without explicit non-main destination)
# Handles: git push, git push origin, git push --tags, git push -u origin, git push && echo done
if printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+push(\s+(-[a-zA-Z]|--[a-zA-Z-]+=?\S*)\s*)*\s*($|&&|;|\||2>)' || \
   printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+push\s+(--[a-zA-Z-]+=?\S*\s+|-[a-zA-Z]\s+\S*\s+)*origin\s*($|&&|;|\||2>)'; then
  # Note: returns "" in non-git directories — branch check silently passes (known limitation)
  CURRENT_BRANCH=$(git branch --show-current 2>/dev/null || echo "")
  if [ "$CURRENT_BRANCH" = "main" ] || [ "$CURRENT_BRANCH" = "master" ]; then
    echo "BLOCKED: You are on '$CURRENT_BRANCH'. Pushing directly to main/master is not allowed."
    echo "Suggestion: Create a feature branch and open a pull request instead."
    exit 2
  fi
fi

exit 0
