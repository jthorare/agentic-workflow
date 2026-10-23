#!/usr/bin/env bash
# scripts/ci/bash-tests.sh — run every tracked bash test suite (`git ls-files '*.test.sh'`),
# each in its own process, and fail if any of them fails. The list comes from git, so a
# new suite can't be missed by a glob; a suite is skipped only through EXCLUDED below,
# with its reason.
#
# Every suite runs with a fresh temp HOME and AW_STATE_DIR (see --run-one), never the caller's, and without the lane environment.
#
# Two phases:
#   1. the pool: every suite not in SERIAL, BASH_TESTS_JOBS at a time (default: the CPU
#      count), each with its own log;
#   2. the serial phase: the SERIAL suites, one at a time, after the pool has drained, so
#      a suite that measures time or races signals runs on a quiet machine.
# One line per suite as it finishes (`PASS|FAIL  par|ser  <path>  <n>s`), then each failed
# suite's failing assertions and log tail, the slowest suites, and a summary.
#
# Usage: scripts/ci/bash-tests.sh              run the suites
#        scripts/ci/bash-tests.sh --list       print the suites it would run
#        scripts/ci/bash-tests.sh --excluded   print "path<TAB>reason" per exclusion
#        scripts/ci/bash-tests.sh --serial     print "path<TAB>reason" per serial suite
# Exit: 0 every suite passed, 1 a suite failed, 2 a usage or configuration error.
#
# Test hooks (scripts/tests/ci-bash-tests.test.sh, never CI): BASH_TESTS_ROOT runs against
# another git checkout, and BASH_TESTS_EXCLUDED / BASH_TESTS_SERIAL (path<TAB>reason lines)
# replace the two lists below.
# Bash 3.2 compatible (macOS /bin/bash): xargs -P is the pool, no wait -n or mapfile.
set -uo pipefail

SELF="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"

