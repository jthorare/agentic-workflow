#!/usr/bin/env bash
# tests/ui-checks/ux-b15-service-why.sh — regression check for the UX-pass finding B-15: on System > Services a failed service says why in its
# row (the run's SND code) and every service has a "What it does" sentence. It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md)
# against a guarded fixture home with a service the build cannot run ('sindri-ui-fixture.sh fail': a service named broken).
#   bash tests/ui-checks/ux-b15-service-why.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui; default this checkout's debug build).

set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b15-service-why-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b15-service-why-driver-XXXXXX")" || exit 2
OUT="${UX_OUT:-$(mktemp -d "$TMP_ROOT/ux-b15-service-why-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "ux-b15-service-why: setup failed: $* (screenshots in $OUT)" >&2
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

prepare() {
  bash "$FIXTURE" fail >/dev/null 2>&1 || setup_fail "sindri-ui-fixture fail"
}

run_check() {
  goto_page "System"
  local i
  for i in $(seq 1 40); do
    "$DRIVER" click --type RadioButton --contains Services >/dev/null 2>&1 && break
    sleep 0.5
  done
  wait_tree_match 'Last run failed' 120 >/dev/null || setup_fail "no service shows 'Last run failed' (the broken service never ran)"
  "$DRIVER" screenshot --out "$OUT/services.png" >/dev/null 2>&1

  # The broken service's row (a Grid named by the service): every text in it.
  local texts
  texts="$("$DRIVER" tree --depth 60 2>/dev/null | jq -r '[.. | objects | select(.name? == "broken" and .type? == "Grid" and .visible != false)][0]
      | [.. | objects | (.text // .name // "")] | map(select(length > 0)) | .[]')"
  [ -n "$texts" ] || setup_fail "no visible row for the broken service"

  printf '%s\n' "$texts" | grep -qE '[A-Z]{2,}-[A-Z]+-[0-9]+' \
    || fail "the failed service 'broken' says only 'Last run failed': its row names no code or reason for the failure (screenshots in $OUT)"
  printf '%s\n' "$texts" | grep -qE '\.$' \
    || fail "the service 'broken' has no 'What it does' sentence (screenshots in $OUT)"

  echo "PASS: a failed service's row says why and every service says what it does (screenshots in $OUT)"
}

prepare
start_app
run_check
