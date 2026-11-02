#!/usr/bin/env bash
# Tests for scripts/check-skills-toolkit.sh and scripts/skills-toolkit-rewrite.sh using temp-dir fixture trees.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

SCRIPTS="$(cd "$(dirname "$0")/.." && pwd)"
CHECK="$SCRIPTS/check-skills-toolkit.sh"
REWRITE="$SCRIPTS/skills-toolkit-rewrite.sh"
REPO="$(cd "$SCRIPTS/.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-skills-toolkit-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

run() { set +e; OUT="$(bash "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -E -- "$2"; then ok "$1"; else fail "$1: output lacks /$2/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# A compliant tree: the new forms only.
fixture() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/skills/_shared" "$d/skills/demo" "$d/config/voice" "$d/bootstrap"
  cat > "$d/skills/demo/SKILL.md" <<'MD'
---
name: demo
---
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md`.

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
```
Write the report to `$AW_DIR/qa/report.md`. State lives in `${AW_STATE_DIR:-$HOME/.agentic-workflow}/judge`.
MD
  printf 'AW_DIR="${AW_STATE_DIR:-$HOME/.agentic-workflow}/$REPO_SLUG"\n' > "$d/skills/_shared/repo-slug.sh"
  printf 'Examples: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/config/voice/joi-examples.md`\n' > "$d/config/voice/joi.md"
  printf '%s' "$d"
}

# The old forms, to be rewritten.
dirty_fixture() {
  local d; d="$(fixture "$1")"
  cat > "$d/skills/demo/OLD.md" <<'MD'
read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md`
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
TK="~/.agentic-workflow/toolkit"
Report: ~/.agentic-workflow/<repo-slug>/reviews/<pr>.json
Slug form: ~/.agentic-workflow/<slug>/plans/x.md
mkdir -p "$HOME/.agentic-workflow/$REPO_SLUG"
Braced: $HOME/.agentic-workflow/${REPO_SLUG}/qa/x.md
Already new: ${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills
Untouched: ~/.agentic-workflow/judge/config.json and .agentic-workflow/deploy.json
MD
  printf '%s' "$d"
}

tree_sum() { (cd "$1" && find . -type f | LC_ALL=C sort | xargs cksum); }

echo "clean tree"
D="$(fixture clean)"
run "$D"; expect_rc "a tree with only the new forms passes" 0

echo "planted drift"
D="$(fixture bare-home)"
printf 'line one\nread `$HOME/.agentic-workflow/toolkit/skills/_preamble.md`\n' > "$D/skills/demo/BARE.md"
run "$D"; expect_rc "a bare \$HOME toolkit path fails" 1
expect_out "it names file:line" 'skills/demo/BARE\.md:2: SKILLS-TOOLKIT-001'

D="$(fixture bare-tilde)"
printf 'x\nSee ~/.agentic-workflow/toolkit/docs/x.md\n' > "$D/skills/demo/TILDE.md"
run "$D"; expect_rc "a bare ~ toolkit path fails" 1
expect_out "it names file:line" 'skills/demo/TILDE\.md:2: SKILLS-TOOLKIT-001'

D="$(fixture bare-voice)"
printf 'a\nb\nIn ~/.agentic-workflow/toolkit/config/voice/joi-examples.md\n' > "$D/config/voice/joi.md"
run "$D"; expect_rc "a bare toolkit path in config/voice fails" 1
expect_out "it names file:line" 'config/voice/joi\.md:3: SKILLS-TOOLKIT-001'

D="$(fixture slug-angle)"
printf 'Report: ~/.agentic-workflow/<repo-slug>/reviews/1.json\n' > "$D/skills/demo/SLUG.md"
run "$D"; expect_rc "a hand-spelled <repo-slug> path fails" 1
expect_out "it names file:line" 'skills/demo/SLUG\.md:1: SKILLS-TOOLKIT-002'

D="$(fixture slug-var)"
printf 'mkdir -p "$HOME/.agentic-workflow/$REPO_SLUG/qa"\n' > "$D/skills/demo/VAR.md"
run "$D"; expect_rc "a hand-spelled \$REPO_SLUG path fails" 1
expect_out "it names file:line" 'skills/demo/VAR\.md:1: SKILLS-TOOLKIT-002'

D="$(fixture ok-neighbours)"
printf 'Config: ~/.agentic-workflow/judge/config.json and .agentic-workflow/deploy.json, %s\n' '${AW_STATE_DIR:-$HOME/.agentic-workflow}/x' > "$D/skills/demo/NEIGHBOURS.md"
run "$D"; expect_rc "other agentic-workflow paths are not flagged" 0

echo "usage"
set +e; OUT="$(bash "$CHECK" --bogus 2>&1)"; RC=$?; set -e
expect_rc "an unknown flag exits 2" 2
mkdir -p "$TMPROOT/empty"; run "$TMPROOT/empty"; expect_rc "a tree without skills/ exits 2" 2

echo "rewrite"
D="$(dirty_fixture dirty)"
run "$D"; expect_rc "the dirty fixture fails the check first" 1
set +e; OUT="$(bash "$REWRITE" --root "$D" 2>&1)"; RC=$?; set -e
expect_rc "the rewrite exits 0" 0
run "$D"; expect_rc "the rewritten tree passes the check" 0
OLD="$D/skills/demo/OLD.md"
expect_has() { if has -F -- "$2" < "$OLD"; then ok "$1"; else fail "$1: OLD.md lacks $2"; sed 's/^/      | /' "$OLD"; fi; }
expect_has "toolkit root, \$HOME form"  'read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md`'
expect_has "toolkit root, SHARED_DIR"   'SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"'
expect_has "toolkit root, ~ form"       'TK="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}"'
expect_has "<repo-slug> -> AW_DIR"      'Report: $AW_DIR/reviews/<pr>.json'
expect_has "<slug> -> AW_DIR"           'Slug form: $AW_DIR/plans/x.md'
expect_has "\$REPO_SLUG -> AW_DIR"      'mkdir -p "$AW_DIR"'
expect_has "\${REPO_SLUG} -> AW_DIR"    'Braced: $AW_DIR/qa/x.md'
expect_has "an already-new line stays"  'Already new: ${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills'
expect_has "judge config path stays"    'Untouched: ~/.agentic-workflow/judge/config.json and .agentic-workflow/deploy.json'

echo "idempotence"
SUM1="$(tree_sum "$D")"
set +e; OUT="$(bash "$REWRITE" --root "$D" 2>&1)"; RC=$?; set -e
SUM2="$(tree_sum "$D")"
expect_rc "a second run exits 0" 0
if [ "$SUM1" = "$SUM2" ]; then ok "a second run changes nothing"; else fail "a second run changed files"; fi
expect_out "a second run reports 0 files" ' 0 file\(s\)'

echo "dry run"
D="$(dirty_fixture dry)"
SUM1="$(tree_sum "$D")"
set +e; OUT="$(bash "$REWRITE" --root "$D" --dry-run 2>&1)"; RC=$?; set -e
SUM2="$(tree_sum "$D")"
expect_rc "--dry-run exits 0" 0
expect_out "--dry-run names the file" 'would change .*OLD\.md'
if [ "$SUM1" = "$SUM2" ]; then ok "--dry-run writes nothing"; else fail "--dry-run changed files"; fi

echo "this repo"
run "$REPO"; expect_rc "the repo's skills/ and config/voice/ pass" 0

echo
echo "passed: $PASS, failed: $FAIL"
[ "$FAIL" -eq 0 ]
