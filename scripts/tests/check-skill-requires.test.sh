#!/usr/bin/env bash
# Tests for scripts/check-skill-requires.sh using temp-dir fixture trees.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

SCRIPTS="$(cd "$(dirname "$0")/.." && pwd)"
CHECK="$SCRIPTS/check-skill-requires.sh"
REPO="$(cd "$SCRIPTS/.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-skill-requires-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

run() { local root="$1"; shift; set +e; OUT="$(bash "$CHECK" --root "$root" "$@" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -E -- "$2"; then ok "$1"; else fail "$1: output lacks /$2/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

GATE='[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"'
GATE_DIR='echo "AW_DIR=${AW_DIR:-}"'
OPENING='Every run ends with the [Result block](#result-block), whatever the outcome. Run its gate now, before the first step. Under `AW_RESULT=1` your final reply opens with the `aw-result` block.'

# skill DIR NAME REQUIRES_TEXT|- : a skill with a Result block; "-" writes no requires.yaml
skill() {
  local d="$1/skills/$2"
  mkdir -p "$d"
  printf -- '---\nname: %s\n---\n%s\n\nBody.\n\n## Result block\n\n```bash\n%s\n```\n\nKeys: `skill`.\n\n## Next steps\n\n- none\n' "$2" "$OPENING" "$GATE"$'\n'"$GATE_DIR" > "$d/SKILL.md"
  if [ "$3" != "-" ]; then printf '%s\n' "$3" > "$d/requires.yaml"; fi
}

# A compliant tree: one agent, an MCP table with two servers, one skill that uses all of it.
fixture() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/skills/_shared" "$d/config/agents" "$d/.agents/rules" "$d/bootstrap"
  : > "$d/config/agents/qa-runner.md"
  cat > "$d/.agents/rules/mcp-servers.md" <<'MD'
| Server | Purpose | Use instead of |
|--------|---------|----------------|
| `serena` | LSP | search |
| `playwright` | Browser | manual |
MD
  skill "$d" good $'bins: [sh, cat]\nagents: [qa-runner]\nmcp: [serena, playwright]\nskills: [superpowers:writing-plans]'
  printf '%s' "$d"
}

echo "check-skill-requires.sh"

T="$(fixture good)"
run "$T"; expect_rc "compliant tree passes" 0
if [ -z "$OUT" ]; then ok "compliant tree prints nothing"; else fail "compliant tree printed: $OUT"; fi

T="$(fixture no-aw-dir)"; skill "$T" nodir '{}'; sed -i '/echo "AW_DIR=/d' "$T/skills/nodir/SKILL.md"
run "$T"; expect_rc "a gate that does not print AW_DIR fails" 1
expect_out "a gate without AW_DIR is named" "skills/nodir/SKILL.md: the Result block's gate does not print AW_DIR"

T="$(fixture empty-braces)"; skill "$T" none '{}'
run "$T"; expect_rc "a skill with no dependencies is {}" 0

T="$(fixture no-sidecar)"; skill "$T" nosidecar -
run "$T"; expect_rc "missing sidecar fails" 1
expect_out "missing sidecar names the file" 'skills/nosidecar/requires.yaml: missing'

T="$(fixture empty-file)"; skill "$T" blank '{}'
: > "$T/skills/blank/requires.yaml"
run "$T"; expect_rc "empty requires.yaml fails" 1
expect_out "empty file reason" 'blank/requires.yaml: empty file'

T="$(fixture unknown-key)"; skill "$T" odd $'bins: [sh]\nneeds: [x]'
run "$T"; expect_rc "unknown key fails" 1
expect_out "unknown key reason" 'odd/requires.yaml: unknown key needs'

T="$(fixture unparseable)"; skill "$T" junk 'bins sh'
run "$T"; expect_rc "unparseable line fails" 1
expect_out "unparseable reason" 'junk/requires.yaml: unparseable line 1'