# --run-one <mode> <suite> <outdir> <index>: the pool's (and the serial phase's) worker.
# Runs one suite with its own log and writes <index>.rc and <index>.secs next to it.
if [ "${1:-}" = "--run-one" ]; then
  mode="$2" suite="$3" out="$4" idx="$5"
  start=$SECONDS
  # Fail closed on state: every suite gets its own fresh HOME and AW_STATE_DIR (a temp dir, removed afterwards), whatever the caller
  # exported, so no suite can read or write the real ~/.agentic-workflow or a state dir a caller pointed at. What the real HOME
  # holds for tools stays reachable by explicit pointers (git identity and config, the .NET SDK, the NuGet and npm caches).
  sandbox="$(mktemp -d)" && mkdir -p "$sandbox/home" "$sandbox/state" "$sandbox/tmp" || {
    echo "bash-tests: could not make a sandbox for $suite" > "$out/$idx.log"; echo 2 > "$out/$idx.rc"; echo 0 > "$out/$idx.secs"
    printf 'FAIL  %s  %s  0s  exit 2 (no sandbox)\n' "$mode" "$suite"; exit 0
  }
  real_home="$HOME"
  [ ! -f "$real_home/.gitconfig" ] || export GIT_CONFIG_GLOBAL="${GIT_CONFIG_GLOBAL:-$real_home/.gitconfig}"
  [ ! -d "$real_home/.dotnet" ] || export DOTNET_ROOT="${DOTNET_ROOT:-$real_home/.dotnet}" PATH="$real_home/.dotnet:$PATH"
  export NUGET_PACKAGES="${NUGET_PACKAGES:-$real_home/.nuget/packages}" npm_config_cache="${npm_config_cache:-$real_home/.npm}"
  # One list of what a suite must not inherit, built as `env -u` flags. Two families:
  #  - the hook and provider overrides the install helpers honour: a caller's must not steer a suite at real files;
  #  - the lane environment: every lane and meta pane exports AW_META_LANE*, AW_META_TEAM, AW_META_DIR, AW_SINDRI_CHILD,
  #    AW_SINDRI_LAUNCH, AW_LAUNCHER_CONFIG, TMUX and TMUX_PANE. Hooks (session-id-report, meta-lane-gate) and meta-launch branch on them, so a
  #    suite run from a lane fails on state it never set. Suites that need one set it themselves.
  # GIT_CONFIG_GLOBAL, DOTNET_ROOT, NUGET_PACKAGES, npm_config_cache and PATH are deliberately kept.
  unset_flags=()
  for v in CLAUDE_SETTINGS_FILE CLAUDE_HOOKS_DIR CLAUDE_DIR CODEX_HOOKS_FILE CODEX_HOME CURSOR_HOOKS_FILE AW_HOOKS_DIR AW_HOOK_ADAPTER_FLAGS \
    AW_META_CONFIG AW_SINDRI_HOME CLAUDE_LOCAL_BIN \
    AW_META_TEAM AW_META_DIR AW_META_LANE AW_META_LANE_PID AW_META_LANE_AGENT AW_META_LANE_PROC AW_SINDRI_CHILD AW_SINDRI_LAUNCH AW_LAUNCHER_CONFIG TMUX TMUX_PANE; do
    unset_flags+=(-u "$v")
  done
  # TMPDIR is inside the sandbox too: every `mktemp` a suite (or a hook or tool it runs) makes lands under it and goes with it, so a
  # suite that forgets its trap, or is killed, leaves nothing in /tmp (100k+ stray tmp.* dirs and ~3 GB private NuGet caches did).
  env "${unset_flags[@]}" HOME="$sandbox/home" AW_STATE_DIR="$sandbox/state" TMPDIR="$sandbox/tmp" bash "$suite" >"$out/$idx.log" 2>&1 </dev/null
  rc=$?
  # A suite that restores NuGet packages into its sandbox HOME (NUGET_PACKAGES unset or overridden) fills the disk with a private ~2.7 GB cache.
  if [ -d "$sandbox/home/.nuget/packages" ]; then
    echo "bash-tests: $suite populated a private NuGet cache in its sandbox HOME; keep NUGET_PACKAGES at the shared cache" >>"$out/$idx.log"
    [ "$rc" -ne 0 ] || rc=1
  fi
  find "$sandbox" -delete 2>/dev/null
  secs=$((SECONDS - start))
  echo "$rc" >"$out/$idx.rc"; echo "$secs" >"$out/$idx.secs"
  if [ "$rc" -eq 0 ]; then
    printf 'PASS  %s  %s  %ss\n' "$mode" "$suite" "$secs"
  else
    printf 'FAIL  %s  %s  %ss  exit %s\n' "$mode" "$suite" "$secs" "$rc"
  fi
  exit 0
fi

ROOT="${BASH_TESTS_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$ROOT" || exit 2

# path<TAB>reason. Keep this short: every entry is a suite CI does not run.
EXCLUDED=(
  "config/lib/tests/judge-cs-hook-surface.test.sh	needs the .NET SDK to build the C# judge; CI runs it in the dotnet job (step: Hook contract), which changed-areas.sh also selects for a hook, statusline or hook-library change; the local gate runs it directly"
  "config/lib/tests/sindri-cs-hook-surface.test.sh	needs the .NET SDK to build the C# sindri; CI runs it in the dotnet job (step: Hook contract, sindri), which changed-areas.sh also selects for a hook change; the local gate runs it directly"
  "config/tests/statusline-judge.test.sh	stale on main: it extracts only judge_segment() from statusline.sh, which now calls judge_health(), so all 3 cases fail; config/lib/tests/statusline.test.sh covers the judge column"
)

