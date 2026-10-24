#!/usr/bin/env bash
# aw:prompt-sort — UserPromptSubmit hook (prompt sorter, Plan C). Sorts the real
# user prompt with `judge prompt-sort` (ONE batched Jev call) and prints a short
# scaffold note ONLY when a scaffold fires (all scaffold switches ship off, so
# by default this records a decision and prints nothing).
#
# Fails open: always exits 0; any failure, timeout (hard kill after
# AW_PROMPT_SORT_BUDGET_MS, default 4000) or bad judge output prints nothing.
# Machine text and slash commands never reach node (same prefixes as
# turn-origin.sh and judge/src/prompt-sort/tier1.ts). Never prints skill names:
# Prism's prism-route hook already does keyword skill routing.
set -uo pipefail
# HOOK-FAILOPEN: record why this hook printed nothing (config/lib/failopen.sh, installed at <hooks>/lib/failopen.sh).
# Logging only, in a subshell with its output discarded: it never changes this hook's exit code or output.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen prompt-sort "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || exit 0
command -v jq >/dev/null 2>&1 || exit 0
PROMPT="$(printf '%s' "$INPUT" | jq -r '.prompt // empty' 2>/dev/null)" || exit 0
[ -n "$PROMPT" ] || exit 0
SESSION_ID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2>/dev/null)"

case "$PROMPT" in
  "<"*|"[Request interrupted"*|"Another Claude session sent a message"*|"Base directory for this skill"*|"Caveat:"*|"This session is being continued"*) exit 0 ;;
esac
# "/bugFixOrchestrator FRN-1", "/clear": slash word then space or end. A pasted
# absolute path ("/Users/joi/x") has "/" after the first segment and passes.
if printf '%s' "$PROMPT" | grep -qE '^[[:space:]]*/[A-Za-z][A-Za-z0-9:_-]*([[:space:]]|$)'; then exit 0; fi

command -v judge >/dev/null 2>&1 || exit 0
PAYLOAD="$(jq -nc --arg p "$PROMPT" --arg s "$SESSION_ID" '{prompt:$p, sessionId:$s}' 2>/dev/null)" || exit 0
OUT_FILE="$(mktemp 2>/dev/null)" || exit 0
trap 'rm -f "$OUT_FILE"' EXIT

BUDGET_MS="${AW_PROMPT_SORT_BUDGET_MS:-4000}"
case "$BUDGET_MS" in ''|*[!0-9]*) BUDGET_MS=4000 ;; esac
TICKS=$((BUDGET_MS / 100))

( printf '%s' "$PAYLOAD" | AW_SESSION_ID="$SESSION_ID" judge prompt-sort > "$OUT_FILE" 2>/dev/null ) > /dev/null 2>&1 &
PID=$!
i=0
while kill -0 "$PID" 2>/dev/null; do
  if [ "$i" -ge "$TICKS" ]; then
    pkill -P "$PID" 2>/dev/null
    kill -9 "$PID" 2>/dev/null
    failopen timeout
    exit 0
  fi
  sleep 0.1
  i=$((i + 1))
done

# A crashed or failed judge never contributes output; the prompt proceeds.
wait "$PID" 2>/dev/null || exit 0
CONTEXT="$(jq -r '.context // empty' "$OUT_FILE" 2>/dev/null)" || exit 0
[ -n "$CONTEXT" ] && printf '%s\n' "$CONTEXT"
exit 0
