#!/usr/bin/env bash
# aw:turn-origin — UserPromptSubmit hook (N2, R2). Clears auto_continued_at
# on the user's next real prompt, but never for a machine-delivered one (RF-4).
# Confirmed empirically (Probe gate item 3): UserPromptSubmit never fires for
# a delivered teammate/cross-session message, so the prefix filter below is
# belt-and-braces defense, not load-bearing. Also sweeps
# judge/sessions/*.json files older than 24h (N2), since this hook already
# runs on every real prompt.
set -uo pipefail
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

INPUT="$(cat 2>/dev/null || true)"
SESSIONS_DIR="${AW_JUDGE_SESSIONS_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/judge/sessions}"

RETRY_DIR="${AW_STATE_DIR:-$HOME/.agentic-workflow}/retry-guard"

# Also the retry guard's per-session state files (issue 4248), on the same 24 h rule.
sweep_old_sessions() {
  [ -d "$SESSIONS_DIR" ] && find "$SESSIONS_DIR" -maxdepth 1 -name '*.json' -mtime +0 -delete 2>/dev/null
  [ -d "$RETRY_DIR" ] && find "$RETRY_DIR" -maxdepth 1 -name '*.json' -mtime +0 -delete 2>/dev/null
  return 0
}

if [ -n "$INPUT" ]; then
  PROMPT="$(printf '%s' "$INPUT" | jq -r '.prompt // empty' 2>/dev/null)"
  SESSION_ID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2>/dev/null)"

  if grep -qE '^(<teammate-message|Another Claude session sent a message)' <<<"$PROMPT"; then
    sweep_old_sessions
    exit 0
  fi

  if [ -n "$SESSION_ID" ] && [ -f "$SESSIONS_DIR/$SESSION_ID.json" ]; then
    jq 'del(.auto_continued_at)' "$SESSIONS_DIR/$SESSION_ID.json" > "$SESSIONS_DIR/$SESSION_ID.json.tmp" 2>/dev/null \
      && mv "$SESSIONS_DIR/$SESSION_ID.json.tmp" "$SESSIONS_DIR/$SESSION_ID.json" || true
  fi
fi

sweep_old_sessions
exit 0
