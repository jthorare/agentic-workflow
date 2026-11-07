#!/usr/bin/env bash
# Tests for scripts/check-library-fixtures.mjs (the design-doc fixtures, PR review page spec Ph0): the real folder passes, and each kind of drift fails.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK="$REPO/scripts/check-library-fixtures.mjs"
FIX="$REPO/tests/fixtures/library"
PASS=0
FAIL=0

if ! command -v node >/dev/null 2>&1; then echo "SKIP: node is not installed"; exit 0; fi
if ! command -v jq >/dev/null 2>&1; then echo "SKIP: jq is not installed"; exit 0; fi

TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/check-library-fixtures-test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

# Ajv is a dev dependency of scripts/ (scripts/package.json); installed once under a lock, since suites run in parallel.
# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO/scripts/lib/ensure-scripts-deps.sh"
ensure_scripts_deps "$REPO/scripts" || { echo "SKIP: Ajv is not installed in scripts/"; exit 0; }

run() { set +e; OUT="$(node "$CHECK" --dir "$1" 2>&1)"; RC=$?; set -e; }
expect_rc() { if [ "$RC" -eq "$2" ]; then ok "$1 (exit $RC)"; else fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }
expect_out() { if printf '%s\n' "$OUT" | has -F -- "$2"; then ok "$1"; else fail "$1: output lacks '$2'"; printf '%s\n' "$OUT" | sed 's/^/      | /'; fi; }

# A throwaway copy of the fixtures (cp -R keeps symlinks as symlinks).
copy() { local d="$TMPROOT/$1"; mkdir -p "$d"; cp -R "$FIX/." "$d/"; echo "$d"; }
subst() { local f="$1"; shift; sed "$@" "$f" > "$f.new"; mv "$f.new" "$f"; }
edit() { local f="$1"; shift; jq "$@" "$f" > "$f.new"; mv "$f.new" "$f"; }

echo "repo"
run "$FIX"; expect_rc "the repo's fixtures pass" 0; expect_out "prints ok" "check-library-fixtures: ok"
set +e; OUT="$(node "$CHECK" --dir 2>&1)"; RC=$?; set -e
expect_rc "--dir with no value is a usage error" 2; expect_out "prints usage" "usage: node scripts/check-library-fixtures.mjs"

echo "drift"
d="$(copy unlisted)"; printf -- '---\naw-doc: 1\nid: extra-doc\nkind: note\ntitle: "Extra"\nstatus: draft\n---\n' > "$d/vault/extra.md"
run "$d"; expect_rc "a .md that expected.json does not list fails" 1; expect_out "names the file" "vault/extra.md: is a .md under the fixtures but not in expected.json"

d="$(copy missing)"; mv "$d/vault/ferry-ideas.md" "$TMPROOT/ferry-ideas.moved"
run "$d"; expect_rc "a listed file that is missing fails" 1; expect_out "names the file" "vault/ferry-ideas.md: is in expected.json but missing on disk"

d="$(copy stray)"; echo '{}' > "$d/vault/stray.json"
run "$d"; expect_rc "a non-md file that is not in supporting fails" 1; expect_out "names the file" "vault/stray.json: is a file under the fixtures but not in expected.json"

d="$(copy nocode)"; mv "$d/broken/013-two-headings.md" "$TMPROOT/013.moved"; edit "$d/expected.json" '.files |= map(select(.path != "broken/013-two-headings.md")) | del(.codes["SND-LIB-013"])'
run "$d"; expect_rc "a code with no fixture fails" 1; expect_out "names the code" "SND-LIB-013: has no fixture"

d="$(copy badkind)"; subst "$d/aw/acme-harbor/plans/20261001-100000-harbor-booking/plan.md" -e 's/^kind: plan$/kind: rfc/'
run "$d"; expect_rc "a good doc with a bad kind fails" 1; expect_out "names the doc" "harbor-booking/plan.md: front matter fails"

d="$(copy fixed010)"; subst "$d/broken/010-bad-kind.md" -e 's/^kind: rfc$/kind: note/'
run "$d"; expect_rc "a 010 fixture that passes fails" 1; expect_out "names the doc" "010-bad-kind.md: SND-LIB-010 fixture: its front matter passes"

d="$(copy shadrift)"; printf '{"slug":"harbor-ferry","title":"Edited"}\n' > "$d/aw/acme-harbor/system-design/harbor-ferry/packet.json"
run "$d"; expect_rc "sha drift on a good generated doc fails" 1; expect_out "names the doc" "harbor-ferry/doc.md: source-sha256"

