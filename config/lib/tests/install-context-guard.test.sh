#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"
# The installer puts its files under $AW_STATE_DIR when set, else under the HOME each case passes (a temp dir): never an inherited state dir.
unset AW_STATE_DIR

test_creates_digests_dir_and_installs_hook_and_default_config() {
  local settings home
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh"
  [ -d "$home/.agentic-workflow/digests" ] || { echo "FAIL: digests dir not created"; exit 1; }
  [ -f "$home/.agentic-workflow/digests/README.md" ] || { echo "FAIL: README not installed"; exit 1; }
  [ -f "$home/.agentic-workflow/context-guard/config.json" ] || { echo "FAIL: default config not installed"; exit 1; }
  jq -e '.hooks.PostToolUse[].hooks[].command | select(contains("context-guard"))' "$settings" > /dev/null \
    || { echo "FAIL: hook not installed into settings.json"; exit 1; }
  echo "PASS: test_creates_digests_dir_and_installs_hook_and_default_config"
}

test_does_not_overwrite_an_existing_config_user_already_tuned() {
  local settings home
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  mkdir -p "$home/.agentic-workflow/context-guard"
  echo '{"thresholdTokens": 999999}' > "$home/.agentic-workflow/context-guard/config.json"
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh"
  [ "$(jq -r '.thresholdTokens' "$home/.agentic-workflow/context-guard/config.json")" = "999999" ] \
    || { echo "FAIL: install overwrote the user's tuned threshold"; exit 1; }
  echo "PASS: test_does_not_overwrite_an_existing_config_user_already_tuned"
}

test_is_idempotent() {
  local settings home
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh"
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh"
  local count; count="$(jq '[.hooks.PostToolUse[].hooks[] | select(.command | contains("context-guard"))] | length' "$settings")"
  [ "$count" -eq 1 ] || { echo "FAIL: expected exactly one installed entry after re-running, got $count"; exit 1; }
  echo "PASS: test_is_idempotent"
}

test_uninstall_removes_hook_entry_and_context_guard_dir_keeps_nonempty_digests() {
  local settings home
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh"
  echo "a digest a fresh agent would want" > "$home/.agentic-workflow/digests/some-task.md"

  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh" --uninstall

  jq -e '.hooks.PostToolUse[]?.hooks[]? | select(.command | contains("context-guard"))' "$settings" > /dev/null \
    && { echo "FAIL: expected the hook entry to be removed"; exit 1; }
  [ -d "$home/.agentic-workflow/context-guard" ] && { echo "FAIL: expected context-guard/ to be removed"; exit 1; }
  [ -f "$home/.agentic-workflow/digests/some-task.md" ] || { echo "FAIL: expected a non-empty digests/ to be left alone"; exit 1; }
  echo "PASS: test_uninstall_removes_hook_entry_and_context_guard_dir_keeps_nonempty_digests"
}

test_uninstall_removes_empty_digests_dir_too() {
  local settings home
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh"

  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh" --uninstall

  [ -d "$home/.agentic-workflow/digests" ] && { echo "FAIL: expected an empty digests/ to be removed too"; exit 1; }
  echo "PASS: test_uninstall_removes_empty_digests_dir_too"
}

test_uninstall_is_safe_when_nothing_was_installed() {
  local settings home
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-context-guard.sh" --uninstall
  echo "PASS: test_uninstall_is_safe_when_nothing_was_installed"
}

test_creates_digests_dir_and_installs_hook_and_default_config
test_does_not_overwrite_an_existing_config_user_already_tuned
test_is_idempotent
test_uninstall_removes_hook_entry_and_context_guard_dir_keeps_nonempty_digests
test_uninstall_removes_empty_digests_dir_too
test_uninstall_is_safe_when_nothing_was_installed
echo "All install-context-guard tests passed."
