#!/usr/bin/env bash
# tests/ui-checks/ux-a6-needs-you-count.sh — regression check for the UX-pass finding A-6: Home's Needs-you header says "5 need you" while
# the card shows 4 rows and there is no "all 5" link. The header counts every row the API serves (AttentionSummary.For: feed.Rows.Count);
# the card leaves out the setup rows ("Approve all new workflows", "Turn on for your repos") while Home's setup line stands for them
# (ShellViewModel.Setup.cs ShownOnHome), so with three decisions (under the card's cap, so no overflow link) the two disagree.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home (scripts/sindri-ui-fixture.sh through
# scripts/sindri-prove.sh): three decisions open on the approved fixture profile (its setup rows are in the feed), then one is decided at the CLI.
# The header count n must equal the rows the card shows, or the card must show an "all n" link: read once the header settled, and again after a row left.
#   bash tests/ui-checks/ux-a6-needs-you-count.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXA6_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-a6-needs-you-count-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-a6-needs-you-count-driver-XXXXXX")" || exit 2
OUT="${UXA6_OUT:-$(mktemp -d "$TMP_ROOT/ux-a6-needs-you-count-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-a6-needs-you-count: setup failed: $*" >&2; exit 2; }
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

# 1. The control plane with three decisions in Needs you (a session's requests; each opens a decision).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
ids=()
for i in 1 2 3; do
  answer="$(bash "$FIXTURE" request "ux-a6 request $i" 2>"$OUT/request.err")" || setup_fail "sindri-ui-fixture request $i (see $OUT/request.err)"
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


summary_n() {
  nodes "$OUT/summary.json" --type TextBlock --contains " need you · "
  first_text "$OUT/summary.json" | sed -n 's/^\([0-9][0-9]*\) need you.*/\1/p'
}
summary_is() { [ "$(( $(summary_n) + 0 ))" -eq "$1" ]; }
N=0
stable_since=0
settle_n() {  # true once the header has read the same non-zero number on every look for ~3 s
  local v; v="$(( $(summary_n) + 0 ))"
  if [ "$v" -gt 0 ] && [ "$v" -eq "$N" ]; then
    [ "$SECONDS" -ge "$stable_since" ] && return 0
    return 1
  fi
  N="$v"; stable_since=$((SECONDS + 3))
  return 1
}
# A-6: the header count must equal the rows the card shows, or the card must name the rest with an "all n" link.
count_vs_rows() {  # count_vs_rows <label>: sets $verdict to "" when it holds, else one sentence
  nodes "$OUT/rows-$1.json" --type NeedsYouRow
  local n r
  n="$(summary_n)"; r="$(visible_n "$OUT/rows-$1.json")"
  verdict=""
  if [ "$r" -ne "$n" ]; then
    nodes "$OUT/link-$1.json" --type TextBlock --contains "all $n"
    if [ "$(visible_n "$OUT/link-$1.json")" -lt 1 ]; then
      verdict="$1: the header says $n need you but the card shows $r rows and there is no 'all $n' link"
    fi
  fi
}

poll 90 settle_n || setup_fail "Home's header never settled on a need-you count (last read $N; see $OUT/app.log)"
[ "$N" -ge 3 ] || setup_fail "Home's header settled on $N need you, fewer than the three decisions opened"
echo "ux-a6-needs-you-count: header settled on N=$N need you"
shot settled
count_vs_rows settled
first="$verdict"

# One decision is decided at the terminal: the card follows and the count must still hold.
bash "$FIXTURE" decide "${ids[0]}" accept >/dev/null 2>&1 || setup_fail "sindri-ui-fixture decide ${ids[0]} accept"
poll 60 summary_is $((N - 1)) || setup_fail "the header never dropped to $((N - 1)) need you after the decision"
sleep 3   # the card has had every chance to re-measure
shot after-decide
count_vs_rows after-decide
second="$verdict"

if [ -n "$first" ] || [ -n "$second" ]; then
  msgs=(); [ -n "$first" ] && msgs+=("$first"); [ -n "$second" ] && msgs+=("$second")
  fail "$(IFS=';'; echo "${msgs[*]}") (screenshots in $OUT)"
fi

echo "PASS: the Needs-you count matched its rows once settled and after a row left (screenshots in $OUT)"
