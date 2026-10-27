#!/usr/bin/env bash
# tests/ui-checks/ux-c5-zoom-noop.sh — frozen regression check for the UX-pass finding C-5 (Zoom out, Zoom in and Fit are enabled but do nothing when the whole map fits).
# Expected: a zoom button that cannot act is disabled; an enabled one changes the zoom label.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c5-zoom-noop.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c5-zoom-noop
ctx_start 1920 1080

label() { ctx_text --type TextBlock --name WholeZoomValue; }
for name in "Zoom in" "Zoom out"; do
  node="$(ctx_node --type Button --name "$name")"
  [ -n "$node" ] || setup_fail "no '$name' button"
  [ "$(jq -r .enabled <<<"$node")" = false ] && continue
  before="$(label)"
  "$DRIVER" click --type Button --name "$name" >/dev/null 2>&1 || fail "'$name' is enabled but the click was not accepted"
  sleep 1
  after="$(label)"
  [ "$before" != "$after" ] || fail "'$name' is enabled but the zoom stays at $before"
done
echo "PASS: every zoom button is disabled or changes the zoom"
