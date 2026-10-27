#!/usr/bin/env bash
# Tests for scripts/cutover-prove.sh: argument parsing, the guard and the dry run. Builds nothing and runs no switch step.
# Run: bash scripts/tests/cutover-prove.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROVE="$DIR/../cutover-prove.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac; }
lacks() { case "$2" in *"$3"*) echo "not ok - $1"; echo "  unexpected: $3"; fail=1 ;; *) echo "ok - $1" ;; esac; }

TMP="$(cd "$(mktemp -d)" && pwd -P)"
trap 'find "$TMP" -delete 2> /dev/null' EXIT
export TMPDIR="$TMP"
cd "$TMP" || exit 2

out="$(bash "$PROVE" --help 2>&1)"; rc=$?
check "help exits 0" "$rc" 0
has "help names the flags" "$out" "--dry-run"

out="$(bash "$PROVE" --bogus 2>&1)"; rc=$?
check "an unknown flag is a usage error" "$rc" 2
has "usage is printed" "$out" "cutover-prove.sh"

out="$(bash "$PROVE" --dry-run --budget-ms abc 2>&1)"; rc=$?
check "a non-numeric budget is refused" "$rc" 2
out="$(bash "$PROVE" --dry-run --budget-ms 2>&1)"; rc=$?
check "--budget-ms without a value is refused" "$rc" 2
out="$(bash "$PROVE" --dry-run --work 2>&1)"; rc=$?
check "--work without a value is refused" "$rc" 2

out="$(bash "$PROVE" --dry-run --work /var/cutover-prove-x 2>&1)"; rc=$?
check "a scratch box outside the temp root is refused" "$rc" 2
has "the guard names the temp root" "$out" "strictly inside"
out="$(bash "$PROVE" --dry-run --work "$TMP" 2>&1)"; rc=$?
check "the temp root itself is refused" "$rc" 2
mkdir "$TMP/exists"
out="$(bash "$PROVE" --dry-run --work "$TMP/exists" 2>&1)"; rc=$?
check "an existing scratch box is refused" "$rc" 2
ln -s /var "$TMP/escape"
out="$(bash "$PROVE" --dry-run --work "$TMP/escape/box" 2>&1)"; rc=$?
check "a path that resolves outside the temp root through a link is refused" "$rc" 2

out="$(bash "$PROVE" --dry-run --work "$TMP/box" --budget-ms 1234 --keep --skip-downloads 2>&1)"; rc=$?
check "dry run exits 0" "$rc" 0
check "dry run writes nothing" "$([ -e "$TMP/box" ] && echo exists || echo absent)" absent
has "dry run names the scratch box" "$out" "scratch box $TMP/box"
has "dry run shows the budget" "$out" "nudge budget 1234 ms"
has "dry run shows the flags" "$out" "downloads skipped; keep=1"
has "dry run shows HOME unchanged" "$out" "HOME stays $HOME"
for c in failed-publish-changes-nothing step1-refusal-changes-nothing publish pre-link link post-link removed-exactly-8 kept-byte-identical \
  hook-v3 hook-refuses-secret hook-passes-clean hook-suites seeded-hooks judge-stub-sort-why judge-stub-sort-promote setup-dry-run \
  second-run-no-change live-judge-state-survives nudge-cold-budget; do
  has "dry run lists check $c" "$out" "check $c "
done
lacks "dry run runs no switch step" "$out" "PASS "

# the default budget follows the nudge hook's own variable
out="$(AW_SINDRI_NUDGE_BUDGET_MS=777 bash "$PROVE" --dry-run 2>&1)"
has "the budget defaults to the hook's variable" "$out" "nudge budget 777 ms"
out="$(bash "$PROVE" --dry-run 2>&1)"
has "the default scratch box is under the temp root" "$out" "scratch box $TMP/cutover-prove."
check "no scratch box was created by any dry run" "$(find "$TMP" -maxdepth 1 -name 'cutover-prove.*' | wc -l | tr -d ' ')" 0

# a non-dry run refuses to start without the cleanup script (nothing seeded, nothing built)
cp "$PROVE" "$TMP/lone.sh"; mkdir -p "$TMP/lone-root/scripts"; mv "$TMP/lone.sh" "$TMP/lone-root/scripts/cutover-prove.sh"
out="$(bash "$TMP/lone-root/scripts/cutover-prove.sh" --work "$TMP/lone-box" 2>&1)"; rc=$?
check "a run without cutover-clean-ts.sh is a usage-level error" "$rc" 2
has "it names the missing script" "$out" "cutover-clean-ts.sh"
check "and creates no scratch box" "$([ -e "$TMP/lone-box" ] && echo exists || echo absent)" absent

exit "$fail"
