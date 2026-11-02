#!/usr/bin/env bash
# tests/ui-checks/ux-c1-map-refresh.sh — frozen regression check for the UX-pass finding C-1 (the whole map never picks up a newer map version).
# Expected: a map version imported while the Context page is open reaches the page (its size line and head change) within 30 s, with no restart.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/ux-c1-map-refresh.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init ux-c1-map-refresh
ctx_start 1280 900

before_size="$(ctx_text --type TextBlock --contains "links")"
before_head="$(ctx_text --type TextBlock --contains "whole map")"
[ -n "$before_size" ] || setup_fail "no size line before the import"

# A newer map version: 'over' imports the generated 612-node map as the next version (the page opened on the tabs map).
bash "$FIXTURE" context whole over >"$OUT/whole-over.log" 2>&1 || setup_fail "sindri-ui-fixture context whole over (see $OUT/whole-over.log)"

changed=0
for _ in $(seq 1 30); do
  size="$(ctx_text --type TextBlock --contains "links")"
  head="$(ctx_text --type TextBlock --contains "whole map")"
  if [ "$size" != "$before_size" ] || [ "$head" != "$before_head" ]; then changed=1; break; fi
  sleep 1
done
[ "$changed" = 1 ] || fail "the page still shows '$before_head' / '$before_size' 30 s after a newer map version was imported"
echo "PASS: the Context page followed the new map version ('$head' / '$size')"
