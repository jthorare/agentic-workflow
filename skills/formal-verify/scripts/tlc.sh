#!/usr/bin/env bash
# skills/formal-verify/scripts/tlc.sh — the TLA+ tools for /formal-verify: check the box, fetch the pinned jar, run TLC.
#
# Usage: tlc.sh doctor [--jar J]
#        tlc.sh fetch [--dest DIR] [--lock F]
#        tlc.sh check <Spec.tla> [--config F] [--workers N] [--no-deadlock] [--jar J]
#        tlc.sh translate <Spec.tla> [--jar J]
#   doctor     java 11 or newer on PATH and the pinned tla2tools.jar in the cache; one line each, with the fix when missing.
#   fetch      downloads the jar named in tla2tools.lock (next to this script's directory), verifies its sha256 and installs it
#              as <DIR>/tla2tools-<version>/tla2tools.jar (DIR default ${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin).
#              An installed jar is kept when present. Prints the jar's path.
#   check      runs TLC on the spec with its .cfg (default: the spec's name with .cfg). TLC's state files go in a temp dir that
#              is removed afterwards, so nothing is written next to the spec. Prints TLC's output, then one last line:
#              'tlc: PASS distinct=<n> depth=<n>', 'tlc: VIOLATION <invariant|deadlock|property|assumption> <name>' or
#              'tlc: ERROR <first error line>'. TLC runs with -Xmx$TLC_HEAP (default 2g; a number with an optional k, m or g);
#              a run out of heap ends 'tlc: ERROR ... java.lang.OutOfMemoryError ... TLC_HEAP=<value> ...' (exit 2; a Sindri
#              step matches the text OutOfMemoryError: keep it). A TERM, INT or HUP to tlc.sh stops the java it started (TERM,
#              then KILL after 5 s), removes the temp dir and exits 143, 130 or 129.
#   translate  runs the PlusCal translator on the spec in place (the TLA+ translation goes between its BEGIN/END TRANSLATION lines).
# --jar J uses that tla2tools.jar instead of the cached one.
# Exit: 0 pass (or ok), 1 TLC found a violation (or the sha256 didn't match), 2 usage, parse or tool error,
#       3 java or the jar is missing (the fix is printed on stderr).
# Bash 3.2 compatible (macOS /bin/bash).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCK="$HERE/../tla2tools.lock"
DEST="${AW_STATE_DIR:-$HOME/.agentic-workflow}/cache/bin"

usage() { sed -n '4,8p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//' >&2; exit 2; }

cmd="${1:-}"; [ -n "$cmd" ] || usage; shift
spec="" cfg="" workers=1 deadlock=1 jar=""
case "$cmd" in check|translate) spec="${1:-}"; [ -n "$spec" ] || usage; shift ;; esac
while [ $# -gt 0 ]; do
  case "$1" in
    --config) cfg="${2:?--config needs a file}"; shift 2 ;;
    --workers) workers="${2:?--workers needs a number}"; shift 2 ;;
    --no-deadlock) deadlock=0; shift ;;
    --jar) jar="${2:?--jar needs a file}"; shift 2 ;;
    --dest) DEST="${2:?--dest needs a directory}"; shift 2 ;;
    --lock) LOCK="${2:?--lock needs a file}"; shift 2 ;;
    *) usage ;;
  esac
done
case "$workers" in ''|*[!0-9]*) echo "tlc: --workers wants a number, got '$workers'" >&2; exit 2 ;; esac

# lock_row: prints "version sha256 url" from the lock, or exits 2.
lock_row() {
  [ -r "$LOCK" ] || { echo "tlc: cannot read $LOCK" >&2; exit 2; }
  local row
  row="$(awk '!/^[[:space:]]*(#|$)/ { print $1, $2, $3; exit }' "$LOCK")"
  set -- $row
  if [ $# -ne 3 ] || ! grep -qE '^[0-9a-f]{64}$' <<<"$2"; then
    echo "tlc: $LOCK wants one row 'version sha256 url'" >&2; exit 2
  fi
  echo "$row"
}

cached_jar() { local row; row="$(lock_row)" || exit "$?"; set -- $row; echo "$DEST/tla2tools-$1/tla2tools.jar"; }

fetch_line() { echo "  fix: bash $HERE/tlc.sh fetch    (downloads the pinned tla2tools.jar, about 2 MB, and checks its sha256)"; }
java_line() {
  case "$(uname -s)" in
    Darwin) echo "  fix: brew install openjdk@21    (TLC needs java 11 or newer)" ;;
    *) echo "  fix: install a JDK or JRE 11 or newer, e.g. sudo apt-get install -y openjdk-21-jre-headless (dnf: java-21-openjdk-headless)" ;;
  esac
}

