#!/usr/bin/env bash
# Claude Code adapter for Agentic Workflow installer.
# Sourced by setup.sh (after providers/lib.sh). Defines claude_* functions only.
#
# End state (unchanged from the pre-multi-provider setup.sh):
#   ~/.claude/skills/<name>  → symlinks into this repo (+ bootstrap, + external packs)
#   ~/.claude/settings.json  (copied if absent; statusLine + WINCH hooks merged)
#   ~/.claude/statusline.sh, ~/.claude/shell-integration.sh (+ rc source line)
#   ~/.claude/hooks/*        via providers/claude/install-hooks.sh
#   ~/.claude/agents/*.md    via --install-agents
#   MCP servers registered with `claude mcp add --scope user`

CLAUDE_DIR="${CLAUDE_DIR:-$HOME/.claude}"

claude_detect() { aw_has claude; }
claude_skills_dir() { echo "$CLAUDE_DIR/skills"; }
claude_agents_dir() { echo "$CLAUDE_DIR/agents"; }

claude_install_settings() {
  echo ""
  echo "Installing Claude Code settings..."
  local settings="$CLAUDE_DIR/settings.json"
  if [ -f "$settings" ]; then
    echo "  settings.json already exists."
    echo "  Current file will NOT be overwritten."
    echo "  Compare manually: diff $settings $TOOLKIT_DIR/config/settings.json"
  else
    aw_run mkdir -p "$CLAUDE_DIR"
    aw_run cp "$TOOLKIT_DIR/config/settings.json" "$settings"
    echo "  settings.json: copied"
  fi

  echo ""
  echo "Installing statusline..."
  aw_run cp "$TOOLKIT_DIR/config/statusline.sh" "$CLAUDE_DIR/statusline.sh"
  aw_run chmod +x "$CLAUDE_DIR/statusline.sh"
  echo "  statusline script installed"

  [ -f "$settings" ] || return 0
  # Resize hooks are owned, tagged entries ("# aw:winch"): installed no matter what other
  # Stop/PreToolUse hooks exist, replaced in place on re-run, never touching other hooks.
  # Migration: the old untagged form read the global $HOME/.claude/shell_pid file, which the
  # shell integration no longer writes; those commands (and only those) are removed.
  # shellcheck source=../../config/lib/merge-hook.sh
  source "$TOOLKIT_DIR/config/lib/merge-hook.sh"
  # Per-window: walks up to this session's own tty and signals only the shell that wrote
  # ~/.claude/shell_pid.d/<tty>. No pid file is shared between windows.
  local WINCH_CMD='p=$PPID; for i in 1 2 3 4 5 6 7 8; do o=$(ps -o ppid=,tty= -p $p 2>/dev/null); set -- $o; t=$2; case $t in ""|"??"|-) p=$1;; *) f="$HOME/.claude/shell_pid.d/$t"; [ -f "$f" ] && kill -WINCH "$(cat "$f")" 2>/dev/null; break;; esac; done; '
  local legacy_jq='[.hooks.Stop[]?, .hooks.PreToolUse[]? | .hooks[]? | (.command // "") | select(contains("$HOME/.claude/shell_pid\"") and (contains("# aw:") | not))] | length'
  local legacy_n
  legacy_n=$(jq "$legacy_jq" "$settings" 2>/dev/null || echo 0)
  if aw_dry; then
    echo "  [dry-run] would merge statusLine into $settings if absent"
    echo "  [dry-run] would migrate $legacy_n legacy global-shell_pid resize hook(s) and set per-tty '# aw:winch' hooks on Stop + PreToolUse"
    return 0
  fi

  # Add statusLine key if absent (use has() so null values are not re-merged)
  if ! jq -e 'has("statusLine")' "$settings" &>/dev/null; then
    jq '. + {"statusLine": {"type": "command", "command": "~/.claude/statusline.sh"}}' \
      "$settings" > "$settings.tmp" && mv "$settings.tmp" "$settings"
    echo "  statusLine config added to existing settings.json"
  fi

  if [ "$legacy_n" -gt 0 ]; then
    jq 'def legacy: (.command // "") | (contains("$HOME/.claude/shell_pid\"") and (contains("# aw:") | not));
        reduce ("Stop","PreToolUse") as $e (.;
          if .hooks[$e] then
            .hooks[$e] = [ .hooks[$e][] | .hooks = [ (.hooks // [])[] | select(legacy | not) ] | select(.hooks | length > 0) ]
            | if (.hooks[$e] | length) == 0 then .hooks |= del(.[$e]) else . end
          else . end)' \
      "$settings" > "$settings.tmp" && mv "$settings.tmp" "$settings"
    echo "  resize hooks: migrated $legacy_n legacy global-shell_pid hook(s)"
  fi
  local stop_hook ptu_hook
  stop_hook=$(jq -n --arg c "$WINCH_CMD sleep 0.05; true # aw:winch" '{hooks:[{type:"command",command:$c}]}')
  ptu_hook=$(jq -n --arg c "$WINCH_CMD true # aw:winch" '{matcher:".*",hooks:[{type:"command",command:$c}]}')
  merge_hook "$settings" Stop aw:winch "$stop_hook" || return 1
  merge_hook "$settings" PreToolUse aw:winch "$ptu_hook" || return 1
  echo "  hooks.Stop / hooks.PreToolUse: per-tty resize hooks set (aw:winch)"
}

claude_install_shell_integration() {
  echo ""
  echo "Installing shell integration (statusline width sync)..."
  if aw_dry; then
    echo "  [dry-run] would write $CLAUDE_DIR/shell-integration.sh and source it from ~/.zshrc / ~/.bashrc"
    return 0
  fi

  local si_file="$CLAUDE_DIR/shell-integration.sh" si_tmp
  si_tmp="$(mktemp)"
  cat > "$si_tmp" << 'SHELL_EOF'
# Claude Code shell integration — written by Agentic Workflow setup.sh
# Records THIS terminal's width in ~/.claude/terminal_width.d/<tty> (e.g. ttys003) and
# this shell's pid in ~/.claude/shell_pid.d/<tty>, so statusline.sh and the Claude Code
# hooks of a session running in this terminal can find them. Everything is keyed by tty:
# nothing is shared between windows. statusline.sh normally reads the tty size directly and
# uses this file only as a fallback. Only an interactive shell attached to a tty writes, and
# never a Claude Code tool shell (CLAUDECODE is set; its COLUMNS/tput values are bogus).
# When zsh/bash receive SIGWINCH they refresh $COLUMNS (ioctl TIOCGWINSZ) before the trap.

_claude_update_width() {
  case $- in *i*) ;; *) return 0 ;; esac
  [ -t 1 ] || return 0
  [ -z "${CLAUDECODE:-}" ] || return 0
  local tty width
  tty=$(basename "$(tty 2>/dev/null)")
  case "$tty" in ""|"not a tty"|"not") return 0 ;; esac
  mkdir -p "$HOME/.claude/terminal_width.d" "$HOME/.claude/shell_pid.d" 2>/dev/null
  printf '%s\n' "$$" > "$HOME/.claude/shell_pid.d/$tty"
  # $COLUMNS (kept current by the shell) first; tput cols when it is unset.
  width="${COLUMNS:-$(tput cols 2>/dev/null)}"
  [ -n "$width" ] && [ "$width" -gt 0 ] 2>/dev/null && \
    printf '%s\n' "$width" > "$HOME/.claude/terminal_width.d/$tty"
  return 0
}

if [ -n "$ZSH_VERSION" ]; then
  autoload -U add-zsh-hook 2>/dev/null && add-zsh-hook precmd _claude_update_width
  trap '_claude_update_width' WINCH
elif [ -n "$BASH_VERSION" ]; then
  [[ "$PROMPT_COMMAND" != *"_claude_update_width"* ]] && \
    PROMPT_COMMAND="${PROMPT_COMMAND:+$PROMPT_COMMAND; }_claude_update_width"
  trap '_claude_update_width' WINCH
fi

_claude_update_width
SHELL_EOF

  if cmp -s "$si_tmp" "$si_file" 2>/dev/null; then
    echo "  shell-integration.sh: already up to date (skipped)"
  else
    mv "$si_tmp" "$si_file"
    echo "  shell-integration.sh: written"
  fi
  rm -f "$si_tmp"

  local line='[ -f ~/.claude/shell-integration.sh ] && source ~/.claude/shell-integration.sh'
  local rc added=""
  for rc in "$HOME/.zshrc" "$HOME/.bashrc"; do
    [ -f "$rc" ] || continue
    if ! grep -qF 'source ~/.claude/shell-integration.sh' "$rc" 2>/dev/null; then
      printf '\n# Claude Code statusline width sync\n%s\n' "$line" >> "$rc"
      added="$added $rc"
      echo "  Added to $rc"
    else
      echo "  Already in $rc"
    fi
  done

  if [ -n "$added" ]; then
    echo ""
    echo "  Shell integration added. To enable width sync in this session:"
    for rc in $added; do echo "    source $rc"; done
  fi
}

