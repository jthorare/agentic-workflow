# Capabilities (shared)

Canonical, provider-neutral vocabulary used by every skill. Skills name a **capability**; the agent
running the skill uses the tool its host provides. Every supported provider (Claude Code, Codex,
Cursor) has every capability below — use the right tool, never skip the step.
Referenced via: SHARED_DIR pattern (CD2).

## Capability map

| Capability | Claude Code | Codex | Cursor |
|------------|-------------|-------|--------|
| **Ask the user** (structured question, 2–4 options) | `AskUserQuestion` | `request_user_input` | `AskQuestion` |
| **Spawn a subagent** | `Agent` (`subagent_type`, `prompt`) | `spawn_agent`, then `wait_agent` | `Task` (`subagent_type`, `prompt`) |
| **Dispatch in parallel** | N `Agent` calls in one message | N `spawn_agent` calls, then one `wait_agent` over all ids | N `Task` calls in one message |
| **Message a running agent** | `SendMessage` | `send_message` | `Task` resume / follow-up |
| **List agents** (other live sessions and their names) | `ListAgents` | — (no cross-session messaging) | — |
| **Watch in the background** (a long-running command whose output lines arrive as events) | `Monitor` | run it in a background shell and poll its output | run it in a background shell and poll its output |
| **Invoke another skill** | `Skill` tool (`skill: <name>`) | Mention `$<name>` or read `$TOOLKIT/skills/<name>/SKILL.md` and follow it | `/<name>` or read `$TOOLKIT/skills/<name>/SKILL.md` and follow it |
| **Run shell** | `Bash` | `exec` / shell | `Shell` |
| **Read / search files** | `Read`, `Grep`, `Glob` | shell (`rg`, `sed -n`) | `Read`, `Grep`, `Glob` |
| **Write / edit files** | `Write`, `Edit` | `apply_patch` | `Write`, `StrReplace` |
| **Fetch a URL / search the web** | `WebFetch`, `WebSearch` | `web_search` | `WebFetch`, `WebSearch` |
| **Track todos** | `TaskCreate` / `TodoWrite` | `update_plan` | `TodoWrite` |
| **Call an MCP tool** | `mcp__<server>__<tool>` | `<tool>` in the `mcp__<server>` namespace | `CallMcpTool` (`server`, `toolName`) |
| **Repo instructions** | `CLAUDE.md` (imports `AGENTS.md`) + `.claude/rules/` | `AGENTS.md` (+ nested `AGENTS.md`) | `AGENTS.md` + `.cursor/rules/*.mdc` |

## Notation used in skill text

- **Ask the user:** bold phrase, followed by the question and options. Use the provider's structured
  question tool — do not ask in free text when the tool exists.
- **Spawn a subagent** / **Dispatch in parallel:** bold phrase. See `parallel-dispatch.md` for fan-out rules.
- **Invoke skill `<name>`:** bold phrase with the skill name.
- MCP tools are written `mcp: <server>/<tool>` (e.g. `mcp: prism-mcp/session_load_context`,
  `mcp: playwright/browser_navigate`). Resolve to the provider's MCP calling convention above.
- `allowed-tools` in SKILL.md frontmatter uses Claude Code tool names. Other providers ignore it; do
  not treat it as a list of tools to call.

## Stable paths

| Variable | Value | Purpose |
|----------|-------|---------|
| `TOOLKIT` | `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}` | Symlink to the toolkit repo root, created by `setup.sh` |
| `SHARED_DIR` | `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared` | Shared skill fragments — never resolve through a provider's skills dir |
| Provider registry | `$HOME/.agentic-workflow/providers` | One line per installed provider: `<name> <skills-dir>` |
