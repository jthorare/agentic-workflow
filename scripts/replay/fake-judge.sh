#!/usr/bin/env bash
# scripts/replay/fake-judge.sh — the judge a replayed hook reaches (#4257, docs/hooks/replay.md "The fake judge").
# scripts/replay/run-hooks.sh puts it first on PATH as `judge` and in AW_JUDGE_CS_BIN, so a hook under replay never
# reaches the C# judge or a model.
#
#   judge <question args...> < input
#
# The key is the sha256 of the question args joined by spaces, a newline, then stdin's bytes. The shim looks the key up
# in the case's verdicts file (AW_REPLAY_VERDICTS: JSONL, line 1 a {"header": …} line, the others
# {key, itemId, label, status, decision, confidence, reasonCode, latencyMs, stdout, exit}). Found: it prints the
# recorded `stdout` and exits with the recorded `exit` (default 0). Not found, or no verdicts file: it prints
#   {"status":"unavailable","reasonCode":"replay-no-verdict","text":"judge unavailable"}
# and exits 3. Each call appends {question, key, found} to AW_REPLAY_CALLS when set; the runner counts them.
# Outside a replay (AW_REPLAY is not 1) it refuses with exit 2 and prints nothing on stdout.
set -uo pipefail

if [ "${AW_REPLAY:-}" != "1" ]; then
  echo "fake-judge: refused outside a replay (AW_REPLAY is not 1)" >&2
  exit 2
fi

question="$*"
tmp="$(mktemp "${TMPDIR:-/tmp}/aw-fake-judge.XXXXXX")" || exit 3
trap 'rm -f "$tmp"' EXIT
cat > "$tmp"
key="$( { printf '%s\n' "$question"; cat "$tmp"; } | { sha256sum 2>/dev/null || shasum -a 256; } | cut -d' ' -f1)"

verdicts="${AW_REPLAY_VERDICTS:-}"
hit=""
if [ -n "$verdicts" ] && [ -f "$verdicts" ]; then
  hit="$(jq -c --arg k "$key" 'select(type == "object" and .key == $k)' "$verdicts" 2>/dev/null | head -n 1)"
fi

calls="${AW_REPLAY_CALLS:-}"
if [ -n "$calls" ]; then
  jq -nc --arg q "$question" --arg k "$key" --argjson found "$([ -n "$hit" ] && echo true || echo false)" \
    '{question: $q, key: $k, found: $found}' >> "$calls" 2>/dev/null
fi

if [ -z "$hit" ]; then
  echo '{"status":"unavailable","reasonCode":"replay-no-verdict","text":"judge unavailable"}'
  exit 3
fi
out="$(printf '%s' "$hit" | jq -r '.stdout // empty' 2>/dev/null)"
rc="$(printf '%s' "$hit" | jq -r '.exit // 0' 2>/dev/null)"
case $rc in "" | *[!0-9]*) rc=0 ;; esac
[ -z "$out" ] || printf '%s\n' "$out"
exit "$rc"
