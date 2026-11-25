#!/usr/bin/env bash
# #2636: install-headroom.test.sh must pass when another process already holds a port it could have picked.
# Its old pick was 20000 + pid % 9000 with nothing checking that the port was free. This check runs the suite
# under a known pid (a `bash -c` that execs the suite keeps its pid), holds exactly that port first, and expects
# every case of the suite to pass anyway.
# Run: bash scripts/tests/install-headroom-port-taken.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SUITE="$DIR/install-headroom.test.sh"
WORK="$(mktemp -d)"
cleanup() {
  [ -s "$WORK/holder.pid" ] && kill "$(cat "$WORK/holder.pid")" 2>/dev/null
  rm -rf "$WORK"
}
trap cleanup EXIT

bash -c '
P=$((20000 + $$ % 9000))
# Hold the port (a listener), or note that someone else already holds it: either way it is taken.
python3 -c "
import socket, sys, time
s = socket.socket()
try:
    s.bind((\"127.0.0.1\", int(sys.argv[1]))); s.listen(1); state = \"held\"
except OSError:
    state = \"busy\"
open(sys.argv[2], \"w\").write(state)
time.sleep(600)" "$P" "$1/ready" >/dev/null 2>&1 &
echo $! > "$1/holder.pid"
for _ in $(seq 1 100); do [ -s "$1/ready" ] && break; sleep 0.1; done
echo "# port $P taken ($(cat "$1/ready" 2>/dev/null || echo unknown)) before the suite starts"
exec bash "$2"' _ "$WORK" "$SUITE" > "$WORK/out" 2>&1
RC=$?

fail=0
grep -m1 '^# port' "$WORK/out"
if [ "$RC" = 0 ]; then echo "ok - the suite exits 0 with its old pid-derived port taken"; else
  echo "not ok - the suite exits 0 with its old pid-derived port taken (exit $RC)"; fail=1; fi
if grep -q '^not ok' "$WORK/out"; then
  echo "not ok - no case of the suite fails"; grep '^not ok' "$WORK/out" | head -5 | sed 's/^/  /'; fail=1
else echo "ok - no case of the suite fails"; fi
exit "$fail"
