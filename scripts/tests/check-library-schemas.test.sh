#!/usr/bin/env bash
# Tests for scripts/check-library-schemas.mjs (the design-doc schema, PR review page spec Ph0): the repo passes, and each kind of drift fails.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-library-schemas.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-library-schemas-test.XXXXXX")"
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
  cp -R "$REPO/schemas/library" "$d/schemas/"
  echo "$d"
}

# Rewrites a JSON file in a copy through a jq filter.
edit() { local f="$1"; shift; jq "$@" "$f" > "$f.new"; mv "$f.new" "$f"; }

S=design-doc.schema.json
E=schemas/library/examples

echo "repo"
run "$REPO"; expect_rc "the repo's design-doc schema passes" 0; expect_out "prints ok" "check-library-schemas: ok"

echo "drift"
d="$(copy verdict)"; edit "$d/$E/spec.items.json" '.frontMatter.kind = "rfc"'
run "$d"; expect_rc "a valid example the schema refuses fails" 1; expect_out "names the example" "spec.items.json"

d="$(copy relax-kind)"; edit "$d/schemas/library/$S" '.["$defs"].frontMatter.properties.kind = {"type": "string"}'
run "$d"; expect_rc "a schema that accepts an unknown kind fails" 1; expect_out "names the invalid example" "kind.unknown.json"

d="$(copy relax-items)"; edit "$d/schemas/library/$S" '.allOf |= .[:1]'
run "$d"; expect_rc "a schema that keeps items beside a parse error fails" 1; expect_out "names the invalid example" "items.beside-error.json"

d="$(copy relax-phase)"; edit "$d/schemas/library/$S" '.["$defs"].item.allOf[1].then.properties.id.pattern = "^Ph[0-9]+"'
run "$d"; expect_rc "a schema that stores Ph01 fails" 1; expect_out "names the invalid example" "phase.leading-zero.json"

d="$(copy wrong-path)"; edit "$d/$E/manifest.json" '(.examples[] | select(.file == "kind.unknown.json") | .failsAt) = "/frontMatter/id"'
run "$d"; expect_rc "an invalid example that fails somewhere else fails" 1; expect_out "says where it failed" "expected to fail at /frontMatter/id, fails at /frontMatter/kind"

d="$(copy no-path)"; edit "$d/$E/manifest.json" '(.examples[] | select(.file == "kind.unknown.json")) |= del(.failsAt)'
run "$d"; expect_rc "an invalid example with no failsAt fails" 1; expect_out "names the requirement" "names no failsAt"

d="$(copy unlisted)"; echo '{}' > "$d/$E/extra.json"
run "$d"; expect_rc "an example missing from the manifest fails" 1; expect_out "names the file" "extra.json"

d="$(copy broken)"; echo '{"type": 5}' > "$d/schemas/library/$S"
run "$d"; expect_rc "a schema that does not compile fails" 1; expect_out "names the schema" "$S"

d="$(copy nosource)"; edit "$d/$E/manifest.json" '.examples[0].source = ""'
run "$d"; expect_rc "an example with no source fails" 1; expect_out "names the requirement" "names no source"

d="$(copy noinvalid)"; edit "$d/$E/manifest.json" '.examples |= map(select(.valid))'
run "$d"; expect_rc "a schema with no invalid example fails" 1; expect_out "names the schema" "$S: has no invalid example"

echo
echo "passed $PASS, failed $FAIL"
[ "$FAIL" -eq 0 ]
