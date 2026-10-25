#!/usr/bin/env bash
# Tests for providers/lib.sh + providers/*/install.sh (installer plumbing).
# Runs entirely inside a throwaway $HOME; never touches the real one.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib/test-grep.sh"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$DIR/../.." && pwd)"

fail() { echo "FAIL: $*"; exit 1; }

setup_env() {
  SANDBOX="$(mktemp -d)"
  export HOME="$SANDBOX/home"
  mkdir -p "$HOME"
  export TOOLKIT_DIR="$ROOT"
  export AW_DRY_RUN=0
  unset AW_STATE_ROOT AW_STATE_DIR CODEX_HOME CLAUDE_DIR CURSOR_DIR
  # shellcheck source=../lib.sh
  source "$ROOT/providers/lib.sh"
  source "$ROOT/config/lib/install-agents.sh"
  source "$ROOT/providers/claude/install.sh"
  source "$ROOT/providers/codex/install.sh"
  source "$ROOT/providers/cursor/install.sh"
  MANAGED_SKILLS=(review cso)
  DEPRECATED_SKILLS=(bolder)
  AW_EXTERNAL_SKILL_DIRS=""
}
teardown_env() { rm -rf "$SANDBOX"; }

test_links_managed_and_bootstrap_then_is_idempotent() {
  setup_env
  local d="$HOME/.claude/skills" out
  aw_install_skills_into "$d" </dev/null >/dev/null
  [ "$(readlink "$d/review")" = "$ROOT/skills/review" ] || fail "review not linked"
  [ "$(readlink "$d/bootstrap")" = "$ROOT/bootstrap" ] || fail "bootstrap not linked"
  out="$(aw_install_skills_into "$d" </dev/null)"
  echo "$out" | has "review: up to date" || fail "second run should be up to date"
  teardown_env
  echo "PASS: test_links_managed_and_bootstrap_then_is_idempotent"
}

test_refreshes_legacy_link_and_removes_deprecated() {
  setup_env
  local d="$HOME/.codex/skills"
  mkdir -p "$d/bolder"
  ln -s "/old/place/agentic-workflow/skills/review" "$d/review"
  aw_install_skills_into "$d" </dev/null >/dev/null
  [ "$(readlink "$d/review")" = "$ROOT/skills/review" ] || fail "legacy link not refreshed"
  [ ! -e "$d/bolder" ] || fail "deprecated skill not removed"
  teardown_env
  echo "PASS: test_refreshes_legacy_link_and_removes_deprecated"
}

test_foreign_collision_is_kept_when_stdin_closed() {
  setup_env
  local d="$HOME/.cursor/skills"
  mkdir -p "$d" "$SANDBOX/other/cso"
  ln -s "$SANDBOX/other/cso" "$d/cso"
  aw_install_skills_into "$d" </dev/null >/dev/null || fail "collision prompt aborted the install"
  [ "$(readlink "$d/cso")" = "$SANDBOX/other/cso" ] || fail "foreign symlink was replaced without consent"
  teardown_env
  echo "PASS: test_foreign_collision_is_kept_when_stdin_closed"
}

test_stale_cleanup_only_removes_our_links() {
  setup_env
  local d="$HOME/.claude/skills"
  mkdir -p "$d" "$SANDBOX/foreign/x"
  ln -s "$ROOT/skills/retired-skill" "$d/retired-skill"
  ln -s "$SANDBOX/foreign/x" "$d/foreign-skill"
  AW_ASSUME_YES=1 aw_cleanup_stale_skills "$d" >/dev/null
  [ ! -L "$d/retired-skill" ] || fail "stale toolkit link not removed"
  [ -L "$d/foreign-skill" ] || fail "foreign link removed"
  teardown_env
  echo "PASS: test_stale_cleanup_only_removes_our_links"
}

test_external_skills_link_but_native_wins() {
  setup_env
  local d="$HOME/.claude/skills" ext="$HOME/.agentic-workflow/external-skills/pack/skills"
  mkdir -p "$ext/review" "$ext/polish-x"
  echo "---" > "$ext/review/SKILL.md"; echo "---" > "$ext/polish-x/SKILL.md"
  AW_EXTERNAL_SKILL_DIRS="$ext/review
$ext/polish-x"
  aw_install_skills_into "$d" </dev/null >/dev/null
  [ "$(readlink "$d/review")" = "$ROOT/skills/review" ] || fail "external pack overrode native skill"
  [ "$(readlink "$d/polish-x")" = "$ext/polish-x" ] || fail "external skill not linked"
  teardown_env
  echo "PASS: test_external_skills_link_but_native_wins"
}

