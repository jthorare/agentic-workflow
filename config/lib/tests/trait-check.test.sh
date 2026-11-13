#!/usr/bin/env bash
# Tests for config/hooks/trait-check.sh against a fake `judge`.
# Run: bash config/lib/tests/trait-check.test.sh
set -uo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null; rm -rf "$p"; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; }
trap __aw_tmp_clean EXIT
HOOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../hooks" && pwd)/trait-check.sh"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

STATE="$(mktemp -d)"
FAKE_BIN="$(mktemp -d)"
# The fake records its arguments one per line and exits with $FAKE_RC,
# printing $FAKE_ERR on stderr.
cat > "$FAKE_BIN/judge" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' "$@" > "$FAKE_ARGS"
[ -n "${FAKE_ERR:-}" ] && printf '%s\n' "$FAKE_ERR" >&2
exit "${FAKE_RC:-0}"
EOF
chmod +x "$FAKE_BIN/judge"
export FAKE_ARGS="$STATE/args" AW_STATE_DIR="$STATE"

run() { # run <stdin-json> -> sets OUT (stdout+stderr) and RC
  rm -f "$FAKE_ARGS"
  OUT="$(printf '%s' "$1" | PATH="$FAKE_BIN:$PATH" bash "$HOOK" 2>&1)"; RC=$?
}
args() { [ -f "$FAKE_ARGS" ] && tr '\n' ' ' < "$FAKE_ARGS" | sed 's/ $//'; }

run '{"hook_event_name":"PostToolUse","tool_name":"Edit","tool_input":{"file_path":"/r/src/a.ts"}}'
check "Claude Edit: passes the file in edit mode" "$(args)" "trait-check --edit --file /r/src/a.ts"
check "clean check: exits 0" "$RC" "0"

run '{"hook_event_name":"PostToolUse","tool_name":"apply_patch","tool_input":{"input":"*** Begin Patch\n*** Update File: src/a.ts\n@@\n-x\n+y\n*** Add File: src/b.ts\n+z\n*** Update File: src/a.ts\n*** Move to: src/c.ts\n*** End Patch"}}'
check "Codex apply_patch: every patched file, once each" "$(args)" "trait-check --edit --file src/a.ts --file src/b.ts --file src/c.ts"

run '{"hook_event_name":"PostToolUse","tool_name":"NotebookEdit","tool_input":{"notebook_path":"n.ipynb"}}'
check "NotebookEdit: notebook_path" "$(args)" "trait-check --edit --file n.ipynb"

run '{"hook_event_name":"PostToolUse","tool_name":"Read","tool_input":{"path":"/r/src/a.ts"}}'
check "a read is not an edit: judge not called" "$(args)" ""
check "a read is not an edit: exits 0" "$RC" "0"

run '{"hook_event_name":"PostToolUse","tool_name":"Write","tool_input":"not-an-object"}'
check "no paths in tool_input: judge not called" "$(args)" ""

FAKE_RC=2 FAKE_ERR="trait-check: your last edit likely breaks 1 project trait." run '{"hook_event_name":"PostToolUse","tool_name":"Write","tool_input":{"file_path":"a.ts"}}'
check "findings: exit 2" "$RC" "2"
check "findings: judge's message reaches the agent" "$OUT" "trait-check: your last edit likely breaks 1 project trait."

run '{"hook_event_name":"Stop","stop_hook_active":false}'
check "Stop: branch check" "$(args)" "trait-check --stop"

# --- the session id: edit records the repo it touched, Stop judges every repo the session edited in ---
run '{"hook_event_name":"PostToolUse","session_id":"s1","tool_name":"Edit","tool_input":{"file_path":"/b/src/a.ts"}}'
check "edit with a session id: passes --session" "$(args)" "trait-check --edit --file /b/src/a.ts --session s1"
run '{"hook_event_name":"Stop","session_id":"s1","stop_hook_active":false}'
check "Stop with a session id: passes --session" "$(args)" "trait-check --stop --session s1"
run '{"hook_event_name":"Stop","session_id":"","stop_hook_active":false}'
check "an empty session id: no --session" "$(args)" "trait-check --stop"

run '{"hook_event_name":"Stop","stop_hook_active":true}'
check "Stop re-entry: judge not called (no loop)" "$(args)" ""
check "Stop re-entry: exits 0" "$RC" "0"

run '{"hook_event_name":"SessionStart"}'
check "other events: ignored" "$(args)" ""

FAKE_RC=3 FAKE_ERR="trait-check: jev unavailable (no-api-key)" run '{"hook_event_name":"Stop"}'
check "jev unavailable: fails open silently" "$RC/$OUT" "0/"
check "jev unavailable: logged as HOOK-FAILOPEN" "$(jq -r 'select(.hook=="trait-check") | .cause' "$STATE/logs/failopen.log" | tail -1)" "jev-unavailable"

FAKE_RC=1 run '{"hook_event_name":"Stop"}'
check "judge error: fails open" "$RC" "0"
check "judge error: logged with its exit code" "$(jq -r 'select(.hook=="trait-check") | .cause' "$STATE/logs/failopen.log" | tail -1)" "judge-exit-1"

OUT="$(printf '%s' '{"hook_event_name":"Stop"}' | AW_JUDGE_BIN=/nonexistent/judge bash "$HOOK" 2>&1)"; RC=$?
check "judge missing: fails open" "$RC/$OUT" "0/"
check "judge missing: logged" "$(jq -r 'select(.hook=="trait-check") | .cause' "$STATE/logs/failopen.log" | tail -1)" "judge-missing"

OUT="$(printf 'not json' | PATH="$FAKE_BIN:$PATH" bash "$HOOK" 2>&1)"; RC=$?
check "unparsable input: fails open" "$RC/$OUT" "0/"

rm -f "$FAKE_ARGS"
# A here-string, not a pipe: the guard exits before reading stdin, so a piped printf could die of SIGPIPE.
OUT="$(AW_JUDGE_CHILD=1 PATH="$FAKE_BIN:$PATH" bash "$HOOK" 2>&1 <<<'{"hook_event_name":"Stop"}')"; RC=$?
check "AW_JUDGE_CHILD: recursion guard exits 0" "$RC" "0"
check "AW_JUDGE_CHILD: judge not called" "$(args)" ""


# --- judge images (spec 5.2): the hook adds nothing for an edited image; the judge reads the PNG --file as an image ---
# Green today and must stay so: argv for an edited PNG or JPEG is exactly what it is today, one --file each.
run '{"hook_event_name":"PostToolUse","tool_name":"Write","tool_input":{"file_path":"/r/src/logo.png"}}'
check "edited PNG: argv is byte-identical to today's" "$(args)" "trait-check --edit --file /r/src/logo.png"
run '{"hook_event_name":"PostToolUse","tool_name":"Edit","tool_input":{"file_path":"/r/src/photo.jpeg"}}'
check "edited JPEG: argv is byte-identical to today's" "$(args)" "trait-check --edit --file /r/src/photo.jpeg"

exit $fail
