#!/usr/bin/env bash
# Tests for scripts/ci/bash-tests.sh — the CI bash-suite runner. Its suite list must
# be every tracked *.test.sh, minus excluded ones that each carry a reason, so a new
# suite can't be left out of CI by a glob that didn't match it. Independent suites run
# in a parallel pool; the timing-sensitive ones (SERIAL, each with a reason) run one at
# a time after the pool, alone on the machine.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUNNER="$ROOT/scripts/ci/bash-tests.sh"

fail() { echo "FAIL: $*"; exit 1; }

test_lists_every_tracked_suite_except_the_excluded() {
  local listed tracked excluded
  listed="$(bash "$RUNNER" --list)"
  excluded="$(bash "$RUNNER" --excluded | cut -f1)"
  tracked="$(git -C "$ROOT" ls-files '*.test.sh')"
  local want
  want="$(comm -23 <(printf '%s\n' "$tracked" | sort) <(printf '%s\n' "$excluded" | sed '/^$/d' | sort))"
  [ "$(printf '%s\n' "$listed" | sort)" = "$want" ] \
    || fail "runner list differs from tracked suites minus exclusions:
$(diff <(printf '%s\n' "$listed" | sort) <(printf '%s\n' "$want") || true)"
  echo "PASS: test_lists_every_tracked_suite_except_the_excluded"
}

test_suites_outside_the_old_globs_are_listed() {
  bash "$RUNNER" --list | has -x 'skills/ui-evidence/scripts/lib/tests/locks.test.sh' \
    || fail "a nested skill suite is not run"
  echo "PASS: test_suites_outside_the_old_globs_are_listed"
}

test_every_exclusion_is_tracked_and_has_a_reason() {
  local path reason
  while IFS=$'\t' read -r path reason; do
    [ -n "$path" ] || continue
    git -C "$ROOT" ls-files --error-unmatch "$path" >/dev/null 2>&1 || fail "excluded suite is not tracked: $path"
    [ "${#reason}" -ge 20 ] || fail "exclusion needs a reason: $path"
  done < <(bash "$RUNNER" --excluded)
  echo "PASS: test_every_exclusion_is_tracked_and_has_a_reason"
}

test_every_serial_suite_is_listed_and_has_a_reason() {
  local path reason listed n=0
  listed="$(bash "$RUNNER" --list)"
  while IFS=$'\t' read -r path reason; do
    [ -n "$path" ] || continue
    n=$((n + 1))
    printf '%s\n' "$listed" | has -xF "$path" || fail "serial suite is not one the runner runs: $path"
    [ "${#reason}" -ge 20 ] || fail "serial suite needs a reason: $path"
  done < <(bash "$RUNNER" --serial)
  [ "$n" -gt 0 ] || fail "no serial suites declared"
  echo "PASS: test_every_serial_suite_is_listed_and_has_a_reason"
}

test_the_timing_sensitive_suites_are_serial() {
  local serial s
  serial="$(bash "$RUNNER" --serial | cut -f1)"
  for s in scripts/tests/agent-token.test.sh config/hooks/tests/failopen.test.sh config/lib/tests/failopen.test.sh \
           config/lib/tests/statusline.test.sh config/hooks/tests/sindri-nudge.test.sh config/lib/tests/prompt-sort.test.sh \
           scripts/tests/readiness-baseline.test.sh config/lib/tests/aw-state-dir-isolation.test.sh \
           config/lib/tests/installed-hooks-smoke.test.sh config/lib/tests/install-wake-gating.test.sh; do
    printf '%s\n' "$serial" | has -xF "$s" || fail "timing-sensitive or shared-state suite is not serial: $s"
  done
  echo "PASS: test_the_timing_sensitive_suites_are_serial"
}

