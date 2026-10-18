#!/usr/bin/env bash
# The Sindri host smoke's normalizer (src/Sindri/aot-smoke/normalize.sh): a captured transcript normalizes to what
# expected.txt pins, whether or not the heartbeat has ticked before 'sindri status' runs.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
N="$ROOT/src/Sindri/aot-smoke/normalize.sh"
fail=0
check() { # name input expected
  local got; got="$(printf '%s\n' "$2" | bash "$N" /tmp/smoke-work /tmp/smoke-root)"
  if [ "$got" = "$3" ]; then echo "ok - $1"; else echo "not ok - $1"; printf '  expected: %s\n  got:      %s\n' "$3" "$got"; fail=1; fi
}
check "status before the first heartbeat" "  services  heartbeat every 5s" "  services  heartbeat every 5s"
check "status after the first heartbeat (the (last …) suffix is dropped)" "  services  heartbeat every 5s (last 12:34:56Z)" "  services  heartbeat every 5s"
check "the home path is WORK" "  profile   /tmp/smoke-work/home/profile/profile.yaml (approved, hash 90f90d)" "  profile   WORK/home/profile/profile.yaml (approved, hash HASH6)"
check "the pid is a fixed word" "  lease     held by pid 4242, epoch 1" "  lease     held by pid PID, epoch 1"
check "the port is a fixed word" "  api       127.0.0.1:60237" "  api       127.0.0.1:PORT"
check "doctor's claude-code line is dropped" "$(printf 'ok    claude-code re-probed: the markers are set, version 2.1.296\nwarn  cursor-agent pending: owner probe')" "warn  cursor-agent pending: owner probe"
check "doctor's launcher line is dropped (this box's viewer, config path and probe facts)" "$(printf 'ok    launcher          viewer print from /home/x/launcher.json; probed 3 min ago; 0 request(s) past their deadline; last failure none\nwarn  cursor-agent pending: owner probe')" "warn  cursor-agent pending: owner probe"
check "doctor's launcher:agents line is dropped (this box's CLI paths)" "$(printf 'warn  launcher:agents  claude-code (builtin): /usr/bin/claude; codex (builtin): not on the login PATH\nwarn  cursor-agent pending: owner probe')" "warn  cursor-agent pending: owner probe"
check "doctor's launcher:adoption line is kept (it is pinned)" "ok    launcher:adoption no opens in the last 30 days" "ok    launcher:adoption no opens in the last 30 days"
check "this host's name is HOST" "  lease     held by $(hostname -s)/4242 since 2026-10-14T12:00:00Z (epoch 1)" "  lease     held by HOST/PID since TIME (epoch 1)"
check "a full hash is HASH before its 6-digit form" "hash $(printf 'a%.0s' $(seq 64)) (approve profile abcdef)" "hash HASH (approve profile HASH6)"
check "the repo root is ROOT" "## sindri recipe validate /tmp/smoke-root/recipes/sindri/workflow.step.yaml" "## sindri recipe validate ROOT/recipes/sindri/workflow.step.yaml"
check "status's views line keeps its words and drops the numbers" "  views     cursor 7 of 9 (rebuilding: cursor 3 of 9)" "  views     cursor N of M (rebuilding: cursor N of M)"
check "doctor's views line keeps 'head'" "ok    views             cursor 12 of head 12" "ok    views             cursor N of head M"
check "a rebuild's sequence is SEQ" "views rebuild requested (sequence 31)" "views rebuild requested (sequence SEQ)"
check "an item id is ITM-ID, its time and sequence fixed words" "ITM-0000000000000000000000000000000e  Smoke item  new  since 2026-10-15T09:00:01Z" "ITM-ID  Smoke item  new  since TIME"
check "an item add's sequence is SEQ" "ITM-00000000000000000000000000000014 created (sequence 12)" "ITM-ID created (sequence SEQ)"
check "a request and its decision id are REQ-ID and DEC-ID" "REQ-0192a1b2c3d4e5f6a7b8c9d0e1f2a301 filed; DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2a302 waits for a human (sequence 14)" "REQ-ID filed; DEC-ID waits for a human (sequence SEQ)"
check "a decision's command line names DEC-ID" "  sindri decide DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2a302 accept" "  sindri decide DEC-ID accept"
check "a job id is JOB-ID" "job JOB-0123456789abcdef0123456789abcdef started" "job JOB-ID started"
[ "$fail" -eq 0 ] && echo "PASS: sindri-smoke-normalize tests" || { echo "FAIL: sindri-smoke-normalize tests"; exit 1; }
