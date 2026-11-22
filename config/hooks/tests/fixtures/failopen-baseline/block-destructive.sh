#!/usr/bin/env bash
set -euo pipefail

# PreToolUse hook: Block destructive commands
# Reads tool input JSON from stdin. Exit 2 = deny, Exit 0 = allow.

INPUT=$(cat)
COMMAND=$(printf '%s\n' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null) || true
[ -z "$COMMAND" ] && exit 0

# rm with recursive and force flags in any combination
# Catches: rm -rf, rm -r -f, rm -r --force, rm --recursive -f, rm -rfd, etc.
if printf '%s\n' "$COMMAND" | grep -qE '\brm\b' && \
   printf '%s\n' "$COMMAND" | grep -qE '(\s-[a-zA-Z]*r|\s--recursive)' && \
   printf '%s\n' "$COMMAND" | grep -qE '(\s-[a-zA-Z]*f|\s--force)'; then
  echo "BLOCKED: rm -rf is destructive and irreversible."
  echo "Suggestion: Use 'trash' or 'mv' to a backup location instead."
  exit 2
fi

# git reset --hard
if printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+reset\s+--hard\b'; then
  echo "BLOCKED: git reset --hard discards all uncommitted changes."
  echo "Suggestion: Use 'git stash' to save changes, or 'git reset --soft' to unstage."
  exit 2
fi

# git push --force (but NOT --force-with-lease)
# Match --force followed by whitespace or end-of-string to avoid matching --force-with-lease
if printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+push\s+.*--force(\s|$)' && \
   ! printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+push\s+.*--force-with-lease\b'; then
  echo "BLOCKED: git push --force can overwrite remote history."
  echo "Suggestion: Use 'git push --force-with-lease' for safer force pushes, or create a PR."
  exit 2
fi

# git checkout . (anchored to end of line to avoid false positive on .gitignore etc.)
if printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+checkout\s+(--\s+)?\.\s*$'; then
  echo "BLOCKED: git checkout . discards all unstaged changes."
  echo "Suggestion: Use 'git stash' to save changes, or checkout specific files."
  exit 2
fi

# git restore . (modern equivalent of git checkout -- .)
if printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+restore\s+(--\s+)?\.\s*$'; then
  echo "BLOCKED: git restore . discards all unstaged changes."
  echo "Suggestion: Use 'git stash' to save changes, or restore specific files."
  exit 2
fi

# git clean -f (any flag combo containing f, or --force long form)
if printf '%s\n' "$COMMAND" | grep -qE '\bgit\s+clean\b' && \
   printf '%s\n' "$COMMAND" | grep -qE '(\s-[a-zA-Z]*f|\s--force)'; then
  echo "BLOCKED: git clean -f permanently deletes untracked files."
  echo "Suggestion: Use 'git clean -n' (dry run) first to preview what would be deleted."
  exit 2
fi

exit 0
