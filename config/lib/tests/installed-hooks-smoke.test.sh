#!/usr/bin/env bash
# General regression guard for the class of bug found 2026-09-27: an
# installer that copies a hook script without one of its dependencies (here,
# send-gate.sh's config/lib/locks.sh), so the INSTALLED copy silently breaks
# in a way that running the hook straight out of the repo checkout never
# reveals (the repo checkout always has every sibling file in place).
#
# For every installer, install into fresh temp dirs, then run each installed
# hook copy — from the temp HOOKS_DIR, never the repo — with realistic
# sample stdin, and assert it doesn't choke on a missing dependency.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../../.." && pwd)"

fake_judge_bin() {
  local bin_dir="$1"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
if [ "$1" = "health" ]; then echo '{"status":"ok","failures24h":0}'; exit 0; fi
if [ "$1" = "wake-gate" ]; then
  cat >/dev/null  # drain stdin first, like the real judge (src/Judge/Aw.Judge.Host/Commands/CommandOutput.cs): else the hook's printf can die of SIGPIPE (141) and fail open
  N=0
  [ -n "${WAKE_GATE_COUNT_FILE:-}" ] && N="$(cat "$WAKE_GATE_COUNT_FILE" 2>/dev/null || echo 0)"
  N=$((N + 1))
  [ -n "${WAKE_GATE_COUNT_FILE:-}" ] && echo "$N" > "$WAKE_GATE_COUNT_FILE"
  if [ "$N" -eq 1 ]; then echo '{"decision":"batch"}'; else echo '{"decision":"send"}'; fi
  exit 0
fi
if [ "$1" = "ask-check" ]; then cat >/dev/null; echo '{"decision":"ask"}'; exit 2; fi
if [ "$1" = "brief" ] && [ "$2" = "get" ]; then exit 1; fi
echo '{"decision":"fine","confidence":1,"reason_code":"stub"}'
EOF
  chmod +x "$bin_dir/judge"
}

# Installs one gate with the FULL sandbox envelope (every override the
# 2026-09-27 fixes exercise) into fresh temp dirs, and returns them via the
# named variables the caller passed (bash 4 nameref-free style: caller reads
# globals SETTINGS/HOOKS_DIR/STATE_DIR/BIN_DIR after calling).
install_full_envelope() {
  local installer="$1"
  SETTINGS="$(mktemp -d)/settings.json"
  HOOKS_DIR="$(mktemp -d)/hooks"
  STATE_DIR="$(mktemp -d)/state"
  BIN_DIR="$(mktemp -d)/bin"
  LAUNCH_AGENTS="$(mktemp -d)/LaunchAgents"
  mkdir -p "$BIN_DIR"
  echo '{}' > "$SETTINGS"
  fake_judge_bin "$BIN_DIR"
  CLAUDE_SETTINGS_FILE="$SETTINGS" CLAUDE_HOOKS_DIR="$HOOKS_DIR" AW_STATE_DIR="$STATE_DIR" \
    CLAUDE_LOCAL_BIN="$BIN_DIR" AW_LAUNCH_AGENTS_DIR="$LAUNCH_AGENTS" AW_SKIP_LAUNCHD=1 \
    PATH="$BIN_DIR:$PATH" AW_JUDGE_BIN="$BIN_DIR/judge" \
    bash "$ROOT/scripts/$installer.sh" > /dev/null 2>&1
}

# Runs $1 (an installed hook path, NOT a repo path) with stdin $2, asserting
# exit 0 and no dependency-resolution error on stderr.
assert_installed_hook_runs_clean() {
  local label="$1" hook_path="$2" stdin_json="$3" state_dir="$4" bin_dir="$5"
  [ -f "$hook_path" ] || { echo "FAIL: $label — installed hook missing at $hook_path"; exit 1; }
  local out err rc
  err="$(mktemp)"
  set +e
  out="$(printf '%s' "$stdin_json" | AW_STATE_DIR="$state_dir" PATH="$bin_dir:$PATH" bash "$hook_path" 2>"$err")"
  rc=$?
  set -e
  local errtext; errtext="$(cat "$err")"
  [ "$rc" -eq 0 ] || { echo "FAIL: $label — exit $rc, stderr: $errtext"; exit 1; }
  if printf '%s' "$errtext" | has -iE 'no such file|command not found'; then
    echo "FAIL: $label — installed copy hit a missing dependency: $errtext"
    exit 1
  fi
  echo "PASS: $label (exit 0, no dependency errors) — stdout: ${out:0:120}"
}

