#!/usr/bin/env bash
# tests/ui-checks/gh-1045-takeover-inspector.sh — frozen regression check for GitHub issue #1045: Take over in the run inspector
# (Workflows, the instance's run drawer) must send the takeover command as human:ui and show on screen what came of it: the viewer's
# outcome (opened in <label>, or failed with its reason) or the refusal. Today the drawer's Take over only opens the run's frame and
# sends nothing. It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home
# (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh): `agent-run` leaves an instance on a live agent Step; the check opens
# Workflows, selects the instance, presses Take over in its drawer and expects, within 10 s, ONE takeover accepted from human:ui in the
# ledger, and an outcome or refusal line on screen.
# Every driver call is bounded by `timeout 120`; it starts its own Xvfb (tests/ui-checks/xvfb-lib.sh) and never uses the caller's DISPLAY.
#   bash tests/ui-checks/gh-1045-takeover-inspector.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1045_OUT (screenshots and logs; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1045-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1045-driver-XXXXXX")" || exit 2
OUT="${GH1045_OUT:-$(mktemp -d "$TMP_ROOT/gh1045-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER" "$@"; }
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1045-takeover-inspector: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
}
fail() { drv screenshot --out "$OUT/fail.png" >/dev/null 2>&1 || true; echo "FAIL: $*" >&2; exit 1; }

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

export FIXTURE_AGENT_WAIT="${FIXTURE_AGENT_WAIT:-600}"   # a busy heavy lock delays the launch
command -v jq >/dev/null 2>&1 || setup_fail "jq is needed"

# The takeover lines of the ledger that human:ui had accepted (one line each).
takeovers_by_ui() {
  "$PROVE" human ledger --item "$item" --max 3000 2>&1 | tr -d '\r' | grep -i 'takeover' | grep 'human:ui' | grep -c 'accepted'
}

# 1. The control plane, with a live agent run: an instance on a step.agent Step, its launch and a live session on its item.
bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"
bash "$FIXTURE" sessions up >"$OUT/sessions-up.log" 2>&1 || setup_fail "sindri-ui-fixture sessions up (see $OUT/sessions-up.log)"
bash "$FIXTURE" agent-run >"$OUT/agent-run.log" 2>&1 || setup_fail "sindri-ui-fixture agent-run (see $OUT/agent-run.log)"
item="$(grep -oE 'ITM-[0-9a-f]+' "$OUT/agent-run.log" | head -1)"   # the verb's output is ledger lines too: the first item id is the run's item
[ -n "$item" ] || setup_fail "agent-run printed no item (see $OUT/agent-run.log)"

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

# 3. Workflows -> the instance -> its run drawer (the Take over button of the drawer, named "Take over").
# The top bar's pages are ListBoxItems named by their label.
drv click --type ListBoxItem --name Workflows >"$OUT/nav.log" 2>&1 \
  || setup_fail "the Workflows nav item was not clicked (driver: $(tr -d '\n' <"$OUT/nav.log" | cut -c1-160))"
# The page opens as the board, which holds only runs on a lane; the List toggle shows every run.
drv click --type ListBoxItem --name List >"$OUT/list.log" 2>&1 \
  || setup_fail "the List toggle was not clicked (driver: $(tr -d '\n' <"$OUT/list.log" | cut -c1-160))"
# The run's title button is in the row; focus arriving on it selects the row (WorkflowsPage.OnSelect) and opens the drawer without opening the run.
# Find the run's row by its accessible name ("<title>, <workflow>, <status>, ..." on the focusable row border) in a complete search, polling up to 60 s.
row_id=""
for _ in $(seq 1 30); do
  nodes="$(drv search --limit 5000 2>/dev/null)" || nodes=""
  if [ -n "$nodes" ]; then
    [ "$(printf '%s' "$nodes" | jq '(.total // 0) > (.matches | length)')" = false ] || setup_fail "the node search is incomplete (more nodes than --limit 5000)"
    row_id="$(printf '%s' "$nodes" | jq -r '[.matches[] | select(.visible == true and ((.name // "") | startswith("Fixture agent run, ")))][0].id // empty')"
    [ -n "$row_id" ] && break
  fi
  sleep 2
done
[ -n "$row_id" ] || setup_fail "Workflows lists no row for Fixture agent run"
# Focus selects the row; Enter on the selected row opens the drawer on it (a second Enter would open the run). The row border is not a button, so the key reaches the page.
drv action --do focus --node-id "$row_id" >"$OUT/select.log" 2>&1 \
  || setup_fail "the run's row could not take focus (driver: $(tr -d '\n' <"$OUT/select.log" | cut -c1-160))"
drv key --chord Enter --node-id "$row_id" >"$OUT/enter.log" 2>&1 \
  || setup_fail "Enter on the run's row was not accepted (driver: $(tr -d '\n' <"$OUT/enter.log" | cut -c1-160))"
drv wait-for --name "Run details" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the run drawer (Run details) never opened after Enter on the row"
# The drawer's button may carry its label as its automation name or as its text, so look for either, visible, any type.
take_id=""
for _ in $(seq 1 30); do
  take_id="$( { drv search --name "Take over" --limit 50; drv search --text "Take over" --limit 50; } 2>/dev/null \
    | jq -rs '[.[].matches[]? | select(.visible)][0].id // empty')"
  [ -n "$take_id" ] && break
  sleep 1
done
[ -n "$take_id" ] || setup_fail "the run drawer shows no Take over button for the live agent run"
drv screenshot --out "$OUT/before-click.png" >/dev/null 2>&1
before="$(takeovers_by_ui)"

# 4. Press it once. Within 10 s the ledger must hold ONE new takeover accepted from human:ui.
drv click --node-id "$take_id" >/dev/null 2>&1 || setup_fail "the Take over click was not accepted by the driver"
sent=0
for _ in $(seq 1 20); do
  now="$(takeovers_by_ui)"
  if [ "${now:-0}" -gt "${before:-0}" ]; then sent=1; break; fi
  sleep 0.5
done
drv screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$sent" = 1 ] || fail "no takeover from human:ui (Take over in the run inspector sent nothing; screenshots in $OUT)"
sleep 1
now="$(takeovers_by_ui)"
[ "$((now - before))" = 1 ] || fail "one press sent $((now - before)) takeovers from human:ui, not one (screenshots in $OUT)"

# 5. The outcome or a refusal is on screen: the viewer's words (opened, no viewer, failed, unconfirmed) or the refusal and its code.
shown=0
for text in "opened in" "did not confirm" "No terminal was opened" "viewer failed" "No viewer is set up" "didn't say whether a terminal opened" "can't take over" "refused" "SND-"; do
  if drv wait-for --type TextBlock --text "$text" --state visible --wait-ms 1500 >/dev/null 2>&1; then shown=1; break; fi
done
[ "$shown" = 1 ] || fail "no viewer outcome or refusal shown after Take over (screenshots in $OUT)"

echo "PASS: Take over in the run inspector sent one takeover as human:ui and the app showed the outcome (screenshots in $OUT)"
