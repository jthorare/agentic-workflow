#!/usr/bin/env bash
# Tests for the default-terminal viewer of scripts/meta-launch.sh (#4516): one window per lane in the user's own terminal.
# macOS: one executable .command file per lane in a private dir, opened with `open`. Linux: xdg-terminal-exec, else
# x-terminal-emulator -e, else a refusal that names both. No real terminal opens: fakes first on PATH record their argv.
# The launcher runs on a private tmux server (scripts/test-tmux.sh). The OS is a fake `uname` first on PATH.
# Run: bash scripts/tests/meta-launch-default-terminal.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_ROOT="$(cd "$DIR/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
command -v tmux > /dev/null || { echo "ok - skipped: tmux not installed"; exit 0; }
command -v jq > /dev/null || { echo "ok - skipped: jq not installed"; exit 0; }
AW_TEST_TMUX=t1
. "$DIR/test-tmux.sh" || exit 2
# shellcheck source=scripts/lib/ensure-scripts-deps.sh
. "$REPO_ROOT/scripts/lib/ensure-scripts-deps.sh"

WORK="$(mktemp -d)"
trap 'ttmux_cleanup; rm -rf "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done
unset AW_LAUNCHER_CONFIG
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_WARP_LAUNCH_DIR="$WORK/warp" TMPDIR="$WORK/tmp"
export AW_META_NO_OOM_CHECK=1 AW_META_NO_STALE_CHECK=1 AW_META_NO_SHELL=1 AW_META_LEGACY_TMUX_SOCKET=t1-legacy
REPO="$WORK/my repo's dir"   # a space and a single quote: the .command file has to carry it byte for byte
mkdir -p "$HOME" "$TMPDIR" "$AW_STATE_DIR/meta" "$REPO" "$WORK/prompts"
printf '#!/usr/bin/env bash\nexec sleep 600\n' > "$WORK/claude"; chmod +x "$WORK/claude"
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/aw-meta"; chmod +x "$WORK/aw-meta"
export AW_META_CLAUDE="$WORK/claude" AW_META_BIN="$WORK/aw-meta"
jq -n --arg r "$REPO" --arg p "$WORK/prompts" '{team:"t1", repo:$r, prompts:$p, metaLane:"meta", lanes:["api","ui"]}' > "$AW_STATE_DIR/meta/team.json"
printf '{"schema":1,"viewer":{"kind":"default-terminal"}}\n' > "$AW_STATE_DIR/launcher.json"

mode() { stat -c %a "$1" 2> /dev/null || stat -f %Lp "$1"; }   # GNU first, BSD (macOS) second

# FARM: every executable on PATH except the terminals under test, so a box that has xdg-terminal-exec or x-terminal-emulator
# installed does not change the "only one of them" and "neither" cases.
FARM="$WORK/farm"; mkdir -p "$FARM"
IFS=: read -r -a _dirs <<< "$PATH"
for _d in "${_dirs[@]}"; do
  [ -d "$_d" ] || continue
  for _f in "$_d"/*; do
    [ -e "$_f" ] || continue
    case "${_f##*/}" in xdg-terminal-exec|x-terminal-emulator|warp-terminal|open|xdg-open) continue ;; esac
    [ -e "$FARM/${_f##*/}" ] || ln -s "$_f" "$FARM/${_f##*/}" 2> /dev/null
  done
done

# fake_dir <name> <os> [opener...]: a dir with a fake uname and the named fake openers. Every opener records its argv and cwd.
fake_dir() {
  local d="$WORK/fakes-$1" os="$2" o; shift 2
  mkdir -p "$d"
  printf '#!/bin/sh\n[ "$1" = -s ] && echo %s || echo %s\n' "$os" "$os" > "$d/uname"
  for o in "$@"; do
    case "$o" in
      open)   # copies the file before the launcher can remove it, records its mode and dir; FAKE_OPEN_RUN=1 runs it like a terminal would
        cat > "$d/open" <<EOF
#!/bin/sh
echo "\$1" >> "$WORK/open.args"
mkdir -p "$WORK/seen"
cp "\$1" "$WORK/seen/\$(basename "\$1")"
stat -c %a "\$1" > "$WORK/seen/\$(basename "\$1").mode" 2> /dev/null || stat -f %Lp "\$1" > "$WORK/seen/\$(basename "\$1").mode"
d="\$(dirname "\$1")"; echo "\$d" > "$WORK/seen/dir"
stat -c %a "\$d" > "$WORK/seen/dir.mode" 2> /dev/null || stat -f %Lp "\$d" > "$WORK/seen/dir.mode"
if [ -n "\${FAKE_OPEN_RUN:-}" ]; then ( cd / && "\$1" > /dev/null 2>&1 < /dev/null ); [ -e "\$1" ] && echo kept || echo removed; fi >> "$WORK/seen/run.log"
exit 0
EOF
        ;;
      *)
        cat > "$d/$o" <<EOF
