#!/usr/bin/env bash
# aw:outbox-flush — TeammateIdle hook (lever 1A). Exit 2 keeps a teammate
# working when it still has its own queued progress to send; exit 0 lets it
# go idle otherwise. Scoped by teammate_name == the queued item's agentType
# (confirmed field correspondence, 2026-09-27 probe) — not a fallback. An item with a null agentType is the lead's
# own: it matches the lead (no teammate_name) only, never a teammate (q93 JG-25).
set -uo pipefail
# HOOK-FAILOPEN: record why this hook is failing open (config/lib/failopen.sh,
# installed at <hooks>/lib/failopen.sh). Logging only: it runs in a subshell
# with its output discarded, so it can never change this hook's exit code,
# stdout or stderr, whatever set -e/-u or the environment do to it.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen outbox-flush "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# No EXIT trap: on bash 3.2 a trap defers SIGTERM until a running command
# returns, so the host's timeout kill would take longer. A crash keeps its exit
# code and is not counted under Claude Code (see "Fail-open logging" in
# .agents/rules/hooks.md); under Codex/Cursor the adapter counts it.
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }

SESSION_ID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty')"
TEAMMATE_NAME="$(printf '%s' "$INPUT" | jq -r '.teammate_name // empty')"
[ -n "$SESSION_ID" ] || { failopen input "$INPUT"; exit 0; }

OUTBOX_DIR="${AW_OUTBOX_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/judge/outbox}"
OUTBOX_FILE="$OUTBOX_DIR/$SESSION_ID.jsonl"
[ -f "$OUTBOX_FILE" ] || exit 0
[ -s "$OUTBOX_FILE" ] || exit 0

if [ -n "$TEAMMATE_NAME" ]; then
  MATCH="$(jq -r --arg n "$TEAMMATE_NAME" 'select(.agentType == $n)' "$OUTBOX_FILE" 2>/dev/null)"
  JQ_RC=$?
  if [ -z "$MATCH" ]; then
    [ "$JQ_RC" -ne 0 ] && failopen outbox-unreadable
    exit 0
  fi
fi

echo "Send your queued progress as one message now before going idle." >&2
exit 2
