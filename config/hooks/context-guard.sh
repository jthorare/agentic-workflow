#!/usr/bin/env bash
# aw:context-guard — PostToolUse hook (lever 2B). Measures the current
# transcript's context size and, past a threshold, nudges the agent to write
# a handoff digest. Fails open unconditionally: on any error, prints nothing
# and exits 0. Never blocks a tool call — it can only add additionalContext.
#
# Cost discipline (2026-09-27 review): this fires on EVERY tool call in
# EVERY session on the box (matcher .*). It must never spawn a heavy
# subprocess (no .NET or Node start-up here). A byte-growth gate skips the real check unless the
# transcript has grown enough since the last real check to plausibly matter.
#
# Latency fix (2026-09-27, review #2): the skip path (the common case, run on
# EVERY tool call) was measured at ~68ms median, mostly one `jq` process per
# field plus a config-file read. It now does exactly one subprocess (a single
# `jq -r` producing both fields it needs in one call — a bash builtin
# read+regex extraction was considered but real hook input can carry
# arbitrary tool_input/tool_response content that may itself contain a
# `"transcript_path":"..."` or `"session_id":"..."` substring, which a
# builtin regex match can't safely disambiguate from the top-level field;
# jq's structured `.transcript_path`/`.session_id` access has no such risk),
# one `stat`/`wc` file-size read, a `read` builtin against the state file (no
# `cat`), and one arithmetic comparison. The growth-gate threshold itself is
# a constant (default 20000 bytes, override via
# AW_CONTEXT_GUARD_GROWTH_GATE_BYTES) rather than read from config.json on
# this path — config.json (thresholdTokens, growthGateBytes) is read only
# once the growth gate has already been cleared, on the full-check path.
#
# Blocker fix (2026-09-27, review #1): a real message.usage object is NOT
# flat — its key order is input_tokens, cache_creation_input_tokens,
# cache_read_input_tokens, output_tokens, then several NESTED objects
# (output_tokens_details{...}, server_tool_use{...}, cache_creation{...},
# ...). The original fast path (`grep -oE '"usage":\{[^}]*\}'`) stops at the
# first nested "}" and never matches a real usage object, so it always
# failed silently. The fix greps for the last complete "assistant" line
# (not a byte-window `tail`, which can miss it once a large tool_result
# follows) and lets jq parse that whole JSON line directly.
set -uo pipefail

