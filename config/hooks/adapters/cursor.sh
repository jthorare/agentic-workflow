#!/usr/bin/env bash
# Cursor hook adapter: runs a canonical (Claude-protocol) hook script under
# Cursor's hook system (~/.cursor/hooks.json, version 1).
#
# Usage (from ~/.cursor/hooks.json):
#   ~/.agentic-workflow/hooks/adapters/cursor.sh ~/.agentic-workflow/hooks/<script>.sh # aw:<id>
#
# Normalizes Cursor's stdin to the Claude shape:
#   beforeShellExecution {command}          -> PreToolUse, tool_name Bash
#   preToolUse {tool_name Shell|Task|...}   -> PreToolUse, Shell->Bash, Task->Agent
#   beforeMCPExecution {tool_name, mcp_server_name, tool_input: "<json>"}
#                                           -> PreToolUse, tool_name mcp__<server>__<tool>
#   postToolUse {tool_output: "<json>"}     -> PostToolUse, tool_response
#   beforeSubmitPrompt                      -> UserPromptSubmit
#   sessionStart                            -> SessionStart
#   stop {loop_count}                       -> Stop, stop_hook_active = loop_count > 0,
#                                              last_assistant_message from the transcript
#   afterAgentResponse {text}               -> AfterAgentResponse, last_assistant_message = text (no transcript read);
#                                              Cursor ignores the output of this event, the adapter prints {}
#   subagentStart {subagent_id, subagent_type, task, tool_call_id}
#                                           -> SubagentStart + an Agent-shaped tool_input
#   subagentStop                            -> SubagentStop
# and session_id = conversation_id, prompt_id = generation_id,
# cwd = cwd | workspace_roots[0] | $CURSOR_PROJECT_DIR (user hooks otherwise
# run from ~/.cursor/, which would break every git-aware hook).
#
# Translates the result into Cursor's per-event output. Cursor BLOCKS a
# permission hook's action on invalid/missing JSON, so this adapter always
# prints valid JSON and fails open to "allow" on its own internal errors.
set -uo pipefail
# shellcheck source=common.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"
# Tell canonical hooks (and the judge they call) which host is running them.
export AW_PROVIDER="cursor"

default_output() {
  case "$1" in
    beforeShellExecution|beforeMCPExecution|preToolUse|subagentStart|beforeReadFile|beforeTabFileRead)
      echo '{"permission":"allow"}' ;;
    beforeSubmitPrompt) echo '{"continue":true}' ;;
    *) echo '{}' ;;
  esac
}

aw_adapter_parse_args "$@" || { aw_adapter_failopen cursor args-invalid; default_output "${AW_ADAPTER_EVENT:-}"; exit 0; }

RAW_INPUT="$(cat 2>/dev/null || true)"
# An empty or unreadable host payload becomes {} below, which the canonical
# hook can't check: log it here, where the raw payload is still visible.
aw_adapter_failopen cursor input "$RAW_INPUT"
# Logging only: the flag tells the hook's logger this payload is already counted.
[ -n "$RAW_INPUT" ] || { RAW_INPUT='{}'; export AW_FAILOPEN_INPUT_LOGGED=1; }
printf '%s' "$RAW_INPUT" | jq -e 'type == "object"' > /dev/null 2>&1 || { RAW_INPUT='{}'; export AW_FAILOPEN_INPUT_LOGGED=1; }

EVENT="$AW_ADAPTER_EVENT"
[ -n "$EVENT" ] || EVENT="$(printf '%s' "$RAW_INPUT" | jq -r '.hook_event_name // empty' 2>/dev/null)"

# Last assistant text turn from a Cursor agent transcript
# ({"role":"assistant","message":{"content":[{"type":"text","text":...}]}}).
last_assistant_text() {
  local t="$1" line
  [ -n "$t" ] && [ -f "$t" ] || return 0
  line="$(tail -c 1048576 "$t" 2>/dev/null | grep '"role":"assistant"' | grep '"type":"text"' | tail -n 1)"
  [ -n "$line" ] || return 0
  printf '%s' "$line" | jq -r '
    if (.message.content | type) == "string" then .message.content
    else ([.message.content[]? | select(.type == "text") | .text] | join("\n")) end' 2>/dev/null
}

