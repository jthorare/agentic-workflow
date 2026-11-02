#!/usr/bin/env bash
# tests/ui-checks/ux-a1-answer.sh — frozen regression check for the UX-pass finding A-1: Home's Answer on a desk instance opens the
# instance sheet, and the sheet's Complete must report the desk run (today it is a label with "Not available in the app yet").
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home
# (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh): `workflows` starts dev-desk-round-trip instances, the check
# clicks a strip row's Answer, then Complete on the sheet, and expects, within 5 s, the ledger to record the complete
# (CompleteStep) by human:ui and the waiting row to leave Home. Take over is not part of this check.
#   bash tests/ui-checks/ux-a1-answer.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXA1_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-a1-answer-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-a1-answer-driver-XXXXXX")" || exit 2
OUT="${UXA1_OUT:-$(mktemp -d "$TMP_ROOT/ux-a1-answer-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-a1-answer: setup failed: $*" >&2; exit 2; }
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

# 2. A strip row for a desk instance ("Fixture work ...") offers Answer (its name is "Answer <subject>"); click the first.
answer_name() {
  "$DRIVER" search --type Button --text Answer --limit 20 2>/dev/null | jq -r '[.matches[].name | select(contains("Fixture work"))][0] // empty'
}
answer=""
for _ in $(seq 1 60); do
  answer="$(answer_name)"
  [ -n "$answer" ] && break
  sleep 1
done
[ -n "$answer" ] || setup_fail "no Answer button on a desk instance's strip row (see $OUT/app.log)"
"$DRIVER" click --type Button --name "$answer" >/dev/null 2>&1 || setup_fail "the Answer click was not accepted by the driver"
"$DRIVER" screenshot --out "$OUT/sheet.png" >/dev/null 2>&1

# 3. The instance sheet names its commands "<label> <address>" and the address holds the recipe, so Complete there is the
#    button whose name has "/dev-desk-round-trip/" (Home's own Complete is "Complete <subject>").
sheet_complete() {
  "$DRIVER" search --type Button --text Complete --limit 20 2>/dev/null | jq -r '[.matches[].name | select(contains("/dev-desk-round-trip/"))][0] // empty'
}
complete=""
for _ in $(seq 1 20); do
  complete="$(sheet_complete)"
  [ -n "$complete" ] && break
  sleep 0.5
done
[ -n "$complete" ] || fail "no complete sent (the instance sheet has no Complete button; screenshot in $OUT)"

before="$("$PROVE" human ledger --json --max 2000 2>&1 | tr -d '\r')"
"$DRIVER" click --type Button --name "$complete" >/dev/null 2>&1 || fail "no complete sent (the Complete click was not accepted)"

# 4. Within 5 s: the ledger records the complete from the UI, and the Answer row is gone.
sent=0
for _ in $(seq 1 10); do
  after="$("$PROVE" human ledger --json --max 2000 2>&1 | tr -d '\r')"
  if "$PROVE" human ledger --max 2000 2>&1 | tr -d '\r' | grep 'CompleteStep' | grep 'human:ui' | grep -q 'accepted'; then sent=1; break; fi   # one line: CompleteStep by human:ui, accepted
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$sent" = 1 ] || fail "no complete sent (no CompleteStep by human:ui in the ledger after 5 s; screenshots in $OUT)"
[ "$before" != "$after" ] || fail "no complete sent (the ledger did not change; screenshots in $OUT)"
"$DRIVER" wait-for --type Button --name "$answer" --state gone --wait-ms 5000 >/dev/null 2>&1 \
  || fail "the Answer row is still shown 5 s after the Complete (screenshots in $OUT)"

echo "PASS: the sheet's Complete reported the desk run as human:ui and the row left Home (screenshots in $OUT)"
