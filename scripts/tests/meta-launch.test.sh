#!/usr/bin/env bash
# Tests for scripts/meta-launch.sh and scripts/meta-lane.sh: the Warp launch configuration, tmux sessions recreated
# on a private tmux server, resume-by-id vs a fresh start, and the meta lane's role-set. A stub stands in for claude.
# Run: bash scripts/tests/meta-launch.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
command -v tmux > /dev/null || { echo "ok - skipped: tmux not installed"; exit 0; }
# Every tmux in this file runs on a private server: scripts/test-tmux.sh unsets TMUX (a shell inside a tmux client would otherwise
# send these calls to the box's own server, whatever TMUX_TMPDIR says), makes a temp TMUX_TMPDIR, and puts a `tmux` shim first on PATH
# that adds `-L t1` and refuses to run with TMUX set. The team under test is t1, and meta-launch.sh runs `tmux -L t1` itself, so the
# preset socket name makes this file's own calls and the launcher's one server.
AW_TEST_TMUX=t1
. "$DIR/test-tmux.sh" || exit 2
# golden_transcript <scripts-dir>: what scripts/meta-launch.sh and scripts/meta-lane.sh do with NO launcher.json, as one
# normalized transcript: the dry-run, the Warp YAML, every tmux command the launcher issues (a recording shim in front of
# tmux), and the claude command lines meta-lane.sh runs (fresh with a multi-line prompt, resumed, no prompt, with
# AW_META_CLAUDE_ARGS, the meta lane's role-set). The committed golden began as the scripts' output before SL-1 and
# has since gained only the team's `-L <team>` and the legacy-socket check lines (`bash meta-launch.test.sh --print-golden
# <scripts dir>` regenerates it; review the diff); the test compares the current scripts to it.
golden_transcript() {
  local S="$1" G; G="$(mktemp -d)"
  (
    export HOME="$G/home" AW_STATE_DIR="$G/state" AW_WARP_LAUNCH_DIR="$G/warp" CALLS="$G/calls"
    unset TMUX AW_LAUNCHER_CONFIG
    for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done
    export AW_META_NO_STALE_CHECK=1   # the stub aw-meta has no version command: the staleness check (meta-version.test.sh) would add a warning and a call
    export AW_META_LEGACY_TMUX_SOCKET=legacy-golden   # the launcher's legacy-socket check looks here (a private socket, empty), never at tmux's default
    mkdir -p "$HOME/.claude/projects/p" "$G/repo" "$G/prompts" "$AW_STATE_DIR/meta" "$G/shim"
    printf 'API kickoff\nline two $HOME "quoted" '"'"'single'"'"'\n' > "$G/prompts/api.md"
    local real; real="$(type -P tmux)"
    printf '#!/usr/bin/env bash\nprintf "tmux" >> "%s"; printf " %%q" "$@" >> "%s"; echo >> "%s"\nexec "%s" "$@"\n' "$G/tmux-calls" "$G/tmux-calls" "$G/tmux-calls" "$real" > "$G/shim/tmux"
    printf '#!/usr/bin/env bash\nprintf "%%s|" "$@" >> "$CALLS"; echo >> "$CALLS"\n' > "$G/claude"
    printf '#!/usr/bin/env bash\nexec sleep 600\n' > "$G/claude-noop"
    printf '#!/usr/bin/env bash\necho "$*" >> "$CALLS.meta"\n' > "$G/aw-meta"
    for o in open xdg-open; do printf '#!/usr/bin/env bash\nexit 0\n' > "$G/shim/$o"; chmod +x "$G/shim/$o"; done   # never the real Warp
    chmod +x "$G/shim/tmux" "$G/claude" "$G/claude-noop" "$G/aw-meta"
    export PATH="$G/shim:$PATH" AW_META_BIN="$G/aw-meta" AW_META_NO_SHELL=1 AW_META_CLAUDE="$G/claude-noop"
    local cfg="$AW_STATE_DIR/meta/team.json"
    jq -n --arg r "$G/repo" --arg p "$G/prompts" '{team:"t1", repo:$r, prompts:$p, metaLane:"meta", lanes:["api","ui"]}' > "$cfg"
    echo "== dry-run, no sessions"; bash "$S/meta-launch.sh" --dry-run; echo "rc=$?"
    tmux new-session -d -s keepalive "sleep 600"
    echo "== launch"; bash "$S/meta-launch.sh" --no-open; echo "rc=$?"
    echo "== yaml"; cat "$AW_WARP_LAUNCH_DIR/t1-team.yaml"
    for _ in $(seq 100); do [ "$(ls "$AW_STATE_DIR/meta/lane-sessions" 2> /dev/null | grep -vc exited)" -ge 3 ] && break; sleep 0.1; done
    echo "== rerun with --lanes"; bash "$S/meta-launch.sh" --no-open --lanes api; echo "rc=$?"
    tmux kill-session -t t1-api 2> /dev/null; tmux new-session -d -s t1-api bash; : > "$AW_STATE_DIR/meta/lane-sessions/api.exited"
    echo "== dry-run, exited lane"; bash "$S/meta-launch.sh" --dry-run --lanes api; echo "rc=$?"
    echo "== tmux calls"; cat "$G/tmux-calls"
    tmux kill-server 2> /dev/null; sleep 0.3
    find "$AW_STATE_DIR/meta/lane-sessions" -type f -delete
    rm -f "$CALLS.meta"; export AW_META_CLAUDE="$G/claude"
    echo "== lane api, fresh"; bash "$S/meta-lane.sh" t1 api; cat "$CALLS"; : > "$CALLS"
    touch "$HOME/.claude/projects/p/$(cat "$AW_STATE_DIR/meta/lane-sessions/api").jsonl"
    echo "== lane api, resumed"; bash "$S/meta-lane.sh" t1 api; cat "$CALLS"; : > "$CALLS"
    echo "== lane api, resumed, AW_META_CLAUDE_ARGS"; AW_META_CLAUDE_ARGS="--model x --verbose" bash "$S/meta-lane.sh" t1 api; cat "$CALLS"; : > "$CALLS"
    echo "== lane ui, no prompt"; bash "$S/meta-lane.sh" t1 ui; cat "$CALLS"; : > "$CALLS"
    echo "== lane ui, fresh, AW_META_CLAUDE_ARGS"; rm "$AW_STATE_DIR/meta/lane-sessions/ui"; AW_META_CLAUDE_ARGS="--model x" bash "$S/meta-lane.sh" t1 ui; cat "$CALLS"; : > "$CALLS"
    echo "== lane meta"; bash "$S/meta-lane.sh" t1 meta; cat "$CALLS.meta"
    echo "== bad lane"; bash "$S/meta-lane.sh" t1 'Bad Lane' 2>&1; echo "rc=$?"
  ) 2>&1 | sed -E -e "s#$G#@WORK@#g" -e "s#$S#@SCRIPTS@#g" -e 's/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/@ID@/g'
  rm -r "$G"
}
if [ "${1:-}" = "--print-golden" ]; then golden_transcript "${2:?--print-golden <scripts dir>}"; exit 0; fi

# The golden: with no launcher.json the Warp layout, every tmux command and every claude command line are unchanged.
GOLDEN="$DIR/tests/fixtures/meta-launch/no-launcher.golden.txt"
golden_transcript "$DIR" > "${TMPDIR:-/tmp}/meta-launch-golden.$$"
if diff -u "$GOLDEN" "${TMPDIR:-/tmp}/meta-launch-golden.$$" > "${TMPDIR:-/tmp}/meta-launch-golden.diff.$$"; then
  echo "ok - no launcher.json: dry-run, Warp YAML, tmux commands and claude command lines match the committed golden"
else
  echo "not ok - no launcher.json: output differs from the committed golden"; sed 's/^/  /' "${TMPDIR:-/tmp}/meta-launch-golden.diff.$$"; fail=1
fi
unlink "${TMPDIR:-/tmp}/meta-launch-golden.$$"; unlink "${TMPDIR:-/tmp}/meta-launch-golden.diff.$$"

