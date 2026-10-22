#!/usr/bin/env bash
# aw:fleet — writes one state file per agent session for the Sindri session collector (spec
# docs/superpowers/specs/2026-10-16-sindri-session-collector-design.md 5.1, 5.2, 5.4):
#   $AW_FLEET_DIR/<provider>/<sessionId>.json   (default $AW_STATE_DIR/fleet), contract schemas/fleet-state.schema.json
# Reads the hook payload on stdin. Advisory: no allow or deny, prints nothing on stdout (a UserPromptSubmit hook's stdout would reach
# the model), and always exits 0. A payload it can't read, an id that fails the pattern or an unwritable directory is a silent exit 0.
# Stores no free text: never tool_input, the notification message, the prompt or a response.
# Writes nothing when AW_FLEET_DISABLE=1 (a session the human wants unseen) or AW_JUDGE_CHILD is set (the toolkit's own claude -p).
# Provider: AW_HOOK_PROVIDER (set by the Codex and Cursor adapters; unset or claude is Claude Code) -> the catalog's AgentId.
# Bounded: no network and no CLI calls (macOS: a ps per ancestor step, to find the agent's pid and start time); one jq run, one mktemp, one mv (a PostToolUse or Notification may also fork a grep). PostToolUse (every tool call) exits before jq unless the
# file says asking.
exec 2>/dev/null
export LC_ALL=C
[ "${AW_FLEET_DISABLE:-}" = "1" ] && exit 0
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0
case "${AW_HOOK_PROVIDER:-claude}" in
  claude) PROVIDER=claude-code ;;
  codex) PROVIDER=codex ;;
  cursor) PROVIDER=cursor ;;
  *) exit 0 ;;
esac
FLEET="${AW_FLEET_DIR:-}"
if [ -z "$FLEET" ]; then
  STATE_ROOT="${AW_STATE_DIR:-}"
  [ -n "$STATE_ROOT" ] || { [ -n "${HOME:-}" ] && STATE_ROOT="$HOME/.agentic-workflow"; }
  [ -n "$STATE_ROOT" ] || exit 0
  FLEET="$STATE_ROOT/fleet"
fi
umask 077

IFS= read -r -d '' INPUT || true
[ -n "$INPUT" ] || exit 0

# The id and the event name come out of the payload without jq (both are near the top; jq re-checks them before anything is written),
# so a PostToolUse that has nothing to do costs no jq run.
RE_SID='"session_id"[[:space:]]*:[[:space:]]*"([^"]*)"'
RE_EVENT='"hook_event_name"[[:space:]]*:[[:space:]]*"([A-Za-z]+)"'
[[ $INPUT =~ $RE_SID ]] || exit 0
SID="${BASH_REMATCH[1]}"
[[ $SID =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || exit 0
[[ $INPUT =~ $RE_EVENT ]] || exit 0
EVENT="${BASH_REMATCH[1]}"
PDIR="$FLEET/$PROVIDER"
FILE="$PDIR/$SID.json"

REASON=""
case "$EVENT" in
  SessionStart|Stop|Interrupt) STATE=idle ;;   # Interrupt is Codex's (CO-1b); Claude Code has no such event
  UserPromptSubmit) STATE=running ;;
  SessionEnd) STATE=ended ;;
  PermissionRequest) STATE=asking; REASON=approval ;;
  PostToolUse)
    # Running again after an approval: only when the file says asking, else nothing to change.
    # (The grep matches the compact JSON this hook itself writes with jq -c; a pretty-printed hand edit would never clear.)
    [ -f "$FILE" ] || exit 0
    grep -q '"state":"asking"' "$FILE" || exit 0
    STATE=running ;;
  Notification)
    RE_TYPE='"notification_type"[[:space:]]*:[[:space:]]*"([a-z_]+)"'
    [[ $INPUT =~ $RE_TYPE ]] || exit 0
    case "${BASH_REMATCH[1]}" in
      permission_prompt)
        # Claude fires PermissionRequest for the same prompt, and this notification arrives seconds later with no link to a
        # request: after a quick approval it would turn running back into a tool-less asking that nothing clears. The request
        # already wrote asking approval with the tool, so the notification never writes (the probe: 2.1.302 fires both).
        exit 0 ;;
      elicitation_dialog) STATE=asking; REASON=input ;;
      idle_prompt) STATE=idle ;;
      *) exit 0 ;;
    esac ;;
  *) exit 0 ;;
esac

