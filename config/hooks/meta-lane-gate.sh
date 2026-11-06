#!/usr/bin/env bash
# aw:meta-lane-gate — the lane hook for a non-Claude Meta lane (Codex, Cursor), installed user-wide by scripts/install-meta.sh
# through the provider adapters (config/hooks/adapters/README.md). Claude lanes use meta-stop-gate.sh and SendMessage instead.
# It acts only in a lane: AW_META_TEAM and AW_META_LANE set by scripts/meta-lane.sh; anywhere else it exits 0 at once.
#   UserPromptSubmit (Codex)              prints `aw-meta lane-inbox` as plain text: the adapter makes it developer context
#   SessionStart, PostToolUse (Cursor)    the same text as hookSpecificOutput.additionalContext (cursor.sh -> additional_context)
#   AfterAgentResponse (Cursor)           records the reply from the event's text (Cursor's stop payload carries none) and never
#                                         asks lane-stop-check; the Stop that follows does not record the same text again
#   Stop                                  records the reply, then asks `aw-meta lane-stop-check`: the turn's last assistant
#                                         text after a `REPLY TO META:` line goes to `aw-meta lane-reply --stdin` (the hook runs
#                                         outside the agent's sandbox, so the agent never needs to write the state dir); exit 1
#                                         from lane-stop-check = the lane still owes a reply -> exit 2 with the reason on stderr
#                                         (Codex: the stop is blocked; Cursor: followup_message, capped by loop_limit)
# The team's meta lane has no lane inbox: UserPromptSubmit / SessionStart / PostToolUse print `aw-meta inbox` the same way.
# Every aw-meta call carries AW_META_HOOK=<event>: only a hook's call counts as hook proof (a hand-typed run does not). Each run
# writes one line to lane-sessions/<lane>.hook, "<UTC time> meta-lane-gate <event> <outcome>", which `aw-meta health` reads to name
# why a lane's hooks are not verified (outcomes: ran, not-own-agent, aw-meta-missing, meta-lane, lane-inbox-failed,
# lane-reply-failed, lane-stop-check-failed, reply-unmarked: a Stop that owes a reply after an answer with no marker line). A failure is not overwritten by a later `ran` of the same run. Never fails the hook.
# Only lane-stop-check exit 1 blocks. Exit 0, exit 2 (variables unset, no team config, a tool error) and anything else pass,
# the cause logged by failopen.
set -uo pipefail
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen meta-lane-gate "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# A pass-through reads its stdin first, so the host's write never meets a closed pipe.
pass() { cat > /dev/null 2>&1; exit 0; }
[ -n "${AW_JUDGE_CHILD:-}" ] && pass
[ -n "${AW_META_TEAM:-}" ] && [ -n "${AW_META_LANE:-}" ] || pass

# own_agent: whether this hook runs for the lane's own agent. scripts/meta-lane.sh exports AW_META_LANE_PID (its own pid), AW_META_LANE_AGENT
# (the catalog id) and AW_META_LANE_PROC (the agent's process name) in every lane. The lane's agent root is whatever process meta-lane.sh
# started (the ancestor whose parent is AW_META_LANE_PID), however it is named: a native binary, or the node wrapper of an npm install that
# spawns the native one. Not the lane's: no AW_META_LANE_PID (not started by a lane), a Claude lane (its hooks are Claude's; a `codex exec`
# inside it is nobody's), no lane script among the ancestors, or a nested agent instance, that is, a second stretch of ancestors named like
# the agent below the root, behind a tool process such as a shell (a `codex exec` or `cursor-agent -p` the agent ran; it inherits the lane
# variables). Forks of one instance share its command line and count as one stretch. A codex a human starts after `exec bash -i` in the
# pane keeps the lane script's pid, so it counts as the lane's own. If ps can't answer, the hook acts.
named_like_agent() { # <pid>: its command name or its argv[0] is the agent's process name (sets AGENT_ARGS)
  local c a
  c="$(ps -o comm= -p "$1" 2>/dev/null)"; a="$(ps -o args= -p "$1" 2>/dev/null)"; AGENT_ARGS="$a"; a="${a%% *}"; a="${a##*/}"
  [ -n "${AW_META_LANE_PROC:-}" ] && { [ "$c" = "$AW_META_LANE_PROC" ] || [ "$a" = "$AW_META_LANE_PROC" ]; }
}
own_agent() {
  [ -n "${AW_META_LANE_PID:-}" ] || return 1
  [ "${AW_META_LANE_AGENT:-}" != claude-code ] || return 1
  local cur=$$ par runs=0 i=0 prev_named=0 prev_args=""
  while [ "$i" -lt 50 ]; do
    i=$((i + 1))
    par="$(ps -o ppid= -p "$cur" 2>/dev/null | tr -d '[:space:]')"
    case "$par" in ''|*[!0-9]*) return 0 ;; esac
    if [ "$par" = "$AW_META_LANE_PID" ]; then   # $cur is the agent's root
      [ "$runs" -le 1 ]
      return
    fi
    [ "$par" -gt 1 ] || return 1
    cur="$par"
    if named_like_agent "$cur"; then
      if [ "$prev_named" = 0 ] || [ "$AGENT_ARGS" != "$prev_args" ]; then runs=$((runs + 1)); fi
      prev_named=1; prev_args="$AGENT_ARGS"
    else
      prev_named=0
    fi
  done
  return 1
}

