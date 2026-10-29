#!/usr/bin/env bash
# Tests for scripts/ci/lane-budgets.mjs — the per-package lane budget check (spec §4b.5,
# .agents/rules/csharp-testing.md "Lane budgets"). It times
# `dotnet test --project <test project> --no-build --configuration <C> --filter-trait "Lane=<lane>" --ignore-exit-code 8`
# per test project, sums the wall clock per package (the deps.yaml projectPrefix, longest
# wins) and fails a package at or over its lane budget. A stub `dotnet` stands in for the SDK.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/ci/lane-budgets.mjs"

fail() { echo "FAIL: $*"; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# A fixture repo: two packages whose prefixes nest (Aw.Foo, Aw.Foo.Data), a test-support
# project (never run), and a test assembly no package owns (Aw.Architecture.Tests).
make_fixture() {
  local d="$1"
  mkdir -p "$d/tests"
  cat > "$d/deps.yaml" <<'YAML'
version: 1
packages:
  - id: Foo
    kind: package
    tier: 0
    projectPrefix: Aw.Foo
  - id: FooData
    kind: package
    tier: 0
    sublevel: 1
    projectPrefix: Aw.Foo.Data
  - id: Hooks
    kind: shell
YAML
  local p
  for p in Aw.Foo.Tests Aw.Foo.Core.Tests Aw.Foo.Data.Tests Aw.Foo.Testing Aw.Architecture.Tests; do
    mkdir -p "$d/tests/$p"; echo '<Project />' > "$d/tests/$p/$p.csproj"
  done
  cat > "$d/AgenticWorkflow.slnx" <<'XML'
<Solution>
  <Folder Name="/tests/">
    <Project Path="tests/Aw.Foo.Tests/Aw.Foo.Tests.csproj" />
    <Project Path="tests/Aw.Foo.Core.Tests/Aw.Foo.Core.Tests.csproj" />
    <Project Path="tests/Aw.Foo.Data.Tests/Aw.Foo.Data.Tests.csproj" />
    <Project Path="tests/Aw.Foo.Testing/Aw.Foo.Testing.csproj" />
    <Project Path="tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj" />
  </Folder>
</Solution>
XML
}

# The stub logs its arguments, sleeps STUB_<project>_<lane> seconds (dots as _), and exits
# STUB_EXIT_<project>_<lane> (default 0).
cat > "$WORK/dotnet" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$STUB_LOG"
proj=""; lane=""
while [ $# -gt 0 ]; do
  case "$1" in
    --project) proj="$(basename "$2" .csproj)"; shift ;;
    --filter-trait) lane="${2#Lane=}"; shift ;;
  esac
  shift
done
key="$(echo "${proj}_${lane}" | tr '.' '_')"
s="STUB_${key}"; e="STUB_EXIT_${key}"
sleep "${!s:-0}"
exit "${!e:-0}"
STUB
chmod +x "$WORK/dotnet"

secs_of() { printf '%s\n' "$OUT" | sed -nE "s/^(ok|OVER) +$1 +$2 +([0-9.]+) s .*/\\2/p"; }
at_least() { [ -n "$1" ] && awk -v a="$1" -v b="$2" 'BEGIN { exit !(a + 0 >= b + 0) }'; }

run_check() { # <fixture> [args...]; sets OUT and RC
  local d="$1"; shift
  RC=0
  OUT="$(STUB_LOG="$WORK/log" node "$CHECK" --root "$d" --dotnet "$WORK/dotnet" "$@" 2>&1)" || RC=$?
}

