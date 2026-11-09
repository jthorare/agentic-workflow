#!/usr/bin/env bash
# Tests for scripts/check-handoff-schema.mjs (the handoff inbox contract): the repo passes, and each kind of drift fails.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-handoff-schema.mjs"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-handoff-test.XXXXXX")"
cleanup() { [ -d "$TMPROOT" ] && rm -r "$TMPROOT"; return 0; }
trap cleanup EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# Ajv and yaml are dev dependencies of scripts/ (scripts/package.json); installed once under a lock, since suites run in parallel.
# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || exit 1

run() { set +e; OUT="$(node "$CHECK" --root "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# A throwaway copy of the files the check reads.
copy() {
  local d="$TMPROOT/$1"
  mkdir -p "$d/schemas" "$d/tests/fixtures/handoff" "$d/docs/sindri"
  cp -R "$REPO/schemas/handoff" "$d/schemas/"
  cp -R "$REPO/tests/fixtures/handoff/fake-inbox" "$d/tests/fixtures/handoff/"
  cp "$REPO/docs/sindri/handoff.md" "$d/docs/sindri/"
  echo "$d"
}

echo "repo"
run "$REPO"; expect_rc "the repo's contract, examples and fixtures pass" 0; expect_out "prints ok" "check-handoff-schema: ok"

echo "drift"
d="$(copy verdict)"
cp "$d/tests/fixtures/handoff/fake-inbox/offer-full.json" "$d/tests/fixtures/handoff/fake-inbox/offer-extra-field.json"
run "$d"; expect_rc "a fixture whose verdict differs from its manifest entry fails" 1; expect_out "names the fixture" "offer-extra-field.json: expected invalid"

d="$(copy unlisted)"
echo '{}' > "$d/tests/fixtures/handoff/fake-inbox/stray.json"
run "$d"; expect_rc "a fixture missing from the manifest fails" 1; expect_out "names it" "stray.json: not listed in manifest.json"

d="$(copy noclaim)"
perl -0pi -e 's/  \/offers\/\{id\}\/claim:/  \/offers\/{id}\/claimed:/' "$d/schemas/handoff/inbox.openapi.yaml"
run "$d"; expect_rc "a renamed claim path fails" 1; expect_out "names the missing operation" "no POST /offers/{id}/claim"

d="$(copy status)"
perl -0pi -e 's/        "409":\n          description: Another caller claimed it first. Nothing starts.\n//' "$d/schemas/handoff/inbox.openapi.yaml"
run "$d"; expect_rc "a claim without its 409 fails" 1; expect_out "names it" "claimOffer has no 409 response"

d="$(copy example)"
perl -0pi -e 's/title: Fix the login timeout\n                        repo: web\n                        branch: fix\/login-timeout\n                        prompt: Users are logged out after 5 minutes. Make it 30.\n                        links: \[\{ label: Ticket/repo: web\n                        links: [{ label: Ticket/' "$d/schemas/handoff/inbox.openapi.yaml"
run "$d"; expect_rc "a spec example that breaks its own schema fails" 1; expect_out "names the example" "example 'twoOffers' is not a valid OfferPage"

d="$(copy docs)"
perl -pi -e 's#/offers/\{id\}/result#/offers/{id}/answer#g' "$d/docs/sindri/handoff.md"
run "$d"; expect_rc "docs that do not name a path fail" 1; expect_out "names it" "does not name /offers/{id}/result"

d="$(copy bad-yaml)"
printf 'openapi: [unclosed\n' > "$d/schemas/handoff/inbox.openapi.yaml"
run "$d"; expect_rc "a file that does not parse fails" 1; expect_out "says so" "does not parse"

echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
