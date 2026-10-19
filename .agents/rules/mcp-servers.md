---
description: MCP servers registered globally with every provider — available in every session
globs: []
alwaysApply: true
---

# MCP Servers

All servers are registered globally with every installed provider by `setup.sh`:

| Provider | Registration |
|----------|--------------|
| Claude Code | `claude mcp add --scope user` |
| Codex | `codex mcp add` (writes `~/.codex/config.toml`) |
| Cursor | `mcpServers` in `~/.cursor/mcp.json` |

Skill text refers to MCP tools as `mcp: <server>/<tool>`; map that to your host's calling convention via `skills/_shared/capabilities.md`.

| Server | Purpose | Use instead of |
|--------|---------|----------------|
| `serena` | LSP symbol navigation — find definitions, usages, call hierarchy | File search + read for code structure questions |
| `context7` | Up-to-date library/framework documentation | Guessing API shapes from training data |
| `playwright` | Browser automation — click, fill, screenshot, DOM inspection | Manual browser testing |
| `github` | GitHub API — PRs, issues, releases, comments | `gh` CLI for read operations |
| `design-comparison` | Visual diff between implementation and design reference | Manual screenshot comparison |
| `xcodebuildmcp` | iOS Simulator control — build, launch, snapshot_ui, screenshot, tap, swipe | Manual iOS testing |
| `prism-mcp` | Persistent memory, semantic search, conversation visualization (Mind Palace) | Manual context passing between sessions |

### Per-repo servers

Not registered by `setup.sh`, and absent from most sessions.

| Server | Purpose | Registration |
|--------|---------|--------------|
| `sindri` | Sindri's `sindri_*` tools (status, queue, item, timeline, pending decisions, job, `sindri_request`) and the `context_*` tools, served by the local Sindri process at `/sindri/mcp`; text in results is fenced as untrusted | Registered by the owner running `sindri-app mcp register --apply --providers all` (a bare `scripts/sindri-app.sh` prints that command once the control plane's port is known) or `sindri mcp register --apply --providers all` in a work repo, per repo where the provider allows: Claude Code `claude mcp add --scope local`, Cursor the checkout's `.cursor/mcp.json`, Codex the checkout's `.codex/config.toml` (plus a `projects."<checkout>".trust_level = "trusted"` row in the user's Codex config, never overwriting another level); each with an `X-Sindri-Repo` header. Idempotent, re-registered when the port changes. Never by `setup.sh` or a provider installer; an agent session is refused (`SND-AUTH-002`). `sindri doctor` warns on a user-wide entry |

## When to use Serena vs search/read

| Task | Tool |
|------|------|
| Where is function/class X defined? | `serena: find_symbol` |
| What calls function X? | `serena: get_symbol_usages` |
| What does this file export? | `serena: get_document_symbols` |
| Navigate to a type definition | `serena: find_symbol` |
| Search for a string, log message, or config value | Search files (`rg` / Grep) |
| Read full file context | Read file |

## When to use XcodeBuildMCP

| Task | Tool |
|------|------|
| Check what's on screen (structural) | `xcodebuildmcp: snapshot_ui` |
| Check layout/visual appearance | `xcodebuildmcp: screenshot` |
| Tap a button or UI element | `xcodebuildmcp: tap` |
| Swipe / scroll | `xcodebuildmcp: swipe` |
| List available simulators | `xcodebuildmcp: list_sims` |
| Launch app on simulator | `xcodebuildmcp: launch_app_sim` |

Use `snapshot_ui` by default — it returns a structured view hierarchy that's faster and cheaper to parse than screenshots. Only use `screenshot` (via `/verify-ios --visual` or `/design-verify-ios`) when pixel-level visual inspection is needed.

Serena requires `.serena/project.yml` in the repo root. Run `/bootstrap` on any repo that
lacks it — the skill detects languages and writes the config automatically.

## Claude Code Settings

`disableBypassPermissionsMode: "disable"` in `.claude/settings.json` prevents `--dangerously-skip-permissions` mode. This value is a string `"disable"` (not a boolean) per Claude Code 1.x settings schema.
