#!/usr/bin/env bash
# Codex adapter for Agentic Workflow installer.
# Sourced by setup.sh (after providers/lib.sh). Defines codex_* functions only.
#
# Verified against codex-cli 0.158.0 (2026-09-28):
#   - Skills: Codex scans $CODEX_HOME/skills (default ~/.codex/skills) AND
#     ~/.agents/skills, and follows symlinked skill dirs (checked with
#     `codex debug prompt-input` against a temp CODEX_HOME). We link into
#     $CODEX_HOME/skills only: ~/.agents/skills is also read by Cursor and on
#     this machine already holds Cursor-managed real dirs (e.g. `review`), so
#     linking there would collide. One dir per provider = no double registration.
#   - Custom agents: $CODEX_HOME/agents/<name>.toml with name, description,
#     developer_instructions (required) + optional model_reasoning_effort,
#     sandbox_mode (feature `multi_agent` is stable/on).
#   - MCP: `codex mcp add <name> [--env K=V] -- <cmd> <args...>`;
#     `codex mcp get <name>` exits non-zero when absent.

CODEX_HOME_DIR="${CODEX_HOME:-$HOME/.codex}"

codex_detect() { aw_has codex; }
codex_skills_dir() { echo "$CODEX_HOME_DIR/skills"; }
codex_agents_dir() { echo "$CODEX_HOME_DIR/agents"; }

codex_mcp_present() { codex mcp get "$1" &>/dev/null; }

codex_mcp_add() {
  local envflags=() e
  for e in ${AW_MCP_ENV[@]+"${AW_MCP_ENV[@]}"}; do envflags+=(--env "$e"); done
  aw_run codex mcp add "$AW_MCP_NAME" ${envflags[@]+"${envflags[@]}"} \
    -- "$AW_MCP_CMD" ${AW_MCP_ARGV[@]+"${AW_MCP_ARGV[@]}"}
}

codex_mcp_remove() { aw_run codex mcp remove "$AW_MCP_NAME"; }

codex_register_mcp() {
  aw_mcp_register_cli "Codex" codex_mcp_present codex_mcp_add
  aw_mcp_retire_cli "Codex" codex_mcp_present codex_mcp_remove
}

codex_install_hooks() {
  local hooks_script="$TOOLKIT_DIR/providers/codex/install-hooks.sh"
  echo ""
  if [ -f "$hooks_script" ]; then
    TOOLKIT_DIR="$TOOLKIT_DIR" AW_DRY_RUN="${AW_DRY_RUN:-0}" bash "$hooks_script"
  else
    echo "  (no providers/codex/install-hooks.sh — skipping)"
  fi
  # judge's SessionStart health hook (honors AW_DRY_RUN itself).
  AW_DRY_RUN="${AW_DRY_RUN:-0}" bash "$TOOLKIT_DIR/scripts/install-judge-hooks.sh" --provider codex
  # sindri's SessionStart nudge (honors AW_DRY_RUN itself).
  AW_DRY_RUN="${AW_DRY_RUN:-0}" bash "$TOOLKIT_DIR/scripts/install-sindri-hooks.sh" --provider codex
}

codex_install_agents() {
  local manifest="$AW_STATE_ROOT/managed/agents-codex.json"
  aw_run mkdir -p "$AW_STATE_ROOT/managed"
  aw_dry || { [ -f "$manifest" ] || echo '{}' > "$manifest"; }
  install_agents "$TOOLKIT_DIR/config/agents" "$(codex_agents_dir)" "$manifest" codex
  echo "  codex: lean agent types → $(codex_agents_dir) (manifest: $manifest)"
}

codex_install_voice() { aw_install_voice "$CODEX_HOME_DIR/AGENTS.md"; }

codex_install() {
  echo ""
  echo "=== Provider: Codex ==="
  aw_install_skills_into "$(codex_skills_dir)"
  codex_install_hooks
  codex_install_voice
  aw_install_levers codex
  codex_register_mcp
}
