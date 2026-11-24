#!/usr/bin/env bash
# Tests for config/lib/policy.sh (policy_mode, #4257): the one reader of an intervention policy's mode.
# Order: env AW_POLICY_<NAME>_MODE > $AW_STATE_DIR/policy/<name>.mode > config/policy/interventions.json > advisory.
# A set but bad value reads as advisory, a JSON block without evidence reads as advisory, and the reader never
# writes and never exits non-zero. Contract: docs/hooks/replay.md "Policy mode".
# Run: bash config/lib/tests/policy.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIB="$DIR/../policy.sh"
REPO_JSON="$DIR/../../policy/interventions.json"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
# A copy of the lib with its own policy file beside it, laid out like the repo (lib/ and policy/ siblings).
TREE="$WORK/tree"
mkdir -p "$TREE/lib" "$TREE/policy"
cp "$LIB" "$TREE/lib/policy.sh"
STATE="$WORK/state"
mkdir -p "$STATE"

# json <retry-guard/1 mode> <evidence JSON>: writes the tree's policy file with one policy.
json() {
  printf '{"schema":1,"policies":{"retry-guard/1":{"mode":"%s","promotion":{},"evidence":%s}}}\n' "$1" "$2" \
    > "$TREE/policy/interventions.json"
}
# mode <policy> [env assignments...]: policy_mode from the tree copy in a clean shell; prints "<stdout>|rc=<rc>".
mode() {
  local policy="$1"; shift
  local out rc
  out="$(env -u AW_POLICY_RETRY_GUARD_1_MODE AW_STATE_DIR="$STATE" "$@" \
    "$BASH" -c '. "$1" && policy_mode "$2"' _ "$TREE/lib/policy.sh" "$policy" 2>/dev/null)"
  rc=$?
  printf '%s|rc=%s' "$out" "$rc"
}
mode_file() { mkdir -p "$STATE/policy"; printf '%s\n' "$1" > "$STATE/policy/retry-guard-1.mode"; }
no_mode_file() { rm -f "$STATE/policy/retry-guard-1.mode"; }

check "the lib file exists" "$([ -f "$LIB" ] && echo y)" "y"

# The repo's own policy file: retry-guard/1 ships advisory with no evidence.
check "the repo's interventions.json is valid JSON" "$(jq -e . "$REPO_JSON" >/dev/null 2>&1 && echo y)" "y"
check "repo: retry-guard/1 is advisory" \
  "$(env -u AW_POLICY_RETRY_GUARD_1_MODE AW_STATE_DIR="$STATE" bash -c '. "$1" && policy_mode retry-guard/1' _ "$LIB" 2>/dev/null)" "advisory"
check "repo: retry-guard/1 evidence is null" "$(jq -r '.policies["retry-guard/1"].evidence' "$REPO_JSON" 2>/dev/null)" "null"

# JSON default, nothing else set.
json advisory null
check "JSON advisory -> advisory" "$(mode retry-guard/1)" "advisory|rc=0"
json off null
check "JSON off -> off" "$(mode retry-guard/1)" "off|rc=0"
json block '{"reportSha256":"a","fixturesSha256":"b","criteriaSha256":"c","reviewedBy":"owner"}'
check "JSON block with evidence -> block" "$(mode retry-guard/1)" "block|rc=0"
json block null
check "JSON block with evidence null -> advisory" "$(mode retry-guard/1)" "advisory|rc=0"
json bogus null
check "JSON bad mode -> advisory" "$(mode retry-guard/1)" "advisory|rc=0"
printf '{"schema":1,"policies":{"retry-guard/1":{"mode":"off"' > "$TREE/policy/interventions.json"
check "JSON truncated -> advisory" "$(mode retry-guard/1)" "advisory|rc=0"
rm -f "$TREE/policy/interventions.json"
check "JSON missing -> advisory" "$(mode retry-guard/1)" "advisory|rc=0"
json off null
check "JSON without jq on PATH -> advisory" "$(mode retry-guard/1 PATH="$WORK/empty-bin")" "advisory|rc=0"

# Unknown and malformed policy ids.
json off null
check "unknown policy -> advisory" "$(mode retry-guard/2)" "advisory|rc=0"
check "no policy argument -> advisory" "$(mode '')" "advisory|rc=0"
check "an id with a path in it -> advisory" "$(mode '../retry-guard/1')" "advisory|rc=0"
check "an id without a version -> advisory" "$(mode retry-guard)" "advisory|rc=0"

