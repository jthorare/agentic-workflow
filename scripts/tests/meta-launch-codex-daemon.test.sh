#!/usr/bin/env bash
# Tests for #2286: a Codex lane keeps its own team identity when Codex runs hooks and tools in a shared daemon.
# A fake codex mimics the daemon topology: unless it was started with --no-daemon, its hook runs in a detached process whose
# ancestry does not include the lane script and whose environment carries ANOTHER team's AW_META_* (team A). With --no-daemon
# the hook runs under the fake, as in the default direct topology. The hook is the real config/hooks/meta-lane-gate.sh.
# Run: bash scripts/tests/meta-launch-codex-daemon.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$DIR/lib/test-portable.sh"   # timeout for macOS
ROOT="$(cd "$DIR/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
command -v jq > /dev/null || { echo "ok - skipped: jq not installed"; exit 0; }
WORK="$(mktemp -d)"
KEEP=""
trap '[ -z "$KEEP" ] || kill "$KEEP" 2> /dev/null; rm -r "$WORK"' EXIT
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state"
unset TMUX AW_LAUNCHER_CONFIG AW_META_CONFIG AW_META_TEAM AW_META_LANE AW_META_DIR AW_META_LANE_PID AW_META_NO_STALE_CHECK
mkdir -p "$HOME" "$WORK/repo" "$AW_STATE_DIR/meta/teams/t1" "$WORK/teamA/lane-sessions"
jq -n --arg r "$WORK/repo" '{team:"t1", repo:$r, metaLane:"meta", lanes:["api","ui"]}' > "$AW_STATE_DIR/meta/teams/t1/team.json"
ids="$AW_STATE_DIR/meta/teams/t1/lane-sessions"
cat > "$WORK/aw-meta" <<'EOS'
#!/usr/bin/env bash
# Logs which team and lane each hook call resolved, and the reply text a Stop hook records.
in=""; [ "${2:-}" = --stdin ] && in="$(cat)"
echo "${AW_META_HOOK:-}|${AW_META_TEAM:-}/${AW_META_LANE:-}|$1|$in" >> "$FAKE/calls"
[ "$1" != lane-inbox ] || echo inbox
exit 0
EOS
chmod +x "$WORK/aw-meta"
export AW_META_BIN="$WORK/aw-meta" AW_META_NO_SHELL=1 AW_META_ID_WAIT=5 AW_META_ID_GRACE=1
sleep 600 & KEEP=$!   # team A's lane script stand-in: alive, and never an ancestor of anything

cat > "$WORK/codex" <<'EOS'
#!/usr/bin/env bash
# Show up in ps as "codex" (a native codex binary's name), as the hooks' own_agent check matches the agent by name.
if [ -z "${FAKE_REEXEC:-}" ]; then FAKE_REEXEC=1 exec -a codex bash "$0" "$@"; fi
if [ "${1:-}" = --help ]; then
  echo "Usage: codex [OPTIONS] [PROMPT]"
  [ ! -e "$FAKE/has-no-daemon" ] || echo "      --no-daemon   Run without the shared app-server daemon"
  exit 0
fi
printf '%s\n' "$*" >> "$FAKE/argv"
id="$(cat /proc/sys/kernel/random/uuid 2> /dev/null || uuidgen | tr '[:upper:]' '[:lower:]')"
if [ "${1:-}" != resume ] && [ "${2:-}" != resume ]; then
  mkdir -p "$HOME/.codex/sessions/2026/11/08"
  : > "$HOME/.codex/sessions/2026/11/08/rollout-2026-11-08T00-00-00-$id.jsonl"
fi
hook() { # a turn: a prompt, then a Stop that answers with a REPLY TO META line
  printf '{"hook_event_name":"UserPromptSubmit"}' | bash "$ROOT/config/hooks/meta-lane-gate.sh" > /dev/null 2>&1
  jq -nc --arg m "REPLY TO META: hi from $AW_META_LANE" '{hook_event_name:"Stop", last_assistant_message:$m}' | bash "$ROOT/config/hooks/meta-lane-gate.sh" > /dev/null 2>&1
}
nested_hook() { # a `codex exec` the agent's shell tool starts: named like the agent, below a shell, inheriting the lane variables
  bash -c 'exec -a codex bash -c "printf %s \"\$0\" | bash \"\$1\" > /dev/null 2>&1" "{\"hook_event_name\":\"UserPromptSubmit\"}" "$ROOT/config/hooks/meta-lane-gate.sh"'
}
case " $* " in
  *" --no-daemon "*) [ ! -e "$FAKE/nested" ] || nested_hook; hook ;;   # direct topology: the hook is this process's child, under the lane script
  *)  # shared daemon: a double fork reparents the hook away from the lane script, and the environment is team A's
    rm -f "$FAKE/daemon-done"
    ( ( AW_META_TEAM=teamA AW_META_LANE=meta AW_META_DIR="$FAKE/teamA" AW_META_LANE_PID="$KEEP" hook; : > "$FAKE/daemon-done" ) & ) &
    for _ in $(seq 100); do [ -e "$FAKE/daemon-done" ] && break; sleep 0.1; done ;;   # the whole turn, so no late write lands in the next run
esac
EOS
chmod +x "$WORK/codex"

# A catalog alias derived from the built-in codex entry, as a user's launcher.json would add it.
LCFG="$WORK/launcher.json"
jq --arg c "$WORK/codex" '{schema:1, agents:{
    codex: {override:true, bin:$c},
    "codex-yolo": (.agents.codex + {label:"Codex yolo", bin:$c})}}' "$ROOT/config/launcher/builtins.json" > "$LCFG"

