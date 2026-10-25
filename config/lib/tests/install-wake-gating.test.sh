#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"
# The installer puts its files under $AW_STATE_DIR when set, else under the HOME each case passes (a temp dir): never an inherited state dir.
unset AW_STATE_DIR

fake_judge_bin_dir() {
  local bin_dir; bin_dir="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
echo '{"status":"ok"}'
EOF
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

test_install_judge_no_longer_touches_pretooluse_or_teammateidle() {
  local settings home
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-judge-hooks.sh" > /dev/null
  jq -e '.hooks.PreToolUse' "$settings" > /dev/null 2>&1 \
    && { echo "FAIL: install-judge-hooks.sh must not touch PreToolUse"; exit 1; }
  jq -e '.hooks.TeammateIdle' "$settings" > /dev/null 2>&1 \
    && { echo "FAIL: install-judge-hooks.sh must not touch TeammateIdle"; exit 1; }
  echo "PASS: test_install_judge_no_longer_touches_pretooluse_or_teammateidle"
}

test_refuses_without_judge_on_path() {
  local settings home out rc
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  set +e
  out="$(CLAUDE_SETTINGS_FILE="$settings" HOME="$home" PATH="/usr/bin:/bin" bash "$ROOT/scripts/install-wake-gating.sh" 2>&1)"
  rc=$?
  set -e
  [ "$rc" -ne 0 ] || { echo "FAIL: expected a nonzero exit when judge isn't installed, got 0: $out"; exit 1; }
  echo "$out" | has -i "judge" || { echo "FAIL: expected the refusal message to mention judge, got: $out"; exit 1; }
  jq -e '.hooks.PreToolUse' "$settings" > /dev/null 2>&1 \
    && { echo "FAIL: refusal must not install any hook entries"; exit 1; }
  echo "PASS: test_refuses_without_judge_on_path"
}

test_installs_exactly_its_three_entries() {
  local settings home bin_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null

  jq -e '.hooks.PreToolUse[]?.hooks[]? | select(.command | contains("send-gate"))' "$settings" > /dev/null \
    || { echo "FAIL: expected send-gate installed"; exit 1; }
  jq -e '.hooks.PreToolUse[]?.hooks[]? | select(.command | contains("record-teammate-name"))' "$settings" > /dev/null \
    || { echo "FAIL: expected record-teammate-name installed"; exit 1; }
  jq -e '.hooks.TeammateIdle[]?.hooks[]? | select(.command | contains("outbox-flush"))' "$settings" > /dev/null \
    || { echo "FAIL: expected outbox-flush installed"; exit 1; }

  local pre_count; pre_count="$(jq '[.hooks.PreToolUse[]?.hooks[]? | select(.command | contains("send-gate") or contains("record-teammate-name"))] | length' "$settings")"
  [ "$pre_count" -eq 2 ] || { echo "FAIL: expected exactly 2 PreToolUse entries installed, got $pre_count"; exit 1; }
  local idle_count; idle_count="$(jq '[.hooks.TeammateIdle[]?.hooks[]? | select(.command | contains("outbox-flush"))] | length' "$settings")"
  [ "$idle_count" -eq 1 ] || { echo "FAIL: expected exactly 1 TeammateIdle entry installed, got $idle_count"; exit 1; }
  echo "PASS: test_installs_exactly_its_three_entries"
}

test_is_idempotent() {
  local settings home bin_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null
  local count; count="$(jq '[.hooks.PreToolUse[]?.hooks[]? | select(.command | contains("send-gate") or contains("record-teammate-name"))] | length' "$settings")"
  [ "$count" -eq 2 ] || { echo "FAIL: expected exactly 2 PreToolUse entries after re-running, got $count"; exit 1; }
  local idle_count; idle_count="$(jq '[.hooks.TeammateIdle[]?.hooks[]? | select(.command | contains("outbox-flush"))] | length' "$settings")"
  [ "$idle_count" -eq 1 ] || { echo "FAIL: expected exactly 1 TeammateIdle entry after re-running, got $idle_count"; exit 1; }
  echo "PASS: test_is_idempotent"
}

test_uninstall_restores_settings_byte_for_byte() {
  local settings home bin_dir before after
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{"hooks":{"SessionStart":[{"hooks":[{"type":"command","command":"/some/other/hook.sh # aw:judge-health"}]}]}}' > "$settings"
  before="$(jq -S . "$settings")"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null

  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" --uninstall > /dev/null
  after="$(jq -S . "$settings")"
  [ "$before" = "$after" ] || { echo "FAIL: expected settings.json restored byte for byte (jq -S), got:
before: $before
after:  $after"; exit 1; }
  echo "PASS: test_uninstall_restores_settings_byte_for_byte"
}

test_uninstall_removes_empty_state_dirs_but_keeps_queued_items() {
  local settings home bin_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null
  mkdir -p "$home/.agentic-workflow/judge/outbox"
  echo '{"ts":"2026-09-27T00:00:00Z","agentType":"builder-a","text":"still working"}' > "$home/.agentic-workflow/judge/outbox/s1.jsonl"

  local out
  out="$(PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" --uninstall)"
  echo "$out" | has -i "queued" || { echo "FAIL: expected a warning about queued items, got: $out"; exit 1; }
  [ -f "$home/.agentic-workflow/judge/outbox/s1.jsonl" ] || { echo "FAIL: expected the queued outbox file to be kept"; exit 1; }
  echo "PASS: test_uninstall_removes_empty_state_dirs_but_keeps_queued_items"
}

test_uninstall_removes_empty_outbox_and_teammate_names() {
  local settings home bin_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null
  mkdir -p "$home/.agentic-workflow/judge"
  echo '{"ts":"x","sessionId":"s1","name":"builder-a"}' > "$home/.agentic-workflow/judge/teammate-names.jsonl"

  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" --uninstall > /dev/null
  [ -d "$home/.agentic-workflow/judge/outbox" ] && { echo "FAIL: expected the empty outbox dir to be removed"; exit 1; }
  [ -f "$home/.agentic-workflow/judge/teammate-names.jsonl" ] && { echo "FAIL: expected teammate-names.jsonl to be removed"; exit 1; }
  echo "PASS: test_uninstall_removes_empty_outbox_and_teammate_names"
}

test_installs_lib_locks_alongside_send_gate() {
  local settings home bin_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null
  [ -f "$home/.claude/hooks/lib/locks.sh" ] \
    || { echo "FAIL: expected lib/locks.sh installed alongside send-gate.sh"; exit 1; }
  diff "$home/.claude/hooks/lib/locks.sh" "$ROOT/config/lib/locks.sh" > /dev/null \
    || { echo "FAIL: installed lib/locks.sh doesn't match the repo's"; exit 1; }
  echo "PASS: test_installs_lib_locks_alongside_send_gate"
}

test_uninstall_removes_lib_locks_keeps_shared_failopen() {
  local settings home bin_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" --uninstall > /dev/null
  [ -f "$home/.claude/hooks/lib/locks.sh" ] && { echo "FAIL: expected lib/locks.sh to be removed"; exit 1; }
  # lib/failopen.sh is shared by every fail-open lever hook (scope-gate,
  # done-gate, …), so uninstalling wake gating leaves it and the lib dir.
  [ "$(ls -A "$home/.claude/hooks/lib")" = "failopen.sh" ] \
    || { echo "FAIL: expected hooks/lib to hold only the shared failopen.sh, got: $(ls -A "$home/.claude/hooks/lib")"; exit 1; }
  echo "PASS: test_uninstall_removes_lib_locks_keeps_shared_failopen"
}

test_uninstall_leaves_lib_dir_if_something_else_lives_there() {
  local settings home bin_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" > /dev/null
  echo "not ours" > "$home/.claude/hooks/lib/other-file.txt"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-wake-gating.sh" --uninstall > /dev/null
  [ -f "$home/.claude/hooks/lib/locks.sh" ] && { echo "FAIL: expected lib/locks.sh to be removed even though the dir is kept"; exit 1; }
  [ -f "$home/.claude/hooks/lib/other-file.txt" ] || { echo "FAIL: expected the unrelated file to survive"; exit 1; }
  echo "PASS: test_uninstall_leaves_lib_dir_if_something_else_lives_there"
}

test_install_judge_no_longer_touches_pretooluse_or_teammateidle
test_refuses_without_judge_on_path
test_installs_exactly_its_three_entries
test_is_idempotent
test_uninstall_restores_settings_byte_for_byte
test_installs_lib_locks_alongside_send_gate
test_uninstall_removes_lib_locks_keeps_shared_failopen
test_uninstall_leaves_lib_dir_if_something_else_lives_there
test_uninstall_removes_empty_state_dirs_but_keeps_queued_items
test_uninstall_removes_empty_outbox_and_teammate_names
echo "All install-wake-gating tests passed."
