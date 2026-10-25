# Hook adapters

Hook logic lives once, in `config/hooks/*.sh`, written against Claude Code's hook protocol
(JSON on stdin with `tool_name`/`tool_input`/`session_id`/...; exit 2 = deny/block, reason on
stdout or stderr; optional `hookSpecificOutput` JSON on stdout). Each adapter here runs one of
those scripts under another host:

```
<adapter>.sh [--raw] [--event <provider-event>] <hook-script> [args...]   # aw:<id>
```

It reads the host's stdin, converts it to the Claude shape, `cd`s to the session's working
directory, runs the script with `AW_HOOK_PROVIDER=<provider>`, and converts the result to the
host's allow/deny protocol. `--raw` skips the input conversion (used by the probe).

| File | Purpose |
|------|---------|
| `codex.sh` | Codex (`~/.codex/hooks.json`) |
| `cursor.sh` | Cursor (`~/.cursor/hooks.json`) |
| `common.sh` | Shared argument parsing and script runner |
| `install-lib.sh` | Install helpers used by `providers/<name>/install-hooks.sh`, `scripts/install-*.sh --provider`, `scripts/probe.sh --provider` |

## Install layout

| Provider | Hook config | Scripts copied to | Base installer |
|----------|-------------|-------------------|----------------|
| Claude Code | `~/.claude/settings.json` | `~/.claude/hooks/` (unchanged) | `providers/claude/install-hooks.sh` |
| Codex | `~/.codex/hooks.json` | `~/.agentic-workflow/hooks/` (+ `adapters/`, `lib/locks.sh`) | `providers/codex/install-hooks.sh` |
| Cursor | `~/.cursor/hooks.json` | `~/.agentic-workflow/hooks/` (+ `adapters/`, `lib/locks.sh`) | `providers/cursor/install-hooks.sh` |