test_toolkit_link_and_registry_merge() {
  setup_env
  aw_link_toolkit >/dev/null
  [ "$(readlink "$HOME/.agentic-workflow/toolkit")" = "$ROOT" ] || fail "toolkit link wrong"
  [ -d "$HOME/.agentic-workflow/toolkit/skills/_shared" ] || fail "_shared not reachable via toolkit"
  mkdir -p "$HOME/.claude/skills" "$HOME/.codex/skills"
  printf 'claude %s\ngone %s\n' "$HOME/.claude/skills" "$HOME/nope" > "$HOME/.agentic-workflow/providers"
  aw_write_provider_registry "codex $HOME/.codex/skills" >/dev/null
  local reg="$HOME/.agentic-workflow/providers"
  grep -qx "claude $HOME/.claude/skills" "$reg" || fail "existing provider dropped from registry"
  grep -qx "codex $HOME/.codex/skills" "$reg" || fail "new provider missing from registry"
  ! grep -q '^gone ' "$reg" || fail "provider with missing skills dir kept"
  teardown_env
  echo "PASS: test_toolkit_link_and_registry_merge"
}

test_cursor_mcp_merge_preserves_and_is_idempotent() {
  setup_env
  mkdir -p "$HOME/.cursor"
  echo '{"mcpServers":{"mine":{"url":"https://x"}},"other":1}' > "$HOME/.cursor/mcp.json"
  AW_MCP_SERVERS='[{"name":"sample-server","command":"node","args":["/b/mcp.js"]},{"name":"prism-mcp","command":"npx","args":["-y","p@1"],"env":{"PRISM_DASHBOARD_PORT":"7180"}}]'
  cursor_register_mcp >/dev/null
  local f="$HOME/.cursor/mcp.json"
  jq -e '.mcpServers.mine.url == "https://x" and .other == 1' "$f" >/dev/null || fail "existing entries not preserved"
  jq -e '.mcpServers["sample-server"].args == ["/b/mcp.js"]' "$f" >/dev/null || fail "sample server not added"
  jq -e '.mcpServers["prism-mcp"].env.PRISM_DASHBOARD_PORT == "7180"' "$f" >/dev/null || fail "env not written"
  jq -e '.mcpServers["sample-server"] | has("env") | not' "$f" >/dev/null || fail "empty env should be omitted"
  cursor_register_mcp | has "up to date" || fail "second merge should be a no-op"
  teardown_env
  echo "PASS: test_cursor_mcp_merge_preserves_and_is_idempotent"
}

# A server the toolkit no longer ships (AW_MCP_RETIRED) is removed from mcp.json on re-run; the user's own entries
# and our current ones stay, and a second run removes nothing.
test_cursor_retires_stale_servers() {
  setup_env
  mkdir -p "$HOME/.cursor"
  echo '{"mcpServers":{"mine":{"url":"https://x"},"agentic-bridge":{"command":"node","args":["/old/mcp.js"]}}}' > "$HOME/.cursor/mcp.json"
  AW_MCP_SERVERS='[{"name":"sample-server","command":"node","args":["/b/mcp.js"]}]'
  AW_MCP_RETIRED="agentic-bridge"
  local f="$HOME/.cursor/mcp.json" out
  out="$(cursor_register_mcp)"
  jq -e '.mcpServers | has("agentic-bridge") | not' "$f" >/dev/null || fail "retired server not removed from mcp.json"
  jq -e '.mcpServers.mine.url == "https://x" and (.mcpServers | has("sample-server"))' "$f" >/dev/null || fail "other servers not kept"
  echo "$out" | has "agentic-bridge: removed (retired)" || fail "removal not reported: $out"
  out="$(cursor_register_mcp)"
  echo "$out" | has "up to date" || fail "second run should be a no-op: $out"
  ! echo "$out" | has "removed" || fail "second run should remove nothing: $out"
  AW_MCP_RETIRED="sample-server"
  out="$(cursor_register_mcp)"
  ! echo "$out" | has "sample-server: removed" || fail "a shipped server is not retired, so don't report it removed: $out"
  jq -e '.mcpServers | has("sample-server")' "$f" >/dev/null || fail "a shipped server must stay"
  teardown_env
  echo "PASS: test_cursor_retires_stale_servers"
}

