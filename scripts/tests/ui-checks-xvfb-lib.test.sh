#!/usr/bin/env bash
# Tests for tests/ui-checks/xvfb-lib.sh with a FAKE Xvfb on PATH (a script that writes the display's lock file and waits): a shell that
# starts the display and exits leaves no Xvfb behind, and an EXIT trap the shell already had still runs. Bounded: a watchdog ends the
# whole test after 20 s, and the lib's own waits are bounded (a normal run takes a few seconds).
set -u
unset TMUX
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LIB="$DIR/tests/ui-checks/xvfb-lib.sh"
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
W="$(mktemp -d)"
# No EXIT trap in this shell on purpose: a subshell below must see no trap of its own to chain. The watchdog is killed by pid at the end.
( sleep 20; echo "not ok - the test timed out" >&2; kill -TERM "$$" 2>/dev/null ) </dev/null >/dev/null 2>&1 &
WATCHDOG=$!
mkdir -p "$W/bin" "$W/locks"
# The fake: Xvfb :<n> ... writes the lock file the real one writes, then waits until it is terminated.
cat > "$W/bin/Xvfb" <<'FAKE'
#!/usr/bin/env bash
n="${1#:}"
echo $$ > "$XVFB_LOCK_DIR/.X${n}-lock"
sleep 600 </dev/null >/dev/null 2>&1 &
child=$!
# `wait` is interrupted by the signal at once, so the fake dies on TERM without finishing a sleep.
trap 'rm -f "$XVFB_LOCK_DIR/.X${n}-lock"; kill "$child" 2>/dev/null; exit 0' TERM
wait "$child"
FAKE
chmod +x "$W/bin/Xvfb"
export PATH="$W/bin:$PATH" XVFB_LOCK_DIR="$W/locks"

alive() { if kill -0 "$1" 2>/dev/null; then echo alive; else echo gone; fi; }

# 1. A shell that starts the display and exits stops that Xvfb, and an EXIT trap it already had still runs.
(
  trap 'echo ran > "$W/earlier-trap"' EXIT
  source "$LIB"
  xvfb_start 800x600x24 || exit 3
  echo "$XVFB_PID" > "$W/pid"
  echo "$DISPLAY" > "$W/display"
) </dev/null
pid="$(cat "$W/pid" 2>/dev/null)"
check "the display was started and exported" "$(cut -c1-2 "$W/display" 2>/dev/null)" ":9"
check "the fake Xvfb is gone after the shell exited" "$(alive "$pid")" "gone"
check "the EXIT trap the shell already had still ran" "$(cat "$W/earlier-trap" 2>/dev/null)" "ran"

# 2. xvfb_stop kills by pid and is safe twice.
( source "$LIB"; xvfb_start || exit 3; echo "$XVFB_PID" > "$W/pid2"; xvfb_stop; xvfb_stop; echo done > "$W/stopped" ) </dev/null
check "xvfb_stop twice is harmless" "$(cat "$W/stopped" 2>/dev/null)" "done"
check "xvfb_stop stopped the Xvfb" "$(alive "$(cat "$W/pid2" 2>/dev/null)")" "gone"

# 3. A display with a lock file is skipped.
echo 1 > "$W/locks/.X90-lock"
( source "$LIB"; xvfb_start || exit 3; echo "$DISPLAY" > "$W/display3" ) </dev/null
check "a locked display is skipped" "$(cat "$W/display3" 2>/dev/null)" ":91"

kill "$WATCHDOG" 2>/dev/null
rm -rf "$W"
exit "$fail"
