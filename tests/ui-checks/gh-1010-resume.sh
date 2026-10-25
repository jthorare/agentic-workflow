#!/usr/bin/env bash
# tests/ui-checks/gh-1010-resume.sh — frozen regression check for GitHub issue #1010: Resume on a timeout park must send
# ApplyCommand:answer-resume. It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture
# home (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh): workflow.dev-timeout is parked by its 1m timeout, the
# Needs-you row on Home offers Resume, the check clicks it and expects, within 5 s, the ledger to record answer-resume by
# human:ui and the app to show the change. Takes ~1.5 min (the park waits out the recipe's 1m timeout).
#   bash tests/ui-checks/gh-1010-resume.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1010_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1010-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1010-driver-XXXXXX")" || exit 2
OUT="${GH1010_OUT:-$(mktemp -d "$TMP_ROOT/gh1010-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "gh-1010-resume: setup failed: $*" >&2; exit 2; }
fail() { echo "FAIL: $*" >&2; exit 1; }

cleanup() {
  local app_pid=""
  if [ -f "$AW_UI_DRIVER_DIR/driver.json" ]; then
    app_pid="$(sed -n 's/.*"pid":\([0-9][0-9]*\).*/\1/p' "$AW_UI_DRIVER_DIR/driver.json")"
  fi
  if [ -n "$app_pid" ]; then kill "$app_pid" 2>/dev/null || true; fi
  if [ -n "$APP_WRAPPER_PID" ]; then kill "$APP_WRAPPER_PID" 2>/dev/null || true; fi
  FIXTURE_DESK_TIMEOUT= bash "$FIXTURE" down >/dev/null 2>&1 || true
}
trap cleanup EXIT

command -v jq >/dev/null 2>&1 || setup_fail "jq is needed"

# 1. The control plane, with the desk floor lowered so the recipe's own 1m timeout rules.
FIXTURE_DESK_TIMEOUT=1m bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
item="$(bash "$FIXTURE" park-timeout 2>/dev/null | tail -1)"
[ -n "$item" ] || setup_fail "park-timeout printed no item"

# 2. Wait for the timeout park (the recipe's 1m, so allow 150 s).
address=""
parked=0
for _ in $(seq 1 75); do
  line="$("$PROVE" human workflow list --item "$item" 2>&1 | tr -d '\r' | grep -m1 'workflow\.dev-timeout')"
  address="$(printf '%s\n' "$line" | awk '{print $1}')"
  if printf '%s\n' "$line" | grep -q 'parked'; then parked=1; break; fi
  sleep 2
done
[ "$parked" = 1 ] && [ -n "$address" ] || setup_fail "the instance never parked by its timeout (last line: ${line:-none})"
status="$("$PROVE" human workflow status "$address" 2>&1 | tr -d '\r')"
printf '%s\n' "$status" | grep -qi 'resume' || setup_fail "the parked status does not offer resume: $status"

# 3. The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
#    make the build the owner's current one), unless AW_SINDRI_UI names a binary.
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

# 4. The Needs-you row for the instance is on Home; its Resume button is the row's control (name "Resume <subject>").
resume=(--type Button --text Resume)
"$DRIVER" wait-for "${resume[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "no Resume button on the Needs-you row (see $OUT/app.log)"
"$DRIVER" screenshot --out "$OUT/before-click.png" >/dev/null 2>&1

before="$("$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r')"
"$DRIVER" click "${resume[@]}" >/dev/null 2>&1 || setup_fail "the Resume click was not accepted by the driver"

# 5. Within 5 s: the ledger records the answer from the UI, and the app shows it.
sent=0
for _ in $(seq 1 10); do
  after="$("$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r')"
  if "$PROVE" human ledger --item "$item" --max 1000 2>&1 | tr -d '\r' | grep 'answer-resume' | grep 'human:ui' | grep -q 'accepted'; then sent=1; break; fi   # one line: answer-resume by human:ui, accepted
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$sent" = 1 ] || fail "no answer-resume in the ledger after 5 s (screenshots in $OUT)"
[ "$before" != "$after" ] || fail "the ledger did not change after the Resume click (screenshots in $OUT)"
"$DRIVER" wait-for "${resume[@]}" --state gone --wait-ms 5000 >/dev/null 2>&1 \
  || fail "the Resume row is still shown 5 s after the click (screenshots in $OUT)"

echo "PASS: Resume sent answer-resume as human:ui and the row left Home (screenshots in $OUT)"
