#!/usr/bin/env bash
# tests/ui-checks/review-fix-sheet.sh — the review page's fix sheet on a real serve (#2208 follow-up): sindri-ui-fixture.sh
# `reviews up` seeds PR acme/api#1 in a file forge and Context, the app opens Reviews and the PR (its row, Enter), line 42 is focused,
# `c` opens the fix sheet, the text is sent; then the item, the sindri.fix thread and the fix-task file must exist.
# Exit 0 pass, 1 FAIL, 2 setup broke (context-lib.sh contract). Run it through scripts/run-test-guard.sh.
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init review-fix-sheet
TEXT="Cap the wait at the budget total"

# Polls until a visible TextBlock shows exactly $1 ($2 seconds); a wait-for by text picks its first match, which may be a hidden one.
wait_visible_text() {
  local _
  for _ in $(seq 1 "$2"); do
    [ -n "$(ctx_text --type TextBlock --text "$1")" ] && return 0
    sleep 1
  done
  return 1
}

bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"
bash "$FIXTURE" context up >"$OUT/context-up.log" 2>&1 || setup_fail "sindri-ui-fixture context up (see $OUT/context-up.log)"
bash "$FIXTURE" reviews up >"$OUT/reviews.out" 2>"$OUT/reviews.log" || setup_fail "sindri-ui-fixture reviews up (see $OUT/reviews.log)"
page="$(sed -n 1p "$OUT/reviews.out")"; [ "$page" = /sindri/reviews/acme/api/1 ] || setup_fail "reviews up printed '$page'"

export AW_SINDRI_UI="${AW_SINDRI_UI:-$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui}"
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
setsid env -u DISPLAY xvfb-run -a -s "-screen 0 2200x1400x24" "$AW_SINDRI_UI" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
"$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
"$DRIVER" window --width 1280 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
"$DRIVER" click --type ListBoxItem --name Reviews >/dev/null 2>&1 || setup_fail "no Reviews tab"
"$DRIVER" wait-for --contains "Add a retry budget to sync" --state visible --wait-ms 60000 >/dev/null 2>&1 || fail "the Reviews list never showed the fixture PR"
shot list
# the row is a focusable Border named by its AutomationName: focus it, then Enter opens the PR (the list's own key)
row="$("$DRIVER" search --type Border --limit 200 2>/dev/null | jq -r 'first(.matches[] | select(.visible and ((.name // "") | contains("Add a retry budget to sync")))) | .id // empty')"
[ -n "$row" ] || fail "no Reviews row named for the fixture PR"
"$DRIVER" action --do focus --node-id "$row" >"$OUT/click-row.log" 2>&1 || fail "could not focus the fixture PR's row (see $OUT/click-row.log)"
"$DRIVER" key --chord Enter >"$OUT/key-enter.log" 2>&1 || fail "could not press Enter on the PR row (see $OUT/key-enter.log)"
wait_visible_text "Add a retry budget to sync" 60 || fail "the review page never showed the PR's title"
shot page

line="line 42, added:     if (delay is null) return Retry.Stop;"
"$DRIVER" wait-for --name "$line" --state visible --wait-ms 30000 >/dev/null 2>&1 || fail "no diff line 42 on the page"
"$DRIVER" action --do focus --name "$line" >"$OUT/focus-line.log" 2>&1 || fail "could not focus line 42"
"$DRIVER" key --chord C >/dev/null 2>&1 || fail "could not press c"
"$DRIVER" wait-for --type TextBox --name "What needs fixing" --state visible --wait-ms 15000 >/dev/null 2>&1 || fail "c on line 42 opened no fix sheet"
shot sheet-open
"$DRIVER" type --type TextBox --name "What needs fixing" --value "$TEXT" >/dev/null 2>&1 || fail "could not type the fix"
"$DRIVER" click --type Button --name Send >/dev/null 2>&1 || fail "could not click Send"
# the outcome line names the item once the server answered; poll the server for the item rather than guess the words
item=""
for _ in $(seq 1 30); do
  items="$("$PROVE" human items --json 2>/dev/null | tr -d '\r' | sed -n '/^{/,$p')"
  item="$(printf '%s' "$items" | jq -r --arg t "Fix: $TEXT" 'first(.items[] | select(.title == $t)) | .id // empty' 2>/dev/null)"
  [ -n "$item" ] && break
  sleep 1
done
sleep 1
outcome="$("$DRIVER" search --type TextBlock --limit 400 2>/dev/null | jq -r --arg i "${item:-ITM-}" 'first(.matches[] | select(.visible and ((.text // "") | contains($i)))) | .text // empty')"
shot sent
printf 'outcome: %s\n' "$outcome" >"$OUT/outcome.txt"
[ -n "$item" ] || fail "no item 'Fix: $TEXT' (outcome: $outcome)"
grep -q "aw-marker:sindri.fix:$item" "$AW_SINDRI_HOME/ui-fixture-forge/state/forge/file/forge.json" || fail "no sindri.fix thread for $item on the forge PR"
grep -lq "\"$item\"" "$AW_SINDRI_HOME"/fix-tasks/*.json 2>/dev/null || fail "no fix-task file names $item"
echo "pass: $item; outcome: $outcome; screenshots in $OUT"
