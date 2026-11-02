#!/usr/bin/env bash
# tests/bugfix-checks/gh-1130-run-test-leftovers.sh — frozen regression check for #1130: when bugfix-state's run-test stops a
# check at its time limit, the check must get the chance to clean up (its EXIT trap runs, the way the UI checks stop their
# fixture serve) and nothing it started may stay behind, a detached child (setsid, its own session, the way a fixture
# `sindri serve` or `temporal server start-dev` runs) included.
#   bash tests/bugfix-checks/gh-1130-run-test-leftovers.sh     exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: BUGFIX_STATE_BIN (a built bugfix-state; default: this checkout's Aw.BugFix.Host, built into a temp dir).
# Kills only the PIDs it finds by its own token (never a pattern kill).
set -uo pipefail
unset TMUX
export AW_LOG_LEVEL=none

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
work="$(mktemp -d "${TMPDIR:-/tmp}/gh1130-XXXXXX")" || exit 2
token="gh1130-$$-$(date +%s%N)"

leftovers() { # every PID of this user whose command line carries the token
  local p
  for p in /proc/[0-9]*; do
    p=${p#/proc/}
    [ -O "/proc/$p" ] || continue
    { tr '\0' ' ' < "/proc/$p/cmdline"; } 2>/dev/null | grep -q -- "$token" && echo "$p"
  done
}
cleanup() {
  local left; left="$(leftovers)"
  [ -z "$left" ] || kill -KILL $left 2>/dev/null
  rm -rf -- "$work"
}
trap cleanup EXIT
setup_fail() { echo "gh-1130-run-test-leftovers: setup failed: $*" >&2; exit 2; }
fail() { echo "FAIL: $*" >&2; exit 1; }

# The built one runs as `dotnet <dll>`: run-test hands a check no DOTNET_ROOT, so the apphost could not find the runtime.
if [ -n "${BUGFIX_STATE_BIN:-}" ]; then
  [ -x "$BUGFIX_STATE_BIN" ] || setup_fail "no bugfix-state at $BUGFIX_STATE_BIN"
  bf=("$BUGFIX_STATE_BIN")
else
  dotnet build "$ROOT/src/BugFix/Aw.BugFix.Host" -nologo -v q -o "$work/bin" >"$work/build.log" 2>&1 \
    || { tail -20 "$work/build.log" >&2; setup_fail "the bugfix-state build failed"; }
  [ -f "$work/bin/bugfix-state.dll" ] || setup_fail "no bugfix-state.dll in $work/bin"
  bf=(dotnet "$work/bin/bugfix-state.dll")
fi

# A repo whose check starts a plain child, a daemonized child (double fork: re-parented, out of the process tree) and an EXIT trap, then outlives the limit.
repo="$work/repo"
mkdir -p "$repo/tests"
cat >"$repo/tests/slow.test.sh" <<EOF
trap 'touch "$work/trap-ran"' EXIT
bash -c 'exec -a "\$0" sleep 300' plain-$token &
( setsid bash -c 'exec -a "\$0" sleep 300' detached-$token </dev/null >/dev/null 2>&1 & )
sleep 120
EOF
git_() { git -C "$repo" -c user.name=check -c user.email=check@example.invalid "$@"; }
git_ init -q -b main && git_ add -A && git_ commit -q -m base || setup_fail "git repo"
printf '{"source":"text","id":null,"title":"Leftovers","brief":"run-test leaves the check running","expected":"nothing left","actual":"left"}' >"$work/ticket.json"
printf 'status: diagnosed\n\n## Hypotheses\n\n| # | Hypothesis | Cause-site files | Likelihood | Result |\n|---|---|---|---|---|\n| 1 | Kill misses detached children | `tests/slow.test.sh` | high | confirmed |\n\n## Root Cause\n\nThe kill misses them.\n' >"$work/handoff.md"
state="$work/state"
(cd "$repo" && "${bf[@]}" init --state "$state" --ticket "$work/ticket.json") >"$work/init.log" 2>&1 || setup_fail "init: $(cat "$work/init.log")"
(cd "$repo" && "${bf[@]}" advance investigate --state "$state" --evidence "$work/handoff.md") >"$work/inv.log" 2>&1 || setup_fail "investigate: $(cat "$work/inv.log")"

started=$(date +%s)
(cd "$repo" && AW_BUGFIX_CHECK_TIMEOUT_SECONDS=3 "${bf[@]}" run-test --state "$state" --check tests/slow.test.sh -- bash tests/slow.test.sh) >"$work/run.log" 2>&1
took=$(( $(date +%s) - started ))
[ "$took" -lt 110 ] || fail "run-test did not stop the check at its 3 s limit (took ${took}s)"
grep -q '"evidence"' "$work/run.log" || setup_fail "run-test wrote no run record: $(tail -5 "$work/run.log")"

# Give a stop that is still finishing a few seconds, then nothing of the check may be left.
for _ in 1 2 3 4 5; do [ -z "$(leftovers)" ] && break; sleep 1; done
left="$(leftovers)"
if [ -n "$left" ]; then
  names="$(for p in $left; do tr '\0' ' ' < "/proc/$p/cmdline" 2>/dev/null; echo; done | sed "s/$token/<token>/g" | tr '\n' ';')"
  fail "the check's processes outlived run-test's time limit: $names"
fi
[ -e "$work/trap-ran" ] || fail "the check was killed without the chance to run its EXIT trap (its fixture cleanup)"
echo "gh-1130-run-test-leftovers: pass (stopped after ${took}s, trap ran, nothing left)"
