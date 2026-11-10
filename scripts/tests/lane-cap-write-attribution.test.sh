#!/usr/bin/env bash
# #2534: WriteAttributionTests' launched-session sweep stays within the integration lane's per-test cap (LANE-TEST-001).
# Runs ONLY that class (integration lane, CTRF report) and feeds the report to the repo's own checker (tests/Aw.LaneCap.Testing).
# Exit 0 = every test of the class is within its lane cap; 1 = a cap violation (the checker's exit); 2 = the test run itself failed.
# Run: bash scripts/tests/lane-cap-write-attribution.test.sh   (heavy: wrap with ~/.agentic-workflow/bin-r1b/heavy)
set -uo pipefail
unset TMUX
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="$(mktemp -d "${TMPDIR:-/tmp}/lane-cap-2534.XXXXXX")"
trap 'rm -r -- "$OUT"' EXIT
cd "$ROOT" || exit 2
dotnet test --project tests/Aw.Sindri.Tests --configuration Release \
  --filter-class "Aw.Sindri.Tests.Catalog.WriteAttributionTests" \
  --filter-trait "Lane=integration" \
  --results-directory "$OUT" --report-xunit-ctrf || { echo "test run failed"; exit 2; }
dotnet run --project tests/Aw.LaneCap.Testing --configuration Release -- "$OUT"
