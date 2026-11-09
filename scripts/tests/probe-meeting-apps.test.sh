#!/usr/bin/env bash
# Tests for scripts/probe-meeting-apps.sh with a stub curl. Run: bash scripts/tests/probe-meeting-apps.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIX="$ROOT/tests/fixtures/probe-meeting-apps"
SCRIPT="$ROOT/scripts/probe-meeting-apps.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { # name haystack needle
  if grep -qF -- "$3" <<<"$2"; then echo "ok - $1"; else echo "not ok - $1 (missing: $3)"; sed 's/^/    /' <<<"$2"; fail=1; fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
GKEY="gran_SUPERSECRETKEY123" WKEY="wispr_SUPERSECRETKEY456"
WURL=https://api.wispr.example/v1/meetings
mkdir -p "$WORK/bin" "$WORK/home"
cp "$FIX/curl" "$WORK/bin/"
export HOME="$WORK/home" STUB_LOG="$WORK/stub.log" PATH="$WORK/bin:$PATH"
unset AW_GRANOLA_PROBE_KEY AW_GRANOLA_PROBE_URL AW_WISPR_FLOW_PROBE_KEY AW_WISPR_FLOW_PROBE_URL STUB_FAIL

run() { # env assignments first, then script args after a literal --; sets $out and $rc
  local envs=() args=()
  while [ $# -gt 0 ] && [ "$1" != -- ]; do envs+=("$1"); shift; done
  [ "${1:-}" = -- ] && shift
  args=("$@")
  out="$(env ${envs[@]+"${envs[@]}"} bash "$SCRIPT" ${args[@]+"${args[@]}"} 2>&1)"; rc=$?
}

# --- both apps: one line each, never the token or a meeting title ---
run AW_GRANOLA_PROBE_KEY="$GKEY" AW_WISPR_FLOW_PROBE_KEY="$WKEY" AW_WISPR_FLOW_PROBE_URL="$WURL"
check "both: exit 0" "$rc" 0
has "granola line says mcp and counts tools" "$out" "granola: mcp tools=2 names=list_meetings,get_meeting_notes"
has "wispr-flow line says rest" "$out" "wispr-flow: rest status=200"
check "one line per app in the paste block" "$(sed -n '/^```$/,$p' <<<"$out" | grep -cE '^(granola|wispr-flow): ')" 2
check "the token never prints" "$(grep -c -e "$GKEY" -e "$WKEY" <<<"$out")" 0
check "no meeting title prints" "$(grep -c SECRET-MEETING-TITLE <<<"$out")" 0
check "the token never goes to curl as an argument" "$(grep -c -e "$GKEY" -e "$WKEY" "$STUB_LOG")" 0

# --- an app without a key is skipped ---
run AW_GRANOLA_PROBE_KEY="$GKEY"
check "one key: exit 0" "$rc" 0
has "the other app is skipped" "$out" "wispr-flow: skipped (no key)"

# --- --app limits the probe ---
run AW_GRANOLA_PROBE_KEY="$GKEY" AW_WISPR_FLOW_PROBE_KEY="$WKEY" AW_WISPR_FLOW_PROBE_URL="$WURL" -- --app granola
check "--app granola: exit 0" "$rc" 0
check "--app granola: no wispr-flow line" "$(grep -c '^wispr-flow:' <<<"$out")" 0

# --- no key at all: exit 2 ---
run X=1
check "no key: exit 2" "$rc" 2
has "no key: says so" "$out" "probe: no key"

# --- a failed call: exit 1 and names the app ---
run AW_GRANOLA_PROBE_KEY="$GKEY" STUB_FAIL=granola
check "failed call: exit 1" "$rc" 1
has "failed call: names the app" "$out" "failed: granola"
check "failed call: key still not printed" "$(grep -c -e "$GKEY" <<<"$out")" 0

# --- an app with a key but no endpoint fails clearly ---
run AW_WISPR_FLOW_PROBE_KEY="$WKEY"
check "no endpoint: exit 1" "$rc" 1
has "no endpoint: says which variable" "$out" "AW_WISPR_FLOW_PROBE_URL"

# --- usage ---
run AW_GRANOLA_PROBE_KEY="$GKEY" -- --nope
check "bad flag: exit 2" "$rc" 2
run AW_GRANOLA_PROBE_KEY="$GKEY" -- --app zoom
check "bad app: exit 2" "$rc" 2

if [ "$fail" -eq 0 ]; then echo "all ok"; else echo "FAILED"; exit 1; fi
