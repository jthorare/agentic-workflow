#!/usr/bin/env bash
# A job waiting in scripts/heavy.sh for a box-wide slot must not take a slot above heavy-slots-cap when the cap is lowered
# while the memory guard runs (#2718). Run: bash scripts/tests/heavy-stale-cap.test.sh
# acquire reads the cap, then runs the memory guard (awk on /proc/meminfo, or vm_stat | awk on macOS), then mkdirs a slot.
# A slow guard (macOS vm_stat) widens that window. An awk shim on PATH makes the window slow on every OS: it sleeps only
# when the memory guard's awk programs run, then execs the real awk. Scratch state and lock dirs only.
set -uo pipefail
unset TMUX
for v in $(compgen -e | grep -E '^AW_(HEAVY|SESSION_HEAVY|META_LANE)'); do unset "$v"; done   # this suite may itself run under a heavy job
unset DOTNET_PROCESSOR_COUNT MSBUILDDISABLENODEREUSE
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$DIR/../lib/test-portable.sh"   # timeout for macOS (no GNU timeout)
HEAVY="$DIR/../heavy.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
sleep 300 & live=$!
cleanup() { kill "$live" 2>/dev/null; rm -rf -- "$WORK"; }
trap cleanup EXIT

# The shim: resolve the real awk before PATH changes and bake it in.
REAL_AWK="$(command -v awk)"
mkdir -p "$WORK/shim"
cat > "$WORK/shim/awk" <<SHIM
#!/usr/bin/env bash
for a in "\$@"; do
  case "\$a" in *MemAvailable*|*"page size of"*) sleep "\${SLOW_AWK:-0.7}"; break ;; esac
done
exec "$REAL_AWK" "\$@"
SHIM
chmod +x "$WORK/shim/awk"

scratch() { T="$WORK/$1"; BASE="$T/locks/heavy-job.lock"; mkdir -p "$T/locks"; echo "$2" > "$T/heavy-slots-cap"; }
hold() { mkdir -p "$1"; echo "$live" > "$1/pid"; }

# run_wrapper: the wrapper with the slow shim first on PATH; prints its exit code (124 = still waiting at 5 s).
run_wrapper() {
  local rc=0
  timeout 5 env AW_STATE_DIR="$T" AW_HEAVY_JOB_LOCK="$BASE" AW_HEAVY_SESSION_PID="$live" AW_HEAVY_RUN_MAX=0 AW_HEAVY_MIN_FREE_MB=0 \
    PATH="$WORK/shim:$PATH" bash "$HEAVY" --expensive true >/dev/null 2>&1 || rc=$?
  echo "$rc"
}

# freer <delay> <lower-cap: 1|0>: after the delay, optionally lower the cap to 1, then free slot 2.
freer() { ( sleep "$1"; [ "$2" = 1 ] && echo 1 > "$T/heavy-slots-cap"; rm -r "$BASE.2" ) & freer_pid=$!; }

scratch lowered 2; hold "$BASE"; hold "$BASE.2"; freer 1 1
rc="$(run_wrapper)"; wait "$freer_pid"
check "wrapper, slow memory guard: a cap lowered to 1 while waiting keeps a job off slot 2 (exit 124 = still waiting)" "$rc" "124"

scratch control 2; hold "$BASE"; hold "$BASE.2"; freer 1 0
rc="$(run_wrapper)"; wait "$freer_pid"
check "wrapper, slow memory guard: cap stays 2, slot 2 freed, the job takes slot 2 (exit 0)" "$rc" "0"

scratch lowered-early 2; hold "$BASE"; hold "$BASE.2"; freer 0.3 1
rc="$(run_wrapper)"; wait "$freer_pid"
check "wrapper, slow memory guard: a cap lowered to 1 at 0.3 s keeps a job off slot 2 (exit 124 = still waiting)" "$rc" "124"

exit "$fail"
