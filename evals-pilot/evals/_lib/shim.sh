# Sourced by a case's scaffold script. Places the pieces of the installed toolkit that the skill text reads
# under the run's temporary HOME: skills/_preamble.md, skills/_shared/, config/voice/. These are the paths
# skills hard-code as $HOME/.agentic-workflow/toolkit/... Nothing is executed from here.
# Needs: AW_LIB (this directory), set by the caller.
shim_toolkit() {
  local t="$HOME/.agentic-workflow/toolkit"
  mkdir -p "$t/skills" "$t/config" "$HOME/.agentic-workflow"
  cp "$AW_LIB/../../skills/_preamble.md" "$t/skills/_preamble.md"
  cp -r "$AW_LIB/../../skills/_shared" "$t/skills/_shared"
  [ -d "$AW_LIB/voice" ] && cp -r "$AW_LIB/voice" "$t/config/voice"
  # the installed-skills registry the preamble's bootstrap check reads: one "<provider> <dir>" line per provider
  echo "claude $HOME/.claude/skills" > "$HOME/.agentic-workflow/providers"
  return 0
}