WORK="$(mktemp -d)"
trap 'tmux -L t1-legacy kill-server 2> /dev/null; ttmux_cleanup; rm -r "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done   # the caller may be a meta session: its team config, bin and claude must not reach the stubs
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_WARP_LAUNCH_DIR="$WORK/warp"
mkdir -p "$HOME/.claude/projects/p" "$WORK/repo" "$WORK/prompts" "$AW_STATE_DIR/meta"
echo "API kickoff" > "$WORK/prompts/api.md"
export CALLS="$WORK/claude-calls"
cat > "$WORK/claude" <<'EOF'
#!/usr/bin/env bash
printf '%s|' "$@" >> "$CALLS"; echo >> "$CALLS"
EOF
cat > "$WORK/aw-meta" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$CALLS.meta"
EOF
chmod +x "$WORK/claude" "$WORK/aw-meta"
export AW_META_CLAUDE="$WORK/claude" AW_META_BIN="$WORK/aw-meta" AW_META_NO_SHELL=1
# `open` (macOS) and `xdg-open` would start the real Warp from a launch that opens its window: stubs first on PATH record the URI instead.
mkdir -p "$WORK/guard"
for o in open xdg-open; do printf '#!/usr/bin/env bash\necho "$1" > "%s"\n' "$WORK/opened.guard" > "$WORK/guard/$o"; chmod +x "$WORK/guard/$o"; done
export PATH="$WORK/guard:$PATH"
export AW_META_NO_STALE_CHECK=1   # the stub aw-meta has no version command; the staleness check has its own suite (meta-version.test.sh)
export AW_META_LEGACY_TMUX_SOCKET=t1-legacy   # the launcher's legacy (default-socket) check goes to this private socket, never the real default
cfg="$AW_STATE_DIR/meta/team.json"
jq -n --arg r "$WORK/repo" --arg p "$WORK/prompts" '{team:"t1", repo:$r, prompts:$p, metaLane:"meta", lanes:["api","ui"]}' > "$cfg"
# A process the pane check and tmux see under a name of the test's choosing: a copy of sleep named <name> on Linux. macOS kills a copy
# of its system sleep (platform binary), so there it is a one-line C program built with cc; both show <name> to ps and to tmux.
if [ "$(uname -s)" = Darwin ]; then
  printf '#include <unistd.h>\n#include <stdlib.h>\nint main(int c, char **v) { sleep(c > 1 ? (unsigned) atoi(v[1]) : 1); return 0; }\n' > "$WORK/sleeper.c"
  cc -o "$WORK/sleeper" "$WORK/sleeper.c" || { echo "not ok - cc is needed to build the sleeper stub on macOS"; exit 1; }
  SLEEPBIN="$WORK/sleeper"
else SLEEPBIN="$(command -v sleep)"; fi
mkdir -p "$WORK/procs"
sleeper() { [ -x "$WORK/procs/$1" ] || cp "$SLEEPBIN" "$WORK/procs/$1"; echo "$WORK/procs/$1"; }   # sleeper <name>: its path

# meta-lane.sh: fresh start with a prompt, then resume once a transcript exists.
cat > "$WORK/claude-env" <<'EOF2'
#!/usr/bin/env bash
env | grep -E '^(CLAUDECODE|CLAUDE_CODE_SESSION_ID|CLAUDE_CODE_MESSAGING_SOCKET|CLAUDE_CODE_USE_BEDROCK)=' | sort > "$CALLS.env"
EOF2
chmod +x "$WORK/claude-env"
CLAUDECODE=1 CLAUDE_CODE_SESSION_ID=parent CLAUDE_CODE_MESSAGING_SOCKET=/s CLAUDE_CODE_USE_BEDROCK=1 \
  AW_META_CLAUDE="$WORK/claude-env" bash "$DIR/meta-lane.sh" t1 envcheck
check "claude exited, no shell (tests): no exited marker" "$(test -e "$AW_STATE_DIR/meta/lane-sessions/envcheck.exited" && echo yes)" ""
check "launched from a Claude session: its identity cleared, settings kept" "$(cat "$CALLS.env")" "CLAUDE_CODE_USE_BEDROCK=1"
bash "$DIR/meta-lane.sh" t1 api
id="$(cat "$AW_STATE_DIR/meta/lane-sessions/api")"
check "fresh: id recorded" "$(printf '%s' "$id" | grep -cE '^[0-9a-f-]{36}$')" "1"
check "fresh: session id, stable name, kickoff prompt" "$(cat "$CALLS")" "--session-id|$id|-n|t1-api|API kickoff|"
: > "$CALLS"; touch "$HOME/.claude/projects/p/$id.jsonl"
bash "$DIR/meta-lane.sh" t1 api
check "transcript exists: resumes the same id" "$(cat "$CALLS")" "--resume|$id|-n|t1-api|"
: > "$CALLS"
bash "$DIR/meta-lane.sh" t1 ui
check "no prompt file: starts without a prompt" "$(cut -d'|' -f3,4 "$CALLS")" "-n|t1-ui"
: > "$CALLS"
bash "$DIR/meta-lane.sh" t1 meta
mid="$(cat "$AW_STATE_DIR/meta/lane-sessions/meta")"
check "meta lane: role-set with its id and name" "$(cat "$CALLS.meta")" "role-set --session $mid --name t1-meta"
bash "$DIR/meta-lane.sh" t1 'Bad Lane' 2> /dev/null; RC=$?
check "bad lane name refused" "$RC" "2"

# meta-launch.sh: sessions created on a private server, then kept on a rerun; Warp YAML written. The stub lanes exit at
# once, so a keepalive session stops the server from shutting down between the launcher's new-session calls.
tmux new-session -d -s keepalive "sleep 600"
OUT="$(bash "$DIR/meta-launch.sh" --no-open)"; RC=$?
check "launch: exit 0" "$RC" "0"
check "launch: three sessions started" "$(printf '%s\n' "$OUT" | grep -c ': started')" "3"
yaml="$AW_WARP_LAUNCH_DIR/t1-team.yaml"
check "launch: warp yaml written" "$(test -s "$yaml" && echo yes)" "yes"
check "launch: one attach per lane" "$(grep -c 'exec: tmux -L t1 new -A -s t1-' "$yaml")" "3"
check "launch: a pane that creates its session runs the lane, not a shell" "$(grep -c "exec: tmux -L t1 new -A -s t1-api -c $WORK/repo -e AW_META_CONFIG=$cfg '$DIR/meta-lane.sh t1 api'" "$yaml")" "1"
check "launch: absolute cwd everywhere" "$(grep -c "cwd: $WORK/repo" "$yaml")" "3"
check "launch: meta pane first" "$(grep -m1 -o 't1-[a-z]*' "$yaml")" "t1-meta"
# A live lane: a script running a child, as meta-lane.sh runs claude (tmux reports the pane's command as bash).
tmux kill-session -t t1-api 2> /dev/null
tmux new-session -d -s t1-api "bash -c 'sleep 60; true'" 2> /dev/null
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes api)"
check "rerun: live session kept" "$OUT" "$(printf 'tmux t1-api: running\nwarp: %s' "$yaml")"
check "rerun with --lanes: the layout still has every lane" "$(grep -c 'exec: tmux -L t1 new -A -s t1-' "$yaml")" "3"
mkdir -p "$WORK/path"; for o in open xdg-open; do printf '#!/usr/bin/env bash\necho "$1" > "%s"\n' "$WORK/opened" > "$WORK/path/$o"; chmod +x "$WORK/path/$o"; done   # open on macOS, xdg-open on Linux
OUT="$(PATH="$WORK/path:$PATH" bash "$DIR/meta-launch.sh" --lanes api)"
for _ in $(seq 50); do [ -s "$WORK/opened" ] && break; sleep 0.1; done   # xdg-open runs in the background
check "open: by the config's name, URI-encoded" "$(tail -1 <<< "$OUT"):$(cat "$WORK/opened" 2> /dev/null)" "warp: opening warp://launch/t1%20team:warp://launch/t1%20team"
# An open that fails (no app registered for warp://: LSOpenURLs -10814) never aborts a launch whose sessions and layout exist.
mkdir -p "$WORK/failpath"; for o in open xdg-open warp-terminal; do printf '#!/usr/bin/env bash\nexit 1\n' > "$WORK/failpath/$o"; chmod +x "$WORK/failpath/$o"; done
OUT="$(PATH="$WORK/failpath:$PATH" bash "$DIR/meta-launch.sh" --lanes api 2>&1)"; RC=$?
check "open fails: the launch still exits 0" "$RC" "0"
if [ "$(uname -s)" = Darwin ]; then
  check "open fails: the manual hint is printed" "$(grep -c 'Command Palette' <<< "$OUT")" "1"
