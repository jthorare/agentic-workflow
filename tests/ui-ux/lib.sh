# tests/ui-ux/lib.sh — sourced by the UX-pass checks (tests/ui-ux/<id>.sh): each check drives the real sindri-ui
# through aw-ui-driver against a seeded fixture home and asserts the expected behaviour of one UX-pass finding.
#
# Needs the debug builds in this checkout's artifacts/: sindri (Aw.Sindri.Host), sindri-ui (Aw.Sindri.Ui.Desktop) and
# aw-ui-driver (Aw.UiDriver.Cli); xvfb-run, jq. Exit 0 = the expected behaviour holds, 1 = it does not, 2 = setup failed.
#
#   uxu_start [fixture verb]...   make a guarded home under /tmp, run the fixture verbs (each one a quoted string, e.g.
#                                 "up" "sessions up" "items"), start sindri-ui under xvfb-run with the driver on and wait
#                                 for it. "recipes" is a verb of its own here: register and approve the dev recipes
#                                 (recipes/dev), which 'workflows' and 'incident start' need.
#   uxu_fixture <verb...>         one more fixture verb, after the start
#   d <aw-ui-driver args...>      the driver against this check's app
#   expect "<what>" <cmd...>      run a check; a failing one is reported and fails the run at the end
#   uxu_done                      print the result and exit (0 when every expect held, else 1)
# Teardown (trap): the app by its recorded pid, the control plane through 'sindri stop', the home removed (kept with UXU_KEEP=1).

set -u
UXU_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UXU_BIN="$UXU_ROOT/artifacts/bin"
SINDRI="$UXU_BIN/Aw.Sindri.Host/debug/sindri"
UXU_UI="$UXU_BIN/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
UXU_DRIVER="$UXU_BIN/Aw.UiDriver.Cli/debug/aw-ui-driver"
UXU_FAILS=0
UXU_UI_PID=""
export SINDRI

uxu_die() { printf 'ui-ux: setup: %s\n' "$*" >&2; exit 2; }

for b in "$SINDRI" "$UXU_UI" "$UXU_DRIVER"; do [ -x "$b" ] || uxu_die "missing $b (build the debug binaries first)"; done
command -v xvfb-run >/dev/null || uxu_die "xvfb-run is not installed"
command -v jq >/dev/null || uxu_die "jq is not installed"

# The lane's environment never reaches the fixture: no tmux socket, no lane or child markers.
unset TMUX TMUX_PANE AW_SINDRI_CHILD AW_SINDRI_PROFILE
for v in $(env | grep -oE '^AW_META_LANE[A-Z_]*'); do unset "$v"; done
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" AW_SINDRI_APP_NO_MCP=1

UXU_TMP="$(mktemp -d /tmp/uxu-XXXXXX)" || uxu_die "mktemp failed"
export AW_SINDRI_HOME="$UXU_TMP/home" AW_UI_DRIVER_DIR="$UXU_TMP/driver"
mkdir -p -m 700 "$AW_SINDRI_HOME" "$AW_UI_DRIVER_DIR"

uxu_teardown() {
  if [ -n "$UXU_UI_PID" ] && kill -0 "$UXU_UI_PID" 2>/dev/null; then
    # setsid made the wrapper a group leader: its group is this check's xvfb-run, Xvfb and sindri-ui only.
    kill -- "-$UXU_UI_PID" 2>/dev/null || kill "$UXU_UI_PID" 2>/dev/null
  fi
  bash "$UXU_ROOT/scripts/sindri-prove.sh" human stop >/dev/null 2>&1 || true
  if [ -n "${UXU_KEEP:-}" ]; then echo "ui-ux: kept $UXU_TMP" >&2; else rm -rf -- "$UXU_TMP"; fi
}
trap uxu_teardown EXIT

uxu_fixture() { bash "$UXU_ROOT/scripts/sindri-ui-fixture.sh" "$@"; }

# Registers and approves recipes/dev/*.yaml, children first: a refused one is retried while any other progresses.
uxu_recipes() {
  local pending="" f out next progress pass
  for f in "$UXU_ROOT"/recipes/dev/*.yaml; do [ "$(basename "$f")" = profile.yaml ] || pending="$pending $f"; done
  for pass in 1 2 3 4 5 6; do
    progress=0; next=""
    for f in $pending; do
      out="$(bash "$UXU_ROOT/scripts/sindri-prove.sh" human recipe approve "$f" </dev/null 2>&1)"
      if printf '%s\n' "$out" | grep -qE '^(approved|already approved)'; then progress=1; else next="$next $f"; fi
    done
    pending="$next"
    [ -z "$pending" ] && { uxu_engine_ready; return; }
    [ "$progress" = 1 ] || break
  done
  uxu_die "dev recipes not approved:$pending"
}

# The workflow engine answers a little after the control plane does: a start before then is refused (SND-API-005).
uxu_engine_ready() {
  local i
  for i in $(seq 1 60); do
    bash "$UXU_ROOT/scripts/sindri-prove.sh" human workflow list </dev/null >/dev/null 2>&1 && return 0
    sleep 1
  done
  uxu_die "the workflow engine did not answer within 60 s"
}

d() { "$UXU_DRIVER" --dir "$AW_UI_DRIVER_DIR" "$@"; }

uxu_start() {
  local verb
  for verb in "$@"; do
    if [ "$verb" = recipes ]; then uxu_recipes; continue; fi
    # shellcheck disable=SC2086 # a verb is words: "sessions up"
    uxu_fixture $verb > "$UXU_TMP/fixture.log" 2>&1 || { cat "$UXU_TMP/fixture.log" >&2; uxu_die "fixture '$verb' failed"; }
  done
  AW_UI_DRIVER=1 setsid xvfb-run -a -s "-screen 0 1600x1000x24" "$UXU_UI" > "$UXU_TMP/ui.log" 2>&1 < /dev/null &
  UXU_UI_PID=$!
  d ready --wait-ms 60000 >/dev/null || { tail -20 "$UXU_TMP/ui.log" >&2; uxu_die "the app's driver never answered"; }
  d window --width 1280 --height 900 >/dev/null || uxu_die "could not size the window"
}

expect() {
  local what="$1"; shift
  if "$@" > "$UXU_TMP/expect.out" 2>&1; then
    printf 'ok   %s\n' "$what"
  else
    printf 'FAIL %s\n' "$what"; sed 's/^/     /' "$UXU_TMP/expect.out" | head -15
    UXU_FAILS=$((UXU_FAILS + 1))
  fi
}

uxu_done() {
  if [ "$UXU_FAILS" -eq 0 ]; then echo "ui-ux: all checks held"; exit 0; fi
  echo "ui-ux: $UXU_FAILS check(s) failed"; exit 1
}