claude_install_legacy_mcp_json() {
  # config/mcp.json holds only xcodebuildmcp, which needs Xcode: macOS only (the catalog in setup.sh gates it the same way)
  [ "$(uname)" = "Darwin" ] || return 0
  echo ""
  echo "Installing MCP config..."
  local mcp_file="$CLAUDE_DIR/mcp.json"
  if [ -f "$mcp_file" ]; then
    echo "  mcp.json already exists."
    echo "  Current file will NOT be overwritten."
    echo "  Compare manually: diff $mcp_file $TOOLKIT_DIR/config/mcp.json"
  else
    aw_run cp "$TOOLKIT_DIR/config/mcp.json" "$mcp_file"
    echo "  mcp.json: copied"
  fi
}

claude_install_hooks() {
  echo ""
  echo "Installing Claude Code hooks..."
  local hooks_script="$TOOLKIT_DIR/providers/claude/install-hooks.sh"
  if [ ! -f "$hooks_script" ]; then
    echo "  WARN: $hooks_script not found — safety hooks not installed"
  else
    TOOLKIT_DIR="$TOOLKIT_DIR" CLAUDE_DIR="$CLAUDE_DIR" AW_DRY_RUN="${AW_DRY_RUN:-0}" bash "$hooks_script"
  fi

  # judge's SessionStart health hook lands after settings.json is seeded.
  if aw_dry; then
    echo "  [dry-run] would run scripts/install-judge-hooks.sh"
  else
    bash "$TOOLKIT_DIR/scripts/install-judge-hooks.sh"
  fi
  # sindri's SessionStart nudge.
  if aw_dry; then
    echo "  [dry-run] would run scripts/install-sindri-hooks.sh (sindri-nudge)"
  else
    bash "$TOOLKIT_DIR/scripts/install-sindri-hooks.sh"
  fi
  aw_install_levers claude
}

