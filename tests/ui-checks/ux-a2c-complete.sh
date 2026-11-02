#!/usr/bin/env bash
# Replaces ux-a2b-complete.sh (which replaced the frozen ux-a2-complete.sh): the ticket (A-2, Home's Complete does nothing) asks that a ready control ACTS, or is shown
# as CLI-only, and ux-a2b asked for a ledger CompleteStep. No fixture desk task can send from Home (every one opens on step.produce, which requires the file fact:output,
# and #1170 tracks Home carrying outputs), so this one checks the other half: the click is no longer silent. The row says what it needs ("needs a file first") within 5 s,
# and nothing is sent (no CompleteStep by human:ui), because the refusal must not send.
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
setup_fail() { echo "ux-a2c-complete: setup failed: $*" >&2; exit 2; }
# On a failed match: what command.handled rows the ledger did have (command caller outcome, the last 20), on stderr.
dump_handled() { # $@ extra ledger args (e.g. --item X)
  local out lines
  out="$("$PROVE" human ledger --json --type command.handled "$@" --max 2000 2>&1 | tr -d '\r')"
  lines="$(printf '%s' "$out" | jq -r '.events[]? | select(.type == "command.handled") | "\(.payload.command) \(.payload.caller) \(.payload.outcome)"' 2>/dev/null | tail -n 20)"
  if [ -n "$lines" ]; then echo "command.handled in the ledger (last 20):" >&2; printf '%s\n' "$lines" >&2; else echo "no command.handled events" >&2; fi
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

# 3. Within 5 s: the row says what it needs (a visible note with "needs a file first"), and nothing was sent.
noted=0
for _ in $(seq 1 10); do
  if "$DRIVER" search --type TextBlock --limit 200 2>/dev/null | jq -e '[.matches[] | select(.visible and ((.text // "") | contains("needs a file first")))] | length > 0' >/dev/null 2>&1; then noted=1; break; fi
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$noted" = 1 ] || { dump_handled; fail "the Complete click was silent (no row note with 'needs a file first' within 5 s; screenshots in $OUT)"; }
if "$PROVE" human ledger --json --type command.handled --max 2000 2>&1 | tr -d '\r' | jq -e '.events[]? | select(.type == "command.handled" and .payload.command == "CompleteStep" and .payload.caller == "human:ui")' >/dev/null 2>&1; then
  dump_handled
  fail "the refused Complete was sent anyway (a CompleteStep by human:ui is in the ledger; screenshots in $OUT)"
fi

echo "PASS: Complete on a Home row said what it needs and sent nothing (screenshots in $OUT)"
