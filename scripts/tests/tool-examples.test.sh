#!/usr/bin/env bash
# Tests for scripts/check-tool-examples.mjs (issue 4249): the registered Sindri MCP tool examples in
# config/tool-examples/sindri-mcp.json are checked against the last-known tools/list golden
# (tests/Aw.Sindri.Tests/Golden/Mcp/tools-list/default.json), and every `mcp: sindri/<tool>` in skills/ and bootstrap/
# must name a tool in that golden and have a registered example. The real tree passes; fixture trees with a small
# golden fail on an obsolete argument, a wrong type, an enum miss, a missing required field, a removed tool, a skill
# naming an unknown tool, a call site with no example and a stale source line, each with its location and field list.
# A missing golden is "unknown" (exit 1), never a pass. Usage errors exit 2.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-tool-examples.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/tool-examples-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1

run() { set +e; OUT="$(node "$CHECK" "$@" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_no() { if printf '%s\n' "$OUT" | has -F -- "$2"; then fail "$1: output has '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; else ok "$1"; fi; }

GOLDEN_REL=tests/Aw.Sindri.Tests/Golden/Mcp/tools-list/default.json
EX_REL=config/tool-examples/sindri-mcp.json

# tree NAME: a fixture root with a small golden (t_query, t_node), one skill naming both, and examples for both call sites.
tree() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/$(dirname "$GOLDEN_REL")" "$d/$(dirname "$EX_REL")" "$d/skills/demo" "$d/bootstrap"
  cat > "$d/$GOLDEN_REL" << 'EOF'
[
  {"name": "t_query", "description": "secret prose that must never be printed",
   "inputSchema": {"type": "object", "additionalProperties": false,
     "properties": {"q": {"type": "string"}, "depth": {"type": "integer", "minimum": 0},
                    "mode": {"type": "string", "enum": ["fast", "deep"]}}}},
  {"name": "t_node", "description": "x",
   "inputSchema": {"type": "object", "additionalProperties": false,
     "properties": {"key": {"type": "string"}, "limit": {"type": "integer"}}, "required": ["key"]}}
]
EOF
  printf '# Demo\n\nAsk `mcp: sindri/t_query` first.\nThen `mcp: sindri/t_node` for the node.\n' > "$d/skills/demo/SKILL.md"
  printf '# Bootstrap\n\nNo Sindri calls here.\n' > "$d/bootstrap/SKILL.md"
  examples "$d" \
    '{"tool": "t_query", "args": {"q": "where is x", "mode": "fast"}, "source": "skills/demo/SKILL.md:3"}' \
    '{"tool": "t_node", "args": {"key": "checkout"}, "source": "skills/demo/SKILL.md:4"}'
  echo "$d"
}
# examples ROOT JSON...: write the examples file
examples() {
  local d="$1"; shift
  local IFS=,
  printf '{"examples": [%s]}\n' "$*" > "$d/$EX_REL"
}

# --- the real tree ----------------------------------------------------------------------------------------------------
run
expect_rc "the real examples and skills pass" 0
expect_out "the output names the golden it read" "last-known: $GOLDEN_REL at "
if [ -f "$REPO/$EX_REL" ] && [ "$(node -e 'console.log(JSON.parse(require("fs").readFileSync(process.argv[1])).examples.length)' "$REPO/$EX_REL")" -ge 1 ]; then
  ok "the real file registers examples"
else fail "the real file registers examples: $REPO/$EX_REL missing or empty"; fi

# --- a good fixture ---------------------------------------------------------------------------------------------------
D="$(tree good)"
run --root "$D"
expect_rc "a good fixture passes" 0
expect_out "a fixture outside git is labelled" "last-known: $GOLDEN_REL at "

# --- an obsolete argument --------------------------------------------------------------------------------------------
D="$(tree obsolete)"
examples "$D" \
  '{"tool": "t_query", "args": {"q": "x", "lens": "money"}, "source": "skills/demo/SKILL.md:3"}' \
  '{"tool": "t_node", "args": {"key": "k"}, "source": "skills/demo/SKILL.md:4"}'
run --root "$D"
expect_rc "an obsolete argument fails" 1
expect_out "...with its location and the field" "$EX_REL: examples[0] (t_query, from skills/demo/SKILL.md:3): unknown field \"lens\""
expect_out "...and the fields the tool takes" "t_query takes q, depth, mode"
expect_no "...never the tool's description" "secret prose"

# --- a wrong type ----------------------------------------------------------------------------------------------------
D="$(tree wrongtype)"
examples "$D" \
  '{"tool": "t_query", "args": {"q": "x", "depth": "two"}, "source": "skills/demo/SKILL.md:3"}' \
  '{"tool": "t_node", "args": {"key": "k"}, "source": "skills/demo/SKILL.md:4"}'
run --root "$D"
expect_rc "a wrong type fails" 1
expect_out "...naming the field and the type" "examples[0] (t_query, from skills/demo/SKILL.md:3): field \"depth\" must be integer"

# --- an enum miss ----------------------------------------------------------------------------------------------------
D="$(tree enum)"
examples "$D" \
  '{"tool": "t_query", "args": {"q": "x", "mode": "slow"}, "source": "skills/demo/SKILL.md:3"}' \
  '{"tool": "t_node", "args": {"key": "k"}, "source": "skills/demo/SKILL.md:4"}'
run --root "$D"
expect_rc "an enum miss fails" 1
expect_out "...naming the allowed values" "field \"mode\" must be one of fast, deep"