T="$(fixture unknown-agent)"; skill "$T" spawn 'agents: [ghost-agent]'
run "$T"; expect_rc "unknown agent fails" 1
expect_out "unknown agent reason" 'spawn/requires.yaml: unknown agent ghost-agent'

T="$(fixture unknown-mcp)"; skill "$T" calls 'mcp: [nope]'
run "$T"; expect_rc "unknown mcp fails" 1
expect_out "unknown mcp reason" 'calls/requires.yaml: unknown mcp server nope'

T="$(fixture mock-mcp)"; skill "$T" calls 'mcp: [linear]'; mkdir -p "$T/evals/mocks/linear"
run "$T"; expect_rc "an mcp server with a mock passes" 0

T="$(fixture unknown-bin)"; skill "$T" runs 'bins: [no-such-binary-xyz]'
run "$T"; expect_rc "unknown bin fails" 1
expect_out "unknown bin reason" 'runs/requires.yaml: bin no-such-binary-xyz is not on PATH'
run "$T" --skip-bins; expect_rc "--skip-bins ignores bins" 0

T="$(fixture stub-bin)"; skill "$T" runs 'bins: [no-such-binary-xyz]'
mkdir -p "$T/evals/stubs/bin"; printf '#!/bin/sh\n' > "$T/evals/stubs/bin/no-such-binary-xyz"; chmod +x "$T/evals/stubs/bin/no-such-binary-xyz"
run "$T"; expect_rc "a bin found in evals/stubs/bin passes" 0

T="$(fixture no-gate)"; skill "$T" nogate '{}'
printf -- '---\nname: nogate\n---\nBody.\n\n## Result block\n\nKeys: `skill`.\n' > "$T/skills/nogate/SKILL.md"
run "$T"; expect_rc "Result block without the gate line fails" 1
expect_out "gate line reason" "nogate/SKILL.md: no '## Result block' section with the gate line"

T="$(fixture gate-elsewhere)"; skill "$T" misplaced '{}'
printf -- '---\nname: misplaced\n---\n%s\n\n## Result block\n\nKeys: `skill`.\n' "$GATE" > "$T/skills/misplaced/SKILL.md"
run "$T"; expect_rc "gate line outside the Result block fails" 1

T="$(fixture two-blocks)"; skill "$T" twice '{}'
printf -- '\n## Result block\n\n```bash\n%s\n```\n' "$GATE" >> "$T/skills/twice/SKILL.md"
run "$T"; expect_rc "two Result block sections fail" 1
expect_out "two-blocks reason" "twice/SKILL.md: 2 '## Result block' sections \\(keep one\\)"

T="$(fixture no-block)"; skill "$T" noblock '{}'
printf -- '---\nname: noblock\n---\nBody.\n' > "$T/skills/noblock/SKILL.md"
run "$T"; expect_rc "no Result block at all fails" 1

# in_block FILE LINE: put LINE inside FILE's Result block (skill() closes it with `## Next steps`)
in_block() { awk -v line="$2" '/^## Next steps/ { print line; print "" } { print }' "$1" > "$1.tmp" && mv "$1.tmp" "$1"; }

T="$(fixture old-order)"; skill "$T" oldorder '{}'
in_block "$T/skills/oldorder/SKILL.md" 'Write the result file and print the same JSON as a fenced `aw-result` block before `## Next steps`.'
run "$T"; expect_rc "aw-result block before Next steps without final reply fails" 1
expect_out "final-reply reason" 'oldorder/SKILL.md: the Result block puts the aw-result block before ## Next steps without saying final reply'

T="$(fixture new-order)"; skill "$T" neworder '{}'
in_block "$T/skills/neworder/SKILL.md" 'Write the result file first; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.'
run "$T"; expect_rc "aw-result block in the final reply passes" 0

