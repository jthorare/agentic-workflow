#!/usr/bin/env bash
# Replaces gh-1038b-row-actions.sh (which replaced the frozen gh-1038-row-actions.sh): step 5 there looked for the dialog's Approve button with `--text "Approve all workflows"`,
# but the driver reports a Button's text only when its Content is a string, and the dialog's button holds a TextBlock child, so it never matched and the dialog looked absent
# (it was open). This one finds the dialog by its own content (its title TextBlock, which only the dialog has) and its Approve button by name (index 1: the Home row's
# button, same name, comes first in the tree).
# tests/ui-checks/gh-1038-row-actions.sh — frozen regression check for GitHub issue #1038: after #986/#897 no Home action sent a request.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh): `changed-recipe` gives Home a "Approve a changed workflow" row (an older, smaller workflow.hand-test is approved, so
# the shipped one waits for its own approval) and the "Approve all new workflows" row. The check then expects, each against the server's ledger on
# ONE line (the command, human:ui, accepted):
#   1. the changed-workflow row's Approve recipe records ApproveRecipe, and the row leaves Home;
#   2. Mark all seen visibly changes Home (the "Undo mark all seen" control appears);
#   3. the Approve all row opens its dialog and the dialog's Approve records the approve-all command.
# Every driver call is bounded by `timeout 120`; it starts its own Xvfb (tests/ui-checks/xvfb-lib.sh) and never uses the caller's DISPLAY.
#   bash tests/ui-checks/gh-1038-row-actions.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1038_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX
unset DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1038-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1038-driver-XXXXXX")" || exit 2
OUT="${GH1038_OUT:-$(mktemp -d "$TMP_ROOT/gh1038-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER" "$@"; }
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1038c-row-actions: setup failed: $* (screenshot in $OUT)" >&2
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

# On a failed match: what command.handled rows the ledger did have (command caller outcome, the last 20), on stderr.
dump_handled() { # $@ extra ledger args (e.g. --item X)
  local out lines
  out="$("$PROVE" human ledger --json --type command.handled "$@" --max 3000 2>&1 | tr -d '\r')"
  lines="$(printf '%s' "$out" | jq -r '.events[]? | select(.type == "command.handled") | "\(.payload.command) \(.payload.caller) \(.payload.outcome)"' 2>/dev/null | tail -n 20)"
  if [ -n "$lines" ]; then echo "command.handled in the ledger (last 20):" >&2; printf '%s\n' "$lines" >&2; else echo "no command.handled events" >&2; fi
}
# The ledger as JSON (the text ledger line has no command name or outcome): a command.handled event whose payload names the
# command (an ActionId string), caller human:ui and outcome accepted.
accepted_by_ui() { # $1 a jq regex for the command
  "$PROVE" human ledger --json --type command.handled --max 3000 2>&1 | tr -d '\r' \
    | jq -e --arg c "$1" '.events[]? | select(.type == "command.handled" and (.payload.command | test($c)) and .payload.caller == "human:ui" and .payload.outcome == "accepted")' >/dev/null 2>&1
}
# Waits up to 5 s for it.
wait_ledger() {
  local _
  for _ in $(seq 1 10); do
    if accepted_by_ui "$1"; then return 0; fi
    sleep 0.5
  done
  return 1
}

# 1. The control plane, and Home's rows: a changed workflow, and the new shipped ones.
bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"
bash "$FIXTURE" changed-recipe >"$OUT/changed-recipe.log" 2>&1 || setup_fail "sindri-ui-fixture changed-recipe (see $OUT/changed-recipe.log)"

# 2. The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
#    make the build the owner's current one), unless AW_SINDRI_UI names a binary.
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
# An Xvfb this check owns and stops by pid on exit (xvfb-run leaves its Xvfb behind when a timeout kills the check).
source "$ROOT/tests/ui-checks/xvfb-lib.sh"
xvfb_start || setup_fail "no Xvfb display could be started"
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
drv ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

recipe=(--type Button --text "Approve recipe")
all=(--type Button --text "Approve all workflows")
seen=(--type Button --name "Undo mark all seen")
markall=(--type Button --name "Mark all seen")

drv wait-for "${recipe[@]}" --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "no Approve recipe button on a changed-workflow row (see $OUT/app.log)"
drv wait-for "${all[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "no Approve all workflows button on Home (see $OUT/app.log)"
drv screenshot --out "$OUT/before.png" >/dev/null 2>&1

# 3. Approve recipe on the changed-workflow row: ApproveRecipe by human:ui, accepted; the row leaves Home.
drv click "${recipe[@]}" >/dev/null 2>&1 || setup_fail "the Approve recipe click was not accepted by the driver"
wait_ledger '^ApproveRecipe$' || { dump_handled; fail "no ApproveRecipe from human:ui"; }
drv wait-for "${recipe[@]}" --state gone --wait-ms 5000 >/dev/null 2>&1 \
  || fail "the Approve recipe row is still on Home 5 s after the approval"

# 4. Mark all seen visibly changes Home (the Approve all row is still there): the Undo offer appears.
drv wait-for "${markall[@]}" --state visible --wait-ms 10000 >/dev/null 2>&1 || setup_fail "no Mark all seen button on Home"
drv click "${markall[@]}" >/dev/null 2>&1 || setup_fail "the Mark all seen click was not accepted by the driver"
drv wait-for "${seen[@]}" --state visible --wait-ms 5000 >/dev/null 2>&1 \
  || fail "Mark all seen changed nothing on Home (no Undo mark all seen)"

# 5. Approve all: the row opens the dialog; the dialog is found by its own content (the title TextBlock "Approve all workflows" exists only inside it: the Home row's
#    button carries its words as a string, so it has no TextBlock child) and its Approve button by name, the second of the buttons so named (the row's comes first).
dialog_title=(--type TextBlock --text "Approve all workflows")
dialog_button=(--type Button --name "Approve all workflows" --index 1)
drv click "${all[@]}" --index 0 >/dev/null 2>&1 || setup_fail "the Approve all workflows click on Home was not accepted by the driver"
drv wait-for "${dialog_title[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || fail "no approve-all dialog after the Approve all click (its title is not shown)"
drv wait-for "${dialog_button[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || fail "the approve-all dialog is open but has no Approve button (screenshot in $OUT)"
drv click "${dialog_button[@]}" >/dev/null 2>&1 || setup_fail "the dialog's Approve click was not accepted by the driver"
wait_ledger '^ApproveAll' || { dump_handled; fail "no approve-all from human:ui"; }

drv screenshot --out "$OUT/after.png" >/dev/null 2>&1
echo "PASS: Mark all seen, Approve recipe and Approve all each changed Home or sent their command as human:ui (screenshots in $OUT)"