[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

# One subprocess for the whole skip path's input parsing: transcript_path,
# session_id and agent_id, tab-separated, straight off stdin (no
# intermediate `cat`). agent_id is only USED on the full-check path (for the
# nudge's wording), but it's captured here too since stdin can only be read
# once and this costs nothing extra in the same jq call.
FIELDS="$(jq -r '(.transcript_path // "") + "\t" + (.session_id // "unknown") + "\t" + (.agent_id // "")' 2>/dev/null)"
[ -n "$FIELDS" ] || exit 0
TRANSCRIPT="${FIELDS%%$'\t'*}"
REST="${FIELDS#*$'\t'}"
SESSION_ID="${REST%%$'\t'*}"
AGENT_ID="${REST#*$'\t'}"
[ -n "$TRANSCRIPT" ] || exit 0
[ -f "$TRANSCRIPT" ] || exit 0

# Byte-growth gate: skip the real check entirely unless the transcript has
# grown by more than the gate since the last time we actually looked. Just a
# file-size read, a `read` builtin, and an integer comparison — no jq, no
# config read, no subshell pipeline.
GROWTH_GATE_BYTES="${AW_CONTEXT_GUARD_GROWTH_GATE_BYTES:-20000}"
STATE_DIR="${AW_CONTEXT_GUARD_STATE_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/context-guard/state}"
# `[ -d ]` is a builtin test; skip the `mkdir` subprocess on the (overwhelmingly
# common) case where the directory already exists — this alone was ~6-7ms of
# the skip path's measured latency.
[ -d "$STATE_DIR" ] || mkdir -p "$STATE_DIR" 2>/dev/null
STATE_FILE="$STATE_DIR/$SESSION_ID.size"

CURRENT_SIZE="$(stat -c%s "$TRANSCRIPT" 2>/dev/null || stat -f%z "$TRANSCRIPT" 2>/dev/null)"   # GNU first: on Linux `stat -f` is the filesystem status and succeeds with junk
if [ -z "$CURRENT_SIZE" ]; then
  CURRENT_SIZE="$(wc -c < "$TRANSCRIPT" 2>/dev/null)"
  CURRENT_SIZE="${CURRENT_SIZE//[[:space:]]/}"
fi
[ -n "$CURRENT_SIZE" ] || exit 0

LAST_SIZE=0
if [ -f "$STATE_FILE" ]; then
  read -r LAST_SIZE < "$STATE_FILE" 2>/dev/null || LAST_SIZE=0
fi
case "$LAST_SIZE" in ('' | *[!0-9]*) LAST_SIZE=0 ;; esac

GROWTH=$((CURRENT_SIZE - LAST_SIZE))
[ "$GROWTH" -ge "$GROWTH_GATE_BYTES" ] || exit 0
# Trailing newline matters: `read` reports failure on a final line with no
# newline (even though it still sets the variable), which would otherwise
# trip the `|| LAST_SIZE=0` fallback below and make the gate never persist.
printf '%s\n' "$CURRENT_SIZE" > "$STATE_FILE" 2>/dev/null || true

# --- Past this point: the full check. Config is read only here. ---
CONFIG_DIR="${AW_CONTEXT_GUARD_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/context-guard}"
CONFIG_FILE="$CONFIG_DIR/config.json"
THRESHOLD=200000
if [ -f "$CONFIG_FILE" ]; then
  RAW_THRESHOLD="$(jq -r '.thresholdTokens // empty' "$CONFIG_FILE" 2>/dev/null)"
  case "$RAW_THRESHOLD" in ('' | *[!0-9]*) ;; (*) THRESHOLD="$RAW_THRESHOLD" ;; esac
fi

# The real check: read the last 64KB (comfortably more than one assistant
# line, and enough to survive a large trailing tool_result line before it),
# find the LAST complete line whose "type" is "assistant", and let jq parse
# that whole JSON object directly — no brace-counting regex, so nested
# objects inside message.usage (output_tokens_details, server_tool_use,
# cache_creation, ...) can't break it.
LAST_ASSISTANT_LINE="$(tail -c 1048576 "$TRANSCRIPT" 2>/dev/null | grep '"type":"assistant"' | tail -n 1)"
if [ -z "$LAST_ASSISTANT_LINE" ]; then
  # Rare fallback: even a 1MB tail didn't contain an assistant line — a long
  # run of huge hook-attachment payloads after the last assistant turn can
  # push it further back than that (2026-09-27 finding, done-gate.sh's
  # identical pattern). Scan the whole file once rather than silently
  # skipping the growth check.
  LAST_ASSISTANT_LINE="$(grep '"type":"assistant"' "$TRANSCRIPT" 2>/dev/null | tail -n 1)"
fi
if [ -n "$LAST_ASSISTANT_LINE" ]; then
  TOKENS="$(printf '%s' "$LAST_ASSISTANT_LINE" | jq -r '(.message.usage.input_tokens // 0) + (.message.usage.cache_read_input_tokens // 0) + (.message.usage.cache_creation_input_tokens // 0)' 2>/dev/null)"
else
  # Codex rollout transcripts (run via config/hooks/adapters/codex.sh) have
  # no Claude "assistant" lines; their context size is the latest
  # event_msg/token_count's last_token_usage.input_tokens (which already
  # includes cached_input_tokens). Only reached when no Claude assistant
  # line exists anywhere in the file, so Claude sessions never take it.
  LAST_COUNT_LINE="$(tail -c 1048576 "$TRANSCRIPT" 2>/dev/null | grep '"type":"token_count"' | tail -n 1)"
  [ -n "$LAST_COUNT_LINE" ] || exit 0
  TOKENS="$(printf '%s' "$LAST_COUNT_LINE" | jq -r '.payload.info.last_token_usage.input_tokens // 0' 2>/dev/null)"
fi
case "$TOKENS" in ('' | *[!0-9]*) exit 0 ;; esac
[ "$TOKENS" -ge "$THRESHOLD" ] || exit 0

jq -nc --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" --arg agent "$AGENT_ID" --arg sid "$SESSION_ID" --argjson tokens "$TOKENS" \
  '{"ts": $ts, "agentId": (if $agent == "" then null else $agent end), "sessionId": $sid, "tokens": $tokens}' \
  >> "$CONFIG_DIR/fires.jsonl" 2>/dev/null || true

DIGESTS_DIR="${AW_STATE_DIR:-$HOME/.agentic-workflow}/digests"
if [ -n "$AGENT_ID" ]; then
  MSG="This session's context is now ~$TOKENS tokens (past the ${THRESHOLD}-token guard). If your task can be handed off, consider writing a short handoff to $DIGESTS_DIR/<task-slug>.md (goal, what's done, what's left, exact file paths) and returning your result now, so the orchestrator can re-dispatch a fresh agent from that digest instead of continuing here."
else
  MSG="This session's context is now ~$TOKENS tokens (past the ${THRESHOLD}-token guard). Consider writing a handoff digest to $DIGESTS_DIR/<task-slug>.md for anything you'd want a fresh session to pick up from, so future work here starts from the digest instead of re-reading everything."
fi

jq -nc --arg msg "$MSG" '{"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": $msg}}'
exit 0
