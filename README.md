# Agentic Workflow

A portable, provider-agnostic workflow toolkit for AI coding agents. It works the same way in **Claude Code**, **Codex**, and **Cursor**: 53 native skills plus 3 fetched external design packs (impeccable, emil-design-eng, taste-skill), a repo bootstrapper, safety hooks, a cheap-decision judge, a cost/involvement scorer, and a token-efficiency tool (the rtk command rewriter).

There is one canonical core: skills, hook logic, MCP servers, judge, and scorer. Each provider gets a thin adapter on top. Skills describe *capabilities* ("ask the user", "spawn a subagent", "call an MCP tool"), and each provider maps those to its own tools. See [Providers](#providers) and [`planning/PROVIDERS.md`](planning/PROVIDERS.md).

> **New here?** Start with the onboarding guide: [`ONBOARDING.md`](ONBOARDING.md), or the visual walkthrough in [`docs/onboarding.html`](docs/onboarding.html) (open it in a browser).

> **Invoking skills:** the examples below use `/<name>` (Claude Code, Cursor). In Codex, use `$<name>`. For example, `$review` instead of `/review`.

## Workflow: Product Vision → Ship

This toolkit supports an end-to-end product workflow where **AI sessions replace documents**. The goal is to make GitHub issues the single source of truth, instead of piling up local markdown files.

### Stage 1 — Ideation

```
/withInterview
```

**Human in the loop:** You're in the hot seat. The agent interviews you. It asks questions, challenges assumptions, and surfaces contradictions while you answer in your own words. The output is a coherent problem statement and set of goals distilled from your raw thinking. You don't have to write polished prose yourself.

### Stage 2 — Spec & Design Doc

```
/officeHours [feature or problem]
```

**Human in the loop:** This is a back-and-forth collaboration. The agent proposes requirements and you push back. It drafts the technical design, and you redirect priorities and flag constraints it doesn't know about. Think of it as a YC office hours session: you leave with decisions made, not just options listed. It also gives structure to multi-team collaboration. Product and engineering can align on vision, scope, and trade-offs in a shared session before anyone writes a line of code. The output lands in `~/.agentic-workflow/<repo>/plans/<feature>/`: a canonical `plan.md` handoff plus per-owner docs:

| File | Owner | Contents |
|------|-------|----------|
| `product.md` | Product | Problem statement, EARS requirements, acceptance criteria, success metrics, MVP scope |
| `engineering.md` | Engineering | Current state, approach, architecture decisions, open questions |
| `design-brief.md` | Design | Experience goals, key interactions, UX requirements, design language reference |
| `TASKS.md` | Engineering | Atomic task breakdown with `domain` tags for cross-team visibility |

Each file is a standalone artifact. Everyone leaves the session with a doc they own, not a monolith that nobody owns.

You can optionally pressure-test the outputs before moving on. Run each lens on its own, or use `/autoplan` to run them all in parallel:

```
/productReview    # Founder/product lens: is this the right thing to build?
/archReview       # Engineering lens: is this the right way to build it?
/autoplan         # productReview + archReview + planDesignReview + planDevexReview + cso(plan), in parallel
```

### Stage 3 — Design System & Mockups

```
/design-analyze   # Extract design tokens from reference sites (web or iOS)
/design-language  # Define brand personality and aesthetic direction
/design-shotgun   # Optional: 4–6 mockup variants in parallel to pick a direction
/design-mockup    # Generate HTML or SwiftUI mockup from design language
/design-refine    # Agents self-critique and iterate against the design language
```

**Human in the loop:** Once the first mockup exists, agents enter a self-critique loop. They check whether the mockup reflects the design language, find deviations, and refine on their own. You step in at natural breakpoints to review the current state, direct emphasis ("make the data table the focus, not the sidebar"), and decide when the visual spec is ready to lock. You're the final judge of "good enough to build from." You don't take part in every pixel decision.

These produce `design-tokens.json`, `.impeccable.md`, and per-screen mockups (HTML or SwiftUI) with screenshot baselines that serve as the visual specification.

### Stage 4 — Engineering Roadmap (GitHub Issues)

Create a **multi-phase issue hierarchy** directly from the officeHours output:

1. **Epic issue**: paste the product vision, `product.md`, and the approach section of `engineering.md`
2. **Task issues**: one per entry in `TASKS.md`, each referencing the epic and embedding relevant context
3. **Attach mockups**: link or embed the mockup screenshot so the visual spec lives in the issue

The officeHours MD files are **ephemeral**. Once the context is in GitHub issues, delete or ignore them. The issues become the canonical source of truth: product vision, design language reference, and mockups all in one place, with no local file sprawl.

### Stage 5 — Ship

```
/specToProvenPR   # Turn an approved spec into proven, review-clean PRs
/review           # Multi-agent PR code review
/postReview       # Publish findings to GitHub as batched comments
/addressReview    # Implement fixes with parallel agents
/cso              # Pre-ship security check (OWASP Top 10 + STRIDE)
/shipRelease      # Sync, test, push, open PR → auto-chains /landAndDeploy → /canary → /syncDocs
/weeklyRetro      # Retrospective with shipping streaks
```

**Human in the loop:** Shipping is a loop, not a one-shot. The review agents surface issues and publish them to GitHub. You decide what to fix before merge and what to track as follow-ups. `/addressReview` implements the fixes in parallel, and you review the diff. `/shipRelease` runs the gate checks, and you approve the PR. The retro closes the loop: what shipped, what slipped, and what to carry into next week.

---

## Providers

The toolkit treats Claude Code, Codex, and Cursor as equal hosts. `setup.sh` installs for every provider CLI it detects, or for the ones you name with `--providers`.

### Support matrix

| Feature | Claude Code | Codex | Cursor |
|---------|-------------|-------|--------|
| Native skills + external design packs | Yes | Yes | Yes |
| Invocation | `/<name>` | `$<name>` | `/<name>` |
| Skills installed to | `~/.claude/skills/` | `~/.codex/skills/` | `~/.cursor/skills/` |
| Repo instructions | `CLAUDE.md` (symlink to `AGENTS.md`) + `.claude/rules` (symlink) | `AGENTS.md` (Rules Index) | `AGENTS.md` + `.cursor/rules/*.mdc` (symlinks) |
| Safety hooks (`config/hooks/`) | Native | Via adapter (`config/hooks/adapters/codex.sh`); trust with `/hooks` | Via adapter (`config/hooks/adapters/cursor.sh`) |
| MCP servers (serena, prism-mcp, …) | `claude mcp add --scope user` | `codex mcp add` | merged into `~/.cursor/mcp.json` (may need `cursor-agent mcp enable <name>`) |
| Judge model provider | `claude-cli` | `codex-cli` | `cursor-cli` |
| Scorer transcript source | Yes | Yes | Involvement only (no token/cost data) |
| Statusline + shell integration | Yes | — | — |
| Plugin marketplaces | Yes | — | — |

The per-provider paths, tool names, and hook event names are recorded in [`planning/PROVIDERS.md`](planning/PROVIDERS.md). The capability-to-tool map that skills rely on lives in [`skills/_shared/capabilities.md`](skills/_shared/capabilities.md).

### How it fits together

- **Stable toolkit path.** `setup.sh` creates `~/.agentic-workflow/toolkit` as a symlink to this repo. Skills find shared fragments through `~/.agentic-workflow/toolkit/skills/`, never through a provider's skills directory, so every provider resolves them the same way. That includes the shared preamble (`_preamble.md`, and `_design-preamble.md` for design skills), which each `SKILL.md` references instead of embedding.
- **Provider registry.** `~/.agentic-workflow/providers` lists each installed provider and its skills directory, one per line.
- **Per-provider installers.** Provider-specific logic lives in `providers/<name>/install.sh` (skills, MCP registration, config) and `providers/<name>/install-hooks.sh` (hook wiring).
- **Hooks.** The canonical hook scripts in `config/hooks/` speak the Claude Code hook protocol (JSON on stdin, exit 2 = deny). Claude Code runs them directly. For Codex and Cursor, a small adapter translates each provider's hook input and exit codes to that protocol, so there is only one copy of the safety logic. Lever hooks install with `scripts/install-*.sh --provider <name>`. The event mapping is in [`config/hooks/adapters/README.md`](config/hooks/adapters/README.md).
- **Repo instructions.** `AGENTS.md` and `.agents/rules/` are the only copies. `scripts/sync-rules.sh` symlinks `CLAUDE.md` → `AGENTS.md`, `.claude/rules` → `.agents/rules`, and each `.cursor/rules/<name>.mdc` → `.agents/rules/<name>.md`. It also regenerates the Rules Index table in `AGENTS.md` for Codex. `/bootstrap` produces this same layout in any target repo.

### Per-provider setup

```bash
./setup.sh                                  # install for every provider CLI detected on PATH
./setup.sh --providers claude               # Claude Code only
./setup.sh --providers codex                # Codex only
./setup.sh --providers cursor               # Cursor only
./setup.sh --providers claude,codex,cursor  # explicit list
```

Setup is idempotent, so you can re-run it with a different `--providers` list to add a provider later. Add `--dry-run` to print every change without writing anything.

After setup, a couple of provider-specific steps remain:
- **Codex** only runs hooks you trust. Open `codex`, run `/hooks`, and trust the `aw:*` entries. Do this again after a reinstall that changes a hook command.
- **Cursor** may ask you to approve new MCP servers on first use. Run `cursor-agent mcp enable <name>` for each one.

## Prerequisites

- At least one agent CLI: [Claude Code](https://claude.com/claude-code) (`claude`), [Codex](https://github.com/openai/codex) (`codex`), or [Cursor CLI](https://cursor.com/cli) (`cursor-agent`)
- Node.js >= 22
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running (required for Serena LSP)
- [GitHub CLI (`gh`)](https://cli.github.com/) installed and authenticated (required by review skills)
- [`jq`](https://jqlang.github.io/jq/) installed (required by hooks and the statusline; `brew install jq` on macOS)
- [`rtk`](https://github.com/rtk-ai/rtk): token-compressing CLI proxy (`brew install rtk` on macOS; installed automatically by `setup.sh`)

## Setup

```bash
git clone https://github.com/jthorare/agentic-workflow.git ~/repos/agentic-workflow
cd ~/repos/agentic-workflow
./setup.sh                 # or: ./setup.sh --providers claude,codex,cursor
```

The setup script:
- Checks hard prerequisites (`jq`, Docker) and detects which provider CLIs are installed (or uses `--providers`)
- Creates the `~/.agentic-workflow/toolkit` symlink and writes the `~/.agentic-workflow/providers` registry
- Symlinks native skills, `/bootstrap`, and the external design packs (cloned at pinned commits from `EXTERNAL_PINS.env`) into each selected provider's skills directory
- Installs the safety hooks (`block-destructive.sh`, `block-push-main.sh`, `detect-secrets.sh`, `rtk-rewrite.sh`) and session-context hooks for each provider. Claude Code uses them natively; Codex and Cursor go through their hook adapters.
- Builds the Serena Docker images (base TS/Python image; opt-in C# and Swift extensions) and installs the `serena-docker` wrapper to `~/.local/bin/`
- Registers the MCP servers (`serena`, `prism-mcp`, and `xcodebuildmcp` on macOS) with each selected provider, and removes old `agentic-bridge` and `headroom` registrations and the leftover `mcp-bridge/` build output (the MCP bridge was retired)
- Configures `prism-mcp` (persistent memory, downloaded on first use) with its Mind Palace dashboard at `http://localhost:7180` (`PRISM_DASHBOARD_PORT`). The `prism-context.sh` session-start hook warns if the dashboard is unreachable; `/prismStatus` runs a full health check
- Installs rtk
- **Claude Code only:** copies `settings.json`, installs the statusline and shell integration, and adds plugin marketplaces and plugins

## Contents

### 1. Skills

53 native skills, installed as symlinks into each provider's skills directory. Every skill uses the same text for every provider: steps name a capability, and the running agent uses its host's tool for it (see `skills/_shared/capabilities.md`).

| Stage | Skills |
|-------|--------|
| Ideation & planning | `withInterview`, `enhancePrompt`, `officeHours`, `autoplan`, `productReview`, `archReview`, `planDesignReview`, `planDevexReview` |
| Design | `design-analyze`, `design-language`, `design-evolve`, `design-shotgun`, `design-mockup`, `design-implement`, `design-refine`, `design-verify` (dispatchers auto-detect web/iOS and route to their `-web` / `-ios` sub-skills) |
| Build & verify | `specToProvenPR`, `verify-app` (→ `verify-web`, `verify-ios`), `ui-evidence` |
| C# scaffolds | `new-package`, `new-slice` |
| Review | `review`, `postReview`, `addressReview`, `cso` |
| Debug & QA | `rootCause`, `bugHunt`, `bugReport`, `bugFixOrchestrator`, `testAudit` |
| Ship & operate | `shipRelease`, `landAndDeploy`, `canary`, `syncDocs`, `weeklyRetro`, `prismStatus`, `judge`, `meta` (opt-in: `./setup.sh --with-meta`; Linux and macOS) |
| Repo setup | `bootstrap` |
| Self-evolution | `reflect`, `correct` |

Skills write their artifacts to `~/.agentic-workflow/<repo-slug>/<domain>/`, and downstream skills discover them from there.

### 2. Bootstrap Skill

Run `/bootstrap` (`$bootstrap` in Codex) in any repo to generate its documentation:

- Detects which of 17 Pivot-pattern docs exist (BUSINESS_PLAN, ARCHITECTURE, ERD, etc.)
- Generates missing docs adapted to the target repo's tech stack
- Writes a canonical `AGENTS.md` (a navigation doc, not a reference manual), with `CLAUDE.md` as a symlink to it
- Infers glob-scoped rule files from the repo's structure into `.agents/rules/`, then links them for each provider with `sync-rules.sh` (`.claude/rules`, `.cursor/rules/*.mdc`)
- Handles bare repos, partially documented repos, and well-documented repos

### 3. Agent Handoffs

Agents hand off through Prism handoffs (`session_save_handoff` / `session_load_context`), judge's outbox and teammate names (agent teams), and Sindri's ledger; there is no bridge server.

### 4. Judge and Scorer

- **`judge/`** makes cheap, typed decisions: a rules fast path, then a per-content-class model chain. Model calls go through a headless provider CLI: `claude-cli` (`claude -p`), `codex-cli` (`codex exec`), or `cursor-cli` (`cursor-agent -p`). Any one of them on `PATH` is enough. The order is `providers.agentClis` in the judge config if set; otherwise claude, codex, cursor, with the current host (`AW_PROVIDER`) first. Cursor is slow (~8–13s per call), so on Cursor-only machines text questions often time out and fall back to rules.
- **`scorer/`** produces a daily cost and involvement report from agent transcripts. Each provider has its own transcript source: Claude Code (`~/.claude/projects/`), Codex (`~/.codex/sessions/`), and Cursor (`~/.cursor/projects/`). By default it reads every provider whose directory exists. `--provider claude|codex|cursor|all` narrows that, and `--codex-dir` / `--cursor-dir` override the paths. The report includes a "By provider" section. Cursor data is involvement-only because its transcripts carry no token or cost data. Codex rollouts imported from Claude are skipped so they aren't counted twice. Run `scorer --since 7d` to write a report to `~/.agentic-workflow/scorer/reports/`. Install it with `scripts/install-scorer.sh`.

### 5. Statusline (Claude Code only)

`config/statusline.sh` is an adaptive two-line statusline for Claude Code sessions. `setup.sh` installs it to `~/.claude/statusline.sh` and wires it into `settings.json` when Claude Code is a selected provider.

**Columns (left → right, highest priority leftmost):**

| Column | Description |
|--------|-------------|
| 5h Usage | 5-hour rate-limit percentage + reset time |
| 7d Usage | 7-day rate-limit percentage + reset day |
| Context | Color-coded bar + percentage of context window used |
| Model | Active model name (trimmed) |
| Branch | Current git branch |
| Cost | Session cost in USD |
| Time | Session duration |
| Cache | Cache read hit rate |
| API | API wait percentage |
| Lines | Lines added/removed |

**Adaptive width tiers:** columns drop out automatically as the terminal narrows.

| Tier | Min width | Columns shown |
|------|-----------|---------------|
| FULL | 116 cols | All columns, branch up to 15 chars |
| MEDIUM | 101 cols | No Lines; branch up to 12 chars |
| NARROW | 78 cols | No Lines/Cache/API; 7d % only; narrow context bar |
| COMPACT | 65 cols | 5h % only; narrow context bar; branch up to 10 chars |
| COMPACT-S | < 65 cols | Same as COMPACT but drops Time column |

The statusline reads the size of the tty its own Claude Code process is attached to (found by walking up the process tree), so every window gets its own width and no state is shared between windows. When that read fails it falls back to `~/.claude/terminal_width.d/<tty>`, written by the shell integration from interactive terminals only (never from Claude Code tool shells). `AW_STATUSLINE_DEBUG=1` prints the width source to stderr.

`setup.sh` installs the **shell integration** to `~/.claude/shell-integration.sh` and sources it from `~/.zshrc` / `~/.bashrc`. It keeps `~/.claude/terminal_width.d/<tty>` current and writes `~/.claude/shell_pid.d/<tty>`, so the hooks of a session can send `SIGWINCH` to the shell on its own tty.

## Testing

```bash
cd scorer && npm test
cd judge && npm test
bash providers/tests/install.test.sh
bash scripts/tests/sync-rules.test.sh
bash config/hooks/tests/codex-adapter.test.sh
bash config/hooks/tests/cursor-adapter.test.sh
bash config/hooks/tests/provider-install-hooks.test.sh
bash config/lib/tests/merge-hook.test.sh
bash config/hooks/tests/probe-log.test.sh
scripts/sync-rules.sh --check
```

The full list is under Commands in [`AGENTS.md`](AGENTS.md).

`/* v8 ignore */` annotations are prohibited; write the test instead.

## Repository Layout

```
agentic-workflow/
├── AGENTS.md                # Canonical repo instructions (CLAUDE.md is a symlink to it)
├── .agents/rules/           # Glob-scoped rules, the only copy (.claude/rules, .cursor/rules/*.mdc are symlinks)
├── skills/                  # 53 native skills (+ _shared/ fragments, incl. capabilities.md)
├── bootstrap/               # /bootstrap — repo documentation generator
├── providers/<name>/        # Per-provider installers: install.sh, install-hooks.sh (claude, codex, cursor)
├── config/                  # Settings, MCP config, statusline, hooks (+ hooks/adapters/ for codex, cursor)
├── judge/                   # Cheap typed decisions (rules → model chain via claude/codex/cursor CLI)
├── scorer/                  # Cost/involvement report from provider transcripts
├── scripts/                 # sync-rules.sh, serena-docker, probe.sh, install-*.sh, refresh-external-pins.sh
├── planning/                # Project documentation (see planning/PROVIDERS.md)
├── Dockerfile.serena*       # Serena base image + opt-in C# / Swift extensions
└── setup.sh                 # One-command setup: ./setup.sh [--providers claude,codex,cursor]
```
