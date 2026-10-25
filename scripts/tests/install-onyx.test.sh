#!/usr/bin/env bash
# scripts/install-onyx.sh and its place in setup.sh: the dry run, a missing or silent Docker, no terminal, the call to
# `sindri onyx up`, a failure, a rerun, and the --no-onyx opt-out. Docker and sindri are stubs; nothing is pulled.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# A bin dir with a fake docker ($1: answers | silent) and, as $TMP/app, a stub sindri that records its arguments.
make_world() {
  local docker_mode="$1" app_exit="${2:-0}"
  rm -rf "$TMP/bin" "$TMP/calls"
  mkdir -p "$TMP/bin"
  # a PATH with the tools the script needs but no docker unless asked
  for t in bash env cat dirname mkdir rm; do
    [ -x "/usr/bin/$t" ] && ln -sf "/usr/bin/$t" "$TMP/bin/$t"
    [ -x "/bin/$t" ] && ln -sf "/bin/$t" "$TMP/bin/$t"
  done
  case "$docker_mode" in
    answers) printf '#!/bin/sh\nexit 0\n' > "$TMP/bin/docker" ;;
    silent)  printf '#!/bin/sh\nexit 1\n' > "$TMP/bin/docker" ;;
  esac
  [ -f "$TMP/bin/docker" ] && chmod +x "$TMP/bin/docker"
  printf '#!/bin/sh\necho "$@" >> "%s"\necho "stub sindri ran: $*"\nexit %s\n' "$TMP/calls" "$app_exit" > "$TMP/app"
  chmod +x "$TMP/app"
}

run_install() {
  # shellcheck disable=SC2086
  PATH="$TMP/bin" AW_ONYX_SINDRI_APP="$TMP/app" "$@" /bin/bash "$ROOT/scripts/install-onyx.sh" 2>&1
}

test_dry_run_prints_the_step_and_runs_nothing() {
  make_world answers
  local out
  out="$(AW_DRY_RUN=1 run_install env)" || { echo "FAIL: dry run failed: $out"; exit 1; }
  grep -q "would run: $TMP/app onyx up" <<<"$out" || { echo "FAIL: dry-run line missing: $out"; exit 1; }
  [ ! -e "$TMP/calls" ] || { echo "FAIL: dry run called sindri"; exit 1; }
  echo "PASS: test_dry_run_prints_the_step_and_runs_nothing"
}

test_docker_missing_warns_with_the_fix_and_the_later_command() {
  make_world none
  local out rc=0
  out="$(AW_ONYX_FORCE_TERMINAL=1 run_install env)" || rc=$?
  [ "$rc" = 3 ] || { echo "FAIL: expected exit 3, got $rc: $out"; exit 1; }
  grep -q "Docker isn't installed" <<<"$out" || { echo "FAIL: no clear Docker text: $out"; exit 1; }
  grep -q "$TMP/app onyx up" <<<"$out" || { echo "FAIL: no later command: $out"; exit 1; }
  [ ! -e "$TMP/calls" ] || { echo "FAIL: sindri ran without Docker"; exit 1; }
  echo "PASS: test_docker_missing_warns_with_the_fix_and_the_later_command"
}

test_docker_not_answering_says_to_start_it() {
  make_world silent
  local out rc=0
  out="$(AW_ONYX_FORCE_TERMINAL=1 run_install env)" || rc=$?
  [ "$rc" = 3 ] || { echo "FAIL: expected exit 3, got $rc: $out"; exit 1; }
  grep -q "not answering" <<<"$out" || { echo "FAIL: no not-answering text: $out"; exit 1; }
  [ ! -e "$TMP/calls" ] || { echo "FAIL: sindri ran with Docker down"; exit 1; }
  echo "PASS: test_docker_not_answering_says_to_start_it"
}

