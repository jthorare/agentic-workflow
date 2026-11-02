#!/usr/bin/env bash
# tests/ui-checks/ux-b16-instance-sheet.sh — frozen regression check for the UX-pass finding B-16: on a workflow instance's sheet a long
# command in "What this run collected" wraps inside the sheet (its text and its Copy button stay within the sheet's right edge), and the
# attempt line of a run that waits for a desk answer says what it waits for (not "Attempt 1, with you"). It drives the REAL sindri-ui
# (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the incident-hold workflow started
# (sindri-ui-fixture.sh incident start: one desk Step; its next-step command is a long text fact).
#   bash tests/ui-checks/ux-b16-instance-sheet.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB16_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b16-instance-sheet-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b16-instance-sheet-driver-XXXXXX")" || exit 2
OUT="${UXB16_OUT:-$(mktemp -d "$TMP_ROOT/ux-b16-instance-sheet-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b16-instance-sheet: setup failed: $*" >&2; exit 2; }
fail() { echo "FAIL: $*" >&2; exit 1; }
# A driver call that timed out is a broken setup, never a FAIL; this also runs inside $(...), so it tells the main shell to stop.
trap 'exit 2' USR1
timed_out() { echo "ux-b16-instance-sheet: setup failed: aw-ui-driver $1 timed out after 120 s" >&2; kill -USR1 $$; exit 2; }

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

# 1. The control plane with the incident-hold instance waiting at its desk Step.
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" incident start >/dev/null 2>&1 || setup_fail "sindri-ui-fixture incident start"

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

# 2. The Workflows page, List view; the incident run's row is a Button named "Open <title> ...".
drv click --type ListBoxItem --name Workflows >/dev/null 2>&1 || setup_fail "the Workflows page could not be opened"
drv wait-for --type ListBoxItem --name List --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "the Workflows page never drew its view switch"
drv click --type ListBoxItem --name List >/dev/null 2>&1 || setup_fail "the List view could not be chosen"
open=""
for _ in $(seq 1 60); do
  open="$(nodes --type Button | jq -r '[.matches[] | select(.visible) | .name // empty | select(startswith("Open ") and test("incident"; "i"))][0] // empty')"
  [ -n "$open" ] && break
  sleep 1
done
[ -n "$open" ] || setup_fail "no Open button for the incident run in the Workflows list (see $OUT/app.log)"
drv click --type Button --name "$open" >/dev/null 2>&1 || setup_fail "the click on \"$open\" was not accepted"

# 3. The sheet's scroll viewer is the card's edge; the command fact is a TextBlock with the next command in it.
for _ in $(seq 1 60); do
  [ -n "$(text_of --type TextBlock --contains "sindri workflow complete")" ] && break
  sleep 1
done
[ -n "$(text_of --type TextBlock --contains "sindri workflow complete")" ] || setup_fail "the instance sheet shows no \"sindri workflow complete ...\" command (see $OUT/app.log)"
sleep 1
drv screenshot --out "$OUT/sheet.png" >/dev/null 2>&1

view="$(nodes --name InstanceSheetScroll | jq -c '.matches[0] // empty')"
[ -n "$view" ] || setup_fail "the instance sheet is not in the tree"
view_left="$(printf '%s' "$view" | jq -r '.x')"
view_right="$(printf '%s' "$view" | jq -r '.x + .width')"

reasons=()

# (a) The command line and its Copy button stay inside the sheet (a small allowance for rounding).
cmd="$(nodes --type TextBlock --contains "sindri workflow complete" | jq -c --argjson l "$view_left" '[.matches[] | select(.x >= $l)][0] // empty')"
[ -n "$cmd" ] || setup_fail "the command text is not inside the instance sheet"
cmd_right="$(printf '%s' "$cmd" | jq -r '.x + .width')"
awk -v r="$cmd_right" -v e="$view_right" 'BEGIN { exit !(r <= e + 1) }' \
  || reasons+=("the command text runs to x=$cmd_right, past the sheet's right edge x=$view_right")
copy_right="$(nodes --type Button --text Copy | jq -r --argjson c "$cmd" \
  '[.matches[] | select(.y >= ($c.y - 20) and .y <= ($c.y + $c.height + 20)) | .x + .width] | max // empty')"
if [ -n "$copy_right" ] && ! awk -v r="$copy_right" -v e="$view_right" 'BEGIN { exit !(r <= e + 1) }'; then
  reasons+=("the command's Copy button is pushed to x=$copy_right, past the sheet's right edge x=$view_right")
fi

# (b) The attempt line (the first text in the sheet that starts "Attempt") says what the run waits for. Default wording: "waiting".
attempt="$(nodes --type TextBlock | jq -r --argjson l "$view_left" '[.matches[] | select(.x >= $l) | .text // empty | select(startswith("Attempt"))][0] // empty')"
[ -n "$attempt" ] || setup_fail "the instance sheet shows no attempt line"
case "$attempt" in *waiting*) ;; *) reasons+=("the attempt line reads \"$attempt\", it does not say what the run waits for") ;; esac

if [ "${#reasons[@]}" -gt 0 ]; then
  fail "the instance sheet: $(IFS='; '; echo "${reasons[*]}") (screenshot in $OUT)"
fi

echo "PASS: the instance sheet wraps its command inside the card and says \"$attempt\" (screenshot in $OUT)"
