#!/usr/bin/env bash
# #1896: /canary reads the pre-deploy baseline that /landAndDeploy writes to releases/<id>/baseline.json.
# Every `baseline.<key>` canary's text reads must be a key landAndDeploy's text says it writes.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LAND="${CANARY_KEYS_LAND:-$ROOT/skills/landAndDeploy/SKILL.md}"
CANARY="${CANARY_KEYS_CANARY:-$ROOT/skills/canary/SKILL.md}"
fail=0

# The writer: "persist to `releases/<release-id>/baseline.json` (`{error_rate, p95, ...}`)".
written_keys() {
  grep -o 'baseline\.json` (`{[^}]*}' "$1" | head -1 | sed 's/.*{//; s/}$//' | tr ',' '\n' | tr -d ' ' | sed '/^$/d' | sort -u
}
# The reader: every `baseline.<key>` in the text, minus the file name itself.
read_keys() {
  grep -o 'baseline\.[A-Za-z_][A-Za-z0-9_]*' "$1" | sed 's/^baseline\.//' | grep -vx 'json' | sort -u
}

check() { # check <name> <land> <canary> <expect: pass|fail>
  local written read missing got
  written="$(written_keys "$2")"
  read="$(read_keys "$3")"
  if [ -z "$written" ]; then
    echo "FAIL: $1: no baseline.json key list found in $2"; fail=1; return
  fi
  missing="$(comm -13 <(printf '%s\n' "$written") <(printf '%s\n' "$read"))"
  got=pass; [ -z "$missing" ] || got=fail
  if [ "$got" = "$4" ]; then
    echo "PASS: $1"
  else
    echo "FAIL: $1: expected $4, got $got (written: $(echo $written); read but not written: $(echo $missing))"; fail=1
  fi
}

tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
printf '%s\n' '- persist to `releases/<release-id>/baseline.json` (`{error_rate, p95, sampled_at}`).' >"$tmp/land.md"
printf '%s\n' 'Read `baseline.json` -> `baseline.error_rate`, `baseline.p95`.' >"$tmp/ok.md"
printf '%s\n' 'Read `baseline.json` -> `baseline.errorRate`, `baseline.p95`.' >"$tmp/bad.md"

check "a reader of the written keys passes" "$tmp/land.md" "$tmp/ok.md" pass
check "a reader of a key nobody writes fails" "$tmp/land.md" "$tmp/bad.md" fail
check "canary reads only keys landAndDeploy writes" "$LAND" "$CANARY" pass

exit "$fail"
