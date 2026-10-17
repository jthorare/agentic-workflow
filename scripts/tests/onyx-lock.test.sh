#!/usr/bin/env bash
# Tests for scripts/onyx-lock.sh check: the repo's context/onyx.lock passes, and each malformed fixture fails with its code.
set -euo pipefail

SCRIPT="$(cd "$(dirname "$0")/.." && pwd)/onyx-lock.sh"
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0
TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/onyx-lock-test.XXXXXX")"
trap 'find "$TMPROOT" -delete' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }

run() { set +e; OUT="$(bash "$SCRIPT" "$@" 2>&1)"; RC=$?; set -e; }
expect() {   # <label> <rc> [<regex>]
  if [ "$RC" -ne "$2" ]; then fail "$1: exit $RC, want $2"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  if [ -n "${3:-}" ] && ! printf '%s\n' "$OUT" | grep -qE -- "$3"; then fail "$1: output lacks /$3/"; printf '%s\n' "$OUT" | sed 's/^/      | /'; return; fi
  ok "$1"
}

COMMIT=0123456789abcdef0123456789abcdef01234567
SHA=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
DIGEST="sha256:$SHA"
lock() { local f="$TMPROOT/$1.lock"; cat >"$f"; echo "$f"; }
good_body() {   # a valid file, one line per row
  echo "version v4.9.0 $COMMIT"
  echo "file docker_compose/docker-compose.yml $SHA"
  echo "image docker.io/o/app v1 linux/arm64 $DIGEST"
  echo "image docker.io/o/app v1 linux/amd64 $DIGEST"
  echo "need disk-bytes 100"
  echo "need docker-memory-bytes 200"
}

echo "onyx-lock check"

run check --file "$REPO/context/onyx.lock"
expect "the repo's context/onyx.lock passes" 0

f="$(good_body | lock good)"
run check --file "$f"
expect "a well-formed file passes" 0

run check --file "$TMPROOT/absent.lock"
expect "an unreadable file is a tool error" 2

run bogus
expect "an unknown command is a usage error" 2

f="$(good_body | grep -v '^version' | lock noversion)"
run check --file "$f"
expect "no version row fails" 1 'ONYX-LOCK-001 .*no version row'

f="$( { good_body; echo "version v4.9.1 $COMMIT"; } | lock twoversions)"
run check --file "$f"
expect "two version rows fail" 1 'ONYX-LOCK-001 .*2 version rows'

f="$(good_body | sed 's/^version v4.9.0/version 4.9/' | lock badtag)"
run check --file "$f"
expect "a tag that is not vX.Y.Z fails" 1 'ONYX-LOCK-001 .*tag "4.9"'

f="$(good_body | sed 's/^file \(.*\) .*/file \1 ABCDEF/' | lock badfilesha)"
run check --file "$f"
expect "a bad file sha256 fails" 1 'ONYX-LOCK-002 .*sha256'

f="$(good_body | sed 's/^image docker.io\/o\/app v1 linux\/amd64 .*/image docker.io\/o\/app v1 linux\/amd64 sha256:abc/' | lock baddigest)"
run check --file "$f"
expect "a digest that is not sha256: and 64 hex fails" 1 'ONYX-LOCK-002 .*digest'

f="$(good_body | grep -v 'linux/amd64' | lock missingplatform)"
run check --file "$f"
expect "an image missing a platform fails" 1 'ONYX-LOCK-003 docker.io/o/app: no linux/amd64 row'

f="$( { good_body; echo "image docker.io/o/app v1 linux/arm64 $DIGEST"; } | lock dupplatform)"
run check --file "$f"
expect "an image platform listed twice fails" 1 'ONYX-LOCK-003 .*listed twice'

# Shared with OnyxPinsParseTests (Parse_refuses_a_lock_with_no_image_rows, ..._no_compose_file_row): the same two fixtures.
f="$(good_body | grep -v '^image' | lock noimages)"
run check --file "$f"
expect "a lock with no image rows fails" 1 'ONYX-LOCK-003 .*no image rows'

f="$(good_body | grep -v '^file' | lock nocomposefile)"
run check --file "$f"
expect "a lock with no compose file row fails" 1 'ONYX-LOCK-002 .*no file row for docker_compose/docker-compose.yml'

f="$(good_body | grep -v 'docker-memory-bytes' | lock noneed)"
run check --file "$f"
expect "a missing need row fails" 1 'ONYX-LOCK-004 .*docker-memory-bytes'

