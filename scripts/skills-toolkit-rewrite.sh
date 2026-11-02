#!/usr/bin/env bash
# scripts/skills-toolkit-rewrite.sh — make skill text self-contained (skill-revamp spec §2.1, §2.2).
# Idempotent: a second run changes nothing, so a skill PR in flight rebases by re-running it on its branch.
#
#   toolkit  $HOME/.agentic-workflow/toolkit and ~/.agentic-workflow/toolkit
#            -> ${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}          (skills/, bootstrap/, config/voice/)
#   per-repo ~/.agentic-workflow/<repo-slug>/, <slug>, $REPO_SLUG, ${REPO_SLUG} (also after $HOME)
#            -> $AW_DIR/ (set by skills/_shared/repo-slug.sh)           (skills/, bootstrap/)
#
# What a regex can't do (a skill that never sources repo-slug.sh before using $AW_DIR, other
# spellings) is left to the review after the run: scripts/check-skills-toolkit.sh lists what is still bare.
#
# Usage: scripts/skills-toolkit-rewrite.sh [--root DIR] [--dry-run]
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DRY=0
while [ $# -gt 0 ]; do
  case "$1" in
    --root) ROOT="${2:?--root needs a directory}"; shift 2 ;;
    --dry-run) DRY=1; shift ;;
    *) echo "usage: $0 [--root DIR] [--dry-run]" >&2; exit 2 ;;
  esac
done
cd "$ROOT" || { echo "skills-toolkit-rewrite: cannot cd to $ROOT" >&2; exit 2; }
command -v perl >/dev/null || { echo "skills-toolkit-rewrite: perl is required" >&2; exit 2; }

# Every text file under the given directories (skills may carry scripts and examples beside SKILL.md).
list() {
  local d
  for d in "$@"; do
    [ -d "$d" ] && find "$d" -type f \( -name '*.md' -o -name '*.sh' -o -name '*.json' -o -name '*.yaml' -o -name '*.yml' \
      -o -name '*.py' -o -name '*.txt' -o -name '*.html' -o -name '*.mjs' -o -name '*.ts' \) -print
  done | LC_ALL=C sort
}

changed=0
rewrite() { # $1 = perl program, files on stdin
  local f before
  while IFS= read -r f; do
    before="$(cksum <"$f")"
    if [ "$DRY" = 1 ]; then
      perl -pe "$1" "$f" | cksum | grep -qxF "$before" || { echo "would change $f"; changed=$((changed + 1)); }
    else
      perl -i -pe "$1" "$f"
      [ "$(cksum <"$f")" = "$before" ] || { echo "changed $f"; changed=$((changed + 1)); }
    fi
  done
}

# toolkit root: not when already inside ${AW_TOOLKIT:-...}
list skills bootstrap config/voice | rewrite \
  's{(?<!\$\{AW_TOOLKIT:-)(?:\$HOME|~)/\.agentic-workflow/toolkit\b}{\${AW_TOOLKIT:-\$HOME/.agentic-workflow/toolkit}}g'

# per-repo output dir
list skills bootstrap | rewrite \
  's{(?:\$HOME|~)/\.agentic-workflow/(?:<repo-slug>|<slug>|\$\{REPO_SLUG\}|\$REPO_SLUG)(?![\w-])}{\$AW_DIR}g'

echo "skills-toolkit-rewrite: $changed file(s) $([ "$DRY" = 1 ] && echo 'would change' || echo changed)"