# path<TAB>reason. Suites that measure wall time or race signals, or that watch state outside
# their own sandbox: they run after the pool, one at a time, so CPU contention or another
# suite's writes can't turn a pass into a failure.
# Everything else must be safe to run beside any other suite (its own mktemp dirs, its
# own HOME or AW_STATE_DIR, stubs on PATH, no fixed ports).
SERIAL=(
  "scripts/tests/agent-token.test.sh	starts a fake HTTP server under a 30 s start budget and sends signals mid-request, asserting the script exits within 2 s and its curl is gone"
  "config/hooks/tests/failopen.test.sh	SIGTERMs hooks 0.5 s into a 4 s fake-judge wait and runs each hook under a 10 s watchdog; a loaded machine shifts both"
  "config/lib/tests/failopen.test.sh	runs hooks against hung and slow fake judges under a short watchdog (rc=hung) and checks the fail-open path within the hook's own timeout"
  "config/lib/tests/statusline.test.sh	asserts the statusline returns within 1 s while a 3 s scorer runs, then polls a 10 s deadline for the cache"
  "config/hooks/tests/fleet-state.test.sh	asserts every aw:fleet hook event runs under 100 ms (best of 5), which CPU contention from a parallel suite can turn into a false failure"
  "config/hooks/tests/sindri-nudge.test.sh	asserts the nudge gives up within a 300 ms budget (AW_SINDRI_NUDGE_BUDGET_MS) against a 5 s sleeping sindri"
  "config/lib/tests/prompt-sort.test.sh	asserts the hook gives up within a 600 ms budget (AW_PROMPT_SORT_BUDGET_MS) against a 5 s sleeping sorter"
  "config/lib/tests/aw-state-dir-isolation.test.sh	snapshots the real HOME's install-target locations around a sandboxed installer run and fails on any change, so another suite writing there at the same time is a false failure"
  "config/lib/tests/installed-hooks-smoke.test.sh	runs scripts/install-judge-hooks.sh for real and fires the installed hooks against a fake judge under their timeouts; it built judge/dist before the cutover and stays serial until the switch PR rechecks it"
  "config/lib/tests/install-wake-gating.test.sh	runs scripts/install-judge-hooks.sh and the wake-gating hooks against a fake judge under their timeouts; it built judge/ before the cutover and stays serial until the switch PR rechecks it"
  "config/lib/tests/judge-brief-real-cli.test.sh	spawns the real judge CLI (node judge/dist/cli.js, sqlite open) for each step; it flaked in the #224 and #273 gates beside heavy jobs and passes alone, so it runs on a quiet machine, after the pool, and never beside the serial suites that rebuild judge/dist"
  "config/lib/tests/send-gate-real-judge.test.sh	spawns the real judge CLI (node judge/dist/cli.js wake-gate) per sender kind; it flaked in the #224 and #273 gates beside heavy jobs and passes alone, so it runs on a quiet machine"
  "scripts/tests/forge-fixture.test.sh	dotnet-builds src/Forge in the shared checkout (obj dirs), which races any other dotnet build there; builds once, then runs git and jq only"
  "scripts/tests/readiness-baseline.test.sh	asserts a 200 ms gh timeout (READINESS_GH_TIMEOUT_MS) fires against a 3 s stub and is reported as the timeout, not a parse error"
)

if [ "${BASH_TESTS_EXCLUDED+set}" = set ]; then
  EXCLUDED=()
  while IFS= read -r line; do [ -n "$line" ] && EXCLUDED+=("$line"); done <<<"$BASH_TESTS_EXCLUDED"
fi
if [ "${BASH_TESTS_SERIAL+set}" = set ]; then
  SERIAL=()
  while IFS= read -r line; do [ -n "$line" ] && SERIAL+=("$line"); done <<<"$BASH_TESTS_SERIAL"
fi

paths_of() { local entry; for entry in "$@"; do printf '%s\n' "${entry%%	*}"; done; }

suites() {
  # `|| true`: grep exits 1 when every tracked suite is excluded.
  git ls-files '*.test.sh' | { grep -vxF -f <(paths_of ${EXCLUDED[@]+"${EXCLUDED[@]}"}; echo '//none//') || true; }
}

case "${1:-}" in
  --list) suites; exit 0 ;;
  --excluded) [ "${#EXCLUDED[@]}" -eq 0 ] || printf '%s\n' "${EXCLUDED[@]}"; exit 0 ;;
  --serial) [ "${#SERIAL[@]}" -eq 0 ] || printf '%s\n' "${SERIAL[@]}"; exit 0 ;;
  "") ;;
  *) echo "usage: $0 [--list|--excluded|--serial]" >&2; exit 2 ;;
