#!/usr/bin/env bash
# Tests for scripts/probe-linear-projects.sh with stub curl and gh. Run: bash scripts/tests/probe-linear-projects.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"
FIX="$ROOT/tests/fixtures/probe-linear-projects"
SCRIPT="$ROOT/scripts/probe-linear-projects.sh"
fail=0

check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
has() { # name haystack needle
  if grep -qF -- "$3" <<<"$2"; then echo "ok - $1"; else echo "not ok - $1 (missing: $3)"; sed 's/^/    /' <<<"$2"; fail=1; fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
KEY="lin_api_SUPERSECRETKEY123"
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state"
mkdir -p "$HOME" "$AW_STATE_DIR/sindri-app/profile" "$WORK/bin"
cp "$FIX/profile.yaml" "$AW_STATE_DIR/sindri-app/profile/profile.yaml"
cp "$FIX/curl" "$FIX/gh" "$WORK/bin/"
export STUB_FIX="$FIX" STUB_LOG="$WORK/stub.log" PATH="$WORK/bin:$PATH"
unset AW_SINDRI_HOME AW_LINEAR_PROBE_KEY STUB_MODE STUB_FAIL PROBE_TEST_LINEAR_KEY

run() { # env assignments first, then optional script args after a literal --; sets $out and $rc
  local envs=() args=()
  while [ $# -gt 0 ] && [ "$1" != -- ]; do envs+=("$1"); shift; done
  [ "${1:-}" = -- ] && shift
  args=("$@")
  out="$(env "${envs[@]}" bash "$SCRIPT" "${args[@]}" 2>&1)"; rc=$?
}

# --- happy path: key from the profile's env: pointer, teams and repo from the profile ---
run PROBE_TEST_LINEAR_KEY="$KEY"
check "happy: exit 0" "$rc" 0
for want in "projects: 3" "present.status: 3" "present.status.type: 3" "present.startedAt: 1" "present.lastUpdate: 1" \
  "present.lastUpdate.health: 1" "present.targetDate: 1" "present.lead: 2" "viewer: ok" "projects.led: 1" \
  "filter.project.status.type: accepted:yes" "issues.open_old_in_open_projects: 3" "issues.attachments25: accepted:yes" \
  "ratelimit.x-ratelimit-requests-remaining: 1499" "ratelimit.x-complexity: 310" "project 1: 2 vs 4" \
  "pr.merged_checked: 4" "pr.source1_attachment: 2" "pr.source2_key_in_text: 2" "pr.both: 1" "pr.either: 3" "pr.neither: 1" \
  "paste this into the Ph1/Ph6 PR"; do has "happy: $want" "$out" "$want"; done
block="$(sed -n '/paste this into/,$p' <<<"$out")"
has "happy: the paste block repeats the lines" "$block" "projects: 3"

# --- the key and record text never print ---
check "key never in output" "$(grep -c "$KEY" <<<"$out")" 0
check "viewer name never in output" "$(grep -c SECRET-NAME <<<"$out")" 0
check "key never an argument to curl" "$(grep -c "$KEY" "$STUB_LOG")" 0

# --- --repo is accepted ---
run PROBE_TEST_LINEAR_KEY="$KEY" -- --repo acme/api --window-days 30
check "--repo and --window-days: exit 0" "$rc" 0

# --- AW_LINEAR_PROBE_KEY wins (the profile's pointer variable is unset) ---
run AW_LINEAR_PROBE_KEY="$KEY"
check "env key: exit 0" "$rc" 0

# --- legacy state fallback ---
run PROBE_TEST_LINEAR_KEY="$KEY" STUB_MODE=legacy
check "legacy: exit 0" "$rc" 0
has "legacy: says so" "$out" "status: unsupported, using state"
has "legacy: state presence" "$out" "present.state: 3"

# --- nested project.status filter refused, project.state accepted ---
run PROBE_TEST_LINEAR_KEY="$KEY" STUB_MODE=nostatusfilter
check "filter fallback: exit 0" "$rc" 0
has "filter fallback: status refused" "$out" "filter.project.status.type: accepted:no"
has "filter fallback: state accepted" "$out" "filter.project.state: accepted:yes"

# --- no key: exit 2 ---
run PROBE_TEST_LINEAR_KEY=
check "no key: exit 2" "$rc" 2
has "no key: says so" "$out" "no Linear key"

# --- a failed call: exit 1 and names the call ---
run PROBE_TEST_LINEAR_KEY="$KEY" STUB_FAIL=IssuesPull
check "failed call: exit 1" "$rc" 1
has "failed call: names it" "$out" "failed: IssuesPull with attachments"
check "failed call: key still not printed" "$(grep -c "$KEY" <<<"$out")" 0

# --- usage ---
run PROBE_TEST_LINEAR_KEY="$KEY" -- --nope
check "bad flag: exit 2" "$rc" 2

if [ "$fail" -eq 0 ]; then echo "all ok"; else echo "FAILED"; exit 1; fi
