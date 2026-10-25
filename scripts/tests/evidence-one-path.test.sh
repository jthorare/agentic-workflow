#!/usr/bin/env bash
# The skill and the recipe stay one path (cutover spec §3.4): no second Playwright, and SKILL.md runs a check only through
# a verb of the ui-evidence binary. The per-step briefs are checked by the Sindri unit lane (EvidenceAgentGrantsTests).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SKILL="$ROOT/skills/ui-evidence/SKILL.md"
fail=0
bad() { echo "FAIL: $*"; fail=1; }

verbs="$(grep -h 'internal const string Name = "' "$ROOT"/src/Evidence/Aw.Evidence.Host/Commands/*Command.cs | sed 's/.*Name = "\([a-z-]*\)".*/\1/' | sort -u)"
[ -n "$verbs" ] || bad "found no ui-evidence verbs under src/Evidence/Aw.Evidence.Host/Commands"

# 1. Playwright lives in src/Evidence only (and its tests); the central version pin and lock files are not code.
hits="$(cd "$ROOT" && grep -rl 'Microsoft\.Playwright' --include='*.cs' --include='*.csproj' src tests 2>/dev/null | grep -v '^src/Evidence/' | grep -v '^tests/Aw\.Evidence' || true)"
[ -z "$hits" ] || bad "Playwright outside src/Evidence: $hits"

# 2. SKILL.md names no other way to run a check.
for banned in 'node dist' 'npx ' 'npm ' 'with_stack_lock' 'doctor\.sh' 'dist/bin\.js'; do
  if grep -q -- "$banned" "$SKILL"; then bad "SKILL.md still names '$banned'"; fi
done

# 3. Every 'ui-evidence <verb>' in SKILL.md and in the recipes is a verb of the binary.
check_verbs() {
  local file="$1" verb
  while read -r verb; do
    [ -n "$verb" ] || continue
    echo "$verbs" | grep -qx -- "$verb" || bad "$file names 'ui-evidence $verb', which is not a verb of the binary"
  done < <(grep -o 'ui-evidence [a-z][a-z-]*' "$file" | awk '{print $2}' | sort -u | grep -vx -e skill -e binary -e is -e and -e runs -e verb || true)
}
check_verbs "$SKILL"
for recipe in "$ROOT"/recipes/sindri/*.yaml; do check_verbs "$recipe"; done

[ "$fail" -eq 0 ] || exit 1
echo "PASS: the ui-evidence skill and the recipes use one code path"
