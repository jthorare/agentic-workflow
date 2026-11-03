---
name: prismStatus
description: "Use when prism-mcp, the Mind Palace memory server, seems down, slow or empty and the user wants to know whether it is up: dashboard, MCP connection, stored entries, version. A read-only check; not for saving or loading memory."
argument-hint: "[--port <port>]"
allowed-tools: Bash(curl *), Bash(claude mcp *), Bash(codex mcp *), Bash(PORT=*), Bash(SHARED_DIR=*), Bash(source *), Bash(jq *), mcp__prism-mcp__session_health_check, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Prism Status — Verify Mind Palace Health

Quick health check for the prism-mcp Mind Palace.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

> **Preamble exemption:** If the preamble's prism-mcp calls failed, continue anyway — diagnosing that failure is this skill's purpose; Session Close ledger/handoff are exempt for this skill.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Overview

Probes the prism-mcp dashboard HTTP endpoint, the MCP server connection, and the per-repo knowledge base. Returns a single status report; writes no file. Useful at session start (matches the `prism-context.sh` SessionStart hook) and on demand.

## Inputs

- `--port <port>` (optional) — override `PRISM_DASHBOARD_PORT`. Default port comes from the env var, or `7180` if unset.

## Steps

Assign `PORT` explicitly at the top of **every** bash block (shell state does not persist between blocks): `--port` arg if provided, else `$PRISM_DASHBOARD_PORT`, else `7180`.

1. **Dashboard reachability:**
   ```bash
   PORT="${PRISM_DASHBOARD_PORT:-7180}"   # replace with the --port value if one was given
   curl -fsS --max-time 3 -o /dev/null "http://localhost:$PORT/"
   ```
   Capture exit code and body. Report ✓ on 0, ✗ on non-zero.

2. **MCP tool surface:**
   Call `mcp: prism-mcp/session_health_check`. Report ✓ on success, ✗ + error on failure.

3. **Knowledge base population for current repo:**
   Resolve the repo slug via the shared helper:
   ```bash
   SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
   source "$SHARED_DIR/repo-slug.sh"
   echo "repo-slug: $REPO_SLUG"
   ```
   Call `mcp: prism-mcp/session_load_context` with `project=$REPO_SLUG` and `level="minimal"`. Report ✓ "KB has N entries" or ✗ "KB empty".

4. **Version** — structured sources, in order of preference:
   1. The `version` field of the `session_health_check` response from check 2, if present.
   2. The dashboard `/health` JSON body:
      ```bash
      PORT="${PRISM_DASHBOARD_PORT:-7180}"   # replace with the --port value if one was given
      curl -fsS --max-time 3 "http://localhost:$PORT/health" | jq -r '.version // "unknown"'
      ```
   3. Fallback: the host CLI's MCP registration for `prism-mcp`, taking the first strict semver match (`[0-9]+.[0-9]+.[0-9]+`) — `claude mcp get prism-mcp` (Claude Code), `codex mcp get prism-mcp` (Codex), or the `prism-mcp` entry in `~/.cursor/mcp.json` (Cursor):
      ```bash
      { command -v claude >/dev/null && claude mcp get prism-mcp 2>/dev/null; \
        command -v codex >/dev/null && codex mcp get prism-mcp 2>/dev/null; \
        jq -c '.mcpServers["prism-mcp"] // empty' "$HOME/.cursor/mcp.json" 2>/dev/null; } \
        | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1
      ```
   Report the version (e.g., "5.1.0"); "unknown" if all three fail.

5. Summarize with a 4-line block — one line per check (dashboard, MCP surface, KB, version) — each prefixed `✓ ` or `✗ `. If any check failed, add a fix suggestion line at the bottom (re-run setup.sh, check env, etc.).

## Outputs

stdout only. No file written.

## Next steps

- `/officeHours` — if KB is empty, start populating it with a brainstorm
- `/bootstrap` — if you've never run it on this repo, do so to seed planning docs

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/prismStatus-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`prismStatus`), `artifacts` (absolute paths: `[]` (this skill only talks)), `summary` (one line),
plus `healthy` (true when dashboard, MCP server and knowledge base all answer); `dashboard`, `mcp`, `kb` (each `ok` or `fail`, one per check); `version` (the version string, or `unknown`).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