# A fixture repo whose suites record when they run. "active-<name>" marker dirs show which
# suites overlap: a parallel suite waits (bounded) until its partner is active too, which
# proves the pool runs them at the same time; a serial suite records any other active
# suite as an overlap.
make_fixture() {
  local d="$1"
  git -C "$d" init -q
  local par='set -u; mkdir "$MARK/active-$1"; i=0
while [ ! -d "$MARK/active-$2" ] && [ ! -e "$MARK/done-$2" ] && [ $i -lt 100 ]; do sleep 0.05; i=$((i + 1)); done
[ -d "$MARK/active-$2" ] && touch "$MARK/saw-$1-$2"
sleep 0.2; touch "$MARK/done-$1"; rmdir "$MARK/active-$1"'
  local ser='set -u; mkdir "$MARK/active-$1"
for a in "$MARK"/active-*; do [ "$a" = "$MARK/active-$1" ] || echo "$1 overlapped ${a##*/active-}" >> "$MARK/overlaps"; done
sleep 0.3
for a in "$MARK"/active-*; do [ "$a" = "$MARK/active-$1" ] || echo "$1 overlapped ${a##*/active-}" >> "$MARK/overlaps"; done
rmdir "$MARK/active-$1"'
  mkdir -p "$d/t"
  printf '%s\n' "set -- p1 p2" "$par" > "$d/t/p1.test.sh"
  printf '%s\n' "set -- p2 p1" "$par" > "$d/t/p2.test.sh"
  printf '%s\n' "set -- s1" "$ser" > "$d/t/s1.test.sh"
  printf '%s\n' "set -- s2" "$ser" > "$d/t/s2.test.sh"
  printf '%s\n' 'echo "should never run"; exit 1' > "$d/t/skipped.test.sh"
  printf '%s\n' 'echo "ok - a path with a space runs"' > "$d/t/with space.test.sh"
  git -C "$d" add t
}

FIXTURE_SERIAL=$'t/s1.test.sh\tfixture: serial because it records overlaps with any other suite\nt/s2.test.sh\tfixture: serial because it records overlaps with any other suite'
FIXTURE_EXCLUDED=$'t/skipped.test.sh\tfixture: excluded, and it fails if it ever runs at all'

run_fixture() { # <fixture dir> <mark dir> [runner args...]
  local d="$1" m="$2"; shift 2
  MARK="$m" BASH_TESTS_ROOT="$d" BASH_TESTS_JOBS=4 \
    BASH_TESTS_SERIAL="$FIXTURE_SERIAL" BASH_TESTS_EXCLUDED="$FIXTURE_EXCLUDED" \
    bash "$RUNNER" "$@"
}

test_pool_runs_in_parallel_and_serial_suites_run_alone() {
  local d m out rc=0
  d="$(mktemp -d)"; m="$(mktemp -d)"
  make_fixture "$d"
  out="$(run_fixture "$d" "$m" 2>&1)" || rc=$?
  [ "$rc" -eq 0 ] || fail "fixture run exited $rc:
$out"
  [ -e "$m/saw-p1-p2" ] && [ -e "$m/saw-p2-p1" ] || fail "parallel suites did not run at the same time:
$out"
  [ ! -e "$m/overlaps" ] || fail "a serial suite overlapped another suite: $(cat "$m/overlaps")"
  printf '%s\n' "$out" | has -E '^PASS +par +t/p1\.test\.sh +[0-9]+s$' || fail "summary lacks a par row with a duration:
$out"
  printf '%s\n' "$out" | has -E '^PASS +ser +t/s2\.test\.sh +[0-9]+s$' || fail "summary lacks a ser row with a duration:
$out"
  if printf '%s\n' "$out" | has 'should never run'; then fail "an excluded suite ran"; fi
  printf '%s\n' "$out" | has -E '^PASS +par +t/with space\.test\.sh +[0-9]+s$' || fail "a suite whose path has a space did not run as one suite:
$out"
  printf '%s\n' "$out" | has '5 run, 0 failed, 1 excluded' || fail "summary counts are wrong:
$out"
  rm -rf "$d" "$m"
  echo "PASS: test_pool_runs_in_parallel_and_serial_suites_run_alone"
}

