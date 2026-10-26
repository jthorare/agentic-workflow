#!/usr/bin/env bash
# tests/ui-checks/gh-1004-takeover.sh — frozen regression check for GitHub issue #1004: Take over on an agent run must send the
# takeover command and show what the viewer did (opened in <label>, or the reason plus the attach command). It drives the REAL
# sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh): `agent-run` leaves an instance on a live agent Step (a launch, and a live session on its item),
# the check opens the instance, clicks Take over and expects, within 5 s, the ledger to record a takeover by human:ui and the app
# to show the viewer's outcome line.
#   bash tests/ui-checks/gh-1004-takeover.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1004_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1004-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1004-driver-XXXXXX")" || exit 2
OUT="${GH1004_OUT:-$(mktemp -d "$TMP_ROOT/gh1004-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "gh-1004-takeover: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane, with a live agent run: an instance on a step.agent Step, its launch and a live session on its item.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" sessions up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture sessions up"
item="$(bash "$FIXTURE" agent-run 2>/dev/null | tail -1)"
[ -n "$item" ] || setup_fail "agent-run printed no item"
address="$("$PROVE" human workflow list --item "$item" 2>&1 | tr -d '\r' | awk 'NR==1 {print $1}')"
[ -n "$address" ] || setup_fail "the item has no workflow instance"

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

# 4. Open the instance (its expanded state carries the Attach control); its chevron menu offers Take over.
"$DRIVER" wait-for --type Button --text "Watch the agent" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "no Watch the agent control on the instance (see $OUT/app.log)"
"$DRIVER" screenshot --out "$OUT/before-click.png" >/dev/null 2>&1
before="$("$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r')"
# Take over is a MenuItem in the chevron's flyout (AttachButton.axaml), which joins the tree only once the menu is open.
"$DRIVER" click --type Button --text "▾" --index 0 >/dev/null 2>&1 || setup_fail "the chevron click was not accepted by the driver"
"$DRIVER" wait-for --type MenuItem --text "Take over" --state visible --wait-ms 5000 >/dev/null 2>&1 \
  || setup_fail "the chevron menu shows no Take over (see $OUT/app.log)"
"$DRIVER" click --type MenuItem --text "Take over" >/dev/null 2>&1 || setup_fail "the Take over click was not accepted by the driver"

# 5. Within 5 s: the ledger records the takeover from the UI, and the app shows the viewer's outcome.
sent=0
for _ in $(seq 1 10); do
  if "$PROVE" human ledger --item "$item" --max 1000 2>&1 | tr -d '\r' | grep 'takeover' | grep 'human:ui' | grep -q 'accepted'; then sent=1; break; fi   # one line: takeover by human:ui, accepted
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$sent" = 1 ] || fail "Take over sent nothing (no takeover in the ledger after 5 s; screenshots in $OUT)"
after="$("$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r')"
[ "$before" != "$after" ] || fail "the ledger did not change after the Take over click (screenshots in $OUT)"
# The outcome line: "…opened in <label>." or the no-viewer / reason lines, which come with the attach command beside them.
shown=0
for text in "opened in" "did not confirm" "No terminal was opened" "viewer failed" "No viewer is set up" "didn't say whether a terminal opened"; do
  if "$DRIVER" wait-for --type TextBlock --text "$text" --state visible --wait-ms 1000 >/dev/null 2>&1; then shown=1; break; fi
done
[ "$shown" = 1 ] || fail "no viewer outcome shown (screenshots in $OUT)"

echo "PASS: Take over sent the takeover as human:ui and the app showed the viewer outcome (screenshots in $OUT)"
