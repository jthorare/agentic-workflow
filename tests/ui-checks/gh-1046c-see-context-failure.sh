#!/usr/bin/env bash
# tests/ui-checks/gh-1046c-see-context-failure.sh — frozen regression check for the failure half of GitHub issue #1046: when See its context
# can't read the item's context, the Context page must say so, with the item and the reason, instead of staying silently on the whole map.
# Same setup as gh-1046b-see-context.sh (guarded fixture home, `context up`, `line-run`, the real sindri-ui under its own Xvfb, the run
# opened from the Workflows list, its first stage or step selected). Then, with the inspector showing See its context, the check stops
# the control plane (`sindri-ui-fixture.sh down`, sindri stop as the owner) and presses the button: the app can't reach the API, so the
# item, scope and map reads all fail. It expects a VISIBLE note in the Context page's head naming the item and that the read failed
# ("Could not read the context of <item>: <reason>"). The loading line ("Reading the context of <item>…") is not asserted: with the API
# down it lasts one failed request, far shorter than a driver poll, so the driver can't catch it reliably.
# Every driver call is bounded by `timeout 120`; it starts its own Xvfb (tests/ui-checks/xvfb-lib.sh) and never uses the caller's DISPLAY.
#   bash tests/ui-checks/gh-1046c-see-context-failure.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1046_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX
unset DISPLAY

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh1046-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh1046-driver-XXXXXX")" || exit 2
OUT="${GH1046_OUT:-$(mktemp -d "$TMP_ROOT/gh1046-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
drv() { timeout 120 "$DRIVER" "$@"; }
setup_fail() {
  drv screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1046c-see-context-failure: setup failed: $* (screenshot in $OUT)" >&2
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

# 1. The control plane, the context map of a repo, and a line run on a new item of that repo.
bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" line-run >"$OUT/line-run.log" 2>&1 || setup_fail "sindri-ui-fixture line-run (see $OUT/line-run.log)"
item="$(grep -o 'ITM-[A-Za-z0-9]*' "$OUT/line-run.log" | tail -1)"
[ -n "$item" ] || setup_fail "line-run printed no item id (see $OUT/line-run.log)"

# 2. The real app, drivable, on the fixture's control plane: this checkout's debug build, unless AW_SINDRI_UI names a binary.
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
source "$ROOT/tests/ui-checks/xvfb-lib.sh"
xvfb_start "2200x1400x24" || setup_fail "no Xvfb display could be started"
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
drv ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
drv window --width 1400 --height 1000 >/dev/null 2>&1 || setup_fail "could not size the window"

# 3. Workflows page, List view -> the line run's page (its row's title button is named "Open <instance>, <workflow>").
drv click --type ListBoxItem --name Workflows >"$OUT/nav.log" 2>&1 || setup_fail "no Workflows nav item (see $OUT/nav.log)"
# The page opens on the Board, where a run is a card; the List view's row has the "Open <instance>, <workflow>" button.
drv click --type ListBoxItem --name List >"$OUT/list.log" 2>&1 || setup_fail "no List view toggle on the Workflows page (see $OUT/list.log)"
open_name=""
for _ in $(seq 1 60); do
  drv search --type Button --limit 500 >"$OUT/workflows-tree.json" 2>/dev/null
  open_name="$(jq -r '[.. | objects | select((.type // "") == "Button" and (.visible // false)) | .name // empty | select(startswith("Open ") and contains(", "))] | first // empty' "$OUT/workflows-tree.json")"
  [ -n "$open_name" ] && break
  sleep 1
done
[ -n "$open_name" ] || { drv tree --depth 20 >"$OUT/workflows-tree-full.json" 2>/dev/null; setup_fail "the Workflows page shows no run to open (buttons in $OUT/workflows-tree.json, tree in $OUT/workflows-tree-full.json)"; }
drv click --type Button --name "$open_name" >/dev/null 2>&1 || setup_fail "the click on \"$open_name\" was not accepted"

# 4. Select the first step (a stage card when the stage has no steps drawn: "Scope, Stage 1 of 3, Running, Sub-workflow"), so the inspector shows; its actions include See its context (the run has an item).
step=""
for _ in $(seq 1 40); do
  drv search --type Button --limit 500 >"$OUT/run-tree.json" 2>/dev/null
  step="$(jq -r '[.. | objects | select((.type // "") == "Button") | .name // empty | select(contains(" stage, step ") or contains(", Stage "))] | first // empty' "$OUT/run-tree.json")"
  [ -n "$step" ] && break
  sleep 1
done
[ -n "$step" ] || setup_fail "the run page shows no step or stage (tree in $OUT/run-tree.json)"
drv click --type Button --name "$step" >/dev/null 2>&1 || setup_fail "the click on step \"$step\" was not accepted"
drv wait-for --type Button --name "See its context" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the inspector offers no \"See its context\" (the run has item $item)"
drv screenshot --out "$OUT/inspector.png" >/dev/null 2>&1

# 5. Take the API away, then press it: the Context page must say why it can't open the item's context.
bash "$FIXTURE" down >"$OUT/down.log" 2>&1 || setup_fail "could not stop the control plane (see $OUT/down.log)"
drv click --type Button --name "See its context" >/dev/null 2>&1 || setup_fail "the See its context click was not accepted by the driver"
shown=""
for _ in $(seq 1 40); do
  # Every page is in the tree and the Context page comes after the others: the search must be whole, and a cut-off one is a broken
  # setup, never a FAIL.
  drv search --limit 5000 >"$OUT/context-tree.json" 2>/dev/null
  jq -e '.total <= (.matches | length)' "$OUT/context-tree.json" >/dev/null 2>&1 \
    || setup_fail "the driver's search was cut off or unreadable ($(jq -c '[.total, (.matches | length)]' "$OUT/context-tree.json" 2>/dev/null) total/returned; see $OUT/context-tree.json)"
  # Only a VISIBLE node in the Context page's own head panel (the title's panel) counts: the item's id is also on pages behind it.
  if jq -e --arg item "$item" '([.. | objects | select((.name // "") == "ContextTitle") | .id] | first // empty) as $title
      | ($title | sub("/[^/]*$"; "")) as $panel
      | [.. | objects | select(.visible == true and ((.id // "") | startswith($panel + "/"))
          and ((((.name // "") | contains("Could not read the context of " + $item)) or ((.text // "") | contains("Could not read the context of " + $item)))))] | length > 0' "$OUT/context-tree.json" >/dev/null 2>&1; then
    shown=1; break
  fi
  sleep 1
done
drv screenshot --out "$OUT/context.png" >/dev/null 2>&1
if [ -z "$shown" ]; then
  head_text="$(jq -r '[.. | objects | select((.name // "") == "ContextHead" and .visible == true) | .text // .name // empty] | first // "(no head)"' "$OUT/context-tree.json")"
  fail "See its context with the control plane down shows no \"Could not read the context of $item\" note in the Context page's head (head \"$head_text\"); the page stays silent about why"
fi

echo "PASS: See its context said why $item's context could not be read (screenshots in $OUT)"
