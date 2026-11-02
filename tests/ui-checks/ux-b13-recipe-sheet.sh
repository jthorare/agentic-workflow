#!/usr/bin/env bash
# tests/ui-checks/ux-b13-recipe-sheet.sh — frozen regression check for the UX-pass finding B-13: opening a workflow type's sheet from the
# Workflows list must not squeeze the list to 520 px and drop its columns, and the sheet itself reads like prose: a title that is not the
# raw slug (workflow.dev-...) and no "Finished 0%" for runs that are all still open. It drives the REAL sindri-ui (UI driver,
# docs/csharp/ui-driver.md) against a guarded fixture home with the strip's instances started (sindri-ui-fixture.sh workflows).
#   bash tests/ui-checks/ux-b13-recipe-sheet.sh   exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXB13_OUT (screenshots; default a new temp dir), SINDRI (a built sindri for sindri-prove.sh; default dotnet run).
set -uo pipefail
unset TMUX

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FIXTURE="$ROOT/scripts/sindri-ui-fixture.sh"
PROVE="$ROOT/scripts/sindri-prove.sh"
DRIVER="$ROOT/scripts/aw-ui-driver.sh"
TMP_ROOT="${TMPDIR:-/tmp}"

export AW_SINDRI_HOME
AW_SINDRI_HOME="$(mktemp -d "$TMP_ROOT/ux-b13-recipe-sheet-home-XXXXXX")" || exit 2
touch "$AW_SINDRI_HOME/.prover-scratch"
export AW_UI_DRIVER=1
export AW_UI_DRIVER_DIR
AW_UI_DRIVER_DIR="$(mktemp -d "$TMP_ROOT/ux-b13-recipe-sheet-driver-XXXXXX")" || exit 2
OUT="${UXB13_OUT:-$(mktemp -d "$TMP_ROOT/ux-b13-recipe-sheet-out-XXXXXX")}"
mkdir -p "$OUT" || exit 2

APP_WRAPPER_PID=""
setup_fail() { echo "ux-b13-recipe-sheet: setup failed: $*" >&2; exit 2; }
fail() { echo "FAIL: $*" >&2; exit 1; }
# A driver call that timed out is a broken setup, never a FAIL; this also runs inside $(...), so it tells the main shell to stop.
trap 'exit 2' USR1
timed_out() { echo "ux-b13-recipe-sheet: setup failed: aw-ui-driver $1 timed out after 120 s" >&2; kill -USR1 $$; exit 2; }

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

# 1. The control plane with open workflow runs (one triage and two desk round trips, all still open).
bash "$FIXTURE" up >/dev/null 2>&1 || setup_fail "sindri-ui-fixture up"
bash "$FIXTURE" workflows >/dev/null 2>&1 || setup_fail "sindri-ui-fixture workflows"

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

# 2. The Workflows page, in List view (the page's nav entry and the view switch are list items named "Workflows" and "List").
drv click --type ListBoxItem --name Workflows >/dev/null 2>&1 || setup_fail "the Workflows page could not be opened"
drv wait-for --type ListBoxItem --name List --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "the Workflows page never drew its view switch"
drv click --type ListBoxItem --name List >/dev/null 2>&1 || setup_fail "the List view could not be chosen"

# The list's head names its columns; with no sheet open all of them show (the window is 1280 wide).
column_shown() { nodes --type TextBlock --text "$1" | jq -e '[.matches[] | select(.visible and .width >= 20)] | length > 0' >/dev/null 2>&1; }
ok=0
for _ in $(seq 1 40); do column_shown Stage && column_shown "Waiting on" && column_shown Step && { ok=1; break; }; sleep 0.5; done
[ "$ok" = 1 ] || setup_fail "the Workflows list does not show its Stage, Step and Waiting on columns before the sheet opens (see $OUT/app.log)"
drv screenshot --out "$OUT/before-sheet.png" >/dev/null 2>&1

# 3. Open the sheet of the Desk Round Trip workflow ("How it works: <name>", its two runs are open).
link=""
for _ in $(seq 1 60); do
  link="$(nodes --type Button | jq -r '[.matches[].name // empty | select(startswith("How it works: ") and contains("Desk Round Trip"))][0] // empty')"
  [ -n "$link" ] && break
  sleep 1
done
[ -n "$link" ] || setup_fail "no \"How it works\" link for the Desk Round Trip workflow (see $OUT/app.log)"
drv click --type Button --name "$link" >/dev/null 2>&1 || setup_fail "the click on \"$link\" was not accepted"
drv wait-for --type TextBlock --text Finished --state visible --wait-ms 60000 >/dev/null 2>&1 \
  || setup_fail "the workflow sheet never showed its Finished figure (are the runs counted? see $OUT/app.log)"
sleep 2
drv screenshot --out "$OUT/sheet-open.png" >/dev/null 2>&1

reasons=()

# (a) The list keeps its columns, or the sheet covers it (then the page is still in the tree, drawn full width).
for col in Stage Step "Waiting on"; do
  column_shown "$col" || reasons+=("the list lost its $col column while the sheet is open")
done

# (b) The sheet's title (x:Name SheetSubjectText) is the deep link's subject: the raw slug.
title="$(text_of --name SheetSubjectText)"
[ -n "$title" ] || setup_fail "the sheet's title text is not in the tree"
case "$title" in workflow.*) reasons+=("the sheet's title is the raw slug \"$title\"") ;; esac

# (c) The Finished figure for a workflow whose runs are all still open is not "0%". (The figure is the text after the "Finished" label.)
left="$(left_of --name RecipeSheetScroll)"
[ -n "$left" ] || setup_fail "the workflow sheet is not in the tree"
finished="$(nodes --type TextBlock | jq -r --argjson left "$left" \
  '[.matches[] | select(.visible and .x >= $left)] as $a | ([$a | to_entries[] | select(.value.text == "Finished") | .key][0]) as $i | if $i == null then "" else ($a[$i + 1].text // "") end')"
[ -n "$finished" ] || setup_fail "no figure after the Finished label"
[ "$finished" != "0%" ] || reasons+=("it says \"Finished 0%\" although every run is still open")

if [ "${#reasons[@]}" -gt 0 ]; then
  fail "the workflow sheet: $(IFS='; '; echo "${reasons[*]}") (screenshots in $OUT)"
fi

echo "PASS: the workflow sheet keeps the list's columns, has a readable title ($title) and shows Finished as \"$finished\" (screenshots in $OUT)"
