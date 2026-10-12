#!/usr/bin/env bash
# Tests for scripts/denylist-check.sh: hits print only file:line and a count, never the matched words; a missing or
# empty denylist fails closed. The words here are synthetic.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SCRIPT="$HERE/../denylist-check.sh"
PASS=0
FAIL=0
T="$(mktemp -d "${TMPDIR:-/tmp}/denylist-check-test.XXXXXX")"
trap 'rm -rf "$T"' EXIT

ok()   { PASS=$((PASS + 1)); echo "  ok   $1"; }
fail() { FAIL=$((FAIL + 1)); echo "  FAIL $1"; }
run()  { set +e; OUT="$(bash "$SCRIPT" "$@" 2>&1)"; CODE=$?; set -e; }
expect() { # expect <name> <code> <substring>...
  local name="$1" code="$2" good=1 s; shift 2
  [ "$CODE" = "$code" ] || good=0
  for s in "$@"; do case "$OUT" in *"$s"*) ;; *) good=0 ;; esac; done
  if [ "$good" = 1 ]; then ok "$name"; else fail "$name (exit $CODE, want $code)"; printf '%s\n' "$OUT" | sed 's/^/       | /'; fi
}
refuse() { # refuse <name> <substring>... — OUT contains none of them (case-insensitive)
  local name="$1" s; shift
  for s in "$@"; do
    if printf '%s' "$OUT" | grep -qiF -- "$s"; then fail "$name (printed '$s')"; printf '%s\n' "$OUT" | sed 's/^/       | /'; return; fi
  done
  ok "$name"
}

printf '# team keys and names (synthetic)\n\nZQX-\n  quokka-lattice  \nexample-ws.slack.test\r\n' > "$T/deny.txt"
printf 'title: fix the pane split\nbody line two\n' > "$T/clean.md"
printf 'line one\nsee zqx-482 for details\nnothing\nthe Quokka-Lattice project and ZQX-9\nEXAMPLE-WS.slack.test\n' > "$T/hit.md"

echo "denylist-check.sh"

run "$T/deny.txt" "$T/clean.md"
expect "a clean file passes with a zero count" 0 "denylist-check: 0 hits in 1 file"

run "$T/deny.txt" "$T/hit.md" "$T/clean.md"
expect "hits print file:line per matching line and the count" 1 "$T/hit.md:2" "$T/hit.md:4" "$T/hit.md:5" "denylist-check: 3 hits in 2 files"
refuse "hits never print the matched words or the line text" "zqx" "quokka" "lattice" "example-ws" "details" "project"
case "$OUT" in *"$T/clean.md"*) fail "a clean file is not listed"; printf '%s\n' "$OUT" | sed 's/^/       | /' ;; *) ok "a clean file is not listed" ;; esac

run "$T/nope.txt" "$T/clean.md"
expect "a missing denylist fails closed" 2 "denylist" "missing"

: > "$T/empty.txt"; run "$T/empty.txt" "$T/clean.md"
expect "an empty denylist fails closed" 2 "no entries"
printf '# only a comment\n\n   \n' > "$T/comments.txt"; run "$T/comments.txt" "$T/clean.md"
expect "a denylist with only comments and blanks fails closed" 2 "no entries"

run "$T/deny.txt" "$T/clean.md" "$T/missing.md"
expect "a missing input file is an error, not a pass" 2 "$T/missing.md" "not a readable file"

run "$T/deny.txt"
expect "no input files is a usage error" 2 "usage:"
run
expect "no arguments is a usage error" 2 "usage:"

mkdir "$T/dir"; run "$T/dir" "$T/clean.md"
expect "a directory as the denylist fails closed" 2 "denylist"

printf 'a\nb line with -e and .* chars\n' > "$T/regex.md"; printf '.*\n-e\n' > "$T/deny-regex.txt"
run "$T/deny-regex.txt" "$T/regex.md"
expect "entries are fixed strings, not patterns or options" 1 "$T/regex.md:2" "denylist-check: 1 hit in 1 file"

echo
echo "denylist-check: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
