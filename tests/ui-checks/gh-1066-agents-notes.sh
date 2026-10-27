#!/usr/bin/env bash
# tests/ui-checks/gh-1066-agents-notes.sh — frozen regression check for issue #1066 (UX pass A-18, Agents notes).
# Agents page: a session row's name must not end in a dangling hyphen ("pid-411-"), and a problem line must not show the raw
# code ("SND-SESSION-003 ...") as plain text. It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded
# fixture home with the session fixture. Takes about a minute.
#   bash tests/ui-checks/gh-1066-agents-notes.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/gh-1066-agents-notes-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/gh-1066-agents-notes-driver-XXXXXX")" || exit 2
OUT="${UXCHECK_OUT:-$(mktemp -d "$TMP_ROOT/gh-1066-agents-notes-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
# A setup miss keeps a screenshot of what the app showed, so the next miss can be read.
setup_fail() {
  "$DRIVER" screenshot --out "$OUT/setup-fail.png" >/dev/null 2>&1 || true
  echo "gh-1066-agents-notes: setup failed: $* (screenshot in $OUT)" >&2
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

# 1. The control plane, then the Agents fixture (its /proc tree holds pid 411, a session with no id of its own: a pid-411-<ticks> id).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" sessions up >"$OUT/sessions-up.log" 2>&1 || setup_fail "sindri-ui-fixture sessions up (see $OUT/sessions-up.log)"

# 2. The real app, drivable, on the fixture's control plane (this checkout's debug build unless AW_SINDRI_UI names a binary).
if [ -z "${AW_SINDRI_UI:-}" ]; then
  dotnet build "$ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
if [ -z "${DISPLAY:-}" ]; then
  source "$ROOT/tests/ui-checks/xvfb-lib.sh"
  xvfb_start || setup_fail "no Xvfb display could be started"
fi
bash "$ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
timeout 120 "$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# 3. Open the Agents page and wait for the session rows.
timeout 120 "$DRIVER" click --type ListBoxItem --name Agents >"$OUT/nav.log" 2>&1 \
  || setup_fail "the Agents nav item was not clicked (driver: $(tr -d '\n' <"$OUT/nav.log" | cut -c1-160))"
names() { timeout 120 "$DRIVER" search --type "$1" --limit 500 2>"$OUT/search-$1.err" | jq -r ".. | objects | select(has(\"name\")) | .name // empty" 2>/dev/null; }
# The rows come from the fixture's /proc tree after the collector's first pass: poll for the pid-411 row.
found=0
for _ in $(seq 1 60); do
  if names Button | grep -q "pid-411"; then found=1; break; fi
  sleep 1
done
[ "$found" = 1 ] || setup_fail "no row for the pid-411 session on the Agents page (see $OUT/app.log, $OUT/search-Button.err)"

# 4. Assert, in order: no dangling hyphen in a session name; no raw SND- code opening a problem line.
dangling="$(names Button | grep -E "pid-[0-9]+-([^0-9A-Za-z]|$)" | head -1)"
timeout 120 "$DRIVER" screenshot --out "$OUT/agents.png" >/dev/null 2>&1
[ -z "$dangling" ] || fail "a session name ends in a dangling hyphen: \"$dangling\" (screenshot in $OUT)"
raw="$(names Text | grep -E "^SND-[A-Z]+-[0-9]+ " | head -1)"
[ -z "$raw" ] || fail "the Agents page shows a raw error code as plain text: \"$raw\" (screenshot in $OUT)"

echo "PASS: no dangling hyphen and no raw code on the Agents page (screenshots in $OUT)"
