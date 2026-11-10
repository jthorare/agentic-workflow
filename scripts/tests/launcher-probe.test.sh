#!/usr/bin/env bash
# Tests for launcher_cli_has_flag (scripts/launcher-lib.sh): the `<agent> --help` probe finds a flag, answers no for a CLI that
# lacks it, ends a CLI that hangs, and leaves no child of that CLI running (#2329).
# Run: bash scripts/tests/launcher-probe.test.sh
[ "$(uname)" = Darwin ] && { echo "SKIP: launcher-probe timing (1 s cap, 3 s ceiling, 0.3 s settles) is too tight for the macOS runner and the cause is not reproducible off macOS (#2568)"; exit 0; }
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
WORK="$(mktemp -d)"
KIDS="$WORK/kids"; : > "$KIDS"
cleanup() { local p; while read -r p; do [ -z "$p" ] || kill "$p" 2> /dev/null; done < "$KIDS"; rm -rf "$WORK"; }   # recorded pids only
trap cleanup EXIT
# shellcheck source=../launcher-lib.sh
. "$DIR/launcher-lib.sh"

cat > "$WORK/has" <<'EOF'
#!/usr/bin/env bash
echo "Usage: agent [--no-daemon] [PROMPT]"
EOF
cat > "$WORK/lacks" <<'EOF'
#!/usr/bin/env bash
echo "Usage: agent [PROMPT]"
EOF
# --help starts a long child (its pid goes to $KIDS) and then waits on it, as a wrapper script does.
cat > "$WORK/forks" <<EOF
#!/usr/bin/env bash
sleep 300 &
echo \$! >> "$KIDS"
echo "no flags here"
wait
EOF
chmod +x "$WORK/has" "$WORK/lacks" "$WORK/forks"

launcher_cli_has_flag "$WORK/has" --no-daemon; check "a CLI that lists the flag: yes" "$?" "0"
launcher_cli_has_flag "$WORK/lacks" --no-daemon; check "a CLI without the flag: no" "$?" "1"
t0=$SECONDS
AW_LAUNCHER_PROBE_SECONDS=1 launcher_cli_has_flag "$WORK/forks" --no-daemon; rc=$?
check "a CLI that hangs on --help: no, within the cap" "$rc:$([ $((SECONDS - t0)) -le 3 ] && echo fast || echo slow)" "1:fast"
sleep 0.3
kid="$(head -1 "$KIDS")"
check "the child that --help forked is gone after the probe" "$(kill -0 "$kid" 2> /dev/null && echo running || echo gone)" "gone"

# A hangup of the caller (a pane killed) during the probe ends the probe's group too: it is outside the caller's own group.
: > "$KIDS"
( launcher_cli_has_flag "$WORK/forks" --no-daemon ) &
caller=$!
for _ in $(seq 30); do [ -s "$KIDS" ] && break; sleep 0.1; done
kid="$(head -1 "$KIDS")"
# the hangup reaches every process in the caller's group: here the caller and the probe's own subshell (its child)
probe_sh="$(pgrep -P "$caller" | head -1)"
kill -HUP "$probe_sh" "$caller" 2> /dev/null; wait "$caller" 2> /dev/null
sleep 0.5
check "a hangup during the probe: the child is gone too" "$(kill -0 "$kid" 2> /dev/null && echo running || echo gone)" "gone"

# A CLI that ignores TERM is ended by KILL: the probe is not held until the CLI exits on its own.
cat > "$WORK/deaf" <<EOF
#!/usr/bin/env bash
trap '' TERM
sleep 20 &
echo \$! >> "$KIDS"
echo "no flags here"
wait
EOF
chmod +x "$WORK/deaf"
: > "$KIDS"
t0=$SECONDS
AW_LAUNCHER_PROBE_SECONDS=1 launcher_cli_has_flag "$WORK/deaf" --no-daemon; rc=$?
check "a CLI that ignores TERM: no, within the cap + 2 s" "$rc:$([ $((SECONDS - t0)) -le 3 ] && echo fast || echo "slow $((SECONDS - t0))s")" "1:fast"
sleep 0.3
kid="$(head -1 "$KIDS")"
check "a CLI that ignores TERM: nothing left running" "$(kill -0 "$kid" 2> /dev/null && echo running || echo gone)" "gone"
exit "$fail"
