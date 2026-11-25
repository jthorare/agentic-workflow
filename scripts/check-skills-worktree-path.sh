#!/usr/bin/env bash
# A skill that runs `git worktree add` must take the path from skills/_shared/worktree-path.sh (one worktree root per repo, issue #2765).
# Rule: a file under skills/ or bootstrap/ that holds a `git worktree add` command (also `git -C <x> worktree add`) must name
# worktree-path.sh somewhere in the same file. What counts as a command:
#   - *.sh: any line that is not a # comment;
#   - *.md: a line inside a fenced code block, or a backtick span that starts with `git` and has arguments after `worktree add`.
# A bare `git worktree add` in backticks, as in "no `git worktree add`", is prose and is not counted.
# Usage: scripts/check-skills-worktree-path.sh [ROOT]    (default: the repo root next to this script; scans ROOT/skills and ROOT/bootstrap)
# Exit: 0 ok, 1 drift (file:line per hit), 2 usage.
set -uo pipefail

SELF_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[ "$#" -le 1 ] || { echo "usage: check-skills-worktree-path.sh [ROOT]" >&2; exit 2; }
ROOT="${1:-$SELF_ROOT}"
[ -d "$ROOT" ] || { echo "check-skills-worktree-path: not a directory: $ROOT" >&2; exit 2; }

CMD='git( +-C +[^ ]+)? +worktree +add'
rc=0
for top in skills bootstrap; do
  [ -d "$ROOT/$top" ] || continue
  while IFS= read -r f; do
    # the helper itself only mentions the command in its header
    case "$f" in */_shared/worktree-path.sh) continue ;; esac
    grep -q 'worktree-path\.sh' "$f" && continue
    case "$f" in
      *.sh) hits="$(grep -nE "$CMD" "$f" | grep -vE '^[0-9]+:[[:space:]]*#' | cut -d: -f1)" ;;
      *) hits="$(awk -v cmd="$CMD" '
          /^[[:space:]]*(```|~~~)/ { fence = !fence; next }
          fence && $0 ~ cmd { print NR; next }
          !fence && $0 ~ ("`" cmd " +[^`]+`") { print NR }
        ' "$f")" ;;
    esac
    for n in $hits; do
      echo "$f:$n: git worktree add without worktree-path.sh in the same file (take the path from skills/_shared/worktree-path.sh)" >&2
      rc=1
    done
  done < <(find "$ROOT/$top" \( -name '*.md' -o -name '*.sh' \) -type f | sort)
done
exit "$rc"