fi
# claude restarted by hand in the fallback shell: the marker is still there, but a claude runs in the pane.
mkdir -p "$WORK/fakebin"; cp "$SLEEPBIN" "$WORK/fakebin/claude"
: > "$AW_STATE_DIR/meta/lane-sessions/api.exited"
tmux kill-session -t t1-api; tmux new-session -d -s t1-api "bash -c '$WORK/fakebin/claude 60; true'"
for _ in $(seq 50); do pgrep -f "$WORK/fakebin/claude 60" > /dev/null && break; sleep 0.1; done
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes api)"
check "rerun: a claude restarted by hand under a stale marker is left alone" "$(head -1 <<< "$OUT")" "tmux t1-api: running"
tmux kill-session -t t1-api; tmux new-session -d -s t1-api bash   # claude exited; the pane is back at its shell
: > "$AW_STATE_DIR/meta/lane-sessions/api.exited"                       # as meta-lane.sh leaves it
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes api)"
check "rerun: a lane whose claude exited is restarted in place" "$(head -1 <<< "$OUT")" "tmux t1-api: restarted (claude had exited)"
tmux kill-server 2> /dev/null
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes api)"
check "after tmux died: recreated" "$(printf '%s\n' "$OUT" | head -1)" "tmux t1-api: started"
OUT="$(AW_META_CONFIG="$WORK/none.json" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "no team config: exit 1 with the fix" "$RC:$(printf '%s' "$OUT" | grep -c 'aw-meta team-init')" "1:1"
jq '.repo = "relative/path"' "$cfg" > "$WORK/rel.json"
OUT="$(AW_META_CONFIG="$WORK/rel.json" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "relative repo refused (Warp needs absolute cwd)" "$RC" "1"
tmux kill-server 2> /dev/null   # the stub lane may still be winding down: no session may exist for this dry run
mkdir -p "$WORK/bin"; ln -s "$DIR/meta-launch.sh" "$WORK/bin/aw-meta-launch"   # as install-meta.sh links it
OUT="$("$WORK/bin/aw-meta-launch" --no-open --dry-run --lanes api)"
check "run through a symlink: finds meta-lane.sh beside the real script" "$(printf '%s\n' "$OUT" | grep -c "^\[dry-run\] tmux -L t1 new-session .* $DIR/meta-lane.sh t1 api")" "1"
jq '.lanes = ["meta", "api"]' "$cfg" > "$WORK/dup.json"   # team-init lists the meta lane in lanes too
OUT="$(AW_META_CONFIG="$WORK/dup.json" bash "$DIR/meta-launch.sh" --no-open --dry-run)"
check "meta lane listed in lanes: one pane, not two" "$(printf '%s\n' "$OUT" | grep -c 'exec: tmux -L t1 new -A -s t1-meta')" "1"

# The switch to the team's own socket: lanes still running on the legacy (default) socket stop a launch.
tmux kill-server 2> /dev/null; tmux -L t1-legacy kill-server 2> /dev/null; sleep 0.3
tmux -L t1-legacy new-session -d -s t1-api "sleep 600"
OUT="$(bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "legacy-socket session present: refused, naming it, exit 1" "$RC:$(grep -c 't1-api' <<< "$OUT"):$(grep -c 'still runs on tmux' <<< "$OUT")" "1:1:1"
check "legacy-socket session present: nothing was created" "$(tmux list-sessions -F '#S' 2> /dev/null | grep -c '^t1-')" "0"
OUT="$(bash "$DIR/meta-launch.sh" --no-open --dry-run 2>&1)"; RC=$?
check "legacy-socket session present: --dry-run prints the check and refuses too" "$RC:$(grep -c '^\[dry-run\] legacy-socket check: tmux -L t1-legacy has-session' <<< "$OUT")" "1:1"
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes ui 2>&1)"; RC=$?
check "warp viewer, legacy t1-api, --lanes ui: refused (the layout's panes cover every lane)" "$RC:$(grep -c 't1-api' <<< "$OUT"):$(tmux list-sessions -F '#S' 2> /dev/null | grep -c '^t1-')" "1:1:0"
echo '{"schema":1,"viewer":{"kind":"print"}}' > "$WORK/print.json"
OUT="$(AW_LAUNCHER_CONFIG="$WORK/print.json" bash "$DIR/meta-launch.sh" --no-open --lanes ui 2>&1)"; RC=$?
check "print viewer, legacy t1-api, --lanes ui: proceeds (it only attaches the lanes of the run)" "$RC:$(grep -c 'tmux t1-ui: started' <<< "$OUT")" "0:1"
tmux kill-server 2> /dev/null; sleep 0.3
OUT="$(bash "$DIR/meta-launch.sh" --no-open --allow-legacy 2>&1)"; RC=$?
check "legacy-socket session present, --allow-legacy: proceeds" "$RC:$(grep -c 'tmux t1-api: started' <<< "$OUT")" "0:1"
tmux kill-server 2> /dev/null; sleep 0.3
OUT="$(AW_META_ALLOW_LEGACY=1 bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "legacy-socket session present, AW_META_ALLOW_LEGACY=1: proceeds" "$RC:$(grep -c 'tmux t1-api: started' <<< "$OUT")" "0:1"
tmux kill-server 2> /dev/null; tmux -L t1-legacy kill-server 2> /dev/null; sleep 0.3
OUT="$(bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "no legacy-socket session: proceeds" "$RC:$(grep -c 'tmux t1-api: started' <<< "$OUT")" "0:1"
tmux kill-server 2> /dev/null; sleep 0.3

# ---- launcher.json (SL-1; docs/csharp/sindri-launcher.md) ----
FIX="$DIR/../tests/fixtures/launcher"
LCFG="$WORK/launcher.json"
reset() { # a fresh tmux server and lane state
  tmux kill-server 2> /dev/null; sleep 0.3
  find "$AW_STATE_DIR/meta/lane-sessions" -mindepth 1 -delete 2> /dev/null   # files and the start-lock directory alike
  tmux new-session -d -s keepalive "sleep 600"
}
waitfor() { for _ in $(seq 100); do eval "$1" && return 0; sleep 0.1; done; return 1; }

# Every shared fixture against its bash: tag (the reader accepts the file or refuses it with SND-LAUNCH-011).
( . "$DIR/launcher-lib.sh"
  bad=""; n=0
  while IFS=' ' read -r f tag; do
    n=$((n + 1))
    if out="$(launcher_validate "$FIX/$f")"; then got=accept; else got=refuse; fi
    [ "$got" = "$tag" ] || bad="$bad $f(want $tag)"
    [ "$got" = accept ] || case "$out" in SND-LAUNCH-011:*) ;; *) bad="$bad $f(wording)" ;; esac
  done < <(jq -r '.fixtures[] | "\(.file) \(.bash)"' "$FIX/manifest.json")
  echo "$n:$bad" > "$WORK/fixture-verdicts" )
check "every shared fixture honours its bash: tag" "$(cut -d: -f2 "$WORK/fixture-verdicts")" ""
check "the whole fixture set ran" "$([ "$(cut -d: -f1 "$WORK/fixture-verdicts")" -ge 40 ] && echo yes)" "yes"

# A file that is refused stops the launch before any session exists.
reset
cp "$FIX/unknown-top-key.json" "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "invalid launcher.json: exit 1, SND-LAUNCH-011 with the file and the key" "$RC:$(grep -c "SND-LAUNCH-011: unknown key \"terminal\".*" <<< "$OUT"):$(grep -c "$LCFG" <<< "$OUT")" "1:1:1"
check "invalid launcher.json: no tmux session was created" "$(tmux list-sessions -F '#S' | grep -c '^t1-')" "0"
OUT="$(AW_LAUNCHER_CONFIG="$WORK/missing.json" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "AW_LAUNCHER_CONFIG naming a missing file: exit 1, the file named, no session created" "$RC:$(grep -c "$WORK/missing.json" <<< "$OUT"):$(tmux list-sessions -F '#S' | grep -c '^t1-')" "1:1:0"
OUT="$(bash "$DIR/meta-launch.sh" --force 2>&1)"; RC=$?
check "--force, --viewer and --stdout without --init-launcher: usage error" "$RC:$(grep -c -- '--init-launcher' <<< "$OUT")" "2:1"
echo '{"schema":1,"meta":{"lanesAgent":"nonesuch"}}' > "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "a lane agent the catalog lacks: exit 1 with SND-LAUNCH-003" "$RC:$(grep -c 'SND-LAUNCH-003' <<< "$OUT")" "1:1"
echo '{"schema":1,"agents":{"codex":{"bin":"x"}},"meta":{"lanesAgent":"codex"}}' > "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "a built-in id without override: true is named" "$RC:$(grep -c 'codex is built in: add "override": true' <<< "$OUT")" "1:1"

# viewer: warp written explicitly equals the no-file layout.
reset
OUT0="$(bash "$DIR/meta-launch.sh" --no-open --dry-run | sed "s#$WORK#W#g")"
cp "$FIX/viewer-warp.json" "$LCFG"
OUT1="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --dry-run | sed "s#$WORK#W#g")"
OUT2="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --dry-run | sed "s#$WORK#W#g" | grep -c 'AW_LAUNCHER_CONFIG')"
check "viewer warp in the file: today's layout, plus the file's path for each pane" "$(diff <(printf '%s\n' "$OUT0") <(printf '%s\n' "$OUT1" | sed 's# -e AW_LAUNCHER_CONFIG=[^ ]*##') > /dev/null && echo same):$OUT2" "same:3"
# The file at the default path ($AW_STATE_DIR/launcher.json) needs no variable and adds nothing to the panes.
cp "$FIX/viewer-warp.json" "$AW_STATE_DIR/launcher.json"
OUT1="$(bash "$DIR/meta-launch.sh" --no-open --dry-run | sed "s#$WORK#W#g")"
check "launcher.json at the default path with viewer warp: the same layout, byte for byte" "$OUT1" "$OUT0"
unlink "$AW_STATE_DIR/launcher.json"

