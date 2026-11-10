#!/usr/bin/env bash
# tests/ui-checks/ui-audit-shots.sh — the full-app screenshot audit (2026-11-10): every page of the Sindri desktop app at two widths, per state.
#   STATE=empty    the fixture up with no data
#   STATE=typical  up + items + request + todos + resources + context up + projects + reviews up + workflows up
#   STATE=error    typical + fail (a service the build can't run) + stall (the views stall banner)
# Output: $OUT/<page>/<state>-<width>.png (OUT default a new temp dir). PAGES (space list of tab names, e.g. PAGES="Workflows Reviews" to reshoot those two) defaults to the nav tabs below.
# States the fixture can't make (Linear catch-up changes, connections) are not faked: the shots show what the app does with none.
#   bash tests/ui-checks/ui-audit-shots.sh   (STATE=...)   run guarded: scripts/run-test-guard.sh -- bash tests/ui-checks/ui-audit-shots.sh
# Env: STATE, PAGES, WIDTHS (default "1280 900"), OUT, AW_SINDRI_UI, SINDRI, AW_UI_DRIVER_BIN (a built aw-ui-driver).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ui-audit
STATE="${STATE:-typical}"
PAGES="${PAGES:-Home Work Projects Reviews Connections Agents Workflows Context System}"
WIDTHS="${WIDTHS:-1280 900}"
export AW_UI_DRIVER_BIN="${AW_UI_DRIVER_BIN:-$CTX_ROOT/artifacts/bin/Aw.UiDriver.Cli/debug/aw-ui-driver}"
[ -x "$AW_UI_DRIVER_BIN" ] || setup_fail "no aw-ui-driver at $AW_UI_DRIVER_BIN (heavy --light dotnet build src/UiDriver/Aw.UiDriver.Cli)"
d() { timeout 60 "$DRIVER" "$@"; }
# Waits (1 s apart, 60 s at most) until two reads of the window's tree in a row are identical, so a page still loading is not shot. Writes "unsettled: <label>" to missing.txt on a timeout and goes on.
settle() {
  local label="$1" prev="" cur i
  for i in $(seq 1 60); do
    cur="$(d tree 2>/dev/null | sha256sum)"
    [ -n "$prev" ] && [ "$cur" = "$prev" ] && return 0
    prev="$cur"
    sleep 1
  done
  echo "unsettled: $label" >>"$OUT/missing.txt"
}

bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "fixture up"
if [ "$STATE" != empty ]; then
  bash "$FIXTURE" items >"$OUT/items.log" 2>&1   # not --large: 30 more items + 12 requests took past 600 s
  bash "$FIXTURE" request "Which retry budget should sync use?" >>"$OUT/items.log" 2>&1
  bash "$FIXTURE" todos >>"$OUT/items.log" 2>&1
  bash "$FIXTURE" resources >>"$OUT/items.log" 2>&1
  bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1
  bash "$FIXTURE" projects >"$OUT/projects-up.log" 2>&1
  bash "$FIXTURE" reviews up >"$OUT/reviews-up.log" 2>&1     # after projects: it deletes the github:pr rows
  bash "$FIXTURE" workflows up >"$OUT/workflows-up.log" 2>&1
fi
if [ "$STATE" = error ]; then
  bash "$FIXTURE" fail >/dev/null 2>&1
  bash "$FIXTURE" stall >/dev/null 2>&1
fi
if [ -z "${AW_SINDRI_UI:-}" ]; then export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"; fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
setsid env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
d ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
sleep 4
d search --type ListBoxItem --limit 60 2>/dev/null | jq -r '.matches[] | .name // .text' >"$OUT/nav-tabs.txt"
for page in $PAGES; do
  mkdir -p "$OUT/$page"
  if [ "$page" != Home ]; then
    d click --type ListBoxItem --name "$page" >/dev/null 2>&1 || { echo "no tab: $page" >>"$OUT/missing.txt"; continue; }
  else
    d click --type ListBoxItem --name Home >/dev/null 2>&1 || true
  fi
  settle "$page"
  for w in $WIDTHS; do
    d window --width "$w" --height 900 >/dev/null 2>&1
    settle "$page $w"
    d screenshot --out "$OUT/$page/$STATE-$w.png" >/dev/null 2>&1 || echo "no shot: $page $w" >>"$OUT/missing.txt"
  done
  if [ "$page" = Work ]; then   # the Linear tab of Work, as its own page dir
    d click --name Linear >/dev/null 2>&1 || { echo "no Linear tab" >>"$OUT/missing.txt"; continue; }
    mkdir -p "$OUT/Work-Linear"
    for w in $WIDTHS; do
      d window --width "$w" --height 900 >/dev/null 2>&1; settle "Work-Linear $w"
      d screenshot --out "$OUT/Work-Linear/$STATE-$w.png" >/dev/null 2>&1 || echo "no shot: Work-Linear $w" >>"$OUT/missing.txt"
    done
  fi
done
echo "shots in $OUT"; find "$OUT" -name '*.png' | sort