# The opening: the gate runs before the first step, so an early stop still ends with the Result block (#2002)
T="$(fixture no-opening)"; skill "$T" noopen '{}'
grep -vF -- '(#result-block)' "$T/skills/noopen/SKILL.md" > "$T/x" && mv "$T/x" "$T/skills/noopen/SKILL.md"
run "$T"; expect_rc "a skill whose opening never runs the gate fails" 1
expect_out "opening reason" 'noopen/SKILL.md: the opening \(before the first ## heading\) does not run the Result block.s gate'

T="$(fixture late-opening)"; skill "$T" lateopen '{}'
grep -vF -- '(#result-block)' "$T/skills/lateopen/SKILL.md" > "$T/x" && mv "$T/x" "$T/skills/lateopen/SKILL.md"
in_block "$T/skills/lateopen/SKILL.md" "$OPENING"
run "$T"; expect_rc "the gate pointer only after the first heading fails" 1
expect_out "late opening reason" 'lateopen/SKILL.md: the opening'

T="$(fixture no-order)"; skill "$T" noorder '{}'
sed -i 's/ Under `AW_RESULT=1` your final reply opens with the `aw-result` block\.//' "$T/skills/noorder/SKILL.md"
run "$T"; expect_rc "an opening that runs the gate but never says the reply opens with the block fails" 1
expect_out "no-order reason" 'noorder/SKILL.md: the opening'

T="$(fixture unswept-slash)"; skill "$T" later '{}'
mkdir -p "$T/scripts"; printf 'skills/later/\n' > "$T/scripts/check-skill-requires.unswept"
run "$T"; expect_rc "an unswept entry with a trailing slash fails" 1
expect_out "trailing slash reason" 'scripts/check-skill-requires.unswept: skills/later/ ends in /'

T="$(fixture unswept)"; skill "$T" later '{}'
grep -vF -- '(#result-block)' "$T/skills/later/SKILL.md" > "$T/x" && mv "$T/x" "$T/skills/later/SKILL.md"
mkdir -p "$T/scripts"; printf '# not swept yet\n\nskills/later\n' > "$T/scripts/check-skill-requires.unswept"
run "$T"; expect_rc "a skill on the unswept list may lack the opening" 0

T="$(fixture unswept-stale)"; skill "$T" done '{}'
mkdir -p "$T/scripts"; printf 'skills/done\n' > "$T/scripts/check-skill-requires.unswept"
run "$T"; expect_rc "a listed skill that already has the opening fails" 1
expect_out "stale entry reason" 'scripts/check-skill-requires.unswept: skills/done already runs the gate in its opening \(drop it from the list\)'

T="$(fixture unswept-unknown)"
mkdir -p "$T/scripts"; printf 'skills/ghost\n' > "$T/scripts/check-skill-requires.unswept"
run "$T"; expect_rc "a listed skill that does not exist fails" 1
expect_out "unknown entry reason" 'scripts/check-skill-requires.unswept: skills/ghost has no SKILL.md'

T="$(fixture bootstrap)"
printf -- '---\nname: bootstrap\n---\n\n## Result block\n\nKeys: `skill`.\n' > "$T/bootstrap/SKILL.md"
run "$T"; expect_rc "bootstrap/SKILL.md is checked too" 1
expect_out "bootstrap findings name the sidecar" 'bootstrap/requires.yaml: missing'

T="$(fixture non-skill-dir)"; mkdir -p "$T/skills/helpers"; echo x > "$T/skills/helpers/notes.md"
run "$T"; expect_rc "a skills/ directory without SKILL.md is not a skill" 0

echo "usage and setup errors"
run "$TMPROOT" --bogus; expect_rc "unknown flag exits 2" 2
run "$TMPROOT/does-not-exist"; expect_rc "bad --root exits 2" 2
mkdir -p "$TMPROOT/bare"; run "$TMPROOT/bare"; expect_rc "no skills/ exits 2" 2

echo "the real tree"
run "$REPO" --skip-bins; expect_rc "every real skill has a sidecar and a Result block" 0

echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
