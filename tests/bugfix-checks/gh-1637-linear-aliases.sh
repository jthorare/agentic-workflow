#!/usr/bin/env bash
# tests/bugfix-checks/gh-1637-linear-aliases.sh — frozen regression check for #1637: an owned Linear issue's tracker identifier
# (SPL-103, from its stored linear.app permalink) is an alias of its intent row again, rows synced before the fix gain it, the
# item query answers and suggests it, and the backtest's brief source reads the key without a title prefix.
#   bash tests/bugfix-checks/gh-1637-linear-aliases.sh     exit 0 pass; non-zero the tests failed
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
exec dotnet test --project "$ROOT/tests/Aw.Context.Tests" \
  --filter-class "*.ItemShapeTests" --filter-class "*.SyncSourcesTests" --filter-class "*.StoreBriefSourceTests"
