#!/usr/bin/env bash
# Every skill whose text has a git or gh write command must carry the Git mode section (spec 2026-10-25-skill-revamp-evals-design.md §2.4):
# a "## Git mode" heading and the `${AW_GIT_MODE:-full}` default in its SKILL.md.
# Usage: scripts/check-skills-git-mode.sh [SKILLS_DIR]    (default: skills/ next to this script's repo root)
# Exit: 0 ok, 1 drift (file:line of the first write command per skill), 2 usage.
# Scans every *.md/*.sh under a skill dir, not the shared fragments in _* dirs (a skill that follows a fragment carries its own section).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SKILLS_DIR="${1:-$ROOT/skills}"
[ "$#" -le 1 ] || { echo "usage: check-skills-git-mode.sh [SKILLS_DIR]" >&2; exit 2; }
[ -d "$SKILLS_DIR" ] || { echo "check-skills-git-mode: not a directory: $SKILLS_DIR" >&2; exit 2; }

# git: commit, push, tag <name>, add, merge|rebase|cherry-pick|revert|am, worktree add, checkout -b, switch -c, branch <name>, add (also after `git -C <dir>`).
# gh: pr create|merge|comment|review|edit|close, issue create|comment, api with an explicit write method.
# The bare `--method|-X POST` alternative is deliberately over-inclusive: it also matches `curl -X POST`. A false hit costs one Git mode section; a miss lets a write through.
WRITE_RE='git( +-C +[^ ]+)? +(commit|push|tag +[^- ]|add|(merge|rebase|cherry-pick|revert|am)( |$)|worktree +add|checkout +-b|switch +-c|branch +[^- ])|gh +pr +(create|merge|comment|review|edit|close)|gh +issue +(create|comment)|gh +api .*(--method|-X) +(POST|PUT|PATCH|DELETE)|(--method|-X) +(POST|PUT|PATCH|DELETE)'

rc=0
for dir in "$SKILLS_DIR"/*/; do
  name="$(basename "$dir")"
  case "$name" in _*) continue ;; esac
  hit="$(grep -rnE --include='*.md' --include='*.sh' "$WRITE_RE" "$dir" 2>/dev/null | head -n 1)"
  [ -n "$hit" ] || continue
  skill_md="$dir/SKILL.md"
  if [ -f "$skill_md" ] && grep -q '^## Git mode' "$skill_md" && grep -qF '${AW_GIT_MODE:-full}' "$skill_md"; then
    continue
  fi
  where="$(printf '%s' "$hit" | cut -d: -f1,2)"
  echo "$where: skill '$name' has a git/gh write command but no '## Git mode' section with \${AW_GIT_MODE:-full} in its SKILL.md" >&2
  rc=1
done
exit "$rc"
