#!/usr/bin/env bash
# tests/ui-checks/gh-1059-palette-ranking.sh — frozen regression check for issue #1059 (UX-pass finding A-4).
# Typing "work" in the command palette must select a page match first (the Work page), never "Approve all workflows" (Enter
# would open the approve dialog), (the list order is covered by the unit tests). The palette's polite live region says the
# selected entry as "<name>, N of M", so it is read from there.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home. Takes well under a minute.
#   bash tests/ui-checks/gh-1059-palette-ranking.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX
unset DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1059-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1059-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/gh-1059-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() {
  timeout 120 "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1059-palette-ranking: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
}
fail() { echo "FAIL: $*" >&2; exit 1; }

cleanup() {
  local app_pid=""
  if [ -f "$AW_UI_DRIVER_DIR/driver.json" ]; then
    app_pid="$(sed -n 's/.*"pid":\([0-9][0-9]*\).*/\1/p' "$AW_UI_DRIVER_DIR/driver.json")"
  fi
  if [ -n "$app_pid" ]; then kill "$app_pid" 2>/dev/null || true; fi
  if [ -n "$APP_WRAPPER_PID" ]; then kill "$APP_WRAPPER_PID" 2>/dev/null || true; fi
  bash "$FIXTURE" down >/dev/null 2>&1 || true
}
trap cleanup EXIT

command -v jq >/dev/null 2>&1 || setup_fail "jq is needed"

bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"

if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
source "$ROOT/tests/ui-checks/xvfb-lib.sh"
xvfb_start || setup_fail "no Xvfb display could be started"
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
timeout 120 "$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# The palette opens by Ctrl+K on the window (node 0: nothing has focus in a fresh window).
timeout 120 "$DRIVER" key --chord Ctrl+K --node-id 0 >/dev/null 2>&1 || setup_fail "the Ctrl+K chord was not accepted"
timeout 120 "$DRIVER" wait-for --name PaletteAnnouncer --state exists --wait-ms 30000 >/dev/null 2>&1 || setup_fail "the palette did not open"
timeout 120 "$DRIVER" type --value work >/dev/null 2>&1 || setup_fail "typing into the palette query failed"
sleep 1
timeout 120 "$DRIVER" screenshot --out "$OUT/work.png" >/dev/null 2>&1

selected="$(timeout 120 "$DRIVER" search --name PaletteAnnouncer 2>"$OUT/search.err" | jq -r '.matches[0].text // empty')"
[ -n "$selected" ] || setup_fail "the palette's live region has no text (see $OUT/search.err)"

# 1. The selected entry is never the mutating Approve all.
case "$selected" in
  "Approve all workflows"*) fail "typing 'work' selects '$selected': Enter would open the approve dialog (screenshot in $OUT)" ;;
esac
# 2. A page match ranks first: the Work page.
case "$selected" in
  "Work, "*) ;;
  *) fail "typing 'work' selects '$selected', not the Work page (screenshot in $OUT)" ;;
esac
echo "PASS: 'work' selects the Work page first (screenshots in $OUT)"
