#!/usr/bin/env bash
# Tests for scripts/code-index-lock.sh: the repo's context/code-index.lock passes (shape, requirements hash, parity with
# sindri/package.json), each malformed fixture fails with its code, and `get` prints each pinned value.
set -euo pipefail

SCRIPT="$(cd "$(dirname "$0")/.." && pwd)/code-index-lock.sh"
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/code-index-lock-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

run() { set +e; OUT="$(bash "$SCRIPT" "$@" 2>&1)"; RC=$?; set -e; }
expect() {   # <label> <rc> [<regex>]
  if [ "$RC" -ne "$2" ]; then fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  if [ -n "${3:-}" ] && ! printf '%s\n' "$OUT" | grep -qE -- "$3"; then fail "$1: output lacks /$3/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  ok "$1"
}

# A fixture world: a requirements file, a package.json with the same pin, and a good lock naming both.
printf 'graphifyy==1.2.3 \\\n    --hash=sha256:00\n' >"$TMPROOT/code-index-requirements.txt"
REQSHA="$(sha256sum "$TMPROOT/code-index-requirements.txt" | cut -d' ' -f1)"
printf '{"sindri":{"graphifyPin":"1.2.3","graphifyPinDate":"2026-01-02T00:00:00Z"}}\n' >"$TMPROOT/package.json"
DIGEST=sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
good_body() {
  echo "graphify 1.2.3 2026-01-02T00:00:00Z $REQSHA"
  echo "ollama-model nomic-embed-text:latest $DIGEST"
  echo "need disk-bytes 100"
}
lock() { local f="$TMPROOT/$1.lock"; cat >"$f"; echo "$f"; }
chk() { run check --file "$1" --requirements "$TMPROOT/code-index-requirements.txt" --package "$TMPROOT/package.json"; }

echo "code-index-lock check"

run check
expect "the repo's context/code-index.lock passes (shape, requirements hash, package.json parity)" 0

f="$(good_body | lock good)"
chk "$f"; expect "a well-formed file passes" 0

run check --file "$TMPROOT/absent.lock"; expect "an unreadable file is a tool error" 2
run bogus; expect "an unknown command is a usage error" 2

f="$(good_body | grep -v '^graphify' | lock nograph)"
chk "$f"; expect "no graphify row fails" 1 'CODEIDX-LOCK-001 .*no graphify row'

f="$( { good_body; echo "graphify 1.2.3 2026-01-02T00:00:00Z $REQSHA"; } | lock twograph)"
chk "$f"; expect "two graphify rows fail" 1 'CODEIDX-LOCK-001 .*2 graphify rows'

f="$(good_body | sed 's/^graphify 1.2.3/graphify v1.2/' | lock badver)"
chk "$f"; expect "a version that is not X.Y.Z fails" 1 'CODEIDX-LOCK-001 .*version "v1.2"'

f="$(good_body | sed 's/2026-01-02T00:00:00Z/2026-01-02/' | lock baddate)"
chk "$f"; expect "a date that is not a UTC timestamp fails" 1 'CODEIDX-LOCK-002 .*date "2026-01-02"'

f="$(good_body | sed "s/$REQSHA/abc/" | lock badsha)"
chk "$f"; expect "a short requirements sha fails" 1 'CODEIDX-LOCK-002 .*requirements sha256'

f="$(good_body | grep -v '^ollama-model' | lock nomodel)"
chk "$f"; expect "no ollama-model row fails" 1 'CODEIDX-LOCK-003 .*no ollama-model row'

f="$( { good_body; echo "ollama-model other $DIGEST"; } | lock twomodels)"
chk "$f"; expect "two ollama-model rows fail" 1 'CODEIDX-LOCK-003 .*2 ollama-model rows'

f="$(good_body | sed 's/ sha256:.*/ sha256:PLACEHOLDER-no-digest-yet/' | lock placeholder)"
chk "$f"; expect "a placeholder model digest is refused" 1 'CODEIDX-LOCK-002 .*placeholder is not a pin'

f="$(good_body | sed 's/nomic-embed-text:latest/Nomic Embed/' | lock badname)"
chk "$f"; expect "a bad model name fails" 1 'CODEIDX-LOCK-00[23] '

f="$(good_body | sed 's/nomic-embed-text:latest/some-cloud-model/' | lock cloud)"
chk "$f"; expect "a cloud model fails" 1 'CODEIDX-LOCK-002 .*cloud model'

f="$(good_body | grep -v '^need' | lock nodisk)"
chk "$f"; expect "no need row fails" 1 'CODEIDX-LOCK-004 .*no need disk-bytes row'