f="$(good_body | sed 's/^need disk-bytes 100/need disk-bytes lots/' | lock neednan)"
run check --file "$f"
expect "a need that is not a number fails" 1 'ONYX-LOCK-004 .*disk-bytes "lots"'

f="$( { good_body; echo "frobnicate x"; } | lock unknownkind)"
run check --file "$f"
expect "an unknown row kind fails" 1 'ONYX-LOCK-005 .*frobnicate'

f="$( { good_body; echo "need cpu-cores 4"; } | lock unknownneed)"
run check --file "$f"
expect "an unknown need name fails" 1 'ONYX-LOCK-005 .*cpu-cores'

# Parity cases: the same malformed fixtures as OnyxPinsParseTests.Parse_refuses_the_malformed_fixtures_the_bash_check_refuses
# (the C# parser refuses each with SND-ONYX-004), and the same accepted rows.
f="$(good_body | sed 's/^need disk-bytes 100/need disk-bytes 1234567890123456789/' | lock need19)"
run check --file "$f"
expect "a need of 19 digits fails" 1 'ONYX-LOCK-004 .*disk-bytes'

f="$(good_body | sed 's/^need disk-bytes 100/need disk-bytes 123456789012345678/' | lock need18)"
run check --file "$f"
expect "a need of 18 digits passes" 0

f="$( { good_body; echo "file docker_compose/docker-compose.yml $SHA"; } | lock dupfile)"
run check --file "$f"
expect "a file path listed twice fails" 1 'ONYX-LOCK-002 .*listed twice'

f="$( { good_body; echo "image docker.io/o/app v1 linux/arm64 $DIGEST"; } | lock dupimage)"
run check --file "$f"
expect "a (repository, platform) listed twice fails" 1 'ONYX-LOCK-003 .*listed twice'

for repo in Bad_Repo -lead; do
  f="$( { good_body; echo "image $repo v1 linux/arm64 $DIGEST"; echo "image $repo v1 linux/amd64 $DIGEST"; } | lock badrepo)"
  run check --file "$f"
  expect "repository name $repo fails" 1 'ONYX-LOCK-002 .*repository'
done

for path in 'a\b' 'a//b' 'a/' '../x' 'a/../x' '/x' 'a[1]' 'a?b' 'a%2e' 'a#b' 'a:b' 'a*'; do
  f="$( { good_body; printf 'file %s %s\n' "$path" "$SHA"; } | lock badpath)"
  run check --file "$f"
  expect "file path $path fails" 1 'ONYX-LOCK-002 .*path'
done

f="$( { good_body; printf 'file a\0b %s\n' "$SHA"; } | lock nulpath)"
run check --file "$f"
expect "a NUL byte fails" 1 'ONYX-LOCK-002 .*NUL'

# A CR, VT or FF anywhere on a line is refused (the shipped lock is LF-only), as OnyxPins.Parse refuses it.
f="$( { good_body; printf '\r\n'; } | lock lonecr)"
run check --file "$f"
expect "a line holding a lone CR fails" 1 'ONYX-LOCK-002 7: .*CR, VT or FF'

f="$( { good_body; printf '# note\r\n'; } | lock crcomment)"
run check --file "$f"
expect "a comment line ending in CR fails" 1 'ONYX-LOCK-002 7: .*CR, VT or FF'

f="$( { good_body; printf 'need disk-bytes 100\r # c\n'; } | lock crbeforehash)"
run check --file "$f"
expect "a CR before a # fails" 1 'ONYX-LOCK-002 7: .*CR, VT or FF'

for c in '\v' '\f'; do
  f="$( { good_body; printf "# a${c}b\n"; } | lock ctl)"
  run check --file "$f"
  expect "a $c inside a line fails" 1 'ONYX-LOCK-002 7: .*CR, VT or FF'
done

# Fields split on space and tab only (as awk does): CRLF line endings and a no-break space are refused, as OnyxPins.Parse refuses them.
f="$(good_body | awk '{ printf "%s\r\n", $0 }' | lock crlf)"
run check --file "$f"
expect "CRLF line endings fail" 1 'ONYX-LOCK-00'

f="$(good_body | sed $'s/^need disk-bytes 100/need disk-bytes\xc2\xa0100/' | lock nbsp)"
run check --file "$f"
expect "a no-break space between fields fails" 1 'ONYX-LOCK-00'

