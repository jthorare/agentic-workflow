#!/usr/bin/env bash
# tests/ui-checks/ux-b10-system-overview.sh — regression check for the UX-pass finding B-10: the System overview's Actions card shows no
# internal stage code, and every command to copy keeps its Copy button inside the card (the command wraps or scrolls instead of clipping it).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home.
#   bash tests/ui-checks/ux-b10-system-overview.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui; default this checkout's debug build).

set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b10-system-overview-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b10-system-overview-driver-XXXXXX")" || exit 2
OUT="${UX_OUT:-$(mktemp -d "$TMP_ROOT/ux-b10-system-overview-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "ux-b10-system-overview: setup failed: $* (screenshots in $OUT)" >&2
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
  if [ -n "${DISPLAY:-}" ]; then
    bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
  else
    xvfb-run -a -s "-screen 0 1600x1000x24" bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
  fi
  APP_WRAPPER_PID=$!
  "$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
  "$DRIVER" window --width 1280 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
}

# The top bar's page tabs are list items named by their label.
goto_page() { # $1 label
  local i
  for i in $(seq 1 40); do
    "$DRIVER" click --type ListBoxItem --name "$1" >/dev/null 2>&1 && return 0
    sleep 0.5
  done
  setup_fail "no top-bar tab named '$1'"
}

# Every node of the window's tree whose text or name matches a regex: prints "text<TAB>x<TAB>y<TAB>w<TAB>h" lines.
tree_matches() { # $1 jq regex
  "$DRIVER" tree --depth 60 2>/dev/null | jq -r --arg re "$1" '[.. | objects | select(.type? != null) | select(((.text // .name // "") | tostring) | test($re)) | select(.visible != false)]
    | .[] | [(.text // .name), .x, .y, .width, .height] | @tsv'
}

wait_tree_match() { # $1 jq regex, $2 tries (0.5 s each); prints the first match
  local i hit
  for i in $(seq 1 "${2:-40}"); do
    hit="$(tree_matches "$1" | head -1)"
    [ -n "$hit" ] && { printf '%s\n' "$hit"; return 0; }
    sleep 0.5
  done
  return 1
}

bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"

prepare() { :; }

run_check() {
  goto_page "System"
  local card="" i
  for i in $(seq 1 60); do   # the Actions card draws once the catalog is read
    card="$("$DRIVER" tree --depth 60 2>/dev/null | jq -c '[.. | objects | select(.name? == "ActionsSection" and .visible != false)][0] // empty')"
    [ -n "$card" ] && [ -n "$(tree_matches '^Copy: ' | head -1)" ] && break
    sleep 0.5
  done
  [ -n "$card" ] || setup_fail "the System overview shows no Actions card"
  [ -n "$(tree_matches '^Copy: ' | head -1)" ] || setup_fail "the Actions card lists no command to copy"
  "$DRIVER" screenshot --out "$OUT/system-overview.png" >/dev/null 2>&1

  # Internal stage codes are not words for a person.
  local code
  code="$(tree_matches 'arrives in |CO-[0-9]' | head -1 | cut -f1)"
  [ -z "$code" ] || fail "the System overview shows an internal stage code: '$code' (screenshots in $OUT)"

  # A command to copy wraps (or scrolls) inside its card: every Copy button is inside the Actions card and the window.
  local right
  right="$(printf '%s' "$card" | jq -r '.x + .width')"
  local over
  over="$("$DRIVER" tree --depth 60 2>/dev/null | jq -r --argjson right "$right" '[.. | objects | select(.type? == "Button") | select((.name // "") | startswith("Copy: ")) | select(.visible != false)
      | select((.x + .width) > ($right + 1)) | "\(.name) ends at x=\(.x + .width)"] | first // empty')"
  [ -z "$over" ] || fail "a Copy button lies outside the Actions card (card ends at x=$right): $over; the command line is clipped and its Copy is out of reach (screenshots in $OUT)"

  echo "PASS: no stage codes, and every command's Copy button is inside the Actions card (screenshots in $OUT)"
}

prepare
start_app
run_check