# CLI providers (claude, codex): a retired server is removed only where it is registered; dry-run only prints.
test_cli_retire_removes_only_registered() {
  setup_env
  local calls="$SANDBOX/calls" out
  fake_present() { [ "$1" = "agentic-bridge" ]; }
  fake_remove() { aw_run echo "remove $AW_MCP_NAME" >>"$calls"; }
  AW_MCP_RETIRED="agentic-bridge never-registered"
  out="$(aw_mcp_retire_cli "Fake" fake_present fake_remove)"
  [ "$(cat "$calls")" = "remove agentic-bridge" ] || fail "expected one removal, got: $(cat "$calls" 2>/dev/null)"
  echo "$out" | has "agentic-bridge: removed from Fake (retired)" || fail "removal not reported: $out"
  ! echo "$out" | has "never-registered" || fail "absent server should be silent: $out"
  rm -f "$calls"
  AW_DRY_RUN=1
  out="$(aw_mcp_retire_cli "Fake" fake_present fake_remove)"
  [ ! -s "$calls" ] || fail "dry run removed something: $(cat "$calls")"
  echo "$out" | has "would remove agentic-bridge from Fake (retired)" || fail "dry run should say what it would remove: $out"
  AW_MCP_RETIRED=""
  [ -z "$(aw_mcp_retire_cli "Fake" fake_present fake_remove)" ] || fail "nothing retired should print nothing"
  teardown_env
  echo "PASS: test_cli_retire_removes_only_registered"
}

# A name still shipped (in AW_MCP_SERVERS) is never retired, whatever order the installer calls register and retire
# in: cursor's upsert keeps it, so claude and codex must too.
test_cli_retire_skips_shipped_servers() {
  setup_env
  local calls="$SANDBOX/calls" out
  fake_present() { return 0; }
  fake_remove() { aw_run echo "remove $AW_MCP_NAME" >>"$calls"; }
  AW_MCP_SERVERS='[{"name":"serena","command":"x","args":[]}]'
  AW_MCP_RETIRED="serena agentic-bridge"
  out="$(aw_mcp_retire_cli "Fake" fake_present fake_remove)"
  [ "$(cat "$calls")" = "remove agentic-bridge" ] || fail "only the unshipped server should go, got: $(cat "$calls" 2>/dev/null)"
  ! echo "$out" | has "serena" || fail "a shipped server should be silent: $out"
  AW_MCP_SERVERS=""; AW_MCP_RETIRED=""
  teardown_env
  echo "PASS: test_cli_retire_skips_shipped_servers"
}