test_judge_health_hook_installed_runs_clean() {
  install_full_envelope install-judge-hooks
  assert_installed_hook_runs_clean "judge-health.sh" "$HOOKS_DIR/judge-health.sh" '{}' "$STATE_DIR" "$BIN_DIR"
}

test_context_guard_hook_installed_runs_clean() {
  install_full_envelope install-context-guard
  local transcript; transcript="$(mktemp)"
  echo '{"type":"assistant","message":{"usage":{"input_tokens":10,"cache_read_input_tokens":10}}}' > "$transcript"
  assert_installed_hook_runs_clean "context-guard.sh" "$HOOKS_DIR/context-guard.sh" \
    "$(jq -nc --arg t "$transcript" '{session_id:"s1", transcript_path:$t}')" "$STATE_DIR" "$BIN_DIR"
}

test_scope_gate_hook_installed_runs_clean() {
  install_full_envelope install-scope-gate
  assert_installed_hook_runs_clean "scope-gate.sh" "$HOOKS_DIR/scope-gate.sh" \
    '{"tool_input":{"subagent_type":"Explore","prompt":"look"}}' "$STATE_DIR" "$BIN_DIR"
  assert_installed_hook_runs_clean "subagent-start-map.sh" "$HOOKS_DIR/subagent-start-map.sh" '{}' "$STATE_DIR" "$BIN_DIR"
}

test_done_gate_hook_installed_runs_clean() {
  install_full_envelope install-done-gate
  assert_installed_hook_runs_clean "done-gate.sh" "$HOOKS_DIR/done-gate.sh" \
    '{"stop_hook_active":true,"transcript_path":"/nonexistent"}' "$STATE_DIR" "$BIN_DIR"
  assert_installed_hook_runs_clean "done-gate-annotate.sh" "$HOOKS_DIR/done-gate-annotate.sh" '{}' "$STATE_DIR" "$BIN_DIR"
}

test_external_write_guard_hook_installed_runs_clean() {
  install_full_envelope install-external-write-guard
  assert_installed_hook_runs_clean "external-write-guard.sh" "$HOOKS_DIR/external-write-guard.sh" \
    '{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"echo hi"}}' "$STATE_DIR" "$BIN_DIR"
  assert_installed_hook_runs_clean "turn-origin.sh" "$HOOKS_DIR/turn-origin.sh" '{"session_id":"s1"}' "$STATE_DIR" "$BIN_DIR"
}

# The specific regression: run the INSTALLED send-gate.sh (not the repo's)
# end to end — a batch message must queue an outbox item, then a send
# message must emit hookSpecificOutput.updatedInput carrying the queued
# text, and the outbox must be empty afterwards. Before the fix
# (scripts/install-wake-gating.sh not installing config/lib/locks.sh
# alongside send-gate.sh), this test fails: no updatedInput is ever emitted
# and the outbox never gets written to in the first place.
test_installed_send_gate_batches_then_attaches_the_digest() {
  install_full_envelope install-wake-gating
  assert_installed_hook_runs_clean "record-teammate-name.sh" "$HOOKS_DIR/record-teammate-name.sh" '{}' "$STATE_DIR" "$BIN_DIR"
  assert_installed_hook_runs_clean "outbox-flush.sh" "$HOOKS_DIR/outbox-flush.sh" '{"session_id":"s1"}' "$STATE_DIR" "$BIN_DIR"

  [ -f "$HOOKS_DIR/lib/locks.sh" ] || { echo "FAIL: expected $HOOKS_DIR/lib/locks.sh to be installed alongside send-gate.sh"; exit 1; }

  local count_file; count_file="$(mktemp -d)/count"
  local sid="smoke-session-1"
  local out1 out2

  out1="$(WAKE_GATE_COUNT_FILE="$count_file" AW_STATE_DIR="$STATE_DIR" PATH="$BIN_DIR:$PATH" bash "$HOOKS_DIR/send-gate.sh" <<< \
    "$(jq -nc --arg s "$sid" '{session_id:$s, tool_input:{to:"main", message:"QUEUED-DIGEST-TEXT still working", type:"message"}}')")"
  echo "$out1" | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null \
    || { echo "FAIL: expected the first (batch) call to deny-and-queue, got: $out1"; exit 1; }
  [ -s "$STATE_DIR/judge/outbox/$sid.jsonl" ] \
    || { echo "FAIL: expected the batch call to queue an outbox item at $STATE_DIR/judge/outbox/$sid.jsonl"; exit 1; }

  out2="$(WAKE_GATE_COUNT_FILE="$count_file" AW_STATE_DIR="$STATE_DIR" PATH="$BIN_DIR:$PATH" bash "$HOOKS_DIR/send-gate.sh" <<< \
    "$(jq -nc --arg s "$sid" '{session_id:$s, tool_input:{to:"main", message:"final result text", type:"message"}}')")"
  echo "$out2" | jq -e '.hookSpecificOutput.updatedInput.message' > /dev/null \
    || { echo "FAIL: expected the second (send) call to emit updatedInput carrying the digest, got: $out2"; exit 1; }
  echo "$out2" | jq -r '.hookSpecificOutput.updatedInput.message' | has "QUEUED-DIGEST-TEXT" \
    || { echo "FAIL: updatedInput.message doesn't carry the queued text, got: $out2"; exit 1; }
  [ -s "$STATE_DIR/judge/outbox/$sid.jsonl" ] \
    && { echo "FAIL: expected the outbox to be emptied after the digest was attached"; exit 1; }

  echo "PASS: test_installed_send_gate_batches_then_attaches_the_digest"
}

