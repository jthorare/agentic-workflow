#!/usr/bin/env bash
# Tests for scripts/check-line-schemas.mjs (the line's report schemas, LN-Y1): the repo passes, and each kind of drift fails.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-line-schemas.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-line-schemas-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# Ajv is a dev dependency of scripts/ (scripts/package.json); installed once under a lock, since suites run in parallel.
# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1

run() { set +e; OUT="$(node "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# A throwaway copy of the files the check reads.
copy() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/schemas"
  cp -R "$REPO/schemas/line" "$d/schemas/"
  cp -R "$REPO/schemas/pr" "$d/schemas/"
  echo "$d"
}

# Rewrites a JSON file in a copy through a jq filter.
edit() { local f="$1"; shift; jq "$@" "$f" > "$f.new"; mv "$f.new" "$f"; }

echo "repo"
run "$REPO"; expect_rc "the repo's line schemas pass" 0; expect_out "prints ok" "check-line-schemas: ok"

echo "drift"
d="$(copy verdict)"; echo '{"head": 5}' > "$d/schemas/line/examples/built.two-commits.json"
run "$d"; expect_rc "a valid example the schema refuses fails" 1; expect_out "names the example" "built.two-commits.json"

d="$(copy relax)"; edit "$d/schemas/line/holdout-sets.json" '.items.additionalProperties = true'
run "$d"; expect_rc "a schema that accepts an invalid example fails" 1; expect_out "names the invalid example" "holdout-sets.with-text.json"

d="$(copy unlisted)"; echo '{}' > "$d/schemas/line/examples/extra.json"
run "$d"; expect_rc "an example missing from the manifest fails" 1; expect_out "names the file" "extra.json"

d="$(copy broken)"; echo '{"type": 5}' > "$d/schemas/line/built.json"
run "$d"; expect_rc "a schema that does not compile fails" 1; expect_out "names the schema" "built.json"

d="$(copy nosource)"; edit "$d/schemas/line/examples/manifest.json" '.examples[0].source = ""'
run "$d"; expect_rc "an example with no source fails" 1; expect_out "names the requirement" "names no source"

d="$(copy noinvalid)"; edit "$d/schemas/line/examples/manifest.json" '.examples |= map(select(.schema != "built.json" or .valid))'
run "$d"; expect_rc "a schema with no invalid example fails" 1; expect_out "names the schema" "built.json: has no invalid example"

d="$(copy prrelax)"; edit "$d/schemas/pr/reply.json" '.additionalProperties = true | .required = []'
run "$d"; expect_rc "a pr schema that accepts an invalid example fails" 1; expect_out "names the invalid example" "reply.no-body.json"

echo
echo "passed $PASS, failed $FAIL"
[ "$FAIL" -eq 0 ]
