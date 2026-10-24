#!/usr/bin/env bash
# aw:external-write-guard — PreToolUse hook matching gh pr
# merge|comment|review, Linear save_*/create_*/delete_*, and Slack send_*
# tools. Pushing a feature branch and opening a PR are deliberately NOT
# matched (pushes to the base branch stay blocked by block-push-main.sh).
# Denies "needs the user" only when the current turn began with an
# auto-continue (Task 4's turn-state); allows otherwise. Deterministic
# pre-rules only — a model never decides this (repo-wide rule).
# Per RF-5: any tool call reaching this hook that isn't actually one of the
# matched external-write tools always allows — a matcher misconfiguration
# must never turn into a silent default-deny.
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
      if [ -f "$f" ]; then . "$f" && aw_failopen external-write-guard "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# No EXIT trap: on bash 3.2 a trap defers SIGTERM until a running command
# returns, so the host's timeout kill would take longer. A crash keeps its exit
# code and is not counted under Claude Code (see "Fail-open logging" in
# .agents/rules/hooks.md); under Codex/Cursor the adapter counts it.

if [ -n "${AW_JUDGE_CHILD:-}" ]; then
  echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}'
  exit 0
fi

allow() {
  echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}'
  exit 0
}
deny() {
  jq -nc --arg r "$1" '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":$r}}'
  exit 0
}

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; allow; }

TOOL_NAME="$(printf '%s' "$INPUT" | jq -r '.tool_name // empty')"
COMMAND="$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty')"

is_matched_external_write() {
  case "$TOOL_NAME" in
    Bash)
      case "$COMMAND" in
        *"gh pr merge"*|*"gh pr comment"*|*"gh pr review"*) return 0 ;;
        *) return 1 ;;
      esac
      ;;
    mcp__claude_ai_Linear__save_*|mcp__claude_ai_Linear__create_*|mcp__claude_ai_Linear__delete_*) return 0 ;;
    mcp__claude_ai_Slack__slack_send_*) return 0 ;;
    # Codex/Cursor register the same servers under their own names (the
    # Cursor adapter normalizes to mcp__<server>__<tool>): match any server
    # literally named linear/Linear or slack/Slack.
    mcp__[Ll]inear__save_*|mcp__[Ll]inear__create_*|mcp__[Ll]inear__delete_*) return 0 ;;
    mcp__[Ss]lack__slack_send_*) return 0 ;;
    *) return 1 ;;
  esac
}

# RF-5: only the exact matched set ever denies — everything else allows,
# even if this hook was mistakenly wired to a broader matcher.
# An unreadable payload has no tool name, so it can never match: log why.
is_matched_external_write || { [ -n "$TOOL_NAME" ] || failopen input "$INPUT"; allow; }

# q93 JG-01/02: a meta-harness lane or the meta itself (AW_META_TEAM and AW_META_LANE, exported by scripts/meta-lane.sh) works
# unattended by design and never has a human turn to reset auto_continued_at, so it is never denied. Interactive sessions keep the block.
[ -n "${AW_META_TEAM:-}" ] && [ -n "${AW_META_LANE:-}" ] && allow

SESSION_ID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty')"
SESSIONS_DIR="${AW_JUDGE_SESSIONS_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/judge/sessions}"
SESSION_FILE="$SESSIONS_DIR/$SESSION_ID.json"

if [ -z "$SESSION_ID" ]; then
  failopen session-id-missing
elif [ -f "$SESSION_FILE" ]; then
  AUTO_CONTINUED_AT="$(jq -r '.auto_continued_at // empty' "$SESSION_FILE" 2>/dev/null)"
  JQ_RC=$?
  [ "$JQ_RC" -ne 0 ] && [ -z "$AUTO_CONTINUED_AT" ] && failopen session-state-unreadable
  if [ -n "$AUTO_CONTINUED_AT" ]; then
    deny "This turn auto-continued without the user — external writes (merge/PR comments/reviews/Linear/Slack) need the user's own turn first."
  fi
fi

allow
