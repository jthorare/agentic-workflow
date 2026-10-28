#!/usr/bin/env bash
# tests/ui-checks/ux-b1-needs-you-card.sh — frozen regression check for the UX-pass finding B-1: after a Needs-you row leaves, Home's
# card must keep its bottom border and its "all N" link, and the header count must equal the rows (plus the overflow the link names).
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh): seven requests open seven decisions, one is decided at the CLI, and the card is read once it settled.
#   bash tests/ui-checks/ux-b1-needs-you-card.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB1_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b1-needs-you-card-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b1-needs-you-card-driver-XXXXXX")" || exit 2
OUT="${UXB1_OUT:-$(mktemp -d "$TMP_ROOT/ux-b1-needs-you-card-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b1-needs-you-card: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane with seven decisions in Needs you (a session's requests; each opens a decision).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
ids=()
for i in 1 2 3 4 5 6 7; do
  answer="$(bash "$FIXTURE" request "ux-b1 request $i" 2>"$OUT/request.err")" || setup_fail "sindri-ui-fixture request $i (see $OUT/request.err)"
  id="$(printf '%s' "$answer" | jq -r '.. | objects | .decisionId? // empty' | head -1)"
  [ -n "$id" ] || setup_fail "no decision id in the request answer: $answer"
  ids+=("$id")
done

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


# 2. Home lists the seven decisions plus whatever setup rows Needs you also counts (e.g. "Approve all new workflows"): the header
# is read once it shows a number and has held it for ~3 s; that number is N.
summary_n() {
  nodes "$OUT/summary.json" --type TextBlock --contains " need you · "
  first_text "$OUT/summary.json" | sed -n 's/^\([0-9][0-9]*\) need you.*/\1/p'
}
summary_is() { [ "$(( $(summary_n) + 0 ))" -eq "$1" ]; }
N=0
settle_n() {  # true once the header has read the same non-zero number on every look for ~3 s
  local v; v="$(( $(summary_n) + 0 ))"
  if [ "$v" -gt 0 ] && [ "$v" -eq "$N" ]; then
    [ "$SECONDS" -ge "$stable_since" ] && return 0
    return 1
  fi
  N="$v"; stable_since=$((SECONDS + 3))
  return 1
}
stable_since=0
poll 90 settle_n || setup_fail "Home's header never settled on a need-you count (last read $N; see $OUT/app.log)"
[ "$N" -ge 7 ] || setup_fail "Home's header settled on $N need you, fewer than the seven decisions opened"
echo "ux-b1-needs-you-card: header settled on N=$N need you"
shot before-decide

# 3. One decision is decided at the terminal (the card must follow).
bash "$FIXTURE" decide "${ids[0]}" accept >/dev/null 2>&1 || setup_fail "sindri-ui-fixture decide ${ids[0]} accept"
poll 60 summary_is $((N - 1)) || setup_fail "the header never dropped to $((N - 1)) need you after the decision"
sleep 3   # the card has had every chance to re-measure
shot after-decide

# 4. What the user sees: the count equals the rows (plus the overflow the "all N" link names), and the card still ends below its last row.
nodes "$OUT/rows.json" --type NeedsYouRow
nodes "$OUT/panel.json" --type Border --name "NEEDS YOU"
n="$(summary_n)"
r="$(visible_n "$OUT/rows.json")"
[ "$(visible_n "$OUT/panel.json")" -ge 1 ] || setup_fail "no visible NEEDS YOU card in the tree"
fails=()
if [ "$r" -gt "$n" ]; then
  fails+=("[count-vs-rows mismatch, hypothesis 2] the header says $n need you but $r rows show")
elif [ "$r" -lt "$n" ]; then
  nodes "$OUT/link.json" --type TextBlock --contains "all $n"
  [ "$(visible_n "$OUT/link.json")" -ge 1 ] || fails+=("[count-vs-rows mismatch, hypothesis 2] the header says $n need you, $r rows show and there is no 'all $n' link")
fi
# bounds are window coordinates: the card's bottom edge must lie below the last row's bottom edge (the card's 1 px border), and below the link
panel_bottom="$(jq '[.matches[] | select(.visible)][0] | ((.y // .Y) + (.height // .Height))' "$OUT/panel.json")"
rows_bottom="$(jq '[.matches[] | select(.visible) | ((.y // .Y) + (.height // .Height))] | max // 0' "$OUT/rows.json")"
if ! jq -n --argjson p "$panel_bottom" --argjson r "$rows_bottom" '$p >= $r + 1' | grep -q true; then
  fails+=("[card not re-measured, hypothesis 1] the card ends at y=$panel_bottom, not below its last row (y=$rows_bottom): the bottom border is cut off")
fi
if [ "${#fails[@]}" -gt 0 ]; then
  fail "$(IFS=';'; echo "${fails[*]}") (screenshots in $OUT)"
fi

echo "PASS: the Needs-you card kept its border and its count after a row left (screenshots in $OUT)"
