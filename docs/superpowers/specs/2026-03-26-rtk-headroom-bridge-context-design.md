# Design: rtk + headroom + bridge-context integration

**Date:** 2026-03-26
**Status:** Approved

## Overview

Integrates three token-efficiency tools into the agentic-workflow setup so every user who runs `./setup.sh` gets them automatically:

- **rtk** — CLI proxy that rewrites bash commands (git, test, build) to compressed equivalents, saving 60–90% tokens on command output
- **headroom** — ML-based content compressor registered as an MCP server, compressing file reads, API responses, and structured data on demand
- **bridge-context** — New SessionStart hook that auto-injects the current repo's memory graph context (recent decisions, topics) at the start of every Claude Code session

MemStack was considered and rejected — the existing agentic-bridge already exceeds it in every dimension (richer graph model, hybrid FTS5+KNN search, MCP tools, dashboard).

## Architecture

```
Session starts
      │
      ▼
[SessionStart: git-context.sh]      (existing)
[SessionStart: bridge-context.sh]   ← NEW: injects repo memory context

Claude processes session
      │
      ├─ Bash tool call
      │      │
      │      ▼
      │  PreToolUse Bash chain:
      │  1. block-destructive.sh    (existing)
      │  2. block-push-main.sh      (existing)
      │  3. detect-secrets.sh       (existing)
      │  4. rtk-rewrite.sh          ← NEW: rewrites eligible commands
      │
      ├─ MCP: headroom_compress / headroom_retrieve / headroom_stats   ← NEW
      └─ MCP: search_memory / traverse_memory / get_context / ...      (existing)
```

Safety hooks run first on the original command; rtk rewrites only after all safety checks pass.

## Component 1: rtk

**Source:** https://github.com/rtk-ai/rtk
**What it is:** Single Rust binary. Rewrites bash commands to compressed equivalents before Claude sees the output.

### Installation (setup.sh — fatal on failure)

```bash
# macOS
brew install rtk

# Linux
curl -fsSL https://raw.githubusercontent.com/rtk-ai/rtk/refs/heads/master/install.sh | sh

# Verify
rtk --version || { echo "FATAL: rtk not found after installation."; exit 1; }
```

### Hook: `config/hooks/rtk-rewrite.sh`

- Reads `{"tool_name":"Bash","tool_input":{"command":"..."}}` from stdin
- Checks command against rewrite-eligible prefixes
- If matched, outputs modified tool input JSON and exits 0
- If unmatched, exits 0 unchanged (passthrough)

**Rewrite patterns:**
| Original | Rewritten |
|----------|-----------|
| `git status` / `git log` / `git diff` / `git push` / `git show` | `rtk git <sub>` |
| `vitest run` | `rtk vitest run` |
| `npm test` / `npm run test` | `rtk npm test` |
| `npx tsc` / `tsc` | `rtk tsc` |
| `eslint` / `npx eslint` | `rtk lint` |
| `cargo test` / `cargo build` | `rtk cargo <sub>` |
| `next build` | `rtk next build` |

**Hook output format:**
```json
{
  "hookSpecificOutput": {
    "permissionDecision": "allow",
    "updatedInput": { "command": "rtk git status" }
  }
}
```

### setup.sh changes

- New `=== Installing rtk ===` section (fatal on failure)
- Copy `config/hooks/rtk-rewrite.sh` → `~/.claude/hooks/rtk-rewrite.sh`
- Extend the idempotent Bash hook jq command to include `rtk-rewrite.sh` as 4th entry in the hook array

The existing jq replace pattern in setup.sh covers this cleanly — replace the entire Bash matcher entry with the updated 4-hook version.

## Component 2: headroom

**Source:** https://github.com/chopratejas/headroom
**What it is:** Context optimization layer. Compresses tool outputs, file reads, API responses using AST-aware and ML-based compression. Exposes MCP tools for on-demand use.

### Installation (setup.sh — fatal on failure)

```bash
# Prerequisite check
python3 --version || { echo "FATAL: Python 3 required. Install Python 3 and re-run."; exit 1; }
pip3 --version   || { echo "FATAL: pip3 required. Install pip and re-run."; exit 1; }

# Install (full tier — includes ML compression)
pip3 install "headroom-ai[all]" || { echo "FATAL: headroom installation failed."; exit 1; }

# Verify
headroom --version || { echo "FATAL: headroom not found after installation."; exit 1; }
```