LANE_DIR="${AW_META_DIR:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/meta}/lane-sessions"
# A lane name becomes a path component (the hook trace, the reply marker): letters, digits, '.', '_' and '-', and not only dots.
{ [[ "$AW_META_LANE" =~ ^[A-Za-z0-9._-]+$ ]] && [[ "$AW_META_LANE" =~ [^.] ]]; } || { failopen lane-name-invalid; pass; }
REPLIED="$LANE_DIR/$AW_META_LANE.replied"   # the checksum of the reply last recorded
OWED="$LANE_DIR/$AW_META_LANE.owed"         # the checksum of the owed list the last block showed
EVENT=""; TRACE_FAILED=0
# trace <outcome>: the one-line hook trace. Never fails the hook; a failure outcome stays for the rest of the run.
trace() {
  [ "$TRACE_FAILED" = 1 ] && return 0
  case "$1" in *-failed) TRACE_FAILED=1 ;; esac
  { mkdir -p "$LANE_DIR" && printf '%s meta-lane-gate %s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "${EVENT:-unknown}" "$1" > "$LANE_DIR/$AW_META_LANE.hook"; } 2> /dev/null || true
}
# code_of <output>: "-AW-META-NNN" when aw-meta's first line is a coded error, appended to a failopen cause (the code, never the text)
code_of() { if [[ "$1" =~ ^(AW-META-[0-9][0-9][0-9]): ]]; then printf -- '-%s' "${BASH_REMATCH[1]}"; fi; }

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
command -v jq > /dev/null 2>&1 || { failopen jq-missing; exit 0; }
EVENT="$(printf '%s' "$INPUT" | jq -r '.hook_event_name // empty' 2>/dev/null)" || { failopen input-unparsable; exit 0; }
export AW_META_HOOK="$EVENT"   # every aw-meta call below is a hook's: it names the event

META="${AW_META_BIN:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-meta}"
[ -x "$META" ] || { trace aw-meta-missing; exit 0; }   # not installed: nothing to gate
own_agent || { trace not-own-agent; exit 0; }          # a nested agent instance inside the lane is not the lane
# The team's meta lane (AW_META_LANE is the metaLane of the team config AW_META_CONFIG names, "meta" when it names none) has no lane
# inbox: it gets `aw-meta inbox` as context and no lane command.
if [ -n "${AW_META_CONFIG:-}" ] && [ -f "$AW_META_CONFIG" ] \
  && [ "$(jq -r '.metaLane // "meta"' "$AW_META_CONFIG" 2> /dev/null)" = "$AW_META_LANE" ]; then
  trace meta-lane
  case "$EVENT" in
    UserPromptSubmit|SessionStart|PostToolUse)
      OUT="$(AW_LOG_LEVEL=none "$META" inbox 2>&1)"; rc=$?
      [ "$rc" = 0 ] || { failopen "inbox-exit-$rc$(code_of "$OUT")"; exit 0; }
      [ -n "$OUT" ] || exit 0
      if [ "$EVENT" = UserPromptSubmit ]; then
        printf '%s\n' "$OUT"
      else
        jq -nc --arg e "$EVENT" --arg m "$OUT" '{hookSpecificOutput: {hookEventName: $e, additionalContext: $m}}' 2> /dev/null
      fi ;;
  esac
  exit 0
fi

