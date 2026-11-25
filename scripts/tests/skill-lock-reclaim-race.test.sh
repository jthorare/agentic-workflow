#!/usr/bin/env bash
# Two waiters reclaiming one dead holder of a skills/_shared/skill-lock.sh lock never both hold it (#2569).
# Run: bash scripts/tests/skill-lock-reclaim-race.test.sh
#
# The race: A and B both read the dead holder's pid. A removes the lock and takes a fresh one; B's remove comes late and
# deletes A's live lock, then B takes it too. A PATH stub for `rm` holds B at its remove of the lock file until A reports
# it holds the lock (or 5 s pass), which replays exactly that order. Scratch AW_STATE_DIR only.
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCK="$DIR/../../skills/_shared/skill-lock.sh"
fail=0
pids=()

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
cleanup() { [ "${#pids[@]}" -eq 0 ] || kill "${pids[@]}" 2>/dev/null; rm -rf -- "$WORK"; }
trap cleanup EXIT
export AW_STATE_DIR="$WORK/state"
LF="$AW_STATE_DIR/.browser.lock"
mkdir -p "$AW_STATE_DIR" "$WORK/stub" "$WORK/sig"

dead_pid() { bash -c 'exit 0' & local p=$!; wait "$p"; echo "$p"; }

# rm stub: for role B, a remove that names the lock file waits until A holds the lock (gives up after 5 s).
real_rm="$(command -v rm)"
cat > "$WORK/stub/rm" <<EOF
#!/usr/bin/env bash
if [ "\${STUB_ROLE:-}" = B ]; then
  for a in "\$@"; do
    if [ "\$a" = "$LF" ]; then
      : > "$WORK/sig/B-at-rm"
      for _ in \$(seq 1 50); do [ -e "$WORK/sig/A-holds" ] && break; sleep 0.1; done
    fi
  done
fi
exec "$real_rm" "\$@"
EOF
chmod +x "$WORK/stub/rm"

# waiter <role>: its own live owner (the shell's pid), takes the lock, records it, holds it 4 s, releases.
waiter() {
  env STUB_ROLE="$1" PATH="$WORK/stub:$PATH" SKILL_LOCK_TIMEOUT=2 bash -c '
    SKILL_LOCK_OWNER_PID=$$ LOCK_NAME=browser source "$1"
    if acquire_lock > /dev/null; then echo "$2" >> "$3/holders"; : > "$3/$2-holds"; sleep 4; release_lock > /dev/null; fi
    exit 0' _ "$LOCK" "$1" "$WORK/sig"
}

# --- B reads the dead holder and is held at its remove; A then reclaims and takes the lock; B's late remove follows ---
echo "$(dead_pid)" > "$LF"
waiter B & pb=$!; pids+=("$pb")
for _ in $(seq 1 50); do [ -e "$WORK/sig/B-at-rm" ] && break; sleep 0.1; done
check "B reached its remove of the dead holder's lock" "$([ -e "$WORK/sig/B-at-rm" ] && echo yes)" "yes"
waiter A & pa=$!; pids+=("$pa")
wait "$pa" "$pb"
check "only one waiter got the lock while the other held it (never both)" "$(wc -l < "$WORK/sig/holders" 2> /dev/null | tr -d ' ')" "1"

[ "$fail" -eq 0 ] && echo "skill-lock-reclaim-race: all passed" || echo "skill-lock-reclaim-race: FAILED"
exit "$fail"
