#!/usr/bin/env bash
# tests/ui-checks/ux-b3-pause.sh — frozen regression check for the UX-pass finding B-3: Home's failure row "Pause service" must
# pause the service. It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home
# (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh): `fail` adds a service the build cannot run, its failure row
# is on Home with a "Pause service" control, the check clicks it and expects, within 5 s, service.paused by human:ui in the ledger.
#   bash tests/ui-checks/ux-b3-pause.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB3_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b3-pause-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b3-pause-driver-XXXXXX")" || exit 2
OUT="${UXB3_OUT:-$(mktemp -d "$TMP_ROOT/ux-b3-pause-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b3-pause: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane with a failing service (the scheduler fails it within ~5 s).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" fail >/dev/null 2>&1 || setup_fail "sindri-ui-fixture fail"

# The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
# make the build the owner's current one), unless AW_SINDRI_UI names a binary.
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
if [ -n "${DISPLAY:-}" ]; then
  bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
else
  xvfb-run -a bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
fi
APP_WRAPPER_PID=$!
"$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# 2. The failure row is on Home; its control is named "Pause service <subject>".
pause=(--type Button --text "Pause service")
"$DRIVER" wait-for "${pause[@]}" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "no Pause service button on the failure row (see $OUT/app.log)"
"$DRIVER" screenshot --out "$OUT/before-click.png" >/dev/null 2>&1

"$DRIVER" click "${pause[@]}" >/dev/null 2>&1 || setup_fail "the Pause service click was not accepted by the driver"

# 3. Within 5 s the ledger records service.paused by the UI.
sent=0
for _ in $(seq 1 10); do
  after="$("$PROVE" human ledger --json --type service.paused --max 1000 2>&1 | tr -d '\r')"
  if printf '%s' "$after" | grep -q 'human:ui'; then sent=1; break; fi
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$sent" = 1 ] || fail "no service.paused in the ledger after 5 s (screenshots in $OUT)"

echo "PASS: Pause service paused the service as human:ui (screenshots in $OUT)"
