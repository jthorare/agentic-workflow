#!/usr/bin/env bash
# scripts/code-index-lock.sh — check the shape of context/code-index.lock, the pins the Sindri code index is built from
# (spec docs/superpowers/specs/2026-10-19-sindri-code-index-design.md §9), and its parity with sindri/package.json.
#
# Usage: scripts/code-index-lock.sh check [--file F] [--requirements F] [--package F]
#        scripts/code-index-lock.sh get <graphify-version|graphify-date|requirements-sha256|model|model-digest|disk-bytes> [--file F]
#   check  every row is well formed, there is exactly one graphify, one ollama-model and one need disk-bytes row, the sha256 of the
#          requirements file (default: code-index-requirements.txt next to the lock) is the graphify row's, and the graphify version
#          and date equal sindri/package.json's sindri.graphifyPin and sindri.graphifyPinDate. Prints one line per problem:
#          'CODEIDX-LOCK-00n <line>: <what>'. Needs jq for the package.json parity.
#   get    print one pinned value (what scripts/install-deps.sh reads); runs no check, prints nothing and exits 1 when the row is absent.
# Rows (whitespace-separated; '#' comments and blank lines are ignored):
#   graphify <version> <exclude-newer-date> <requirements-sha256>   exactly one; version X.Y.Z, date YYYY-MM-DDTHH:MM:SSZ
#   ollama-model <name> <digest>                                    exactly one; name ^[a-z0-9][a-z0-9._/-]*(:[A-Za-z0-9_][A-Za-z0-9_.-]*)?$ with no
#                                                                   "cloud" in it, digest sha256: and 64 lowercase hex digits
#                                                                   (a placeholder digest is refused: the pin must be real)
#   need disk-bytes <n>                                             exactly one; 1 to 18 digits, positive
#   a line holds no NUL, CR, VT or FF (the lock is LF-only).
# Codes: 001 graphify row (missing, repeated, bad version or date), 002 a malformed value, 003 ollama-model row (missing, repeated),
#        004 need row (missing, repeated, not a number), 005 unknown row kind or need name, 006 the requirements file is missing or its
#        sha256 differs from the graphify row, 007 the graphify row differs from sindri/package.json.
# Exit: 0 ok, 1 a check failed, 2 usage or I/O error.
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() { sed -n '5,6p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

cmd="${1:-}"; { [ "$cmd" = "check" ] || [ "$cmd" = "get" ]; } || usage; shift
key=""
if [ "$cmd" = "get" ]; then key="${1:-}"; [ -n "$key" ] || usage; shift; fi
file="$ROOT/context/code-index.lock"; reqs=""; pkg="$ROOT/sindri/package.json"
while [ $# -gt 0 ]; do
  case "$1" in
    --file) file="${2:?--file needs a path}"; shift 2 ;;
    --requirements) reqs="${2:?--requirements needs a path}"; shift 2 ;;
    --package) pkg="${2:?--package needs a path}"; shift 2 ;;
    *) usage ;;
  esac
done
[ -r "$file" ] || { echo "code-index-lock: cannot read $file" >&2; exit 2; }
[ -n "$reqs" ] || reqs="$(dirname "$file")/code-index-requirements.txt"

if [ "$cmd" = "get" ]; then
  LC_ALL=C awk -v k="$key" '
    $1 == "graphify" && k == "graphify-version" { print $2; found = 1; exit }
    $1 == "graphify" && k == "graphify-date" { print $3; found = 1; exit }
    $1 == "graphify" && k == "requirements-sha256" { print $4; found = 1; exit }
    $1 == "ollama-model" && k == "model" { print $2; found = 1; exit }
    $1 == "ollama-model" && k == "model-digest" { print $3; found = 1; exit }
    $1 == "need" && $2 == "disk-bytes" && k == "disk-bytes" { print $3; found = 1; exit }
    END { exit !found }
  ' "$file"
  exit $?
fi

# A NUL byte anywhere is refused before awk (which would truncate at it).
if [ "$(LC_ALL=C tr -d '\000' <"$file" | wc -c)" -ne "$(LC_ALL=C wc -c <"$file")" ]; then
  echo "CODEIDX-LOCK-002 0: the file contains a NUL byte"
  exit 1
fi
ctl="$(LC_ALL=C grep -n $'[\r\v\f]' "$file" | cut -d: -f1 | head -n1 || true)"
if [ -n "$ctl" ]; then
  echo "CODEIDX-LOCK-002 $ctl: the line contains a CR, VT or FF control character"
  exit 1
fi

sha256_of() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi; }

