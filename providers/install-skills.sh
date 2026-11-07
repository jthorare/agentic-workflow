#!/usr/bin/env bash
# Links the toolkit's skills into one agent config home, the way setup.sh links them into ~/.claude/skills and ~/.codex/skills:
# DIR/skills/<name> -> TOOLKIT_DIR/skills/<name> for every managed skill, plus bootstrap. `sindri sandbox login` runs it for the
# separate-login home it makes (sandbox spec SB-8), so a skill Step finds /review, $review and the rest in that home. Neither the
# owner's real home nor the state directory is written; external skill packs are not linked.
#
# Usage: providers/install-skills.sh --target-dir DIR
#        --target-dir DIR  the agent's config home (CLAUDE_CONFIG_DIR or CODEX_HOME); skills go to DIR/skills
# Env:   TOOLKIT_DIR  (default: repo root)
#        AW_DRY_RUN=1 prints what would change and touches nothing
set -euo pipefail
USAGE="Usage: providers/install-skills.sh --target-dir DIR"
TARGET_DIR=""
while [ $# -gt 0 ]; do
  case "$1" in
    --target-dir)
      [ $# -ge 2 ] && [ -n "$2" ] || { echo "$USAGE" >&2; exit 2; }
      TARGET_DIR="$2"; shift 2 ;;
    --target-dir=*)
      TARGET_DIR="${1#--target-dir=}"
      [ -n "$TARGET_DIR" ] || { echo "$USAGE" >&2; exit 2; }
      shift ;;
    *) echo "$USAGE" >&2; exit 2 ;;
  esac
done
[ -n "$TARGET_DIR" ] || { echo "$USAGE" >&2; exit 2; }
case "$TARGET_DIR" in /*) ;; *) TARGET_DIR="$PWD/$TARGET_DIR" ;; esac
TARGET_DIR="${TARGET_DIR%/}"

TOOLKIT_DIR="${TOOLKIT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
export TOOLKIT_DIR
# shellcheck source=lib.sh
source "$TOOLKIT_DIR/providers/lib.sh"
# why: the managed set lives in setup.sh, which is a script, not a library; reading its two array lines keeps one list.
MANAGED_SKILLS=()
DEPRECATED_SKILLS=()
for array in MANAGED_SKILLS DEPRECATED_SKILLS; do
  line="$(grep -m1 "^${array}=(" "$TOOLKIT_DIR/setup.sh" || true)"
  [ -n "$line" ] || continue
  eval "$line"
done
[ "${#MANAGED_SKILLS[@]}" -gt 0 ] || { echo "install-skills: no MANAGED_SKILLS line in $TOOLKIT_DIR/setup.sh" >&2; exit 1; }

aw_install_skills_into "$TARGET_DIR/skills" < /dev/null
