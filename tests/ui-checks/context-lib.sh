# tests/ui-checks/context-lib.sh — sourced by the Context page's UX-pass checks (tests/ui-checks/ux-c*.sh, ux-a15-*.sh). It builds
# what ux-a2-complete.sh builds by hand: a guarded fixture home with the Context page's data (scripts/sindri-ui-fixture.sh
# `up` + `context up`), this checkout's debug sindri-ui under xvfb with the UI driver on (never sindri-app.sh's publish), and
# the helpers the checks read the screen with. Exit contract of every check: 0 pass, 1 FAIL (one line on stderr), 2 setup broke;
# every setup failure and every FAIL takes a screenshot into $OUT first.
#
#   ctx_init <check-name>          home, driver dir, $OUT (CTXUX_OUT, default a new temp dir), the cleanup trap (the app's own process group is TERMed then KILLed, `fixture down` runs, then ctx_reap stops what serve left with this run's AW_SINDRI_HOME; #1331)
#   ctx_start [<w> <h>]            fixture up + context up, the app, the window at w x h (default 1280x900), the Context page open
#   ctx_window <w> <h>             resize the window and let the layout settle
#   ctx_text <driver search args>  the text of the first visible match (empty when none)
#   ctx_node <driver search args>  the first visible match with a size, as JSON (empty when none)
#   ctx_prop <driver search args> <Property>   one registered Avalonia property of the first match, as text
#   ctx_nums <text>                the numbers in a text, one per line (a Rect "x, y, w, h", a ZoomView "{ Scale = .., X = .., Y = .. }")
#   fail <message>                 screenshot, one line on stderr, exit 1
#   setup_fail <message>           screenshot, one line on stderr, exit 2
# Env: CTXUX_OUT (screenshots), AW_SINDRI_UI (a built sindri-ui; default this checkout's debug build), SINDRI (a built sindri).
set -uo pipefail
unset TMUX

CTX_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$CTX_ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$CTX_ROOT/scripts/sindri-prove.sh"
DRIVER="$CTX_ROOT/scripts/aw-ui-driver.sh"
CTX_NAME="ux-context"
APP_WRAPPER_PID=""

ctx_init() {
  CTX_NAME="$1"
  local tmp="${TMPDIR:-/tmp}"
  export AW_SINDRI_HOME
  AW_SINDRI_HOME="$(mktemp -d "$tmp/$CTX_NAME-home-XXXXXX")" || exit 2
  touch "$AW_SINDRI_HOME/.prover-scratch"
  export AW_UI_DRIVER=1
  export AW_UI_DRIVER_DIR
  AW_UI_DRIVER_DIR="$(mktemp -d "$tmp/$CTX_NAME-driver-XXXXXX")" || exit 2
  OUT="${CTXUX_OUT:-$(mktemp -d "$tmp/$CTX_NAME-out-XXXXXX")}"
  mkdir -p "$OUT" || exit 2
  trap ctx_cleanup EXIT
  command -v jq >/dev/null 2>&1 || setup_fail "jq is needed"
  command -v awk >/dev/null 2>&1 || setup_fail "awk is needed"
}

ctx_cleanup() {
  local app_pid=""
  if [ -f "$AW_UI_DRIVER_DIR/driver.json" ]; then
    app_pid="$(sed -n 's/.*"pid":\([0-9][0-9]*\).*/\1/p' "$AW_UI_DRIVER_DIR/driver.json")"
  fi
  if [ -n "$app_pid" ]; then kill "$app_pid" 2>/dev/null || true; fi
  if [ -n "$APP_WRAPPER_PID" ]; then kill "$APP_WRAPPER_PID" 2>/dev/null || true; fi
  # The app, xvfb-run and its Xvfb are one process group of their own (ctx_start: setsid), its id is APP_WRAPPER_PID: TERM it, give it ~3 s, then KILL what is left.
  if [ -n "$APP_WRAPPER_PID" ]; then
    kill -TERM -- "-$APP_WRAPPER_PID" 2>/dev/null || true
    local i
    for i in $(seq 1 30); do kill -0 -- "-$APP_WRAPPER_PID" 2>/dev/null || break; sleep 0.1; done
    kill -KILL -- "-$APP_WRAPPER_PID" 2>/dev/null || true
  fi
  bash "$FIXTURE" down >/dev/null 2>&1 || true
  ctx_reap
}

# Stops what this run's serve left behind (`fixture down` kills serve, orphaning its children such as a hung `docker version` probe): our own processes
# whose environment holds this run's AW_SINDRI_HOME, a mktemp path unique to the run. Never the check itself or its ancestors, never anything by name.
ctx_reap() {
  [ -n "${AW_SINDRI_HOME:-}" ] || return 0
  local skip=" $$ $BASHPID " p="$$" d pid found i
  while [ -n "$p" ] && [ "$p" -gt 1 ] 2>/dev/null; do
    skip="$skip$p "
    p="$(awk '{ print $4 }' "/proc/$p/stat" 2>/dev/null)"
  done
  for i in 1 2; do
    found=""
    for d in /proc/[0-9]*; do
      pid="${d#/proc/}"
      case "$skip" in *" $pid "*) continue ;; esac
      [ -O "$d" ] || continue
      if tr '\0' '\n' 2>/dev/null <"$d/environ" | grep -qxF "AW_SINDRI_HOME=$AW_SINDRI_HOME"; then found="$found $pid"; fi
    done
    [ -n "$found" ] || return 0
    if [ "$i" = 1 ]; then
      # shellcheck disable=SC2086  # a list of pids
      kill -TERM $found 2>/dev/null || true
      sleep 1
    else
      # shellcheck disable=SC2086
      kill -KILL $found 2>/dev/null || true
    fi
  done
}

