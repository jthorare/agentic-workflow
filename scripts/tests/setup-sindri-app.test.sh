#!/usr/bin/env bash
# Tests for setup.sh's Sindri app step (config/lib/setup-sindri-app-step.sh, setup-once SO-A7a) through a stub `sindri-app`
# (AW_SINDRI_APP=PATH) and a faked desktop session (AW_SETUP_DESKTOP): nothing real is published or started, and every run uses
# a scratch HOME and AW_STATE_DIR. Both branches (desktop: starts detached and checks it answers; no desktop: prints
# `sindri-app --headless`, exit 0), a failed publish exits 1, a start that doesn't answer exits 1, a dry run writes nothing,
# and setup.sh runs the step only with --with-sindri, after the provider installs, keeping the Onyx step.
# Run: bash scripts/tests/setup-sindri-app.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$WORK"' EXIT
export HOME="$WORK/home"; mkdir -p "$HOME"
unset AW_SINDRI_HOME AW_SINDRI_APP AW_SETUP_DESKTOP SSH_CONNECTION

# The stub launcher: --publish-only exits STUB_PUBLISH_RC; `status --json` answers (exit 1, "attention") once $UP exists, else exits 2;
# a bare run is the detached start: it records its environment and, unless STUB_NO_START=1, comes up.
STUB="$WORK/sindri-app"
cat > "$STUB" <<'STUBEOF'
#!/usr/bin/env bash
echo "$*" >> "$CALLS"
case "${1:-}" in
  --publish-only) exit "${STUB_PUBLISH_RC:-0}" ;;
  status) [ -f "$UP" ] && exit 1; exit 2 ;;
  "") echo "no_mcp=${AW_SINDRI_APP_NO_MCP:-} stdin=$([ -t 0 ] && echo tty || echo none)" >> "$CALLS"
      [ "${STUB_NO_START:-0}" = 1 ] || : > "$UP"; exit 0 ;;
esac
STUBEOF
chmod +x "$STUB"

# run <name> [env…]: sources the step and runs both halves like setup.sh does; sets OUT and RC (1 on a failed publish, else 1 on a
# failed start, else 0). Fresh state dir per run.
run() {
  local name="$1"; shift
  local s="$WORK/$name"; mkdir -p "$s"
  export AW_STATE_DIR="$s/state" CALLS="$s/calls" UP="$s/up"; : > "$CALLS"
  OUT="$(env "$@" AW_SINDRI_APP="$STUB" AW_SETUP_SINDRI_WAIT=2 bash -c '
    aw_dry() { [ "${AW_DRY_RUN:-0}" = 1 ]; }
    source "$1/config/lib/setup-sindri-app-step.sh"
    aw_setup_sindri_app_publish "$1" || exit 1
    aw_setup_sindri_app_start "$1" || exit 1' _ "$DIR" 2>&1)"; RC=$?
}

run desktop AW_SETUP_DESKTOP=1
check "desktop: exit 0" "$RC" "0"
check "desktop: published first" "$(sed -n 1p "$WORK/desktop/calls")" "--publish-only"
check "desktop: started detached with no MCP offer and no stdin" "$(grep -c 'no_mcp=1 stdin=none' "$WORK/desktop/calls")" "1"
check "desktop: reports running" "$(grep -c 'Sindri is running' <<<"$OUT")" "1"
check "desktop: own step name" "$(grep -c '=== Sindri app ===' <<<"$OUT")" "1"
check "desktop: the log is private" "$(stat -c %a "$WORK/desktop/state/logs/sindri-app.log" 2>/dev/null || stat -f %Lp "$WORK/desktop/state/logs/sindri-app.log")" "600"

run headless AW_SETUP_DESKTOP=0
check "headless: exit 0" "$RC" "0"
check "headless: prints the headless command" "$(grep -c -- "$STUB --headless" <<<"$OUT")" "1"
check "headless: only the publish ran" "$(cat "$WORK/headless/calls")" "--publish-only"

run ssh AW_SETUP_DESKTOP= DISPLAY=:0 SSH_CONNECTION="1 2 3 4"
check "ssh session counts as no desktop" "$(grep -c -- '--headless' <<<"$OUT")" "1"
if [ "$(uname)" != "Darwin" ]; then
  run display AW_SETUP_DESKTOP= DISPLAY=:0 SSH_CONNECTION=
  check "DISPLAY without ssh counts as a desktop" "$(grep -c 'Sindri is running' <<<"$OUT")" "1"
fi

run badpub AW_SETUP_DESKTOP=1 STUB_PUBLISH_RC=1
check "failed publish: exit 1" "$RC" "1"
check "failed publish: names the log" "$(grep -c 'could not be published; see .*publish.log' <<<"$OUT")" "1"
check "failed publish: nothing started" "$(cat "$WORK/badpub/calls")" "--publish-only"

run nostart AW_SETUP_DESKTOP=1 STUB_NO_START=1
check "start that doesn't answer: exit 1" "$RC" "1"
check "start that doesn't answer: names the log" "$(grep -c "Sindri didn't start, see .*sindri-app.log" <<<"$OUT")" "1"

run dry AW_SETUP_DESKTOP=1 AW_DRY_RUN=1
check "dry run: exit 0" "$RC" "0"
check "dry run: says what it would do" "$(grep -c 'would run sindri-app --publish-only' <<<"$OUT")" "1"
check "dry run: runs and writes nothing" "$(cat "$WORK/dry/calls"; ls "$WORK/dry/state" 2>/dev/null | wc -l | tr -d ' ')" "0"

# Wiring in setup.sh: only with --with-sindri, after the provider installs, before the Onyx step, which stays.
S="$DIR/setup.sh"
check "setup.sh runs the step once" "$(grep -c 'aw_setup_sindri_app_publish' "$S")" "1"
check "setup.sh: the step is gated on --with-sindri" "$(grep -B3 'aw_setup_sindri_app_publish' "$S" | grep -c 'WITH_SINDRI')" "1"
check "setup.sh: the step comes after the provider installs" "$([ "$(grep -n '"${_p}_install"' "$S" | head -1 | cut -d: -f1)" -lt "$(grep -n 'aw_setup_sindri_app_publish' "$S" | cut -d: -f1)" ] && echo yes)" "yes"
check "setup.sh: the Onyx step is kept, after it" "$([ "$(grep -n 'aw_setup_sindri_app_publish' "$S" | cut -d: -f1)" -lt "$(grep -n 'install-onyx.sh' "$S" | head -1 | cut -d: -f1)" ] && echo yes)" "yes"

exit "$fail"
