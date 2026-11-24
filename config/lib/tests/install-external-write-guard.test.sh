#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"

fake_judge_bin_dir() {
  local bin_dir; bin_dir="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
echo '{"status":"ok"}'
EOF
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

test_refuses_without_judge_on_path() {
  local settings home out rc
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  set +e
  out="$(CLAUDE_SETTINGS_FILE="$settings" HOME="$home" PATH="/usr/bin:/bin" bash "$ROOT/scripts/install-external-write-guard.sh" 2>&1)"
  rc=$?
  set -e
  [ "$rc" -ne 0 ] || { echo "FAIL: expected a nonzero exit when judge isn't installed, got 0: $out"; exit 1; }
  echo "$out" | has -i "judge" || { echo "FAIL: expected the refusal message to mention judge, got: $out"; exit 1; }
  jq -e '.hooks.PreToolUse' "$settings" > /dev/null 2>&1 \
    && { echo "FAIL: refusal must not install any hook entries"; exit 1; }
  echo "PASS: test_refuses_without_judge_on_path"
}

test_installs_both_entries() {
  local settings home bin_dir
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{}' > "$settings"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-external-write-guard.sh" > /dev/null

  jq -e '.hooks.PreToolUse[]?.hooks[]? | select(.command | contains("external-write-guard"))' "$settings" > /dev/null \
    || { echo "FAIL: expected a PreToolUse external-write-guard entry"; exit 1; }
  jq -e '.hooks.UserPromptSubmit[]?.hooks[]? | select(.command | contains("turn-origin"))' "$settings" > /dev/null \
    || { echo "FAIL: expected a UserPromptSubmit turn-origin entry"; exit 1; }
  echo "PASS: test_installs_both_entries"
}

test_uninstall_restores_settings_byte_for_byte() {
  local settings home bin_dir before after
  settings="$(mktemp -d)/settings.json"; home="$(mktemp -d)"
  echo '{"hooks":{"SessionStart":[{"hooks":[{"type":"command","command":"/some/other/hook.sh # aw:judge-health"}]}]}}' > "$settings"
  before="$(jq -S . "$settings")"
  bin_dir="$(fake_judge_bin_dir)"
  PATH="$bin_dir:$PATH" CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-external-write-guard.sh" > /dev/null
  CLAUDE_SETTINGS_FILE="$settings" HOME="$home" bash "$ROOT/scripts/install-external-write-guard.sh" --uninstall > /dev/null
  after="$(jq -S . "$settings")"
  [ "$before" = "$after" ] || { echo "FAIL: expected settings to be byte-for-byte restored after uninstall (jq -S compared)"; echo "before: $before"; echo "after: $after"; exit 1; }
  echo "PASS: test_uninstall_restores_settings_byte_for_byte"
}

test_refuses_without_judge_on_path
test_installs_both_entries
test_uninstall_restores_settings_byte_for_byte
echo "All install-external-write-guard tests passed."
