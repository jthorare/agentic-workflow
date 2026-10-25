#!/usr/bin/env bash
# Tests for the prompt-sort entry that scripts/install-judge-hooks.sh --hook-only installs.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"

fresh() { local d; d="$(mktemp -d)"; echo '{}' > "$d/settings.json"; mkdir -p "$d/hooks" "$d/home"; echo "$d"; }
install() { # install <dir> [env...]
  local d="$1"; shift
  env HOME="$d/home" AW_STATE_DIR="$d/state" CLAUDE_SETTINGS_FILE="$d/settings.json" CLAUDE_HOOKS_DIR="$d/hooks" "$@" \
    bash "$ROOT/scripts/install-judge-hooks.sh" --hook-only > /dev/null
}

test_installs_a_timed_user_prompt_submit_hook_idempotently_and_keeps_foreign_hooks() {
  local d; d="$(fresh)"
  echo '{"hooks":{"UserPromptSubmit":[{"matcher":"*","hooks":[{"type":"command","command":"python3 /x/prism-route/on_prompt.py --v4","timeout":15}]}]}}' > "$d/settings.json"
  install "$d"; install "$d"
  [ -x "$d/hooks/prompt-sort.sh" ] || { echo "FAIL: hook script not copied/executable"; exit 1; }
  [ "$(jq '[.hooks.UserPromptSubmit[].hooks[] | select(.command | endswith("# aw:prompt-sort"))] | length' "$d/settings.json")" = "1" ] || { echo "FAIL: expected exactly one aw:prompt-sort entry"; exit 1; }
  [ "$(jq -r '.hooks.UserPromptSubmit[].hooks[] | select(.command | endswith("# aw:prompt-sort")) | .timeout' "$d/settings.json")" = "6" ] || { echo "FAIL: timeout must be 6s"; exit 1; }
  [ "$(jq -r '.hooks.UserPromptSubmit[0].hooks[0].command' "$d/settings.json")" = "python3 /x/prism-route/on_prompt.py --v4" ] || { echo "FAIL: the foreign Prism hook must stay first and untouched"; exit 1; }
  jq -e '.hooks.SessionStart[].hooks[] | select(.command | endswith("# aw:judge-health"))' "$d/settings.json" > /dev/null || { echo "FAIL: judge-health must still be installed"; exit 1; }
  echo "PASS: test_installs_a_timed_user_prompt_submit_hook_idempotently_and_keeps_foreign_hooks"
}

test_opt_out_env_skips_the_hook() {
  local d; d="$(fresh)"
  install "$d" AW_NO_PROMPT_SORT=1
  [ ! -e "$d/hooks/prompt-sort.sh" ] || { echo "FAIL: AW_NO_PROMPT_SORT=1 must not copy the hook"; exit 1; }
  jq -e '.hooks.UserPromptSubmit' "$d/settings.json" > /dev/null 2>&1 && { echo "FAIL: AW_NO_PROMPT_SORT=1 must not write UserPromptSubmit"; exit 1; }
  echo "PASS: test_opt_out_env_skips_the_hook"
}

test_installs_a_timed_user_prompt_submit_hook_idempotently_and_keeps_foreign_hooks
test_opt_out_env_skips_the_hook
echo "All install-prompt-sort tests passed."
