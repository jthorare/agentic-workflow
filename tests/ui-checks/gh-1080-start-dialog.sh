#!/usr/bin/env bash
# tests/ui-checks/gh-1080-start-dialog.sh — regression check for issue #1080 (UX-pass C-9): the Start workflow dialog (1) keeps its buttons
# where they were when an empty submit shows its error, (2) captions the second Item control (the list under the Item box), and (3) does not
# fill the focused workflow-type combo with the solid accent. It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a
# guarded fixture home (scripts/sindri-ui-fixture.sh through scripts/sindri-prove.sh) with one approved workflow and four items.
#   bash tests/ui-checks/gh-1080-start-dialog.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: GH1080_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui; default this checkout's debug build).
# Every driver call runs under `timeout 120`; a timeout is a setup failure.

set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER_SH="$ROOT/scripts/aw-ui-driver.sh"
driver() { timeout 120 "$DRIVER_SH" "$@"; }
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1080-start-dialog-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1080-start-dialog-driver-XXXXXX")" || exit 2
OUT="${GH1080_OUT:-$(mktemp -d "$TMP_ROOT/gh-1080-start-dialog-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() {
  driver screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1080-start-dialog: setup failed: $* (screenshots in $OUT)" >&2
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

# A dev recipe is not a shipped one: register and approve it as the owner (typing id and version) so the app has a workflow to list.
approve_dev_recipe() { # $1 recipe id
  local file version out
  file="$(grep -lx "id: $1" "$ROOT"/recipes/dev/*.yaml | head -1)"
  [ -n "$file" ] || setup_fail "no recipes/dev file for $1"
  version="$(sed -n 's/^version: *//p' "$file" | head -1)"
  out="$(printf 'approve %s %s\n' "$1" "$version" | "$PROVE" human recipe approve "$file" 2>&1)"
  printf '%s\n' "$out" | grep -qE '^(approved|already approved)' || setup_fail "recipe approve $1 was refused: $out"
}

# The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
# make the build the owner's current one), unless AW_SINDRI_UI names a binary.
start_app() {
  if [ -z "${AW_SINDRI_UI:-}" ]; then
    dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
      || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
    export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
  fi
  [ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
  unset DISPLAY
  # shellcheck source=xvfb-lib.sh
  source "$ROOT/tests/ui-checks/xvfb-lib.sh"
  xvfb_start || setup_fail "no Xvfb display could be started"
  bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
  APP_WRAPPER_PID=$!
  driver ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
  driver window --width 1280 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
}

# Every node of the window's tree whose text or name matches a regex: prints "text<TAB>x<TAB>y<TAB>w<TAB>h" lines. A search is capped
# loudly: a cut-off answer is a setup failure, never a quiet miss.
search_matches() { # $@ driver selector flags; prints the visible matches as compact JSON, one per line
  local out
  out="$(driver search --limit 5000 "$@" 2>/dev/null)" || return 1
  printf '%s' "$out" | jq -e '.total <= (.matches | length)' >/dev/null || setup_fail "a search for $* was cut off (total over the limit)"
  printf '%s' "$out" | jq -c '.matches[] | select(.visible == true)'
}

wait_text() { # $1 exact text, $2 tries (0.5 s each)
  local i
  for i in $(seq 1 "${2:-40}"); do
    [ -n "$(search_matches --text "$1" | head -1)" ] && return 0
    sleep 0.5
  done
  return 1
}

# Both Start workflow buttons (Home's, then the dialog's): prints the dialog's as one JSON object, or nothing.
dialog_start() {
  driver search --type Button --name "Start workflow" --limit 5000 2>/dev/null | jq -c 'if .total >= 2 then .matches | last else empty end'
}

run_check() {
  driver click --type ListBoxItem --name Home >/dev/null 2>&1 || true   # Home is the first page; the click only makes sure
  driver click --type Button --name "Start workflow" --index 0 >/dev/null 2>&1 || setup_fail "Home has no Start workflow button"
  local start="" i
  for i in $(seq 1 60); do   # the dialog is open and its recipes are read once its Start button is enabled
    start="$(dialog_start)"
    [ -n "$start" ] && [ "$(printf '%s' "$start" | jq -r '.enabled')" = true ] && break
    sleep 0.5
  done
  [ -n "$start" ] && [ "$(printf '%s' "$start" | jq -r '.enabled')" = true ] || setup_fail "the Start dialog never offered an enabled Start workflow"
  driver screenshot --out "$OUT/before-submit.png" >/dev/null 2>&1
  local y_before total
  y_before="$(printf '%s' "$start" | jq -r '.y')"
  total="$(driver search --type Button --name "Start workflow" --limit 5000 2>/dev/null | jq -r '.total')"

  # 1. Nothing chosen: Start workflow answers in the error line. The line's space is reserved, so the buttons stay where they were.
  driver click --type Button --name "Start workflow" --index $((total - 1)) >/dev/null 2>&1 || setup_fail "the dialog's Start workflow click was not accepted"
  wait_text "Choose a workflow type" 20 || setup_fail "an empty submit showed no 'Choose a workflow type' line"
  driver screenshot --out "$OUT/after-submit.png" >/dev/null 2>&1
  local y_after
  y_after="$(dialog_start | jq -r '.y')"
  [ "$y_before" = "$y_after" ] || fail "the Start workflow button moved from y=$y_before to y=$y_after when the empty-submit error appeared; the error's space is not reserved (screenshots in $OUT)"

  # 2. The second Item control (the list under the Item box) has a caption of its own.
  wait_text "Or pick one from the queue" 10 \
    || fail "the item list under the Item box has no caption of its own (screenshots in $OUT)"

  # 3. The focused workflow-type combo keeps the dark face: its left padding (clear of the text) is not the solid accent #5e6ad2.
  driver action --do focus --type ComboBox --name "Workflow type" >/dev/null 2>&1 || setup_fail "the workflow type combo could not take focus"
  sleep 1
  local combo cx cy
  combo="$(search_matches --type ComboBox --name "Workflow type" | head -1)"
  [ -n "$combo" ] || setup_fail "no visible Workflow type combo"
  cx="$(printf '%s' "$combo" | jq -r '(.x + 6) | floor')"
  cy="$(printf '%s' "$combo" | jq -r '(.y + .height / 2) | floor')"
  driver screenshot --out "$OUT/combo-focused.png" >/dev/null 2>&1 || setup_fail "no screenshot of the focused combo"
  local px
  px="$(python3 -I -c 'import sys
from PIL import Image
im = Image.open(sys.argv[1]).convert("RGB")
r, g, b = im.getpixel((int(sys.argv[2]), int(sys.argv[3])))
print(r, g, b)' "$OUT/combo-focused.png" "$cx" "$cy" 2>/dev/null)" || setup_fail "could not read the combo pixel (python3 with Pillow is needed)"
  read -r r g b <<<"$px"
  if [ "$r" -ge 82 ] && [ "$r" -le 106 ] && [ "$g" -ge 94 ] && [ "$g" -le 118 ] && [ "$b" -ge 198 ] && [ "$b" -le 226 ]; then
    fail "the focused Workflow type combo is filled solid accent (rgb $r $g $b at $cx,$cy; screenshots in $OUT)"
  fi

  echo "PASS: the empty-submit error reserves its space, the item list is captioned and the focused combo is not solid accent (screenshots in $OUT)"
}

prepare() {
  approve_dev_recipe workflow.dev-desk-round-trip
  bash "$FIXTURE" items >/dev/null 2>&1 || setup_fail "sindri-ui-fixture items"
}

bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
prepare
start_app
run_check