### MCP Registration

```bash
# Claude Code
claude mcp add --scope user headroom -- headroom mcp serve \
  2>/dev/null || echo "WARN: headroom already registered (or claude CLI not found)"

# Codex
codex mcp add headroom -- headroom mcp serve \
  2>/dev/null || echo "WARN: headroom Codex registration skipped"
```

### MCP Tools (3)

| Tool | Purpose |
|------|---------|
| `headroom_compress` | Compress content before storing or passing to model |
| `headroom_retrieve` | Recover full original from a compressed reference |
| `headroom_stats` | Show token savings for the current session |

### `.claude/rules/mcp-servers.md` addition

New row in the servers table:

| Server | Purpose | Use instead of |
|--------|---------|----------------|
| `headroom` | Content compression — compress large file reads, API responses, structured data | Reading large files raw when token budget is tight |

## Component 3: bridge-context SessionStart Hook

### Hook: `config/hooks/bridge-context.sh`

Fires at session start. Silently no-ops if the bridge isn't running — the bridge is started separately via `start.sh` and may not be up at session open.

**Logic:**
1. Derive repo slug (same pattern used in skills: `git remote get-url origin` → `org-repo`)
2. Check bridge health: `GET http://localhost:3100/health` — exit 0 silently if unreachable
3. Query context: `GET http://localhost:3100/memory/context?query=recent+decisions+tasks+topics&repo=<SLUG>&max_tokens=800&agent=session-start`
4. Format response sections as markdown and print to stdout

**Output format (example):**
```
=== Project Memory ===
[Decision] Use Fastify 5 for HTTP layer (2 days ago)
[Topic] Memory graph ingestion pipeline
[Topic] Design system tokens
```

**Errors:**
- Bridge unreachable → silent exit 0 (no output)
- Bridge returns empty context → silent exit 0
- Malformed response → silent exit 0 (never block session start)

### setup.sh changes

- Copy `config/hooks/bridge-context.sh` → `~/.claude/hooks/bridge-context.sh`
- Append SessionStart entry (idempotent: check for `bridge-context` before adding, same pattern as `git-context.sh`)

## Documentation Updates

### `README.md`

**Tagline (line 3):** Add rtk + headroom to the description.

**Prerequisites section:** Add three new hard prerequisites:
- `rtk` — token-compressing CLI proxy (`brew install rtk` on macOS)
- Python 3 + pip — required for headroom
- `headroom` — context optimization layer (`pip install headroom-ai[all]`)

**Setup script description:** Add bullets:
- Installs rtk (Homebrew on macOS, install script on Linux) and wires `rtk-rewrite.sh` into the Bash hook chain
- Installs headroom (`pip install headroom-ai[all]`) and registers the `headroom` MCP server with Claude Code and Codex
- Installs `bridge-context.sh` SessionStart hook for automatic repo memory injection at session start

### `.claude/rules/hooks.md`

Add two rows to the hooks table:

| Hook | Type | Matcher |
|------|------|---------|
| `rtk-rewrite.sh` | PreToolUse | `Bash` |
| `bridge-context.sh` | SessionStart | — |

Add a note that `rtk-rewrite.sh` runs 4th in the Bash chain (after all safety hooks), so safety checks always see the original command.

### `CLAUDE.md`

Update tagline (line 3) to reflect the new tooling.

## New Files

| File | Purpose |
|------|---------|
| `config/hooks/rtk-rewrite.sh` | Rewrites eligible bash commands to `rtk <command>` |
| `config/hooks/bridge-context.sh` | Injects repo memory context at session start |

## Modified Files

| File | Change |
|------|--------|
| `setup.sh` | Three new sections: rtk install, headroom install, bridge-context hook |
| `README.md` | Tagline, prerequisites, setup description |
| `.claude/rules/hooks.md` | Two new hook rows + ordering note |
| `.claude/rules/mcp-servers.md` | headroom row in servers table |
| `CLAUDE.md` | Tagline update |

## Non-Goals

- No MemStack integration (redundant with agentic-bridge)
- No per-prompt UserPromptSubmit hook for bridge retrieval (latency cost not worth it; MCP tools serve on-demand retrieval)
- No `rtk init -g` delegation (would clobber our hook setup; we own the hook script directly)
- No headroom proxy-mode wrapping of `start.sh` (MCP tool mode is sufficient)
