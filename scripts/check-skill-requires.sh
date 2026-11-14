#!/usr/bin/env bash
# scripts/check-skill-requires.sh — every skill declares its dependencies in a sidecar and ends with a Result block
# (skill-revamp spec §2.5, §2.6).
# Exit 0 when clean, 1 on findings (one `file: reason` per hit), 2 on a usage or setup error.
#
# Checks, per skills/*/SKILL.md and bootstrap/SKILL.md:
#   - requires.yaml sits next to it, parses, and has only the keys bins, agents, mcp, skills (lists of names; `{}` for none)
#   - every agents name is a file in config/agents/<name>.md
#   - every mcp name is a server in the table of .agents/rules/mcp-servers.md or a directory evals/mocks/<name>/
#   - every bins name is on PATH or in evals/stubs/bin/ (--skip-bins skips this one: a box without ffmpeg or bun)
#   - the SKILL.md has exactly one `## Result block` section, holding the exact gate line from skills/_shared/output-contract.md
#   - a Result block line that puts the `aw-result` block before `## Next steps` also says "final reply" (the block goes in the reply after the last tool call; #1941)
#   - the opening (everything before the first `## ` heading) has a line that links `(#result-block)` and says "gate now" and
#     "opens with": the gate runs before the first step, so a run that stops early still ends with the Result block, and the final
#     reply opens with the aw-result block, so the result file never stands in for it (#2002). Skills listed in
#     scripts/check-skill-requires.unswept (one skill directory per line, `#` comments) are skipped until they are swept; a listed
#     skill that already has the opening, or that does not exist, is a finding, so the list only shrinks
#   - a skill with a block.yaml (a Sindri block) has `AW_RESULT_FILE` and `waiting` in its Result block: under AW_RESULT_SKILL it
#     writes the result there and hands a question back as `waiting`, never a picker (skill-flows spec §3.3)
# A skills/ directory without a SKILL.md (_shared, the preamble files) is not a skill.
# Usage: scripts/check-skill-requires.sh [--root DIR] [--skip-bins]
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SKIP_BINS=0
while [ $# -gt 0 ]; do
  case "$1" in
    --root) ROOT="${2:?--root needs a directory}"; shift 2 ;;
    --skip-bins) SKIP_BINS=1; shift ;;
    *) echo "usage: $0 [--root DIR] [--skip-bins]" >&2; exit 2 ;;
  esac
done
cd "$ROOT" || { echo "SKILL-REQUIRES: cannot cd to $ROOT" >&2; exit 2; }
[ -d skills ] || { echo "SKILL-REQUIRES: no skills/ directory in $ROOT" >&2; exit 2; }

GATE='[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"'
UNSWEPT_FILE="scripts/check-skill-requires.unswept"
FINDINGS=0
finding() { printf '%s: %s\n' "$1" "$2"; FINDINGS=$((FINDINGS + 1)); }

# MCP server names: the first backticked cell of each row of the first table in mcp-servers.md, plus evals/mocks/*/.
MCP_KNOWN=" "
if [ -f .agents/rules/mcp-servers.md ]; then
  MCP_KNOWN+="$(sed -n 's/^| `\([a-z0-9_-]*\)` |.*/\1/p' .agents/rules/mcp-servers.md | tr '\n' ' ') "
fi
if [ -d evals/mocks ]; then
  for m in evals/mocks/*/; do [ -d "$m" ] && MCP_KNOWN+="$(basename "$m") "; done
fi

# requires_lines FILE: prints "key<TAB>name" per list item, "BAD<TAB>reason" for anything it cannot read.
# Accepted shape: `{}` alone, or lines `key: [a, b]` (flow list) or `key: []`. Comments and blank lines are ignored.
requires_lines() {
  perl -ne '
    s/\r?\n$//; s/\s+#.*$//; next if /^\s*$/ || /^\s*#/;
    $seen = 1;
    if (/^\{\s*\}$/) { $empty = 1; next }
    if (/^([A-Za-z_]+):\s*\[(.*)\]$/) {
      my ($k, $v) = ($1, $2);
      if ($k !~ /^(bins|agents|mcp|skills)$/) { print "BAD\tunknown key $k\n"; next }
      for my $n (split /\s*,\s*/, $v) { $n =~ s/^\s+|\s+$//g; next if $n eq ""; print "$k\t$n\n" }
      next;
    }
    print "BAD\tunparseable line $.: $_\n";
    END { print "BAD\tempty file (write {} for no dependencies)\n" unless $seen }
  ' "$1"
}

# has_opening FILE: exit 0 when a line before the first `## ` heading links the Result block and says "gate now" and "opens with".
has_opening() {
  awk '/^## / { exit } index($0, "(#result-block)") && index($0, "gate now") && index($0, "opens with") { found = 1; exit } END { exit !found }' "$1"
}

