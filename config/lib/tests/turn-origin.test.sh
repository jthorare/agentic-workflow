#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; return 0; }
trap __aw_tmp_clean EXIT
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/turn-origin.sh"

test_real_prompt_clears_auto_continued_at() {
  local sessions_dir
  sessions_dir="$(mktemp -d)"
  echo '{"auto_continued_at":"2026-09-27T00:00:00Z"}' > "$sessions_dir/s1.json"
  AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< '{"session_id":"s1","prompt":"please fix the failing test"}'
  jq -e '.auto_continued_at' "$sessions_dir/s1.json" > /dev/null 2>&1 \
    && { echo "FAIL: expected auto_continued_at to be cleared for a real prompt"; exit 1; }
  echo "PASS: test_real_prompt_clears_auto_continued_at"
}

test_teammate_message_prefix_does_not_clear_rf4() {
  local sessions_dir
  sessions_dir="$(mktemp -d)"
  echo '{"auto_continued_at":"2026-09-27T00:00:00Z"}' > "$sessions_dir/s1.json"
  AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< '{"session_id":"s1","prompt":"<teammate-message teammate_id=\"x\">hi</teammate-message>"}'
  jq -e '.auto_continued_at' "$sessions_dir/s1.json" > /dev/null 2>&1 \
    || { echo "FAIL: RF-4 — a teammate-message-wrapped prompt must not clear auto_continued_at"; exit 1; }
  echo "PASS: test_teammate_message_prefix_does_not_clear_rf4"
}

test_another_claude_session_prefix_does_not_clear_rf4() {
  local sessions_dir
  sessions_dir="$(mktemp -d)"
  echo '{"auto_continued_at":"2026-09-27T00:00:00Z"}' > "$sessions_dir/s1.json"
  AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< '{"session_id":"s1","prompt":"Another Claude session sent a message: done"}'
  jq -e '.auto_continued_at' "$sessions_dir/s1.json" > /dev/null 2>&1 \
    || { echo "FAIL: RF-4 — an 'Another Claude session' prefixed prompt must not clear auto_continued_at"; exit 1; }
  echo "PASS: test_another_claude_session_prefix_does_not_clear_rf4"
}

test_24h_sweep_deletes_old_session_files_keeps_fresh() {
  local sessions_dir
  sessions_dir="$(mktemp -d)"
  echo '{}' > "$sessions_dir/old.json"
  touch -d "2 days ago" "$sessions_dir/old.json" 2>/dev/null || touch -t "$(date -v-2d +%Y%m%d%H%M)" "$sessions_dir/old.json"
  echo '{}' > "$sessions_dir/fresh.json"
  AW_JUDGE_SESSIONS_DIR="$sessions_dir" bash "$HOOK" <<< '{"session_id":"s2","prompt":"hi"}'
  [ ! -f "$sessions_dir/old.json" ] || { echo "FAIL: expected old.json (>24h) to be swept"; exit 1; }
  [ -f "$sessions_dir/fresh.json" ] || { echo "FAIL: expected fresh.json to be kept"; exit 1; }
  echo "PASS: test_24h_sweep_deletes_old_session_files_keeps_fresh"
}

test_real_prompt_clears_auto_continued_at
test_teammate_message_prefix_does_not_clear_rf4
test_another_claude_session_prefix_does_not_clear_rf4
test_24h_sweep_deletes_old_session_files_keeps_fresh
echo "All turn-origin tests passed."