# sessions (session collector CO-0): the key is the collector's; the bash reader accepts it and ignores it, so the layout is today's.
reset
cp "$FIX/sessions.json" "$LCFG"
OUT1="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --dry-run 2>&1 | sed "s#$WORK#W#g"; exit "${PIPESTATUS[0]}")"; RC=$?
check "a launcher.json with sessions: accepted, the same layout as no file" "$RC:$(diff <(printf '%s\n' "$OUT0") <(printf '%s\n' "$OUT1" | sed 's# -e AW_LAUNCHER_CONFIG=[^ ]*##') > /dev/null && echo same)" "0:same"
cp "$FIX/sessions-bogus.json" "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --dry-run 2>&1)"; RC=$?
check "a sessions key with an unknown member: still accepted by the bash reader (sindri launch show refuses it)" "$RC:$(grep -c 'SND-LAUNCH-011' <<< "$OUT")" "0:0"

# model and policy (L-17, owner 2026-10-16): per-entry form names the bash reader accepts and never applies, so the layout is today's.
reset
cp "$FIX/model-policy-forms.json" "$LCFG"
OUT1="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --dry-run 2>&1 | sed "s#$WORK#W#g"; exit "${PIPESTATUS[0]}")"; RC=$?
check "a launcher.json with model and policy forms: accepted, the same layout as no file" "$RC:$(diff <(printf '%s\n' "$OUT0") <(printf '%s\n' "$OUT1" | sed 's# -e AW_LAUNCHER_CONFIG=[^ ]*##') > /dev/null && echo same)" "0:same"
cp "$FIX/unknown-model-form.json" "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --dry-run 2>&1)"; RC=$?
check "an unknown model form value: still accepted by the bash reader (sindri launch show refuses it)" "$RC:$(grep -c 'SND-LAUNCH-011' <<< "$OUT")" "0:0"

# viewer: print — one attach line per lane, no Warp YAML.
reset; unlink "$yaml" 2> /dev/null; cp "$FIX/viewer-print.json" "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" 2>&1)"; RC=$?
check "viewer print: exit 0, three sessions started" "$RC:$(grep -c ': started' <<< "$OUT")" "0:3"
check "viewer print: one tmux attach line per lane, meta first" "$(grep '^tmux -L t1 attach' <<< "$OUT" | tr '\n' ';')" "tmux -L t1 attach -t =t1-meta;tmux -L t1 attach -t =t1-api;tmux -L t1 attach -t =t1-ui;"
check "viewer print: no Warp YAML written" "$(test -e "$yaml" && echo yes || echo no)" "no"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --lanes api 2>&1)"
check "viewer print with --lanes: only that lane" "$(grep -c '^tmux -L t1 attach' <<< "$OUT"):$(grep '^tmux -L t1 attach' <<< "$OUT")" "1:tmux -L t1 attach -t =t1-api"

# viewer: exec-template — one window per lane, the placeholders expanded into separate arguments.
cat > "$WORK/viewer" <<'EOF'
#!/usr/bin/env bash
printf '%s|' "$@" >> "$VIEWER_CALLS"; echo >> "$VIEWER_CALLS"
EOF
chmod +x "$WORK/viewer"; export VIEWER_CALLS="$WORK/viewer-calls"
jq -n --arg v "$WORK/viewer" '{schema:1, viewer:{kind:"exec-template", name:"rec", argv:[$v,"--title","{title}","--directory","{cwd}","--","{cmd...}"]}}' > "$LCFG"
reset; : > "$VIEWER_CALLS"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" 2>&1)"; RC=$?
waitfor '[ "$(wc -l < "$VIEWER_CALLS")" -ge 3 ]'
check "exec-template: exit 0, one window per lane" "$RC:$(wc -l < "$VIEWER_CALLS" | tr -d ' ')" "0:3"
check "exec-template: title, cwd and the attach argv as separate arguments" "$(grep -- '-t1-api\|t1-api' "$VIEWER_CALLS")" "--title|t1-api|--directory|$WORK/repo|--|tmux|-L|t1|attach|-t|=t1-api|"
check "exec-template: no Warp YAML written" "$(test -e "$yaml" && echo yes || echo no)" "no"
: > "$VIEWER_CALLS"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; sleep 0.3
check "exec-template with --no-open: no window is opened" "$(wc -c < "$VIEWER_CALLS" | tr -d ' ')" "0"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --dry-run 2>&1)"; sleep 0.3
check "exec-template --dry-run: one argv line per lane, nothing run" "$(grep -c '^\[dry-run\] viewer rec: ' <<< "$OUT"):$(wc -c < "$VIEWER_CALLS" | tr -d ' ')" "3:0"
jq -n '{schema:1, viewer:{kind:"exec-template", name:"ghost", argv:["no-such-terminal-xyz","{title}"]}}' > "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" 2>&1)"; RC=$?
check "a viewer that is not installed never stops the sessions (exit 0, the attach line is shown)" "$RC:$(grep -c 'ghost: no-such-terminal-xyz not found' <<< "$OUT"):$(grep -c '^tmux -L t1 attach -t =t1-api' <<< "$OUT")" "0:3:1"
jq -n --arg v "$WORK/viewer" '{schema:1, viewer:{kind:"exec-template", name:"rec", argv:[$v,"{title}"]}}' > "$LCFG"
jq '.viewer.argv[1] = "{cmd...}x"' "$LCFG" > "$WORK/l2.json"
OUT2="$(AW_LAUNCHER_CONFIG="$WORK/l2.json" bash "$DIR/meta-launch.sh" --dry-run 2>&1)"; RC=$?
check "a {cmd...} inside an argument is refused" "$RC:$(grep -c 'SND-LAUNCH-011' <<< "$OUT2")" "1:1"