esac

jobs="${BASH_TESTS_JOBS:-$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 2)}"
case "$jobs" in
  ''|*[!0-9]*|0) echo "bash-tests: BASH_TESTS_JOBS must be a positive integer, got '$jobs'" >&2; exit 2 ;;
esac

all="$(suites)"
serial_paths="$(paths_of ${SERIAL[@]+"${SERIAL[@]}"})"
# A serial entry must name a suite the runner runs, or a rename would silently move a
# timing-sensitive suite into the pool.
while IFS= read -r p; do
  [ -n "$p" ] || continue
  grep -qxF -- "$p" <<<"$all" \
    || { echo "bash-tests: serial suite '$p' is not a tracked, non-excluded *.test.sh" >&2; exit 2; }
done <<<"$serial_paths"
pool="$(printf '%s\n' "$all" | { grep -vxF -f <(printf '%s\n' "$serial_paths" '//none//') || true; } | sed '/^$/d')"

OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT
began=$SECONDS

# Phase 1: the pool. Index i<n> keeps each suite's files apart; xargs -P bounds the jobs.
i=0
while IFS= read -r s; do
  [ -n "$s" ] || continue
  i=$((i + 1)); printf 'par\t%s\tp%03d\n' "$s" "$i"
done <<<"$pool" >"$OUT/plan"
j=0
while IFS= read -r s; do
  [ -n "$s" ] || continue
  j=$((j + 1)); printf 'ser\t%s\ts%03d\n' "$s" "$j"
done <<<"$serial_paths" >>"$OUT/plan"

# NUL-delimited, so a path with a blank or quote stays one argument.
grep '^par' "$OUT/plan" | cut -f2,3 | tr '\t\n' '\0\0' \
  | xargs -0 -n 2 -P "$jobs" sh -c 'exec bash "$0" --run-one par "$2" "$1" "$3"' "$SELF" "$OUT" \
  || true
# Phase 2: the serial suites, one at a time, after the pool has drained.
while IFS=$'\t' read -r mode s idx; do
  [ "$mode" = ser ] || continue
  bash "$SELF" --run-one ser "$s" "$OUT" "$idx"
done <"$OUT/plan"
wall=$((SECONDS - began))

# Collect. A worker that never wrote its .rc (killed) counts as failed.
failed=()
count=0
: >"$OUT/times"
while IFS=$'\t' read -r mode s idx; do
  count=$((count + 1))
  rc="$(cat "$OUT/$idx.rc" 2>/dev/null || echo missing)"
  secs="$(cat "$OUT/$idx.secs" 2>/dev/null || echo 0)"
  printf '%s\t%s\t%s\n' "$secs" "$mode" "$s" >>"$OUT/times"
  if [ "$rc" != 0 ]; then
    failed+=("$s")
    echo
    echo "FAIL  $mode  $s  (exit $rc, ${secs}s)"
    # The failing assertions first (a long suite's tail can hide them), then the end of the log.
    grep -E 'FAIL|not ok' "$OUT/$idx.log" 2>/dev/null | sed 's/^/      ! /' | head -20
    sed 's/^/      | /' "$OUT/$idx.log" 2>/dev/null | tail -25
  fi
done <"$OUT/plan"

echo
echo "slowest suites:"
sort -t$'\t' -k1,1nr "$OUT/times" | head -8 | while IFS=$'\t' read -r secs mode s; do
  printf '  %5ss  %s  %s\n' "$secs" "$mode" "$s"
done
echo
echo "bash suites: $count run, ${#failed[@]} failed, ${#EXCLUDED[@]} excluded ($(printf '%s\n' "$serial_paths" | sed '/^$/d' | wc -l | tr -d ' ') serial, $jobs jobs, ${wall}s wall)"
for entry in ${EXCLUDED[@]+"${EXCLUDED[@]}"}; do printf '  excluded: %s\n' "${entry%%	*}"; done
if [ "${#failed[@]}" -gt 0 ]; then
  printf '  failed: %s\n' "${failed[@]}"
  exit 1
fi
