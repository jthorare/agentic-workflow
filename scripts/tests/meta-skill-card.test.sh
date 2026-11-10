#!/usr/bin/env bash
# #2498: skills/meta/SKILL.md is read every meta turn. It must open with one short card per harness
# and stay small; the long reference lives in docs/csharp/meta.md and docs/csharp/meta-reference-extra.md.
set -u
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
skill="$root/skills/meta/SKILL.md"
fail=0

max=14000
size=$(wc -c <"$skill")
[ "$size" -le "$max" ] || { echo "FAIL: SKILL.md is $size bytes, cap $max"; fail=1; }
for h in "Claude Code" "Codex" "Cursor"; do
  grep -qE "^### Card: $h" "$skill" || { echo "FAIL: no '### Card: $h' section"; fail=1; }
done
first_card=$(grep -nE '^### Card:' "$skill" | head -1 | cut -d: -f1)
first_h2=$(grep -nE '^## ' "$skill" | head -1 | cut -d: -f1)
[ -n "$first_card" ] && [ -n "$first_h2" ] && [ "$first_card" -le $((first_h2 + 12)) ] || { echo "FAIL: cards must open the skill"; fail=1; }
grep -qE 'trust the .?FAIL.? lines' "$skill" || { echo "FAIL: skill must say to trust the CLI FAIL lines"; fail=1; }
grep -qE "open(ing)? (the )?gate log" "$skill" || { echo "FAIL: skill must say not to open gate logs"; fail=1; }
[ -f "$root/docs/csharp/meta-reference-extra.md" ] || { echo "FAIL: moved detail has no home (meta-reference-extra.md)"; fail=1; }
[ "$fail" -eq 0 ] && echo "PASS"
exit "$fail"
