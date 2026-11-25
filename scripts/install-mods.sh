#!/usr/bin/env bash
# Install the toolkit's Claude Code mods (mods/pr-line, mods/my-prs) through Claude Code's own plugin commands.
# Usage: scripts/install-mods.sh [--scope user|project|local] [--dry-run]
# Opt-in (./setup.sh --with-mods); setup.sh does not run it by default. Two steps, each skipped when already done:
#   1. claude plugin marketplace add <repo>/mods      (marketplace "aw-mods", from mods/.claude-plugin/marketplace.json)
#   2. claude plugin install <mod>@aw-mods --scope S  (for pr-line and my-prs)
# Re-running changes nothing. --dry-run prints each command and runs none. Without `claude` on PATH it prints a note and
# exits 0. Set the mods' options (bot reviewers, check prefixes, home repo) afterwards: `claude plugin configure <mod>`.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MARKETPLACE="aw-mods"
MODS="pr-line my-prs"
SCOPE="user"
DRY=0
while [ $# -gt 0 ]; do
  case "$1" in
    --scope) SCOPE="${2:-}"; shift 2 || { echo "--scope needs a value" >&2; exit 2; } ;;
    --scope=*) SCOPE="${1#--scope=}"; shift ;;
    --dry-run) DRY=1; shift ;;
    -h|--help) sed -n '2,9p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done
case "$SCOPE" in user|project|local) ;; *) echo "--scope must be user, project or local" >&2; exit 2 ;; esac

if ! command -v claude >/dev/null 2>&1; then
  echo "  mods: claude is not on PATH; skipping (install Claude Code, then run scripts/install-mods.sh)"
  exit 0
fi

run() {
  if [ "$DRY" = "1" ]; then echo "  [dry-run] $*"; else "$@"; fi
}

if claude plugin marketplace list --json 2>/dev/null | grep -q "\"name\": *\"$MARKETPLACE\""; then
  echo "  mods: marketplace $MARKETPLACE already added"
else
  run claude plugin marketplace add "$ROOT/mods"
fi

installed="$(claude plugin list --json 2>/dev/null || true)"
for mod in $MODS; do
  if printf '%s\n' "$installed" | grep -q "\"id\": *\"$mod@$MARKETPLACE\""; then
    echo "  mods: $mod already installed"
  else
    run claude plugin install "$mod@$MARKETPLACE" --scope "$SCOPE"
  fi
done
