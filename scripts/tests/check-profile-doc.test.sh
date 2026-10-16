#!/usr/bin/env bash
# Tests for scripts/check-profile-doc.mjs: docs/csharp/sindri-profile.md names every key of the C# SindriProfile.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-profile-doc.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-profile-doc-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
run() { set +e; OUT="$(node "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | grep -qF -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

echo "repo"
run "$REPO"; expect_rc "docs/csharp/sindri-profile.md names every SindriProfile key" 0

echo "drift"
d="$TMPROOT/new-key"
mkdir -p "$d/src/Sindri/Aw.Sindri.Core" "$d/docs/csharp"
cp -R "$REPO/src/Sindri/Aw.Sindri.Core/Profile" "$d/src/Sindri/Aw.Sindri.Core/"
cp "$REPO/docs/csharp/sindri-profile.md" "$d/docs/csharp/"
perl -pi -e 's/IndexSettings\? Index = null\)\n/IndexSettings? Index = null,\n    string? Launch = null)\n/' "$d/src/Sindri/Aw.Sindri.Core/Profile/SindriProfile.cs"
run "$d"; expect_rc "a new profile key the doc lacks fails" 1
expect_out "names the key and the fix" "add launch to docs/csharp/sindri-profile.md"

echo
echo "check-profile-doc.test.sh: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
