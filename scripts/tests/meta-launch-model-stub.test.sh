#!/usr/bin/env bash
# Tests for the routed-lane stub of meta-launch.test.sh (scripts/tests/fixtures/meta-launch/agent-model): the `<bin> --help` probe
# that launcher_cli_has_flag runs records no launch, and a launch-shaped call records its argv and pid (#2637).
# Run: bash scripts/tests/meta-launch-model-stub.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() { if [ "$2" == "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1; fi; }
waitfor() { for _ in $(seq 100); do eval "$1" && return 0; sleep 0.1; done; return 1; }
WORK="$(mktemp -d)"
cleanup() { rm -r "$WORK"; }
trap cleanup EXIT
# shellcheck source=../launcher-lib.sh
. "$DIR/launcher-lib.sh"
cp "$DIR/tests/fixtures/meta-launch/agent-model" "$WORK/agent-model"; chmod +x "$WORK/agent-model"
export AGENT_MODEL_SLEEP=5 AW_LAUNCHER_PROBE_SECONDS=1

# The probe the lane script runs before a codex launch.
export CALLS="$WORK/probe"
launcher_cli_has_flag "$WORK/agent-model" --no-daemon > /dev/null 2>&1
sleep 0.3
check "the --no-daemon probe of the routed-lane stub records no launch: no model file, no pid file" \
  "$([ -e "$CALLS.model" ] && echo model):$([ -e "$CALLS.modelpid" ] && echo pid)" ":"

# A launch-shaped call records its argv and pid.
export CALLS="$WORK/launch"
"$WORK/agent-model" -m gpt-5.5 -C x &
lp=$!
waitfor '[ -s "$CALLS.modelpid" ]'
check "a launch-shaped call records its argv" "$(cat "$CALLS.model" 2> /dev/null)" "-m|gpt-5.5|-C|x|"
check "a launch-shaped call records its pid" "$(cat "$CALLS.modelpid" 2> /dev/null)" "$lp"
[ -s "$CALLS.modelpid" ] && kill "$(cat "$CALLS.modelpid")" 2> /dev/null
wait "$lp" 2> /dev/null
exit "$fail"