# The agent process: the first ancestor (at most 8 steps from the hook's parent) whose name is the provider's agent.
# Linux reads /proc. macOS has none: one 'ps -p <pid>' per step (about 8 ms each, where a listing of every process takes 130 ms or more) gives the
# parent, the start time and the name; the start time is ps's lstart read in UTC, as epoch seconds, which is the start ticks the collector keeps on macOS.
MAC_EPOCH=""
mac_epoch() { # <Mon> <day> <HH:MM:SS> <year>, UTC -> MAC_EPOCH (days from civil, no process started)
  local m d y hh mm ss era yoe doy
  case "$1" in
    Jan) m=1 ;; Feb) m=2 ;; Mar) m=3 ;; Apr) m=4 ;; May) m=5 ;; Jun) m=6 ;; Jul) m=7 ;; Aug) m=8 ;; Sep) m=9 ;; Oct) m=10 ;; Nov) m=11 ;; Dec) m=12 ;; *) return 1 ;;
  esac
  case "$2$4" in ''|*[!0-9]*) return 1 ;; esac
  IFS=: read -r hh mm ss <<< "$3"
  case "$hh$mm$ss" in ''|*[!0-9]*) return 1 ;; esac
  d=$((10#$2)); y=$((10#$4))
  [ "$m" -le 2 ] && y=$((y - 1))
  era=$((y / 400)); yoe=$((y - era * 400)); doy=$(( (153 * ((m + 9) % 12) + 2) / 5 + d - 1 ))
  MAC_EPOCH=$(( (era * 146097 + yoe * 365 + yoe / 4 - yoe / 100 + doy - 719468) * 86400 + 10#$hh * 3600 + 10#$mm * 60 + 10#$ss ))
}
PID=null
TICKS=null
if [ -r /proc/self/stat ]; then
  p="${PPID:-0}"
  for _ in 1 2 3 4 5 6 7 8; do
    [ "$p" -gt 1 ] 2>/dev/null || break
    stat="$(<"/proc/$p/stat")" || break
    comm="${stat#*(}"; comm="${comm%)*}"
    rest="${stat##*) }"
    read -r -a f <<< "$rest"
    a0=""; IFS= read -r -d '' a0 < "/proc/$p/cmdline" || true
    base="${a0##*/}"
    hit=0
    case "$PROVIDER" in
      claude-code)
        if [ "$base" = claude ] || [ "$comm" = claude ]; then hit=1
        else case "$(readlink "/proc/$p/exe")" in */claude/versions/*) hit=1 ;; esac; fi ;;
      codex) if [ "$base" = codex ] || [ "$comm" = codex ]; then hit=1; fi ;;
      cursor)
        if [ "$base" = cursor-agent ] || [ "$comm" = cursor-agent ]; then hit=1
        elif [ "$base" = agent ]; then case "$(readlink "/proc/$p/exe")" in *cursor*) hit=1 ;; esac; fi ;;
    esac
    if [ "$hit" = 1 ]; then
      case "${f[19]:-}" in ''|*[!0-9]*) ;; *) PID="$p"; TICKS="${f[19]}" ;; esac
      break
    fi
    p="${f[1]:-0}"
  done
elif [ -x /bin/ps ]; then
  p="${PPID:-0}"
  for _ in 1 2 3 4 5 6 7 8; do
    [ "$p" -gt 1 ] 2>/dev/null || break
    row="$(TZ=UTC /bin/ps -p "$p" -o ppid=,lstart=,comm=)" || break
    [ -n "$row" ] || break
    read -r parent _ mon day clock year comm <<< "$row"
    base="${comm##*/}"
    hit=0
    case "$PROVIDER" in
      claude-code) case "$base:$comm" in claude:*|*:*/claude/versions/*) hit=1 ;; esac ;;
      codex) [ "$base" = codex ] && hit=1 ;;
      cursor)
        case "$base:$comm" in
          cursor-agent:*) hit=1 ;;
          agent:*cursor*) hit=1 ;;
        esac ;;
    esac
    if [ "$hit" = 1 ]; then
      if mac_epoch "$mon" "$day" "$clock" "$year"; then PID="$p"; TICKS="$MAC_EPOCH"; fi
      break
    fi
    p="${parent:-0}"
  done
fi

NOW="${EPOCHREALTIME:-}"
[ -n "$NOW" ] || NOW="$(date +%s)"

JSON="$(jq -c --arg sid "$SID" --arg provider "$PROVIDER" --arg event "$EVENT" --arg state "$STATE" --arg reason "$REASON" \
  --argjson now "$NOW" --argjson pid "$PID" --argjson ticks "$TICKS" '
  select(.session_id == $sid and .hook_event_name == $event and (.cwd | type == "string" and length > 0))
  | (($now * 1000) | floor) as $ms
  | (.tool_name // "") as $tool
  | {
      schema: 1,
      provider: $provider,
      sessionId: $sid,
      event: $event,
      state: $state
    }
    + (if $reason != "" then {reason: $reason} else {} end)
    + (if $event == "PermissionRequest" and ($tool | type == "string" and test("^[A-Za-z0-9_.:-]{1,64}$")) then {tool: $tool} else {} end)
    + {
      at: ((($ms / 1000) | floor | strftime("%Y-%m-%dT%H:%M:%S")) + "." + (($ms % 1000) | tostring | ("000" + .) | .[-3:]) + "Z"),
      cwd: .cwd,
      transcript: ((.transcript_path // null) | if type == "string" and length > 0 then . else null end),
      agentPid: $pid,
      agentStartTicks: $ticks,
      env: {}
    }' <<< "$INPUT")" || exit 0
[ -n "$JSON" ] || exit 0

if [ ! -d "$PDIR" ]; then
  mkdir -p -m 0700 "$PDIR" || exit 0
fi
[ -e "$PDIR/.aw-fleet" ] || : > "$PDIR/.aw-fleet" || exit 0
TMP="$(mktemp "$PDIR/.tmp.XXXXXX")" || exit 0
if printf '%s\n' "$JSON" > "$TMP" && mv -f "$TMP" "$FILE"; then :; else rm -f "$TMP"; fi
exit 0