# The state file beats the JSON.
json advisory null
mode_file off
check "file off beats JSON advisory" "$(mode retry-guard/1)" "off|rc=0"
mode_file block
check "file block beats JSON advisory (the owner's switch)" "$(mode retry-guard/1)" "block|rc=0"
json block '{"reportSha256":"a","fixturesSha256":"b","criteriaSha256":"c","reviewedBy":"owner"}'
mode_file advisory
check "file advisory beats JSON block (the rollback switch)" "$(mode retry-guard/1)" "advisory|rc=0"
printf '  off  \n\n' > "$STATE/policy/retry-guard-1.mode"
check "file value with spaces and blank lines -> off" "$(mode retry-guard/1)" "off|rc=0"
mode_file OFF
check "file bad value -> advisory, not the JSON block" "$(mode retry-guard/1)" "advisory|rc=0"
: > "$STATE/policy/retry-guard-1.mode"
check "empty file counts as unset -> JSON block" "$(mode retry-guard/1)" "block|rc=0"
no_mode_file

# The env var beats the file.
json advisory null
mode_file off
check "env block beats file off" "$(mode retry-guard/1 AW_POLICY_RETRY_GUARD_1_MODE=block)" "block|rc=0"
check "env advisory beats file off" "$(mode retry-guard/1 AW_POLICY_RETRY_GUARD_1_MODE=advisory)" "advisory|rc=0"
check "env bad value -> advisory, not the file's off" "$(mode retry-guard/1 AW_POLICY_RETRY_GUARD_1_MODE=nope)" "advisory|rc=0"
check "env empty counts as unset -> file off" "$(mode retry-guard/1 AW_POLICY_RETRY_GUARD_1_MODE=)" "off|rc=0"
no_mode_file
check "env off with no file -> off" "$(mode retry-guard/1 AW_POLICY_RETRY_GUARD_1_MODE=off)" "off|rc=0"

# AW_STATE_DIR unset: the file is read under $HOME/.agentic-workflow.
mkdir -p "$WORK/home/.agentic-workflow/policy"
echo off > "$WORK/home/.agentic-workflow/policy/retry-guard-1.mode"
json advisory null
check "AW_STATE_DIR unset -> reads ~/.agentic-workflow/policy" \
  "$(env -u AW_STATE_DIR -u AW_POLICY_RETRY_GUARD_1_MODE HOME="$WORK/home" \
    bash -c '. "$1" && policy_mode retry-guard/1' _ "$TREE/lib/policy.sh" 2>/dev/null)" "off"

# Sourcing under set -e and calling with a bad id never kills the caller.
check "set -eu caller survives a bad id" \
  "$(env AW_STATE_DIR="$STATE" bash -c 'set -eu; . "$1"; policy_mode "../x" >/dev/null; echo alive' _ "$TREE/lib/policy.sh" 2>/dev/null)" "alive"

# Never writes: the state dir (without a policy dir) and the tree are the same before and after every kind of call.
EMPTY_STATE="$WORK/empty-state"
mkdir -p "$EMPTY_STATE"
before="$(cd "$WORK" && find tree empty-state -printf '%p %s %T@\n' | LC_ALL=C sort)"
for args in "retry-guard/1" "retry-guard/2" "../x" ""; do
  env AW_STATE_DIR="$EMPTY_STATE" AW_POLICY_RETRY_GUARD_1_MODE=nope bash -c '. "$1" && policy_mode "$2"' _ "$TREE/lib/policy.sh" "$args" >/dev/null 2>&1
  env AW_STATE_DIR="$EMPTY_STATE" bash -c '. "$1" && policy_mode "$2"' _ "$TREE/lib/policy.sh" "$args" >/dev/null 2>&1
done
after="$(cd "$WORK" && find tree empty-state -printf '%p %s %T@\n' | LC_ALL=C sort)"
check "no call writes a file or a directory" "$after" "$before"
check "no policy dir is created in the state dir" "$([ -e "$EMPTY_STATE/policy" ] && echo made || echo none)" "none"

# Stderr stays quiet on a bad JSON file: hooks print stderr to the agent.
printf 'not json' > "$TREE/policy/interventions.json"
check "bad JSON: nothing on stderr" \
  "$(env AW_STATE_DIR="$STATE" bash -c '. "$1" && policy_mode retry-guard/1 >/dev/null' _ "$TREE/lib/policy.sh" 2>&1)" ""

exit "$fail"