# record_reply: the text after the last line that starts with the marker (the rest of that line and every line after it) goes to
# `aw-meta lane-reply --stdin`, once. The marker may carry markdown decoration (> - * # ** __ `) before it and around its colon.
# Sets UNMARKED=1 when the text is non-blank and has no marker line.
UNMARKED=0
record_reply() { # <text>
  local REPLY SUM RO rrc
  UNMARKED=0
  if [ -n "$(printf '%s' "$1" | tr -d '[:space:]')" ] && ! printf '%s\n' "$1" | grep -Eq '^[[:space:]>*#_`-]*REPLY TO META:'; then UNMARKED=1; fi
  REPLY="$(printf '%s\n' "$1" | awk '
    { if (match($0, /^[[:space:]>*#_`-]*REPLY TO META:/)) { found = 1; sub(/^[[:space:]>*#_`-]*REPLY TO META:(\*\*|__|`)?[[:space:]]*/, ""); text = $0; next }
      if (found) text = text "\n" $0 }
    END { if (found) print text }')"
  [ -n "$(printf '%s' "$REPLY" | tr -d '[:space:]')" ] || return 0
  # Record a reply once: a later Stop whose last assistant text is the same (a continuation, a turn with no new text, Cursor's
  # transcript tail, the afterAgentResponse that already recorded it) must not open the meta's reply obligation again. Mail
  # delivered since (above) clears the marker.
  SUM="$(printf '%s' "$REPLY" | cksum | tr ' ' ':')"
  [ "$(cat "$REPLIED" 2> /dev/null)" != "$SUM" ] || return 0
  RO="$(printf '%s\n' "$REPLY" | AW_LOG_LEVEL=none "$META" lane-reply --stdin 2>&1)"; rrc=$?
  if [ "$rrc" = 0 ]; then
    mkdir -p "$(dirname "$REPLIED")" 2> /dev/null && printf '%s\n' "$SUM" > "$REPLIED" 2> /dev/null
  else
    trace lane-reply-failed
    failopen "lane-reply-exit-$rrc$(code_of "$RO")"
  fi
  return 0
}

case "$EVENT" in
  UserPromptSubmit|SessionStart|PostToolUse)
    OUT="$(AW_LOG_LEVEL=none "$META" lane-inbox 2>&1)"; rc=$?
    [ "$rc" = 0 ] || { trace lane-inbox-failed; failopen "lane-inbox-exit-$rc$(code_of "$OUT")"; exit 0; }
    trace ran
    [ -n "$OUT" ] || exit 0
    unlink "$REPLIED" 2> /dev/null; unlink "$OWED" 2> /dev/null   # new mail: the next reply is a new one, even if its text repeats
    if [ "$EVENT" = UserPromptSubmit ]; then
      printf '%s\n' "$OUT"
    else
      jq -nc --arg e "$EVENT" --arg m "$OUT" '{hookSpecificOutput: {hookEventName: $e, additionalContext: $m}}' 2> /dev/null
    fi
    exit 0 ;;
  AfterAgentResponse)
    # Cursor: the reply text comes with this event; it never blocks (Cursor ignores its output), lane-stop-check is the Stop's.
    record_reply "$(printf '%s' "$INPUT" | jq -r '.last_assistant_message // empty' 2>/dev/null)"
    trace ran
    exit 0 ;;
  Stop)
    ACTIVE="$(printf '%s' "$INPUT" | jq -r '.stop_hook_active // false' 2>/dev/null)"
    record_reply "$(printf '%s' "$INPUT" | jq -r '.last_assistant_message // empty' 2>/dev/null)"
    args=(lane-stop-check)
    [ "$ACTIVE" = true ] && args+=(--stop-hook-active)
    OUT="$(AW_LOG_LEVEL=none "$META" "${args[@]}" 2>&1)"; rc=$?
    case "$rc" in
      0) trace ran
         unlink "$OWED" 2> /dev/null   # nothing owed any more: the next owed list is a new one
         # the loop guard's "stop allowed ..." warning: shown to the user (Codex; the Cursor adapter drops it, and
         # `aw-meta health` shows the stop cap either way)
         [ -z "$OUT" ] || jq -nc --arg m "$OUT" '{systemMessage: $m}' 2> /dev/null
         exit 0 ;;
      1) if [ "$UNMARKED" = 1 ]; then trace reply-unmarked; else trace ran; fi
         # A block shows the owed messages, so it is a delivery too: a new owed list makes the same reply text recordable again (the
         # agent answers "on it" to a message that arrived mid-turn); the same list blocked again (the stop cap loop) does not.
         OSUM="$(printf '%s' "$OUT" | cksum | tr ' ' ':')"
         if [ "$(cat "$OWED" 2> /dev/null)" != "$OSUM" ]; then
           unlink "$REPLIED" 2> /dev/null
           mkdir -p "$LANE_DIR" 2> /dev/null && printf '%s\n' "$OSUM" > "$OWED" 2> /dev/null
         fi
         # the lane owes a reply and this answer carries none: say so, in front of the block text (after the checksum, which is the
         # owed list's alone)
         [ "$UNMARKED" = 1 ] && OUT="Your last answer has no line starting REPLY TO META:, so meta did not get it. End the turn with REPLY TO META: <reply>.
$OUT"
         printf '%s\n' "$OUT" >&2; exit 2 ;;
      *) trace lane-stop-check-failed; failopen "lane-stop-check-exit-$rc$(code_of "$OUT")"; exit 0 ;;
    esac ;;
  *) exit 0 ;;
esac