f="$( { good_body; echo "image docker.io/library/postgres 15.2-alpine linux/arm64 $DIGEST"; echo "image docker.io/library/postgres 15.2-alpine linux/amd64 $DIGEST"; echo "image ghcr.io/o/a_b-c.d/e RELEASE.2025-07-23T15-54-02Z-cpuv1 linux/arm64 $DIGEST"; echo "image ghcr.io/o/a_b-c.d/e RELEASE.2025-07-23T15-54-02Z-cpuv1 linux/amd64 $DIGEST"; echo "file data/nginx/app.conf.template $SHA"; } | lock accepted)"
run check --file "$f"
expect "dotted, nested repositories and nested file paths pass" 0

echo
echo "onyx-lock verify (stubbed curl and docker on PATH)"

# A fake curl that serves $STUB/<basename of the URL> to -o <file>, and a fake docker that answers every
# `buildx imagetools inspect ... --raw` with $STUB/index.json. jq is the real one.
STUB="$TMPROOT/stub"; mkdir -p "$STUB/bin"
cat >"$STUB/bin/curl" <<'EOS'
#!/usr/bin/env bash
out=""; url=""
echo "$*" >>"$STUB/curl.log"
while [ $# -gt 0 ]; do case "$1" in -o) out="$2"; shift 2 ;; -*) shift ;; *) url="$1"; shift ;; esac; done
[ -f "$STUB/$(basename "$url")" ] || exit 22
cp "$STUB/$(basename "$url")" "$out"
EOS
cat >"$STUB/bin/docker" <<'EOS'
#!/usr/bin/env bash
# An inspect of <repo>@<digest> is answered from $STUB/by-digest/<digest with : as _>.json when that file exists; everything else from index.json.
echo "$*" >>"$STUB/docker.log"
for a in "$@"; do
  case "$a" in *@sha256:*) f="$STUB/by-digest/$(printf '%s' "${a#*@}" | tr : _).json"; if [ -f "$f" ]; then cat "$f"; exit 0; fi ;; esac
done
[ -f "$STUB/index.json" ] || { echo "no such manifest" >&2; exit 1; }
cat "$STUB/index.json"
EOS
mkdir -p "$STUB/by-digest"
chmod +x "$STUB/bin/curl" "$STUB/bin/docker"
DEV="sha256:1111111111111111111111111111111111111111111111111111111111111111"
ARM="sha256:2222222222222222222222222222222222222222222222222222222222222222"
INDEXD="sha256:5555555555555555555555555555555555555555555555555555555555555555"   # an index the compose pins (answered by index.json)
OTHERD="sha256:4444444444444444444444444444444444444444444444444444444444444444"   # an index with other platform digests
OTHERM="sha256:6666666666666666666666666666666666666666666666666666666666666666"   # a single manifest the lock doesn't hold
compose() {   # [<digest pinned on the redis line>] [<image line count: 2 or 0>] -- writes the compose the stubs serve and sets CSHA
  { echo "services:"
    if [ "${2:-2}" -ne 0 ]; then
      echo "  a:"; echo "    image: '\${A_IMAGE:-o/app:\${IMAGE_TAG:-latest}}'"
      echo "  b:"; echo "    image: \"\${REG:-docker.io}/library/redis:7.4-alpine${1:+@$1}\""
    fi
    echo "  # image: unused/thing:1"; } >"$STUB/docker-compose.yml"
  CSHA="$(shasum -a 256 "$STUB/docker-compose.yml" | cut -d' ' -f1)"
}
cat >"$STUB/index.json" <<EOS
{"manifests":[{"digest":"$ARM","platform":{"os":"linux","architecture":"arm64"}},{"digest":"$DEV","platform":{"os":"linux","architecture":"amd64"}},{"digest":"sha256:3333333333333333333333333333333333333333333333333333333333333333","platform":{"os":"unknown","architecture":"unknown"}}]}
EOS
cat >"$STUB/by-digest/$(printf '%s' "$OTHERD" | tr : _).json" <<EOS
{"manifests":[{"digest":"$DEV","platform":{"os":"linux","architecture":"arm64"}},{"digest":"$ARM","platform":{"os":"linux","architecture":"amd64"}}]}
EOS
for m in "$ARM" "$OTHERM"; do printf '{"schemaVersion":2,"config":{"digest":"x"},"layers":[]}' >"$STUB/by-digest/$(printf '%s' "$m" | tr : _).json"; done
compose "$INDEXD"
vlock() {   # <name> [<image digest for arm64>] — a lock the stubs satisfy
  { echo "version v4.9.0 $COMMIT"; echo "file docker_compose/docker-compose.yml ${3:-$CSHA}"
    echo "image docker.io/o/app v4.9.0 linux/arm64 ${2:-$ARM}"; echo "image docker.io/o/app v4.9.0 linux/amd64 $DEV"
    echo "image docker.io/library/redis 7.4-alpine linux/arm64 $ARM"; echo "image docker.io/library/redis 7.4-alpine linux/amd64 $DEV"
    echo "need disk-bytes 100"; echo "need docker-memory-bytes 200"; } | lock "$1"
}
vrun() { set +e; OUT="$(STUB="$STUB" PATH="$STUB/bin:$PATH" bash "$SCRIPT" verify "$@" 2>&1)"; RC=$?; set -e; }

vrun --file "$(vlock vgood)"
expect "verify passes when the files, images and compose all match (the compose image lines are single- and double-quoted)" 0 'verify: ok \(1 files, 4 image rows, 2 compose image lines\)'

grep -q -- "--proto =https --proto-redir =https" "$STUB/curl.log" && ok "verify downloads over https only (--proto and --proto-redir)" || fail "verify's curl lacks --proto =https --proto-redir =https"

vrun --file "$(vlock vbadimage "$DEV")"
expect "verify reports an image digest that differs" 1 'ONYX-LOCK-011 docker.io/o/app:v4.9.0 linux/arm64: expected sha256:1111.*got sha256:2222'

vrun --file "$(vlock vbadfile "$ARM" "$(printf 'f%.0s' $(seq 64))")"
expect "verify reports a file sha256 that differs" 1 'ONYX-LOCK-010 docker_compose/docker-compose.yml: expected f+, got '"$CSHA"

grep -q -- "-fsSLg" "$STUB/curl.log" && ok "verify's curl turns URL globbing off (-g)" || fail "verify's curl lacks -g"

grep -q -- "buildx imagetools inspect docker.io/library/redis@$INDEXD --raw" "$STUB/docker.log" && ok "a digest-pinned compose line is inspected as <repository>@<digest>" || fail "no inspect of redis@digest"

# A digest-pinned compose line: an index must hold the lock's two platform digests, a single manifest must be one of them.
compose "$OTHERD"; vrun --file "$(vlock vdiffindex)"
expect "verify fails a compose line pinning an index whose platform digests differ from the lock" 1 'ONYX-LOCK-012 .*redis:7.4-alpine@sha256:4444.* pins an index'

compose "$ARM"; vrun --file "$(vlock vmanifest)"
expect "verify passes a compose line pinning a single manifest the lock holds" 0 'verify: ok'

compose "$OTHERM"; vrun --file "$(vlock vbadmanifest)"
expect "verify fails a compose line pinning a single manifest the lock does not hold" 1 'ONYX-LOCK-012 .*pins the manifest sha256:6666'

compose "sha256:abc"; vrun --file "$(vlock vbogus)"
expect "verify fails a compose line pinning a malformed digest" 1 'ONYX-LOCK-012 .*sha256:abc'

compose "" 0; vrun --file "$(vlock vnoimages)"
expect "verify fails a compose with no image line" 1 'ONYX-LOCK-012 .*extracted 0 image lines'
compose "$INDEXD"

sed '/^image docker.io\/library\/redis/d' "$(vlock vsrc)" >"$TMPROOT/vunpinned.lock"
vrun --file "$TMPROOT/vunpinned.lock"
expect "verify reports a compose image that is not pinned" 1 'ONYX-LOCK-012 .*names docker.io/library/redis:7.4-alpine'

sed 's/^version .*/version 4.9 x/' "$(vlock vsrc2)" >"$TMPROOT/vbadshape.lock"
vrun --file "$TMPROOT/vbadshape.lock"
expect "verify refuses a file that fails check" 1 'ONYX-LOCK-001'

rm "$STUB/docker-compose.yml"
vrun --file "$(vlock voffline)"
expect "verify with an unreachable download is a network error" 2 'cannot download'
printf 'x' >"$STUB/docker-compose.yml"

rm "$STUB/index.json"
vrun --file "$(vlock vnoindex "$ARM" "$(shasum -a 256 "$STUB/docker-compose.yml" | cut -d' ' -f1)")"
expect "verify with an unanswering registry is a network error" 2 'cannot inspect'

run verify --bogus
expect "verify with an unknown flag is a usage error" 2

echo
echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
