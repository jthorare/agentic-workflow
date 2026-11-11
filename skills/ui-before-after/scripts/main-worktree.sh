#!/usr/bin/env bash
# Makes <root>/proof-main (the repo's worktree root, see below) a locked worktree and prints its path. Takes run <n>'s
# lease on it first. It moves to the latest origin/main only when no other run holds a live lease,
# so a run in progress keeps its build. The lock keeps worktree cleanup from removing it.
#
#   main-worktree.sh <repo> <n>
set -eu

HERE="$(cd "$(dirname "$0")" && pwd)"
. "$HERE/lease.sh"

repo="$1"
n="$2"
# One worktree root per repo (AW_WORKTREE_ROOT, <repo>/.agentic-workflow/worktrees.json, else ~/repos/aw-wt/<repo>), from the shared helper.
path="$(cd "$repo" && bash "$HERE/../../_shared/worktree-path.sh" proof-main)" || exit 1

git -C "$repo" fetch -q origin main
# Forget a proof-main whose folder was deleted, so it can be added again.
git -C "$repo" worktree prune

lease_lock
lease_take "$n"
others=$(lease_others "$n")
if [ ! -d "$path" ]; then
  git -C "$repo" worktree add -q --detach "$path" origin/main
elif [ "$others" -eq 0 ]; then
  git -C "$path" checkout -q --detach origin/main
  echo "proof-main refreshed to origin/main" >&2
else
  echo "proof-main kept at $(git -C "$path" rev-parse --short=7 HEAD): $others other run(s) hold it" >&2
fi
lease_unlock
git -C "$repo" worktree lock --reason "ui-before-after main build" "$path" 2>/dev/null || true
echo "$path"
