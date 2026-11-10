#!/usr/bin/env bash
# scripts/ci/test-ports.sh — static check: no shell test listens on a fixed port (#2597).
#
# Two gates ran stack.test.sh at once, both bound its literal PORT=39917, and one failed with "address in use" (#2596). A port that
# is written into a test is shared by every checkout and every concurrent gate on the box. The rule: a test asks the kernel for a
# free port (port 0, or a helper that reads the bound port back) and passes that value around in a variable.
#
# Scanned: every tracked *.test.sh. A violation is a non-comment line with
#   - a listener on a literal non-zero port: nc / ncat with an -l flag or --listen and a number (nc -l 39917, nc -lk 8080,
#     nc -l -p 9000, nc -lp9000, ncat --listen 9000), socat TCP-LISTEN:N in any case (also TCP4- and TCP6-), `http.server N` with
#     or without flags before N, a python bind((host, N)) or *Server((host, N), H), or a node .listen(N) with N >= 1024 (a smaller
#     number is a socket backlog), with N literal digits;
#   - a word that is port or ends in _port, in any case, assigned literal non-zero digits (PORT=39917, port=39917, AW_PORT=3100,
#     export X_PORT=1, local srv_port=5, X_PORT=7410 cmd), or given one as a dollar-brace default (PORT:-39918, AW_PORT-3100) (#2635).
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

# -l flags with the number after a space (nc -l 1, nc -l -p 1) or glued to them (nc -lp1), and ncat's --listen (#2635)
NC_LISTEN='(^|[^A-Za-z0-9_./-])(nc|ncat)[[:space:]]([^#]*[[:space:]])?(-[A-Za-z]*l[A-Za-z]*|--listen)([[:space:]]([^#]*[[:space:]])?)?[1-9][0-9]*([[:space:]]|$)'
SOCAT_LISTEN='[Tt][Cc][Pp][46]?-[Ll][Ii][Ss][Tt][Ee][Nn]:[1-9]'
# the port may come after flags and their values (http.server --bind 127.0.0.1 1); a dotted host never ends in a lone number
HTTP_SERVER='http\.server[[:space:]]([^#]*[[:space:]])?[1-9][0-9]*([[:space:]]|$)'
# s.bind((host, N)), and the servers that take the same tuple: HTTPServer((host, N), H), socketserver.TCPServer((host, N), H)
PY_BIND='(bind|Server)\(\([^,)]*,[[:space:]]*[1-9][0-9]*[[:space:]]*\)'
# four digits or more: a smaller number is a python socket's backlog (s.listen(1)), and a test can't bind a port under 1024 anyway
NODE_LISTEN='\.listen\([[:space:]]*[1-9][0-9]{3,}[[:space:]]*[,)]'
# a word that is port or ends in _port, in any case: PORT=1, port=1, srv_port=1 (never report=1 or passport=1)
PORT_ASSIGN='(^|[^A-Za-z0-9])([A-Za-z0-9_]*_)?[Pp][Oo][Rr][Tt]=["'"'"']?[1-9][0-9]*(["'"'"']|[^0-9A-Za-z_$.]|$)'
# a literal default for such a word, in dollar-brace form: PORT:-1, AW_PORT-1, port:=1
PORT_DEFAULT='\$\{([A-Za-z0-9_]*_)?[Pp][Oo][Rr][Tt]:?[-=]["'"'"']?[1-9][0-9]*["'"'"']?\}'
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
      || [[ "$line" =~ $PY_BIND ]] || [[ "$line" =~ $NODE_LISTEN ]] || [[ "$line" =~ $PORT_ASSIGN ]] || [[ "$line" =~ $PORT_DEFAULT ]]; then
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