shot() { "$DRIVER" screenshot --out "$OUT/$1.png" >/dev/null 2>&1 || true; }
setup_fail() { shot setup-failed; echo "$CTX_NAME: setup failed: $* (screenshot in $OUT)" >&2; exit 2; }
fail() { shot failed; echo "FAIL: $* (screenshot in $OUT)" >&2; exit 1; }

ctx_start() {
  local w="${1:-1280}" h="${2:-900}"
  bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
  bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
  if [ -z "${AW_SINDRI_UI:-}" ]; then
    dotnet build "$CTX_ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
      || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
    export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
  fi
  [ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
  # A screen larger than any window the checks ask for, whatever DISPLAY the caller has: sizes must be the same everywhere.
  setsid env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
  APP_WRAPPER_PID=$!
  "$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
  "$DRIVER" window --width "$w" --height "$h" >/dev/null 2>&1 || setup_fail "could not size the window"
  "$DRIVER" click --type ListBoxItem --name Context >/dev/null 2>&1 || setup_fail "no Context tab"
  "$DRIVER" wait-for --type TextBlock --contains "links" --state visible --wait-ms 60000 >/dev/null 2>&1 \
    || setup_fail "the whole map never showed its size line"
  sleep 1
}

ctx_window() {
  "$DRIVER" window --width "$1" --height "$2" >/dev/null 2>&1 || setup_fail "could not size the window to $1x$2"
  sleep 1.5
}

ctx_text() { "$DRIVER" search "$@" --limit 20 2>/dev/null | jq -r 'first(.matches[] | select(.visible)) | .text // empty'; }
ctx_node() { "$DRIVER" search "$@" --limit 20 2>/dev/null | jq -c 'first(.matches[] | select(.visible and .width > 0 and .height > 0)) // empty'; }
ctx_prop() {
  local prop="${!#}"
  set -- "${@:1:$(($# - 1))}"
  "$DRIVER" props "$@" 2>/dev/null | jq -r --arg p "$prop" '.properties[$p] // empty'
}
ctx_nums() { printf '%s\n' "$1" | grep -oE -- '-?[0-9]+(\.[0-9]+)?([eE][-+]?[0-9]+)?'; }

# ctx_geometry: where the whole map's canvas is and where the world it holds is drawn, in window coordinates. Sets CANVAS_X/Y/W/H,
# VIEW_S/X/Y (the zoom's scale and offset, read from the ZoomBorder's registered View property), WB_X/Y/W/H (its WorldBounds) and
# WORLD_L/T/R/B (the world's rectangle on screen: canvas origin + view offset + world * scale). The world is drawn by one custom
# control, so its extent is not in the element tree; the view and the world bounds are what the page draws from.
ctx_geometry() {
  local node id view world
  node="$(ctx_node --type ZoomBorder)"
  [ -n "$node" ] || setup_fail "no visible ZoomBorder (the whole map's canvas)"
  id="$(jq -r .id <<<"$node")"
  CANVAS_X="$(jq -r .x <<<"$node")"; CANVAS_Y="$(jq -r .y <<<"$node")"
  CANVAS_W="$(jq -r .width <<<"$node")"; CANVAS_H="$(jq -r .height <<<"$node")"
  view="$("$DRIVER" props --node-id "$id" 2>/dev/null | jq -r '.properties.View // empty')"
  world="$("$DRIVER" props --node-id "$id" 2>/dev/null | jq -r '.properties.WorldBounds // empty')"
  VIEW_S="$(sed -n 's/.*Scale = \([^,} ]*\).*/\1/p' <<<"$view")"
  VIEW_X="$(sed -n 's/.*X = \([^,} ]*\).*/\1/p' <<<"$view")"
  VIEW_Y="$(sed -n 's/.*Y = \([^,} ]*\).*/\1/p' <<<"$view")"
  [ -n "$VIEW_S" ] && [ -n "$VIEW_X" ] && [ -n "$VIEW_Y" ] || setup_fail "the canvas's View property did not read as a ZoomView ('$view')"
  mapfile -t wb < <(ctx_nums "$world")
  [ "${#wb[@]}" -eq 4 ] || setup_fail "the canvas's WorldBounds did not read as a rectangle ('$world')"
  WB_X="${wb[0]}"; WB_Y="${wb[1]}"; WB_W="${wb[2]}"; WB_H="${wb[3]}"
  read -r WORLD_L WORLD_T WORLD_R WORLD_B < <(awk -v cx="$CANVAS_X" -v cy="$CANVAS_Y" -v s="$VIEW_S" -v vx="$VIEW_X" -v vy="$VIEW_Y" \
    -v wx="$WB_X" -v wy="$WB_Y" -v ww="$WB_W" -v wh="$WB_H" \
    'BEGIN { l = cx + vx + wx * s; t = cy + vy + wy * s; printf "%.1f %.1f %.1f %.1f\n", l, t, l + ww * s, t + wh * s }')
}

# lt A B: true when A < B (decimals).
lt() { awk -v a="$1" -v b="$2" 'BEGIN { exit !(a < b) }'; }
