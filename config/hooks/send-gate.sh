#!/usr/bin/env bash
# aw:send-gate — PreToolUse hook on SendMessage (lever 1A). Classifies every
# outgoing message via `judge wake-gate` and allows, denies-and-queues, or
# denies-as-pure-ack. Fails open to allow on any judge failure (RF-1).
set -uo pipefail
HOOK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# HOOK-FAILOPEN: record why this hook is failing open (config/lib/failopen.sh,
# installed at <hooks>/lib/failopen.sh). Logging only: it runs in a subshell
# with its output discarded, so it can never change this hook's exit code,
# stdout or stderr, whatever set -e/-u or the environment do to it.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen send-gate "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# No EXIT trap: on bash 3.2 a trap defers SIGTERM until a running $(judge …)
# returns, so the host's timeout kill would take longer. A crash keeps its exit
# code and is not counted under Claude Code (see "Fail-open logging" in
# .agents/rules/hooks.md); under Codex/Cursor the adapter counts it.
# Installed layout first (scripts/install-wake-gating.sh copies locks.sh to
# $HOOKS_DIR/lib/locks.sh alongside this script), then a raw repo-checkout
# layout (config/hooks/send-gate.sh next to config/lib/locks.sh) — a bare
# `cp` of this script alone, with neither sibling present, used to silently
# disable the whole outbox-queue/digest mechanism with no error at all
# (2026-09-27 finding); this must never happen quietly again.
if [ -f "$HOOK_DIR/lib/locks.sh" ]; then
  LOCKS_LIB="$HOOK_DIR/lib/locks.sh"
elif [ -f "$HOOK_DIR/../lib/locks.sh" ]; then
  LOCKS_LIB="$HOOK_DIR/../lib/locks.sh"
else
  LOCKS_LIB=""
fi
# shellcheck source=../lib/locks.sh — owned by Plan 6, Task 1; sourced only.
[ -n "$LOCKS_LIB" ] && source "$LOCKS_LIB" 2>/dev/null

if [ -z "$LOCKS_LIB" ] || ! command -v acquire_lock > /dev/null 2>&1; then
  # Loud fail-open: never again let a missing/broken locks.sh silently skip
  # the outbox mechanism. Allow the message through (RF-1's spirit — never
  # block a real send on our own plumbing being broken) but say so loudly,
  # via systemMessage, so it's visible in the transcript instead of a quiet
  # no-op. Recording a judge failure isn't done here: there's no CLI surface
  # for it — judge/src/db.ts's `failures` table is only ever written from
  # inside evaluate()'s own provider-failure path, not from a plain shell
  # hook — so it's skipped rather than faked.
  failopen locks-lib-missing
  jq -nc '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"},"systemMessage":"send-gate: locks.sh missing, wake gating disabled"}'
  exit 0
fi

if [ -n "${AW_JUDGE_CHILD:-}" ]; then
  echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}'
  exit 0
fi

# allow [msg]: with a msg, rewrite the message and keep every other field of the original tool input. updatedInput
# REPLACES the whole tool input, so emitting only {message} dropped the required `to` and Claude Code rejected the
# rewrite. If the rewrite can't be built, allow the original unchanged (fail open).
allow() {
  local msg="${1:-}" out=""
  if [ -n "$msg" ] && [ -n "${input:-}" ]; then
    out="$(printf '%s' "$input" | jq -ce --arg m "$msg" '(.tool_input | select(type == "object" and (.to // "") != "") | .message = $m) as $u | {"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow","updatedInput":$u}}' 2>/dev/null)" || out=""
  fi
  if [ -n "$out" ]; then
    printf '%s\n' "$out"
  else
    [ -z "$msg" ] || failopen rewrite-unbuildable
    echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}'
  fi
  exit 0
}
deny() {
  jq -nc --arg r "$1" '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":$r}}'
  exit 0
}

input="$(cat 2>/dev/null || true)"
[ -n "$input" ] || { failopen empty-input; allow; }

SESSION_ID="$(printf '%s' "$input" | jq -r '.session_id // "unknown"')"
AGENT_TYPE="$(printf '%s' "$input" | jq -r '.agent_type // empty')"
MESSAGE="$(printf '%s' "$input" | jq -r '.tool_input.message // empty')"
TO="$(printf '%s' "$input" | jq -r '.tool_input.to // empty' 2>/dev/null)"   # stderr silenced: an unparsable payload is already reported by the jq calls above
# A control message carries no message and is not a failure; a plain
# SendMessage without one means the payload shape changed.
[ -n "$MESSAGE" ] || { failopen input "$input" '.tool_name == "SendMessage" and ((.tool_input.type // "message") == "message")'; allow; }

# BLOCKER fix (Step 2, F6): a control message (shutdown_request,
# shutdown_response, ...) is never routed through the classifier —
# deterministic, checked before senderKind derivation or any judge call.
TOOL_INPUT_TYPE="$(printf '%s' "$input" | jq -r '.tool_input.type // "message"')"
[ "$TOOL_INPUT_TYPE" = "message" ] || allow

