#!/usr/bin/env bash
# Tests for scripts/lib/sindri-open-runs.sh (#1000 ask 4) against a stub `sindri` that prints a fixture and exits as a fixture
# file says: plane down (SND-API-001) is silent, unreadable output warns and goes on, no open runs is silent, open runs with no
# terminal stop (naming AW_SINDRI_STOP_OK), AW_SINDRI_STOP_OK=1 goes on, and rows with launch null or {} don't count. The
# terminal cases (go, wait, anything else) run under `script` when it is available, else print a SKIP line.
# Run: bash scripts/tests/sindri-open-runs.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { # $1 = name, $2 = text, $3 = needle
  case "$2" in *"$3"*) echo "ok - $1" ;; *) echo "not ok - $1"; echo "  missing: $3"; echo "  in:      $2"; fail=1 ;; esac
}
WORK="$(cd "$(mktemp -d "${TMPDIR:-/tmp}/sindri-open-runs.XXXXXX")" && pwd -P)"
trap 'rm -rf "$WORK"' EXIT
unset TMUX AW_SINDRI_STOP_OK AW_SINDRI_OPEN_RUNS_POLL
for v in $(compgen -v | grep '^AW_META_LANE' || true); do unset "$v"; done

# The stub: prints $WORK/out (or, when $WORK/seq exists, out.<n> for its n-th call, else out.last) and exits with $WORK/rc.
STUB="$WORK/sindri"
cat > "$STUB" <<'STUBEOF'
#!/usr/bin/env bash
w="$(dirname "$0")"
if [ -f "$w/seq" ]; then
  n=$(( $(cat "$w/seq") + 1 )); echo "$n" > "$w/seq"
  f="$w/out.$n"; [ -f "$f" ] || f="$w/out.last"
else f="$w/out"; fi
cat "$f"
exit "$(cat "$w/rc" 2>/dev/null || echo 0)"
STUBEOF
chmod +x "$STUB"

RUNS='{"sessions":[{"ref":"claude/abc","state":"running","launch":{"instance":"ITM-1/line/3/scoping.1","state":"challenge","attempt":1}}],"recentlyEnded":[],"problems":[],"counts":{},"empty":null,"asOf":"2026-10-25T00:00:00Z"}'
NONE='{"sessions":[{"ref":"claude/n1","state":"running","launch":null},{"ref":"claude/n2","state":"idle","launch":{}}],"recentlyEnded":[],"problems":[],"counts":{},"empty":null,"asOf":"2026-10-25T00:00:00Z"}'

# Runs the guard in a subshell like the scripts that source it (set -euo pipefail), with no terminal; sets rc, err and stdout.
run_guard() {
  rc=0
  bash -c 'set -euo pipefail; source "$1"; r=0; sindri_open_runs_guard "$2" </dev/null || r=$?; exit $r' _ \
    "$DIR/lib/sindri-open-runs.sh" "$STUB" >"$WORK/cap.out" 2>"$WORK/cap.err" || rc=$?
  err="$(cat "$WORK/cap.err")"
  stdout="$(cat "$WORK/cap.out")"
}

echo "SND-API-001: control plane is not running" > "$WORK/out"; echo 1 > "$WORK/rc"
run_guard
check "plane down: rc 0" "$rc" 0
check "plane down: silent" "$err$stdout" ""

echo "not json at all" > "$WORK/out"; echo 0 > "$WORK/rc"
run_guard
check "unreadable output: rc 0" "$rc" 0
has "unreadable output: warns" "$err" "couldn't read the open agent runs"

echo "boom" > "$WORK/out"; echo 2 > "$WORK/rc"
run_guard
check "other failure: rc 0" "$rc" 0
has "other failure: warns" "$err" "couldn't read the open agent runs"

echo 0 > "$WORK/rc"
printf '%s\n' "$NONE" > "$WORK/out"
run_guard
check "launch null and {} don't count: rc 0" "$rc" 0
check "launch null and {} don't count: silent" "$err$stdout" ""

printf '%s\n' "$RUNS" > "$WORK/out"
run_guard
check "open runs, no terminal: rc 1" "$rc" 1
has "open runs: head line" "$err" "1 agent run(s) are open in Sindri; publishing now can stop the control plane mid-Step (#1000):"
has "open runs: lists the ref and instance" "$err" "  claude/abc  ITM-1/line/3/scoping.1 · challenge  running"
has "open runs, no terminal: names AW_SINDRI_STOP_OK" "$err" "AW_SINDRI_STOP_OK=1"
has "open runs, no terminal: says stopping" "$err" "not a terminal: stopping."

AW_SINDRI_STOP_OK=1 run_guard
check "AW_SINDRI_STOP_OK=1: rc 0" "$rc" 0
has "AW_SINDRI_STOP_OK=1: says going on" "$err" "AW_SINDRI_STOP_OK=1: going on"

# The terminal cases: stdin and stderr are a pty under `script`; the answer is typed after a second.
tty_guard() { # $1 = the answer typed; prints the guard's output, ending in RC=<rc>
  script -qec "bash -c 'source \"$DIR/lib/sindri-open-runs.sh\"; r=0; sindri_open_runs_guard \"$STUB\" || r=\$?; echo RC=\$r'" /dev/null \
    < <(sleep 1; printf '%s\n' "$1"; sleep 2) 2>&1 | tr -d '\r'
}
if command -v script >/dev/null 2>&1 && script -qec true /dev/null </dev/null >/dev/null 2>&1; then
  printf '%s\n' "$RUNS" > "$WORK/out"
  o="$(tty_guard go)"
  has "tty: go goes on" "$o" "RC=0"
  o="$(tty_guard no)"
  has "tty: anything else stops" "$o" "stopped; nothing was changed"
  has "tty: anything else returns 1" "$o" "RC=1"
  # wait: the first read (the guard's own) reports runs, the second (the poll) none.
  printf '%s\n' "$RUNS" > "$WORK/out.1"; printf '%s\n' "$NONE" > "$WORK/out.last"; echo 0 > "$WORK/seq"
  o="$(AW_SINDRI_OPEN_RUNS_POLL=0 tty_guard wait)"
  has "tty: wait returns once none are open" "$o" "RC=0"
else
  echo "SKIP - tty cases (go, wait, stop): no usable script(1)"
fi

exit "$fail"
