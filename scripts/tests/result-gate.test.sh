#!/usr/bin/env bash
# Tests for every skill's Result-block gate (#2036): run on its own in a fresh shell (env -i, a throwaway git
# repo with a fake origin), the gate must print AW_RESULT=1 and a non-empty AW_DIR=<state>/<slug> under
# AW_RESULT=1, and AW_RESULT=0 under AW_RESULT=0, so the run always knows where its result file goes.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/result-gate-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

mkdir -p "$TMPROOT/toolkit" "$TMPROOT/home" "$TMPROOT/work"
cp -r "$REPO/skills" "$TMPROOT/toolkit/skills"
git -C "$TMPROOT/work" init -q
git -C "$TMPROOT/work" remote add origin https://example.invalid/acme/shop.git

# The first ```bash block after the `## Result block` heading; prints nothing when there is none.
extract_gate() {
  awk '
    /^## Result block[[:space:]]*$/ { seen = 1; next }
    seen && !inblock && /^```bash[[:space:]]*$/ { inblock = 1; next }
    inblock && /^```[[:space:]]*$/ { exit }
    inblock { print }
  ' "$1"
}

run_gate() { # gate-text result-flag
  (cd "$TMPROOT/work" && env -i PATH=/usr/bin:/bin HOME="$TMPROOT/home" AW_STATE_DIR="$TMPROOT/state" \
    AW_TOOLKIT="$TMPROOT/toolkit" AW_RESULT="$2" bash -c "$1" 2>&1) || true
}

echo "result-gate: every skill's Result block"
found=0
while IFS= read -r f; do
  rel="${f#"$REPO"/}"
  found=$((found + 1))
  gate="$(extract_gate "$f")"
  if [ -z "$gate" ]; then
    fail "$rel: '## Result block' heading has no bash block after it"
    continue
  fi
  on="$(run_gate "$gate" 1)"
  off="$(run_gate "$gate" 0)"
  why=""
  has -x 'AW_RESULT=1' <<<"$on" || why="no AW_RESULT=1 line under AW_RESULT=1"
  has -F "AW_DIR=$TMPROOT/state/acme-shop" <<<"$on" || why="${why:+$why; }no AW_DIR=$TMPROOT/state/acme-shop under AW_RESULT=1"
  has -F 'AW_RESULT=0' <<<"$off" || why="${why:+$why; }no AW_RESULT=0 under AW_RESULT=0"
  if [ -z "$why" ]; then ok "$rel"; else fail "$rel: $why"; fi
done < <(grep -rlE '^## Result block[[:space:]]*$' "$REPO/skills" "$REPO/bootstrap" --include=SKILL.md | sort)

if [ "$found" -eq 0 ]; then fail "no SKILL.md with a '## Result block' heading found"; fi

echo "result-gate: gates found: $found"
echo "result-gate: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
