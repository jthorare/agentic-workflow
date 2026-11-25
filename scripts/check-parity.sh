#!/usr/bin/env bash
# scripts/check-parity.sh: check a parity ledger (docs/parity/slack.md; Messages spec W6, #3266).
#
# A ledger row is `| # | Function | How, and the limit | Status | Evidence |`, numbered 1..N with no gap or repeat.
# The Status cell is one status, or several joined by commas ("partial, planned M1"). Each one needs its detail,
# or the row is bare and the check fails:
#   done            an evidence path or #N in the Evidence cell
#   partial         the limit in the How cell
#   differs         the limit in the How cell (works, but Sindri-local)
#   planned Mn      a phase id (M1, E1, T2 ...); words after it, like "(render)", are free
#   missing #N      the gap issue
#   dropped: why    the reason after the colon
#   blocked         the P0 probe line that decides it ("P0 line 7") in the How cell
# --release is W6's pass: only done, partial, missing #N and dropped remain; planned, differs and blocked fail.
#
# Usage: scripts/check-parity.sh [--release] <ledger.md>
# Output: one `<file>:<line>: row <n>: <reason>` per finding, then a summary line.
# Exit: 0 no findings, 1 findings, 2 usage or an unreadable file. Bash 3.2 and BSD awk safe.
set -uo pipefail

release=0
file=""
for arg in "$@"; do
  case "$arg" in
    --release) release=1 ;;
    -*) echo "check-parity: unknown flag $arg" >&2; echo "usage: check-parity.sh [--release] <ledger.md>" >&2; exit 2 ;;
    *) if [ -n "$file" ]; then echo "usage: check-parity.sh [--release] <ledger.md>" >&2; exit 2; fi; file="$arg" ;;
  esac
done
if [ -z "$file" ]; then echo "usage: check-parity.sh [--release] <ledger.md>" >&2; exit 2; fi
if [ ! -r "$file" ] || [ -d "$file" ]; then echo "check-parity: cannot read $file" >&2; exit 2; fi

awk -F'|' -v file="$file" -v release="$release" '
function trim(s) { gsub(/^[ \t]+|[ \t]+$/, "", s); return s }
function find(n, msg) { printf "%s:%d: row %s: %s\n", file, NR, n, msg; findings++ }
function word(p) { w = tolower(p); sub(/[^a-z].*$/, "", w); return w }
function known(w) { return w == "done" || w == "partial" || w == "differs" || w == "planned" || w == "missing" || w == "dropped" || w == "blocked" }
/^\|[ \t]*[0-9]+[ \t]*\|/ {
  cells = NF - 2
  n = trim($2) + 0
  rows++
  if (n != expected + 1) find(n, "expected row " (expected + 1))
  expected = n
  if (cells != 5) { find(n, cells " cells, expected 5"); next }
  how = trim($4); status = trim($5); evidence = trim($6)
  # Split the status on commas; a piece that does not start with a status word belongs to the piece before it.
  if (status == "") { find(n, "unknown status \"\""); next }
  np = split(status, raw, ",")
  k = 0
  for (i = 1; i <= np; i++) {
    piece = trim(raw[i])
    if (k > 0 && !known(word(piece))) parts[k] = parts[k] ", " piece
    else parts[++k] = piece
  }
  for (i = 1; i <= k; i++) {
    p = parts[i]; w = word(p); rest = substr(p, length(w) + 1)
    if (!known(w)) { find(n, "unknown status \"" p "\""); continue }
    if (w == "done" && evidence == "") find(n, "done needs an evidence path or #N")
    else if ((w == "partial" || w == "differs") && how == "") find(n, w " needs its limit")
    else if (w == "planned" && rest !~ /^ +[A-Z][0-9]/) find(n, "planned needs a phase (planned M1)")
    else if (w == "missing" && rest !~ /^ +#[0-9]+/) find(n, "missing needs an issue (missing #N)")
    else if (w == "dropped" && rest !~ /^:[ \t]*[^ \t]/) find(n, "dropped needs a reason (dropped: why)")
    else if (w == "blocked" && how !~ /P0 line [0-9]/) find(n, "blocked needs the P0 probe line that decides it")
    if (release && (w == "planned" || w == "differs" || w == "blocked")) find(n, w " is not a release status")
  }
}
END {
  if (rows == 0) { printf "%s: no ledger rows\n", file; findings++ }
  printf "check-parity: %s: %d rows, %d findings\n", file, rows, findings
  exit findings > 0 ? 1 : 0
}' "$file"
