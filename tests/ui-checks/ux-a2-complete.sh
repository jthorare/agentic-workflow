#!/usr/bin/env bash
# tests/ui-checks/ux-a2-complete.sh — frozen regression check for the UX-pass finding A-2: Home's Needs-you row for an open desk
# task offers Complete (CompleteStep, Ready); a click must report the desk run. It drives the REAL sindri-ui (UI driver,
# docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh):
# `workflows` starts dev-desk-round-trip instances that wait on a desk task, the check clicks the row's Complete and expects,
# within 5 s, the ledger to record the complete (CompleteStep) by human:ui and the row to leave Home.
#   bash tests/ui-checks/ux-a2-complete.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXA2_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-a2-complete-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-a2-complete-driver-XXXXXX")" || exit 2
OUT="${UXA2_OUT:-$(mktemp -d "$TMP_ROOT/ux-a2-complete-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-a2-complete: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane with desk tasks open (workflows starts one triage and two dev-desk-round-trip instances).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" workflows >/dev/null 2>&1 || setup_fail "sindri-ui-fixture workflows"

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

# 2. The desk row's Complete is on Home (its name is "Complete <subject>"); take the first.
complete_name() {
  "$DRIVER" search --type Button --text Complete --limit 20 2>/dev/null | jq -r '[.matches[].name | select(startswith("Complete "))][0] // empty'
}
name=""
for _ in $(seq 1 60); do
  name="$(complete_name)"
  [ -n "$name" ] && break
  sleep 1
done
[ -n "$name" ] || setup_fail "no Complete button on a desk row (see $OUT/app.log)"
"$DRIVER" screenshot --out "$OUT/before-click.png" >/dev/null 2>&1

before="$("$PROVE" human ledger --json --max 2000 2>&1 | tr -d '\r')"
"$DRIVER" click --type Button --name "$name" >/dev/null 2>&1 || setup_fail "the Complete click was not accepted by the driver"

# 3. Within 5 s: the ledger records the complete from the UI, and the row is gone.
sent=0
for _ in $(seq 1 10); do
  after="$("$PROVE" human ledger --json --max 2000 2>&1 | tr -d '\r')"
  if "$PROVE" human ledger --max 2000 2>&1 | tr -d '\r' | grep 'CompleteStep' | grep 'human:ui' | grep -q 'accepted'; then sent=1; break; fi   # one line: CompleteStep by human:ui, accepted
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$sent" = 1 ] || fail "no complete sent (no CompleteStep by human:ui in the ledger after 5 s; screenshots in $OUT)"
[ "$before" != "$after" ] || fail "no complete sent (the ledger did not change; screenshots in $OUT)"
"$DRIVER" wait-for --type Button --name "$name" --state gone --wait-ms 5000 >/dev/null 2>&1 \
  || fail "the Complete row is still shown 5 s after the click (screenshots in $OUT)"

echo "PASS: Complete reported the desk run as human:ui and the row left Home (screenshots in $OUT)"
