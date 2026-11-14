#!/usr/bin/env bash
# Narrow check for the #3405 smoke race. Runs the aot-smoke with the map-refresh cadence at 300s and looks only at two lines of the
# actual output: the subgraph QUERY `"http=200"` and the succeeded RefreshContextMap job. The rest of the diff is ignored on
# purpose (AW_SMOKE_MAP_EVERY also changes the services row's `every`, so the whole expected.txt diff fails even on the fix).
# Prints one summary line on stdout. Exit 0 both lines present, 1 a line missing (named on stderr), 2 the smoke did not produce an actual.txt.
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
actual="$root/artifacts/aot-sindri-smoke/actual.txt"
rm -f "$actual"
AW_SMOKE_MAP_EVERY=300s bash "$here/run.sh" >/dev/null 2>&1 || true
[ -s "$actual" ] || { echo "map-wait-check: run.sh wrote no $actual" >&2; exit 2; }
rc=0
rc_missing=0
grep -qx '"http=200"' "$actual" || { echo 'map-wait-check: missing line: "http=200" (the subgraph QUERY)' >&2; rc=1; rc_missing=$((rc_missing + 1)); }
grep -Eq '^JOB-ID +succeeded +RefreshContextMap ' "$actual" || { echo "map-wait-check: missing line: succeeded RefreshContextMap job" >&2; rc=1; rc_missing=$((rc_missing + 1)); }
if [ "$rc" -eq 0 ]; then echo "map-wait-check: both lines present"; else echo "map-wait-check: $rc_missing line(s) missing"; fi
exit "$rc"
