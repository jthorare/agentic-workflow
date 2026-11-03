#!/usr/bin/env bash
# Remove what a prototype created: the running stack, the worktree, the local branch and the
# prototype's scratch dir. Copy to $AW_DIR/prototypes/<slug>/teardown.sh, fill in STOP_CMD, run:
#   teardown.sh <slug> <main-checkout> <worktree> <scratch-root>
# Containers or databases the stack started stay as the project leaves them unless STOP_CMD removes them.
set -uo pipefail

if [ $# -ne 4 ] || ! printf %s "$1" | grep -Eq '^[a-z0-9][a-z0-9-]*$'; then
  echo "usage: $0 <slug> <main-checkout> <worktree> <scratch-root>   (slug: lowercase letters, digits, dashes)" >&2
  exit 1
fi
slug=$1
main=$2
wt=$3
root=$4

STOP_CMD="" # the project's stop command from Phase 2, run from $wt, e.g. "make down"; empty = nothing to stop
if [ -n "$STOP_CMD" ]; then
  (cd "$wt" && bash -c "$STOP_CMD") || exit 1
fi

if [ -d "$wt" ]; then
  git -C "$main" worktree remove --force "$wt" && echo "removed worktree $wt"
  git -C "$main" branch -D "local-prototype/$slug" 2>/dev/null && echo "deleted branch local-prototype/$slug"
  git -C "$main" worktree prune
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