d="$(copy shafixed)"; subst "$d/broken/017-edited-generated/doc.md" -e "s/^source-sha256: .*/source-sha256: $(sha256sum "$d/broken/017-edited-generated/packet.json" | cut -d' ' -f1)/"
run "$d"; expect_rc "a 017 fixture whose sha matches fails" 1; expect_out "names the doc" "SND-LIB-017 fixture: source-sha256 still matches"

d="$(copy nearmiss)"; subst "$d/aw/acme-harbor/plans/20261025-100000-near-misses/quoted.md" -e 's/^aw-doc: "1"$/aw-doc: 1/'
run "$d"; expect_rc "a near miss that is a real aw-doc: 1 fails" 1; expect_out "names the doc" "quoted.md: is listed as a near miss"

d="$(copy vaultread)"; edit "$d/expected.json" '(.files[] | select(.path == "vault/private/diary.md")) |= (.read = true | del(.skipReason))'
run "$d"; expect_rc "a denied vault doc listed as read fails" 1; expect_out "names the doc" "vault/private/diary.md: sits in a vault dot folder or deny glob but is read"

d="$(copy twoids)"; subst "$d/broken/012-shared-id-b.md" -e 's/^id: shared-doc-id$/id: shared-doc-id-b/'
run "$d"; expect_rc "a 012 fixture with no shared id fails" 1; expect_out "names the doc" "012-shared-id-b.md: has SND-LIB-012 but is the lowest path"

d="$(copy body013)"; subst "$d/broken/013-two-headings.md" -e 's/^## Ph1 Second$/## Ph2 Second/'
run "$d"; expect_rc "a 013 fixture that defines no id twice fails" 1; expect_out "names the doc" "013-two-headings.md: SND-LIB-013 fixture defines no id twice"

d="$(copy badkey)"; edit "$d/expected.json" '(.files[] | select(.path == "broken/010-bad-kind.md")) |= (.badKey = "/id")'
run "$d"; expect_rc "a 010 fixture failing at another key fails" 1; expect_out "names the paths" "fails at /kind, want only /id"

d="$(copy links)"; edit "$d/expected.json" '(.files[] | select(.path | endswith("harbor-epic/stages.md"))) |= del(.frontMatterLinks)'
run "$d"; expect_rc "a parent missing from frontMatterLinks fails" 1; expect_out "names the key" "frontMatterLinks.parent"

d="$(copy refline)"; edit "$d/expected.json" '(.files[] | select(.path | endswith("TASKS.md"))).refs[0].line = 1'
run "$d"; expect_rc "a ref on the wrong line fails" 1; expect_out "names the ref" "does not mention REQ-U1"

d="$(copy refkind)"; edit "$d/expected.json" '(.files[] | select(.path | endswith("TASKS.md"))).refs[0].kind = "parent"'
run "$d"; expect_rc "a ref of kind parent fails the ref schema" 1; expect_out "names the schema" "fails \$defs/ref"

d="$(copy body015)"; subst "$d/broken/015-broken-ref.md" -e 's/REQ-99/REQ-1/'; edit "$d/expected.json" '(.files[] | select(.path == "broken/015-broken-ref.md")).refs[0].to = "REQ-1"'
run "$d"; expect_rc "a 015 fixture whose target exists fails" 1; expect_out "names the holder" "SND-LIB-015 fixture: REQ-1 is defined in"

d="$(copy body016)"; subst "$d/broken/016-unknown-parent.md" -e 's/^parent: no-such-doc$/parent: shared-doc-id/'; edit "$d/expected.json" '(.files[] | select(.path == "broken/016-unknown-parent.md")).frontMatterLinks.parent = "shared-doc-id"'
run "$d"; expect_rc "a 016 fixture whose parent exists fails" 1; expect_out "names the doc" "016-unknown-parent.md: SND-LIB-016 fixture: its parent and supersedes all match"

d="$(copy body014)"; subst "$d/broken/014-orphan-ac.md" -e 's/^## Criteria$/## Criteria\n\n- REQ-2 Now there is one./'
run "$d"; expect_rc "a 014 fixture with a requirement above its AC fails" 1; expect_out "names the doc" "014-orphan-ac.md: SND-LIB-014 fixture has no AC without a requirement"

d="$(copy body011)"; subst "$d/broken/011-bad-item-id.md" -e 's/REQ-\*\*/REQ-1**/'
run "$d"; expect_rc "a 011 fixture with a numbered id fails" 1; expect_out "names the doc" "011-bad-item-id.md: SND-LIB-011 fixture has no REQ- without a number"

echo
echo "passed $PASS, failed $FAIL"
[ "$FAIL" -eq 0 ]
