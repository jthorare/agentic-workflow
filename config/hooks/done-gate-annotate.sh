#!/usr/bin/env bash
# aw:done-gate-annotate — PostToolUse hook on Agent (subagents/teammates).
# SubagentStop can't block (confirmed: it never even fires for a subagent —
# Probe gate item 2), so this hook only annotates the Agent tool result with
# whether the dispatch's own final message plausibly satisfies its brief's
# acceptance criteria. It never blocks or denies.
set -uo pipefail
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || exit 0

TOOL_USE_ID="$(printf '%s' "$INPUT" | jq -r '.tool_use_id // empty')"
[ -n "$TOOL_USE_ID" ] || exit 0

BRIEF_JSON="$(judge brief get "$TOOL_USE_ID" 2>/dev/null || true)"
[ -n "$BRIEF_JSON" ] || exit 0

ACCEPTANCE="$(printf '%s' "$BRIEF_JSON" | jq -r '.acceptanceCriteria // empty')"
[ -n "$ACCEPTANCE" ] || exit 0

# The dispatch's own final message — tool_response is whatever shape the
# Agent tool returns; the real, generic text of interest is its `.result` or
# a plain string response. Read both shapes rather than assuming one.
# A real finished Agent result keeps its text in .content[] blocks (status "completed", no .result); .result and a plain string
# are the older shapes, kept as fallbacks (#3543).
FINAL_MESSAGE="$(printf '%s' "$INPUT" | jq -r '
  if (.tool_response | type) == "string" then .tool_response
  elif (.tool_response.content | type) == "array" then
    ([.tool_response.content[]? | select(.type == "text") | .text] | join("\n"))
  else (.tool_response.result // "")
  end
' 2>/dev/null)"

# A background spawn (status teammate_spawned, async_launched, ...) returns before the dispatch has said anything: there is
# no final message to check, and annotating it only pasted the brief back into the lead's context (#3543).
STATUS="$(printf '%s' "$INPUT" | jq -r 'if (.tool_response | type) == "object" then (.tool_response.status // "") else "" end' 2>/dev/null)"
case "$STATUS" in "" | completed) ;; *) exit 0 ;; esac
[ -n "$FINAL_MESSAGE" ] || exit 0

# Quote the criteria short: the lead wrote the brief and already holds it, so a full copy only costs context (#3543).
QUOTED="$(printf '%s' "$ACCEPTANCE" | tr '\n' ' ' | cut -c1-120)"
[ "${#ACCEPTANCE}" -le 120 ] || QUOTED="$QUOTED..."

if printf '%s' "$FINAL_MESSAGE" | grep -qiF "$(printf '%s' "$ACCEPTANCE" | cut -c1-40)"; then
  CONTEXT="Brief acceptance criteria plausibly met: \"$QUOTED\"."
else
  CONTEXT="Brief acceptance criteria NOT clearly addressed in the final message: \"$QUOTED\". Verify before treating this as done."
fi

jq -nc --arg c "$CONTEXT" '{hookSpecificOutput:{hookEventName:"PostToolUse", additionalContext:$c}}'
exit 0
