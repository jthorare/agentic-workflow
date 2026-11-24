#!/usr/bin/env bash
# config/lib/policy.sh — the one reader of an intervention policy's mode (#4257), sourced by the hooks that can deny
# or advise (retry-guard first). Contract: docs/hooks/replay.md "Policy mode".
#   policy_mode <policy>   prints off, advisory or block, one line
# The first source that is set decides:
#   1. env AW_POLICY_<NAME>_MODE                   (retry-guard/1 -> AW_POLICY_RETRY_GUARD_1_MODE)
#   2. $AW_STATE_DIR/policy/<name>.mode            (retry-guard/1 -> retry-guard-1.mode; the owner's per-box switch)
#   3. .policies["<policy>"].mode of interventions.json, in ../policy/ next to this lib (config/policy/ in the repo)
#   4. advisory
# An empty env var or a file with only spaces counts as not set. A set value that is not off, advisory or block reads
# as advisory. So does a policy id that is not <name>/<version>, an unknown policy, a missing or broken JSON file, no
# jq, and a JSON block whose evidence is null. $AW_STATE_DIR defaults to ~/.agentic-workflow. Nothing here writes,
# prints to stderr or exits; the function always returns 0.

# policy_mode <policy>: the policy's mode, see above.
policy_mode() {
  local id="${1:-}" name env_name value json
  case $id in
    [a-z0-9]*/[0-9]*) ;;
    *) printf 'advisory\n'; return 0 ;;
  esac
  name="${id%/*}"
  case $name in *[!a-z0-9-]* | *- | *--*) printf 'advisory\n'; return 0 ;; esac
  case ${id#*/} in *[!0-9]*) printf 'advisory\n'; return 0 ;; esac
  # The two names, one rule: env uppercases and turns - and / into _, the file turns / into -.
  env_name="${id//[-\/]/_}"
  env_name="AW_POLICY_$(printf '%s' "$env_name" | tr '[:lower:]' '[:upper:]' 2>/dev/null)_MODE"
  value="${!env_name:-}"
  value="${value//[[:space:]]/}"
  if [ -z "$value" ]; then
    value="$(_policy_read_file "${AW_STATE_DIR:-${HOME:-}/.agentic-workflow}/policy/${id//\//-}.mode")"
  fi
  if [ -z "$value" ]; then
    json="${BASH_SOURCE[0]}"
    case $json in */*) json="${json%/*}" ;; *) json=. ;; esac
    json="$json/../policy/interventions.json"
    value="$(jq -r --arg id "$id" \
      '.policies[$id] | if .mode == "block" and .evidence == null then "advisory" else .mode end // empty' \
      "$json" 2>/dev/null)" || value=""
  fi
  case $value in
    off | advisory | block) printf '%s\n' "$value" ;;
    *) printf 'advisory\n' ;;
  esac
  return 0
}

# _policy_read_file <path>: the file's text with all whitespace removed, or nothing when it is missing.
_policy_read_file() {
  local text=""
  [ -f "$1" ] || return 0
  { text="$(< "$1")"; } 2>/dev/null
  printf '%s' "${text//[[:space:]]/}"
}
