#!/usr/bin/env bash
# Prints the path for a skill's git worktree: <root>/<name>. One root per repo, shared by all skills (issue #2765).
# Creates nothing: the caller runs `git worktree add` on the printed path.
#
#   worktree-path.sh <name>        run inside the repo or any of its worktrees
#
# The root, first match wins:
#   1. $AW_WORKTREE_ROOT
#   2. "root" in <main checkout>/.agentic-workflow/worktrees.json, i.e. {"root": "~/repos/aw-wt/web"}
#   3. ~/repos/aw-wt/<repo-name>, repo-name = basename of the main checkout
# A leading ~ is $HOME. A relative root is relative to the main checkout.
# A malformed file or one without "root" falls back to the default and warns on stderr.
# A root inside the main checkout is refused: git would see the worktrees as untracked files of the repo.
# Exit: 0 printed, 1 refused or not in a repo, 2 usage. Bash 3.2 and BSD tools safe; jq is used when present, sed otherwise.
set -u

name="${1:-}"
if [ "$#" -ne 1 ] || [ -z "$name" ]; then
  echo "usage: worktree-path.sh <name>" >&2
  exit 2
fi

common="$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null)" || {
  echo "worktree-path: not inside a git repository" >&2
  exit 1
}
repo="$(dirname "${common%/}")"
repo_name="$(basename "$repo")"

# Collapse . and .. segments and repeated slashes of an absolute path (the root may not exist yet).
normalize() {
  local IFS=/ part out="" parts
  set -f
  # shellcheck disable=SC2206
  parts=($1)
  set +f
  for part in "${parts[@]}"; do
    case "$part" in
      "" | .) ;;
      ..) out="${out%/*}" ;;
      *) out="$out/$part" ;;
    esac
  done
  printf '%s\n' "${out:-/}"
}

root=""
if [ -n "${AW_WORKTREE_ROOT:-}" ]; then
  root="$AW_WORKTREE_ROOT"
else
  cfg="$repo/.agentic-workflow/worktrees.json"
  if [ -f "$cfg" ]; then
    if command -v jq >/dev/null 2>&1; then
      root="$(jq -er '.root | select(type == "string" and length > 0)' "$cfg" 2>/dev/null)" || root=""
    else
      root="$(tr -d '\n\r' <"$cfg" | sed -n 's/.*"root"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"
    fi
    [ -n "$root" ] || echo "worktree-path: $cfg is malformed or has no \"root\"; using the default root" >&2
  fi
fi
[ -n "$root" ] || root="$HOME/repos/aw-wt/$repo_name"

case "$root" in
  "~") root="$HOME" ;;
  "~/"*) root="$HOME/${root#\~/}" ;;
esac
case "$root" in
  /*) ;;
  *) root="$repo/$root" ;;
esac
root="$(normalize "$root")"

case "$root" in
  "$repo" | "$repo"/*)
    echo "worktree-path: root $root is inside the repo $repo" >&2
    exit 1
    ;;
esac

printf '%s/%s\n' "$root" "$name"