claude_mcp_present() {
  local name="$1"
  if [ -f "$HOME/.claude.json" ] && jq -e --arg n "$name" '.mcpServers[$n] != null' "$HOME/.claude.json" &>/dev/null; then
    return 0
  fi
  # `claude mcp get` health-checks and may rewrite ~/.claude.json; skip it in dry-run.
  aw_dry && return 1
  claude mcp get "$name" &>/dev/null
}

claude_mcp_add() {
  local envflags=() e
  for e in ${AW_MCP_ENV[@]+"${AW_MCP_ENV[@]}"}; do envflags+=(--env "$e"); done
  aw_run claude mcp add --scope user "$AW_MCP_NAME" ${envflags[@]+"${envflags[@]}"} \
    -- "$AW_MCP_CMD" ${AW_MCP_ARGV[@]+"${AW_MCP_ARGV[@]}"}
}

# A retired server can sit in user scope and in local scope under any project (old installs ran `claude mcp add`
# without --scope from the repo), all in ~/.claude.json. Read it with jq so dry-run sees every scope too.
claude_mcp_local_projects() {
  [ -f "$HOME/.claude.json" ] || return 0
  jq -r --arg n "$1" '(.projects // {}) | to_entries[] | select(.value.mcpServers[$n] != null) | .key' "$HOME/.claude.json"
}

# The local entries the CLI can remove: under a directory that still exists and, inside a git repo, keyed at the main
# repository's root (the CLI keys local scope by it, also from a subdirectory or a linked worktree). Any other entry
# can't be removed with the CLI and is never loaded, so it is left alone.
claude_mcp_removable_projects() {
  local proj common top
  while IFS= read -r proj; do
    [ -d "$proj" ] || continue
    common="$(git -C "$proj" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)" || common=""
    top=""; [ -z "$common" ] || top="$(cd "$common/.." && pwd -P)"
    [ -z "$top" ] || [ "$top" = "$(cd "$proj" && pwd -P)" ] || continue
    printf '%s\n' "$proj"
  done < <(claude_mcp_local_projects "$1")
}