#!/bin/sh
{ printf '%s|' "\$@"; echo; pwd; } >> "$WORK/$o.calls"
EOF
        ;;
    esac
    chmod +x "$d/$o"
  done
  chmod +x "$d/uname"
}
fake_dir mac Darwin open
fake_dir lin-xdg Linux xdg-terminal-exec x-terminal-emulator
fake_dir lin-x11 Linux x-terminal-emulator
fake_dir lin-none Linux

# launch <fakes dir> [args]: the launcher with only that fakes dir ahead of the farm; sets OUT and RC. A fresh server each time.
launch() {
  local f="$1"; shift
  ttmux kill-server 2> /dev/null; sleep 0.2
  ttmux new-session -d -s keepalive "sleep 600"
  rm -rf "$WORK/seen" "$WORK/open.args" "$WORK"/*.calls
  OUT="$(env PATH="$f:$AW_TEST_TMUX_DIR/bin:$FARM" bash "$DIR/meta-launch.sh" "$@" 2>&1)"; RC=$?
}
TMUX_BIN="$(PATH="$AW_TEST_TMUX_DIR/bin:$FARM" command -v tmux)"

# ---- macOS: one .command per lane ----
export AW_META_COMMAND_GRACE=1   # the fake open only copies the file, so it stays; the launcher stops waiting for it after this many seconds
launch "$WORK/fakes-mac"
check "macOS: exit 0" "$RC" "0"
check "macOS: open ran once per lane, on a .command file" "$(cat "$WORK/open.args" 2> /dev/null | wc -l | tr -d ' '):$(grep -c '\.command$' "$WORK/open.args" 2> /dev/null)" "3:3"
check "macOS: the files are named for the lanes" "$(sed 's#.*/##' "$WORK/open.args" 2> /dev/null | sort | tr '\n' ' ')" "t1-api.command t1-meta.command t1-ui.command "
check "macOS: each file is mode 700" "$(cat "$WORK"/seen/*.command.mode 2> /dev/null | sort -u | tr '\n' ' ')" "700 "
check "macOS: the private dir is mode 700" "$(cat "$WORK/seen/dir.mode" 2> /dev/null)" "700"
want="$(printf '#!/bin/sh\nrm -f -- "$0"\ncd %s || exit 1\nexec %s -L t1 new -A -s t1-api\n' "'$WORK/my repo'\\''s dir'" "$TMUX_BIN")"
check "macOS: exact content of the api file (the cwd has a space and a quote)" "$(cat "$WORK/seen/t1-api.command" 2> /dev/null)" "$want"
check "macOS: the cd line puts a shell in the repo" "$(sh -c "$(grep '^cd ' "$WORK/seen/t1-api.command" 2> /dev/null | sed 's/ || exit 1$//'); pwd" 2> /dev/null)" "$REPO"
check "macOS: the private dir is gone after the run" "$([ -n "$(cat "$WORK/seen/dir" 2> /dev/null)" ] && [ ! -e "$(cat "$WORK/seen/dir")" ] && echo gone || echo present)" "gone"
check "macOS: no Warp YAML written" "$(test -e "$AW_WARP_LAUNCH_DIR/t1-team.yaml" && echo yes || echo no)" "no"

# the file removes itself as its first line, so a terminal that reads it late has still read it
FAKE_OPEN_RUN=1 launch "$WORK/fakes-mac" --lanes api
check "macOS: a run file removes itself before it execs" "$(cat "$WORK/seen/run.log" 2> /dev/null | sort -u | tr '\n' ' ')" "removed "
launch "$WORK/fakes-mac" --lanes api --no-open
check "macOS --no-open: nothing opens and no dir is left" "$(test -e "$WORK/open.args" && echo opened || echo none):$(ls "$TMPDIR" | wc -l | tr -d ' ')" "none:0"
launch "$WORK/fakes-mac" --lanes api --dry-run
check "macOS --dry-run: names the lane's file, opens nothing" "$(test -e "$WORK/open.args" && echo opened || echo none):$(grep -c '^\[dry-run\] default-terminal: .*t1-api' <<< "$OUT")" "none:1"

# ---- Linux ----
launch "$WORK/fakes-lin-xdg"
check "Linux: xdg-terminal-exec is used when both exist" "$RC:$(ls "$WORK"/*.calls 2> /dev/null | sed 's#.*/##' | sort -u | tr '\n' ' ')" "0:xdg-terminal-exec.calls "
sleep 0.5
check "Linux: xdg-terminal-exec gets the attach argv, once per lane" "$(grep -c '^tmux|-L|t1|new|-A|-s|t1-' "$WORK/xdg-terminal-exec.calls" 2> /dev/null)" "3"
check "Linux: the api window's exact argv" "$(grep '^tmux|-L|t1|new|-A|-s|t1-api|$' "$WORK/xdg-terminal-exec.calls" 2> /dev/null)" "tmux|-L|t1|new|-A|-s|t1-api|"
check "Linux: the window starts in the repo" "$(grep -A1 't1-api|$' "$WORK/xdg-terminal-exec.calls" 2> /dev/null | tail -1)" "$REPO"
launch "$WORK/fakes-lin-x11"; sleep 0.5
check "Linux: only x-terminal-emulator: it runs with -e and the attach argv" "$RC:$(grep '^-e|tmux|-L|t1|new|-A|-s|t1-ui|$' "$WORK/x-terminal-emulator.calls" 2> /dev/null)" "0:-e|tmux|-L|t1|new|-A|-s|t1-ui|"
launch "$WORK/fakes-lin-none"
check "Linux: neither terminal: exit 1, the error names both" "$RC:$(grep -c 'xdg-terminal-exec.*x-terminal-emulator' <<< "$OUT")" "1:1"
check "Linux: the refusal comes before any tmux session is made" "$(ttmux has-session -t =t1-api 2> /dev/null && echo made || echo none)" "none"
launch "$WORK/fakes-lin-xdg" --lanes api --no-open
check "Linux --no-open: no terminal is run" "$(ls "$WORK"/*.calls 2> /dev/null | wc -l | tr -d ' ')" "0"

# ---- --viewer and --init-launcher ----
INIT="$WORK/init.json"
init() { env -i HOME="$HOME" PATH="$1:$FARM" AW_LAUNCHER_CONFIG="$INIT" AW_WARP_APP="$WORK/NoWarp.app" "${@:2}" 2>&1 | jq -c .viewer 2> /dev/null; true; }
check "--init-launcher --viewer default-terminal forces it" "$(init "$WORK/fakes-lin-xdg" DISPLAY=:0 bash "$DIR/meta-launch.sh" --init-launcher --stdout --viewer default-terminal)" '{"kind":"default-terminal"}'
check "--init-launcher on macOS without Warp picks default-terminal" "$(init "$WORK/fakes-mac" bash "$DIR/meta-launch.sh" --init-launcher --stdout)" '{"kind":"default-terminal"}'
rm -f "$INIT"
env -i HOME="$HOME" PATH="$WORK/fakes-lin-xdg:$FARM" AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --viewer default-terminal > /dev/null 2>&1
check "--init-launcher --viewer default-terminal writes a file the reader accepts" "$(jq -c .viewer "$INIT" 2> /dev/null):$( ( . "$DIR/launcher-lib.sh"; launcher_validate "$INIT" > /dev/null && echo ok ) )" '{"kind":"default-terminal"}:ok'

# ---- the schema and the bash reader agree: no name, no argv ----
mkdir -p "$WORK/fx"
jq -n '{schema:1, viewer:{kind:"default-terminal"}}' > "$WORK/fx/ok.json"
jq -n '{schema:1, viewer:{kind:"default-terminal", name:"x"}}' > "$WORK/fx/name.json"
jq -n '{schema:1, viewer:{kind:"default-terminal", argv:["x"]}}' > "$WORK/fx/argv.json"
bashv() { ( . "$DIR/launcher-lib.sh"; launcher_validate "$1" > /dev/null && echo accept || echo refuse ); }
check "bash reader: default-terminal alone is accepted; with name or argv it is refused" "$(bashv "$WORK/fx/ok.json") $(bashv "$WORK/fx/name.json") $(bashv "$WORK/fx/argv.json")" "accept refuse refuse"
if ensure_scripts_deps "$REPO_ROOT/scripts"; then
  ajvv() { ( cd "$REPO_ROOT/scripts" && node --input-type=module -e '
      import fs from "node:fs"; import { createRequire } from "node:module";
      const Ajv2020 = createRequire(process.cwd() + "/")("ajv/dist/2020").default;
      const schema = JSON.parse(fs.readFileSync("../schemas/launcher.schema.json", "utf8"));
      const v = new Ajv2020({ strict: false, allErrors: true }).compile(schema);
      console.log(v(JSON.parse(fs.readFileSync(process.argv[1], "utf8"))) ? "valid" : "invalid");' "$1" ); }
  check "schema: default-terminal alone is valid; with name or argv it is invalid" "$(ajvv "$WORK/fx/ok.json") $(ajvv "$WORK/fx/name.json") $(ajvv "$WORK/fx/argv.json")" "valid invalid invalid"
  check "check-launcher still passes with the new kind" "$(node "$DIR/check-launcher.mjs" 2>&1 | tail -1)" "check-launcher: ok"
else echo "not ok - Ajv could not be installed"; fail=1; fi
exit $fail
