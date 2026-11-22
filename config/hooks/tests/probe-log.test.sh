#!/usr/bin/env bash
# Tests for config/hooks/probe-log.sh. Run: bash config/hooks/tests/probe-log.test.sh
set -euo pipefail
HOOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/probe-log.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
export AW_PROBE_DIR; AW_PROBE_DIR="$(mktemp -d)/probe"

out=$(echo '{"session_id":"s1","prompt":"hi"}' | bash "$HOOK" UserPromptSubmit); code=$?
check "prints nothing" "$out" ""
check "exits 0" "$code" "0"
check "records the raw input" "$(jq -c '.input' "$AW_PROBE_DIR/UserPromptSubmit.jsonl")" '{"session_id":"s1","prompt":"hi"}'
check "records the event name" "$(jq -r '.event' "$AW_PROBE_DIR/UserPromptSubmit.jsonl")" "UserPromptSubmit"
check "records an ISO timestamp" "$(jq -r '.ts | test("^[0-9]{4}-[0-9]{2}-[0-9]{2}T")' "$AW_PROBE_DIR/UserPromptSubmit.jsonl")" "true"

echo '{"a":2}' | bash "$HOOK" UserPromptSubmit
check "appends, never overwrites" "$(wc -l < "$AW_PROBE_DIR/UserPromptSubmit.jsonl" | tr -d ' ')" "2"

bash "$HOOK" Stop < /dev/null
check "empty stdin records input null" "$(jq -c '.input' "$AW_PROBE_DIR/Stop.jsonl")" "null"

out=$(echo 'not json' | bash "$HOOK" SubagentStop); code=$?
check "invalid JSON still exits 0" "$code" "0"
check "invalid JSON prints nothing" "$out" ""
check "invalid JSON is flagged" "$(jq -c '.parse_error' "$AW_PROBE_DIR/SubagentStop.jsonl")" "true"

AW_PROBE_DIR=/dev/null/cannot-create bash "$HOOK" Stop < /dev/null; code=$?
check "an unwritable probe dir still exits 0" "$code" "0"

exit $fail
