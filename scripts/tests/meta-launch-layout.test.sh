#!/usr/bin/env bash
# Tests for the neutral team layout of scripts/meta-launch.sh (#4517): one layout document (lanes in order, the first one left,
# the session, attach and join argv and the pane's shell line per lane), printed by --print-layout and valid against
# $defs/layout of schemas/launcher.schema.json, and every viewer adapter (warp, print, exec-template, default-terminal) reading
# its lanes and commands from that document. No real terminal opens: fakes first on PATH record their argv. The launcher runs on
# a private tmux server (scripts/test-tmux.sh). Run: bash scripts/tests/meta-launch-layout.test.sh
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
REPO="$WORK/repo"
mkdir -p "$HOME" "$TMPDIR" "$AW_STATE_DIR/meta" "$REPO" "$WORK/prompts" "$WORK/fakes"
printf '#!/usr/bin/env bash\nexec sleep 600\n' > "$WORK/claude"; chmod +x "$WORK/claude"
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/aw-meta"; chmod +x "$WORK/aw-meta"
export AW_META_CLAUDE="$WORK/claude" AW_META_BIN="$WORK/aw-meta"
jq -n --arg r "$REPO" --arg p "$WORK/prompts" '{team:"t1", repo:$r, prompts:$p, metaLane:"meta", lanes:["api","ui"]}' > "$AW_STATE_DIR/meta/team.json"
LCFG="$WORK/launcher.json"; export AW_LAUNCHER_CONFIG="$LCFG"
printf '#!/bin/sh\n[ "$1" = -s ] && echo Linux || echo Linux\n' > "$WORK/fakes/uname"
for o in rec xdg-terminal-exec; do printf '#!/bin/sh\n{ printf "%%s|" "$@"; echo; } >> "%s/%s.calls"\n' "$WORK" "$o" > "$WORK/fakes/$o"; done
chmod +x "$WORK/fakes"/*
P="$WORK/fakes:$AW_TEST_TMUX_DIR/bin:$PATH"

# launch <viewer json> [args]: the launcher with that launcher.json; sets OUT and RC. A fresh server each time.
launch() {
  printf '%s\n' "$1" > "$LCFG"; shift
  ttmux kill-server 2> /dev/null; sleep 0.2
  ttmux new-session -d -s keepalive "sleep 600"
  rm -f "$WORK"/*.calls
  OUT="$(env PATH="$P" bash "$DIR/meta-launch.sh" "$@" 2>&1)"; RC=$?
}
WARP='{"schema":1,"viewer":{"kind":"warp"}}'

# ---- --print-layout: the document, nothing started ----
launch "$WARP" --print-layout
check "--print-layout: exit 0" "$RC" "0"
LAYOUT="$OUT"
check "--print-layout: one JSON document" "$(jq -e type <<< "$LAYOUT" 2> /dev/null)" '"object"'
check "--print-layout: no tmux session is made" "$(ttmux has-session -t =t1-meta 2> /dev/null && echo made || echo none)" "none"
check "--print-layout: no Warp YAML written" "$(test -e "$AW_WARP_LAUNCH_DIR/t1-team.yaml" && echo yes || echo no)" "no"
check "layout: team, socket, cwd, arrangement" "$(jq -r '[.schema, .team, .socket, .cwd, .arrangement] | join("|")' <<< "$LAYOUT" 2> /dev/null)" "1|t1|t1|$REPO|first-left-rest-stacked"
check "layout: every lane in order, the meta lane first" "$(jq -r '[.lanes[].lane] | join(",")' <<< "$LAYOUT" 2> /dev/null)" "meta,api,ui"
check "layout: session names" "$(jq -r '[.lanes[].session] | join(",")' <<< "$LAYOUT" 2> /dev/null)" "t1-meta,t1-api,t1-ui"
check "layout: attach argv on the team's socket" "$(jq -r '.lanes[1].attach | join(" ")' <<< "$LAYOUT" 2> /dev/null)" "tmux -L t1 attach -t =t1-api"
check "layout: join argv (create or attach, no command)" "$(jq -r '.lanes[1].join | join(" ")' <<< "$LAYOUT" 2> /dev/null)" "tmux -L t1 new -A -s t1-api"
check "layout: the pane's shell line starts the lane when its session is missing" "$(jq -r '.lanes[1].shell' <<< "$LAYOUT" 2> /dev/null | grep -c '^tmux -L t1 new -A -s t1-api -c .*meta-lane.sh.* t1 api.* \\; set-option -w remain-on-exit on$')" "1"
check "layout: every lane opens without --lanes" "$(jq -r '[.lanes[].open] | join(",")' <<< "$LAYOUT" 2> /dev/null)" "true,true,true"
launch "$WARP" --print-layout --lanes api
check "layout --lanes api: every lane listed, only api opens" "$(jq -r '[.lanes[] | "\(.lane)=\(.open)"] | join(",")' <<< "$OUT" 2> /dev/null)" "meta=false,api=true,ui=false"
launch "$WARP" --print-layout --dry-run
check "--print-layout and --dry-run do not mix" "$RC" "2"

# ---- the schema describes the document ----
if ensure_scripts_deps "$REPO_ROOT/scripts"; then
  printf '%s\n' "$LAYOUT" > "$WORK/layout.json"
  jq '.lanes[0].attach = "tmux"' "$WORK/layout.json" > "$WORK/bad-attach.json"
  jq '.extra = 1' "$WORK/layout.json" > "$WORK/bad-key.json"
  ajvv() { ( cd "$REPO_ROOT/scripts" && node --input-type=module -e '
      import fs from "node:fs"; import { createRequire } from "node:module";
      const Ajv2020 = createRequire(process.cwd() + "/")("ajv/dist/2020").default;
      const schema = JSON.parse(fs.readFileSync("../schemas/launcher.schema.json", "utf8"));
      const ajv = new Ajv2020({ strict: false, allErrors: true }); ajv.addSchema(schema, "launcher");
      const v = ajv.getSchema("launcher#/$defs/layout");
      if (!v) { console.log("no $defs/layout"); process.exit(0); }
      console.log(v(JSON.parse(fs.readFileSync(process.argv[1], "utf8"))) ? "valid" : "invalid");' "$1" ); }
  check "schema: the printed layout is valid against \$defs/layout" "$(ajvv "$WORK/layout.json")" "valid"
  check "schema: attach must be an argv, unknown keys are refused" "$(ajvv "$WORK/bad-attach.json") $(ajvv "$WORK/bad-key.json")" "invalid invalid"
  check "check-launcher still passes" "$(node "$DIR/check-launcher.mjs" 2>&1 | tail -1)" "check-launcher: ok"
else echo "not ok - Ajv could not be installed"; fail=1; fi

# ---- each adapter takes its lanes and commands from the layout ----
want_attach="$(jq -r '.lanes[] | select(.open) | .attach | join(" ")' <<< "$LAYOUT" 2> /dev/null)"
launch '{"schema":1,"viewer":{"kind":"print"}}' --no-open
check "print adapter: the layout's attach lines" "$(grep '^tmux -L t1 attach' <<< "$OUT")" "$want_attach"

launch "$WARP" --dry-run
check "warp adapter: each pane runs the layout's shell line, in order" "$(sed -n 's/^ *- exec: //p' <<< "$OUT")" "$(jq -r '.lanes[].shell' <<< "$LAYOUT" 2> /dev/null)"

launch '{"schema":1,"viewer":{"kind":"exec-template","name":"rec","argv":["rec","{title}","--","{cmd...}"]}}'
sleep 0.5
check "exec-template adapter: the title and the layout's attach argv per lane" "$(sort "$WORK/rec.calls" 2> /dev/null)" "$(jq -r '.lanes[] | [.session, "--"] + .attach | join("|") + "|"' <<< "$LAYOUT" 2> /dev/null | sort)"

launch '{"schema":1,"viewer":{"kind":"default-terminal"}}'
sleep 0.5
check "default-terminal adapter (Linux): the layout's join argv per lane" "$(sort "$WORK/xdg-terminal-exec.calls" 2> /dev/null)" "$(jq -r '.lanes[] | .join | join("|") + "|"' <<< "$LAYOUT" 2> /dev/null | sort)"
exit $fail
