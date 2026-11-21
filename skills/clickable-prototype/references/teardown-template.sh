#!/usr/bin/env bash
# Remove what a prototype created: the running stack, the worktree, the local branch and the
# prototype's scratch dir. Copy to $AW_DIR/prototypes/<slug>/teardown.sh, fill in STOP_CMD, run:
#   teardown.sh <slug> <main-checkout> <worktree> <scratch-root>
# Containers or databases the stack started stay as the project leaves them unless STOP_CMD removes them.
set -uo pipefail

if [ $# -ne 4 ] || ! grep -Eq '^[a-z0-9][a-z0-9-]*$' <<<"$1"; then
  echo "usage: $0 <slug> <main-checkout> <worktree> <scratch-root>   (slug: lowercase letters, digits, dashes)" >&2
  exit 1
fi
slug=$1
main=$2
wt=$3
root=$4

branch="local-prototype/$slug"

# Refuse before stopping or removing anything unless the worktree is this slug's prototype worktree.
if [ -d "$wt" ]; then
  wt_branch=$(git -C "$wt" symbolic-ref --short HEAD 2>/dev/null || true)
  if [ "$wt_branch" != "$branch" ]; then
    echo "REFUSED: $wt is on branch '${wt_branch:-detached HEAD}', not $branch; this is not the prototype worktree for '$slug'. Nothing was stopped or removed." >&2
    exit 1
  fi
fi

STOP_CMD="" # the project's stop command from Phase 2, run from $wt, e.g. "make down"; empty = nothing to stop
if [ -n "$STOP_CMD" ]; then
  (cd "$wt" && bash -c "$STOP_CMD") || exit 1
fi

fail=0

if [ -d "$wt" ]; then
  wt_real=$(cd "$wt" && pwd -P) || wt_real=$wt
  if git -C "$main" worktree list --porcelain | grep -Fxq "worktree $wt" ||
    git -C "$main" worktree list --porcelain | grep -Fxq "worktree $wt_real"; then
    if git -C "$main" worktree remove --force "$wt"; then
      echo "removed worktree $wt"
    else
      echo "FAILED: could not remove worktree $wt (still there)" >&2
      fail=1
    fi
  else
    echo "FAILED: $wt is not a worktree of $main; leaving it alone" >&2
    fail=1
  fi
fi
git -C "$main" worktree prune

if git -C "$main" show-ref --verify --quiet "refs/heads/$branch"; then
  if git -C "$main" branch -D "$branch" >/dev/null; then
    echo "deleted branch $branch"
  else
    echo "FAILED: could not delete branch $branch (still there)" >&2
    fail=1
  fi
fi
# Cleanup for a leftover branch config section (the worktree has no upstream with --no-track; the push guard is the
# pre-push hook): drop it only once the branch itself is gone.
if ! git -C "$main" show-ref --verify --quiet "refs/heads/$branch" &&
  git -C "$main" config --get-regexp "^branch\.$branch\." >/dev/null 2>&1; then
  git -C "$main" config --remove-section "branch.$branch" || fail=1
fi

# On any failure stop here, so the scratch dir (and this script) survive for a re-run.
if [ "$fail" -ne 0 ]; then
  echo "teardown INCOMPLETE: see FAILED lines above; the worktree, branch or config may remain. Fix the cause and re-run." >&2
  exit 1
fi

# Delete the scratch dir only when it resolves to a non-empty child of $root.
if [ -d "$root/$slug" ]; then
  base=$(cd "$root" && pwd -P)
  resolved=$(cd "$root/$slug" && pwd -P) || exit 1
  case "$resolved" in
    "$base"/?*) rm -r "$resolved" && echo "removed $resolved" ;;
    *)
      echo "refusing to delete $resolved: not inside $base" >&2
      exit 1
      ;;
  esac
fi
echo "teardown done"