# Present = a user entry, or a local one the CLI can remove.
claude_mcp_present_any_scope() {
  jq -e --arg n "$1" '.mcpServers[$n] != null' "$HOME/.claude.json" &>/dev/null && return 0
  [ -n "$(claude_mcp_removable_projects "$1")" ]
}

# Each local entry is removed from its own project directory; succeeds only if every removal does.
claude_mcp_remove() {
  local proj rc=0
  while IFS= read -r proj; do
    (cd "$proj" && aw_run claude mcp remove --scope local "$AW_MCP_NAME") || rc=1
  done < <(claude_mcp_removable_projects "$AW_MCP_NAME")
  if jq -e --arg n "$AW_MCP_NAME" '.mcpServers[$n] != null' "$HOME/.claude.json" &>/dev/null; then
    aw_run claude mcp remove --scope user "$AW_MCP_NAME" || rc=1
  fi
  return "$rc"
}

claude_retire_mcp() {
  aw_mcp_retire_cli "Claude Code" claude_mcp_present_any_scope claude_mcp_remove
}

claude_register_mcp() {
  aw_mcp_register_cli "Claude Code" claude_mcp_present claude_mcp_add
  claude_retire_mcp
}

claude_install_plugins() {
  echo ""
  echo "Installing Claude Code plugins..."
  if aw_dry; then
    echo "  [dry-run] would add marketplaces and install plugins: github, superpowers, compound-engineering, playwright"
    return 0
  fi
  local repo name plugin
  for repo in "anthropics/claude-plugins-official" "VoltAgent/awesome-claude-code-subagents" "EveryInc/compound-engineering-plugin"; do
    name=$(basename "$repo")
    if claude plugins marketplace list 2>&1 | grep -q "$name"; then
      echo "  marketplace $name: already added"
    else
      claude plugins marketplace add "github:$repo" 2>&1 && \
        echo "  marketplace $name: added" || \
        echo "  marketplace $name: failed to add (non-fatal)"
    fi
  done
  for plugin in "github@claude-plugins-official" "superpowers@claude-plugins-official" "compound-engineering@compound-engineering-plugin" "playwright@claude-plugins-official"; do
    if claude plugins list 2>&1 | grep -q "$plugin"; then
      echo "  plugin $plugin: already installed"
    else
      claude plugins install "$plugin" 2>&1 && \
        echo "  plugin $plugin: installed" || \
        echo "  plugin $plugin: failed to install (non-fatal)"
    fi
  done
}

claude_security_check() {
  if grep -qE "Users/${USER:-}/\*\*|home/${USER:-}/\*\*" "$CLAUDE_DIR/settings.local.json" 2>/dev/null; then
    echo "WARN: Broad Read rule detected in settings.local.json — narrow to repos/**, .claude/**, .agentic-workflow/**"
  fi
}

claude_install_agents() {
  local manifest="$AW_STATE_ROOT/managed/agents.json"
  aw_run mkdir -p "$AW_STATE_ROOT/managed"
  aw_dry || { [ -f "$manifest" ] || echo '{}' > "$manifest"; }
  install_agents "$TOOLKIT_DIR/config/agents" "$(claude_agents_dir)" "$manifest" claude
  echo "  claude: lean agent types → $(claude_agents_dir) (manifest: $manifest)"
}

claude_install_voice() { aw_install_voice "$CLAUDE_DIR/CLAUDE.md"; }

# Full Claude install (skills, config, hooks, voice, MCP, plugins). Agents are opt-in
# via `setup.sh --install-agents`.
claude_install() {
  echo ""
  echo "=== Provider: Claude Code ==="
  aw_install_skills_into "$(claude_skills_dir)"
  claude_install_settings
  claude_install_hooks
  claude_install_shell_integration
  claude_install_voice
  claude_install_legacy_mcp_json
  claude_register_mcp
  echo "=== Security check ==="
  claude_security_check
  claude_install_plugins
}