# Lane agents from the catalog: a non-Claude lane, and ids reported one lane at a time.
cat > "$WORK/codex" <<'EOF'
#!/usr/bin/env bash
echo "start $AW_META_LANE" >> "$CALLS.events"
sleep 0.4
id="$(cat /proc/sys/kernel/random/uuid 2> /dev/null || uuidgen | tr '[:upper:]' '[:lower:]')"
mkdir -p "$HOME/.codex/sessions/2026/10/15"
: > "$HOME/.codex/sessions/2026/10/15/rollout-2026-10-15T00-00-00-$id.jsonl"
echo "wrote $AW_META_LANE $id" >> "$CALLS.events"
args=("$@"); printf '%s|' "${args[@]//$'\n'/ }" >> "$CALLS.codex"; echo "$AW_SINDRI_CHILD|$AW_META_TEAM|$AW_META_LANE" >> "$CALLS.codex"
echo "$AW_STATE_DIR|${AW_META_CONFIG:-}" >> "$CALLS.state"
echo "parent=$([ "$AW_META_LANE_PID" = "$PPID" ] && echo lane-script || echo other)|proc=$AW_META_LANE_PROC|agent=$AW_META_LANE_AGENT" >> "$CALLS.pid"
sleep 600
EOF
chmod +x "$WORK/codex"
jq -n --arg c "$WORK/codex" '{schema:1, agents:{codex:{override:true, bin:$c, model:"codex-flag", policy:"codex-overrides"}}, meta:{lanesAgent:"codex"}}' > "$LCFG"   # the forms are named and never applied: the argv check below holds
reset; : > "$CALLS.events"; : > "$CALLS.codex"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "codex lanes: the launch notes that they take part through their hooks" "$RC:$(grep -c -x 'lane api: codex, takes part in the obligations through its hooks (scripts/install-meta.sh installs them)' <<< "$OUT"):$(grep -c '^lane meta' <<< "$OUT")" "0:1:0"
ids="$AW_STATE_DIR/meta/lane-sessions"
waitfor '[ -s "$ids/api" ] && [ -s "$ids/ui" ] && [ "$(wc -l < "$CALLS.codex")" -ge 2 ]'
check "codex lanes: started one at a time (each id is reported before the next lane starts)" "$(awk '{print $1" "$2}' "$CALLS.events" | tr '\n' ';')" "start api;wrote api;start ui;wrote ui;"
a="$(tr -d '[:space:]' < "$ids/api")"; u="$(tr -d '[:space:]' < "$ids/ui")"
check "codex lanes: each lane recorded the id its own rollout file carries" "$(grep -c "wrote api $a" "$CALLS.events"):$(grep -c "wrote ui $u" "$CALLS.events")" "1:1"
check "codex lanes: two distinct ids" "$([ "$a" != "$u" ] && [ -n "$a" ] && echo yes)" "yes"
check "codex lanes: the built-in open argv (the model and policy forms add nothing), the prompt as the last argument, none without one" "$(grep -h -- '-C' "$CALLS.codex" | sort | tr '\n' ';')" "-C|$WORK/repo|1|t1|ui;-C|$WORK/repo|API kickoff  When you answer a message from the meta (aw-meta lane-inbox shows it), end your turn with a line starting REPLY TO META: followed by your reply.|1|t1|api;"
check "codex lanes: <lane>.agent records the catalog facts the lane map needs" "$(jq -cS . "$ids/api.agent")" '{"agent":"codex","hooks":true,"processName":"codex","transcripts":"~/.codex/sessions/*/*/*/rollout-*-{sessionId}.jsonl"}'
check "codex lanes: the meta lane (Claude) has no .agent record" "$(ls "$ids" | grep -c '^meta.agent$')" "0"
check "codex lanes: AW_META_LANE_PID is the agent's parent (the lane script), AW_META_LANE_PROC the process name, AW_META_LANE_AGENT the catalog id" "$(sort -u "$CALLS.pid" | tr '\n' ';')" "parent=lane-script|proc=codex|agent=codex;"
check "codex lanes: the agent's environment names the state directory and the team config" "$(sort -u "$CALLS.state" | tr '\n' ';')" "$AW_STATE_DIR|$cfg;"
check "codex lanes: two codex sessions, not three (the meta lane is Claude)" "$(wc -l < "$CALLS.codex" | tr -d ' ')" "2"
check "the lane variables are in the lane's environment and not in tmux's global one" "$(tmux show-environment -g 2>&1 | grep -c 'AW_SINDRI_CHILD\|AW_META_TEAM\|AW_META_LANE')" "0"
# The meta lane stays Claude Code even when lanesAgent says otherwise; its claude is the stub.
check "the meta lane runs the claude stub, not codex" "$(grep -c 'meta' "$CALLS.events")" "0"
# Resume: a lane whose rollout exists resumes by the reported id.
tmux kill-server 2> /dev/null; : > "$CALLS.codex"
AW_LAUNCHER_CONFIG="$LCFG" AW_META_NO_SHELL=1 bash "$DIR/meta-lane.sh" t1 api > /dev/null 2>&1 &
rp=$!; waitfor '[ -s "$CALLS.codex" ]'; kill "$rp" 2> /dev/null; pkill -f "$WORK/codex" 2> /dev/null; wait "$rp" 2> /dev/null
check "codex lane with a recorded id and its rollout: resumes it" "$(head -1 "$CALLS.codex" | cut -d'|' -f1-2)" "resume|$a"
# A non-Claude lane's pane is named after its agent: tmux reads the foreground process group's leader, and aw-meta's wake guard
# compares it with the catalog's processName. meta-lane.sh gives the agent its own group (job control); a Claude lane is untouched.
printf '#!/usr/bin/env bash\nexec -a codex %s 60\n' "$(sleeper codex)" > "$WORK/codex-named"; chmod +x "$WORK/codex-named"
jq --arg c "$WORK/codex-named" '.agents.codex.bin = $c' "$LCFG" > "$WORK/l4.json"
tmux kill-server 2> /dev/null; tmux new-session -d -s keepalive "sleep 600"
tmux new-session -d -s t1-api "env AW_LAUNCHER_CONFIG=$WORK/l4.json AW_META_NO_SHELL=1 AW_META_ID_WAIT=1 AW_META_ID_GRACE=1 bash $DIR/meta-lane.sh t1 api"
waitfor '[ "$(tmux display-message -p -t =t1-api: "#{pane_current_command}")" = codex ]'
check "a non-Claude lane's pane is named after the agent (processName)" "$(tmux display-message -p -t =t1-api: '#{pane_current_command}')" "codex"
tmux kill-session -t =t1-api 2> /dev/null
# The id watcher ends with the lane: a real hangup (the pty closed, as when a terminal pane is killed) must take the agent, the lane
# script and the watcher with it, and a watcher must not outlive its lane to hold the team start lock.
if command -v script > /dev/null 2>&1; then
  printf '#!/usr/bin/env bash\nexec -a codex %s 60\n' "$(sleeper codex)" > "$WORK/codex-quiet"; chmod +x "$WORK/codex-quiet"
  jq --arg c "$WORK/codex-quiet" '.agents.codex.bin = $c' "$LCFG" > "$WORK/l5.json"
  find "$AW_STATE_DIR/meta/lane-sessions" -mindepth 1 -delete 2> /dev/null
  pty_cmd="env AW_LAUNCHER_CONFIG=$WORK/l5.json AW_META_NO_SHELL=1 AW_META_ID_WAIT=20 AW_META_ID_GRACE=1 bash $DIR/meta-lane.sh t1 api"
  if script --version 2>&1 | grep -q util-linux; then script -qfec "$pty_cmd" /dev/null < /dev/null > /dev/null 2>&1 &   # util-linux
  else script -q /dev/null bash -c "$pty_cmd" < /dev/null > /dev/null 2>&1 & fi   # BSD (macOS)
  sp=$!
  waitfor '[ "$(pgrep -f "^bash $DIR/meta-lane.sh t1 api$" | wc -l | tr -d " ")" -ge 2 ] && pgrep -f "^codex 60$" > /dev/null'
  lane_pids="$(pgrep -f "^bash $DIR/meta-lane.sh t1 api$")"; agent_pid="$(pgrep -f '^codex 60$' | head -1)"
  check "a lane under a pty runs its script, its agent and an id watcher" "$([ "$(echo "$lane_pids" | wc -l | tr -d ' ')" -ge 2 ] && kill -0 "$agent_pid" 2> /dev/null && echo yes)" "yes"
  kill -KILL "$sp" 2> /dev/null; wait "$sp" 2> /dev/null
  waitfor '! pgrep -f "^bash $DIR/meta-lane.sh t1 api$" > /dev/null && ! kill -0 "$agent_pid" 2> /dev/null'
  check "after the pty closes the agent, the lane script and the id watcher are all gone" "$(pgrep -f "^bash $DIR/meta-lane.sh t1 api$" | wc -l | tr -d ' '):$(kill -0 "$agent_pid" 2> /dev/null && echo alive || echo gone)" "0:gone"
check "no snapshot file is left behind by the watcher" "$(ls -A "$AW_STATE_DIR/meta/lane-sessions" | grep -c '\.snap\.')" "0"
else
  echo "ok - skipped: script(1) is not installed (the pty hangup case)"
fi
# The npm install of Codex is a node wrapper that spawns the native binary: tmux names the pane "node". meta-lane.sh records the name
# the pane really shows in the .agent record, which is what aw-meta's wake guard compares.
cat > "$WORK/codex-npm" <<'EOF'
#!/usr/bin/env bash
if command -v node > /dev/null 2>&1; then exec node -e "require('child_process').spawnSync('sleep', ['60'], { stdio: 'inherit' })"; fi
exec -a node SLEEPER_NODE 60
EOF
sed -i.bak "s#SLEEPER_NODE#$(sleeper node)#" "$WORK/codex-npm"; rm "$WORK/codex-npm.bak"
chmod +x "$WORK/codex-npm"
jq --arg c "$WORK/codex-npm" '.agents.codex.bin = $c' "$LCFG" > "$WORK/l6.json"
find "$AW_STATE_DIR/meta/lane-sessions" -mindepth 1 -delete 2> /dev/null
tmux kill-server 2> /dev/null; tmux new-session -d -s keepalive "sleep 600"
tmux new-session -d -s t1-api "env AW_LAUNCHER_CONFIG=$WORK/l6.json AW_META_NO_SHELL=1 AW_META_ID_WAIT=1 AW_META_ID_GRACE=1 bash $DIR/meta-lane.sh t1 api"
waitfor '[ "$(jq -r .processName "$AW_STATE_DIR/meta/lane-sessions/api.agent" 2> /dev/null)" = node ]'
check "an npm-shaped agent (a node wrapper): the .agent record carries the name the pane shows" "$(jq -r .processName "$AW_STATE_DIR/meta/lane-sessions/api.agent"):$(tmux display-message -p -t =t1-api: '#{pane_current_command}')" "node:node"
tmux kill-session -t =t1-api 2> /dev/null
# no processName/transcripts surprises: the pane check uses the catalog's processName.
mkdir -p "$WORK/fakebin2"; cp "$SLEEPBIN" "$WORK/fakebin2/codex"
: > "$ids/api.exited"
tmux kill-server 2> /dev/null; tmux new-session -d -s keepalive "sleep 600"
tmux new-session -d -s t1-api "bash -c '$WORK/fakebin2/codex 60; true'"
waitfor 'pgrep -f "$WORK/fakebin2/codex 60" > /dev/null'
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --lanes api 2>&1 | head -2)"
check "a codex lane whose codex runs under a stale marker is left alone (processName from the catalog)" "$(grep -c 'tmux t1-api: running' <<< "$OUT")" "1"
jq '.agents.codex.processName = "other"' "$LCFG" > "$WORK/l3.json"
OUT="$(AW_LAUNCHER_CONFIG="$WORK/l3.json" bash "$DIR/meta-launch.sh" --no-open --dry-run --lanes api 2>&1)"
check "with another processName that pane counts as exited and is restarted" "$(grep -c 'respawn-pane' <<< "$OUT")" "1"
pkill -f "$WORK/fakebin2/codex 60" 2> /dev/null

