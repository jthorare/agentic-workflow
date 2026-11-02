#!/usr/bin/env bash
# scripts/check-skills-toolkit.sh — skill text names the toolkit and the per-repo output dir through
# variables, so a plugin copy of skills/ can run with nothing else installed (skill-revamp spec §2.1, §2.2).
# Exit 0 when clean, 1 on drift (one `file:line: rule: text` per hit), 2 on a usage or setup error.
#
# Rules:
#   SKILLS-TOOLKIT-001  a bare `$HOME/.agentic-workflow/toolkit` or `~/.agentic-workflow/toolkit` in skills/,
#                       bootstrap/ or config/voice/ (only `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}` is allowed)
#   SKILLS-TOOLKIT-002  `agentic-workflow/` followed by `<` or `$` in skills/ (a hand-spelled per-repo output
#                       path such as `~/.agentic-workflow/<repo-slug>/`; write `$AW_DIR/` instead, set by
#                       skills/_shared/repo-slug.sh)
#
# Fix: scripts/skills-toolkit-rewrite.sh (idempotent).
# Usage: scripts/check-skills-toolkit.sh [--root DIR]
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
while [ $# -gt 0 ]; do
  case "$1" in
    --root) ROOT="${2:?--root needs a directory}"; shift 2 ;;
    *) echo "usage: $0 [--root DIR]" >&2; exit 2 ;;
  esac
done
cd "$ROOT" || { echo "SKILLS-TOOLKIT: cannot cd to $ROOT" >&2; exit 2; }
command -v perl >/dev/null || { echo "SKILLS-TOOLKIT: perl is required" >&2; exit 2; }
[ -d skills ] || { echo "SKILLS-TOOLKIT: no skills/ directory in $ROOT" >&2; exit 2; }

# perl prints "path:line: RULE: text" for each hit in the files given as arguments.
scan() { # $1 = rule id, $2 = regex, files as the rest
  local rule="$1" re="$2"; shift 2
  perl -ne '
    BEGIN { ($rule, $re) = (shift, shift); $re = qr/$re/ }
    if (/$re/) { chomp; $_ = substr($_, 0, 160); print "$ARGV:$.: $rule: $_\n" }
    close ARGV if eof;
  ' "$rule" "$re" "$@"
}

files() { # text files under the given directories
  local d
  for d in "$@"; do
    [ -d "$d" ] && find "$d" -type f \( -name '*.md' -o -name '*.sh' -o -name '*.json' -o -name '*.yaml' -o -name '*.yml' \
      -o -name '*.py' -o -name '*.txt' -o -name '*.html' -o -name '*.mjs' -o -name '*.ts' \) -print
  done | LC_ALL=C sort
}

HITS="$(
  # shellcheck disable=SC2046
  scan SKILLS-TOOLKIT-001 '(?<!\$\{AW_TOOLKIT:-)(?:\$HOME|~)/\.agentic-workflow/toolkit\b' $(files skills bootstrap config/voice)
  # shellcheck disable=SC2046
  scan SKILLS-TOOLKIT-002 'agentic-workflow/[<$]' $(files skills)
)"

if [ -n "$HITS" ]; then
  printf '%s\n' "$HITS"
  echo "SKILLS-TOOLKIT: $(printf '%s\n' "$HITS" | wc -l | tr -d ' ') hit(s); run scripts/skills-toolkit-rewrite.sh" >&2
  exit 1
fi
exit 0