test_a_failing_suite_fails_the_run_and_shows_its_log() {
  local d m out rc=0
  d="$(mktemp -d)"; m="$(mktemp -d)"
  make_fixture "$d"
  printf '%s\n' 'echo "ok - first"; echo "not ok - boom happened"; exit 3' > "$d/t/p1.test.sh"
  printf '%s\n' 'echo "not ok - serial boom"; exit 4' > "$d/t/s1.test.sh"
  out="$(run_fixture "$d" "$m" 2>&1)" || rc=$?
  [ "$rc" -eq 1 ] || fail "a failing suite must exit 1, got $rc:
$out"
  printf '%s\n' "$out" | has -E '^FAIL +par +t/p1\.test\.sh' || fail "the failing par suite is not reported:
$out"
  printf '%s\n' "$out" | has -E '^FAIL +ser +t/s1\.test\.sh' || fail "the failing ser suite is not reported:
$out"
  printf '%s\n' "$out" | has -F 'not ok - boom happened' || fail "the failing suite's log is not printed"
  printf '%s\n' "$out" | has -F 'exit 3' || fail "the failing suite's exit code is not printed"
  printf '%s\n' "$out" | has '2 failed' || fail "summary does not count the failures:
$out"
  rm -rf "$d" "$m"
  echo "PASS: test_a_failing_suite_fails_the_run_and_shows_its_log"
}

test_every_suite_gets_its_own_fresh_home_and_state_dir_whatever_the_caller_exported() {
  local d m out rc=0 f
  d="$(mktemp -d)"; m="$(mktemp -d)"
  git -C "$d" init -q; mkdir -p "$d/t"
  for f in a b; do
    printf '%s\n' 'echo "$HOME|$AW_STATE_DIR" >> "$MARK/seen"; [ "$HOME" != "$CALLER_HOME" ] && [ "$AW_STATE_DIR" != "$CALLER_STATE" ] && [ -d "$AW_STATE_DIR" ] && [ -d "$HOME" ] && [ -z "$(ls -A "$AW_STATE_DIR")" ]; rc=$?; echo x > "$AW_STATE_DIR/dirty"; exit $rc' > "$d/t/$f.test.sh"
  done
  git -C "$d" add t
  out="$(MARK="$m" CALLER_HOME=/caller/home CALLER_STATE=/caller/state HOME=/caller/home AW_STATE_DIR=/caller/state BASH_TESTS_ROOT="$d" \
    BASH_TESTS_JOBS=2 BASH_TESTS_SERIAL="" BASH_TESTS_EXCLUDED="" bash "$RUNNER" 2>&1)" || rc=$?
  [ "$rc" -eq 0 ] || fail "a suite saw the caller's HOME or state dir, or a dirty state dir (exit $rc):
$out"
  [ "$(sort -u "$m/seen" | wc -l | tr -d ' ')" = 2 ] || fail "two suites shared a HOME or state dir: $(cat "$m/seen")"
  rm -r "$d" "$m"
  echo "PASS: test_every_suite_gets_its_own_fresh_home_and_state_dir_whatever_the_caller_exported"
}

