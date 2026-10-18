#!/usr/bin/env bash
# meta-lane.sh <team> <lane>: run one lane's agent session inside its tmux session (scripts/meta-launch.sh starts it).
# With no launcher.json the agent is Claude Code. The session is named <team>-<lane> (`claude -n`), so its name in the
# agent list never changes across restarts. It resumes the lane's recorded session id when that session has a transcript;
# otherwise it starts a fresh session with a new id, records the id, and sends the lane's kickoff prompt
# (<prompts>/<lane>.md) when there is one. For the meta lane it also registers the session with aw-meta (role-set), which
# arms the meta hooks for it. Drops to a shell when the agent exits, so the pane survives.
# The lane's agent comes from launcher.json (scripts/launcher-lib.sh; docs/csharp/sindri-launcher.md): the catalog entry
# for meta.lanes.<lane>, else meta.lanesAgent, else claude-code. Its open and resume argv templates, process name and
# transcripts glob replace the Claude-only commands. An agent that reports its own session id (sessionId: reported) is
# started fresh, and a background watcher records the id it reports (the lane-sessions/<lane> file its hook writes, else
# the single new session file the catalog's newestFile glob finds that no other lane has claimed; more than one is
# refused and leaves lane-sessions/<lane>.ambiguous). A non-Claude lane also gets lane-sessions/<lane>.agent (the catalog
# facts aw-meta needs), a kickoff note about ending a turn with REPLY TO META:, and AW_STATE_DIR and AW_META_CONFIG exported
# for its hooks (config/hooks/session-id-report.sh, meta-lane-gate.sh). Every lane also gets AW_SINDRI_CHILD=1, AW_META_TEAM and
# AW_META_LANE in its environment, exported here: tmux's global environment stays untouched and the launcher's tmux
# commands stay as they were.
#   AW_META_CONFIG       team config (default $AW_STATE_DIR/meta/teams/<team>/team.json, or the legacy $AW_STATE_DIR/meta/team.json): repo, prompts, metaLane
#   AW_LAUNCHER_CONFIG   the launcher file (default $AW_STATE_DIR/launcher.json)
#   AW_META_CLAUDE       the claude command (default claude; tests): replaces the claude-code entry's bin
#   AW_META_CLAUDE_ARGS  extra claude-code arguments, inserted after {bin} (default none: setup.sh sets
#                        permissions.defaultMode to bypassPermissions, so every lane runs in the same mode and messages
#                        are never held)
#   AW_META_ID_WAIT      seconds a reported session id may take to appear (default 30; tests)
#   AW_META_ID_GRACE     seconds a lane whose id timed out keeps the start lock for its late session file (default 10; tests)
#   AW_META_NO_SHELL     1: exit when the agent exits instead of dropping to a shell (tests)
set -u
self="$0"   # resolve symlinks without readlink -f (older macOS has none)
while [ -L "$self" ]; do
  d="$(cd "$(dirname "$self")" && pwd)"; t="$(readlink "$self")"
  case "$t" in /*) self="$t" ;; *) self="$d/$t" ;; esac
done
here="$(cd "$(dirname "$self")" && pwd)"
team="${1:?usage: meta-lane.sh <team> <lane>}"
lane="${2:?usage: meta-lane.sh <team> <lane>}"
for n in "$team" "$lane"; do
  case "$n" in *[!a-z0-9-]*|"") echo "meta-lane: bad name '$n' (lowercase letters, digits, dashes)" >&2; exit 2 ;; esac
done
state_dir="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
# shellcheck source=meta-team-lib.sh
. "$here/meta-team-lib.sh"
meta_resolve "$state_dir" "$team" || exit 2
config="$(meta_pick_config "$team")"
cfg() { [ -f "$config" ] && jq -r "$1 // empty" "$config" 2> /dev/null; }
repo="$(cfg .repo)"; repo="${repo:-$PWD}"
prompts="$(cfg .prompts)"
meta_lane="$(cfg .metaLane)"; meta_lane="${meta_lane:-meta}"
read -r -a claude_args <<< "${AW_META_CLAUDE_ARGS:-}"
meta_bin="${AW_META_BIN:-$state_dir/bin/aw-meta}"
# shellcheck source=launcher-lib.sh
. "$here/launcher-lib.sh"
launcher_load "$state_dir" || exit 1
agent="$(launcher_lane_agent "$lane" "$meta_lane")"
launcher_agent_ok "$agent" || exit 1
field() { launcher_agent_field "$agent" "$1"; }
ids="$META_DIR/lane-sessions"
lane_pid="$$"   # the id watcher polls it: it ends with this script
mkdir -p "$ids"
cd "$repo" || exit 1
# <lane>.exited marks a pane whose agent has exited and which fell back to its shell; meta-launch.sh restarts only
# those. (tmux names a pane after this script, not the agent, so the pane's command can't tell.)
exited="$ids/$lane.exited"
rm -f "$exited" "$ids/$lane.failed" "$ids/$lane.replied" "$ids/$lane.owed"   # a (re)start begins with no reply marker (config/hooks/meta-lane-gate.sh)
# <lane>.agent tells aw-meta what runs in a non-Claude lane (agent, processName, hooks, transcripts: the catalog entry's facts;
# hooks is the entry's reportedId.hook, the CLI having a hook system the lane hooks can use). A Claude Code lane has none, and a
# stale one from an earlier agent is removed, so the lane map reads the lane as it now runs.
if [ "$agent" = claude-code ]; then
  unlink "$ids/$lane.agent" 2> /dev/null
else
  jq -c --arg id "$agent" '.[$id] | {agent: $id, processName: (.processName // $id), hooks: ((.reportedId.hook // false) == true), transcripts: (.transcripts // null)}' \
    <<< "$LAUNCHER_CAT" > "$ids/$lane.agent.tmp" && mv "$ids/$lane.agent.tmp" "$ids/$lane.agent"
fi

id_mode="$(field sessionId)"; id_mode="${id_mode:-none}"
id_pattern="$(field sessionIdPattern)"
transcripts="$(field transcripts)"
# fail <code> <message>: leave the reason where scripts/meta-launch.sh looks (<lane>.failed, then <lane>.settled) for a
# lane whose agent reports its own id, so the launcher shows it at once instead of waiting out AW_META_ID_WAIT.
fail() {
  printf '%s\n' "$2" >&2
  if [ "$id_mode" = reported ]; then printf '%s\n' "$2" > "$ids/$lane.failed"; : > "$ids/$lane.settled"; fi
  exit "$1"
}
id_ok() { [ -z "$id_pattern" ] || [[ "$1" =~ $id_pattern ]]; }
has_transcript() {
  [ -n "$transcripts" ] || return 1
  local g="${transcripts//\{sessionId\}/$1}"
  compgen -G "${g/#\~/$HOME}" > /dev/null
}

# watch_reported_id: the agent picks its own session id. Wait up to AW_META_ID_WAIT seconds for it: the file the agent's
# hook writes to $ids/<lane>, else the one session file (the catalog's newestFile glob) that did not exist when this lane
# started (the $snapshot of paths, taken before the agent ran) and that no other lane has recorded; its capture group is
# the id. Two candidates are refused. Success and a refusal both leave <lane>.settled for scripts/meta-launch.sh.
# A timeout is not final: the caller keeps the start lock for a grace period (AW_META_ID_GRACE, default 10 s) and watches
# again, so a slow agent's late file is recorded as this lane's before the next lane can start and take it.
watch_reported_id() { # [seconds]: the wait (default AW_META_ID_WAIT); a second call with the grace period follows a timeout
  local deadline=$((SECONDS + ${1:-${AW_META_ID_WAIT:-30}})) glob capture p f cid other claimed n
  glob="$(jq -r --arg id "$agent" '.[$id].reportedId.newestFile // empty' <<< "$LAUNCHER_CAT")"
  capture="$(jq -r --arg id "$agent" '.[$id].reportedId.capture // empty' <<< "$LAUNCHER_CAT")"
  glob="${glob/#\~/$HOME}"
  while [ "$SECONDS" -lt "$deadline" ]; do
    kill -0 "$lane_pid" 2> /dev/null || return 1   # the lane script is gone (its pane closed): nothing to wait for, and the start lock is released
    if [ -s "$ids/$lane" ]; then : > "$ids/$lane.settled"; return 0; fi   # the hook wrote it (any older file was removed at the start)
    if [ -n "$glob" ] && [ -n "$capture" ]; then
      n=0; cid=""
      while IFS= read -r p; do
        [ -n "$p" ] || continue
        [[ "$p" =~ $capture ]] || continue
        f="${BASH_REMATCH[1]}"
        [ -n "$f" ] && id_ok "$f" || continue
        claimed=0
        for other in "$ids"/*; do
          case "$other" in *.exited|*.start|*.tmp|*.agent|*.ambiguous|*.settled|*.failed|*.snap.*|*.argv.*|"$ids/$lane") continue ;; esac
          [ -f "$other" ] && [ "$(tr -d '[:space:]' < "$other")" = "$f" ] && claimed=1
        done
        [ "$claimed" = 0 ] || continue
        if [ "$f" != "$cid" ]; then n=$((n + 1)); cid="$f"; fi
      done < <(compgen -G "$glob" | awk -v f="$snapfile" 'BEGIN { while ((getline l < f) > 0) seen[l] = 1 } !($0 in seen)')   # paths that existed before this start are not ours (one pass, however many there are; BSD grep -f is quadratic)
      if [ "$n" -gt 1 ]; then : > "$ids/$lane.ambiguous"; : > "$ids/$lane.settled"; return 1; fi
      if [ "$n" = 1 ]; then printf '%s\n' "$cid" > "$ids/$lane.tmp" && mv "$ids/$lane.tmp" "$ids/$lane"; : > "$ids/$lane.settled"; return 0; fi
    fi
    sleep 0.2
  done
  return 1
}

# Lanes whose agent reports its own id start one at a time, whoever starts them (aw-meta-launch, a Warp pane that creates
# its session, a reboot, --lanes): the newest-file fallback cannot tell two new session files apart. A lane takes the
# team's start lock before its agent runs; the watcher subshell releases it once the id is recorded, refused or timed out
# (or the lane script ends first). The lock is a symlink, lane-sessions/.reported.lock, whose target is the owner's pid:
# `ln -s` is atomic and records the owner in one step, on macOS (no flock there) and Linux alike. A lock whose owner pid
# is dead is taken over, so a crashed start never leaves a stale one.
# shellcheck source=meta-start-lock.sh
start_lock="$ids/.reported.lock"
. "$here/meta-start-lock.sh"

id=""; mode=fresh; write_id=0
case "$id_mode" in
  none) ;;
  *)
    [ -f "$ids/$lane" ] && id="$(tr -d '[:space:]' < "$ids/$lane")"
    if [ -n "$id" ] && id_ok "$id" && [ -n "$(field resume)" ] && has_transcript "$id"; then
      mode=resume
    elif [ "$id_mode" = pre-chosen-uuid ]; then
      id="$(cat /proc/sys/kernel/random/uuid 2> /dev/null || uuidgen | tr '[:upper:]' '[:lower:]')"   # macOS uuidgen prints uppercase; a transcript is named by the lowercase id
    else
      id=""; rm -f "$ids/$lane" "$ids/$lane.ambiguous" "$ids/$lane.settled"
    fi
    if [ -n "$id" ]; then write_id=1; fi ;;   # recorded below, once the argv is known to be allowed
esac
name="$team-$lane"
bin="$(field bin)"
seed=""; LAUNCHER_KEEP_EMPTY_SEED=0
if [ "$mode" = fresh ] && [ -n "$prompts" ] && [ -f "$prompts/$lane.md" ]; then
  seed="$(cat "$prompts/$lane.md")"; LAUNCHER_KEEP_EMPTY_SEED=1   # an empty prompt file still passes "", as before
fi
# A non-Claude lane with hooks answers the meta by ending a turn with a REPLY TO META: line, which its stop hook records (a
# Claude lane uses SendMessage). A fresh session's kickoff prompt says so; a lane with no prompt file gets none (the message
# itself, shown by lane-inbox, ends with the same instruction).
if [ "$mode" = fresh ] && [ -n "$seed" ] && [ "$agent" != claude-code ] && [ "$lane" != "$meta_lane" ] \
  && [ "$(jq -r --arg id "$agent" '(.[$id].reportedId.hook // false) == true' <<< "$LAUNCHER_CAT")" = true ]; then
  seed="$seed

When you answer a message from the meta (aw-meta lane-inbox shows it), end your turn with a line starting REPLY TO META: followed by your reply."
fi
if [ "$mode" = resume ]; then tpl="$(field resume)"; else tpl="$(field open)"; fi
# Expand once into a file: its status (a refused element, spec section 4) is checked before anything else happens, so a
# refused lane starts no agent and records no id.
argv_file="$ids/$lane.argv.$$"
if ! launcher_expand "$tpl" "$repo" "$name" "$name" "$seed" "$id" "$bin" > "$argv_file" 2> "$argv_file.err"; then
  unlink "$argv_file" 2> /dev/null; refusal="$(sed "s/^jq: error (at <[a-z]*>:[0-9]*): //" "$argv_file.err")"; unlink "$argv_file.err"
  fail 1 "${refusal:+$refusal
}meta-lane: lane $lane refused: the agent was not started"
fi
unlink "$argv_file.err" 2> /dev/null
args=()
while IFS= read -r -d '' a; do args+=("$a"); done < "$argv_file"
unlink "$argv_file"
[ "${#args[@]}" -gt 0 ] || fail 1 "meta-lane: agent '$agent' has an empty argv"
if [ "$write_id" = 1 ]; then printf '%s\n' "$id" > "$ids/$lane.tmp" && mv "$ids/$lane.tmp" "$ids/$lane"; fi
if [ "$id_mode" = reported ] && [ "$mode" = resume ]; then : > "$ids/$lane.settled"; fi
if [ "$lane" = "$meta_lane" ] && [ -x "$meta_bin" ] && [ -n "$id" ]; then
  AW_META_TEAM="$team" AW_LOG_LEVEL=none "$meta_bin" role-set --session "$id" --name "$team-$lane" > /dev/null 2>&1 \
    || echo "meta-lane: aw-meta role-set failed; run it by hand in the meta session" >&2
fi
# AW_META_CLAUDE_ARGS go right after {bin}, before the session flags and the prompt, for the claude-code agent only. The
# built-in claude-code resume template has no -n (Sindri names its sessions another way); a Meta lane's name must not
# change across restarts, so it is appended there.
if [ "$agent" = claude-code ]; then
  [ "$mode" = resume ] && [[ "$tpl" != *'{name}'* ]] && args+=(-n "$name")
  args=("${args[0]}" ${claude_args[@]+"${claude_args[@]}"} "${args[@]:1}")
fi
# A launcher run from inside a Claude session (the meta recreating a dead lane) must not hand the lane that session's
# identity: its id, pid, messaging socket and token. Only these are cleared; user settings such as model or
# provider variables pass through.
unset CLAUDECODE CLAUDE_PID CLAUDE_CODE_SESSION_ID CLAUDE_CODE_CHILD_SESSION CLAUDE_CODE_MESSAGING_SOCKET \
  CLAUDE_CODE_MESSAGING_TOKEN CLAUDE_CODE_BRIDGE_SESSION_ID CLAUDE_CODE_ENTRYPOINT CLAUDE_CODE_SESSION_ATTENDED \
  CLAUDE_CODE_EXECPATH
export AW_SINDRI_CHILD=1 AW_META_TEAM="$team" AW_META_LANE="$lane" AW_META_DIR="$META_DIR"
# A non-Claude lane's hooks and its lane-* commands run in the agent's process: they find the state directory and the team
# config from here, not from a Claude-style settings file. A Claude lane's environment stays as it was.
# AW_META_LANE_PID (this script, the agent's parent), AW_META_LANE_AGENT (the catalog id) and AW_META_LANE_PROC (the agent's process
# name) let a Codex or Cursor lane hook tell the lane's own agent from one the agent started itself (codex exec, cursor-agent -p), which
# inherits the lane variables; they are exported in every lane, a Claude lane included, so a codex exec inside it is not mistaken for it.
# Nothing on the Claude path reads them.
export AW_META_LANE_PID="$$" AW_META_LANE_AGENT="$agent" AW_META_LANE_PROC="$(field processName)"
if [ "$agent" != claude-code ]; then
  export AW_STATE_DIR="$state_dir"
  [ ! -f "$config" ] || export AW_META_CONFIG="$config"
fi
if [ "$id_mode" = reported ] && [ "$mode" = fresh ]; then
  have_lock=0; acquire_start_lock && have_lock=1
  # The session files that exist now are not this lane's: the watcher counts only paths that appear after this point.
  snap_glob="$(jq -r --arg id "$agent" '.[$id].reportedId.newestFile // empty' <<< "$LAUNCHER_CAT")"
  snapfile="$ids/$lane.snap.$$"; : > "$snapfile"
  [ -z "$snap_glob" ] || compgen -G "${snap_glob/#\~/$HOME}" > "$snapfile" 2> /dev/null
  (
    # A timed-out lane keeps the lock through the grace period unless its late file turns up (or it was refused).
    watch_reported_id || [ -e "$ids/$lane.settled" ] || watch_reported_id "${AW_META_ID_GRACE:-10}"
    unlink "$snapfile" 2> /dev/null
    [ "$have_lock" = 0 ] || release_start_lock
  ) > /dev/null 2>&1 &
  watcher=$!
fi
watcher="${watcher:-}"
recorder=""
# Ends the id watcher and the pane-name recorder, and removes the watcher's snapshot (it unlinks it itself only when it finishes).
stop_watcher() {
  [ -z "$watcher" ] || kill "$watcher" 2> /dev/null
  [ "${have_lock:-0}" = 0 ] || release_start_lock
  [ -z "$recorder" ] || kill "$recorder" 2> /dev/null
  [ -z "${snapfile:-}" ] || unlink "$snapfile" 2> /dev/null
  watcher=""; recorder=""
}
if [ "$agent" = claude-code ]; then
  "${args[@]}"
else
  # tmux names a pane after the foreground process group's leader. A script's children share its group, so the pane would read
  # "bash" and aw-meta's wake guard ("the pane runs the agent's processName") would call every non-Claude lane down. Job control
  # gives the agent its own group and the terminal, so the pane reads "codex" or "agent". Claude lanes run as before.
  # The pane is named after the process tmux finds leading the foreground group, which is not always the catalog's processName (an
  # npm install of Codex is a node wrapper that spawns the native codex: the pane reads "node"). Once the agent leads the group, record
  # the name the pane really shows in the .agent record, so aw-meta's wake guard compares against that.
  own_pg="$(ps -o pgid= -p "$lane_pid" 2> /dev/null | tr -d ' ')"
  (
    for _ in $(seq 50); do
      sleep 0.2
      kill -0 "$lane_pid" 2> /dev/null || exit 0
      fg="$(ps -o tpgid= -p "$lane_pid" 2> /dev/null | tr -d ' ')"
      [ -n "$fg" ] && [ "$fg" -gt 0 ] 2> /dev/null && [ "$fg" != "$own_pg" ] || continue
      shown="$(ps -o args= -p "$fg" 2> /dev/null)"; shown="${shown%% *}"; shown="${shown##*/}"
      case "$shown" in ''|bash|sh|dash|zsh|ksh|fish|env) continue ;; esac   # a shell (or env) stands for "the agent is down" (the pane's fallback): never record it
      if [ "$shown" != "$(jq -r .processName "$ids/$lane.agent" 2> /dev/null)" ]; then
        tmpf="$ids/$lane.agent.tmp.$$.$RANDOM"
        jq -c --arg n "$shown" '.processName = $n' "$ids/$lane.agent" > "$tmpf" && mv "$tmpf" "$ids/$lane.agent"
      fi
      exit 0
    done
  ) > /dev/null 2>&1 &
  recorder=$!
  set -m
  "${args[@]}"
  set +m
fi
stop_watcher
[ "${AW_META_NO_SHELL:-0}" = 1 ] && exit 0
: > "$exited"
exec bash -i
