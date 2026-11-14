#!/usr/bin/env bash
# Tests for scripts/images-lock.sh check: the repo's deploy/images.lock and Dockerfile pass, and each malformed fixture fails with its code.
set -euo pipefail

SCRIPT="$(cd "$(dirname "$0")/.." && pwd)/images-lock.sh"
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/images-lock-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

run() { set +e; OUT="$(bash "$SCRIPT" "$@" 2>&1)"; RC=$?; set -e; }
expect() {   # <label> <rc> [<regex>]
  if [ "$RC" -ne "$2" ]; then fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  if [ -n "${3:-}" ] && ! grep -qE -- "$3" <<<"$OUT"; then fail "$1: output lacks /$3/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  ok "$1"
}

SHA=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
OTHER=fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210
lock() { local f="$TMPROOT/$1.lock"; cat >"$f"; echo "$f"; }
good_body() {
  echo "image docker.io/o/app v1 index sha256:$SHA"
  echo "image docker.io/o/app v1 linux/arm64 sha256:$SHA"
  echo "image docker.io/o/app v1 linux/amd64 sha256:$SHA"
  echo "todo docker.io/o/later K-C1"
}
dockerfile() { local f="$TMPROOT/$1.Dockerfile"; cat >"$f"; echo "$f"; }
NODF="$TMPROOT/absent.Dockerfile"

echo "images-lock check"

run check
expect "the repo's deploy/images.lock and Dockerfile pass" 0

f="$(good_body | lock good)"
run check --file "$f" --dockerfile "$NODF"
expect "a well-formed file passes and lists its todo rows" 0 "note: docker.io/o/later is not pinned yet \\(K-C1\\)"

run check --file "$TMPROOT/absent.lock"
expect "an unreadable file is a tool error" 2

run bogus
expect "an unknown command is a usage error" 2

f="$(good_body | grep -v 'index' | lock noindex)"
run check --file "$f" --dockerfile "$NODF"
expect "a missing index row" 1 "IMAGES-LOCK-003 docker.io/o/app:v1: no index row"

f="$(good_body | grep -v 'linux/amd64' | lock noamd)"
run check --file "$f" --dockerfile "$NODF"
expect "a missing platform row" 1 "IMAGES-LOCK-003 docker.io/o/app:v1: no linux/amd64 row"

f="$( { good_body; echo "image docker.io/o/app v1 linux/amd64 sha256:$SHA"; } | lock dup)"
run check --file "$f" --dockerfile "$NODF"
expect "a repeated platform row" 1 "IMAGES-LOCK-003 [0-9]+: docker.io/o/app:v1 linux/amd64 is listed twice"

f="$( { good_body; echo "image docker.io/o/app v2 linux/amd64 sha256:$SHA"; } | lock twotags)"
run check --file "$f" --dockerfile "$NODF"
expect "a second tag of a repository needs its own index and platform rows" 1 "IMAGES-LOCK-003 docker.io/o/app:v2: no index row"

f="$( { good_body; echo "image docker.io/o/app v2 index sha256:$OTHER"; echo "image docker.io/o/app v2 linux/amd64 sha256:$OTHER"; echo "image docker.io/o/app v2 linux/arm64 sha256:$OTHER"; } | lock twotagsok)"
run check --file "$f" --dockerfile "$NODF"
expect "a repository may carry several tags, each fully pinned" 0

f="$(good_body | sed "s/index sha256:$SHA/index sha256:abc/" | lock shortdigest)"
run check --file "$f" --dockerfile "$NODF"
expect "a short digest" 1 "IMAGES-LOCK-002 .*64 lowercase hex"

f="$( { good_body; echo "todo docker.io/o/app K-C1"; } | lock todopinned)"
run check --file "$f" --dockerfile "$NODF"
expect "a todo row for a pinned repository" 1 "IMAGES-LOCK-004"

f="$( { good_body; echo "volume x y"; } | lock unknown)"
run check --file "$f" --dockerfile "$NODF"
expect "an unknown row kind" 1 "IMAGES-LOCK-001"

f="$(printf '# only a comment\n' | lock empty)"
run check --file "$f" --dockerfile "$NODF"
expect "no image rows" 1 "IMAGES-LOCK-005"

f="$(good_body | lock lockfordf)"
d="$(printf 'FROM docker.io/o/app:v1@sha256:%s AS build\nFROM build AS again\nFROM scratch\nCOPY --from=build /a /b\nCOPY --from=docker.io/o/app:v1@sha256:%s /x /y\n' "$SHA" "$SHA" | dockerfile ok)"
run check --file "$f" --dockerfile "$d"
expect "a Dockerfile of pinned images and its own stages passes" 0

d="$(printf 'FROM docker.io/o/app:v1@sha256:%s\n' "$OTHER" | dockerfile wrongdigest)"
run check --file "$f" --dockerfile "$d"
expect "a Dockerfile FROM with another digest" 1 "IMAGES-LOCK-006 1:"

d="$(printf 'FROM docker.io/o/app:v1\n' | dockerfile nodigest)"
run check --file "$f" --dockerfile "$d"
expect "a Dockerfile FROM without a digest" 1 "IMAGES-LOCK-007 1:"

d="$(printf 'FROM docker.io/o/app:v1@sha256:%s\nCOPY --from=ghcr.io/x/y:1@sha256:%s /a /b\n' "$SHA" "$SHA" | dockerfile unpinnedcopy)"
run check --file "$f" --dockerfile "$d"
expect "a COPY --from image the lock does not pin" 1 "IMAGES-LOCK-006 2:"

echo "images-lock: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