test_a_suites_temp_files_and_nuget_cache_stay_in_its_sandbox() {
  local d m out rc=0 t
  d="$(mktemp -d)"; m="$(mktemp -d)"
  git -C "$d" init -q; mkdir -p "$d/t"
  printf '%s\n' 'mkdir "$(mktemp -d | tee -a "$MARK/tmpdirs")/leak"; echo "$TMPDIR" >> "$MARK/tmpdir"' > "$d/t/a.test.sh"
  printf '%s\n' 'echo "${NUGET_PACKAGES:-unset}" >> "$MARK/nuget"' > "$d/t/b.test.sh"
  git -C "$d" add t
  out="$(MARK="$m" TMPDIR="$m" NUGET_PACKAGES=/shared/nuget BASH_TESTS_ROOT="$d" BASH_TESTS_JOBS=1 BASH_TESTS_SERIAL="" BASH_TESTS_EXCLUDED="" bash "$RUNNER" 2>&1)" || rc=$?
  [ "$rc" -eq 0 ] || fail "the run failed (exit $rc): $out"
  [ "$(cat "$m/nuget")" = /shared/nuget ] || fail "NUGET_PACKAGES not kept: $(cat "$m/nuget")"
  [ "$(cat "$m/tmpdir")" != "$m" ] || fail "the suite's TMPDIR is the caller's"
  while IFS= read -r t; do [ ! -e "$t" ] || fail "a suite's temp dir outlived it: $t"; done < "$m/tmpdirs"
  [ "$(find "$m" -mindepth 1 -maxdepth 1 -type d | wc -l | tr -d ' ')" = 0 ] || fail "the runner left a directory in the caller's TMPDIR: $(ls "$m")"
  printf '%s\n' 'mkdir -p "$HOME/.nuget/packages/x"' > "$d/t/c.test.sh"; git -C "$d" add t
  rc=0; out="$(MARK="$m" BASH_TESTS_ROOT="$d" BASH_TESTS_JOBS=1 BASH_TESTS_SERIAL="" BASH_TESTS_EXCLUDED="" bash "$RUNNER" 2>&1)" || rc=$?
  [ "$rc" -eq 1 ] || fail "a private NuGet cache must fail the suite (exit $rc): $out"
  printf '%s\n' "$out" | has 'private NuGet cache' || fail "the failure does not say why: $out"
  rm -r "$d" "$m"
  echo "PASS: test_a_suites_temp_files_and_nuget_cache_stay_in_its_sandbox"
}

test_a_suite_whose_sandbox_cannot_be_made_fails_instead_of_running() {
  local o out
  o="$(mktemp -d)"; : > "$o/x.test.sh"
  # A PATH stub makes mktemp fail the same way on every platform (BSD mktemp ignores a bad TMPDIR and falls back to /tmp).
  mkdir -p "$o/stub"; printf '#!/bin/sh\nexit 1\n' > "$o/stub/mktemp"; chmod +x "$o/stub/mktemp"
  out="$(PATH="$o/stub:$PATH" bash "$RUNNER" --run-one par "$o/x.test.sh" "$o" p001 2>&1)"
  [ "$(cat "$o/p001.rc")" = 2 ] || fail "a missing sandbox must fail the suite (rc file: $(cat "$o/p001.rc" 2>/dev/null)): $out"
  printf '%s\n' "$out" | has 'FAIL  par' || fail "the failure is not reported: $out"
  rm -r "$o"
  echo "PASS: test_a_suite_whose_sandbox_cannot_be_made_fails_instead_of_running"
}

test_the_sindri_home_and_local_bin_overrides_are_scrubbed_per_suite() {
  local d m out rc=0
  d="$(mktemp -d)"; m="$(mktemp -d)"
  git -C "$d" init -q; mkdir -p "$d/t"
  printf '%s\n' 'echo "${AW_SINDRI_HOME:-unset}|${CLAUDE_LOCAL_BIN:-unset}" >> "$MARK/seen"' > "$d/t/a.test.sh"
  git -C "$d" add t
  out="$(MARK="$m" AW_SINDRI_HOME=/caller/sindri CLAUDE_LOCAL_BIN=/caller/bin BASH_TESTS_ROOT="$d" BASH_TESTS_JOBS=1 BASH_TESTS_SERIAL="" BASH_TESTS_EXCLUDED="" bash "$RUNNER" 2>&1)" || rc=$?
  [ "$rc" -eq 0 ] || fail "fixture run exited $rc: $out"
  [ "$(cat "$m/seen")" = "unset|unset" ] || fail "a suite still saw the caller's AW_SINDRI_HOME or CLAUDE_LOCAL_BIN: $(cat "$m/seen")"
  rm -r "$d" "$m"
  echo "PASS: test_the_sindri_home_and_local_bin_overrides_are_scrubbed_per_suite"
}