Every Codex and Cursor entry the toolkit writes ends in `# aw:<id>`. `config/lib/merge-hook.sh`
(`merge_hook` for the nested Claude/Codex shape, `merge_hook_flat` for Cursor's flat shape) only
ever replaces or removes entries carrying its own tag. Lever installers accept
`--provider claude|codex|cursor` (default `claude`, behavior unchanged). A lever with no
equivalent event prints `skipped for <provider>` and writes nothing.

Codex only runs hooks you have trusted. After installing, open `codex`, run `/hooks`, and trust
the `aw:*` entries.

## Mapping

| Canonical hook | Claude Code | Codex | Cursor |
|----------------|-------------|-------|--------|
| `block-destructive.sh` | PreToolUse `Bash` | PreToolUse `^(Bash\|exec_command)$` | `beforeShellExecution` |
| `block-push-main.sh` | PreToolUse `Bash` | PreToolUse `^(Bash\|exec_command)$` | `beforeShellExecution` |
| `detect-secrets.sh` | PreToolUse `Bash` | PreToolUse `^(Bash\|exec_command)$` | `beforeShellExecution` |
| `rtk-rewrite.sh` | PreToolUse `Bash` (`updatedInput`) | PreToolUse (`updatedInput`; dropped when the shell input isn't `{command: string}`) | `preToolUse` `Shell` (`updated_input`, merged over the original input) |
| `git-context.sh` | SessionStart | SessionStart (plain text becomes developer context) | `sessionStart` → `additional_context` |
| `prism-context.sh` | SessionStart | SessionStart | `sessionStart` → `additional_context` |
| `judge-health.sh` | SessionStart (`scripts/install-judge-hooks.sh`) | SessionStart (`install-judge-hooks.sh --provider codex`) | `sessionStart` (`install-judge-hooks.sh --provider cursor`) |
| `sindri-nudge.sh` | SessionStart (`install-sindri-hooks.sh`) | SessionStart (`--provider codex`) | `sessionStart` → `additional_context` (`--provider cursor`) |
| `context-guard.sh` | PostToolUse `.*` | PostToolUse `.*`: reads the rollout's latest `token_count.last_token_usage.input_tokens` | **unmapped**: Cursor transcripts carry no token usage, and no hook reports context size per tool call (`preCompact` fires only at compaction) |
| `done-gate.sh` | Stop | Stop: reads `last_assistant_message`, exit 2 → continuation reason on stderr | `stop`: last assistant text from the transcript; exit 2 → `followup_message`; `stop_hook_active = loop_count > 0` |
| `meta-stop-gate.sh` | Stop (only for the session `aw-meta role-set` registered) | **unmapped**: Codex has no cross-session messages to owe replies for (a Codex lane uses `meta-lane-gate.sh`) | **unmapped**: no cross-session messages (a Cursor lane uses `meta-lane-gate.sh`) |
| `meta-outbox.sh` | PostToolUse `SendMessage` | **unmapped**: as above | **unmapped**: as above |
| `meta-lane-gate.sh` | **unmapped**: a Claude lane has `SendMessage` | UserPromptSubmit (plain stdout = developer context: the lane's inbox) and Stop (`last_assistant_message`; exit 2 → the stop is blocked, reason on stderr), installed by `scripts/install-meta.sh --provider codex`; only in a lane (`AW_META_TEAM` and `AW_META_LANE`), else exit 0 | `sessionStart` and `postToolUse` → `additional_context` (the inbox), `stop` → `followup_message` while a reply is owed (capped by `loop_limit`, default 5; the adapter reads the last assistant text from the transcript), `--provider cursor`; the same pass-through |
| `session-id-report.sh` | **unmapped**: Claude lanes choose their id | SessionStart: writes `session_id` to `lane-sessions/<lane>` (lane) or `$AW_SINDRI_HOME/launches/<id>.session-id` (`AW_SINDRI_LAUNCH`), `--provider codex` | `sessionStart` (`conversation_id` is mapped to `session_id`), `--provider cursor`; exits 0 at once outside a lane or a Sindri launch |
| `fleet-state.sh` | SessionStart, UserPromptSubmit, PermissionRequest, Notification, Stop, SessionEnd and PostToolUse `.*` (`scripts/install-fleet.sh`, `./setup.sh --with-fleet`; CO-1): writes `$AW_FLEET_DIR/claude-code/<sessionId>.json`, advisory, always exit 0 | **unmapped until CO-1b** (the hook already maps `AW_HOOK_PROVIDER=codex` to the `codex` directory): SessionStart, UserPromptSubmit, PermissionRequest, PostToolUse, Stop, Interrupt, SessionEnd | **unmapped until CO-1b**: `sessionStart`, `beforeSubmitPrompt`, `stop` (no asking state exists) |
| `done-gate-annotate.sh` | PostToolUse `Agent` | **unmapped**: `spawn_agent` returns at once with an agent id. The result comes later through `wait_agent`, with no link back to the brief | **unmapped**: `subagentStop` has no dispatch id to look up the brief, and it can't annotate the parent's tool result |
| `scope-gate.sh` | PreToolUse `Agent` | PreToolUse `^(Agent\|spawn_agent)$`: `message`→`prompt`, `agent_type`→`subagent_type`, `task_name`→`name`/`description`, `turn_id`→`prompt_id` | `subagentStart` (can block): `task`→`prompt`, `tool_call_id`→`tool_use_id` |
| `subagent-start-map.sh` | SubagentStart | SubagentStart (`turn_id`→`prompt_id`, the same value scope-gate saved) | not needed: `subagentStart` gives scope-gate the dispatch's `tool_call_id` directly |
| `trait-check.sh` | PostToolUse `Edit\|Write\|MultiEdit\|NotebookEdit` + Stop (`scripts/install-trait-check.sh`, opt-in) | PostToolUse `^(apply_patch\|Edit\|Write)$`: files from the `*** Add/Update File:` / `*** Move to:` lines of the patch; Stop: exit 2 → continuation reason on stderr | `postToolUse` (no matcher; the hook keeps edit/write tools) → `additional_context`; `stop` → `followup_message`, `stop_hook_active = loop_count > 0` |
| `external-write-guard.sh` | PreToolUse `.*` | PreToolUse `.*` | `beforeShellExecution` (gh pr merge / comment / review) + `beforeMCPExecution` (tool renamed `mcp__<server>__<tool>`) |
| `turn-origin.sh` | UserPromptSubmit | UserPromptSubmit | `beforeSubmitPrompt` → `{continue: true}` |
| `prompt-sort.sh` | UserPromptSubmit (`scripts/install-judge-hooks.sh`, timeout 6s) | UserPromptSubmit: the adapter forwards plain stdout as developer context | **unmapped**: `beforeSubmitPrompt` output is `{continue: true}` only, it cannot carry context |
| `send-gate.sh` | PreToolUse `SendMessage` | **unmapped**: nothing flushes the outbox before idle (no TeammateIdle), and `send_message`'s argument shape can't be checked against real input yet | **unmapped**: no teammate message tool |
| `record-teammate-name.sh` | PreToolUse `Agent` | **unmapped** (only consumed by wake gating) | **unmapped** |
| `outbox-flush.sh` | TeammateIdle | **unmapped**: no TeammateIdle event | **unmapped**: no TeammateIdle event |
| `probe-log.sh` | UserPromptSubmit, Stop, SubagentStop, TeammateIdle, SubagentStart, Pre/PostToolUse `Agent\|SendMessage` | same minus TeammateIdle; matcher `Agent\|spawn_agent\|send_message`; logs to `probe/codex/` | `beforeSubmitPrompt`, `stop`, `subagentStop`, `subagentStart`, pre/postToolUse `Task` via `--raw`; logs to `probe/cursor/` |

Canonical-script changes needed for the mapping (Claude Code behavior is unchanged in each case):
- `done-gate.sh` falls back to `last_assistant_message` when there's no readable `transcript_path`.
- `context-guard.sh` falls back to Codex `token_count` lines when the transcript has no Claude
  `assistant` line.
- `external-write-guard.sh` also matches `mcp__linear__…` / `mcp__slack__slack_send_…`. These are
  the server names Codex and Cursor users register.
- `probe-log.sh` takes an optional provider argument and writes to `probe/<provider>/`.
- `meta-lane-gate.sh` and `session-id-report.sh` are new canonical hooks with no Claude registration: they exist for Codex and Cursor lanes and read only fields every adapter already supplies (`hook_event_name`, `session_id`, `stop_hook_active`, `last_assistant_message`).

## Protocol facts the adapters rely on (verified 2026-09-28)

**Codex 0.158.0** (`codex features list` → `hooks stable true`; the event names below are also in
the binary's `HookStateToml` strings). Docs: <https://learn.chatgpt.com/docs/hooks> (redirected from
<https://developers.openai.com/codex/hooks>).
- Hook config goes in `~/.codex/hooks.json` or inline `[hooks]` in `~/.codex/config.toml`, plus
  `<repo>/.codex/`. Its shape is Claude's: `{hooks: {Event: [{matcher, hooks: [{type: "command", command, timeout}]}]}}`.
  Commands run with the session `cwd`.
- Events: PreToolUse, PermissionRequest, PostToolUse, PreCompact, PostCompact, SessionStart,
  SessionEnd, UserPromptSubmit, SubagentStart, SubagentStop, Stop, Interrupt. There is no TeammateIdle.
- Common stdin fields: `session_id`, `transcript_path`, `cwd`, `hook_event_name`, `model`,
  `permission_mode`. Turn events add `turn_id`. Stop and SubagentStop carry `stop_hook_active` and
  `last_assistant_message`.
- Tool names: shell is `Bash` (unified exec also `exec_command`), `apply_patch` (also matches
  `Edit`/`Write`), MCP is `mcp__<server>__<tool>`, and `spawn_agent` matches `Agent`.
- Output: exit 2 blocks and **reads the reason from stderr**. PreToolUse JSON supports
  `permissionDecision: allow|deny`, `permissionDecisionReason`, `additionalContext`, and
  `updatedInput` (command rewrite). Stop and SubagentStop accept JSON only (`decision: "block"` +
  `reason`, or exit 2). Plain-text stdout on SessionStart, UserPromptSubmit, and SubagentStart
  becomes developer context.
- Non-managed hooks need an explicit trust step (`/hooks`). The trust is recorded against the
  hook's hash.

**Cursor** (`cursor-agent` 2026.09.02). Docs: <https://cursor.com/docs/agent/hooks>.
- Hook config goes in `~/.cursor/hooks.json` (and `<project>/.cursor/hooks.json`, enterprise,
  team). Its shape is `{version: 1, hooks: {event: [{command, matcher?, timeout?, loop_limit?, failClosed?}]}}`,
  with each entry flat and no nested `hooks` array. **User hooks run from `~/.cursor/`**, so the
  adapter `cd`s to `cwd` → `workspace_roots[0]` → `$CURSOR_PROJECT_DIR`.
- Common stdin fields: `conversation_id`, `generation_id`, `model`, `hook_event_name`,
  `workspace_roots`, `transcript_path`. Env vars: `CURSOR_PROJECT_DIR`, `CURSOR_TRANSCRIPT_PATH`.
- Events used: `beforeShellExecution` (`command`), `preToolUse` (`tool_name` Shell/Read/Write/Task/MCP:…,
  `tool_input`), `postToolUse`, `beforeMCPExecution` (`tool_name`, `mcp_server_name`, JSON-string
  `tool_input`), `beforeSubmitPrompt`, `sessionStart`, `stop` (`status`, `loop_count`, **no last
  message**), `subagentStart` (`subagent_id`, `subagent_type`, `task`, `tool_call_id`; can block),
  and `subagentStop`.
- Output: permission hooks return `{permission: allow|deny|ask, user_message, agent_message}`.
  `preToolUse` can also return `updated_input`. `allow` doesn't bypass Cursor's own approval.
  **Invalid or missing JSON blocks the action**, so the adapter always prints valid JSON and falls
  back to `allow` on its own errors. `beforeSubmitPrompt` returns `{continue}`, `stop` and
  `subagentStop` return `{followup_message}` (capped by `loop_limit`, default 5), and `sessionStart`
  and `postToolUse` return `{additional_context}`. Exit 2 means deny.

Not yet confirmed against a live session (documented only): whether Cursor applies
`preToolUse.updated_input` to Shell, and the exact `spawn_agent` / `exec_command` argument names
Codex passes in `tool_input`. The adapters accept both spellings. Confirm with
`scripts/probe.sh --provider <codex|cursor> on`.

## Fail-open logging

When an adapter falls back to allow on its own error it logs a `HOOK-FAILOPEN` line (hook
`codex-adapter` or `cursor-adapter`) through `aw_adapter_failopen` in `common.sh`: `empty-input` / `input-unparsable` (the host
payload, checked before it is normalized to `{}`), `args-invalid` (no hook script argument),
`payload-unparsable`, `hook-script-missing`, `hook-exit-<N>` (the
canonical hook exited with neither 0 nor 2), and `result-unparsable` (Cursor only). The log is
`$AW_STATE_DIR/logs/failopen.log`, not the hooks install dir. The output to the host is unchanged. See "Fail-open logging" in `.agents/rules/hooks.md`.

## Tests

`config/hooks/tests/codex-adapter.test.sh`, `config/hooks/tests/cursor-adapter.test.sh`
(fixtures in `config/hooks/tests/fixtures/`), `config/hooks/tests/provider-install-hooks.test.sh`, and `config/hooks/tests/failopen.test.sh`.
All of them run under a temp `HOME`.
