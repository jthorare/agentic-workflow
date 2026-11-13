#!/usr/bin/env bash
# The signal suites pass when they start with SIGHUP ignored (#2377).
#
# A gate started under `setsid nohup` hands every child SIGHUP at SIG_IGN. A child can't trap an ignored signal (POSIX),
# so a suite that sends SIGHUP to the script under test and asserts on its exit code only works when the suite's own
# exec wrapper puts SIGHUP (and every other signal it sends) back to the default first. This suite runs agent-token.test.sh,
# formal-verify-tlc.test.sh and meta-launch.test.sh (its pty case needs a real hangup, #2566) exactly that way: through a
# python exec with SIGHUP at SIG_IGN, and asserts each exits 0.
set -uo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null; rm -rf "$p"; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; }
trap __aw_tmp_clean EXIT

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SUITES=(scripts/tests/agent-token.test.sh scripts/tests/formal-verify-tlc.test.sh scripts/tests/meta-launch.test.sh)

if ! command -v python3 >/dev/null 2>&1; then
  echo "SKIP: python3 not found"
  exit 0
fi

pass=0
fail=0
for suite in "${SUITES[@]}"; do
  log="$(mktemp)"
  (
    cd "$ROOT" || exit 2
    python3 -c 'import os, signal, sys; signal.signal(signal.SIGHUP, signal.SIG_IGN); os.execvp("bash", ["bash", sys.argv[1]])' "$suite"
  ) >"$log" 2>&1
  rc=$?
  if [ "$rc" -eq 0 ]; then
    echo "PASS: $suite exits 0 with SIGHUP ignored"
    pass=$((pass + 1))
  else
    echo "FAIL: $suite exits $rc with SIGHUP ignored (want 0); its failing rows:"
    grep -E 'FAIL|not ok' "$log" | head -20 | sed 's/^/  /'
    fail=$((fail + 1))
  fi
  rm -f "$log"
done

echo "signal-suites-ignored-hup: $pass passed, $fail failed"
[ "$fail" -eq 0 ]
