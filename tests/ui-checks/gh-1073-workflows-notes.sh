#!/usr/bin/env bash
# tests/ui-checks/gh-1073-workflows-notes.sh — regression check for #1073 (B-22, the Workflows page's smaller notes), list view at 900 wide:
#   1. no empty band between the toolbar and the list (the hidden live region keeps its line height and a panel gap of its own);
#   2. the key-hint line stays inside the toolbar (it wraps under it today).
# It drives the REAL sindri-ui (docs/csharp/ui-driver.md) against a guarded fixture home with three workflow instances.
#   bash tests/ui-checks/gh-1073-workflows-notes.sh     exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: UXCHECK_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
unset TMUX DISPLAY

source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
source "$CTX_ROOT/tests/ui-checks/xvfb-lib.sh"
ctx_init gh-1073-workflows-notes
[ -n "${UXCHECK_OUT:-}" ] && OUT="$UXCHECK_OUT" && mkdir -p "$OUT"

bash "$FIXTURE" up >"$OUT/up.log" 2>&1 || setup_fail "sindri-ui-fixture up (see $OUT/up.log)"
bash "$FIXTURE" workflows >"$OUT/workflows.log" 2>&1 || setup_fail "sindri-ui-fixture workflows (see $OUT/workflows.log)"
if [ -z "${AW_SINDRI_UI:-}" ]; then
  timeout 600 dotnet build "$CTX_ROOT/src/Sindri/Aw.Sindri.Ui.Desktop" -nologo -v q >"$OUT/ui-build.log" 2>&1 \
    || setup_fail "the sindri-ui build failed (see $OUT/ui-build.log)"
  export AW_SINDRI_UI="$CTX_ROOT/artifacts/bin/Aw.Sindri.Ui.Desktop/debug/sindri-ui"
fi
[ -x "$AW_SINDRI_UI" ] || setup_fail "no sindri-ui at $AW_SINDRI_UI"
xvfb_start || setup_fail "no Xvfb display could be started"

bash "$CTX_ROOT/scripts/sindri-ui-app.sh" >"$OUT/app.log" 2>&1 &
APP_WRAPPER_PID=$!
timeout 120 "$DRIVER" ready --wait-ms 120000 >/dev/null 2>&1 || setup_fail "the app's driver never answered (see $OUT/app.log)"
timeout 120 "$DRIVER" window --width 900 --height 900 >/dev/null 2>&1 || setup_fail "could not size the window"
timeout 120 "$DRIVER" click --type ListBoxItem --name Workflows >/dev/null 2>&1 || setup_fail "no Workflows tab"
timeout 120 "$DRIVER" wait-for --type ListBoxItem --name List --state visible --wait-ms 30000 >/dev/null 2>&1 || setup_fail "no List view switch"
timeout 120 "$DRIVER" click --type ListBoxItem --name List >/dev/null 2>&1 || setup_fail "the List view switch did not click"
timeout 120 "$DRIVER" wait-for --type Border --automation-id WorkflowColumnHead --state visible --wait-ms 30000 >/dev/null 2>&1 \
  || setup_fail "the list never showed its column head"
sleep 1.5

num() { jq -r "$1" <<<"$2"; }
toolbar="$(ctx_node --automation-id WorkflowsToolbar)"; [ -n "$toolbar" ] || setup_fail "no visible WorkflowsToolbar"
list="$(ctx_node --automation-id WorkflowsList)"; [ -n "$list" ] || setup_fail "no visible WorkflowsList"
hints="$(ctx_node --automation-id WorkflowsKeyHints)"; [ -n "$hints" ] || setup_fail "no visible WorkflowsKeyHints"

# 1. The gap between the toolbar's bottom and the list's top is the page's one panel gap (16), not a band.
gap="$(awk -v ty="$(num .y "$toolbar")" -v th="$(num .height "$toolbar")" -v ly="$(num .y "$list")" 'BEGIN { printf "%.1f", ly - (ty + th) }')"
lt "$gap" 28 || fail "an empty band of $gap px sits between the toolbar and the list (one panel gap is 16)"

# 2. The key hints end inside the toolbar's right edge.
hr="$(awk -v x="$(num .x "$hints")" -v w="$(num .width "$hints")" 'BEGIN { printf "%.1f", x + w }')"
tr="$(awk -v x="$(num .x "$toolbar")" -v w="$(num .width "$toolbar")" 'BEGIN { printf "%.1f", x + w }')"
lt "$hr" "$(awk -v t="$tr" 'BEGIN { print t + 1 }')" || fail "the key hints end at $hr, past the toolbar's right edge $tr, at 900 wide"

shot after
echo "PASS: no band above the list ($gap px) and the key hints fit the toolbar (screenshots in $OUT)"
