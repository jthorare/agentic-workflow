#!/usr/bin/env bash
# Codex hook adapter: runs a canonical (Claude-protocol) hook script under
# Codex's hook system (codex >= 0.158, feature `hooks`, ~/.codex/hooks.json).
#
# Usage (from ~/.codex/hooks.json):
#   ~/.agentic-workflow/hooks/adapters/codex.sh ~/.agentic-workflow/hooks/<script>.sh # aw:<id>
#
# Codex's hook protocol is Claude Code's with a few differences this adapter
# absorbs (see README.md in this directory for sources):
#   - exit 2 reads the reason from STDERR (our safety hooks print it on stdout)
#   - Stop / SubagentStop reject plain-text stdout (JSON or nothing)
#   - shell may arrive as exec_command/shell with tool_input.cmd or an argv array
#   - spawn_agent (matched as Agent) carries message/agent_type/task_name, not
#     prompt/subagent_type/name
#   - no prompt_id; turn_id plays that role (same value within one turn)
#   - Stop carries last_assistant_message; transcript_path is a Codex rollout,
#     not a Claude transcript, so it is dropped for Stop and the canonical
#     done-gate reads last_assistant_message instead
#
# Fails open: any adapter-internal failure exits 0 with no output.
set -uo pipefail
# shellcheck source=common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"
# Tell canonical hooks (and the judge they call) which host is running them.
export AW_PROVIDER="codex"

aw_adapter_parse_args "$@" || { aw_adapter_failopen codex args-invalid; exit 0; }

RAW_INPUT="$(cat 2>/dev/null || true)"
# An empty or unreadable host payload becomes {} below, which the canonical
# hook can't check: log it here, where the raw payload is still visible.
aw_adapter_failopen codex input "$RAW_INPUT"
# Logging only: the flag tells the hook's logger this payload is already counted.
[ -n "$RAW_INPUT" ] || { RAW_INPUT='{}'; export AW_FAILOPEN_INPUT_LOGGED=1; }
printf '%s' "$RAW_INPUT" | jq -e 'type == "object"' > /dev/null 2>&1 || { RAW_INPUT='{}'; export AW_FAILOPEN_INPUT_LOGGED=1; }

EVENT="$AW_ADAPTER_EVENT"
[ -n "$EVENT" ] || EVENT="$(printf '%s' "$RAW_INPUT" | jq -r '.hook_event_name // empty')"

if [ "$AW_ADAPTER_RAW" = "1" ]; then
  PAYLOAD="$RAW_INPUT"
else
  PAYLOAD="$(printf '%s' "$RAW_INPUT" | jq -c --arg ev "$EVENT" '
    .hook_event_name = (if $ev == "" then .hook_event_name else $ev end)
    | .prompt_id = (.prompt_id // .turn_id)
    | (.tool_name // "") as $tn
    | if ($tn | test("^(Bash|exec_command|shell|local_shell|unified_exec|shell_command)$")) then
        .tool_name = "Bash"
        | .tool_input = ((.tool_input // {}) | if type == "object" then . else {command: .} end)
        | .tool_input.command = (
            (.tool_input.command // .tool_input.cmd // "")
            | if type == "array" then map(tostring) | join(" ") else tostring end)
      elif ($tn | test("^(Agent|spawn_agent)$")) then
        .tool_name = "Agent"
        | .tool_input = (.tool_input // {})
        | .tool_input.prompt = (.tool_input.prompt // .tool_input.message // "")
        | .tool_input.subagent_type = (.tool_input.subagent_type // .tool_input.agent_type // "")
        | .tool_input.name = (.tool_input.name // .tool_input.task_name)
        | .tool_input.description = (.tool_input.description // .tool_input.task_name // "")
      else . end
    | if (.hook_event_name == "Stop" or .hook_event_name == "SubagentStop") then
        .transcript_path = null
      else . end
  ' 2>/dev/null)" || { aw_adapter_failopen codex payload-unparsable; exit 0; }
  [ -n "$PAYLOAD" ] || { aw_adapter_failopen codex payload-unparsable; exit 0; }
fi

# Rewrites (updatedInput) are only safe to forward when the original shell
# input used the same {command: "<string>"} shape the canonical hook rewrote.
REWRITE_OK="$(printf '%s' "$RAW_INPUT" | jq -r '
  if (.tool_name // "") == "Bash" and ((.tool_input.command // null) | type) == "string" then "1"
  elif ((.tool_name // "") | test("^(Bash|exec_command|shell|local_shell|unified_exec|shell_command)$")) then "0"
  else "1" end' 2>/dev/null)"

aw_adapter_run "$PAYLOAD" codex

case "$AW_RC" in
  0)
    OUT_JSON="$(aw_adapter_out_json)"
    if [ "$OUT_JSON" != "null" ]; then
      if [ "$REWRITE_OK" != "1" ]; then
        OUT_JSON="$(printf '%s' "$OUT_JSON" | jq -c 'del(.hookSpecificOutput.updatedInput)')"
      fi
      # Codex accepts permissionDecision "allow" only with updatedInput; a bare one fails the hook with "unsupported
      # permissionDecision:allow" (#1585). Without a rewrite, allow means "no objection", which is what no decision means
      # to Codex, so drop it, and print nothing when nothing else is left.
      OUT_JSON="$(printf '%s' "$OUT_JSON" | jq -c '
        if (.hookSpecificOutput.permissionDecision // "") == "allow" and ((.hookSpecificOutput | has("updatedInput")) | not) then
          del(.hookSpecificOutput.permissionDecision, .hookSpecificOutput.permissionDecisionReason)
          | if (.hookSpecificOutput | keys) == ["hookEventName"] then del(.hookSpecificOutput) else . end
        else . end')"
      [ "$OUT_JSON" = "{}" ] || printf '%s\n' "$OUT_JSON"
    elif [ -n "$AW_OUT" ]; then
      case "$EVENT" in
        # Plain-text stdout becomes developer context on these events.
        SessionStart|UserPromptSubmit|SubagentStart) printf '%s\n' "$AW_OUT" ;;
        # Stop/SubagentStop reject plain text; everything else would ignore it.
        *) : ;;
      esac
    fi
    [ -n "$AW_ERR" ] && printf '%s\n' "$AW_ERR" >&2
    exit 0
    ;;
  2)
    MSG="$(aw_adapter_message)"
    if [ -z "$MSG" ]; then
      case "$EVENT" in
        Stop|SubagentStop) MSG="Continue working on the task." ;;
        *) MSG="Blocked by $(basename "$AW_ADAPTER_SCRIPT")." ;;
      esac
    fi
    printf '%s\n' "$MSG" >&2
    exit 2
    ;;
  *)
    # Claude semantics: any other exit is a non-blocking hook error. Codex
    # would record it as a hook failure — keep it non-blocking and quiet.
    aw_adapter_failopen codex "hook-exit-$AW_RC"
    [ -n "$AW_ERR" ] && printf '%s\n' "$AW_ERR" >&2
    exit 0
    ;;
esac
