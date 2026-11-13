#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; return 0; }
trap __aw_tmp_clean EXIT
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/record-teammate-name.sh"

test_records_a_named_dispatch() {
  local table; table="$(mktemp -d)/teammate-names.jsonl"
  AW_TEAMMATE_NAMES_FILE="$table" bash "$HOOK" <<< '{"session_id":"s1","tool_input":{"name":"builder-a","prompt":"..."}}' > /dev/null
  grep -q '"name":"builder-a"' "$table" || { echo "FAIL: expected builder-a recorded"; exit 1; }
  echo "PASS: test_records_a_named_dispatch"
}

test_does_not_record_an_unnamed_dispatch() {
  local table; table="$(mktemp -d)/teammate-names.jsonl"
  AW_TEAMMATE_NAMES_FILE="$table" bash "$HOOK" <<< '{"session_id":"s1","tool_input":{"prompt":"..."}}' > /dev/null
  [ -s "$table" ] && { echo "FAIL: expected no record for an unnamed dispatch"; exit 1; }
  echo "PASS: test_does_not_record_an_unnamed_dispatch"
}

test_always_allows() {
  local table out; table="$(mktemp -d)/teammate-names.jsonl"
  out="$(AW_TEAMMATE_NAMES_FILE="$table" bash "$HOOK" <<< '{"session_id":"s1","tool_input":{"name":"x"}}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || { echo "FAIL: expected allow, got $out"; exit 1; }
  echo "PASS: test_always_allows"
}

test_records_a_named_dispatch
test_does_not_record_an_unnamed_dispatch
test_always_allows
echo "All record-teammate-name tests passed."
