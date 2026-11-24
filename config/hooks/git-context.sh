#!/usr/bin/env bash
# SessionStart hook: Inject recent git context
# Outputs context to stdout for Claude to see.
# Note: set -euo pipefail intentionally omitted — this hook is informational only
# and must always exit 0 even when run outside a git repository.

echo "=== Git Context ==="

BRANCH=$(git branch --show-current 2>/dev/null || echo "(detached)")
echo "Branch: $BRANCH"

echo ""
echo "Recent commits:"
git log --oneline -5 2>/dev/null || echo "(no commits)"

echo ""
echo "Working tree status:"
git status --short 2>/dev/null || echo "(not a git repo)"

# The repo's command catalog (#4221): one pointer line, counting the repo's own entries, when it has one. Fails open and
# stays cheap: outside a repo, without jq, or with a missing file or one that isn't JSON it prints nothing (one jq call).
AW_CATALOG_TOP=$(git rev-parse --show-toplevel 2>/dev/null)
if [ -n "$AW_CATALOG_TOP" ] && [ -f "$AW_CATALOG_TOP/.agentic-workflow/commands.json" ] && command -v jq >/dev/null 2>&1; then
  AW_CATALOG_N=$(jq -r 'if (.entries | type) == "array" then .entries | length else empty end' "$AW_CATALOG_TOP/.agentic-workflow/commands.json" 2>/dev/null)
  case "$AW_CATALOG_N" in
    "" | 0 | *[!0-9]*) ;;
    *) echo ""; echo "Commands: $AW_CATALOG_N in .agentic-workflow/commands.json; find one with: aw-commands search <words>" ;;
  esac
fi
exit 0