# Claude: a retired server is removed from every scope ~/.claude.json holds it in (user, and local scope under any
# project), each local one from its own project directory; dry-run sees a local-only entry and runs nothing.
test_claude_retire_removes_user_and_local_scopes() {
  setup_env
  local proj="$SANDBOX/proj" bin="$SANDBOX/bin" calls="$SANDBOX/calls" out
  mkdir -p "$proj" "$bin"
  printf '#!/usr/bin/env bash\necho "$PWD $*" >>"%s"\n' "$calls" >"$bin/claude"; chmod +x "$bin/claude"
  jq -n --arg p "$proj" '{mcpServers: {"agentic-bridge": {command: "node"}},
    projects: {($p): {mcpServers: {"agentic-bridge": {command: "node"}}}}}' >"$HOME/.claude.json"
  AW_MCP_SERVERS='[]'; AW_MCP_RETIRED="agentic-bridge"
  out="$(PATH="$bin:$PATH" claude_retire_mcp)"
  grep -qx "$proj mcp remove --scope local agentic-bridge" "$calls" || fail "local scope not removed from its project: $(cat "$calls")"
  grep -q " mcp remove --scope user agentic-bridge$" "$calls" || fail "user scope not removed: $(cat "$calls")"
  echo "$out" | has "agentic-bridge: removed from Claude Code (retired)" || fail "removal not reported: $out"
  rm -f "$calls"
  jq -n --arg p "$proj" '{projects: {($p): {mcpServers: {"agentic-bridge": {command: "node"}}}}}' >"$HOME/.claude.json"
  AW_DRY_RUN=1
  out="$(PATH="$bin:$PATH" claude_retire_mcp)"
  [ ! -s "$calls" ] || fail "dry run ran claude: $(cat "$calls")"
  echo "$out" | has "would remove agentic-bridge from Claude Code (retired)" || fail "dry run missed the local-only entry: $out"
  AW_DRY_RUN=0
  jq -n '{projects: {"/no/such/dir": {mcpServers: {"agentic-bridge": {command: "node"}}}}}' >"$HOME/.claude.json"
  [ -z "$(PATH="$bin:$PATH" claude_retire_mcp)" ] && [ ! -s "$calls" ] || fail "an entry under a deleted project should be left alone"
  mkdir -p "$SANDBOX/gitrepo/sub"; git -C "$SANDBOX/gitrepo" init -q
  jq -n --arg p "$SANDBOX/gitrepo/sub" '{projects: {($p): {mcpServers: {"agentic-bridge": {command: "node"}}}}}' >"$HOME/.claude.json"
  [ -z "$(PATH="$bin:$PATH" claude_retire_mcp)" ] && [ ! -s "$calls" ] || fail "an entry under a git subdirectory should be left alone"
  git -C "$SANDBOX/gitrepo" -c user.email=t@t -c user.name=t commit -q --allow-empty -m init
  git -C "$SANDBOX/gitrepo" worktree add -q "$SANDBOX/gitrepo-wt" 2>/dev/null
  jq -n --arg p "$SANDBOX/gitrepo-wt" '{projects: {($p): {mcpServers: {"agentic-bridge": {command: "node"}}}}}' >"$HOME/.claude.json"
  [ -z "$(PATH="$bin:$PATH" claude_retire_mcp)" ] && [ ! -s "$calls" ] || fail "an entry keyed at a linked worktree should be left alone"
  AW_MCP_SERVERS=""; AW_MCP_RETIRED=""
  teardown_env
  echo "PASS: test_claude_retire_removes_user_and_local_scopes"
}

# A retired package's leftover ignored output (dist, node_modules, a db) is removed once git tracks nothing in it;
# a directory git still tracks is kept, and dry-run only prints.
test_remove_retired_dir_only_when_untracked() {
  setup_env
  local repo="$SANDBOX/repo" out
  mkdir -p "$repo/old-pkg/dist" "$repo/kept"
  git -C "$repo" init -q
  touch "$repo/old-pkg/dist/mcp.js" "$repo/kept/file"
  printf 'dist/\n' >"$repo/.gitignore"
  git -C "$repo" add kept/file
  AW_DRY_RUN=1
  out="$(aw_remove_retired_dir "$repo" old-pkg)"
  [ -d "$repo/old-pkg" ] || fail "dry run removed the directory"
  echo "$out" | has "would remove $repo/old-pkg" || fail "dry run should say what it would remove: $out"
  AW_DRY_RUN=0
  out="$(aw_remove_retired_dir "$repo" old-pkg)"
  [ ! -e "$repo/old-pkg" ] || fail "untracked leftover not removed"
  echo "$out" | has "removed $repo/old-pkg (retired)" || fail "removal not reported: $out"
  out="$(aw_remove_retired_dir "$repo" kept)"
  [ -f "$repo/kept/file" ] || fail "a tracked directory must stay"
  mkdir -p "$repo/wip/dist"; touch "$repo/wip/dist/x.js" "$repo/wip/new-feature.ts"
  printf 'dist/\n' >"$repo/.gitignore"
  aw_remove_retired_dir "$repo" wip >/dev/null
  [ -f "$repo/wip/new-feature.ts" ] || fail "untracked work that git doesn't ignore must stay"
  mkdir -p "$SANDBOX/plain/old-pkg"
  aw_remove_retired_dir "$SANDBOX/plain" old-pkg >/dev/null
  [ -d "$SANDBOX/plain/old-pkg" ] || fail "outside a git repo nothing may be removed"
  [ -z "$(aw_remove_retired_dir "$repo" old-pkg)" ] || fail "a missing directory should print nothing"
  teardown_env
  echo "PASS: test_remove_retired_dir_only_when_untracked"
}

