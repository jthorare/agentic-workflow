#!/usr/bin/env bash
# aw:meta-board-line — UserPromptSubmit and PostToolUse hook (Claude Code lanes). Prints the one-line team board
# (`aw-meta board-line`: the latest main sha, the pull requests merged into it and the lane map version) as additionalContext, once per
# change: aw-meta remembers what each lane saw, so an unchanged board prints nothing. This replaces the "main moved" and "LANE MAP"
# messages meta used to send every lane. Non-Claude lanes get the same line from meta-lane-gate.sh, next to lane-inbox.
# Acts only in a lane (AW_META_TEAM and AW_META_LANE set by scripts/meta-lane.sh). Fail open: a missing binary or any error prints
# nothing and exits 0; a failure is logged as a fail-open.
set -uo pipefail
failopen() { :; }   # baseline: the hook without its fail-open logging
# A pass-through reads its stdin first, so the host's write never meets a closed pipe.
pass() { cat > /dev/null 2>&1; exit 0; }
[ -n "${AW_JUDGE_CHILD:-}" ] && pass
[ -n "${AW_META_TEAM:-}" ] && [ -n "${AW_META_LANE:-}" ] || pass
META="${AW_META_BIN:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-meta}"
[ -x "$META" ] || pass
INPUT="$(cat 2> /dev/null || true)"
EVENT="$(printf '%s' "$INPUT" | jq -r '.hook_event_name // empty' 2> /dev/null)" || { failopen input-unparsable; exit 0; }
case "$EVENT" in UserPromptSubmit|PostToolUse) ;; *) exit 0 ;; esac
OUT="$(AW_LOG_LEVEL=none AW_META_HOOK="$EVENT" "$META" board-line 2> /dev/null)"; rc=$?
[ "$rc" = 0 ] || { failopen "board-line-exit-$rc"; exit 0; }
[ -n "$OUT" ] || exit 0
jq -nc --arg e "$EVENT" --arg m "$OUT" '{hookSpecificOutput: {hookEventName: $e, additionalContext: $m}}' 2> /dev/null || true
exit 0