# --- a missing required field ------------------------------------------------------------------------------------------
D="$(tree required)"
examples "$D" \
  '{"tool": "t_query", "args": {"q": "x"}, "source": "skills/demo/SKILL.md:3"}' \
  '{"tool": "t_node", "args": {"limit": 3}, "source": "skills/demo/SKILL.md:4"}'
run --root "$D"
expect_rc "a missing required field fails" 1
expect_out "...naming it" "examples[1] (t_node, from skills/demo/SKILL.md:4): missing required field \"key\""
expect_out "...and marking it in the field list" "t_node takes key (required), limit"

# --- a removed tool ----------------------------------------------------------------------------------------------------
D="$(tree removed)"
examples "$D" \
  '{"tool": "t_query", "args": {"q": "x"}, "source": "skills/demo/SKILL.md:3"}' \
  '{"tool": "t_node", "args": {"key": "k"}, "source": "skills/demo/SKILL.md:4"}' \
  '{"tool": "t_gone", "args": {}, "source": "skills/demo/SKILL.md:3"}'
run --root "$D"
expect_rc "an example of a removed tool fails" 1
expect_out "...as unknown, never a pass" "examples[2] (t_gone, from skills/demo/SKILL.md:3): unknown: t_gone is not in the last-known golden"

# --- a skill naming an unknown tool -------------------------------------------------------------------------------------
D="$(tree unknown-skill)"
printf 'Then `mcp: sindri/t_lenses` too.\n' >> "$D/skills/demo/SKILL.md"
run --root "$D"
expect_rc "a skill naming an unknown tool fails" 1
expect_out "...with file:line" "skills/demo/SKILL.md:5: mcp: sindri/t_lenses names no tool in the last-known golden"
D="$(tree unknown-bootstrap)"
printf 'See `mcp: sindri/t_old`.\n' >> "$D/bootstrap/SKILL.md"
run --root "$D"
expect_rc "bootstrap naming an unknown tool fails" 1
expect_out "...with file:line" "bootstrap/SKILL.md:4: mcp: sindri/t_old names no tool"

# --- a call site with no example, and a stale source --------------------------------------------------------------------
D="$(tree nocover)"
examples "$D" '{"tool": "t_query", "args": {"q": "x"}, "source": "skills/demo/SKILL.md:3"}'
run --root "$D"
expect_rc "a call site with no example fails" 1
expect_out "...naming it" "skills/demo/SKILL.md:4: mcp: sindri/t_node has no example in $EX_REL"
D="$(tree stale)"
examples "$D" \
  '{"tool": "t_query", "args": {"q": "x"}, "source": "skills/demo/SKILL.md:3"}' \
  '{"tool": "t_node", "args": {"key": "k"}, "source": "skills/demo/SKILL.md:1"}'
run --root "$D"
expect_rc "a source line that doesn't name the tool fails" 1
expect_out "...naming the line" "examples[1] (t_node, from skills/demo/SKILL.md:1): the source line does not name t_node"

D="$(tree moved)"
printf '# Demo\n\nA new line.\nAsk `mcp: sindri/t_query` first.\nThen `mcp: sindri/t_node` for the node.\n' > "$D/skills/demo/SKILL.md"
run --root "$D"
expect_rc "a call that moved down a line fails" 1
expect_out "...t_node hint names its new line" 'the call moved to skills/demo/SKILL.md:5: set "source" to that line in config/tool-examples/sindri-mcp.json'
expect_out "...t_query hint names its new line" 'the call moved to skills/demo/SKILL.md:4: set "source" to that line in config/tool-examples/sindri-mcp.json'
D="$(tree gone)"
printf '# Demo\n\nAsk `mcp: sindri/t_query` first.\nThe node call was removed.\n' > "$D/skills/demo/SKILL.md"
run --root "$D"
expect_rc "a call removed from its skill fails" 1
expect_out "...the hint says to fix or remove the example" 'skills/demo/SKILL.md has no mcp: sindri/t_node line now: fix or remove the example in config/tool-examples/sindri-mcp.json'
D="$(tree several)"
printf '# Demo\n\nAsk `mcp: sindri/t_query` first.\nThen look at the node.\nThen `mcp: sindri/t_node` for the node.\nAgain `mcp: sindri/t_node` for more.\n' > "$D/skills/demo/SKILL.md"
run --root "$D"
expect_rc "a call repeated in its skill fails" 1
expect_out "...the hint lists the lines" 'skills/demo/SKILL.md names mcp: sindri/t_node on lines 5, 6: set "source" in config/tool-examples/sindri-mcp.json to the one this example shows'
# a source past the end of a file that got shorter still says where the call went
D="$(tree pastend)"
examples "$D" \
  '{"tool": "t_query", "args": {"q": "x"}, "source": "skills/demo/SKILL.md:3"}' \
  '{"tool": "t_node", "args": {"key": "k"}, "source": "skills/demo/SKILL.md:40"}'
run --root "$D"
expect_rc "a source past the end of the file fails" 1
expect_out "...and says where the call is" 'examples[1] (t_node, from skills/demo/SKILL.md:40): the source line does not exist; the call moved to skills/demo/SKILL.md:4: set "source" to that line in config/tool-examples/sindri-mcp.json'

# --- a missing golden is unknown ------------------------------------------------------------------------------------------
D="$(tree nogolden)"
rm "$D/$GOLDEN_REL"
run --root "$D"
expect_rc "a missing golden exits 1" 1
expect_out "...and says unknown" "unknown: $GOLDEN_REL is missing"
expect_no "...never passes" "ok:"

# --- usage ------------------------------------------------------------------------------------------------------------
run --bogus
expect_rc "an unknown flag is usage" 2
run --root
expect_rc "--root without a value is usage" 2

echo "tool-examples: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
