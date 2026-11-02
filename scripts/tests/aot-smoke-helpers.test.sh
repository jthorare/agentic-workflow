#!/usr/bin/env bash
# src/Sindri/aot-smoke/helpers.sh with a fake sindri (#1949): a session's manual service run gets past a scheduled run's
# SND-JOB-409, as the human's does, so the smoke transcript doesn't depend on whether heartbeat ticked in between.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
fail=0
check() { # name condition-exit-code
  if [ "$2" -eq 0 ]; then echo "ok - $1"; else echo "not ok - $1"; fail=1; fi
}

export AW_SINDRI_HOME="$tmp/home"
mkdir -p "$AW_SINDRI_HOME/auth"
: >"$AW_SINDRI_HOME/auth/human.token"

# The fake: the caller is the session when the token file is out of the way, the human otherwise. The human always
# succeeds; the session's first call meets a scheduled run in flight.
cat >"$tmp/sindri" <<'FAKE'
#!/usr/bin/env bash
tmp="$(dirname "$0")"
if [ "$1 $2" != "services run" ]; then exit 2; fi
if [ -e "$AW_SINDRI_HOME/auth/human.token" ]; then who=human; job=JOB-1; else who=session; job=JOB-2; fi
n=$(($(cat "$tmp/$who.count" 2>/dev/null || echo 0) + 1)); echo "$n" >"$tmp/$who.count"
if [ "$who" = session ] && [ "$n" -eq 1 ]; then
  echo "SND-JOB-409 A scheduled run of $3 is in flight (fix: wait for it to end, then run it again)" >&2
  exit 2
fi
echo "job $job started"
echo "run $3 (manual)"
echo "succeeded: ok: worked"
FAKE
chmod +x "$tmp/sindri"

bin="$tmp/sindri"
alarm=(env)
bounded() { "$@"; }
with_tty() { "$@"; }
settle() { :; }
# shellcheck source=/dev/null
source "$ROOT/src/Sindri/aot-smoke/helpers.sh"

out="$(manual_service_runs heartbeat)"
session_part="${out#*"## sindri services run heartbeat (session)"}"

[[ "$out" == *"job JOB-1 started"* ]]; check "the human's run prints its job" $?
[[ "$out" != *SND-JOB-409* ]]; check "no SND-JOB-409 reaches the transcript" $?
[[ "$out" != *"exit 2"* ]]; check "no exit 2 reaches the transcript" $?
[[ "$session_part" == *"job JOB-2 started"* && "$session_part" == *"succeeded: ok: worked"* ]]; check "the session's run succeeds" $?
[ -e "$AW_SINDRI_HOME/auth/human.token" ]; check "the token file is back in place" $?

if [ "$fail" -eq 0 ]; then echo "PASS: aot-smoke-helpers tests"; else echo "FAIL: aot-smoke-helpers tests"; exit 1; fi
