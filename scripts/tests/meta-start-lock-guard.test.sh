#!/usr/bin/env bash
# A start-lock taker never clears another live taker's takeover guard (#2598).
# Run: bash scripts/tests/meta-start-lock-guard.test.sh
#
# take_over_dead (scripts/meta-start-lock.sh) clears a takeover guard ($start_lock.takeover) it saw held by a dead or unnamed
# holder. Only observable behaviour is asserted: every holder is a real taker running the lib under test, so the guard's
# files are in whatever format the lib writes.
# Case 1: taker E (a live pid) has mkdir'd the guard but not written its holder file yet. Taker D sees an empty guard on
#   several calls in a row and must not read that as "dead holder": E's guard still exists afterwards.
# Case 2: taker Z dies inside the guard (killed in its "gap" hook), so the guard holds a dead holder's file. D (guard_seen
#   starts empty) sees it and goes to clear it. A PATH stub for rm and rmdir fires once, on D's first call that names the
#   guard: it plays C (clears the guard with the real tools) and E (a real taker that takes the fresh guard and waits inside
#   its "gap" hook), then runs the real tool. After D returns, E is still inside its guard, so the guard must still exist.
# Case 3 (#2613): the guard holds a dead waiter's pid file that sorts first and a live holder V's pid file after it. A third
#   waiter must leave the lock alone while V is in the guard: the lock is still there and V's pid file is still in the guard.
# Case 4 (#2613): an empty guard over a minute old and a dead owner: within two calls the guard is cleared and the lock is gone.
# Scratch dirs only; bash 3.2-compatible. Kills only the pids it started.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MSL="$DIR/../meta-start-lock.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

real_rm="$(command -v rm)"; real_rmdir="$(command -v rmdir)"
WORK="$(mktemp -d)"
SIG="$WORK/sig"
mkdir -p "$WORK/stub" "$SIG"
sleep 600 & E1_PID=$!
cleanup() {
  local p
  kill "$E1_PID" 2> /dev/null
  p="$(cat "$SIG/E-pid" 2> /dev/null)"; [ -z "$p" ] || kill "$p" 2> /dev/null
  "$real_rm" -r -f -- "$WORK"
}
trap cleanup EXIT

dead_pid() { bash -c 'exit 0' & local p=$!; wait "$p"; echo "$p"; }

# --- case 1: an empty guard of a live taker survives repeated sightings ---
L1="$WORK/a.lock"; G1="$L1.takeover"
mkdir "$G1"
( start_lock="$L1"; lane_pid=$$; lane=d; guard_seen=""
  . "$MSL"
  take_over_dead x; take_over_dead x; take_over_dead x )
check "an empty guard of a live taker (E, between mkdir and its write) is left in place" "$([ -d "$G1" ] && echo exists || echo cleared)" "exists"

# --- case 2: a late cleanup of a dead holder never destroys a live taker's fresh guard ---
L2="$WORK/b.lock"; G2="$L2.takeover"
# Z: a real taker that dies inside the guard
ln -s "$(dead_pid)" "$L2"
{ bash -c 'start_lock="$1"; lane_pid=$$; lane=z; . "$2"; start_lock_hook() { [ "$1" = gap ] && kill -9 $$; }; take_over_dead x' _ "$L2" "$MSL"; } 2> /dev/null
check "Z died inside the guard, leaving it behind" "$([ -d "$G2" ] && echo exists || echo gone)" "exists"
# Z unlinked the lock; point it at a dead pid again so later takers see a dead lock
unlink "$L2" 2> /dev/null; ln -s "$(dead_pid)" "$L2"

ORIG_PATH="$PATH"
for tool in rm rmdir; do
  real="$(command -v "$tool")"
  cat > "$WORK/stub/$tool" <<STUB
