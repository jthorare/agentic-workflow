#!/usr/bin/env bash
# tests/ui-checks/ux-b2-stale-decision.sh — frozen regression check for the UX-pass finding B-2: when a decision was decided elsewhere and
# the Decide sheet is then refused (SND-ITEM-409), the sheet says it ONCE, calmly, in words: the decision id appears at most once in the
# lines the sheet shows and no raw actor name (human:prover) leaks. It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md)
# against a guarded fixture home: a session files a request (a decision), the check opens its sheet, decides it at the CLI
# (sindri-ui-fixture.sh decide) and presses Accept in the sheet at once (the race the finding reports; it retries on a new decision
# up to 8 times when the sheet had already disabled itself).
#   bash tests/ui-checks/ux-b2-stale-decision.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB2_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b2-stale-decision-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b2-stale-decision-driver-XXXXXX")" || exit 2
OUT="${UXB2_OUT:-$(mktemp -d "$TMP_ROOT/ux-b2-stale-decision-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b2-stale-decision: setup failed: $*" >&2; exit 2; }
fail() { echo "FAIL: $*" >&2; exit 1; }
# A driver call that timed out is a broken setup, never a FAIL; this also runs inside $(...), so it tells the main shell to stop.
trap 'exit 2' USR1
timed_out() { echo "ux-b2-stale-decision: setup failed: aw-ui-driver $1 timed out after 120 s" >&2; kill -USR1 $$; exit 2; }

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

# 1. The control plane (decisions are filed per attempt, below, once the app is up).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"

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
timeout 120 "$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"

# Driver helpers: every aw-ui-driver call is bounded by timeout 120. nodes prints a search answer ({"matches":[...]}, never empty).
drv() { local rc; timeout 120 "$DRIVER" "$@"; rc=$?; [ "$rc" != 124 ] || timed_out "$1"; return "$rc"; }
nodes() {
  local out rc
  out="$(timeout 120 "$DRIVER" search "$@" --limit 1000 2>/dev/null)"; rc=$?
  [ "$rc" != 124 ] || timed_out search
  [ -n "$out" ] || out='{"matches":[]}'
  printf '%s' "$out"
}
# The first match's text / left edge / right edge / width / top edge (empty when nothing matches).
text_of() { nodes "$@" | jq -r '.matches[0].text // empty'; }
left_of() { nodes "$@" | jq -r '.matches[0].x // empty'; }

# 2. Open a fresh decision sheet, decide it at the CLI and press Accept in the sheet before it disables itself.
sheet_left() { left_of --name DecisionSheetScroll; }
raced=0
DEC=""
for attempt in $(seq 1 8); do
  resp="$(bash "$FIXTURE" request "Check the fixture export (try $attempt)" 2>/dev/null)" || setup_fail "sindri-ui-fixture request"
  DEC="$(printf '%s' "$resp" | jq -r '.decisionId // empty')"
  [ -n "$DEC" ] || setup_fail "the request answered without a decisionId: $resp"
  open=""
  for _ in $(seq 1 60); do
    open="$(nodes --type Button | jq -r --arg d "$DEC" '[.matches[] | select(.visible) | .name // empty | select(startswith("Open ") and contains($d))][0] // empty')"
    [ -n "$open" ] && break
    sleep 1
  done
  [ -n "$open" ] || setup_fail "no Home row to open for $DEC (see $OUT/app.log)"
  drv click --type Button --name "$open" >/dev/null 2>&1 || setup_fail "the click on the Home row of $DEC was not accepted"
  drv wait-for --type TextBlock --text "Note, sent with your choice" --state visible --wait-ms 30000 >/dev/null 2>&1 \
    || setup_fail "the decision sheet of $DEC never opened (see $OUT/app.log)"
  # The sheet's Accept is the last of the buttons with that name (Home's row has one too, earlier in the tree).
  last="$(nodes --type Button --name "Accept $DEC" | jq -r '(.matches | length) - 1')"
  [ "$last" -ge 0 ] 2>/dev/null || setup_fail "no Accept $DEC button"
  bash "$FIXTURE" decide "$DEC" decline >/dev/null 2>&1 || setup_fail "sindri-ui-fixture decide $DEC decline"
  if drv click --type Button --name "Accept $DEC" --index "$last" >/dev/null 2>&1; then raced=1; break; fi
  # The sheet had already disabled itself (or the row moved): close it and try a new decision.
  drv key --chord Escape >/dev/null 2>&1
done
[ "$raced" = 1 ] || setup_fail "could not press Accept before the sheet disabled itself in 8 tries"

# 3. Let the refusal and the cleared line draw, then read what the sheet says about this decision.
sleep 4
drv screenshot --out "$OUT/refused.png" >/dev/null 2>&1
left="$(sheet_left)"
[ -n "$left" ] || setup_fail "the decision sheet is not in the tree"
lines="$(nodes --type TextBlock | jq -r --arg d "$DEC" --argjson left "$left" \
  '[.matches[] | select(.visible and .x >= $left) | .text // empty | select(contains($d) or test("ecided|eclined|ccepted"))] | join(" | ")')"
[ -n "$lines" ] || setup_fail "the sheet shows no line about $DEC after the refused Accept (screenshot in $OUT)"

reasons=()
ids="$(printf '%s' "$lines" | grep -o -- "$DEC" | wc -l)"
[ "$ids" -le 1 ] || reasons+=("the sheet repeats $DEC $ids times")
case "$lines" in *human:*) reasons+=("the sheet shows a raw actor name (human:...)") ;; esac
if [ "${#reasons[@]}" -gt 0 ]; then
  fail "the stale-decision refusal reads \"$lines\": ${reasons[*]} (screenshot in $OUT)"
fi

echo "PASS: the stale-decision refusal is one calm line: \"$lines\" (screenshot in $OUT)"