test_sums_each_package_and_passes_under_budget() {
  local d="$WORK/ok"; make_fixture "$d"; : > "$WORK/log"
  export STUB_Aw_Foo_Tests_unit=0.3 STUB_Aw_Foo_Core_Tests_unit=0.3 STUB_Aw_Foo_Data_Tests_integration=0.2
  # Budgets the stubs can't approach: this suite runs in the parallel pool, so only lower
  # bounds on the measured sums are asserted.
  run_check "$d" --budget unit=60,integration=60
  unset STUB_Aw_Foo_Tests_unit STUB_Aw_Foo_Core_Tests_unit STUB_Aw_Foo_Data_Tests_integration
  [ "$RC" -eq 0 ] || fail "under budget must exit 0, got $RC:
$OUT"
  # Foo's unit lane is the sum of its two projects (at least 0.6 s; no upper bound, because
  # this suite runs in the parallel pool); FooData is its own package.
  at_least "$(secs_of Foo unit)" 0.6 || fail "Foo unit should sum both projects (>= 0.6 s):
$OUT"
  printf '%s\n' "$OUT" | has -E '^ok +Foo +unit .* limit 60 s +\(2 projects\)' || fail "Foo unit row should name its limit and 2 projects:
$OUT"
  at_least "$(secs_of FooData integration)" 0.2 || fail "Aw.Foo.Data.Tests should count for FooData (longest prefix):
$OUT"
  printf '%s\n' "$OUT" | has -E '^ok +FooData +integration .*\(1 project\)' || fail "FooData integration is its own one-project row:
$OUT"
  printf '%s\n' "$OUT" | has -E '^ok +\(Aw\.Architecture\.Tests\) +unit' || fail "an unowned test assembly is its own row:
$OUT"
  grep -q 'Aw.Foo.Testing' "$WORK/log" && fail "a *.Testing project is not a test project and must not run"
  grep -q 'Lane=heavy' "$WORK/log" && fail "the heavy lane has no budget and must not run"
  grep -qF -- '--project tests/Aw.Foo.Tests/Aw.Foo.Tests.csproj' "$WORK/log" || fail "dotnet test not called per project:
$(cat "$WORK/log")"
  grep -F -- 'Aw.Foo.Tests.csproj' "$WORK/log" | grep -F -- '--filter-trait Lane=unit' | grep -F -- '--ignore-exit-code 8' | has -F -- '--no-build' \
    || fail "missing lane filter, exit-8 ignore or --no-build:
$(cat "$WORK/log")"
  echo "PASS: test_sums_each_package_and_passes_under_budget"
}

test_a_package_over_budget_fails_with_the_rule_id() {
  local d="$WORK/over"; make_fixture "$d"; : > "$WORK/log"
  # Aw.Foo.Tests is the slower of Foo's two projects, so the fix command names it.
  export STUB_Aw_Foo_Tests_unit=0.6 STUB_Aw_Foo_Core_Tests_unit=0.1
  run_check "$d" --budget unit=0.5,integration=5
  unset STUB_Aw_Foo_Tests_unit STUB_Aw_Foo_Core_Tests_unit
  [ "$RC" -eq 1 ] || fail "over budget must exit 1, got $RC:
$OUT"
  printf '%s\n' "$OUT" | grep -E '^LANE-PKG-001' | grep -F 'Foo' | grep -F 'unit' | grep -F 'limit 0.5 s' \
    | grep -F 'Slowest project: Aw.Foo.Tests' | has -F 'dotnet test --project tests/Aw.Foo.Tests/Aw.Foo.Tests.csproj' \
    || fail "LANE-PKG-001 must name the package, lane, measured s, limit, the slowest project and the fix command:
$OUT"
  printf '%s\n' "$OUT" | has -E '^ok +FooData' || fail "a package under budget still reports ok:
$OUT"
  echo "PASS: test_a_package_over_budget_fails_with_the_rule_id"
}

test_a_failing_test_run_fails_the_check() {
  local d="$WORK/red"; make_fixture "$d"; : > "$WORK/log"
  export STUB_EXIT_Aw_Foo_Data_Tests_integration=2
  run_check "$d" --budget unit=5,integration=5
  unset STUB_EXIT_Aw_Foo_Data_Tests_integration
  [ "$RC" -eq 1 ] || fail "a failing dotnet test must exit 1, got $RC:
$OUT"
  printf '%s\n' "$OUT" | grep -E '^LANE-PKG-002' | grep -F 'Aw.Foo.Data.Tests' | has -F 'exit 2' \
    || fail "LANE-PKG-002 must name the project and the exit code:
$OUT"
  echo "PASS: test_a_failing_test_run_fails_the_check"
}