#!/usr/bin/env bash
hit=""; for a in "\$@"; do case "\$a" in "$G2"|"$G2"/*) hit=1 ;; esac; done
if [ -n "\$hit" ] && [ ! -e "$SIG/fired" ]; then
  : > "$SIG/fired"
  # C clears the guard with the real tools
  for f in "$G2"/* "$G2"/.[!.]*; do [ -e "\$f" ] && "$real_rm" -f -- "\$f"; done
  "$real_rmdir" "$G2" 2> /dev/null
  # E: a real taker (real PATH) that takes the fresh guard and waits inside it
  env PATH="$ORIG_PATH" bash -c 'start_lock="\$1"; lane_pid=\$\$; lane=e; SIG="\$3"; . "\$2"
    start_lock_hook() { [ "\$1" = gap ] && { : > "\$SIG/E-inside"; while [ ! -e "\$SIG/D-done" ]; do sleep 0.05; done; }; }
    take_over_dead x' _ "$L2" "$MSL" "$SIG" > /dev/null 2>&1 &
  echo \$! > "$SIG/E-pid"
  i=0; while [ ! -e "$SIG/E-inside" ] && [ \$i -lt 100 ]; do sleep 0.05; i=\$((i + 1)); done
fi
exec "$real" "\$@"
STUB
  chmod +x "$WORK/stub/$tool"
done
( start_lock="$L2"; lane_pid=$$; lane=d; guard_seen=""
  . "$MSL"
  export PATH="$WORK/stub:$PATH"
  take_over_dead x; take_over_dead x; take_over_dead x ) 2> /dev/null
check "D reached its cleanup of the dead holder's guard (the stub fired)" "$([ -e "$SIG/fired" ] && echo yes || echo no)" "yes"
check "E took the fresh guard and is inside it" "$([ -e "$SIG/E-inside" ] && echo yes || echo no)" "yes"
check "E is still inside its guard after D returned, so the guard exists" "$([ -d "$G2" ] && echo exists || echo destroyed)" "exists"
: > "$SIG/D-done"
E_PID="$(cat "$SIG/E-pid" 2> /dev/null)"
i=0; while [ -n "$E_PID" ] && kill -0 "$E_PID" 2> /dev/null && [ $i -lt 100 ]; do sleep 0.05; i=$((i + 1)); done

# --- case 3: a live holder's pid file behind a dead one keeps the lock (#2613) ---
L3="$WORK/c.lock"; G3="$L3.takeover"
ln -s "$(dead_pid)" "$L3"
sleep 600 & V_PID=$!
trap 'kill "$V_PID" 2> /dev/null; cleanup' EXIT
DEAD3="0000000001"   # all digits, leading 0: live_pid calls it stale, and it sorts before any real pid
mkdir "$G3"; : > "$G3/$DEAD3"; : > "$G3/$V_PID"
check "case 3 precondition: the dead name sorts before the live holder's" "$(printf '%s\n%s\n' "$V_PID" "$DEAD3" | sort | head -n 1)" "$DEAD3"
( start_lock="$L3"; lane_pid=$$; lane=x; . "$MSL"; take_over_dead x ) 2> /dev/null
check "a third waiter leaves the lock alone while a live holder's pid file is in the guard" "$([ -L "$L3" ] && echo kept || echo unlinked)" "kept"
check "the live holder's pid file is still in the guard" "$([ -e "$G3/$V_PID" ] && echo present || echo gone)" "present"

# --- case 4: an old empty guard with a dead owner is cleared and the lock taken over (#2613) ---
L4="$WORK/d.lock"; G4="$L4.takeover"
ln -s "$(dead_pid)" "$L4"
mkdir "$G4"; touch -t "$(date -v-5M +%Y%m%d%H%M 2> /dev/null || date -d '5 minutes ago' +%Y%m%d%H%M)" "$G4"   # BSD date -v, GNU date -d (macOS touch lacks a relative date)
( start_lock="$L4"; lane_pid=$$; lane=x; . "$MSL"; take_over_dead x; take_over_dead x ) 2> /dev/null
check "an old empty guard does not block the takeover: the lock is gone after two calls" "$([ -L "$L4" ] && echo kept || echo unlinked)" "unlinked"

# --- case 5: two overlapping takers leave no empty guard behind (#2631) ---
# A copy of the lib with a sleep after the announce line holds both takers in the guard at the same time. Each sees the other's
# live pid file and backs off. The last one out must remove the guard, or the next taker finds an empty young guard and leaves.
L5="$WORK/e.lock"; G5="$L5.takeover"; SLOW="$WORK/meta-start-lock-slow.sh"
sed '/^  : > "\$guard\/\$lane_pid" 2> \/dev\/null || return 0$/a\
  if [ "$winner" = 1 ]; then sleep 0.6; else sleep 0.9; fi' "$MSL" > "$SLOW"
check "case 5 precondition: the slowed copy has the sleep after the announce line" "$(grep -c 'sleep 0.9; fi' "$SLOW")" "1"
ln -s "$(dead_pid)" "$L5"
bash -c 'start_lock="$1"; lane_pid=$$; lane=a; . "$2"; take_over_dead x' _ "$L5" "$SLOW" > /dev/null 2>&1 & r1=$!
sleep 0.01
bash -c 'start_lock="$1"; lane_pid=$$; lane=b; . "$2"; take_over_dead x' _ "$L5" "$SLOW" > /dev/null 2>&1 & r2=$!
wait "$r1" 2> /dev/null; wait "$r2" 2> /dev/null
check "after two overlapping takers the guard is gone" "$([ -d "$G5" ] && echo exists || echo gone)" "gone"
[ -L "$L5" ] || ln -s "$(dead_pid)" "$L5"
( start_lock="$L5"; lane_pid=$$; lane=c; . "$MSL"; take_over_dead x ) 2> /dev/null
check "a dead lock right after the race is taken over at once" "$([ -L "$L5" ] && echo kept || echo unlinked)" "unlinked"

if [ "$fail" -eq 0 ]; then echo "meta-start-lock-guard: all passed"; else echo "meta-start-lock-guard: FAILED"; fi
exit $fail