# run_lane starts the lanes in $LANES (default: api) one after the other, as meta-launch does.
run_lane() { # <agent> <fresh|resume> <yes|no: does this codex have --no-daemon>
  local agent="$1" mode="$2" support="$3" rid=""
  rm -rf "$ids" "$WORK/teamA/lane-sessions"/* "$WORK/argv"; rm -f "$WORK/nested"; [ -z "${NESTED:-}" ] || : > "$WORK/nested"; mkdir -p "$ids" "$WORK/teamA/lane-sessions"
  rm -f "$WORK/has-no-daemon"; [ "$support" = yes ] && : > "$WORK/has-no-daemon"
  jq --arg a "$agent" '.agent = $a' "$AW_STATE_DIR/meta/teams/t1/team.json" > "$WORK/team.tmp" && mv "$WORK/team.tmp" "$AW_STATE_DIR/meta/teams/t1/team.json"
  if [ "$mode" = resume ]; then
    rid="11111111-2222-3333-4444-555555555555"
    mkdir -p "$HOME/.codex/sessions/2026/11/08"; : > "$HOME/.codex/sessions/2026/11/08/rollout-2026-11-08T00-00-00-$rid.jsonl"
    printf '%s\n' "$rid" > "$ids/api"
  fi
  : > "$WORK/calls"
  for lane in ${LANES:-api}; do
    FAKE="$WORK" ROOT="$ROOT" KEEP="$KEEP" AW_LAUNCHER_CONFIG="$LCFG" timeout 30 bash "$DIR/meta-lane.sh" t1 "$lane" > "$WORK/out" 2>&1 || true
  done
  sleep 0.3
}
export FAKE="$WORK"
state() { # the lane's trace in team B's dir, and how many files team A's dir got
  echo "B=$(awk '{print $NF}' "$ids/api.hook" 2> /dev/null):A=$(ls "$WORK/teamA/lane-sessions" 2> /dev/null | wc -l | tr -d ' ')"
}

for agent in codex codex-yolo; do
  for mode in fresh resume; do
    run_lane "$agent" "$mode" yes
    check "$agent $mode: --no-daemon goes right after the binary" "$(head -1 "$WORK/argv" | awk '{print $1}')" "--no-daemon"
    check "$agent $mode: the hook ran under the lane (team B trace 'ran') and team A's state is untouched" "$(state)" "B=ran:A=0"
  done
done
run_lane codex resume yes
check "resume argv: --no-daemon before the resume subcommand" "$(head -1 "$WORK/argv" | cut -d' ' -f1-2)" "--no-daemon resume"

# Two lanes of team B: every hook call resolves its own lane and team, and a reply is recorded for that lane.
LANES="api ui" run_lane codex fresh yes
check "two lanes: each lane's prompt hook and reply resolved that lane in team t1" \
  "$(sort -u "$WORK/calls" | tr '\n' ';')" \
  "Stop|t1/api|lane-reply|hi from api;Stop|t1/api|lane-stop-check|;Stop|t1/ui|lane-reply|hi from ui;Stop|t1/ui|lane-stop-check|;UserPromptSubmit|t1/api|board-line|;UserPromptSubmit|t1/api|lane-inbox|;UserPromptSubmit|t1/ui|board-line|;UserPromptSubmit|t1/ui|lane-inbox|;"
check "two lanes: team A's state stays empty" "$(ls "$WORK/teamA/lane-sessions" | wc -l | tr -d ' ')" "0"
run_lane codex fresh no
check "without --no-daemon (the bug): no hook call is accepted for team t1 (or anyone: the daemon's hooks are not under the lane)" \
  "$(cut -d'|' -f2 "$WORK/calls" | sort -u | tr '\n' ';')" ""

# Nested agent: a codex exec started by the lane's agent is still rejected under --no-daemon; the lane's own hook still acts.
NESTED=1 run_lane codex fresh yes
check "nested codex exec: its hook makes no aw-meta call, the lane's own turn is recorded once" \
  "$(grep -c 'UserPromptSubmit|t1/api|lane-inbox' "$WORK/calls"):$(grep -c 'lane-reply' "$WORK/calls")" "1:1"
check "nested codex exec: the lane's trace still ends 'ran'" "$(state)" "B=ran:A=0"

# An older codex that has no --no-daemon still launches, with its argv unchanged.
run_lane codex-yolo fresh no
check "a codex without --no-daemon: argv carries no such flag and the lane starts" "$(grep -c -- '--no-daemon' "$WORK/argv"):$(wc -l < "$WORK/argv" | tr -d ' ')" "0:1"
check "that lane's .agent record says the daemon is not turned off" "$(jq -c '[.agent, .noDaemon]' "$ids/api.agent")" '["codex-yolo",false]'
run_lane codex-yolo fresh yes
check "a codex with --no-daemon: the .agent record says so" "$(jq -c '[.agent, .noDaemon]' "$ids/api.agent")" '["codex-yolo",true]'

# Other agents are untouched.
cp "$WORK/codex" "$WORK/plain-agent"
jq -n --arg c "$WORK/plain-agent" '{schema:1, agents:{plain:{bin:$c, open:["{bin}","{seed}"], processName:"plain", sessionId:"none"}}}' > "$LCFG"
run_lane plain fresh yes
check "a non-Codex agent never gets --no-daemon" "$(grep -c -- '--no-daemon' "$WORK/argv" 2> /dev/null)" "0"
exit "$fail"