# A refused prompt ({seed} starting with "-", spec section 4) starts no agent and records no id, whatever the agent.
killstubs() { pkill -f "$WORK/codex" 2> /dev/null; sleep 0.1; }
jq -n '{schema:1, viewer:{kind:"print"}}' > "$WORK/print.json"
for first in '- First: read the spec' '---'; do
  printf '%s\nbody\n' "$first" > "$WORK/prompts/refuse.md"; : > "$CALLS"; rm -f "$ids/refuse"
  OUT="$(AW_LAUNCHER_CONFIG="$WORK/print.json" bash "$DIR/meta-lane.sh" t1 refuse 2>&1)"; RC=$?
  check "a prompt starting with '$first' and a launcher.json: refused, no agent start, no recorded id" "$RC:$(grep -c 'SND-LAUNCH-011' <<< "$OUT"):$(wc -c < "$CALLS" | tr -d ' '):$(test -e "$ids/refuse" && echo id || echo none)" "1:1:0:none"
done
: > "$CALLS"; rm -f "$ids/refuse"; : > "$CALLS.codex"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-lane.sh" t1 refuse 2>&1)"; RC=$?
check "the same prompt with a codex lane: refused, no agent start" "$RC:$(wc -c < "$CALLS.codex" | tr -d ' ' | grep -c '^0$'):$(test -e "$ids/refuse" && echo id || echo none)" "1:1:none"
rm -f "$WORK/prompts/refuse.md"
# An existing but empty prompt file still passes "" as the last argument, as it did before launcher.json.
: > "$WORK/prompts/empty.md"; : > "$CALLS"
bash "$DIR/meta-lane.sh" t1 empty
check "an empty <lane>.md passes an empty prompt argument, as before" "$(sed -E 's/[0-9a-f]{8}-[0-9a-f-]{27}/ID/' "$CALLS")" "--session-id|ID|-n|t1-empty||"
rm -f "$WORK/prompts/empty.md"
# The three lane variables reach a Claude Code lane too.
cat > "$WORK/claude-aw" <<'EOF2'
#!/usr/bin/env bash
echo "$AW_SINDRI_CHILD|$AW_META_TEAM|$AW_META_LANE" > "$CALLS.aw"
EOF2
chmod +x "$WORK/claude-aw"
AW_META_CLAUDE="$WORK/claude-aw" bash "$DIR/meta-lane.sh" t1 awenv
check "a Claude Code lane sees AW_SINDRI_CHILD, AW_META_TEAM and AW_META_LANE" "$(cat "$CALLS.aw")" "1|t1|awenv"

# Reported ids: only session files created after the start count (an old rollout appended to meanwhile is not chosen).
codex_dir="$HOME/.codex/sessions/2026/10/14"; mkdir -p "$codex_dir"
old="$codex_dir/rollout-2026-10-14T00-00-00-11111111-1111-1111-1111-111111111111.jsonl"; : > "$old"
cat > "$WORK/codex-race" <<'EOF2'
#!/usr/bin/env bash
( sleep 0.4; echo more >> "$OLD_ROLLOUT" ) &
sleep 1
mkdir -p "$HOME/.codex/sessions/2026/10/15"
: > "$HOME/.codex/sessions/2026/10/15/rollout-2026-10-15T00-00-00-22222222-2222-2222-2222-222222222222.jsonl"
sleep 600
EOF2
chmod +x "$WORK/codex-race"; export OLD_ROLLOUT="$old"
jq -n --arg c "$WORK/codex-race" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$WORK/race.json"
rm -f "$ids/race" "$ids/race.settled"
AW_LAUNCHER_CONFIG="$WORK/race.json" AW_META_ID_WAIT=8 bash "$DIR/meta-lane.sh" t1 race > /dev/null 2>&1 &
rp=$!; waitfor '[ -e "$ids/race.settled" ]'
check "an existing rollout appended to during the wait is not chosen; the lane's own new one is" "$(tr -d '[:space:]' < "$ids/race" 2> /dev/null)" "22222222-2222-2222-2222-222222222222"
kill "$rp" 2> /dev/null; killstubs; wait "$rp" 2> /dev/null

# Two new session files at once are refused: no id, a .ambiguous marker, and the launcher says so.
cat > "$WORK/codex-two" <<'EOF2'
#!/usr/bin/env bash
mkdir -p "$HOME/.codex/sessions/2026/10/16"
: > "$HOME/.codex/sessions/2026/10/16/rollout-2026-10-16T00-00-00-33333333-3333-3333-3333-333333333333.jsonl"
: > "$HOME/.codex/sessions/2026/10/16/rollout-2026-10-16T00-00-01-44444444-4444-4444-4444-444444444444.jsonl"
sleep 600
EOF2
chmod +x "$WORK/codex-two"
jq -n --arg c "$WORK/codex-two" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$WORK/two.json"
jq -n --arg r "$WORK/repo" '{team:"t1", repo:$r, metaLane:"meta", lanes:["amb","idle"]}' > "$WORK/team2.json"
tmux kill-server 2> /dev/null; sleep 0.3; tmux new-session -d -s keepalive "sleep 600"; rm -f "$ids/amb" "$ids/amb.ambiguous" "$ids/amb.settled"
OUT="$(AW_META_CONFIG="$WORK/team2.json" AW_LAUNCHER_CONFIG="$WORK/two.json" AW_META_ID_WAIT=5 bash "$DIR/meta-launch.sh" --no-open --lanes amb 2>&1)"; RC=$?
check "two unclaimed new session files: no id recorded, .ambiguous left" "$(test -s "$ids/amb" && echo id || echo none):$(test -e "$ids/amb.ambiguous" && echo marker || echo none)" "none:marker"
check "the launcher reports the ambiguous id and still exits 0" "$RC:$(grep -c 'lane amb: ambiguous session id' <<< "$OUT")" "0:1"
killstubs

# An agent that never reports: the launcher times out with a message and the next lane still starts.
cat > "$WORK/codex-idle" <<'EOF2'
#!/usr/bin/env bash
sleep 600
EOF2
chmod +x "$WORK/codex-idle"
jq -n --arg c "$WORK/codex-idle" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$WORK/idle.json"
rm -f "$ids/idle" "$ids/idle.settled" "$ids/idle.ambiguous"
OUT="$(AW_META_CONFIG="$WORK/team2.json" AW_LAUNCHER_CONFIG="$WORK/idle.json" AW_META_ID_WAIT=1 bash "$DIR/meta-launch.sh" --no-open --lanes idle,amb 2>&1)"; RC=$?
check "an id that never arrives: the timeout message, exit 0, the next lane started" "$RC:$(grep -c 'lane idle has not reported its session id after 1s' <<< "$OUT"):$(grep -c 'tmux t1-amb: started' <<< "$OUT")" "0:1:1"
check "an id that never arrives: nothing recorded" "$(test -s "$ids/idle" && echo id || echo none)" "none"
tmux kill-server 2> /dev/null; killstubs

