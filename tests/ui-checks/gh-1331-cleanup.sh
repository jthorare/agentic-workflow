#!/usr/bin/env bash
# tests/ui-checks/gh-1331-cleanup.sh — check for #1331: a Context UI check leaves no Xvfb and no process of its own serve behind.
# Expected: after a check built on context-lib.sh (ctx_init + ctx_start) has exited, the Xvfb its xvfb-run started is gone, and no process of ours still has
# that run's AW_SINDRI_HOME in its environment (serve's children, such as a hung 'docker version' probe, carry it).
# A child bash runs ctx_init + ctx_start and exits through context-lib.sh's own EXIT trap, writing the Xvfb pid and the home to a file first;
# this script asserts after the child is gone. It never kills anything: a FAIL leaves the leftovers for whoever reads the message.
#   bash tests/ui-checks/gh-1331-cleanup.sh        exit 0 pass; 1 FAIL (one line on stderr); 2 the setup broke
# Env: CTXUX_OUT (screenshots; default a new temp dir), AW_SINDRI_UI (a built sindri-ui), SINDRI (a built sindri).
set -uo pipefail
unset TMUX
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
notes="$(mktemp "${TMPDIR:-/tmp}/gh-1331-notes-XXXXXX")" || exit 2

# The inner run: what any Context check does, then a normal exit (the trap cleans up).
# shellcheck disable=SC2016  # the inner script is single-quoted on purpose
inner='
source "$1/context-lib.sh"
ctx_init gh-1331-inner
ctx_start 1280 900
descendants() { local p c; for c in $(pgrep -P "$1" 2>/dev/null); do echo "$c"; descendants "$c"; done; }
xvfb=""
for p in $(descendants "$APP_WRAPPER_PID"); do
  [ "$(cat "/proc/$p/comm" 2>/dev/null)" = "Xvfb" ] && xvfb="$p"
done
[ -n "$xvfb" ] || setup_fail "no Xvfb under the app wrapper $APP_WRAPPER_PID"
printf "%s\n%s\n" "$xvfb" "$AW_SINDRI_HOME" > "$2"
exit 0'
bash -c "$inner" _ "$here" "$notes"
status=$?
[ "$status" = 0 ] || { echo "gh-1331-cleanup: the inner check exited $status (setup broke)" >&2; exit 2; }
{ read -r xvfb; read -r home; } <"$notes"
[ -n "$xvfb" ] && [ -n "$home" ] || { echo "gh-1331-cleanup: the inner check wrote no pid/home" >&2; exit 2; }

sleep 2   # the trap's own waits are done; this only lets the kernel reap

if [ "$(cat "/proc/$xvfb/comm" 2>/dev/null)" = "Xvfb" ]; then
  echo "FAIL: Xvfb pid $xvfb is still running after the check exited" >&2
  exit 1
fi

left=""
for d in /proc/[0-9]*; do
  pid="${d#/proc/}"
  [ "$pid" = "$$" ] || [ "$pid" = "$BASHPID" ] && continue
  [ -O "$d" ] || continue
  if tr '\0' '\n' <"$d/environ" 2>/dev/null | grep -qxF "AW_SINDRI_HOME=$home"; then
    left="$left $pid($(cat "$d/comm" 2>/dev/null))"
  fi
done
[ -z "$left" ] || { echo "FAIL: processes of the check's own home ($home) are still running:$left" >&2; exit 1; }
echo "PASS: no Xvfb (pid $xvfb) and no process of $home left behind"
