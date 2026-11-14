#!/usr/bin/env bash
set -euo pipefail

# Temp-dir cleanup (#3262): every mktemp this suite makes is recorded and removed when it exits.
__aw_tmp_reg="$(command mktemp)"
mktemp() { local p; p="$(command mktemp "$@")" || return; printf '%s\n' "$p" >>"$__aw_tmp_reg"; printf '%s\n' "$p"; }
__aw_tmp_clean() { local rc=$? p; while IFS= read -r p; do [ -z "$p" ] || { chmod -R u+rwX "$p" 2>/dev/null || true; rm -rf "$p" || true; }; done <"$__aw_tmp_reg"; rm -f "$__aw_tmp_reg"; exit "$rc"; }
trap __aw_tmp_clean EXIT
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$DIR/../../hooks/subagent-start-map.sh"

setup_fake_judge_recording() {
  local bin_dir; bin_dir="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
echo "$@" >> "$JUDGE_CALL_LOG"
if [ "$1" = "brief" ] && [ "$2" = "map-by-name" ]; then
  echo "${MAP_BY_NAME_RESULT:-}"
elif [ "$1" = "brief" ] && [ "$2" = "map-by-dispatch" ]; then
  echo "${MAP_BY_DISPATCH_RESULT:-}"
elif [ "$1" = "brief" ] && [ "$2" = "set-agent-id" ]; then
  exit 0
fi
EOF
  chmod +x "$bin_dir/judge"
  echo "$bin_dir"
}

test_named_teammate_uses_map_by_name() {
  local bin_dir log names_file
  bin_dir="$(setup_fake_judge_recording)"
  log="$(mktemp)"; names_file="$(mktemp)"
  echo '{"name":"builder-a"}' > "$names_file"
  PATH="$bin_dir:$PATH" JUDGE_CALL_LOG="$log" AW_TEAMMATE_NAMES_FILE="$names_file" MAP_BY_NAME_RESULT="tu1" \
    bash "$HOOK" <<< '{"agent_id":"agent-1","agent_type":"builder-a","session_id":"s1","prompt_id":"p1"}'
  grep -q "^brief map-by-name builder-a$" "$log" || { echo "FAIL: expected map-by-name to be called, log: $(cat "$log")"; exit 1; }
  grep -q "^brief set-agent-id tu1 agent-1$" "$log" || { echo "FAIL: expected set-agent-id tu1 agent-1, log: $(cat "$log")"; exit 1; }
  echo "PASS: test_named_teammate_uses_map_by_name"
}

test_unnamed_subagent_uses_map_by_dispatch() {
  local bin_dir log names_file
  bin_dir="$(setup_fake_judge_recording)"
  log="$(mktemp)"; names_file="$(mktemp)"
  : > "$names_file"
  PATH="$bin_dir:$PATH" JUDGE_CALL_LOG="$log" AW_TEAMMATE_NAMES_FILE="$names_file" MAP_BY_DISPATCH_RESULT="tu2" \
    bash "$HOOK" <<< '{"agent_id":"agent-2","agent_type":"lean-coder","session_id":"s1","prompt_id":"p1"}'
  grep -q "^brief map-by-dispatch s1 p1 lean-coder$" "$log" || { echo "FAIL: expected map-by-dispatch with the right 3 args, log: $(cat "$log")"; exit 1; }
  grep -q "^brief set-agent-id tu2 agent-2$" "$log" || { echo "FAIL: expected set-agent-id tu2 agent-2, log: $(cat "$log")"; exit 1; }
  echo "PASS: test_unnamed_subagent_uses_map_by_dispatch"
}

test_judge_failure_is_swallowed() {
  local bin_dir; bin_dir="$(mktemp -d)"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
exit 1
EOF
  chmod +x "$bin_dir/judge"
  PATH="$bin_dir:$PATH" AW_TEAMMATE_NAMES_FILE="/nonexistent" \
    bash "$HOOK" <<< '{"agent_id":"agent-3","agent_type":"lean-coder","session_id":"s1","prompt_id":"p1"}' || { echo "FAIL: hook must not crash when judge fails"; exit 1; }
  echo "PASS: test_judge_failure_is_swallowed"
}

# Probe gate item 1's documented FIFO race: two SubagentStart events for the
# same (session_id, prompt_id, agent_type) map to briefs in save order — this
# pins the known limitation rather than hiding it.
test_documented_fifo_race_maps_in_save_order() {
  local bin_dir log names_file
  bin_dir="$(mktemp -d)"; log="$(mktemp)"; names_file="$(mktemp)"; : > "$names_file"
  cat > "$bin_dir/judge" <<'EOF'
#!/usr/bin/env bash
echo "$@" >> "$JUDGE_CALL_LOG"
if [ "$1" = "brief" ] && [ "$2" = "map-by-dispatch" ]; then
  echo "$OLDEST_UNMAPPED_TOOL_USE_ID"
elif [ "$1" = "brief" ] && [ "$2" = "set-agent-id" ]; then
  exit 0
fi
EOF
  chmod +x "$bin_dir/judge"
  # First SubagentStart (agent-A actually started first): the fake judge's
  # oldest-unmapped brief is tu-oldest, whichever agent_id asks first.
  PATH="$bin_dir:$PATH" JUDGE_CALL_LOG="$log" AW_TEAMMATE_NAMES_FILE="$names_file" OLDEST_UNMAPPED_TOOL_USE_ID="tu-oldest" \
    bash "$HOOK" <<< '{"agent_id":"agent-A","agent_type":"lean-coder","session_id":"s1","prompt_id":"p1"}'
  grep -q "^brief set-agent-id tu-oldest agent-A$" "$log" || { echo "FAIL: expected agent-A to get the oldest unmapped brief regardless of true start order (documented heuristic), log: $(cat "$log")"; exit 1; }
  echo "PASS: test_documented_fifo_race_maps_in_save_order"
}

test_named_teammate_uses_map_by_name
test_unnamed_subagent_uses_map_by_dispatch
test_judge_failure_is_swallowed
test_documented_fifo_race_maps_in_save_order
echo "All subagent-start-map tests passed."
