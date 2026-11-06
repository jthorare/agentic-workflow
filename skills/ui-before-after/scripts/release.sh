#!/usr/bin/env bash
# Releases run <n>'s lease on proof-main. The last live lease out stops proof-main, and only when
# ui-before-after started it.
#
#   release.sh <n> [<main-worktree>]
set -u

HERE="$(cd "$(dirname "$0")" && pwd)"
WEB_APP_ROOT="${WEB_APP_ROOT:-$HOME/work/web-app}"
STACK="${UI_BEFORE_AFTER_STACK:-bash $HERE/stack.sh}"
LEASE="${UI_BEFORE_AFTER_LEASE:-bash $HERE/lease.sh}"

[ $# -ge 1 ] || { echo "usage: release.sh <n> [<main-worktree>]" >&2; exit 64; }
main_worktree="${2:-$WEB_APP_ROOT/.claude/worktrees/proof-main}"

remaining=$($LEASE release "$1") || exit $?
if [ "$remaining" -gt 0 ]; then
  echo "proof-main still in use by $remaining other run(s); leaving it running"
elif [ -d "$main_worktree" ]; then
  echo "no other run holds proof-main; stopping it"
  $STACK down "$main_worktree"
fi
