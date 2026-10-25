#!/usr/bin/env bash
# tests/ui-checks/ux-a3b-item-close.sh — frozen regression check for the UI/UX pass finding A-3: the item sheet's "Close item"
# control (a CommandControl) must be invokable through the UI driver (an automation peer) and must ask before it closes
# (Confirm / Keep, as Cancel job does). It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded
# fixture home (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh): it opens an item's sheet, clicks Close item,
# expects the click to be accepted and a confirm to show with NO item.state-changed in the ledger, then confirms and expects
# item.state-changed by human:ui.
# Version b: no index selection. The sheet's control is the VISIBLE one named "Close item <id>" (its Button; the CommandControl is the visible
# ancestor of that Button), found by search and picked by node id, because `--type CommandControl --index 0` can hit an invisible control elsewhere.
# Every driver call runs under `timeout 120`; a timeout is a setup failure.
#   bash tests/ui-checks/ux-a3b-item-close.sh      exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXA3B_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER_SH="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/uxa3b-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/uxa3b-driver-XXXXXX")" || exit 2
OUT="${UXA3B_OUT:-$(mktemp -d "$TMP_ROOT/uxa3b-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER_SH" "$@"; }
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "ux-a3b-item-close: setup failed: $* (screenshot $OUT/setup-fail.png)" >&2
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

# 1. The control plane and the fixture's items (all open, so each sheet offers Close item and Close as cancelled).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" items >/dev/null 2>&1 || setup_fail "sindri-ui-fixture items"
item="$("$PROVE" human items --json 2>&1 | tr -d '\r' | grep -o 'ITM-[0-9a-f]*' | head -1)"
[ -n "$item" ] || setup_fail "no item in the fixture"

# 2. The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
#    make the build the owner's current one), unless AW_SINDRI_UI names a binary.
# Warm the driver CLI first (its first call may publish it, which can outlast the 120 s a driver call gets).
"$DRIVER_SH" ready --wait-ms 1 >/dev/null 2>&1 || true
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
drv ready --wait-ms 110000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# visible_ids <driver selector flags...>: the node ids of the VISIBLE matches, one per line (a search under the timeout, filtered with jq).
visible_ids() {
  drv search --limit 200 "$@" 2>/dev/null | jq -r '.matches[] | select((.visible // .Visible) == true) | (.id // .Id)' 2>/dev/null
}

# 3. Work page: the top bar's page list is a ListBox whose items are named by their label ("Work", shell NavEntry); the page
#    shows one row per item, each with a title button named "Open ITM-..." (ItemRowViewModel.OpenName).
drv click --type ListBoxItem --name Work >"$OUT/nav.log" 2>&1 \
  || setup_fail "the Work nav item was not clicked (driver: $(tr -d '\n' <"$OUT/nav.log" | cut -c1-160))"
open=(--type Button --name "Open $item")
drv wait-for "${open[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the Work page shows no \"Open $item\" button after the nav click"
open_id="$(visible_ids "${open[@]}" | head -1)"
[ -n "$open_id" ] || setup_fail "no visible \"Open $item\" button"
drv click --node-id "$open_id" >/dev/null 2>&1 || setup_fail "the Open $item click was not accepted"

# The sheet is up when its visible Close item button is (a Button named "<label> <id>", ControlViewModel.Name).
ready=(--type Button --name "Close item $item")
drv wait-for "${ready[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the item sheet never showed its \"Close item $item\" button (see $OUT/app.log)"
button_id="$(visible_ids "${ready[@]}" | head -1)"
[ -n "$button_id" ] || setup_fail "no visible \"Close item $item\" button"
# The CommandControl around it: the visible CommandControl whose node id is the longest proper prefix of the button's.
control_id=""
while read -r candidate; do
  case "$button_id" in "$candidate"/*) [ "${#candidate}" -gt "${#control_id}" ] && control_id="$candidate" ;; esac
done < <(visible_ids --type CommandControl)
[ -n "$control_id" ] || setup_fail "the \"Close item $item\" button is in no visible CommandControl"
drv screenshot --out "$OUT/sheet.png" >/dev/null 2>&1

changed() { "$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r' | grep -o 'item.state-changed' | wc -l; }
before="$(changed)"

# 4. The click on the control must be accepted (an automation peer), and must ask, not close.
timeout 120 "$DRIVER_SH" click --node-id "$control_id" >"$OUT/click.log" 2>&1
rc=$?
[ "$rc" != 124 ] || setup_fail "the Close item click timed out"
[ "$rc" = 0 ] || fail "Close item has no automation peer (driver: $(tr -d '\n' <"$OUT/click.log" | cut -c1-160))"
sleep 2
drv screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$(changed)" = "$before" ] || fail "Close item closed with no confirm (screenshots in $OUT)"

# 5. Confirm: a visible Confirm button named "Confirm: Close item <id>?" appears; clicking it closes the item, by the UI.
confirm=(--type Button --name "Confirm: Close item $item?")
confirm_id="$(visible_ids "${confirm[@]}" | head -1)"
[ -n "$confirm_id" ] || fail "Close item closed with no confirm: no Confirm button after the click (screenshots in $OUT)"
drv click --node-id "$confirm_id" >/dev/null 2>&1 || fail "the Confirm click was not accepted (screenshots in $OUT)"
sent=0
for _ in $(seq 1 10); do
  after="$("$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r')"
  if [ "$(printf '%s' "$after" | grep -o 'item.state-changed' | wc -l)" -gt "$before" ] && printf '%s' "$after" | grep -q 'human:ui'; then sent=1; break; fi
  sleep 0.5
done
drv screenshot --out "$OUT/after-confirm.png" >/dev/null 2>&1
[ "$sent" = 1 ] || fail "no item.state-changed by human:ui 5 s after Confirm (screenshots in $OUT)"

echo "PASS: Close item was invokable, asked first, and closed as human:ui after Confirm (screenshots in $OUT)"
