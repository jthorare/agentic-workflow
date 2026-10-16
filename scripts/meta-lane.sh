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
# refused and leaves lane-sessions/<lane>.ambiguous). Every lane also gets AW_SINDRI_CHILD=1, AW_META_TEAM and
# AW_META_LANE in its environment, exported here: tmux's global environment stays untouched and the launcher's tmux
# commands stay as they were.
#   AW_META_CONFIG       team config (default $AW_STATE_DIR/meta/team.json): repo, prompts, metaLane
#   AW_LAUNCHER_CONFIG   the launcher file (default $AW_STATE_DIR/launcher.json)
#   AW_META_CLAUDE       the claude command (default claude; tests): replaces the claude-code entry's bin
#   AW_META_CLAUDE_ARGS  extra claude-code arguments, inserted after {bin} (default none: setup.sh sets
#                        permissions.defaultMode to bypassPermissions, so every lane runs in the same mode and messages
#                        are never held)
#   AW_META_ID_WAIT      seconds a reported session id may take to appear (default 30; tests)
#   AW_META_ID_GRACE     seconds a lane whose id timed out keeps the start lock for its late session file (default 10; tests)
#   AW_META_NO_SHELL     1: exit when the agent exits instead of dropping to a shell (tests)
set -u
here="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
team="${1:?usage: meta-lane.sh <team> <lane>}"
lane="${2:?usage: meta-lane.sh <team> <lane>}"
for n in "$team" "$lane"; do
  case "$n" in *[!a-z0-9-]*|"") echo "meta-lane: bad name '$n' (lowercase letters, digits, dashes)" >&2; exit 2 ;; esac
done
state_dir="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
config="${AW_META_CONFIG:-$state_dir/meta/team.json}"
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
ids="$state_dir/meta/lane-sessions"
mkdir -p "$ids"
cd "$repo" || exit 1
# <lane>.exited marks a pane whose agent has exited and which fell back to its shell; meta-launch.sh restarts only
# those. (tmux names a pane after this script, not the agent, so the pane's command can't tell.)
exited="$ids/$lane.exited"
rm -f "$exited" "$ids/$lane.failed"

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
          case "$other" in *.exited|*.start|*.tmp|*.ambiguous|*.settled|*.failed|*.snap.*|*.argv.*|"$ids/$lane") continue ;; esac
          [ -f "$other" ] && [ "$(tr -d '[:space:]' < "$other")" = "$f" ] && claimed=1
        done
        [ "$claimed" = 0 ] || continue
        if [ "$f" != "$cid" ]; then n=$((n + 1)); cid="$f"; fi
      done < <(compgen -G "$glob" | grep -Fxv -f "$snapfile")   # paths that existed before this start are not ours (one pass, however many there are)
      if [ "$n" -gt 1 ]; then : > "$ids/$lane.ambiguous"; : > "$ids/$lane.settled"; return 1; fi
      if [ "$n" = 1 ]; then printf '%s\n' "$cid" > "$ids/$lane.tmp" && mv "$ids/$lane.tmp" "$ids/$lane"; : > "$ids/$lane.settled"; return 0; fi
    fi
    sleep 0.2
  done
  return 1
}

# Lanes whose agent reports its own id start one at a time, whoever starts them (aw-meta-launch, a Warp pane that creates
# its session, a reboot, --lanes): the newest-file fallback cannot tell two new session files apart. A lane takes the
# team's start lock (flock on lane-sessions/.reported.flock) before its agent runs; the watcher subshell holds it and
# releases it by exiting, once the id is recorded, refused or timed out. The kernel drops the lock when its holder dies,
# so a crashed start never leaves a stale one. Linux (flock from util-linux).
start_lock="$ids/.reported.flock"
acquire_start_lock() { # opens lock_fd and takes the lock; returns 1 (fd closed) if the holder did not finish in time
  exec {lock_fd}> "$start_lock" || return 1
  if flock -w $(( ${AW_META_ID_WAIT:-30} * 10 )) "$lock_fd"; then return 0; fi
  echo "meta-lane: another lane's start did not finish; starting $lane anyway" >&2
  exec {lock_fd}>&-; return 1
}

id=""; mode=fresh; write_id=0
case "$id_mode" in
  none) ;;
  *)
    [ -f "$ids/$lane" ] && id="$(tr -d '[:space:]' < "$ids/$lane")"
    if [ -n "$id" ] && id_ok "$id" && [ -n "$(field resume)" ] && has_transcript "$id"; then
      mode=resume
    elif [ "$id_mode" = pre-chosen-uuid ]; then
      id="$(cat /proc/sys/kernel/random/uuid 2> /dev/null || uuidgen)"
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
  AW_LOG_LEVEL=none "$meta_bin" role-set --session "$id" --name "$team-$lane" > /dev/null 2>&1 \
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
export AW_SINDRI_CHILD=1 AW_META_TEAM="$team" AW_META_LANE="$lane"
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
  ) > /dev/null 2>&1 &
  [ "$have_lock" = 0 ] || exec {lock_fd}>&-   # the watcher subshell keeps the lock; this shell, and so the agent, must not
fi
"${args[@]}"
[ "${AW_META_NO_SHELL:-0}" = 1 ] && exit 0
: > "$exited"
exec bash -i
