#!/usr/bin/env bash
# src/Sindri/aot-smoke/normalize.sh WORK ROOT — the smoke's normalizer: stdin is the raw transcript, stdout what
# expected.txt pins. It drops the heartbeat's optional ' (last TIME)' suffix from status (present once the first beat
# ran, which a loaded box reaches before 'sindri status'), replaces this run's paths, host, hashes, pids, ports and times with fixed words, and drops doctor's
# claude-code line (re-probed inside Claude Code, the installed version elsewhere; the pending owner-probe lines are pinned).
# Tests: bash scripts/tests/sindri-smoke-normalize.test.sh
set -euo pipefail
work="$1" root="$2"
sed -E -e "s#$work#WORK#g" -e "s#$root#ROOT#g" -e "s#held by $(hostname -s)[^/ ]*#held by HOST#g" -e 's/[0-9a-f]{64}/HASH/g' -e 's/hash [0-9a-f]{6}/hash HASH6/g' -e 's/approve profile [0-9a-f]{6}/approve profile HASH6/g' \
  -e 's/pid [0-9]+/pid PID/g' -e 's#HOST/[0-9]+#HOST/PID#g' -e 's/127\.0\.0\.1:[0-9]+/127.0.0.1:PORT/g' \
  -e 's/[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:]{8}Z/TIME/g' -e 's/\(last [0-9:]{8}Z\)/(last TIME)/g' \
  -e 's/^(  services  heartbeat every [0-9]+s) \(last TIME\)$/\1/' \
  -e 's/cursor [0-9]+ of( head)? [0-9]+/cursor N of\1 M/g' -e 's/\(sequence [0-9]+\)/(sequence SEQ)/' -e 's/ITM-[0-9A-Za-z]+/ITM-ID/g' -e 's/REQ-[0-9A-Za-z]+/REQ-ID/g' -e 's/DEC-[0-9A-Za-z]+/DEC-ID/g' \
  -e '/^(ok|warn) +claude-code /d'