test_the_lane_environment_is_stripped_per_suite() {
  local d m out rc=0 v seen
  d="$(mktemp -d)"; m="$(mktemp -d)"
  git -C "$d" init -q; mkdir -p "$d/t"
  # Every variable a lane or meta pane exports; a suite must see none of them, and still see what it legitimately needs.
  printf '%s\n' 'for v in AW_META_TEAM AW_META_DIR AW_META_LANE AW_META_LANE_PID AW_META_LANE_AGENT AW_META_LANE_PROC AW_SINDRI_CHILD AW_SINDRI_LAUNCH AW_LAUNCHER_CONFIG TMUX TMUX_PANE; do eval "[ -z \"\${$v+x}\" ]" || echo "leaked $v" >> "$MARK/seen"; done
[ -n "${GIT_CONFIG_GLOBAL:-}" ] && [ -n "${PATH:-}" ] || echo "lost a needed variable" >> "$MARK/seen"' > "$d/t/a.test.sh"
  git -C "$d" add t
  out="$(MARK="$m" GIT_CONFIG_GLOBAL=/caller/gitconfig AW_META_TEAM=t AW_META_DIR=/x AW_META_LANE=l AW_META_LANE_PID=1 AW_META_LANE_AGENT=a AW_META_LANE_PROC=p \
    AW_SINDRI_CHILD=1 AW_SINDRI_LAUNCH=x AW_LAUNCHER_CONFIG=/caller/launcher.json TMUX=/tmp/fake,1,0 TMUX_PANE=%1 BASH_TESTS_ROOT="$d" BASH_TESTS_JOBS=1 BASH_TESTS_SERIAL="" BASH_TESTS_EXCLUDED="" bash "$RUNNER" 2>&1)" || rc=$?
  [ "$rc" -eq 0 ] || fail "fixture run exited $rc: $out"
  seen="$(cat "$m/seen" 2>/dev/null || true)"
  [ -z "$seen" ] || fail "a suite saw the lane environment: $seen"
  rm -r "$d" "$m"
  echo "PASS: test_the_lane_environment_is_stripped_per_suite"
}

test_bad_configuration_is_a_tool_error() {
  local d m rc
  d="$(mktemp -d)"; m="$(mktemp -d)"
  make_fixture "$d"
  rc=0; MARK="$m" BASH_TESTS_ROOT="$d" BASH_TESTS_JOBS=0 BASH_TESTS_SERIAL="$FIXTURE_SERIAL" \
    BASH_TESTS_EXCLUDED="$FIXTURE_EXCLUDED" bash "$RUNNER" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 2 ] || fail "BASH_TESTS_JOBS=0 must exit 2, got $rc"
  rc=0; MARK="$m" BASH_TESTS_ROOT="$d" BASH_TESTS_EXCLUDED="$FIXTURE_EXCLUDED" \
    BASH_TESTS_SERIAL=$'t/nope.test.sh\tfixture: a serial path that is not tracked at all' \
    bash "$RUNNER" >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 2 ] || fail "an untracked serial suite must exit 2, got $rc"
  rc=0; bash "$RUNNER" --bogus >/dev/null 2>&1 || rc=$?
  [ "$rc" -eq 2 ] || fail "an unknown flag must exit 2, got $rc"
  rm -rf "$d" "$m"
  echo "PASS: test_bad_configuration_is_a_tool_error"
}

test_lists_every_tracked_suite_except_the_excluded
test_suites_outside_the_old_globs_are_listed
test_every_exclusion_is_tracked_and_has_a_reason
test_every_serial_suite_is_listed_and_has_a_reason
test_the_timing_sensitive_suites_are_serial
test_pool_runs_in_parallel_and_serial_suites_run_alone
test_a_failing_suite_fails_the_run_and_shows_its_log
test_every_suite_gets_its_own_fresh_home_and_state_dir_whatever_the_caller_exported
test_a_suites_temp_files_and_nuget_cache_stay_in_its_sandbox
test_a_suite_whose_sandbox_cannot_be_made_fails_instead_of_running
test_the_sindri_home_and_local_bin_overrides_are_scrubbed_per_suite
test_the_lane_environment_is_stripped_per_suite
test_bad_configuration_is_a_tool_error
