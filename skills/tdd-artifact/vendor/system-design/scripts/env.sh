#!/usr/bin/env bash
# Sourced by the system-design skills at the top of every bash block that needs these (shell state does not persist
# between calls). Replaces the agentic-workflow toolkit's _shared/repo-slug.sh; no toolkit, hooks or MCP needed.
#   REPO_SLUG     owner-repo from the origin remote, else the repo root's folder name, else the current folder's
#   DESIGN_HOME   per-repo deliverables root: ${SYSTEM_DESIGN_OUT:-~/.system-design}/<repo-slug> (never the code repo)
#   DESIGN_DEPS   node deps shared by both skills (playwright-core): ${SYSTEM_DESIGN_DEPS:-~/.local/share/system-design-skills}
# No `set -e`; safe to source repeatedly.

REMOTE_URL=$(git remote get-url origin 2>/dev/null || echo "")
if [ -n "$REMOTE_URL" ]; then
  REPO_SLUG=$(echo "$REMOTE_URL" | sed 's|.*[:/]\([^/]*/[^/]*\)\.git$|\1|;s|.*[:/]\([^/]*/[^/]*\)$|\1|' | tr '/' '-')
else
  REPO_SLUG=$(basename "$(git rev-parse --show-toplevel 2>/dev/null || pwd)")
fi
DESIGN_HOME="${SYSTEM_DESIGN_OUT:-$HOME/.system-design}/$REPO_SLUG"
DESIGN_DEPS="${SYSTEM_DESIGN_DEPS:-$HOME/.local/share/system-design-skills}"
mkdir -p "$DESIGN_HOME"
export REPO_SLUG DESIGN_HOME DESIGN_DEPS