test_dry_run_writes_nothing() {
  setup_env
  AW_DRY_RUN=1
  AW_MCP_SERVERS='[{"name":"sample-server","command":"node","args":["/b/mcp.js"]}]'
  aw_link_toolkit >/dev/null
  aw_install_skills_into "$HOME/.claude/skills" </dev/null >/dev/null
  cursor_register_mcp >/dev/null
  aw_write_provider_registry "claude $HOME/.claude/skills" >/dev/null
  codex_install_agents >/dev/null
  [ -z "$(ls -A "$HOME")" ] || fail "dry run wrote into HOME: $(ls -A "$HOME")"
  teardown_env
  echo "PASS: test_dry_run_writes_nothing"
}

test_agents_render_per_provider() {
  setup_env
  codex_install_agents >/dev/null
  cursor_install_agents >/dev/null
  local c="$HOME/.codex/agents" u="$HOME/.cursor/agents"
  [ "$(ls "$c"/*.toml | wc -l | tr -d ' ')" = "4" ] || fail "expected 4 codex agent tomls"
  grep -q '^name = "lean-researcher"$' "$c/lean-researcher.toml" || fail "codex name missing"
  grep -q '^sandbox_mode = "read-only"$' "$c/lean-researcher.toml" || fail "read-only agent not sandboxed"
  ! grep -q '^sandbox_mode' "$c/lean-coder.toml" || fail "write agent must not be read-only"
  grep -q '^developer_instructions = """$' "$c/lean-coder.toml" || fail "developer_instructions missing"
  grep -q '^model_reasoning_effort = "low"$' "$c/lean-researcher.toml" || fail "haiku pin not mapped to low effort"
  grep -q '^model: fast$' "$u/lean-researcher.md" || fail "cursor haiku→fast mapping missing"
  grep -q '^readonly: true$' "$u/lean-reviewer.md" || fail "cursor readonly missing"
  ! grep -q '^tools:' "$u/lean-coder.md" || fail "claude tool names leaked into cursor agent"
  jq -e '."lean-coder.toml"' "$HOME/.agentic-workflow/managed/agents-codex.json" >/dev/null || fail "codex manifest missing"
  teardown_env
  echo "PASS: test_agents_render_per_provider"
}

test_resize_hooks_migrate_to_tagged_per_tty_entries() {
  setup_env
  local f="$HOME/.claude/settings.json" before after out
  mkdir -p "$HOME/.claude"
  cat >"$f" <<'JSON'
{"hooks":{
 "Stop":[{"hooks":[{"type":"command","command":"~/.claude/hooks/done-gate.sh"}]},
         {"hooks":[{"type":"command","command":"SHELL_PID=$(cat \"$HOME/.claude/shell_pid\" 2>/dev/null); [ -n \"$SHELL_PID\" ] && kill -WINCH \"$SHELL_PID\" 2>/dev/null; sleep 0.05; true"}]}],
 "PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"~/.claude/hooks/scope-gate.sh"}]},
               {"matcher":".*","hooks":[{"type":"command","command":"SHELL_PID=$(cat \"$HOME/.claude/shell_pid\" 2>/dev/null); [ -n \"$SHELL_PID\" ] && kill -WINCH \"$SHELL_PID\" 2>/dev/null; true"}]}]},
 "statusLine":{"type":"command","command":"x"}}