rc=0
out="$(LC_ALL=C awk '
  function bad(code, msg) { printf "%s %d: %s\n", code, NR, msg; failed = 1 }
  function hex(s, n) { return s ~ /^[0-9a-f]+$/ && length(s) == n }
  /^[[:space:]]*(#|$)/ { next }
  $1 == "graphify" {
    graphs++
    if (NF != 4) { bad("CODEIDX-LOCK-001", "graphify wants 3 fields (version date requirements-sha256), got " (NF - 1)); next }
    if ($2 !~ /^[0-9]+\.[0-9]+\.[0-9]+$/) bad("CODEIDX-LOCK-001", "version \"" $2 "\" is not X.Y.Z")
    if ($3 !~ /^[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]T[0-9][0-9]:[0-9][0-9]:[0-9][0-9]Z$/) bad("CODEIDX-LOCK-002", "date \"" $3 "\" is not YYYY-MM-DDTHH:MM:SSZ")
    if (!hex($4, 64)) bad("CODEIDX-LOCK-002", "requirements sha256 is not 64 lowercase hex digits")
    next
  }
  $1 == "ollama-model" {
    models++
    if (NF != 3) { bad("CODEIDX-LOCK-003", "ollama-model wants 2 fields (name digest), got " (NF - 1)); next }
    if ($2 !~ /^[a-z0-9][a-z0-9._\/-]*(:[A-Za-z0-9_][A-Za-z0-9_.-]*)?$/ || length($2) > 200) bad("CODEIDX-LOCK-002", "model name \"" $2 "\" is not a lowercase Ollama model name with an optional :tag")
    if ($2 ~ /[Cc][Ll][Oo][Uu][Dd]/) bad("CODEIDX-LOCK-002", "model " $2 " is a cloud model; the index never sends code off the machine")
    if ($3 !~ /^sha256:[0-9a-f]+$/ || length($3) != 71) bad("CODEIDX-LOCK-002", "digest is not sha256: and 64 lowercase hex digits (a placeholder is not a pin)")
    next
  }
  $1 == "need" {
    if (NF != 3) { bad("CODEIDX-LOCK-004", "need wants 2 fields (name number), got " (NF - 1)); next }
    if ($2 != "disk-bytes") { bad("CODEIDX-LOCK-005", "need \"" $2 "\" is not disk-bytes"); next }
    if ($3 !~ /^[0-9]+$/ || length($3) > 18 || $3 + 0 == 0) bad("CODEIDX-LOCK-004", $2 " \"" $3 "\" is not a positive integer of at most 18 digits")
    if (disk++) bad("CODEIDX-LOCK-004", "disk-bytes is listed twice")
    next
  }
  { bad("CODEIDX-LOCK-005", "unknown row kind \"" $1 "\"") }
  END {
    if (graphs == 0) { print "CODEIDX-LOCK-001 0: no graphify row"; failed = 1 }
    if (graphs > 1) { print "CODEIDX-LOCK-001 0: " graphs " graphify rows, want 1"; failed = 1 }
    if (models == 0) { print "CODEIDX-LOCK-003 0: no ollama-model row"; failed = 1 }
    if (models > 1) { print "CODEIDX-LOCK-003 0: " models " ollama-model rows, want 1"; failed = 1 }
    if (!disk) { print "CODEIDX-LOCK-004 0: no need disk-bytes row"; failed = 1 }
    exit failed
  }
' "$file")" || rc=1
[ -z "$out" ] || printf '%s\n' "$out"

# The requirements file the graphify row hashes, and the TS pin it must equal (only when the row itself parsed).
want_sha="$(awk '$1 == "graphify" && NF == 4 { print $4; exit }' "$file")"
if [ -n "$want_sha" ]; then
  if [ ! -r "$reqs" ]; then
    echo "CODEIDX-LOCK-006 0: cannot read the requirements file $reqs"; rc=1
  else
    got_sha="$(sha256_of "$reqs")"
    if [ "$got_sha" != "$want_sha" ]; then
      echo "CODEIDX-LOCK-006 0: $(basename "$reqs") has sha256 $got_sha, the graphify row says $want_sha"; rc=1
    fi
  fi
  if [ ! -r "$pkg" ] || ! command -v jq >/dev/null 2>&1; then
    echo "code-index-lock: needs jq and a readable $pkg" >&2; exit 2
  fi
  pin="$(jq -r '.sindri.graphifyPin // empty' "$pkg")"; pin_date="$(jq -r '.sindri.graphifyPinDate // empty' "$pkg")"
  lock_ver="$(awk '$1 == "graphify" && NF == 4 { print $2; exit }' "$file")"; lock_date="$(awk '$1 == "graphify" && NF == 4 { print $3; exit }' "$file")"
  if [ "$pin" != "$lock_ver" ]; then
    echo "CODEIDX-LOCK-007 0: graphify version is $lock_ver, sindri/package.json's sindri.graphifyPin is ${pin:-absent}"; rc=1
  fi
  if [ "$pin_date" != "$lock_date" ]; then
    echo "CODEIDX-LOCK-007 0: graphify date is $lock_date, sindri/package.json's sindri.graphifyPinDate is ${pin_date:-absent}"; rc=1
  fi
fi
exit "$rc"
