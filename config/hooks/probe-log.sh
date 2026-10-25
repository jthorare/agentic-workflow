#!/usr/bin/env bash
# aw:probe — hook-input probe (cheap-agent-harness rollout step 0.5).
# Usage: probe-log.sh <event> [provider]
# Appends the raw hook input to $AW_PROBE_DIR/<event>.jsonl (Claude Code,
# the default) or $AW_PROBE_DIR/<provider>/<event>.jsonl for any other
# provider, so a reader of the top-level *.jsonl files sees Claude Code input
# unchanged.
# Logging only: prints nothing and always exits 0, so it can never block work.
EVENT="${1:-unknown}"
PROVIDER="${2:-claude}"
DIR="${AW_PROBE_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/probe}"
[ "$PROVIDER" = "claude" ] || DIR="$DIR/$PROVIDER"
TS="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
mkdir -p "$DIR" 2>/dev/null || exit 0
INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || INPUT=null
if ! printf '%s' "$INPUT" \
    | jq -c --arg ts "$TS" --arg ev "$EVENT" '{"ts": $ts, "event": $ev, "input": .}' \
    >> "$DIR/$EVENT.jsonl" 2>/dev/null; then
  printf '{"ts":"%s","event":"%s","input":null,"parse_error":true}\n' "$TS" "$EVENT" \
    >> "$DIR/$EVENT.jsonl" 2>/dev/null
fi
exit 0
