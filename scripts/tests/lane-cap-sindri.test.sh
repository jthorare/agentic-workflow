#!/usr/bin/env bash
# #2534: the three Aw.Sindri.Tests lane-rule violations CI reported (spec 4b.5), checked with the repo's own checker (tests/Aw.LaneCap.Testing):
#   LANE-TEST-001  Catalog.WriteAttributionTests (one test over the integration cap)
#   LANE-TEST-001  ApiGoldens.ApiGoldenTests     (one test over the integration cap)
#   LANE-TEST-002  Connections.ConnectionRefreshTests (a test with Lane traits unit and integration)
# One dotnet test call runs the three classes (several --filter-class values are OR'd; no --filter-trait, which would AND with them and
# hide a two-trait test), with a CTRF report, then the checker runs once over the report.
# Exit 0 = no violation; 1 = a lane-rule violation (the checker's exit); 2 = the test run failed, or a class matched no test.
# Run: bash scripts/tests/lane-cap-sindri.test.sh   (heavy: wrap it in ~/.agentic-workflow/bin-r1b/heavy)
set -uo pipefail
unset TMUX
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$(mktemp -d "${TMPDIR:-/tmp}/lane-cap-2534.XXXXXX")"
trap 'rm -r -- "$OUT"' EXIT
cd "$ROOT" || exit 2
classes=(Aw.Sindri.Tests.Catalog.WriteAttributionTests Aw.Sindri.Tests.ApiGoldens.ApiGoldenTests Aw.Sindri.Tests.Connections.ConnectionRefreshTests)
args=()
for c in "${classes[@]}"; do args+=(--filter-class "$c"); done
dotnet test --project tests/Aw.Sindri.Tests --configuration Release "${args[@]}" \
  --results-directory "$OUT" --report-xunit-ctrf || { echo "test run failed"; exit 2; }
for c in "${classes[@]}"; do
  if ! cat "$OUT"/*.ctrf 2>/dev/null | grep -q "\"name\": *\"$c\."; then
    echo "no test of $c ran: the filter matched nothing"; exit 2
  fi
done
dotnet run --project tests/Aw.LaneCap.Testing --configuration Release -- "$OUT"
