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
unset TMUX DISPLAY

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
drv() { timeout 120 "$DRIVER" "$@"; }
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1004-takeover: setup failed: $* (screenshot in $OUT)" >&2
  exit 2
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
# An Xvfb this check owns and stops by pid on exit (xvfb-run leaves its Xvfb behind when a timeout kills the check).
source "$ROOT/tests/ui-checks/xvfb-lib.sh"
xvfb_start || setup_fail "no Xvfb display could be started"
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
"$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# 4. Open the instance (its expanded state carries the Attach control); its chevron menu offers Take over. The top bar's pages are
#    ListBoxItems named by their label; the Workflows list has one title button per run, named "Open <title>, <workflow>".
"$DRIVER" click --type ListBoxItem --name Workflows >"$OUT/nav.log" 2>&1 \
  || setup_fail "the Workflows nav item was not clicked (driver: $(tr -d '\n' <"$OUT/nav.log" | cut -c1-160))"
# The page opens as the board, which holds only runs on a lane; the List toggle shows every run.
"$DRIVER" click --type ListBoxItem --name List >"$OUT/list.log" 2>&1 \
  || setup_fail "the List toggle was not clicked (driver: $(tr -d '\n' <"$OUT/list.log" | cut -c1-160))"
# The title button's text is in a child TextBlock; its NAME is "Open <title>, <workflow>". Poll a complete search (up to 60 s) for it.
open_id=""
for _ in $(seq 1 30); do
  buttons="$(drv search --type Button --limit 5000 2>/dev/null)" || buttons=""
  if [ -n "$buttons" ]; then
    [ "$(printf '%s' "$buttons" | jq '(.total // 0) > (.matches | length)')" = false ] || setup_fail "the button search is incomplete (more buttons than --limit 5000)"
    open_id="$(printf '%s' "$buttons" | jq -r '[.matches[] | select(.visible == true and ((.name // "") | startswith("Open Fixture agent run")))][0].id // empty')"
    [ -n "$open_id" ] && break
  fi
  sleep 2
done
[ -n "$open_id" ] || setup_fail "no visible Open Fixture agent run button on Workflows (see $OUT/app.log)"
drv click --node-id "$open_id" >/dev/null 2>&1 || setup_fail "the Open Fixture agent run click was not accepted"
"$DRIVER" wait-for --type Button --text "Watch the agent" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "no Watch the agent control on the instance (see $OUT/app.log)"
"$DRIVER" screenshot --out "$OUT/before-click.png" >/dev/null 2>&1
before="$("$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r')"
# Take over is a MenuItem in the chevron's flyout (AttachButton.axaml). The driver walks only the main window's visual tree, so the menu
# is findable only because a driven run draws popups inside the window (Composition.WithOverlayPopups). The chevron is the Button named
# "More actions for <address>" (its text "▾" is not unique on a page with other Attach controls); 
chevron_id="$(drv search --type Button --limit 5000 2>/dev/null | jq -r '[.matches[] | select(.visible == true and ((.name // "") | startswith("More actions for")))][0].id // empty')"
[ -n "$chevron_id" ] || setup_fail "no visible More actions chevron on the instance"
drv click --node-id "$chevron_id" >/dev/null 2>&1 || setup_fail "the chevron click was not accepted by the driver"
# A MenuItem is a HeaderedSelectingItemsControl, so the driver reports no text for it; what it reports is its automation name,
# "Take over <address>" (AttachButton.FillMenu). Matching the MenuItem by that name also keeps the Stage details "Take over" label out.
take_id=""
for _ in $(seq 1 10); do
  take_id="$(drv search --type MenuItem --limit 5000 2>/dev/null | jq -r '[.matches[] | select(.visible == true and ((.name // "") | startswith("Take over ")))][0].id // empty')"
  [ -n "$take_id" ] && break
  sleep 0.5
done
[ -n "$take_id" ] || setup_fail "the chevron menu shows no Take over (see $OUT/app.log)"
# Click invokes the item through its automation peer; if the peer offers no invoke, focus it and press Enter, as a keyboard user would.
if ! drv click --node-id "$take_id" >/dev/null 2>&1; then
  drv action --node-id "$take_id" --do focus >/dev/null 2>&1 && drv key --node-id "$take_id" --chord Enter >/dev/null 2>&1 \
    || setup_fail "the Take over item was neither clicked nor focused and entered by the driver"
fi

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
