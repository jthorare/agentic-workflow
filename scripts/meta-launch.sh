#!/usr/bin/env bash
# meta-launch.sh — stand up a meta-orchestrated team in one Warp window: a pane per lane (the meta lane first, then
# the team config's lanes), each attached to tmux session <team>-<lane> running that lane's Claude session
# (scripts/meta-lane.sh: resumed by id when one exists, else started from its kickoff prompt). Missing tmux sessions
# are recreated first (tmux may have died with the box), and a lane whose claude exited is restarted in place, so
# the panes only attach. Writes the Warp launch configuration and opens it. One command restores the whole team.
# The team runs on its own tmux socket, named after the team (`tmux -L <team>`): every tmux call here, the Warp layout's
# `tmux new -A` lines, the printed attach lines and the exec-template viewer's attach argv. Attach by hand with
# `tmux -L <team> attach -t =<team>-<lane>`. A team started before this change stays on tmux's default socket until it is relaunched.
# launcher.json (scripts/launcher-lib.sh; docs/csharp/sindri-launcher.md) changes two things, and with no file nothing
# changes: the viewer (warp, the layout above; exec-template, one window per lane running the file's argv; print, one
# `tmux attach` line per lane) and each lane's agent (meta.lanesAgent, meta.lanes.<lane>; Claude Code by default).
# Before it creates anything it looks (read-only: has-session) for `<team>-<lane>` sessions of the lanes it would start on tmux's
# DEFAULT socket, where a team started before the switch still runs, and refuses (exit 1) if any exist: starting them again
# would run a second claude on the same transcripts. The owner stops the old team, then relaunches; --allow-legacy (owner only) skips the check.
#   meta-launch.sh [--team <name>] [--lanes a,b,...] [--no-open] [--dry-run] [--allow-legacy]
#   meta-launch.sh --init-launcher [--viewer warp|print|<terminal id>] [--force] [--stdout]   write launcher.json for this box
#   AW_META_CONFIG      team config (default $AW_STATE_DIR/meta/teams/<team>/team.json, or the legacy $AW_STATE_DIR/meta/team.json; `aw-meta team-init` writes it)
#   AW_META_TEAM        the team to launch (same as --team); with one team on disk, or a legacy config, none is needed (scripts/meta-team-lib.sh)
#   AW_LAUNCHER_CONFIG  the launcher file (default $AW_STATE_DIR/launcher.json); passed on to each lane's session
#   AW_WARP_LAUNCH_DIR  default ${XDG_DATA_HOME:-~/.local/share}/warp-terminal/launch_configurations (Linux),
#                       ~/.warp/launch_configurations (macOS)
#   AW_META_ALLOW_LEGACY=1  same as --allow-legacy
#   AW_META_LEGACY_TMUX_SOCKET  the `-L` name of the legacy server to check instead of tmux's default (tests point it at a private socket)
#   AW_META_ID_WAIT     seconds a lane whose agent reports its own session id has to report it (default 30; tests)
#   AW_META_BIN         the installed aw-meta (default $AW_STATE_DIR/bin/aw-meta); a launch warns on stderr (one line, never fatal) when
#                       its commit stamp (`aw-meta version`) is older than the Meta sources of this checkout (scripts/meta-version-lib.sh)
#   AW_META_NO_STALE_CHECK=1  skip that check
set -euo pipefail
self="$0"   # through the ~/.local/bin/aw-meta-launch symlink too; no readlink -f (older macOS has none)
while [ -L "$self" ]; do
  d="$(cd "$(dirname "$self")" && pwd)"; t="$(readlink "$self")"
  case "$t" in /*) self="$t" ;; *) self="$d/$t" ;; esac
done
here="$(cd "$(dirname "$self")" && pwd)"
only=""; team_sel="${AW_META_TEAM:-}"; open=1; dry=0; allow_legacy="${AW_META_ALLOW_LEGACY:-0}"; init=0; init_viewer=""; init_force=0; init_stdout=0
while [ $# -gt 0 ]; do
  case "$1" in
    --team) team_sel="${2:?--team needs a name}"; shift 2 ;;
    --lanes) only="${2:?--lanes needs a list}"; shift 2 ;;
    --no-open) open=0; shift ;;
    --dry-run) dry=1; shift ;;
    --allow-legacy) allow_legacy=1; shift ;;
    --init-launcher) init=1; shift ;;
    --viewer) init_viewer="${2:?--viewer needs a name}"; shift 2 ;;
    --force) init_force=1; shift ;;
    --stdout) init_stdout=1; shift ;;
    *) echo "usage: meta-launch.sh [--team name] [--lanes a,b,...] [--no-open] [--dry-run] [--allow-legacy] | --init-launcher [--viewer name] [--force] [--stdout]" >&2; exit 2 ;;
  esac
done
if [ "$init" = 0 ] && { [ -n "$init_viewer" ] || [ "$init_force" = 1 ] || [ "$init_stdout" = 1 ]; }; then
  echo "meta-launch: --viewer, --force and --stdout go with --init-launcher" >&2; exit 2
fi
command -v jq > /dev/null || { echo "meta-launch: jq is not installed" >&2; exit 1; }
state_dir="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
# shellcheck source=launcher-lib.sh
. "$here/launcher-lib.sh"
# shellcheck source=meta-team-lib.sh
. "$here/meta-team-lib.sh"
# shellcheck source=meta-version-lib.sh
. "$here/meta-version-lib.sh"

# --init-launcher: detect this box's terminal and write launcher.json (spec section 4, "The generator"): no GUI session
# is print; else Warp when present; else $TERM_PROGRAM when it names a terminal in config/launcher/terminals.json; else
# the first of those terminals on PATH; else print. The templates are the file's, not copies. Exit 0 ok, 1 refused
# (the file exists without --force, or the result fails the reader), 2 usage.
init_launcher() {
  local target="${AW_LAUNCHER_CONFIG:-$state_dir/launcher.json}" kind="" why="" row="" t tp
  [ -f "$LAUNCHER_TERMINALS" ] || { echo "meta-launch: $LAUNCHER_TERMINALS is missing" >&2; return 1; }
  term_by_id() { jq -c --arg k "$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')" '.terminals[] | select((.id | ascii_downcase) == $k or (.name | ascii_downcase) == $k)' "$LAUNCHER_TERMINALS"; }
  if [ -n "$init_viewer" ]; then
    case "$init_viewer" in
      print|warp) kind="$init_viewer"; why="from --viewer" ;;
      *) row="$(term_by_id "$init_viewer")"
         [ -n "$row" ] || { echo "meta-launch: unknown --viewer '$init_viewer' (warp, print, or: $(jq -r '[.terminals[].id] | join(", ")' "$LAUNCHER_TERMINALS"))" >&2; return 2; }
         kind=exec-template; why="from --viewer" ;;
    esac
  elif [ "$(uname -s)" != Darwin ] && [ -z "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then kind=print; why="no GUI session"
  elif command -v warp-terminal > /dev/null 2>&1 || [ "${TERM_PROGRAM:-}" = WarpTerminal ] || { [ "$(uname -s)" = Darwin ] && [ -d /Applications/Warp.app ]; }; then
    kind=warp; why="Warp found"
  else
    tp="${TERM_PROGRAM:-}"
    if [ -n "$tp" ]; then
      row="$(jq -c --arg k "$(printf '%s' "$tp" | tr '[:upper:]' '[:lower:]')" '.terminals[] | select(.termProgram | map(ascii_downcase) | index($k))' "$LAUNCHER_TERMINALS" | head -1)"
      [ -n "$row" ] && { kind=exec-template; why="from \$TERM_PROGRAM"; }
    fi
    if [ -z "$kind" ]; then
      while IFS= read -r t; do
        if command -v "$(jq -r .bin <<< "$t")" > /dev/null 2>&1; then row="$t"; kind=exec-template; why="first on PATH"; break; fi
      done < <(jq -c '.terminals[]' "$LAUNCHER_TERMINALS")
    fi
    [ -n "$kind" ] || { kind=print; why="no supported terminal found"; }
  fi
  local doc label
  if [ "$kind" = exec-template ]; then
    doc="$(jq -n --argjson t "$row" '{schema: 1, viewer: {kind: "exec-template", name: $t.name, argv: $t.argv}}')"; label="$(jq -r .name <<< "$row")"
  else doc="$(jq -n --arg k "$kind" '{schema: 1, viewer: {kind: $k}}')"; label="$kind"; fi
  if [ "$init_stdout" = 1 ]; then printf '%s\n' "$doc"; return 0; fi
  if [ -e "$target" ] && [ "$init_force" = 0 ]; then
    echo "meta-launch: $target exists: add --force to replace it, or --stdout to print the result" >&2; return 1
  fi
  mkdir -p "$(dirname "$target")"
  local tmp="$target.tmp.$$" msg
  ( umask 077; printf '%s\n' "$doc" > "$tmp" )
  if ! msg="$(launcher_validate "$tmp")"; then unlink "$tmp"; echo "meta-launch: the generated file fails the reader: $msg" >&2; return 1; fi
  mv "$tmp" "$target"
  echo "Wrote $target: $label ($why). The next aw-meta-launch uses it."
}
if [ "$init" = 1 ]; then init_launcher; exit $?; fi

command -v tmux > /dev/null || { echo "meta-launch: tmux is not installed" >&2; exit 1; }
# A binary older than the Meta sources this launcher runs from is the usual reason a fix "didn't take" after a reinstall that was skipped.
[ "${AW_META_NO_STALE_CHECK:-0}" = 1 ] || meta_stale_line "${AW_META_BIN:-$state_dir/bin/aw-meta}" "$here/.." >&2
if [ -n "${AW_META_CONFIG:-}" ] && [ -z "$team_sel" ]; then   # an explicit config names its team: the state directory follows it
  team_sel="$(jq -r 'if (.team | type) == "string" then .team else empty end' "$AW_META_CONFIG" 2> /dev/null || true)"
fi
meta_resolve "$state_dir" "$team_sel" || exit 2
config="$(meta_pick_config "$team_sel")"   # a lane's AW_META_CONFIG is ignored when --team names another team
meta_dir="$META_DIR"
[ -f "$config" ] || { echo "meta-launch: no team config at $config (run: aw-meta team-init --team <t> --repo <dir> --lanes a,b)" >&2; exit 1; }
team="$(jq -r '.team // empty' "$config")"
if [ -n "$team_sel" ] && [ "$team" != "$team_sel" ]; then
  echo "meta-launch: $config names team '$team', not the selected '$team_sel' (its directory and the team field must agree)" >&2; exit 2
fi
repo="$(jq -r '.repo // empty' "$config")"
all="$(jq -r '[(.metaLane // "meta")] + (.lanes // []) | reduce .[] as $l ([]; if index([$l]) then . else . + [$l] end) | join(",")' "$config")"
tmux_t=(tmux -L "$team")            # every tmux call of this launcher: the team's own socket
IFS=, read -r -a A <<< "$all"        # every lane: the Warp layout always has a pane for each
IFS=, read -r -a L <<< "${only:-$all}"   # the lanes whose tmux sessions this run checks (--lanes)
for n in "$team" "${A[@]}" "${L[@]}"; do
  case "$n" in *[!a-z0-9-]*|"") echo "meta-launch: bad team or lane name '$n'" >&2; exit 2 ;; esac
done
case "$repo" in /*) [ -d "$repo" ] || { echo "meta-launch: repo $repo not found" >&2; exit 1; } ;;
  *) echo "meta-launch: repo must be an absolute path (Warp requires it): '$repo'" >&2; exit 1 ;; esac
meta_lane="$(jq -r '.metaLane // "meta"' "$config")"
launcher_load "$state_dir" || exit 1
if [ -n "${AW_LAUNCHER_CONFIG:-}" ]; then   # lanes read the same file: pass it on, as an absolute path (their cwd is the repo)
  AW_LAUNCHER_CONFIG="$(cd "$(dirname "$launcher_file")" && pwd)/$(basename "$launcher_file")"; export AW_LAUNCHER_CONFIG
fi
viewer="$(launcher_viewer_kind)"
lc_env=(); lc_str=""
if [ -n "${AW_LAUNCHER_CONFIG:-}" ]; then lc_env=(-e "AW_LAUNCHER_CONFIG=$AW_LAUNCHER_CONFIG"); lc_str=" -e AW_LAUNCHER_CONFIG=$AW_LAUNCHER_CONFIG"; fi
agent_of() { launcher_lane_agent "$1" "$meta_lane"; }
proc_of() { # lane: the process name a pane runs while the lane's agent is up
  local a pn; a="$(agent_of "$1")"; pn="$(launcher_agent_field "$a" processName)"
  [ -n "$pn" ] || pn="$(basename "$(launcher_agent_field "$a" bin)")"
  printf '%s' "$pn"
}
for l in "${A[@]}" "${L[@]}"; do launcher_agent_ok "$(agent_of "$l")" || exit 1; done
if [ "$viewer" = exec-template ] && [ "$(jq -r '.viewer.argv | type' <<< "$LAUNCHER_DOC")" != array ]; then
  echo "meta-launch: SND-LAUNCH-011: viewer.argv is required for an exec-template viewer (in ${launcher_file:-launcher.json})" >&2; exit 1
fi
# viewer_argv <lane> <name>: the exec-template argv for one lane, NUL-separated (the attach command is {cmd...})
viewer_argv() {
  launcher_expand "$(jq -c .viewer.argv <<< "$LAUNCHER_DOC")" "$repo" "$team-$1" "$team-$1" "" "" "" "${tmux_t[@]}" attach -t "=$team-$1"
}
if [ "$viewer" = exec-template ]; then   # a refusal (an expansion starting with "-") stops the launch before any session exists
  for l in "${L[@]}"; do viewer_argv "$l" > /dev/null || exit 1; done
fi
for l in "${L[@]}"; do
  a="$(agent_of "$l")"
  [ "$a" = claude-code ] && continue
  # A CLI with a hook system (the catalog's reportedId.hook) takes part through the lane hooks scripts/install-meta.sh installs;
  # one without can only be launched.
  if [ "$(jq -r --arg id "$a" '(.[$id].reportedId.hook // false) == true' <<< "$LAUNCHER_CAT")" = true ]; then
    echo "lane $l: $a, takes part in the obligations through its hooks (scripts/install-meta.sh installs them)"
  else
    echo "lane $l: $a, launch-only: no obligations"
  fi
done
if [ "$(uname -s)" = Darwin ]; then default_dir="$HOME/.warp/launch_configurations"
else default_dir="${XDG_DATA_HOME:-$HOME/.local/share}/warp-terminal/launch_configurations"; fi
dir="${AW_WARP_LAUNCH_DIR:-$default_dir}"
yaml="$dir/$team-team.yaml"

# claude_in_pane <session> <process name>: the lane's agent process (claude, or the catalog's processName) runs under the
# pane (started by meta-lane.sh, or by hand from the fallback shell after an exit, which leaves the .exited marker behind).
claude_in_pane() {
  local root; root="$("${tmux_t[@]}" display-message -p -t "=$1:" '#{pane_pid}' 2> /dev/null)" || return 1
  [ -n "$root" ] || return 1
  ps -A -o pid= -o ppid= -o comm= 2> /dev/null | awk -v root="$root" -v pn="$2" '
    { pid[NR] = $1; ppid[NR] = $2; comm[NR] = $3 }
    END { tree[root] = 1; grew = 1
      while (grew) { grew = 0; for (i = 1; i <= NR; i++) if ((ppid[i] in tree) && !(pid[i] in tree)) { tree[pid[i]] = 1; grew = 1 } }
      for (i = 1; i <= NR; i++) if ((pid[i] in tree) && comm[i] ~ ("(^|/)" pn "$")) exit 0
      exit 1 }'
}

# A lane whose agent reports its own session id (sessionId: reported) is waited for: meta-lane.sh leaves
# lane-sessions/<lane>.settled once the id is recorded (or refused as ambiguous), and the next lane is started after that.
# A lane that exits before it can watch (a refused prompt, an empty argv) leaves <lane>.failed with the reason and .settled,
# and the launcher prints that reason at once.
# meta-lane.sh serialises such lanes itself too (a start lock), so a Warp pane that creates its session is safe as well.
# AW_META_ID_WAIT seconds at most.
mark_start() { # lane: clear the previous start's markers
  mkdir -p "$meta_dir/lane-sessions"
  rm -f "$meta_dir/lane-sessions/$1.settled" "$meta_dir/lane-sessions/$1.ambiguous" "$meta_dir/lane-sessions/$1.failed"
}
wait_reported() { # lane
  local d="$meta_dir/lane-sessions" n
  n=$(( ${AW_META_ID_WAIT:-30} * 10 ))
  if [ "$(launcher_agent_field "$(agent_of "$1")" sessionId)" = reported ]; then
    while [ "$n" -gt 0 ]; do
      [ -e "$d/$1.settled" ] && break
      sleep 0.1; n=$((n - 1))
    done
    if [ -s "$d/$1.failed" ]; then echo "meta-launch: lane $1 failed to start: $(cat "$d/$1.failed")" >&2
    elif [ "$n" -le 0 ]; then echo "meta-launch: lane $1 has not reported its session id after ${AW_META_ID_WAIT:-30}s" >&2
    elif [ -e "$d/$1.ambiguous" ]; then echo "meta-launch: lane $1: ambiguous session id (more than one new session file); no id recorded" >&2; fi
  fi
}

# 0. The switch to the team's own socket: sessions of this run's lanes still on tmux's default socket mean the old team is
# running. Starting them again here would run a second agent per transcript, so refuse until the owner stops the old team.
# Read-only (has-session) and with TMUX unset, so it never follows a client's server. A team named "default" has no legacy socket.
if [ "$allow_legacy" != 1 ] && [ "$team" != default ]; then
  if [ -n "${AW_META_LEGACY_TMUX_SOCKET:-}" ]; then legacy_t=(env -u TMUX tmux -L "$AW_META_LEGACY_TMUX_SOCKET"); legacy_s="tmux -L $AW_META_LEGACY_TMUX_SOCKET"; else legacy_t=(env -u TMUX tmux); legacy_s=tmux; fi
  [ "$dry" = 1 ] && echo "[dry-run] legacy-socket check: $legacy_s has-session -t =$team-<lane> for each lane (refuses if any exists)"
  legacy_found=()
  # The Warp layout has a pane (tmux new -A) for every lane, so with that viewer every lane is checked; print and
  # exec-template only attach to the lanes of this run.
  if [ "$viewer" = exec-template ] || [ "$viewer" = print ]; then legacy_lanes=("${L[@]}"); else legacy_lanes=("${A[@]}"); fi
  for l in "${legacy_lanes[@]}"; do "${legacy_t[@]}" has-session -t "=$team-$l" 2> /dev/null && legacy_found+=("$team-$l"); done
  if [ "${#legacy_found[@]}" -gt 0 ]; then
    echo "meta-launch: team $team still runs on tmux's default socket (${legacy_found[*]}). Starting its lanes here would run each agent twice on the same transcripts." >&2
    echo "meta-launch: the owner must stop the old team (kill those sessions on the default socket or close their panes), then relaunch." >&2
    exit 1
  fi
fi

# 1. tmux sessions: create the missing ones (detached), keep the live ones.
for l in "${L[@]}"; do
  s="$team-$l"
  if "${tmux_t[@]}" has-session -t "=$s" 2> /dev/null; then
    # Claude exited or crashed and the pane fell back to its shell (meta-lane.sh left <lane>.exited): start the lane
    # again in place, resumed by id. A pane still running claude is never touched.
    if [ ! -f "$meta_dir/lane-sessions/$l.exited" ] || claude_in_pane "$s" "$(proc_of "$l")"; then echo "tmux $s: running"
    elif [ "$dry" = 1 ]; then echo "[dry-run] ${tmux_t[*]} respawn-pane -k -t $s $here/meta-lane.sh $team $l"
    else
      mark_start "$l"
      "${tmux_t[@]}" respawn-pane -k -t "=$s:" -c "$repo" -e "AW_META_CONFIG=$config" ${lc_env[@]+"${lc_env[@]}"} "$here/meta-lane.sh $team $l"
      echo "tmux $s: restarted (claude had exited)"
      wait_reported "$l"
    fi
  elif [ "$dry" = 1 ]; then echo "[dry-run] ${tmux_t[*]} new-session -d -s $s -c $repo $here/meta-lane.sh $team $l"
  else
    mark_start "$l"
    "${tmux_t[@]}" new-session -d -s "$s" -c "$repo" -e "AW_META_CONFIG=$config" ${lc_env[@]+"${lc_env[@]}"} "$here/meta-lane.sh $team $l"
    echo "tmux $s: started"
    wait_reported "$l"
  fi
done

# 2. Warp launch configuration: the first lane on the left, the others stacked on the right.
# The pane attaches, or creates the session itself with the same command as step 1, so a session that is missing when
# Warp opens (a --lanes run, a lane that exited) still runs the lane instead of a plain shell under its name.
attach() { printf 'tmux -L %s new -A -s %s -c %s -e AW_META_CONFIG=%s%s %s' "$team" "$team-$1" "$repo" "$config" "$lc_str" "'$here/meta-lane.sh $team $1'"; }
pane() { # indent lane
  printf '%s- cwd: %s\n%s  commands:\n%s    - exec: %s\n' "$1" "$repo" "$1" "$1" "$(attach "$2")"
}
render() {
  echo "# Written by scripts/meta-launch.sh from $config; rerun it rather than editing."
  echo "name: $team team"
  echo "windows:"
  echo "  - tabs:"
  echo "      - title: $team"
  echo "        layout:"
  if [ "${#A[@]}" = 1 ]; then
    printf '          cwd: %s\n          commands:\n            - exec: %s\n' "$repo" "$(attach "${A[0]}")"
    return
  fi
  echo "          split_direction: vertical"
  echo "          panes:"
  pane "            " "${A[0]}"
  echo "            - split_direction: horizontal"
  echo "              panes:"
  for l in "${A[@]:1}"; do pane "                " "$l"; done
}
viewer_warp() {
  if [ "$dry" = 1 ]; then echo "[dry-run] would write $yaml:"; render; return 0; fi
  mkdir -p "$dir"
  render > "$yaml.tmp"
  mv "$yaml.tmp" "$yaml"
  echo "warp: $yaml"

  # 3. Open it in Warp.
  if [ "$open" = 1 ]; then
    # Warp opens a launch configuration by its name (the YAML's name:, URI-encoded), not by its file path.
    uri="warp://launch/$(jq -rn --arg n "$team team" '$n|@uri')"
    echo "warp: opening $uri"
    hint="open $uri in Warp (Command Palette → Launch Configuration → $team team)"
    # an open that fails (no app registered for warp://) leaves the sessions and the layout in place: print the manual way, exit 0
    if [ "$(uname -s)" = Darwin ]; then open "$uri" || echo "warp: could not open it; $hint"
    elif command -v xdg-open > /dev/null; then xdg-open "$uri" > /dev/null 2>&1 &
    elif command -v warp-terminal > /dev/null; then warp-terminal "$uri" > /dev/null 2>&1 &
    else echo "$hint"; fi
  fi
}

# The print viewer: nothing opens; one attach command per lane (the lanes this run covers).
viewer_print() {
  [ "$dry" = 1 ] && echo "[dry-run] would print:"
  for l in "${L[@]}"; do echo "${tmux_t[*]} attach -t =$team-$l"; done
  return 0
}

# The exec-template viewer: one window per lane running the file's argv, with {title} and {name} the session name, {cwd} the
# repo and {cmd...} the attach command. The window is started detached; a terminal that is not installed never stops the
# sessions (spec SL5): the attach command is printed instead.
viewer_exec() {
  local name argv l a
  name="$(jq -r '.viewer.name // .viewer.argv[0]' <<< "$LAUNCHER_DOC")"
  for l in "${L[@]}"; do
    argv=(); while IFS= read -r -d '' a; do argv+=("$a"); done < <(viewer_argv "$l")
    if [ "$dry" = 1 ]; then printf '[dry-run] viewer %s: ' "$name"; printf '%q ' "${argv[@]}"; echo; continue; fi
    [ "$open" = 1 ] || continue
    if ! command -v "${argv[0]}" > /dev/null 2>&1; then
      echo "meta-launch: viewer $name: ${argv[0]} not found; attach by hand:" >&2
      echo "${tmux_t[*]} attach -t =$team-$l"
      continue
    fi
    ( "${argv[@]}" > /dev/null 2>&1 < /dev/null & )
    echo "viewer $name: window for $team-$l"
  done
  return 0
}

case "$viewer" in
  print) viewer_print ;;
  exec-template) viewer_exec ;;
  *) viewer_warp ;;
esac
