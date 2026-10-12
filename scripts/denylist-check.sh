#!/usr/bin/env bash
# denylist-check.sh <denylist> <file>... — finds work identifiers in PR text without revealing them.
#
# The denylist is a file the owner writes once, outside every repo: one entry per line (team keys such as "ABC-",
# the Slack workspace domain, linear.app/<workspace>, project names). Blank lines and lines starting with # are
# ignored; each entry is a case-insensitive fixed string (a substring, so write a team key with its dash).
# Stage owners run it before marking a PR ready, over the PR title, body and commit messages written to files and
# over evidence.md and owner-check.md (context spec, "Work data stays with the owner"); they never open the denylist.
#
# Output: one "<file>:<line>" per line that holds an entry, then "denylist-check: N hit(s) in M file(s) checked".
# It never prints an entry or the text of a line, so the agent running it never reads the identifiers; a hit is
# removed by rewording the flagged line.
# Exit codes: 0 no hits; 1 hits; 2 the denylist is missing, unreadable or has no entries (fails closed), an input
# file is missing, or a usage error.
set -uo pipefail

usage() { echo "usage: scripts/denylist-check.sh <denylist> <file>..." >&2; exit 2; }
[ "$#" -ge 2 ] || usage
deny="$1"; shift

if [ ! -f "$deny" ] || [ ! -r "$deny" ]; then
  echo "denylist-check: the denylist is missing or unreadable: $deny (the owner writes it; failing closed)" >&2
  exit 2
fi
for f in "$@"; do
  [ -f "$f" ] && [ -r "$f" ] || { echo "denylist-check: $f is not a readable file" >&2; exit 2; }
done

entries="$(mktemp "${TMPDIR:-/tmp}/denylist-check.XXXXXX")" || exit 2
trap 'rm -f "$entries"' EXIT
chmod 600 "$entries"
# Entries without CR, surrounding blanks, comments or empty lines (an empty pattern would match every line).
sed -e 's/\r$//' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' "$deny" | grep -v -e '^#' -e '^$' >"$entries"
if [ ! -s "$entries" ]; then
  echo "denylist-check: the denylist has no entries: $deny (failing closed)" >&2
  exit 2
fi

hits=0
for f in "$@"; do
  # -n prints "<line>:<text>"; only the line number is kept, so the matched text never reaches the output.
  lines="$(grep -a -n -i -F -f "$entries" -- "$f" | cut -d: -f1)"
  rc=$?   # pipefail: grep's 1 (no match) or 2 (an error)
  [ "$rc" -le 1 ] || { echo "denylist-check: grep failed on $f" >&2; exit 2; }
  for n in $lines; do
    echo "$f:$n"
    hits=$((hits + 1))
  done
done

plural() { [ "$1" -eq 1 ] && echo "$1 $2" || echo "$1 $2s"; }
echo "denylist-check: $(plural "$hits" hit) in $(plural "$#" file) checked"
[ "$hits" -eq 0 ]