# senderKind (Probe gate item 4): agent_type absent -> main; agent_type found
# in the teammate-name table -> teammate; otherwise -> subagent.
TEAMMATE_NAMES="${AW_TEAMMATE_NAMES_FILE:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/judge/teammate-names.jsonl}"
SENDER_KIND="main"
if [ -n "$AGENT_TYPE" ]; then
  SENDER_KIND="subagent"
  if [ -f "$TEAMMATE_NAMES" ] && jq -e --arg n "$AGENT_TYPE" 'select(.name == $n)' "$TEAMMATE_NAMES" > /dev/null 2>&1; then
    SENDER_KIND="teammate"
  fi
fi

OUTBOX_DIR="${AW_OUTBOX_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/judge/outbox}"
mkdir -p "$OUTBOX_DIR"
OUTBOX_FILE="$OUTBOX_DIR/$SESSION_ID.jsonl"
LOCK_DIR="$OUTBOX_FILE.lock"

# q93 JG-09: in a meta-harness lane or the meta (AW_META_LANE set), a message to the lead (main, team-lead) or to the team's meta lane
# is never wake-gated (not dropped, not queued): the sender waits on the reply. The meta lane's name is the metaLane of the team
# config AW_META_CONFIG names ("meta" when it names none). Ordinary sessions are gated as before.
if [ -n "${AW_META_LANE:-}" ] && [ -n "$TO" ]; then
  META_LANE="meta"
  [ -n "${AW_META_CONFIG:-}" ] && [ -f "$AW_META_CONFIG" ] && META_LANE="$(jq -r '.metaLane // "meta"' "$AW_META_CONFIG" 2> /dev/null || echo meta)"
  case "$TO" in main|team-lead|"$META_LANE") allow ;; esac
fi

JUDGE_INPUT="$(jq -nc --arg text "$MESSAGE" --arg sk "$SENDER_KIND" '{text: $text, senderKind: $sk}')"
JUDGE_OUT="$(printf '%s' "$JUDGE_INPUT" | AW_SESSION_ID="$SESSION_ID" judge wake-gate 2>/dev/null)" || { failopen "judge-exit-$?"; allow; }
DECISION="$(printf '%s' "$JUDGE_OUT" | jq -r '.decision // empty' 2>/dev/null)" || { failopen judge-output-unparsable; allow; }
[ -n "$DECISION" ] || { failopen judge-decision-missing; allow; }

case "$DECISION" in
  send)
    # RF-4: mkdir-based lock around the read-then-rewrite (no flock on macOS).
    if acquire_lock "$LOCK_DIR" 5; then
      if [ -s "$OUTBOX_FILE" ]; then
        # Classify every queued item: mine (queued for this recipient, or from before items recorded one), other
        # (queued for a different recipient: kept for its own send, never merged here). Nothing expires (q93 JG-10).
        # A queue that doesn't parse leaves the file untouched and the original send goes through (fail open).
        if CLASSIFIED="$(jq -c --arg to "$TO" \
            'if (.to == null or .to == $to) then {k:"mine",e:.} else {k:"other",e:.} end' \
            "$OUTBOX_FILE" 2>/dev/null)"; then
          OTHERS="$(printf '%s\n' "$CLASSIFIED" | jq -c 'select(.k == "other") | .e' 2>/dev/null)"
          FRESH="$(printf '%s\n' "$CLASSIFIED" | jq -c 'select(.k == "mine") | .e' 2>/dev/null)"
          if [ -n "$OTHERS" ]; then printf '%s\n' "$OTHERS" > "$OUTBOX_FILE"; else : > "$OUTBOX_FILE"; fi
          release_lock "$LOCK_DIR"
          if [ -n "$FRESH" ]; then
            DIGEST="$(printf '%s\n' "$FRESH" | jq -r '.text' | paste -sd'; ' -)"
            allow "$MESSAGE

[queued progress, now attached]: $DIGEST"
          fi
        else
          failopen outbox-unreadable
          release_lock "$LOCK_DIR"
        fi
      else
        release_lock "$LOCK_DIR"
      fi
    fi
    allow
    ;;
  drop)
    deny "This was a pure ack with nothing to act on — don't send it."
    ;;
  batch|*)
    if acquire_lock "$LOCK_DIR" 5; then
      jq -nc --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --arg agent "$AGENT_TYPE" --arg to "$TO" --arg text "$MESSAGE" \
        '{ts: $ts, agentType: (if $agent == "" then null else $agent end), to: (if $to == "" then null else $to end), text: $text}' >> "$OUTBOX_FILE"
      release_lock "$LOCK_DIR"
    else
      # Could not take the lock, so the item was not queued: deny-as-queued would lose it. Send it instead.
      failopen outbox-lock-timeout
      allow
    fi
    deny "Queued: this reads as progress with nothing to act on yet. It'll ride along on your next real send, or flush automatically before you go idle."
    ;;
esac