test_tool_errors_exit_2() {
  local d="$WORK/bad"; make_fixture "$d"
  run_check "$d" --budget unit=abc
  [ "$RC" -eq 2 ] || fail "a bad --budget must exit 2, got $RC"
  rm "$d/AgenticWorkflow.slnx"
  run_check "$d"
  [ "$RC" -eq 2 ] || fail "a missing solution must exit 2, got $RC"
  run_check "$d" --bogus
  [ "$RC" -eq 2 ] || fail "an unknown flag must exit 2, got $RC"
  run_check "$d" --budget constructor=5
  [ "$RC" -eq 2 ] || fail "an inherited object key is not a lane and must exit 2, got $RC"
  echo "PASS: test_tool_errors_exit_2"
}

test_no_test_project_is_a_tool_error() {
  local d="$WORK/empty"; make_fixture "$d"
  printf '<Solution>\n  <Project Path="src/Aw.Foo/Aw.Foo.csproj" />\n</Solution>\n' > "$d/AgenticWorkflow.slnx"
  run_check "$d"
  [ "$RC" -eq 2 ] || fail "a solution with no *.Tests project must exit 2 (nothing timed is not a pass), got $RC:
$OUT"
  printf '%s\n' "$OUT" | has 'no \*.Tests project' || fail "the error must say there is nothing to time:
$OUT"
  echo "PASS: test_no_test_project_is_a_tool_error"
}

test_path_need_not_be_the_first_attribute() {
  local d="$WORK/attrs"; make_fixture "$d"; : > "$WORK/log"
  printf '<Solution>\n  <Project Type="Classic C#" Path="tests/Aw.Foo.Tests/Aw.Foo.Tests.csproj" />\n</Solution>\n' > "$d/AgenticWorkflow.slnx"
  run_check "$d"
  [ "$RC" -eq 0 ] || fail "expected exit 0, got $RC:
$OUT"
  grep -qF -- '--project tests/Aw.Foo.Tests/Aw.Foo.Tests.csproj' "$WORK/log" || fail "a Project element with Path after another attribute was skipped:
$OUT"
  echo "PASS: test_path_need_not_be_the_first_attribute"
}

test_default_budgets_are_the_rule_numbers() {
  local d="$WORK/defaults"; make_fixture "$d"
  run_check "$d"
  [ "$RC" -eq 0 ] || fail "default run must pass on the instant stub, got $RC:
$OUT"
  # The script's budgets must equal Aw.Kernel.Testing's Lanes.BudgetOf, which the per-test cap reads.
  local unit integration
  unit="$(sed -nE 's/.*TestLane\.Unit => TimeSpan\.FromSeconds\(([0-9]+)\).*/\1/p' "$ROOT/tests/Aw.Kernel.Testing/Lanes.cs")"
  integration="$(sed -nE 's/.*TestLane\.Integration => TimeSpan\.FromSeconds\(([0-9]+)\).*/\1/p' "$ROOT/tests/Aw.Kernel.Testing/Lanes.cs")"
  [ -n "$unit" ] && [ -n "$integration" ] || fail "could not read the budgets from tests/Aw.Kernel.Testing/Lanes.cs"
  printf '%s\n' "$OUT" | has -E "unit .*limit $unit s" || fail "the unit budget must be Lanes.cs's $unit s:
$OUT"
  printf '%s\n' "$OUT" | has -E "integration .*limit $integration s" || fail "the integration budget must be Lanes.cs's $integration s:
$OUT"
  echo "PASS: test_default_budgets_are_the_rule_numbers"
}

test_sindri_ui_budgets_are_600_s_and_28_s() {
  local d="$WORK/sindriui"; make_fixture "$d"
  mkdir -p "$d/tests/Aw.Sindri.Ui.Tests"; echo '<Project />' > "$d/tests/Aw.Sindri.Ui.Tests/Aw.Sindri.Ui.Tests.csproj"
  perl -pi -e 's#^(\s*)</Folder>#$1  <Project Path="tests/Aw.Sindri.Ui.Tests/Aw.Sindri.Ui.Tests.csproj" />\n$1</Folder>#' "$d/AgenticWorkflow.slnx"
  printf '  - id: SindriUi\n    kind: package\n    tier: 4\n    projectPrefix: Aw.Sindri.Ui\n' >> "$d/deps.yaml"
  run_check "$d"
  printf '%s\n' "$OUT" | has -E 'SindriUi +integration +[0-9.]+ s +limit 600 s' || fail "SindriUi's integration limit is 600 s (owner, 2026-10-28, q148):
$OUT"
  printf '%s\n' "$OUT" | has -E 'SindriUi +unit +[0-9.]+ s +limit 28 s' || fail "SindriUi's unit limit is 28 s (owner, 2026-10-28, q148):
$OUT"
  printf '%s\n' "$OUT" | has -E '^ok +Foo +integration +[0-9.]+ s +limit 60 s' || fail "other packages keep 60 s:
$OUT"
  echo "PASS: test_sindri_ui_budgets_are_600_s_and_28_s"
}

