#!/usr/bin/env bash
# Tests for scripts/run-test-guard.sh. Run: bash scripts/tests/run-test-guard.test.sh
# Linux only (the guard reads /proc/<pid>/environ). Signals only the PIDs it finds by its own token, never a pattern.
set -uo pipefail
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GUARD="$DIR/../run-test-guard.sh"
fail=0

if [ "$(uname -s)" != "Linux" ]; then echo "skip - run-test-guard reads /proc (Linux only)"; exit 0; fi

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
token="rtgtest-$$-$(date +%s%N)"

with_token() {  # every PID of this user whose command line carries the token (the daemonized sleep is renamed to it)
  local p
  for p in /proc/[0-9]*; do
    p=${p#/proc/}
    [ -O "/proc/$p" ] || continue
    { tr '\0' ' ' < "/proc/$p/cmdline"; } 2>/dev/null | grep -q -- "$token" && echo "$p"
  done
}
cleanup() {
  local left; left="$(with_token)"
  [ -z "$left" ] || kill -KILL $left 2>/dev/null
  rm -rf -- "$WORK"
}
trap cleanup EXIT

# A double fork into its own session, named after the token (the way a fixture's serve outlives its check).
daemonize="( setsid bash -c 'exec -a \"\$0\" sleep 300' $token </dev/null >/dev/null 2>&1 & ); sleep 0.5"

# --- (a) the command's exit code passes through, and a daemonized child does not outlive it ---
bash "$GUARD" --timeout 30 --grace 3 -- bash -c "$daemonize; exit 7"
check "a command's exit code passes through" "$?" "7"
check "nothing tagged is left after a normal exit" "$(with_token | wc -l | tr -d ' ')" "0"

bash "$GUARD" -- true
check "exit 0 passes through" "$?" "0"

# --- (b) a timeout exits 124, the check's EXIT trap runs and its daemonized child is gone ---
start=$SECONDS
bash "$GUARD" --timeout 2 --grace 3 -- bash -c "trap 'touch \"$WORK/trap-ran\"' EXIT; trap 'exit 143' TERM; $daemonize; sleep 120" 2>"$WORK/b.err"
rc=$?
took=$((SECONDS - start))
check "a timeout exits 124" "$rc" "124"
check "the timeout returns promptly" "$([ "$took" -lt 20 ] && echo yes || echo "no (${took}s)")" "yes"
check "the check's EXIT trap ran" "$([ -e "$WORK/trap-ran" ] && echo yes || echo no)" "yes"
check "nothing tagged is left after a timeout" "$(with_token | wc -l | tr -d ' ')" "0"
check "the timeout is said on stderr" "$(grep -c 'timed out after 2s' "$WORK/b.err")" "1"

# --- (b2) a timeout exits 124 even when the watchdog is slow to exit (#3219) ---
# Under gate load the check died of the watchdog's TERM and the guard read "watchdog still alive" as "no timeout",
# so it passed the check's own 143 through. The seam holds the watchdog alive after its TERM, as a loaded box does.
AW_GUARD_TEST_WATCHDOG_HOLD=3 bash "$GUARD" --timeout 2 --grace 3 -- bash -c "trap 'exit 143' TERM; sleep 120" 2>/dev/null
check "a timeout exits 124 when the watchdog is slow to exit" "$?" "124"

# --- (c) usage errors exit 2 ---
bash "$GUARD" >/dev/null 2>&1;                          check "no arguments exit 2" "$?" "2"
bash "$GUARD" --timeout 5 >/dev/null 2>&1;              check "no command exits 2" "$?" "2"
bash "$GUARD" --bogus -- true >/dev/null 2>&1;          check "an unknown option exits 2" "$?" "2"
bash "$GUARD" --timeout abc -- true >/dev/null 2>&1;    check "a non-numeric timeout exits 2" "$?" "2"
bash "$GUARD" --timeout >/dev/null 2>&1;                check "a missing value exits 2" "$?" "2"

# --- (d) the check runs in its own systemd scope (#2982) ---
# An OOM kill inside the pane's cgroup scope makes systemd stop the whole pane (the agent session). With a working
# systemd-run the check goes through `systemd-run --user --scope -p OOMPolicy=continue`; without one, setsid stays.
# The stub systemd-run logs each call; GUARD_STUB_SCOPE=ok runs the command in place like --scope, anything else fails
# like a box with no user bus, so the suite never makes a real scope.
STUB="$WORK/stub"; mkdir -p "$STUB"
cat > "$STUB/systemd-run" <<'EOS'
#!/usr/bin/env bash
echo "systemd-run $*" >> "$WORK/systemd-run.log"
if [ "${GUARD_STUB_SCOPE:-}" = ok ]; then
  while [ "$#" -gt 0 ] && [ "$1" != "--" ]; do shift; done
  shift
  exec "$@"
fi
echo "Failed to connect to user scope bus via local transport" >&2
exit 1
EOS
chmod +x "$STUB/systemd-run"
export WORK
SLOG="$WORK/systemd-run.log"
echo "0::/user.slice/user-1000.slice/user@1000.service/app.slice/app.service" > "$WORK/cgroup.plain"
# The runner of this suite may itself sit in a pane scope, so every row names its cgroup line (default: not a scope).
scoped() { PATH="$STUB:$PATH" GUARD_STUB_SCOPE=ok AW_GUARD_TEST_CGROUP="${CG:-$WORK/cgroup.plain}" bash "$GUARD" "$@"; }

: > "$SLOG"
scoped --timeout 30 --grace 3 -- bash -c "$daemonize; exit 7"
check "scope: the exit code passes through" "$?" "7"
check "scope: the check went through systemd-run --user --scope" "$(grep -c -- '--user --scope .*-p OOMPolicy=continue .*-- bash -c' "$SLOG")" "1"
check "scope: nothing tagged is left after a normal exit" "$(with_token | wc -l | tr -d ' ')" "0"
check "scope: no bind on a non-scope cgroup" "$(grep -c 'BindsTo' "$SLOG")" "0"

: > "$SLOG"
scoped --timeout 2 --grace 3 -- bash -c "trap 'exit 143' TERM; $daemonize; sleep 120" 2>/dev/null
check "scope: a timeout exits 124" "$?" "124"
check "scope: nothing tagged is left after a timeout" "$(with_token | wc -l | tr -d ' ')" "0"

for row in "bind: a user-manager scope|0::/user.slice/user-1000.slice/user@1000.service/app.slice/tmux-spawn-abc.scope|1" \
           "bind: a login session scope|0::/user.slice/user-1000.slice/session-3.scope|0" \
           "bind: a user-manager service|0::/user.slice/user-1000.slice/user@1000.service/app.slice/app.service|0"; do
  IFS='|' read -r name line want <<< "$row"
  : > "$SLOG"; echo "$line" > "$WORK/cgroup.row"
  CG="$WORK/cgroup.row" scoped -- bash -c 'exit 0'
  job="$(grep -- "-- bash -c exit 0$" "$SLOG")"
  check "$name: BindsTo on the check line" "$(printf '%s\n' "$job" | grep -c -- '-p BindsTo=tmux-spawn-abc.scope')" "$want"
  check "$name: After on the check line" "$(printf '%s\n' "$job" | grep -c -- '-p After=tmux-spawn-abc.scope')" "$want"
done

: > "$SLOG"
PATH="$STUB:$PATH" AW_GUARD_TEST_CGROUP="$WORK/cgroup.plain" bash "$GUARD" -- bash -c 'exit 5'
check "fallback: a failing systemd-run keeps setsid and the exit code" "$?" "5"
check "fallback: only the probe called systemd-run" "$(grep -c -- '-- bash -c' "$SLOG")" "0"

exit "$fail"