test_send_gate_fails_open_loudly_when_locks_lib_is_missing() {
  local hooks_dir; hooks_dir="$(mktemp -d)"
  cp "$ROOT/config/hooks/send-gate.sh" "$hooks_dir/send-gate.sh"
  chmod +x "$hooks_dir/send-gate.sh"
  local out
  out="$(AW_STATE_DIR="$(mktemp -d)" bash "$hooks_dir/send-gate.sh" <<< '{"session_id":"s1","tool_input":{"to":"main","message":"hi","type":"message"}}')"
  echo "$out" | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null \
    || { echo "FAIL: expected fail-open allow when locks.sh is missing, got: $out"; exit 1; }
  echo "$out" | jq -e '.systemMessage | test("locks.sh")' > /dev/null \
    || { echo "FAIL: expected a loud systemMessage naming locks.sh, got: $out"; exit 1; }
  echo "PASS: test_send_gate_fails_open_loudly_when_locks_lib_is_missing"
}

# HOOK-FAILOPEN (spec §4c, B5): every installer that copies a fail-open hook
# also installs lib/failopen.sh, so the INSTALLED copy logs its fail-open
# events — not only a repo checkout, where ../lib/failopen.sh happens to exist.
test_installed_fail_open_hooks_log_hook_failopen() {
  local pair installer hook
  for pair in install-scope-gate:scope-gate install-done-gate:done-gate \
              install-external-write-guard:external-write-guard install-wake-gating:send-gate; do
    installer="${pair%%:*}"; hook="${pair#*:}"
    install_full_envelope "$installer"
    [ -f "$HOOKS_DIR/lib/failopen.sh" ] || { echo "FAIL: $installer did not install lib/failopen.sh"; exit 1; }
    printf 'not json' | AW_STATE_DIR="$STATE_DIR" PATH="$BIN_DIR:$PATH" bash "$HOOKS_DIR/$hook.sh" > /dev/null 2>&1 || true
    jq -e --arg h "$hook" 'select(.event == "HOOK-FAILOPEN" and .hook == $h and .cause == "input-unparsable")' \
      "$STATE_DIR/logs/failopen.log" > /dev/null 2>&1 \
      || { echo "FAIL: installed $hook.sh did not log HOOK-FAILOPEN to $STATE_DIR/logs/failopen.log"; exit 1; }
  done
  echo "PASS: test_installed_fail_open_hooks_log_hook_failopen"
}

test_judge_health_hook_installed_runs_clean
test_context_guard_hook_installed_runs_clean
test_scope_gate_hook_installed_runs_clean
test_done_gate_hook_installed_runs_clean
test_external_write_guard_hook_installed_runs_clean
test_installed_send_gate_batches_then_attaches_the_digest
test_send_gate_fails_open_loudly_when_locks_lib_is_missing
test_installed_fail_open_hooks_log_hook_failopen
echo "All installed-hooks-smoke tests passed."