test_sindri_budgets_are_340_s_and_100_s() {
  local d="$WORK/sindri"; make_fixture "$d"
  mkdir -p "$d/tests/Aw.Sindri.Tests"; echo '<Project />' > "$d/tests/Aw.Sindri.Tests/Aw.Sindri.Tests.csproj"
  perl -pi -e 's#^(\s*)</Folder>#$1  <Project Path="tests/Aw.Sindri.Tests/Aw.Sindri.Tests.csproj" />\n$1</Folder>#' "$d/AgenticWorkflow.slnx"
  printf '  - id: Sindri\n    kind: package\n    tier: 4\n    projectPrefix: Aw.Sindri.Tests\n' >> "$d/deps.yaml"
  run_check "$d"
  printf '%s\n' "$OUT" | has -E '^ok +Sindri +integration +[0-9.]+ s +limit 340 s' || fail "Sindri's integration limit is 340 s (owner, 2026-10-28, q148):
$OUT"
  printf '%s\n' "$OUT" | has -E 'Sindri +unit +[0-9.]+ s +limit 100 s' || fail "Sindri's unit limit is 100 s (owner, 2026-10-28, q148):
$OUT"
  printf '%s\n' "$OUT" | has -E '^ok +Foo +integration +[0-9.]+ s +limit 60 s' || fail "other packages keep 60 s:
$OUT"
  echo "PASS: test_sindri_budgets_are_340_s_and_100_s"
}

test_a_listed_test_project_is_its_own_row_outside_the_package_sum() {
  local d="$WORK/engine"; make_fixture "$d"
  local p
  for p in Aw.Sindri.Tests Aw.Sindri.Engine.Tests; do
    mkdir -p "$d/tests/$p"; echo '<Project />' > "$d/tests/$p/$p.csproj"
    perl -pi -e 's#^(\s*)</Folder>#$1  <Project Path="tests/'"$p"'/'"$p"'.csproj" />\n$1</Folder>#' "$d/AgenticWorkflow.slnx"
  done
  printf '  - id: Sindri\n    kind: package\n    tier: 4\n    projectPrefix: Aw.Sindri\n' >> "$d/deps.yaml"
  # Each project is under its own limit; a Sindri sum would count both.
  STUB_Aw_Sindri_Tests_integration=1 STUB_Aw_Sindri_Engine_Tests_integration=1 run_check "$d"
  printf '%s\n' "$OUT" | has -E '^ok +Sindri +integration +[0-9.]+ s +limit 340 s +\(1 project\)' || fail "Aw.Sindri.Tests is the Sindri row on its own (owner, 2026-10-18):
$OUT"
  printf '%s\n' "$OUT" | has -E '^ok +Aw\.Sindri\.Engine\.Tests +integration +[0-9.]+ s +limit 250 s +\(1 project\)' || fail "Aw.Sindri.Engine.Tests is a row of its own, kept out of the Sindri sum (owner, 2026-10-18):
$OUT"
  printf '%s\n' "$OUT" | has -E '^ok +Aw\.Sindri\.Engine\.Tests +unit +[0-9.]+ s +limit 10 s' || fail "the project row keeps the unit limit:
$OUT"
  echo "PASS: test_a_listed_test_project_is_its_own_row_outside_the_package_sum"
}

test_sums_each_package_and_passes_under_budget
test_sindri_ui_budgets_are_600_s_and_28_s
test_sindri_budgets_are_340_s_and_100_s
test_a_listed_test_project_is_its_own_row_outside_the_package_sum
test_a_package_over_budget_fails_with_the_rule_id
test_a_failing_test_run_fails_the_check
test_tool_errors_exit_2
test_no_test_project_is_a_tool_error
test_path_need_not_be_the_first_attribute
test_default_budgets_are_the_rule_numbers