if [ "$AW_ADAPTER_RAW" = "1" ]; then
  PAYLOAD="$RAW_INPUT"
else
  TRANSCRIPT="$(printf '%s' "$RAW_INPUT" | jq -r '.transcript_path // empty' 2>/dev/null)"
  [ -n "$TRANSCRIPT" ] || TRANSCRIPT="${CURSOR_TRANSCRIPT_PATH:-}"
  LAST_TEXT=""
  [ "$EVENT" = "stop" ] && LAST_TEXT="$(last_assistant_text "$TRANSCRIPT")"
  PAYLOAD="$(printf '%s' "$RAW_INPUT" | jq -c \
      --arg ev "$EVENT" --arg tp "$TRANSCRIPT" --arg last "$LAST_TEXT" \
      --arg pdir "${CURSOR_PROJECT_DIR:-}" '
    def jsonish: if type == "string" then (fromjson? // .) else . end;
    . as $in
    | .session_id = (.conversation_id // .session_id)
    | .prompt_id = (.generation_id // null)
    | .transcript_path = (if $tp == "" then null else $tp end)
    | .cwd = (.cwd // (.workspace_roots // [])[0] // (if $pdir == "" then null else $pdir end))
    | if $ev == "beforeShellExecution" then
        .hook_event_name = "PreToolUse" | .tool_name = "Bash"
        | .tool_input = {command: ($in.command // "")}
      elif $ev == "preToolUse" then
        .hook_event_name = "PreToolUse"
        | .tool_input = ((.tool_input // {}) | jsonish)
        | if .tool_name == "Shell" then .tool_name = "Bash"
          elif .tool_name == "Task" then
            .tool_name = "Agent"
            | .tool_input.prompt = (.tool_input.prompt // .tool_input.task // "")
          else . end
      elif $ev == "beforeMCPExecution" or $ev == "afterMCPExecution" then
        .hook_event_name = (if $ev == "beforeMCPExecution" then "PreToolUse" else "PostToolUse" end)
        | .tool_name = ("mcp__" + (($in.mcp_server_name // "unknown") | gsub("[^A-Za-z0-9_-]"; "_")) + "__" + ($in.tool_name // ""))
        | .tool_input = (($in.tool_input // {}) | jsonish)
        | .tool_response = (($in.result_json // null) | jsonish)
      elif $ev == "postToolUse" then
        .hook_event_name = "PostToolUse"
        | .tool_input = ((.tool_input // {}) | jsonish)
        | .tool_response = ((.tool_output // null) | jsonish)
        | if .tool_name == "Shell" then .tool_name = "Bash"
          elif .tool_name == "Task" then .tool_name = "Agent" else . end
      elif $ev == "beforeSubmitPrompt" then .hook_event_name = "UserPromptSubmit"
      elif $ev == "sessionStart" then .hook_event_name = "SessionStart"
      elif $ev == "sessionEnd" then .hook_event_name = "SessionEnd"
      elif $ev == "stop" then
        .hook_event_name = "Stop"
        | .stop_hook_active = ((.loop_count // 0) > 0)
        | .last_assistant_message = (if $last == "" then null else $last end)
        | .transcript_path = null
      elif $ev == "afterAgentResponse" then
        .hook_event_name = "AfterAgentResponse"
        | .last_assistant_message = (.text // null)
        | .transcript_path = null
      elif $ev == "subagentStart" then
        .hook_event_name = "SubagentStart"
        | .agent_id = .subagent_id
        | .agent_type = .subagent_type
        | .tool_name = "Agent"
        | .tool_use_id = (.tool_call_id // .subagent_id)
        | .tool_input = {
            subagent_type: (.subagent_type // ""),
            prompt: (.task // ""),
            description: ((.task // "") | split("\n")[0])
          }
      elif $ev == "subagentStop" then
        .hook_event_name = "SubagentStop"
        | .agent_type = .subagent_type
        | .last_assistant_message = (.summary // null)
        | .stop_hook_active = ((.loop_count // 0) > 0)
      else .hook_event_name = $ev end
  ' 2>/dev/null)"
  [ -n "$PAYLOAD" ] || { aw_adapter_failopen cursor payload-unparsable; default_output "$EVENT"; exit 0; }
fi

aw_adapter_run "$PAYLOAD" cursor
[ -n "$AW_ERR" ] && [ "$AW_RC" != "2" ] && printf '%s\n' "$AW_ERR" >&2

OUT_JSON="$(aw_adapter_out_json)"
[ "$AW_RC" = "0" ] || [ "$AW_RC" = "2" ] || { aw_adapter_failopen cursor "hook-exit-$AW_RC"; AW_RC=0; OUT_JSON=null; AW_OUT=""; }
MSG=""
[ "$AW_RC" = "2" ] && MSG="$(aw_adapter_message)"

RESULT="$(jq -nc \
    --arg ev "$EVENT" --argjson rc "$AW_RC" --arg msg "$MSG" --arg text "$AW_OUT" \
    --argjson out "$OUT_JSON" --argjson raw "$RAW_INPUT" --arg script "$(basename "$AW_ADAPTER_SCRIPT")" '
  ($out // {}) as $o
  | ($o.hookSpecificOutput // {}) as $h
  | (if $rc == 2 then "deny" else ($h.permissionDecision // "allow") end) as $decision
  | (if $rc == 2 then (if $msg == "" then "Blocked by " + $script + "." else $msg end)
     else ($h.permissionDecisionReason // $o.reason // "") end) as $reason
  | if ($ev | test("^(beforeShellExecution|beforeMCPExecution|preToolUse|subagentStart|beforeReadFile|beforeTabFileRead)$")) then
      if $decision == "deny" then
        {permission: "deny", user_message: $reason, agent_message: $reason}
      elif $decision == "ask" and ($ev == "beforeShellExecution" or $ev == "beforeMCPExecution") then
        {permission: "ask"} + (if $reason == "" then {} else {user_message: $reason, agent_message: $reason} end)
      else
        {permission: "allow"}
        + (if $ev == "preToolUse" and ($h.updatedInput // null) != null
             and (($raw.tool_input // null) | type) == "object"
           then {updated_input: (($raw.tool_input) + $h.updatedInput)} else {} end)
        + (if ($o.systemMessage // "") != "" then {user_message: $o.systemMessage} else {} end)
      end
    elif $ev == "beforeSubmitPrompt" then
      if $rc == 2 or ($o.decision // "") == "block" then
        {continue: false, user_message: (if $reason == "" then ($o.reason // "Blocked.") else $reason end)}
      else {continue: true} end
    elif $ev == "stop" or $ev == "subagentStop" then
      if $rc == 2 then {followup_message: (if $msg == "" then "Continue working on the task." else $msg end)}
      elif ($o.decision // "") == "block" then {followup_message: ($o.reason // "Continue working on the task.")}
      else {} end
    elif $ev == "sessionStart" then
      (if ($h.additionalContext // "") != "" then $h.additionalContext
       elif $out == null then $text else "" end) as $ctx
      | if $ctx == "" then {} else {additional_context: $ctx} end
    elif $ev == "postToolUse" or $ev == "postToolUseFailure" then
      (if $rc == 2 then $msg else ($h.additionalContext // "") end) as $ctx
      | if $ctx == "" then {} else {additional_context: $ctx} end
    else {} end
' 2>/dev/null)"

if [ -n "$RESULT" ]; then
  printf '%s\n' "$RESULT"
else
  aw_adapter_failopen cursor result-unparsable
  default_output "$EVENT"
fi
exit 0