JSON
  out="$(AW_DRY_RUN=1 claude_install_settings 2>&1)"
  echo "$out" | has "would migrate 2 legacy" || fail "dry-run should announce the migration: $out"
  [ "$(jq '[.. | .command? // empty | select(contains("shell_pid\""))] | length' "$f")" = 2 ] || fail "dry-run changed settings.json"
  claude_install_settings >/dev/null 2>&1 || fail "install failed"
  [ "$(jq '[.. | .command? // empty | select(contains("$HOME/.claude/shell_pid\""))] | length' "$f")" = 0 ] || fail "legacy commands remain"
  for e in Stop PreToolUse; do
    [ "$(jq --arg e "$e" '[.hooks[$e][].hooks[] | select(.command | endswith("# aw:winch"))] | length' "$f")" = 1 ] || fail "$e: expected exactly one aw:winch hook"
  done
  jq -e '.hooks.Stop | any(.[].hooks[]; .command == "~/.claude/hooks/done-gate.sh")' "$f" >/dev/null || fail "done-gate removed"
  jq -e '.hooks.PreToolUse | any(.[].hooks[]; .command == "~/.claude/hooks/scope-gate.sh")' "$f" >/dev/null || fail "scope-gate removed"
  [ "$(jq '.hooks.Stop | length' "$f")" = 2 ] && [ "$(jq '.hooks.PreToolUse | length' "$f")" = 2 ] || fail "unexpected group count"
  jq -e '.hooks.Stop[].hooks[] | select(.command | endswith("# aw:winch")) | .command | contains("shell_pid.d")' "$f" >/dev/null || fail "winch hook is not per-tty"
  before="$(cat "$f")"
  claude_install_settings >/dev/null 2>&1 || fail "second install failed"
  after="$(cat "$f")"
  [ "$before" = "$after" ] || fail "second install changed settings.json"
  teardown_env
  echo "PASS: test_resize_hooks_migrate_to_tagged_per_tty_entries"
}

test_state_root_follows_aw_state_dir() {
  setup_env
  local want="$SANDBOX/state"
  ( unset AW_STATE_ROOT; export AW_STATE_DIR="$want"; source "$ROOT/providers/lib.sh"; [ "$AW_STATE_ROOT" = "$want" ] ) \
    || fail "AW_STATE_ROOT does not default from AW_STATE_DIR"
  ( unset AW_STATE_DIR; export AW_STATE_ROOT="$want/alias"; source "$ROOT/providers/lib.sh"; [ "$AW_STATE_ROOT" = "$want/alias" ] ) \
    || fail "AW_STATE_ROOT alias no longer works"
  ( export AW_STATE_DIR="$want" AW_STATE_ROOT="$want/alias"; source "$ROOT/providers/lib.sh"; [ "$AW_STATE_ROOT" = "$want" ] ) \
    || fail "AW_STATE_DIR should win over the AW_STATE_ROOT alias"
  ( source "$ROOT/providers/lib.sh"; [ "$AW_STATE_ROOT" = "$HOME/.agentic-workflow" ] ) || fail "default state root changed"
  teardown_env
  echo "PASS: test_state_root_follows_aw_state_dir"
}

# config/mcp.json holds only xcodebuildmcp (Darwin-only): the legacy copy runs on macOS and is skipped elsewhere.
test_darwin_only_mcp_json_copy() {
  setup_env
  mkdir -p "$HOME/.claude" "$SANDBOX/fakebin"
  CLAUDE_DIR="$HOME/.claude"
  printf '#!/usr/bin/env bash\necho Linux\n' > "$SANDBOX/fakebin/uname"; chmod +x "$SANDBOX/fakebin/uname"
  PATH="$SANDBOX/fakebin:$PATH" claude_install_legacy_mcp_json >/dev/null
  [ ! -e "$HOME/.claude/mcp.json" ] || fail "mcp.json (xcodebuildmcp) copied on Linux"
  printf '#!/usr/bin/env bash\necho Darwin\n' > "$SANDBOX/fakebin/uname"
  PATH="$SANDBOX/fakebin:$PATH" claude_install_legacy_mcp_json >/dev/null
  [ -f "$HOME/.claude/mcp.json" ] || fail "mcp.json not copied on Darwin"
  teardown_env
  echo "PASS: test_darwin_only_mcp_json_copy"
}

test_links_managed_and_bootstrap_then_is_idempotent
test_darwin_only_mcp_json_copy
test_state_root_follows_aw_state_dir
test_refreshes_legacy_link_and_removes_deprecated
test_foreign_collision_is_kept_when_stdin_closed
test_stale_cleanup_only_removes_our_links
test_external_skills_link_but_native_wins
test_toolkit_link_and_registry_merge
test_cursor_mcp_merge_preserves_and_is_idempotent
test_cursor_retires_stale_servers
test_cli_retire_removes_only_registered
test_cli_retire_skips_shipped_servers
test_claude_retire_removes_user_and_local_scopes
test_remove_retired_dir_only_when_untracked
test_dry_run_writes_nothing
test_agents_render_per_provider
test_resize_hooks_migrate_to_tagged_per_tty_entries