# java_major: the major version java reports (8 for 1.8.0_x), or empty when java is missing or fails -version (the macOS /usr/bin/java stub counts as missing).
java_major() {
  command -v java >/dev/null 2>&1 || return 0
  { java -version 2>&1 || true; } | awk -F'"' '/version/ { split($2, v, "."); print (v[1] == "1" ? v[2] : v[1]); exit }'
}

need_tools() {
  local major
  major="$(java_major)"
  if [ -z "$major" ]; then echo "tlc: java is not on PATH" >&2; java_line >&2; exit 3; fi
  if [ "$major" -lt 11 ] 2>/dev/null; then echo "tlc: java $major is too old" >&2; java_line >&2; exit 3; fi
  [ -n "$jar" ] || jar="$(cached_jar)"
  if [ ! -f "$jar" ]; then echo "tlc: no tla2tools.jar at $jar" >&2; fetch_line >&2; exit 3; fi
}

doctor() {
  local major rc=0
  major="$(java_major)"
  if [ -z "$major" ]; then echo "java: missing"; java_line; rc=3
  elif [ "$major" -lt 11 ] 2>/dev/null; then echo "java: $major, too old"; java_line; rc=3
  else echo "java: ok ($major)"; fi
  [ -n "$jar" ] || jar="$(cached_jar)"
  if [ -f "$jar" ]; then echo "tla2tools: ok ($jar)"; else echo "tla2tools: missing ($jar)"; fetch_line; rc=3; fi
  return "$rc"
}

fetch() {
  local version sha url target got row
  row="$(lock_row)" || exit "$?"   # lock_row runs in a subshell: pass its exit on
  set -- $row; version="$1" sha="$2" url="$3"
  target="$DEST/tla2tools-$version/tla2tools.jar"
  if [ -f "$target" ]; then echo "$target"; return 0; fi
  command -v curl >/dev/null 2>&1 || { echo "tlc: curl is not on PATH" >&2; exit 3; }
  mkdir -p "$(dirname "$target")"
  tmp="$target.partial.$$"   # global: the EXIT trap runs after fetch returns
  trap 'rm -f "$tmp"' EXIT
  curl -fsSL --retry 2 -o "$tmp" "$url" || { echo "tlc: download failed: $url" >&2; exit 2; }
  got="$( { shasum -a 256 "$tmp" 2>/dev/null || sha256sum "$tmp"; } | awk '{print $1}')"
  if [ "$got" != "$sha" ]; then
    echo "tlc: sha256 mismatch for $url: got $got, tla2tools.lock has $sha" >&2
    exit 1
  fi
  # renamed into place: an interrupted fetch never leaves a partial jar the cache check above would reuse
  mv -f "$tmp" "$target"
  echo "$target"
}

check() {
  [ -f "$spec" ] || { echo "tlc: no spec at $spec" >&2; exit 2; }
  [ -n "$cfg" ] || cfg="${spec%.tla}.cfg"
  [ -f "$cfg" ] || { echo "tlc: no config at $cfg (write one next to the spec, or pass --config)" >&2; exit 2; }
  cfg="$(cd "$(dirname "$cfg")" && pwd)/$(basename "$cfg")"   # absolute: TLC runs from the spec's directory
  local heap="${TLC_HEAP:-2g}" n
  n="${heap%[kKmMgG]}"
  case "$n" in ''|*[!0-9]*|0*) echo "tlc: TLC_HEAP wants a number with an optional k, m or g (like 2g or 512m), got '$heap'" >&2; exit 2 ;; esac
  need_tools
  local dir base out rc flags=""
  dir="$(cd "$(dirname "$spec")" && pwd)"; base="$(basename "$spec" .tla)"
  meta="$(mktemp -d "${TMPDIR:-/tmp}/tlc-meta.XXXXXX")"   # global, like tmp
  out="$meta/out.txt"
  trap 'rm -rf "$meta"' EXIT
  [ "$deadlock" = 1 ] || flags="-deadlock"
  set +e
  # in the background so a trapped signal runs now, not after java exits; exec makes $! java's own pid
  # shellcheck disable=SC2086
  (cd "$dir" && exec java -Xmx"$heap" -XX:+UseParallelGC -cp "$jar" tlc2.TLC -cleanup -metadir "$meta/states" \
    -workers "$workers" $flags -config "$cfg" "$base") >"$out" 2>&1 &
  java_pid=$!   # global: the signal traps read it
  trap 'stop_java 143' TERM
  trap 'stop_java 130' INT
  trap 'stop_java 129' HUP
  wait "$java_pid"
  rc=$?
  trap - TERM INT HUP
  set -e
  cat "$out"
  summarize "$out" "$rc"
}