# Three reported lanes started at the same moment (as Warp's panes do) still start one at a time.
reset; : > "$CALLS.events"; : > "$CALLS.codex"; rm -f "$ids/p1" "$ids/p2" "$ids/p3" "$ids/p1.settled" "$ids/p2.settled" "$ids/p3.settled"
export -n OLD_ROLLOUT
# a lock left by a crashed start (its owner pid is dead) is no obstacle: the next start takes it over
( sleep 0 & dead=$!; wait "$dead"; ln -s "$dead" "$ids/.reported.lock" )
AW_LAUNCHER_CONFIG="$LCFG" AW_META_ID_WAIT=10 bash "$DIR/meta-lane.sh" t1 p1 > /dev/null 2>&1 & p1=$!
AW_LAUNCHER_CONFIG="$LCFG" AW_META_ID_WAIT=10 bash "$DIR/meta-lane.sh" t1 p2 > /dev/null 2>&1 & p2=$!
AW_LAUNCHER_CONFIG="$LCFG" AW_META_ID_WAIT=10 bash "$DIR/meta-lane.sh" t1 p3 > /dev/null 2>&1 & p3=$!
waitfor '[ -s "$ids/p1" ] && [ -s "$ids/p2" ] && [ -s "$ids/p3" ]'
a1="$(tr -d '[:space:]' < "$ids/p1")"; a2="$(tr -d '[:space:]' < "$ids/p2")"; a3="$(tr -d '[:space:]' < "$ids/p3")"
check "three lanes starting at once: one at a time (each id recorded before the next agent starts)" "$(awk '{print $1}' "$CALLS.events" | tr '\n' ';')" "start;wrote;start;wrote;start;wrote;"
check "three lanes starting at once: each lane recorded its own rollout's id" "$(grep -c "wrote p1 $a1" "$CALLS.events"):$(grep -c "wrote p2 $a2" "$CALLS.events"):$(grep -c "wrote p3 $a3" "$CALLS.events"):$(printf '%s\n' "$a1" "$a2" "$a3" | sort -u | wc -l | tr -d ' ')" "1:1:1:3"
kill "$p1" "$p2" "$p3" 2> /dev/null; killstubs; wait 2> /dev/null
check "the start lock is released afterwards" "$(test -e "$ids/.reported.lock" -o -L "$ids/.reported.lock" && echo held || echo free)" "free"

# A lock held by a live process is waited for: the lane starts anyway once AW_META_ID_WAIT x 10 s have passed, and says so.
reset; : > "$CALLS.events"; : > "$CALLS.codex"; rm -f "$ids/p1" "$ids/p1.settled"
sleep 60 & holder=$!
ln -s "$holder" "$ids/.reported.lock"
t0=$SECONDS
AW_LAUNCHER_CONFIG="$LCFG" AW_META_ID_WAIT=1 bash "$DIR/meta-lane.sh" t1 p1 > "$WORK/held.out" 2>&1 & p1=$!
for _ in $(seq 250); do grep -q "starting p1 anyway" "$WORK/held.out" && break; sleep 0.1; done
check "a start lock held by a live process: the lane waits about AW_META_ID_WAIT x 10 s, then starts anyway" "$(grep -c "another lane's start did not finish; starting p1 anyway" "$WORK/held.out"):$([ $((SECONDS - t0)) -ge 9 ] && echo waited || echo "early $((SECONDS - t0))s")" "1:waited"
check "the live holder's lock is left alone" "$(readlink "$ids/.reported.lock")" "$holder"
kill "$p1" "$holder" 2> /dev/null; killstubs; wait 2> /dev/null; unlink "$ids/.reported.lock" 2> /dev/null

# The start lock's takeover of a dead owner: no gap lets a second lane in, and an owner that is not a positive integer is stale.
# meta-start-lock.sh is sourced in a subshell per taker; its test seam (start_lock_hook) plays the other lanes at the exact
# points where they could interleave.
mkdir -p "$WORK/lk"; lk="$WORK/lk/.reported.lock"
dead="$(sh -c 'echo $$')"   # a pid that has exited
sleep 60 & live_b=$!
sleep 60 & live_c=$!
run_acquire() { # <lane_pid> [hook body]: acquire with no wait at all, prints the exit status
  ( start_lock="$lk"; lane_pid="$1"; lane=t; AW_META_ID_WAIT=0
    . "$DIR/meta-start-lock.sh"
    [ -z "${2:-}" ] || eval "start_lock_hook() { $2; }"
    acquire_start_lock 2> /dev/null; echo $? )
}
reset_lock() { unlink "$lk" 2> /dev/null; rmdir "$lk.takeover" 2> /dev/null; rm -f "$WORK/lk/c"; }
# two takers of a dead lock: the other one finishes its takeover between our check and ours, and a newcomer tries the gap
reset_lock; ln -s "$dead" "$lk"
rc="$(run_acquire 4242 'case "$1" in checked) unlink "$start_lock"; ln -s '"$live_b"' "$start_lock" ;; gap) ln -s '"$live_c"' "$start_lock" && : > '"$WORK/lk/c"' ;; esac')"
check "takeover: a taker that lost the race leaves the other's live lock alone, and no newcomer gets in" "$rc:$(readlink "$lk"):$(test -e "$WORK/lk/c" && echo newcomer || echo none)" "1:$live_b:none"
# a newcomer in the gap of a real takeover: exactly one of the two holds the lock
reset_lock; ln -s "$dead" "$lk"
rc="$(run_acquire 4242 'case "$1" in gap) ln -s '"$live_c"' "$start_lock" && : > '"$WORK/lk/c"' ;; esac')"
owner="$(readlink "$lk")"
if [ -e "$WORK/lk/c" ]; then want="1:$live_c"; else want="0:4242"; fi
check "takeover: a newcomer in the gap and the taker never both hold the lock" "$rc:$owner" "$want"
# six lanes taking over one dead lock together: never two in the critical section
reset_lock; ln -s "$dead" "$lk"; rm -f "$WORK/lk/violations" "$WORK/lk/entered"; rmdir "$WORK/lk/crit" 2> /dev/null
for _ in 1 2 3 4 5 6; do
  ( start_lock="$lk"; lane_pid="$(sh -c 'echo $PPID')"; lane=t; AW_META_ID_WAIT=5
    . "$DIR/meta-start-lock.sh"
    acquire_start_lock 2> /dev/null || exit 0
    mkdir "$WORK/lk/crit" 2> /dev/null || echo v >> "$WORK/lk/violations"
    echo e >> "$WORK/lk/entered"; sleep 0.3; rmdir "$WORK/lk/crit" 2> /dev/null; release_start_lock ) &
done
wait $(jobs -p | grep -v -e "^$live_b\$" -e "^$live_c\$") 2> /dev/null
check "takeover: six lanes take a dead lock over one at a time" "$(wc -l < "$WORK/lk/entered" | tr -d ' '):$(test -e "$WORK/lk/violations" && echo overlap || echo none)" "6:none"
# an owner that is not a positive integer (0 signals the process group, -1 every process) is stale, never a live holder
for bad in 0 -1 not-a-pid 12x; do
  reset_lock; ln -s -- "$bad" "$lk"
  rc="$(run_acquire 4242)"
  check "takeover: an owner of '$bad' is stale and taken over" "$rc:$(readlink "$lk")" "0:4242"
done
reset_lock; kill "$live_b" "$live_c" 2> /dev/null; wait "$live_b" "$live_c" 2> /dev/null

# The launcher scripts run on macOS's bash 3.2 and BSD tools: no flock, fd-number redirections, BASHPID, mapfile or associative arrays.
check "the launcher scripts use no flock, exec {fd}, BASHPID, mapfile, readarray or declare -A" "$(grep -n -E 'flock|exec \{[a-z_]+\}|BASHPID|mapfile|readarray|declare -A|\$\{[A-Za-z_]+(,,|\^\^)\}' "$DIR/meta-lane.sh" "$DIR/meta-start-lock.sh" "$DIR/meta-launch.sh" "$DIR/launcher-lib.sh" "$DIR/meta-team-lib.sh" | grep -v -E '^[^:]+:[0-9]+:[[:space:]]*#' | wc -l | tr -d ' ')" "0"

