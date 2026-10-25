#!/usr/bin/env bash
# tests/ui-checks/uidriver-tree-hang.sh — frozen regression check: `aw-ui-driver tree --depth 80` never returned on the Context page (no reply for 28 minutes).
# Expected: the call answers within its own time budget: a JSON tree, or a clear error naming an AW-UIDRIVER-0xx code. It never hangs the caller or the app.
# It drives the REAL sindri-ui (UI driver, docs/csharp/ui-driver.md) against a guarded fixture home with the Context page's data
# (context-lib.sh: sindri-ui-fixture.sh up + context up). Red on the baseline, green after the fix.
#   bash tests/ui-checks/uidriver-tree-hang.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/context-lib.sh"
ctx_init uidriver-tree-hang
ctx_start 1280 900

# The one call under test. Every other driver call is bounded by `timeout 120`; this one by 150 s, far past any honest answer.
out="$(timeout 150 "$DRIVER" tree --depth 80 2>"$OUT/tree.err")"
rc=$?

if [ "$rc" -eq 124 ] || { [ "$rc" -eq 0 ] && [ -z "$out" ]; }; then
  # The driver may be wedged, so the failure screenshot is bounded too.
  timeout 120 "$DRIVER" screenshot --out "$OUT/failed.png" >/dev/null 2>&1 || true
  echo "FAIL: tree --depth 80 never answered (hung >150 s) (rc $rc, screenshot in $OUT)" >&2
  exit 1
fi

if [ "$rc" -eq 0 ] && jq -e '.root.id != null' >/dev/null 2>&1 <<<"$out"; then
  echo "PASS: tree --depth 80 answered with a JSON tree"
  exit 0
fi
if grep -q 'AW-UIDRIVER-0[0-9][0-9]' "$OUT/tree.err"; then
  echo "PASS: tree --depth 80 failed clearly: $(head -n1 "$OUT/tree.err")"
  exit 0
fi
timeout 120 "$DRIVER" screenshot --out "$OUT/failed.png" >/dev/null 2>&1 || true
echo "FAIL: tree --depth 80 answered neither a tree nor an AW-UIDRIVER error (rc $rc): $(head -c 200 <<<"$out") $(head -c 200 "$OUT/tree.err") (screenshot in $OUT)" >&2
exit 1
