#!/usr/bin/env bash
# tests/ui-checks/gh-1062-home-enter-stays.sh — frozen regression check for GitHub issue #1062 (UX-pass finding A-9).
# On Home, J J then Enter must open the row's sheet over Home (not jump to Workflows), and Esc must return focus to the row (not
# leave it on the Board/List toggle). It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture
# home (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh): `workflows` gives the strip its instances.
# Every driver call is bounded by `timeout 120`; it starts its own Xvfb (tests/ui-checks/xvfb-lib.sh).
#   bash tests/ui-checks/gh-1062-home-enter-stays.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1062_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX
unset DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1062-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1062-driver-XXXXXX")" || exit 2
OUT="${GH1062_OUT:-$(mktemp -d "$TMP_ROOT/gh1062-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER" "$@"; }
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1062-home-enter-stays: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
}
fail() {
  drv screenshot --out "$OUT/fail.png" >/dev/null 2>&1 || true
  echo "FAIL: $* (screenshots in $OUT)" >&2
  exit 1
}

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

# 1. The control plane and the strip's instances.
bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"
bash "$FIXTURE" workflows >"$OUT/workflows.log" 2>&1 || setup_fail "sindri-ui-fixture workflows (see $OUT/workflows.log)"

# 2. The real app, drivable, on the fixture's control plane (this checkout's debug build unless AW_SINDRI_UI names a binary).
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
drv ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

is_focused() { # $1 a node id
  local p
  p="$(drv props --node-id "$1" 2>/dev/null)" || setup_fail "props failed for node $1"
  printf '%s' "$p" | jq -e '(.properties // .root.properties // {}) | .IsFocused == "True" or .IsFocused == true' >/dev/null
}
# The page region is the Border whose name is the page's words ("Home", "Workflows", ...).
page_named() { # $1 the page's words
  drv search --type Border --limit 200 2>/dev/null | jq -e --arg n "$1" '[.matches[] | select((.name // "") == $n)] | length > 0' >/dev/null
}

# Wait for the strip's instances to show on Home.
for _ in $(seq 1 60); do
  if drv search --type Button --limit 200 2>/dev/null | jq -e '[.matches[] | select((.name // "") | test("Answer|Open"; "i"))] | length > 0' >/dev/null; then break; fi
  sleep 1
done
page_named Home || setup_fail "the app did not start on Home (see $OUT/app.log)"
drv screenshot --out "$OUT/before.png" >/dev/null 2>&1

# 3. J J, Enter: the sheet opens over Home.
drv key --chord J --node-id 0 >/dev/null 2>&1 || setup_fail "the first J was not accepted by the window"
drv key --chord J >/dev/null 2>&1 || setup_fail "the second J was not accepted"
drv key --chord Enter >/dev/null 2>&1 || setup_fail "Enter was not accepted"
sleep 1.5
drv screenshot --out "$OUT/after-enter.png" >/dev/null 2>&1
page_named Home || fail "Enter on a Home row left Home (the page is no longer Home)"

# 4. Esc: focus must not sit on the Board/List toggle.
drv key --chord Escape >/dev/null 2>&1 || setup_fail "Esc was not accepted"
sleep 1.5
drv screenshot --out "$OUT/after-esc.png" >/dev/null 2>&1
page_named Home || fail "after Esc the page is not Home"
for id in $(drv search --limit 400 2>/dev/null | jq -r '[.matches[] | select((.name // "") == "List" or (.name // "") == "Board") | .id][]'); do
  if is_focused "$id"; then fail "after Esc the Board/List toggle (node $id) has focus instead of the row"; fi
done

echo "PASS: Enter on a Home row opened its sheet over Home and Esc left focus off the Board/List toggle (screenshots in $OUT)"