# summarize OUT RC: the last line, from TLC's own messages (exit codes moved between releases; the text did not).
summarize() {
  local out="$1" rc="$2" name line
  if grep -q '^Error: Invariant .* is violated' "$out"; then
    name="$(sed -n 's/^Error: Invariant \([^ ]*\) is violated.*/\1/p' "$out" | head -1)"
    echo "tlc: VIOLATION invariant $name"; exit 1
  fi
  if grep -q '^Error: Deadlock reached' "$out"; then echo "tlc: VIOLATION deadlock reached"; exit 1; fi
  if grep -q '^Error: Temporal properties were violated' "$out"; then
    # TLC doesn't say which PROPERTY failed; name it when the config checks only one
    name="$(awk 'BEGIN { split("SPECIFICATION INIT NEXT CONSTANT CONSTANTS INVARIANT INVARIANTS PROPERTY PROPERTIES SYMMETRY VIEW CONSTRAINT CONSTRAINTS ACTION_CONSTRAINT ACTION_CONSTRAINTS CHECK_DEADLOCK POSTCONDITION ALIAS", k, " "); for (i in k) kw[k[i]] = 1 }
      { sub(/\\\*.*/, ""); if (NF == 0) next; i = 1
        if ($1 in kw) { insec = ($1 == "PROPERTY" || $1 == "PROPERTIES"); i = 2 }
        if (insec) for (; i <= NF; i++) n[++c] = $i }
      END { if (c == 1) print n[1]; else print "unnamed (the config checks " c + 0 " properties: check them one at a time to find it)" }' "$cfg")"
    echo "tlc: VIOLATION property $name"; exit 1
  fi
  if grep -q '^Error: Action property .* is violated' "$out"; then
    name="$(sed -n 's/^Error: Action property \([^ ]*\) is violated.*/\1/p' "$out" | head -1)"
    echo "tlc: VIOLATION property $name"; exit 1
  fi
  if grep -q '^Error: Assumption .* is false' "$out"; then
    name="$(sed -n 's/^Error: Assumption \(.*\) is false.*/\1/p' "$out" | head -1)"
    echo "tlc: VIOLATION assumption $name"; exit 1
  fi
  if [ "$rc" -eq 0 ] && grep -q '^Model checking completed. No error has been found.' "$out"; then
    line="$(grep -E '^[0-9]+ states generated, [0-9]+ distinct states found' "$out" | tail -1)"
    echo "tlc: PASS distinct=$(printf '%s' "$line" | awk '{print $4}') depth=$(sed -n 's/^The depth of the complete state graph search is \([0-9]*\).*/\1/p' "$out" | tail -1)"
    exit 0
  fi
  if grep -q 'OutOfMemoryError' "$out"; then
    line="$(grep -m1 'OutOfMemoryError' "$out")"
    echo "tlc: ERROR $line (TLC_HEAP=${TLC_HEAP:-2g}: raise it, or shrink the model's constants) (full output above)"
    exit 2
  fi
  # the specific line (an unknown symbol, what the parser met), not TLC's "Semantic errors:" header
  line="$(grep -m1 -E 'Could not|Encountered|Unknown|[Ee]xpect|Lexical error|[Ee]xception|^Error: ' "$out" | grep -v 'Parsing or semantic analysis failed' || true)"
  [ -n "$line" ] || line="$(grep -m1 -E 'Error|error' "$out" || true)"
  echo "tlc: ERROR ${line:-TLC exited $rc without a result} (full output above)"
  exit 2
}

# stop_java RC: TERM the java check started, KILL it after 5 s, remove the temp dir, exit RC.
stop_java() {
  local i
  kill -TERM "$java_pid" 2>/dev/null
  for i in 1 2 3 4 5 6 7 8 9 10; do
    kill -0 "$java_pid" 2>/dev/null || break
    sleep 0.5
  done
  kill -KILL "$java_pid" 2>/dev/null
  wait "$java_pid" 2>/dev/null
  rm -rf "$meta"
  exit "$1"
}

translate() {
  [ -f "$spec" ] || { echo "tlc: no spec at $spec" >&2; exit 2; }
  need_tools
  local dir base
  dir="$(cd "$(dirname "$spec")" && pwd)"; base="$(basename "$spec" .tla)"
  (cd "$dir" && java -cp "$jar" pcal.trans -nocfg "$base.tla") || { echo "tlc: the PlusCal translation failed (message above)" >&2; exit 2; }
  rm -f "$dir/$base.old"
}

case "$cmd" in
  doctor) doctor ;;
  fetch) fetch ;;
  check) check ;;
  translate) translate ;;
  *) usage ;;
esac
