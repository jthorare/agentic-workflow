#!/usr/bin/env bash
# Tests for scripts/check-repo-config-schemas.mjs (the per-repo config schemas, issue #2909): the repo passes, and each kind of drift fails.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-repo-config-schemas.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-repo-config-schemas-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1

run() { set +e; OUT="$(node "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# A throwaway copy of the files the check reads.
copy() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/schemas" "$d/tests/fixtures"
  cp -R "$REPO/schemas/repo-config" "$d/schemas/"
  cp -R "$REPO/tests/fixtures/repo-config" "$d/tests/fixtures/"
  echo "$d"
}
edit() { local f="$1"; shift; jq "$@" "$f" > "$f.new"; mv "$f.new" "$f"; }

S=schemas/repo-config
A=tests/fixtures/repo-config/acme/.agentic-workflow
I=tests/fixtures/repo-config/invalid

echo "repo"
run "$REPO"; expect_rc "the repo's schemas pass" 0; expect_out "prints ok" "check-repo-config-schemas: ok"

set +e; OUT="$(node "$CHECK" --root 2>&1)"; RC=$?; set -e
expect_rc "--root with no value is a usage error" 2; expect_out "prints usage" "usage: node scripts/check-repo-config-schemas.mjs"

echo "drift"
d="$(copy acme-bad)"; edit "$d/$A/deploy.json" '.timeout = 0'
run "$d"; expect_rc "an acme file the schema refuses fails" 1; expect_out "names the file" "$A/deploy.json"

d="$(copy acme-missing)"; rm "$d/$A/repo.json"
run "$d"; expect_rc "a schema with no acme file fails" 1; expect_out "names the file" "$A/repo.json: missing"

d="$(copy acme-extra)"; echo '{}' > "$d/$A/other.json"
run "$d"; expect_rc "an acme file with no schema fails" 1; expect_out "names the file" "$A/other.json"

d="$(copy relax-extra)"; edit "$d/$S/canary.schema.json" 'del(.additionalProperties)'
run "$d"; expect_rc "a schema that accepts an unknown key fails" 1; expect_out "names the case" "invalid/canary/unknown-key.json"

d="$(copy wrong-path)"; edit "$d/$I/expected.json" '.["deploy/timeout-zero.json"] = "/command"'
run "$d"; expect_rc "a case that fails somewhere else fails" 1; expect_out "says where" "expected to fail at /command, fails at /timeout"

d="$(copy unlisted)"; echo '{}' > "$d/$I/repo/extra.json"
run "$d"; expect_rc "a case missing from expected.json fails" 1; expect_out "names the file" "repo/extra.json: not listed"

d="$(copy missing-case)"; rm "$d/$I/repo/sibling-number.json"
run "$d"; expect_rc "a listed case with no file fails" 1; expect_out "names the file" "sibling-number.json: listed in expected.json but missing"

d="$(copy broken)"; echo '{"type": 5}' > "$d/$S/worktrees.schema.json"
run "$d"; expect_rc "a schema that does not compile fails" 1; expect_out "names the schema" "worktrees.schema.json"

d="$(copy no-invalid)"; edit "$d/$I/expected.json" 'with_entries(select(.key | startswith("worktrees/") | not))'
rm "$d/$I"/worktrees/*.json
run "$d"; expect_rc "a schema with no invalid case fails" 1; expect_out "names the schema" "worktrees.schema.json: has no invalid case"

echo
echo "passed $PASS, failed $FAIL"
[ "$FAIL" -eq 0 ]
