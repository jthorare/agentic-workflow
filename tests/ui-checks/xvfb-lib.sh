# tests/ui-checks/xvfb-lib.sh — sourced by the UI checks that need a display. It starts its own Xvfb instead of using xvfb-run:
# xvfb-run leaves its Xvfb behind when the check is killed by a timeout, and the leaked servers pile up. Here the check holds the
# Xvfb's pid and an EXIT trap stops exactly that pid (never a pattern kill).
#
#   xvfb_start [<screen>]   pick a free display (:90 to :199, none with a /tmp/.X<n>-lock), start Xvfb on it, wait up to 10 s until
#                           it answers, export DISPLAY, set XVFB_PID and arrange `xvfb_stop` on EXIT before any EXIT trap that is
#                           already set. The screen defaults to 1600x1000x24. Returns 1 when no display could be started.
#   xvfb_stop               kill XVFB_PID by pid and reap it; safe to call twice.
# Env: XVFB_LOCK_DIR (default /tmp: where the X<n>-lock files are looked for; setting it also makes the lock file the readiness
# signal, which is what a fake Xvfb in a test can give).
# Bash 3.2 compatible.

XVFB_PID=""

xvfb_ready() {
  local n="$1"
  if [ -z "${XVFB_LOCK_DIR:-}" ] && command -v xdpyinfo >/dev/null 2>&1; then
    DISPLAY=":$n" xdpyinfo >/dev/null 2>&1
  else
    [ -e "${XVFB_LOCK_DIR:-/tmp}/.X${n}-lock" ]
  fi
}

xvfb_start() {
  local screen="${1:-1600x1000x24}" n i started=0 previous="" previous_cmd=""
  command -v Xvfb >/dev/null 2>&1 || { echo "xvfb-lib: Xvfb is not installed" >&2; return 1; }
  for n in $(seq 90 199); do
    [ -e "${XVFB_LOCK_DIR:-/tmp}/.X${n}-lock" ] && continue
    Xvfb ":$n" -screen 0 "$screen" -nolisten tcp </dev/null >/dev/null 2>&1 &
    XVFB_PID=$!
    for i in $(seq 1 100); do
      kill -0 "$XVFB_PID" 2>/dev/null || break   # it exited (the display was taken after all): try the next one
      if xvfb_ready "$n"; then started=1; break; fi
      sleep 0.1
    done
    if [ "$started" = 1 ]; then break; fi
    xvfb_stop
  done
  if [ "$started" != 1 ]; then echo "xvfb-lib: no Xvfb display came up" >&2; return 1; fi
  export DISPLAY=":$n"
  # Chain: the EXIT trap the check already has (its own cleanup) runs after the Xvfb is stopped.
  # `trap -p` runs in this shell, not in a command substitution, so it reports this shell's trap on every bash.
  local listing
  listing="$(mktemp)" && trap -p EXIT >"$listing" && previous="$(cat "$listing")"
  rm -f "$listing"
  if [ -n "$previous" ]; then
    eval "set -- $previous"   # trap -- 'command' EXIT
    previous_cmd="${3:-}"
  fi
  # shellcheck disable=SC2064 # the previous command is embedded on purpose, once
  trap "xvfb_stop; $previous_cmd" EXIT
}

xvfb_stop() {
  local i
  if [ -n "${XVFB_PID:-}" ]; then
    kill "$XVFB_PID" 2>/dev/null || true
    # Bounded: up to 3 s for it to go, then KILL; the wait below only reaps a process that is already gone.
    for i in $(seq 1 30); do
      kill -0 "$XVFB_PID" 2>/dev/null || break
      sleep 0.1
    done
    if kill -0 "$XVFB_PID" 2>/dev/null; then kill -KILL "$XVFB_PID" 2>/dev/null || true; sleep 0.1; fi
    wait "$XVFB_PID" 2>/dev/null || true
    XVFB_PID=""
  fi
}
