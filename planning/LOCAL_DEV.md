# Local Development Guide

## Prerequisites

| Requirement | Minimum Version | Purpose |
|-------------|----------------|---------|
| .NET SDK | Pinned in `global.json` | Builds the C# binaries (`setup.sh` installs it when missing) |
| Node.js | >= 22 (`.nvmrc`) | `dembrandt`, `prism-mcp`, `xcodebuildmcp` and the schema checkers only; no toolkit package is built with it |
| GitHub CLI (`gh`) | Latest | Required by review skills for PR interaction |
| Claude Code, Codex, or Cursor CLI | Latest | At least one agent host for skills and MCP server registration |

## Initial Setup

### 1. Clone the Repository

```bash
git clone https://github.com/jthorare/agentic-workflow.git ~/repos/agentic-workflow
cd ~/repos/agentic-workflow
```

### 2. Run the Setup Script

```bash
./setup.sh                                  # detect installed provider CLIs
./setup.sh --providers claude,codex,cursor  # or choose explicitly
```

The setup script performs the following (per-provider steps live in `providers/<name>/`; see `planning/PROVIDERS.md`):

1. **Symlinks skills** into each selected provider's skills directory, creates the stable `~/.agentic-workflow/toolkit` symlink, and writes the `~/.agentic-workflow/providers` registry.
   - If a directory already exists (not a symlink), it prompts before replacing.

2. **Copies config files** to `~/.claude/` (Claude Code only):
   - `settings.json` -- only if no existing file (will not overwrite).
   - `mcp.json` -- only if no existing file (will not overwrite).
   - If files exist, it prints a `diff` command for manual comparison.

### 3. MCP Servers

`setup.sh` registers `serena`, `prism-mcp` and, on macOS, `xcodebuildmcp` with every selected provider, and removes old `agentic-bridge` and `headroom` registrations on re-run. Agents hand off through Prism handoffs (`session_save_handoff` / `session_load_context`), judge's outbox and teammate names (agent teams), and Sindri's ledger; there is no bridge server.

## Running Tests

```bash
cd ~/repos/agentic-workflow
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8
bash scripts/ci/bash-tests.sh
```

## Building

```bash
dotnet build -warnaserror               # whole solution, zero warnings
scripts/install-cs.sh --publish         # publish the C# binaries into $AW_STATE_DIR/bin/cs/<stamp>/
scripts/install-cs.sh --link            # point bin/cs/current at the newest build and write the ~/.local/bin wrappers
```

The .NET SDK is pinned in `global.json`; `setup.sh` installs it when missing.

## Project Structure

```
agentic-workflow/
├── skills/                    # Native skills (symlinked into each provider's skills dir)
│   ├── review/                # Multi-agent PR review
│   ├── postReview/            # GitHub comment publisher
│   ├── addressReview/         # Review fix implementer
│   └── enhancePrompt/         # Context-aware prompt rewriter
├── bootstrap/                 # Repo documentation generator skill
├── config/                    # Settings & MCP config archive
│   ├── settings.json
│   └── mcp.json
├── src/ · tests/              # The C# build: judge, Sindri, evidence, bugfix, packets, readiness, meta, forge
├── setup.sh                   # One-command setup script
└── README.md
```

## Common Development Tasks

### Adding a New Skill

1. Create a directory under `skills/` with the skill's name.
2. Add the skill name to the `for` loop in `setup.sh` so it gets symlinked on setup.
3. Re-run `./setup.sh` to install the symlink.
