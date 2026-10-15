#!/usr/bin/env bash
# Remove worktrees that nothing loses: not open by any process, clean, and HEAD either merged into origin/main
# or reachable from some origin branch. Skips active lane branches (open PRs) and the main checkout.
# DRY=1 prints only.
REPO="${AW_REPO:-$HOME/repos/agentic-workflow}"
cd "$REPO" || exit 1
git fetch -q --prune origin
open_heads=$(gh pr list --state open --limit 100 --json headRefName --jq '.[].headRefName')
git worktree list --porcelain | awk '/^worktree /{print $2}' | grep -v "^$REPO$" | while read -r w; do
  b=$(git -C "$w" rev-parse --abbrev-ref HEAD 2>/dev/null)
  h=$(git -C "$w" rev-parse HEAD 2>/dev/null) || continue
  if echo "$open_heads" | grep -qx "$b"; then echo "KEEP open-PR   $b $w"; continue; fi
  case "$b" in feat/ctx-*|feat/sindri-api-*|feat/sindri-wf-*|feat/sindri-ui-*|docs/ctx-*|docs/sindri-*) echo "KEEP lane      $b $w"; continue;; esac
  if [ -n "$(git -C "$w" status --porcelain 2>/dev/null)" ]; then echo "KEEP dirty     $b $w"; continue; fi
  if git merge-base --is-ancestor "$h" origin/main 2>/dev/null; then why=merged
  elif [ -n "$(git branch -r --contains "$h" 2>/dev/null | head -1)" ]; then why=pushed
  else echo "KEEP unpushed  $b $w"; continue; fi
  if lsof +D "$w" >/dev/null 2>&1; then echo "KEEP in-use    $b $w"; continue; fi
  if [ "${DRY:-0}" = 1 ]; then echo "WOULD REMOVE ($why) $b $w"; else git worktree remove "$w" && echo "REMOVED ($why) $b $w"; fi
done
git worktree prune
df -Pk "$HOME" | awk 'NR==2{print int($4/1048576)" GB free"}'