UNSWEPT=" "
if [ -f "$UNSWEPT_FILE" ]; then
  while IFS= read -r entry; do
    entry="${entry%%#*}"; entry="${entry%"${entry##*[![:space:]]}"}"; entry="${entry#"${entry%%[![:space:]]*}"}"
    [ -n "$entry" ] || continue
    case "$entry" in
      */) finding "$UNSWEPT_FILE" "$entry ends in / (write the skill directory without it)"; continue ;;
    esac
    if [ ! -f "$entry/SKILL.md" ]; then
      finding "$UNSWEPT_FILE" "$entry has no SKILL.md (drop it from the list)"
    elif has_opening "$entry/SKILL.md"; then
      finding "$UNSWEPT_FILE" "$entry already runs the gate in its opening (drop it from the list)"
    fi
    UNSWEPT+="$entry "
  done < "$UNSWEPT_FILE"
fi

for skill_md in skills/*/SKILL.md bootstrap/SKILL.md; do
  [ -f "$skill_md" ] || continue
  dir="$(dirname "$skill_md")"
  req="$dir/requires.yaml"

  if [ ! -f "$req" ]; then
    finding "$req" "missing (every skill has a requires.yaml; {} for none)"
  else
    while IFS=$'\t' read -r key name; do
      case "$key" in
        BAD) finding "$req" "$name" ;;
        agents) [ -f "config/agents/$name.md" ] || finding "$req" "unknown agent $name (no config/agents/$name.md)" ;;
        mcp) case "$MCP_KNOWN" in *" $name "*) ;; *) finding "$req" "unknown mcp server $name (not in .agents/rules/mcp-servers.md or evals/mocks/)" ;; esac ;;
        bins)
          if [ "$SKIP_BINS" = 0 ] && ! command -v "$name" >/dev/null 2>&1 && [ ! -x "evals/stubs/bin/$name" ]; then
            finding "$req" "bin $name is not on PATH or in evals/stubs/bin/"
          fi ;;
      esac
    done < <(requires_lines "$req")
  fi

  n_blocks="$(grep -c '^## Result block[[:space:]]*$' "$skill_md" || true)"
  [ "$n_blocks" -le 1 ] || finding "$skill_md" "$n_blocks '## Result block' sections (keep one)"
  if ! awk '/^## Result block[[:space:]]*$/ { in_block = 1; next } in_block && /^## / { in_block = 0 } in_block { print }' "$skill_md" | grep -qxF -- "$GATE"; then
    finding "$skill_md" "no '## Result block' section with the gate line: $GATE"
  fi
  if ! awk '/^## Result block[[:space:]]*$/ { in_block = 1; next } in_block && /^## / { in_block = 0 } in_block { print }' "$skill_md" | grep -qF -- 'echo "AW_DIR='; then
    finding "$skill_md" "the Result block's gate does not print AW_DIR (source skills/_shared/repo-slug.sh in it; #2036)"
  fi
  if awk '/^## Result block[[:space:]]*$/ { in_block = 1; next } in_block && /^## / { in_block = 0 } in_block { print }' "$skill_md" | grep -qF -- 'in your final reply, after the last tool call, before `## Next steps`'; then
    finding "$skill_md" "the Result block puts the aw-result block after the reply's prose; say: open your final reply (the message after the last tool call) with it, before any prose (#2034)"
  fi
  if awk '/^## Result block[[:space:]]*$/ { in_block = 1; next } in_block && /^## / { in_block = 0 } in_block { print }' "$skill_md" | grep -F -- '`aw-result` block' | grep -F -- 'before `## Next steps`' | grep -qvF -- 'final reply'; then
    finding "$skill_md" "the Result block puts the aw-result block before ## Next steps without saying final reply (write every file first, then print it in the final reply, after the last tool call)"
  fi
  if [ -f "$dir/block.yaml" ]; then
    block_text="$(awk '/^## Result block[[:space:]]*$/ { in_block = 1; next } in_block && /^## / { in_block = 0 } in_block { print }' "$skill_md")"
    for word in AW_RESULT_FILE waiting; do
      grep -qF -- "$word" <<<"$block_text" || finding "$skill_md" "has a block.yaml but its Result block never mentions $word (under AW_RESULT_SKILL it writes AW_RESULT_FILE and hands a question back as waiting)"
    done
  fi
  case "$UNSWEPT" in
    *" $dir "*) ;;
    *) has_opening "$skill_md" || finding "$skill_md" "the opening (before the first ## heading) does not run the Result block's gate: add the opening line from skills/_shared/output-contract.md" ;;
  esac
done

[ "$FINDINGS" -eq 0 ]
