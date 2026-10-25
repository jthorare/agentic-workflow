#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"

fake_judge_bin_dir() {
  local bin_dir; bin_dir="$(mktemp -d)"
  printf '#!/usr/bin/env bash\nexit 0\n' > "$bin_dir/judge"
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

test_refuses_without_judge_on_path() {
  local settings home out rc
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  set +e
  out="$(CLAUDE_SETTINGS_FILE="$settings" HOME="$home" PATH="/usr/bin:/bin" bash "$ROOT/scripts/install-trait-check.sh" 2>&1)"
  rc=$?
  set -e
  [ "$rc" -ne 0 ] || { echo "FAIL: expected a nonzero exit when judge isn't installed: $out"; exit 1; }
  echo "$out" | has -i "judge" || { echo "FAIL: expected the refusal to mention judge, got: $out"; exit 1; }
  echo "$out" | has -F "./setup.sh" || { echo "FAIL: expected the refusal to point at ./setup.sh (the C# judge is installed by setup), got: $out"; exit 1; }
  jq -e '.hooks' "$settings" > /dev/null 2>&1 && { echo "FAIL: refusal must not install hook entries"; exit 1; }
  echo "PASS: test_refuses_without_judge_on_path"
}

test_claude_installs_edit_and_stop_entries_with_timeouts() {
  local settings home bin_dir hooks_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"; hooks_dir="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$hooks_dir" HOME="$home" bash "$ROOT/scripts/install-trait-check.sh" > /dev/null
  [ "$(jq -r '.hooks.PostToolUse[] | select(.hooks[0].command | contains("aw:trait-check-edit")) | .matcher' "$settings")" = "Edit|Write|MultiEdit|NotebookEdit" ] \
    || { echo "FAIL: expected a PostToolUse edit entry matching the edit tools"; exit 1; }
  [ "$(jq -r '.hooks.PostToolUse[].hooks[] | select(.command | contains("aw:trait-check-edit")) | .timeout' "$settings")" -gt 15 ] \
    || { echo "FAIL: edit timeout must exceed judge's 15s edit budget"; exit 1; }
  [ "$(jq -r '.hooks.Stop[].hooks[] | select(.command | contains("aw:trait-check-stop")) | .timeout' "$settings")" -gt 40 ] \
    || { echo "FAIL: stop timeout must exceed judge's 40s stop budget"; exit 1; }
  [ -x "$hooks_dir/trait-check.sh" ] && [ -f "$hooks_dir/lib/failopen.sh" ] \
    || { echo "FAIL: expected the hook and its failopen lib in the hooks dir"; exit 1; }
  echo "PASS: test_claude_installs_edit_and_stop_entries_with_timeouts"
}

test_codex_and_cursor_install_through_their_adapters() {
  local home bin_dir codex cursor
  home="$(mktemp -d)"; bin_dir="$(fake_judge_bin_dir)"
  codex="$home/codex-hooks.json"; cursor="$home/cursor-hooks.json"
  PATH="$bin_dir:$PATH" HOME="$home" AW_STATE_DIR="$home/aw" CODEX_HOOKS_FILE="$codex" bash "$ROOT/scripts/install-trait-check.sh" --provider codex > /dev/null
  PATH="$bin_dir:$PATH" HOME="$home" AW_STATE_DIR="$home/aw" CURSOR_HOOKS_FILE="$cursor" bash "$ROOT/scripts/install-trait-check.sh" --provider cursor > /dev/null
  jq -e '.hooks.PostToolUse[] | select(.matcher == "^(apply_patch|Edit|Write)$") | .hooks[0].command | (contains("adapters/codex.sh") and contains("trait-check.sh"))' "$codex" > /dev/null \
    || { echo "FAIL: expected a codex PostToolUse entry through the codex adapter"; exit 1; }
  jq -e '.hooks.Stop[].hooks[0].command | contains("aw:trait-check-stop")' "$codex" > /dev/null \
    || { echo "FAIL: expected a codex Stop entry"; exit 1; }
  jq -e '.hooks.postToolUse[] | select(.command | contains("adapters/cursor.sh")) | has("matcher") | not' "$cursor" > /dev/null \
    || { echo "FAIL: expected a matcher-less cursor postToolUse entry through the cursor adapter"; exit 1; }
  jq -e '.hooks.stop[] | select(.command | contains("aw:trait-check-stop")) | .timeout == 45' "$cursor" > /dev/null \
    || { echo "FAIL: expected a cursor stop entry with the stop timeout"; exit 1; }
  [ -x "$home/aw/hooks/trait-check.sh" ] || { echo "FAIL: expected the hook staged for codex/cursor"; exit 1; }
  HOME="$home" CODEX_HOOKS_FILE="$codex" bash "$ROOT/scripts/install-trait-check.sh" --provider codex --uninstall > /dev/null
  HOME="$home" CURSOR_HOOKS_FILE="$cursor" bash "$ROOT/scripts/install-trait-check.sh" --provider cursor --uninstall > /dev/null
  jq -r '.. | strings' "$codex" "$cursor" | has "trait-check" && { echo "FAIL: uninstall left trait-check entries"; exit 1; }
  echo "PASS: test_codex_and_cursor_install_through_their_adapters"
}

test_uninstall_restores_settings_byte_for_byte() {
  local settings home bin_dir before after
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"/x/done-gate.sh # aw:done-gate"}]}]}}' > "$settings"
  before="$(jq -S . "$settings")"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" CLAUDE_HOOKS_DIR="$(mktemp -d)" HOME="$home" bash "$ROOT/scripts/install-trait-check.sh" > /dev/null
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-trait-check.sh" --uninstall > /dev/null
  after="$(jq -S . "$settings")"
  [ "$before" = "$after" ] || { echo "FAIL: expected settings restored after uninstall"; echo "before: $before"; echo "after: $after"; exit 1; }
  echo "PASS: test_uninstall_restores_settings_byte_for_byte"
}

test_refuses_without_judge_on_path
test_claude_installs_edit_and_stop_entries_with_timeouts
test_codex_and_cursor_install_through_their_adapters
test_uninstall_restores_settings_byte_for_byte
