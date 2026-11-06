#!/usr/bin/env bash
# Tests for providers/install-skills.sh (SB-8: the skills of a separate-login agent home).
# Run: bash providers/tests/install-skills-target.test.sh
# Everything runs under a temp HOME and a temp target dir; the temp HOME stands in for the owner's real one.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
unset AW_STATE_DIR AW_STATE_ROOT TOOLKIT_DIR AW_DRY_RUN AW_EXTERNAL_SKILL_DIRS
q() { "$@" > /dev/null 2>&1; }
# The temp HOME must stay empty: a file here means the installer reached for the owner's home.
home_files() { find "$HOME" -mindepth 1 2>/dev/null | wc -l | tr -d ' '; }
listing() { find "$1" -mindepth 1 -maxdepth 1 | sort | while read -r f; do printf '%s -> %s\n' "$(basename "$f")" "$(readlink "$f")"; done; }

T="$WORK/claude-home"
q bash "$ROOT/providers/install-skills.sh" --target-dir "$T"
check "review links to the toolkit's skill" "$(readlink "$T/skills/review")" "$ROOT/skills/review"
check "bugFixOrchestrator links too" "$(readlink "$T/skills/bugFixOrchestrator")" "$ROOT/skills/bugFixOrchestrator"
check "bootstrap links to the toolkit's bootstrap" "$(readlink "$T/skills/bootstrap")" "$ROOT/bootstrap"
check "the real home is untouched" "$(home_files)" "0"

before="$(listing "$T/skills")"
q bash "$ROOT/providers/install-skills.sh" --target-dir "$T"
check "a second run changes nothing" "$(listing "$T/skills")" "$before"

q bash "$ROOT/providers/install-skills.sh" --target-dir="$WORK/codex-home"
check "--target-dir=DIR is the same install" "$(readlink "$WORK/codex-home/skills/review")" "$ROOT/skills/review"

q env AW_DRY_RUN=1 bash "$ROOT/providers/install-skills.sh" --target-dir "$WORK/dry"
check "a dry run links nothing" "$(find "$WORK/dry" -type l 2>/dev/null | wc -l | tr -d ' ')" "0"

out="$(bash "$ROOT/providers/install-skills.sh" --target-dir 2>&1 >/dev/null)"; rc=$?
check "missing DIR exits 2" "$rc" "2"
check "missing DIR prints usage" "$(printf '%s' "$out" | grep -c 'Usage:')" "1"
bash "$ROOT/providers/install-skills.sh" > /dev/null 2>&1; rc=$?
check "no flag exits 2" "$rc" "2"
check "a refused run writes nothing" "$(home_files)" "0"

exit "$fail"
