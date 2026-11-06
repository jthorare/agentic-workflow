#!/usr/bin/env bash
# aw:session-id-report — SessionStart hook for Codex and Cursor (installed user-wide by scripts/install-meta.sh, through the
# provider adapters). Reports the session's own id to the host that launched it, so lanes started together each record their
# own id instead of guessing from file timing (launcher spec section 4):
#   AW_META_TEAM + AW_META_LANE  ->  ${AW_META_DIR:-$AW_STATE_DIR/meta}/lane-sessions/<lane>          (scripts/meta-lane.sh waits for it)
#   AW_SINDRI_LAUNCH             ->  $AW_SINDRI_HOME/launches/<launchId>.session-id    (Sindri's launcher)
# In the team's meta lane (AW_META_LANE is the metaLane of the team config AW_META_CONFIG names) it also registers the session as the
# meta role (`aw-meta role-set --session <id> --name <team>-<lane>`), which meta-lane.sh cannot do for an agent that reports its id
# after it starts. Here, not in the launcher's id watcher, so a later session of the same lane (a new id) takes the role too.
# The key is a variable the host set on that one session, so two sessions started together can't swap ids. Outside a lane or a
# Sindri launch it exits 0 at once, writing nothing. The id is written atomically and 0600. Reads the id from `session_id`
# (cursor.sh maps Cursor's conversation_id to it). Prints nothing; any problem fails open.
set -uo pipefail
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen session-id-report "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# A pass-through reads its stdin first, so the host's write never meets a closed pipe.
pass() { cat > /dev/null 2>&1; exit 0; }
[ -n "${AW_JUDGE_CHILD:-}" ] && pass
lane_mode=0; [ -n "${AW_META_TEAM:-}" ] && [ -n "${AW_META_LANE:-}" ] && lane_mode=1
[ "$lane_mode" = 1 ] || [ -n "${AW_SINDRI_LAUNCH:-}" ] || pass

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
command -v jq > /dev/null 2>&1 || { failopen jq-missing; exit 0; }
ID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2>/dev/null)" || { failopen input-unparsable; exit 0; }
[ -n "$ID" ] || { failopen field-missing; exit 0; }
# The generic bound on an id: it becomes file content on a line of its own, never a path or a command.
[[ "$ID" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || { failopen id-invalid; exit 0; }

# A name that becomes a path component: letters, digits, '.', '_' and '-', and not only dots.
safe_name() { [[ "$1" =~ ^[A-Za-z0-9._-]+$ ]] && [[ "$1" =~ [^.] ]]; }
# write_id <dir> <file>: atomically, 0600.
write_id() {
  local tmp
  mkdir -p "$1" 2> /dev/null || return 1
  tmp="$(umask 077 && mktemp "$1/.$2.XXXXXX")" || return 1
  if printf '%s\n' "$ID" > "$tmp" && chmod 600 "$tmp" && mv -f "$tmp" "$1/$2"; then return 0; fi
  unlink "$tmp" 2> /dev/null
  return 1
}

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

STATE="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
# trace <outcome>: lane-sessions/<lane>.hook, "<UTC time> session-id-report <event> <outcome>" (see meta-lane-gate.sh). Never fails the hook.
EVENT=""
trace() {
  local d="${AW_META_DIR:-$STATE/meta}/lane-sessions"
  # a nested agent (codex exec, cursor-agent -p) inherits the lane variables: its not-own-agent never replaces the lane's own trace
  # from this session (a trace file newer than the session id file)
  if [ "$1" = not-own-agent ] && [ "$d/$AW_META_LANE.hook" -nt "$d/$AW_META_LANE" ] 2> /dev/null; then return 0; fi
  local event="$EVENT"; [ -n "$event" ] || event=unknown
  { mkdir -p "$d" && printf '%s session-id-report %s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$event" "$1" > "$d/$AW_META_LANE.hook"; } 2> /dev/null || true
}
EVENT="$(printf '%s' "$INPUT" | jq -r '.hook_event_name // empty' 2>/dev/null)"
# Only the lane's own agent reports its id: not a nested instance (codex exec, cursor-agent -p), and never a Claude lane's.
if [ "$lane_mode" = 1 ] && ! own_agent; then
  if safe_name "$AW_META_LANE"; then trace not-own-agent; fi
  lane_mode=0
fi
if [ "$lane_mode" = 1 ]; then
  safe_name "$AW_META_LANE" || { failopen lane-name-invalid; exit 0; }
  write_id "${AW_META_DIR:-$STATE/meta}/lane-sessions" "$AW_META_LANE" || failopen write-failed
  trace ran
  # a new session starts with no reply marker or owed checksum (meta-lane-gate.sh)
  unlink "${AW_META_DIR:-$STATE/meta}/lane-sessions/$AW_META_LANE.replied" 2> /dev/null; unlink "${AW_META_DIR:-$STATE/meta}/lane-sessions/$AW_META_LANE.owed" 2> /dev/null
  META_LANE=""   # unknown without the team config: no role is registered
  if [ -n "${AW_META_CONFIG:-}" ] && [ -f "$AW_META_CONFIG" ]; then META_LANE="$(jq -r '.metaLane // "meta"' "$AW_META_CONFIG" 2> /dev/null)"; fi
  META="${AW_META_BIN:-$STATE/bin/aw-meta}"
  if [ -n "$META_LANE" ] && [ "$AW_META_LANE" = "$META_LANE" ] && [ -x "$META" ]; then
    AW_LOG_LEVEL=none "$META" role-set --session "$ID" --name "$AW_META_TEAM-$AW_META_LANE" > /dev/null 2>&1; rrc=$?
    [ "$rrc" = 0 ] || failopen "role-set-exit-$rrc"
  fi
fi
if [ -n "${AW_SINDRI_LAUNCH:-}" ]; then
  if safe_name "$AW_SINDRI_LAUNCH"; then
    write_id "${AW_SINDRI_HOME:-$STATE/sindri-app}/launches" "$AW_SINDRI_LAUNCH.session-id" || failopen write-failed
  else
    failopen launch-id-invalid
  fi
fi
exit 0
