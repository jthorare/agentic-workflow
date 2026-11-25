#!/usr/bin/env bash
# A non-owner release_lock and a dead-guard cleanup never destroy another waiter's lock or guard (#2581).
# Run: bash scripts/tests/skill-lock-release-guard.test.sh
#
# Case 1: release_lock moves the lock aside, sees it is not ours, and moves it back with mv, which overwrites. In the
# gap a waiter can ln a fresh lock; the move-back then replaces it with the dead holder's pid. A PATH stub for `mv`
# plays waiter A: when the destination is the lock file, A takes the lock (ln) first, then the real mv runs.
# A correct release never writes the lock path as a non-owner, so the stub never fires.
# Case 2: _lock_reclaim clears a dead holder's guard by removing its pid file, then rmdir. In between, another waiter
# can rmdir the empty guard and mkdir a fresh one; the late rmdir removes that fresh guard. A PATH stub for `rm` plays
# that waiter (E) right after the dead pid file is removed. Scratch AW_STATE_DIR only; no sleeps.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCK="$DIR/../../skills/_shared/skill-lock.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

real_mv="$(command -v mv)"; real_rm="$(command -v rm)"; real_ln="$(command -v ln)"
real_rmdir="$(command -v rmdir)"; real_mkdir="$(command -v mkdir)"

WORK="$(mktemp -d)"
trap '"$real_rm" -r -f -- "$WORK"' EXIT
export AW_STATE_DIR="$WORK/state"
LF="$AW_STATE_DIR/.browser.lock"
G="$LF.reclaim"
mkdir -p "$AW_STATE_DIR" "$WORK/stub" "$WORK/sig"

dead_pid() { bash -c 'exit 0' & local p=$!; wait "$p"; echo "$p"; }

cat > "$WORK/stub/mv" <<EOF
#!/usr/bin/env bash
dest=""; for a in "\$@"; do dest="\$a"; done
if [ "\$dest" = "$LF" ] && [ ! -e "$WORK/sig/A-tried" ]; then
  : > "$WORK/sig/A-tried"
  echo 4242 > "$WORK/A.tmp"
  if "$real_ln" "$WORK/A.tmp" "$LF" 2>/dev/null; then : > "$WORK/sig/A-got-lock"; fi
fi
exec "$real_mv" "\$@"
EOF
chmod +x "$WORK/stub/mv"

# --- case 1: a non-owner release must leave a fresh lock alone ---
Y="$(dead_pid)"; echo "$Y" > "$LF"
env PATH="$WORK/stub:$PATH" bash -c '
  SKILL_LOCK_OWNER_PID=$$ LOCK_NAME=browser source "$1"
  release_lock > /dev/null 2>&1; exit 0' _ "$LOCK"
if [ -e "$WORK/sig/A-got-lock" ]; then
  check "release_lock did not overwrite waiter A's fresh lock (A got it in the gap)" "$(cat "$LF" 2>/dev/null)" "4242"
else
  echo "ok - waiter A never got a gap (release did not write the lock path)"
fi
check "a non-owner release left a lock file in place" "$([ -e "$LF" ] && echo yes)" "yes"
"$real_rm" -f "$LF" "$WORK/sig/A-tried" "$WORK/sig/A-got-lock"

# --- case 2: dead-guard cleanup must leave a fresh guard alone ---
Z="$(dead_pid)"; "$real_mkdir" "$G"; : > "$G/$Z"
cat > "$WORK/stub/rm" <<EOF
#!/usr/bin/env bash
"$real_rm" "\$@"; rc=\$?
for a in "\$@"; do
  if [ "\$a" = "$G/$Z" ] && [ ! -e "$WORK/sig/E-tried" ]; then
    : > "$WORK/sig/E-tried"
    if "$real_rmdir" "$G" 2>/dev/null && "$real_mkdir" "$G" 2>/dev/null; then : > "$WORK/sig/E-made-guard"; fi
  fi
done
exit \$rc
EOF
chmod +x "$WORK/stub/rm"
env PATH="$WORK/stub:$PATH" bash -c '
  SKILL_LOCK_OWNER_PID=$$ LOCK_NAME=browser source "$1"
  _lock_reclaim || true; exit 0' _ "$LOCK" > /dev/null 2>&1
check "the dead guard's pid file was cleared (stub fired)" "$([ -e "$WORK/sig/E-tried" ] && echo yes)" "yes"
if [ -e "$WORK/sig/E-made-guard" ]; then
  check "a fresh guard made by waiter E survives the dead-guard cleanup" "$([ -d "$G" ] && echo yes)" "yes"
else
  echo "ok - waiter E never got a gap (cleanup left no window)"
fi

[ "$fail" -eq 0 ] && echo "skill-lock-release-guard: all passed" || echo "skill-lock-release-guard: FAILED"
exit "$fail"
