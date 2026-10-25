#!/usr/bin/env bash
# tests/ui-checks/ux-a3-item-close.sh — frozen regression check for the UI/UX pass finding A-3: the item sheet's "Close item"
# control (a CommandControl) must be invokable through the UI driver (an automation peer) and must ask before it closes
# (Confirm / Keep, as Cancel job does). It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded
# fixture home (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh): it opens an item's sheet, clicks Close item,
# expects the click to be accepted and a confirm to show with NO item.state-changed in the ledger, then confirms and expects
# item.state-changed by human:ui.
#   bash tests/ui-checks/ux-a3-item-close.sh      exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXA3_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/uxa3-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/uxa3-driver-XXXXXX")" || exit 2
OUT="${UXA3_OUT:-$(mktemp -d "$TMP_ROOT/uxa3-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "ux-a3-item-close: setup failed: $* (screenshot $OUT/setup-fail.png)" >&2
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

# 3. Work page: the top bar's page list is a ListBox whose items are named by their label ("Work", shell NavEntry); the page
#    shows one row per item, each with a title button named "Open ITM-…" (ItemRowViewModel.OpenName).
"$DRIVER" click --type ListBoxItem --name Work >"$OUT/nav.log" 2>&1 \
  || setup_fail "the Work nav item was not clicked (driver: $(tr -d '\n' <"$OUT/nav.log" | cut -c1-160))"
# --contains matches the text an element shows (the row title), not its accessible name: wait for the exact name.
open=(--type Button --name "Open $item")
"$DRIVER" wait-for "${open[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the Work page shows no \"Open $item\" button after the nav click"
"$DRIVER" click "${open[@]}" >/dev/null 2>&1 || setup_fail "the Open $item click was not accepted"
# The sheet is up when its Close item button is (a Button named "<label> <id>", ControlViewModel.Name): readiness is the button a user
# and a screen reader see. The click below goes to the CommandControl itself, the first action of the sheet (CloseItem:done).
ready=(--type Button --name "Close item $item")
"$DRIVER" wait-for "${ready[@]}" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the item sheet never showed its \"Close item $item\" button (see $OUT/app.log)"
close=(--type CommandControl --index 0)
"$DRIVER" screenshot --out "$OUT/sheet.png" >/dev/null 2>&1

changed() { "$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r' | grep -o 'item.state-changed' | wc -l; }
before="$(changed)"

# 4. The click must be accepted (an automation peer), and must ask, not close.
"$DRIVER" click "${close[@]}" >"$OUT/click.log" 2>&1 || fail "Close item has no automation peer (driver: $(tr -d '\n' <"$OUT/click.log" | cut -c1-160))"
sleep 2
"$DRIVER" screenshot --out "$OUT/after-click.png" >/dev/null 2>&1
[ "$(changed)" = "$before" ] || fail "Close item closed with no confirm (screenshots in $OUT)"

# 5. Confirm: now the item closes, by the UI.
confirm=(--type Button --contains "Confirm")
"$DRIVER" click "${confirm[@]}" >/dev/null 2>&1 || fail "Close item closed with no confirm: no Confirm button after the click (screenshots in $OUT)"
sent=0
for _ in $(seq 1 10); do
  after="$("$PROVE" human ledger --json --item "$item" --max 1000 2>&1 | tr -d '\r')"
  if [ "$(printf '%s' "$after" | grep -o 'item.state-changed' | wc -l)" -gt "$before" ] && printf '%s' "$after" | grep -q 'human:ui'; then sent=1; break; fi
  sleep 0.5
done
"$DRIVER" screenshot --out "$OUT/after-confirm.png" >/dev/null 2>&1
[ "$sent" = 1 ] || fail "no item.state-changed by human:ui 5 s after Confirm (screenshots in $OUT)"

echo "PASS: Close item was invokable, asked first, and closed as human:ui after Confirm (screenshots in $OUT)"
