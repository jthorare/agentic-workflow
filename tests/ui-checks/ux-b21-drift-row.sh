#!/usr/bin/env bash
# tests/ui-checks/ux-b21-drift-row.sh — frozen regression check for the UX-pass finding B-21: when the live profile drifts from the approved
# one, Home's Needs-you panel must carry a profile row (like a failed service has its failure row), within 8 s of the pill saying so.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh): `drift` edits the live profile so it no longer matches its approval; the pill reads Degraded.
#   bash tests/ui-checks/ux-b21-drift-row.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB21_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b21-drift-row-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b21-drift-row-driver-XXXXXX")" || exit 2
OUT="${UXB21_OUT:-$(mktemp -d "$TMP_ROOT/ux-b21-drift-row-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b21-drift-row: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane with a drifted profile (its approval no longer matches).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" drift >/dev/null 2>&1 || setup_fail "sindri-ui-fixture drift"

# The real app, drivable, on the fixture's control plane: this checkout's debug build (sindri-app.sh's publish would
# make the build the owner's current one), unless AW_SINDRI_UI names a binary.
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
timeout 130 "$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# ---- helpers: every driver call runs under timeout 120; a timeout is a setup failure, never a FAIL ----
drv_to() {  # drv_to <outfile> <verb and flags...>
  local out="$1"; shift
  timeout 120 "$DRIVER" "$@" >"$out" 2>>"$OUT/driver.err"
  local rc=$?
  if [ "$rc" = 124 ]; then setup_fail "driver call timed out: $*"; fi
  return "$rc"
}
nodes() {  # nodes <outfile> <search flags...>: the matches of a search, to a file
  local f="$1"; shift
  drv_to "$f" search --limit 500 "$@" || setup_fail "driver search failed: $*"
}
visible_n() { jq "[.matches[] | select(.visible)] | length" "$1"; }
first_text() { jq -r "[.matches[] | select(.visible)][0].text // empty" "$1"; }
poll() {  # poll <seconds> <command...>: true as soon as the command succeeds
  local end=$((SECONDS + $1)); shift
  while :; do
    if "$@"; then return 0; fi
    if [ "$SECONDS" -ge "$end" ]; then return 1; fi
    sleep 0.5
  done
}
shot() { drv_to /dev/null screenshot --out "$OUT/$1.png" || true; }


# 2. The pill says the profile changed.
drv_to /dev/null wait-for --name HealthPillText --state textContains --expect-text Degraded --wait-ms 120000 || setup_fail "the health pill never read Degraded after drift (see $OUT/app.log)"

# 3. Within 8 s a Needs-you row (a text inside a NeedsYouRow, by node id) must name the profile.
profile_row() {
  nodes "$OUT/rows.json" --type NeedsYouRow
  nodes "$OUT/texts.json" --type TextBlock
  jq -e -n --slurpfile rows "$OUT/rows.json" --slurpfile texts "$OUT/texts.json" '
    [ $rows[0].matches[] | select(.visible) ] as $r
    | [ $texts[0].matches[] | select(.visible) | . as $t | select(($t.text // "") | test("profile"; "i"))
        | select([ $r[] | . as $row | select($t.id | startswith($row.id + "/")) ] | length > 0) ] | length > 0' >/dev/null
}
if ! poll 8 profile_row; then
  shot no-profile-row
  fail "the pill reads Degraded for the changed profile but Needs you has no profile row (screenshots in $OUT)"
fi

echo "PASS: Needs you carries the profile row after a drift (screenshots in $OUT)"