f="$( { good_body; echo "need disk-bytes 5"; } | lock twodisk)"
chk "$f"; expect "two need rows fail" 1 'CODEIDX-LOCK-004 .*listed twice'

f="$(good_body | sed 's/need disk-bytes 100/need disk-bytes 0/' | lock zerodisk)"
chk "$f"; expect "a zero need fails" 1 'CODEIDX-LOCK-004 '

f="$(good_body | sed 's/need disk-bytes 100/need disk-bytes 1234567890123456789/' | lock bigdisk)"
chk "$f"; expect "a need of 19 digits fails" 1 'CODEIDX-LOCK-004 '

f="$( { good_body; echo "need memory-bytes 5"; } | lock badneed)"
chk "$f"; expect "an unknown need name fails" 1 'CODEIDX-LOCK-005 .*memory-bytes'

f="$( { good_body; echo "image a b c d"; } | lock unknownrow)"
chk "$f"; expect "an unknown row kind fails" 1 'CODEIDX-LOCK-005 .*unknown row kind "image"'

f="$( { good_body; printf 'need disk-bytes 5\r\n'; } | lock cr)"
chk "$f"; expect "a CR fails" 1 'CODEIDX-LOCK-002 .*CR, VT or FF'

f="$( { good_body; printf 'x\0y\n'; } | lock nul)"
chk "$f"; expect "a NUL byte fails" 1 'CODEIDX-LOCK-002 .*NUL'

f="$( { echo "# a comment"; echo; good_body; } | lock comments)"
chk "$f"; expect "comments and blank lines are ignored" 0

echo "code-index-lock requirements and parity"

f="$(good_body | lock par)"
printf 'changed\n' >"$TMPROOT/other-requirements.txt"
run check --file "$f" --requirements "$TMPROOT/other-requirements.txt" --package "$TMPROOT/package.json"
expect "a requirements file with another hash fails" 1 'CODEIDX-LOCK-006 .*has sha256'
run check --file "$f" --requirements "$TMPROOT/absent.txt" --package "$TMPROOT/package.json"
expect "a missing requirements file fails" 1 'CODEIDX-LOCK-006 .*cannot read'

printf '{"sindri":{"graphifyPin":"1.2.4","graphifyPinDate":"2026-01-02T00:00:00Z"}}\n' >"$TMPROOT/pkg-ver.json"
run check --file "$f" --requirements "$TMPROOT/code-index-requirements.txt" --package "$TMPROOT/pkg-ver.json"
expect "a graphify version that differs from package.json fails" 1 'CODEIDX-LOCK-007 .*version is 1.2.3, .*graphifyPin is 1.2.4'
printf '{"sindri":{"graphifyPin":"1.2.3","graphifyPinDate":"2026-02-02T00:00:00Z"}}\n' >"$TMPROOT/pkg-date.json"
run check --file "$f" --requirements "$TMPROOT/code-index-requirements.txt" --package "$TMPROOT/pkg-date.json"
expect "a graphify date that differs from package.json fails" 1 'CODEIDX-LOCK-007 .*date is 2026-01-02T00:00:00Z, .*graphifyPinDate is 2026-02-02T00:00:00Z'
printf '{"sindri":{}}\n' >"$TMPROOT/pkg-none.json"
run check --file "$f" --requirements "$TMPROOT/code-index-requirements.txt" --package "$TMPROOT/pkg-none.json"
expect "a package.json with no pin fails" 1 'CODEIDX-LOCK-007 .*absent'

# The shipped lock and package.json agree (the parity the spec asks for, stated directly).
want="$(jq -r '.sindri.graphifyPin' "$REPO/sindri/package.json")"
got="$(bash "$SCRIPT" get graphify-version)"
[ "$want" = "$got" ] && ok "the lock's graphify version is sindri/package.json's pin ($want)" || fail "lock $got, package.json $want"

echo "code-index-lock get"

f="$(good_body | lock get)"
for pair in "graphify-version=1.2.3" "graphify-date=2026-01-02T00:00:00Z" "requirements-sha256=$REQSHA" "model=nomic-embed-text:latest" "model-digest=$DIGEST" "disk-bytes=100"; do
  run get "${pair%%=*}" --file "$f"; expect "get ${pair%%=*}" 0 "^${pair#*=}\$"
done
run get nonsense --file "$f"; expect "get of an unknown key exits 1 and prints nothing" 1
[ -z "$OUT" ] && ok "nothing printed for an unknown key" || fail "printed: $OUT"
run get; expect "get without a key is a usage error" 2

echo ""
echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