# Precedence: AW_META_CLAUDE ranks above the file's claude-code bin; without it the file's bin is used.
jq -n '{schema:1, agents:{"claude-code":{override:true, bin:"/nonexistent/claude"}}}' > "$LCFG"
: > "$CALLS"; rm -f "$ids/api" "$ids/ui"
AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-lane.sh" t1 ui
check "AW_META_CLAUDE wins over the file's claude-code bin" "$(cut -d'|' -f3 "$CALLS")" "-n"
jq -n --arg c "$WORK/claude" '{schema:1, agents:{"claude-code":{override:true, bin:$c}}}' > "$LCFG"
: > "$CALLS"; rm -f "$ids/ui"
( unset AW_META_CLAUDE; AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-lane.sh" t1 ui )
check "without AW_META_CLAUDE the file's bin is the command" "$(cut -d'|' -f3,4 "$CALLS")" "-n|t1-ui"
jq -n --arg c "$WORK/claude" '{schema:1, agents:{"claude-code":{override:true, open:["{bin}","--sid","{sessionId}","--label={name}","{seed}"]}}}' > "$LCFG"
: > "$CALLS"; rm -f "$ids/api"
AW_META_CLAUDE_ARGS="--model x" AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-lane.sh" t1 api
check "an override's open template is used; AW_META_CLAUDE_ARGS go right after {bin}" "$(sed -E 's/[0-9a-f]{8}-[0-9a-f-]{27}/ID/' "$CALLS" | head -1)" "--model|x|--sid|ID|--label=t1-api|API kickoff|"

# --init-launcher: the detection under a PATH holding only the named terminals.
tools="jq mkdir mv chmod cat dirname readlink uname tr head tail grep sed awk date rm printf env bash sh mktemp cp ls basename wc sort"
kd="$WORK/kitty-only"; mkdir -p "$kd"; for t in $tools; do p="$(command -v "$t" 2> /dev/null)" && ln -sf "$p" "$kd/$t"; done
unlink "$kd/uname"; printf '#!/bin/sh\necho Linux\n' > "$kd/uname"; chmod +x "$kd/uname"   # the detection is the same on every host
printf '#!/bin/sh\nexit 0\n' > "$kd/kitty"; chmod +x "$kd/kitty"
INIT="$WORK/init.json"
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher 2>&1)"; RC=$?
check "init-launcher with only kitty on PATH and a display: writes the kitty template" "$RC:$(jq -c '[.viewer.kind, .viewer.name, .viewer.argv[0]]' "$INIT")" '0:["exec-template","kitty","kitty"]'
check "init-launcher: the file is 0600 and passes the reader" "$(stat -c %a "$INIT" 2> /dev/null || stat -f %Lp "$INIT"):$( ( . "$DIR/launcher-lib.sh"; launcher_validate "$INIT" > /dev/null && echo ok ) )" "600:ok"
check "init-launcher says what it chose and why" "$(grep -c 'Wrote .*init.json: kitty (first on PATH)' <<< "$OUT")" "1"
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher 2>&1)"; RC=$?
check "init-launcher refuses to replace a file without --force" "$RC" "1"
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --viewer print --force 2>&1)"; RC=$?
check "init-launcher --viewer print --force replaces it" "$RC:$(jq -c .viewer "$INIT")" '0:{"kind":"print"}'
unlink "$INIT"
OUT="$(env -i HOME="$HOME" PATH="$kd" AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --stdout 2>&1)"
check "init-launcher with no GUI session picks print; --stdout writes no file" "$(jq -c .viewer <<< "$OUT"):$(test -e "$INIT" && echo file || echo none)" '{"kind":"print"}:none'
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 TERM_PROGRAM=kitty AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher 2>&1)"
check "init-launcher names \$TERM_PROGRAM when it picks by it" "$(grep -c 'kitty (from \$TERM_PROGRAM)' <<< "$OUT")" "1"
unlink "$INIT"
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --viewer nonesuch 2>&1)"; RC=$?
check "init-launcher with an unknown --viewer: exit 2" "$RC" "2"
mkdir -p "$WORK/warpbin"; for t in $tools; do p="$(command -v "$t" 2> /dev/null)" && ln -sf "$p" "$WORK/warpbin/$t"; done
unlink "$WORK/warpbin/uname"; printf '#!/bin/sh\necho Linux\n' > "$WORK/warpbin/uname"; chmod +x "$WORK/warpbin/uname"
printf '#!/bin/sh\nexit 0\n' > "$WORK/warpbin/warp-terminal"; chmod +x "$WORK/warpbin/warp-terminal"
OUT="$(env -i HOME="$HOME" PATH="$WORK/warpbin" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --stdout 2>&1)"
check "init-launcher picks Warp when it is installed" "$(jq -c .viewer <<< "$OUT")" '{"kind":"warp"}'

# ---- run-2 fixes (PR 261) ----
# 1. Thousands of old session files do not make the snapshot check slow.
many="$HOME/.codex/sessions/2026/09/01"; mkdir -p "$many"
( cd "$many" && seq 1 2000 | awk '{ printf "rollout-2026-09-01T00-00-00-%08d-0000-0000-0000-000000000000.jsonl\n", $1 }' | xargs touch )
cat > "$WORK/codex-new" <<'EOF2'
#!/usr/bin/env bash
sleep 1
mkdir -p "$HOME/.codex/sessions/2026/10/17"
: > "$HOME/.codex/sessions/2026/10/17/rollout-2026-10-17T00-00-00-55555555-5555-5555-5555-555555555555.jsonl"
sleep 600
EOF2
chmod +x "$WORK/codex-new"
jq -n --arg c "$WORK/codex-new" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$WORK/new.json"
rm -f "$ids/many" "$ids/many.settled"; rm -rf "$HOME/.codex/sessions/2026/10/17"
t0=$SECONDS
AW_LAUNCHER_CONFIG="$WORK/new.json" AW_META_ID_WAIT=25 bash "$DIR/meta-lane.sh" t1 many > /dev/null 2>&1 &
rp=$!; waitfor '[ -e "$ids/many.settled" ]'
check "2000 old session files: the new one is recorded, well inside the wait" "$(tr -d '[:space:]' < "$ids/many" 2> /dev/null):$([ $((SECONDS - t0)) -lt 6 ] && echo fast || echo "slow $((SECONDS - t0))s")" "55555555-5555-5555-5555-555555555555:fast"
kill "$rp" 2> /dev/null; killstubs; wait "$rp" 2> /dev/null
rm -rf "$HOME/.codex/sessions/2026/09" "$HOME/.codex/sessions/2026/10/17"

# 2. A lane whose file shows up after the timeout keeps its id: the next lane does not take it.
cat > "$WORK/codex-late" <<'EOF2'
#!/usr/bin/env bash
case "$AW_META_LANE" in la) sleep 3.5 ;; *) sleep 1.8 ;; esac
id="$(cat /proc/sys/kernel/random/uuid 2> /dev/null || uuidgen | tr '[:upper:]' '[:lower:]')"
mkdir -p "$HOME/.codex/sessions/2026/10/18"
: > "$HOME/.codex/sessions/2026/10/18/rollout-2026-10-18T00-00-00-$id.jsonl"
echo "$AW_META_LANE $id" >> "$CALLS.late"
sleep 600
EOF2
chmod +x "$WORK/codex-late"
jq -n --arg c "$WORK/codex-late" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$WORK/late.json"
jq -n --arg r "$WORK/repo" '{team:"t1", repo:$r, metaLane:"meta", lanes:["la","lb"]}' > "$WORK/team3.json"
reset; tmux set-environment -g AW_META_ID_WAIT 2; tmux set-environment -g AW_META_ID_GRACE 6   # the lanes run under the tmux server, not this shell
: > "$CALLS.late"; rm -f "$ids"/la* "$ids"/lb*; rm -rf "$HOME/.codex/sessions/2026/10/18"
OUT="$(AW_META_CONFIG="$WORK/team3.json" AW_LAUNCHER_CONFIG="$WORK/late.json" AW_META_ID_WAIT=2 AW_META_ID_GRACE=6 bash "$DIR/meta-launch.sh" --no-open --lanes la,lb 2>&1)"
waitfor '[ -s "$ids/la" ] && [ -s "$ids/lb" ]'
la="$(tr -d '[:space:]' < "$ids/la" 2> /dev/null)"; lb="$(tr -d '[:space:]' < "$ids/lb" 2> /dev/null)"
check "lane la's file arrives after the timeout: la records it, lb records its own" "$(grep -c "^la $la\$" "$CALLS.late"):$(grep -c "^lb $lb\$" "$CALLS.late"):$([ -n "$la" ] && [ "$la" != "$lb" ] && echo distinct)" "1:1:distinct"
killstubs; tmux kill-server 2> /dev/null; rm -rf "$HOME/.codex/sessions/2026/10/18"

# 3. A reported lane that exits before its watcher starts: the launcher shows the real error at once.
printf -- '- First: read the spec\nbody\n' > "$WORK/prompts/rf.md"
jq -n --arg r "$WORK/repo" --arg p "$WORK/prompts" '{team:"t1", repo:$r, prompts:$p, metaLane:"meta", lanes:["rf"]}' > "$WORK/team4.json"
reset; rm -f "$ids"/rf*; t0=$SECONDS
OUT="$(AW_META_CONFIG="$WORK/team4.json" AW_LAUNCHER_CONFIG="$WORK/new.json" AW_META_ID_WAIT=8 bash "$DIR/meta-launch.sh" --no-open --lanes rf 2>&1)"; RC=$?
check "a refused reported lane: the launcher prints SND-LAUNCH-011 at once, not the timeout" "$RC:$(grep -c 'lane rf failed to start: SND-LAUNCH-011' <<< "$OUT"):$(grep -c 'has not reported' <<< "$OUT"):$([ $((SECONDS - t0)) -lt 5 ] && echo fast || echo slow)" "0:1:0:fast"
tmux kill-server 2> /dev/null; rm -f "$WORK/prompts/rf.md"
tmux kill-server 2> /dev/null
exit $fail
