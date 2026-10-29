#!/usr/bin/env bash
# tests/ui-checks/ux-b12-decide-sheet.sh — frozen regression check for the UX-pass finding B-12: the Decide sheet reads like a decision, not
# a debug dump. It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home: a session files a
# request, the check opens its Decide sheet and expects (1) a title that is not the raw decision id, (2) a line saying who filed the
# request and when ("Filed ..."), (3) the note label above the option buttons (or no label: a placeholder), and, after pressing the
# first option, (4) that the sheet no longer asks "Accept this ...?" next to disabled buttons.
#   bash tests/ui-checks/ux-b12-decide-sheet.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB12_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b12-decide-sheet-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b12-decide-sheet-driver-XXXXXX")" || exit 2
OUT="${UXB12_OUT:-$(mktemp -d "$TMP_ROOT/ux-b12-decide-sheet-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b12-decide-sheet: setup failed: $*" >&2; exit 2; }
fail() { echo "FAIL: $*" >&2; exit 1; }
# A driver call that timed out is a broken setup, never a FAIL; this also runs inside $(...), so it tells the main shell to stop.
trap 'exit 2' USR1
timed_out() { echo "ux-b12-decide-sheet: setup failed: aw-ui-driver $1 timed out after 120 s" >&2; kill -USR1 $$; exit 2; }

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

# 1. The control plane with one filed request (a decision in Needs you).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
resp="$(bash "$FIXTURE" request "Export the fixture nightly" 2>/dev/null)" || setup_fail "sindri-ui-fixture request"
DEC="$(printf "%s" "$resp" | jq -r ".decisionId // empty")"
[ -n "$DEC" ] || setup_fail "the request answered without a decisionId: $resp"

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

# 2. Home's row for the decision is a Button named "Open <title> <DEC>"; open its sheet.
open=""
for _ in $(seq 1 60); do
  open="$(nodes --type Button | jq -r --arg d "$DEC" '[.matches[] | select(.visible) | .name // empty | select(startswith("Open ") and contains($d))][0] // empty')"
  [ -n "$open" ] && break
  sleep 1
done
[ -n "$open" ] || setup_fail "no Home row to open for $DEC (see $OUT/app.log)"
drv click --type Button --name "$open" >/dev/null 2>&1 || setup_fail "the click on the Home row of $DEC was not accepted"
drv wait-for --type TextBlock --text "Export the fixture nightly" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the decision sheet of $DEC never showed the request (see $OUT/app.log)"
drv wait-for --type Button --name "Accept $DEC" --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the decision sheet of $DEC has no option button (see $OUT/app.log)"
sleep 1
drv screenshot --out "$OUT/sheet.png" >/dev/null 2>&1

left="$(left_of --name DecisionSheetScroll)"
[ -n "$left" ] || setup_fail "the decision sheet is not in the tree"
# The sheet's own text: visible TextBlocks at or right of the sheet's left edge.
sheet_texts() { nodes --type TextBlock | jq -r --argjson left "$left" '[.matches[] | select(.visible and .x >= $left)]'; }

reasons=()

# (1) The title. The sheet's title text (x:Name SheetSubjectText) is the deep link's subject, the raw id.
title="$(text_of --name SheetSubjectText)"
[ -n "$title" ] || setup_fail "the sheet's title text is not in the tree"
case "$title" in "$DEC" | DEC-*) reasons+=("the title is the raw id \"$title\"") ;; esac

# (2) Who and when. Default wording: a line that begins "Filed" (for example "Filed by an agent session at 11:02").
filed="$(sheet_texts | jq -r '[.[] | .text // empty | select(startswith("Filed"))][0] // empty')"
[ -n "$filed" ] || reasons+=("no line says who filed the request and when")

# (3) The note label sits above the option buttons (a label under them is the bug). No label at all is fine (a placeholder).
caption_y="$(sheet_texts | jq -r '[.[] | select(.text == "Note, sent with your choice")][0].y // empty')"
button_y="$(nodes --type Button --name "Accept $DEC" | jq -r '.matches[-1].y // empty')"
[ -n "$button_y" ] || setup_fail "no Accept button position"
if [ -n "$caption_y" ] && ! awk -v c="$caption_y" -v b="$button_y" 'BEGIN { exit !(c < b) }'; then
  reasons+=("the note label (y $caption_y) is below the option buttons (y $button_y)")
fi

# (4) Press the first option; once the sheet disables its buttons it must not still ask the question.
last="$(nodes --type Button --name "Accept $DEC" | jq -r '(.matches | length) - 1')"
drv click --type Button --name "Accept $DEC" --index "$last" >/dev/null 2>&1 || setup_fail "the Accept click was not accepted by the driver"
decided=0
for _ in $(seq 1 40); do
  if nodes --type Button --name "Accept $DEC" | jq -e '.matches | length > 0 and (.[-1].enabled | not)' >/dev/null 2>&1; then decided=1; break; fi
  if [ "$(nodes --type Button --name "Accept $DEC" | jq -r '.matches | length')" = 0 ]; then decided=1; break; fi
  sleep 0.5
done
[ "$decided" = 1 ] || setup_fail "the option buttons never disabled after Accept (see $OUT/app.log)"
sleep 1
drv screenshot --out "$OUT/decided.png" >/dev/null 2>&1
asks="$(sheet_texts | jq -r '[.[] | .text // empty | select(startswith("Accept this"))][0] // empty')"
[ -z "$asks" ] || reasons+=("after deciding, the sheet still asks \"$asks\"")

if [ "${#reasons[@]}" -gt 0 ]; then
  fail "the Decide sheet: $(IFS='; '; echo "${reasons[*]}") (screenshots in $OUT)"
fi

echo "PASS: the Decide sheet has a real title ($title), who filed it ($filed), its note label above the buttons and stops asking once decided (screenshots in $OUT)"
