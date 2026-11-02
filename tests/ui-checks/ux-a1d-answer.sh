#!/usr/bin/env bash
# Replaces ux-a1c-answer.sh (which replaced ux-a1b, which replaced the frozen ux-a1-answer.sh): ux-a1c finished with the sheet's Complete accepted (the file is typed into the
# "Output fact:output" box first), but then waited for the "Answer <subject>" button to be gone. dev-desk-round-trip's export step leads to an import desk step on the SAME instance,
# so the strip row stays and keeps its Answer button; only its words change ("needs your output" becomes "needs your import"). This one keeps every step and the ledger assertion (CompleteStep by human:ui,
# accepted) and replaces the last assertion: within 5 s the clicked instance's strip row (found by its full name "<subject>, <workflow>", not by the button name) is gone or no longer says "needs your output".
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
setup_fail() { echo "ux-a1d-answer: setup failed: $*" >&2; exit 2; }
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

# 3b. The step requires fact:output: the sheet has a path box for it. The check writes a file under its own $OUT and types the path.
export_file="$OUT/export.txt"
printf 'ux-a1c export\n' > "$export_file" || setup_fail "could not write $export_file"
box=""
for _ in $(seq 1 20); do
  box="$("$DRIVER" search --type TextBox --limit 20 2>/dev/null | jq -r '[.matches[].name | select(. != null and contains("fact:output"))][0] // empty')"
  [ -n "$box" ] && break
  sleep 0.5
done
[ -n "$box" ] || fail "no complete sent (the sheet has no box for the required output fact:output; screenshot in $OUT)"
"$DRIVER" type --type TextBox --name "$box" --value "$export_file" --replace >/dev/null 2>&1 || fail "no complete sent (typing the path into the output box was not accepted)"
"$DRIVER" screenshot --out "$OUT/typed.png" >/dev/null 2>&1

before="$("$PROVE" human ledger --json --max 2000 2>&1 | tr -d '\r')"
"$DRIVER" click --type Button --name "$complete" >/dev/null 2>&1 || fail "no complete sent (the Complete click was not accepted)"

# 4. Within 5 s: the ledger records the complete from the UI, and the Answer row is gone.
sent=0
for _ in $(seq 1 10); do
  after="$("$PROVE" human ledger --json --max 2000 2>&1 | tr -d '\r')"
  if "$PROVE" human ledger --json --type command.handled --max 2000 2>&1 | tr -d '\r' | jq -e --arg c "CompleteStep" '.events[]? | select(.type == "command.handled" and .payload.command == $c and .payload.caller == "human:ui" and .payload.outcome == "accepted")' >/dev/null 2>&1; then sent=1; break; fi   # command.handled: CompleteStep by human:ui, accepted
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$sent" = 1 ] || { dump_handled; fail "no complete sent (no CompleteStep by human:ui in the ledger after 5 s; screenshots in $OUT)"; }
[ "$before" != "$after" ] || fail "no complete sent (the ledger did not change; screenshots in $OUT)"
# 5. The instance moved on: its strip row (the Border named "<subject>, <workflow>", Labels.StripRowName; the clicked button is "Answer, <subject>", Labels.AnswerName) no
#    longer says it needs the export's output within 5 s: the row is gone, or its texts no longer hold "needs your output" (they read "needs your import" once the import step is open).
#    The subject is matched whole, with the ", " after it, so another row whose subject only starts the same cannot match.
subject="${answer#Answer, }"
row_texts() {
  local id
  id="$("$DRIVER" search --type Border --limit 200 2>/dev/null | jq -r --arg s "$subject, " '[.matches[] | select((.name // "") | startswith($s))][0].id // empty')"
  [ -n "$id" ] || return 1
  "$DRIVER" tree --node-id "$id" --depth 8 2>/dev/null | jq -r '[.. | objects | .text? | strings] | join(" | ")'
}
moved=0
texts=""
for _ in $(seq 1 10); do
  if texts="$(row_texts)"; then
    if ! printf '%s' "$texts" | grep -q 'needs your output'; then moved=1; break; fi
  else
    moved=1; break   # the row is gone
  fi
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-moved.png" >/dev/null 2>&1
[ "$moved" = 1 ] || fail "the instance's strip row still says it needs the output 5 s after the Complete (row texts: ${texts:-none found}; screenshots in $OUT)"

echo "PASS: the sheet's Complete reported the desk run as human:ui and the instance moved on to its next step (screenshots in $OUT)"