test_no_terminal_skips_with_the_later_command() {
  make_world answers
  local out rc=0
  out="$(run_install env < /dev/null)" || rc=$?
  [ "$rc" = 3 ] || { echo "FAIL: expected exit 3, got $rc: $out"; exit 1; }
  grep -q "needs you at a terminal" <<<"$out" || { echo "FAIL: no terminal text: $out"; exit 1; }
  [ ! -e "$TMP/calls" ] || { echo "FAIL: sindri ran without a terminal"; exit 1; }
  echo "PASS: test_no_terminal_skips_with_the_later_command"
}

test_runs_onyx_up_and_a_rerun_calls_it_again() {
  make_world answers
  local out
  out="$(AW_ONYX_FORCE_TERMINAL=1 run_install env)" || { echo "FAIL: run failed: $out"; exit 1; }
  grep -q "stub sindri ran: onyx up" <<<"$out" || { echo "FAIL: onyx up not run: $out"; exit 1; }
  grep -q "Onyx: done" <<<"$out" || { echo "FAIL: no done line: $out"; exit 1; }
  out="$(AW_ONYX_FORCE_TERMINAL=1 run_install env)" || { echo "FAIL: rerun failed: $out"; exit 1; }
  [ "$(cat "$TMP/calls")" = "$(printf 'onyx up\nonyx up')" ] || { echo "FAIL: calls were: $(cat "$TMP/calls")"; exit 1; }
  echo "PASS: test_runs_onyx_up_and_a_rerun_calls_it_again"
}

test_a_failed_onyx_up_exits_1_and_says_how_to_rerun() {
  make_world answers 1
  local out rc=0
  out="$(AW_ONYX_FORCE_TERMINAL=1 run_install env)" || rc=$?
  [ "$rc" = 1 ] || { echo "FAIL: expected exit 1, got $rc: $out"; exit 1; }
  grep -q "did not finish" <<<"$out" || { echo "FAIL: no failure text: $out"; exit 1; }
  grep -q "Rerun: $TMP/app onyx up" <<<"$out" || { echo "FAIL: no rerun hint: $out"; exit 1; }
  echo "PASS: test_a_failed_onyx_up_exits_1_and_says_how_to_rerun"
}

test_setup_runs_it_with_sindri_and_no_onyx_opts_out() {
  local with without
  with="$(bash "$ROOT/setup.sh" --providers claude --dry-run --with-sindri 2>&1)" || { echo "FAIL: setup dry run failed"; exit 1; }
  grep -q "=== Onyx (Sindri's business-context search) ===" <<<"$with" || { echo "FAIL: --with-sindri did not plan the Onyx step"; exit 1; }
  grep -q "would run: sindri onyx up" <<<"$with" || { echo "FAIL: Onyx dry-run line missing from setup"; exit 1; }
  without="$(bash "$ROOT/setup.sh" --providers claude --dry-run --with-sindri --no-onyx 2>&1)" || { echo "FAIL: setup dry run with --no-onyx failed"; exit 1; }
  ! grep -q "=== Onyx (Sindri's business-context search) ===" <<<"$without" || { echo "FAIL: --no-onyx still planned the Onyx step"; exit 1; }
  echo "PASS: test_setup_runs_it_with_sindri_and_no_onyx_opts_out"
}

test_setup_without_sindri_does_not_touch_onyx() {
  local out
  out="$(bash "$ROOT/setup.sh" --providers claude --dry-run 2>&1)" || { echo "FAIL: setup dry run failed"; exit 1; }
  ! grep -q "=== Onyx (Sindri's business-context search) ===" <<<"$out" || { echo "FAIL: Onyx planned without --with-sindri"; exit 1; }
  echo "PASS: test_setup_without_sindri_does_not_touch_onyx"
}

test_dry_run_prints_the_step_and_runs_nothing
test_docker_missing_warns_with_the_fix_and_the_later_command
test_docker_not_answering_says_to_start_it
test_no_terminal_skips_with_the_later_command
test_runs_onyx_up_and_a_rerun_calls_it_again
test_a_failed_onyx_up_exits_1_and_says_how_to_rerun
test_setup_runs_it_with_sindri_and_no_onyx_opts_out
test_setup_without_sindri_does_not_touch_onyx
echo "install-onyx.test.sh: all passed"
