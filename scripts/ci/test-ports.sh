#!/usr/bin/env bash
# scripts/ci/test-ports.sh — static check: no shell test listens on a fixed port (#2597).
#
# Two gates ran stack.test.sh at once, both bound its literal PORT=39917, and one failed with "address in use" (#2596). A port that
# is written into a test is shared by every checkout and every concurrent gate on the box. The rule: a test asks the kernel for a
# free port (port 0, or a helper that reads the bound port back) and passes that value around in a variable.
#
# Scanned: every tracked *.test.sh. A violation is a non-comment line with
#   - a listener on a literal non-zero port: nc / ncat with an -l flag and a bare number (nc -l 39917, nc -lk 8080, nc -l -p 9000),
#     socat TCP-LISTEN:N (also TCP4- and TCP6-), `http.server N`, or a python bind((host, N)) with N literal digits;
#   - a word ending in PORT assigned literal non-zero digits (PORT=39917, AW_PORT=3100, export X_PORT=1, local PORT=5, X_PORT=7410 cmd).
# Allowed: port 0, anything with a `$` (a variable, a substitution, arithmetic), `nc -z` probes, ports inside URLs, comment lines.
# Waiver: `test-ports: ok` plus a reason (text after it; a leading `:` is fine) on the flagged line, or on the comment line directly
# above it. A bare marker does not waive. A waiver covers one line. Use it for a value no process binds (a stub, a dry run).
# Usage: scripts/ci/test-ports.sh [--root DIR]   prints file:line: text for each violation; exit 0 clean, 1 violations, 2 usage.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
while [ $# -gt 0 ]; do
  case "$1" in
    --root) [ $# -ge 2 ] || { echo "test-ports: --root needs a directory" >&2; exit 2; }; ROOT="$(cd "$2" && pwd)" || exit 2; shift 2 ;;
    *) echo "usage: test-ports.sh [--root DIR]" >&2; exit 2 ;;
  esac
done
cd "$ROOT" || exit 2

SELF="scripts/ci/test-ports.sh"
SELF_TEST="scripts/tests/test-ports.test.sh"   # its fixtures are the violations
# The file list: inside a git checkout, what git tracks or would track; outside one, a find that skips .git, .claude/worktrees,
# node_modules, obj and bin. The root comparison is physical (macOS /var is a symlink to /private/var).
git_top="$(git -C "$ROOT" rev-parse --show-toplevel 2> /dev/null || true)"
if [ -n "$git_top" ] && [ "$(cd "$git_top" && pwd -P)" = "$(pwd -P)" ]; then
  listing="$(git ls-files --cached --others --exclude-standard 2> /dev/null | sort -u)"
else
  listing="$(find . \( -path ./.git -o -path ./.claude/worktrees -o -name node_modules -o -name obj -o -name bin \) -prune -o -type f -name '*.test.sh' -print 2> /dev/null | sed 's|^\./||' | sort -u)"
fi
files="$(grep -E '\.test\.sh$' <<< "$listing")"

violations=0
report() { printf '%s:%s: %s\n' "$1" "$2" "$3"; violations=$((violations + 1)); }

NC_LISTEN='(^|[^A-Za-z0-9_./-])(nc|ncat)[[:space:]]([^#]*[[:space:]])?-[A-Za-z]*l[A-Za-z]*[[:space:]]([^#]*[[:space:]])?[1-9][0-9]*([[:space:]]|$)'
SOCAT_LISTEN='TCP[46]?-LISTEN:[1-9]'
HTTP_SERVER='http\.server[[:space:]]+[1-9][0-9]*'
PY_BIND='bind\(\([^,)]*,[[:space:]]*[1-9][0-9]*[[:space:]]*\)\)'
PORT_ASSIGN='(^|[^A-Za-z0-9])([A-Za-z0-9_]*_)?PORT=["'"'"']?[1-9][0-9]*(["'"'"']|[^0-9A-Za-z_$.]|$)'
WAIVER='test-ports: ok:?[[:space:]]*[^[:space:]:]'

while IFS= read -r f; do
  [ -n "$f" ] && [ -f "$f" ] && [ "$f" != "$SELF" ] && [ "$f" != "$SELF_TEST" ] || continue
  n=0
  prev_waiver=0
  while IFS= read -r line || [ -n "$line" ]; do
    n=$((n + 1))
    waived_above=$prev_waiver
    prev_waiver=0
    # A comment line is one whose first non-space character is `#`; a code line with a trailing comment or a `${#x}` is code.
    if [[ "$line" =~ ^[[:space:]]*# ]]; then
      [[ "$line" =~ $WAIVER ]] && prev_waiver=1
      continue
    fi
    if [[ "$line" =~ $NC_LISTEN ]] || [[ "$line" =~ $SOCAT_LISTEN ]] || [[ "$line" =~ $HTTP_SERVER ]] \
      || [[ "$line" =~ $PY_BIND ]] || [[ "$line" =~ $PORT_ASSIGN ]]; then
      [ "$waived_above" = 1 ] && continue
      [[ "$line" =~ $WAIVER ]] && continue
      report "$f" "$n" "$line"
    fi
  done < "$f"
done <<< "$files"

if [ "$violations" -gt 0 ]; then
  echo "test-ports: $violations fixed port(s) in tests: bind port 0 and read the port back, or add 'test-ports: ok: <reason>' when nothing binds it" >&2
  exit 1
fi
echo "test-ports: ok"
