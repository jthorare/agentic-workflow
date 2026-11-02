# Agentic Workflow Architecture

## System Overview

Agentic Workflow (repo: `agentic-workflow`) is a portable, provider-agnostic workflow toolkit for AI coding agents. It supports **Claude Code**, **Codex**, and **Cursor** as equal hosts. The canonical core has these parts:

- 55 native skills spanning the full development lifecycle (planning, design, review, debugging, QA, shipping, retrospectives)
- a documentation bootstrapper skill
- canonical safety hooks
- the C# binaries: `judge` and the other tool ports, and Sindri (control plane, workflow engine, context engine), published by `scripts/install-cs.sh`
- a centralized output directory for cross-skill artifact sharing

A thin **provider adapter layer** (see below and `planning/PROVIDERS.md`) installs that core into each provider. Skills are symlinked into each installed provider's skills directory. They are invoked as `/<name>` in Claude Code and Cursor and as `$<name>` in Codex. Agents hand off through Prism handoffs (`session_save_handoff` / `session_load_context`), judge's outbox and teammate names (agent teams), and Sindri's ledger; there is no bridge server.

```mermaid
graph TD
    subgraph "Agent Session — Planning"
        User --> |"/officeHours"| OfficeHours[officeHours skill]
        User --> |"/productReview"| ProductReview[productReview skill]
        User --> |"/archReview"| ArchReview[archReview skill]
    end

    subgraph "Agent Session — Review"
        User --> |"/review 42"| Review[review skill]
        User --> |"/postReview"| PostReview[postReview skill]
        User --> |"/addressReview"| AddressReview[addressReview skill]
    end

    subgraph "Agent Session — QA & Debug"
        User --> |"/rootCause"| RootCause[rootCause skill]
        User --> |"/bugHunt"| BugHunt[bugHunt skill]
        User --> |"/bugReport"| BugReport[bugReport skill]
    end

    subgraph "Agent Session — Ship & Retro"
        User --> |"/shipRelease"| ShipRelease[shipRelease skill]
        User --> |"/syncDocs"| SyncDocs[syncDocs skill]
        User --> |"/weeklyRetro"| WeeklyRetro[weeklyRetro skill]
    end

    subgraph "Agent Session — Utilities"
        User --> |"/enhancePrompt"| EnhancePrompt[enhancePrompt skill]
        User --> |"/bootstrap"| Bootstrap[bootstrap skill]
    end

    subgraph "Agent Session — Design"
        User --> |"/design-analyze"| DesignAnalyze[design-analyze skill]
        User --> |"/design-language"| DesignLanguage[design-language skill]
        User --> |"/design-evolve"| DesignEvolve[design-evolve skill]
        User --> |"/design-mockup"| DesignMockup[design-mockup skill]
        User --> |"/design-implement"| DesignImplement[design-implement skill]
        User --> |"/design-refine"| DesignRefine[design-refine skill]
        User --> |"/design-verify"| DesignVerify[design-verify skill]
        DesignAnalyze --> DesignLanguage
        DesignEvolve --> DesignLanguage
        DesignEvolve --> DesignAnalyze
        DesignLanguage --> DesignMockup
        DesignMockup --> DesignImplement
        DesignImplement --> DesignRefine
        DesignRefine --> DesignVerify
    end

    Review --> |"gh pr diff"| GitHub
    Review --> |"spawn subagent"| Triage[Triage Subagent]
    Triage --> Reviewers[Parallel Reviewer Subagents]

    OfficeHours --> OutputDir
    ProductReview --> OutputDir
    ArchReview --> OutputDir
    Reviewers --> OutputDir
    RootCause --> OutputDir
    BugHunt --> OutputDir
    BugReport --> OutputDir
    ShipRelease --> OutputDir
    SyncDocs --> OutputDir
    WeeklyRetro --> OutputDir

    OutputDir["~/.agentic-workflow/repo-slug/"]

    PostReview --> |"gh api"| GitHub
    AddressReview --> |"spawn subagent"| Implementers[Parallel Implementer Subagents]
    Implementers --> |"git push"| GitHub
    ShipRelease --> |"gh pr create"| GitHub
```

### Provider Adapter Layer

Everything provider-specific is kept at the edges. The core is written once and each provider gets a thin adapter:

```mermaid
graph LR
    subgraph "Canonical core (this repo)"
        Skills["skills/*/SKILL.md<br/>(capability phrases)"]
        Hooks["config/hooks/*.sh<br/>(canonical protocol)"]
        Rules["AGENTS.md + .agents/rules/"]
        MCPs["MCP servers<br/>(serena, prism-mcp, ...)"]
    end

    Setup["setup.sh --providers ..."] --> PClaude["providers/claude/"]
    Setup --> PCodex["providers/codex/"]
    Setup --> PCursor["providers/cursor/"]

    Skills --> |symlink| PClaude & PCodex & PCursor
    Hooks --> |native| PClaude
    Hooks --> |"adapters/codex.sh"| PCodex
    Hooks --> |"adapters/cursor.sh"| PCursor
    Rules --> |"scripts/sync-rules.sh"| Emitted[".claude/rules/, .cursor/rules/*.mdc,<br/>AGENTS.md rules index, CLAUDE.md stub"]
    MCPs --> |"claude mcp add / codex mcp add / ~/.cursor/mcp.json"| PClaude & PCodex & PCursor
```

- **Installer.** `setup.sh [--providers claude,codex,cursor]` installs for the listed providers, or detects installed CLIs when no flag is given. It runs the shared steps (Serena images, external pack fetch, rtk) once, then calls `providers/<name>/install.sh` and `providers/<name>/install-hooks.sh` for each selected provider.
- **Stable paths.** `~/.agentic-workflow/toolkit` is a symlink to the repo root. Skills resolve shared fragments through `SHARED_DIR=~/.agentic-workflow/toolkit/skills/_shared`, never through a provider's skills directory. `~/.agentic-workflow/providers` records each installed provider and its skills directory (`<name> <skills-dir>` per line).
- **Capabilities, not tools.** Skill text names capabilities (**Ask the user**, **Spawn a subagent**, **Dispatch in parallel**, **Invoke skill**, `mcp: <server>/<tool>`). `skills/_shared/capabilities.md` maps each one to the right tool on each provider. `allowed-tools` frontmatter uses Claude Code names, and the other providers ignore it.
- **Hooks.** `config/hooks/*.sh` speak one protocol (JSON on stdin, exit 2 = deny). Claude Code runs them directly. `config/hooks/adapters/{codex,cursor}.sh` translate each provider's hook payloads, event names, and exit semantics to that protocol, so the safety logic exists once.
- **Repo instructions.** `AGENTS.md` and `.agents/rules/*.md` (frontmatter `description`, `globs`, `paths`, `alwaysApply`) are the only copies. `scripts/sync-rules.sh` symlinks `.claude/rules` → `.agents/rules`, `.cursor/rules/<name>.mdc` → `.agents/rules/<name>.md`, and `CLAUDE.md` → `AGENTS.md`, and it regenerates the Rules Index in `AGENTS.md` for Codex.
- **Judge.** The C# `judge` (`src/Judge`) has `claude-cli`, `codex-cli`, and `cursor-cli` model providers (`docs/csharp/judge.md`).

### Skill Pipeline

Skills are designed to flow into each other in a natural development lifecycle:

```
officeHours → productReview / archReview
    → design-analyze [web|ios] → design-language → design-mockup [web|ios] → design-implement [web|ios] → design-refine → design-verify [web|ios]
    → review → rootCause → bugHunt → shipRelease → syncDocs → weeklyRetro
```

Each skill writes outputs to `~/.agentic-workflow/<repo-slug>/` that downstream skills auto-discover.

## Directory Tree

```
agentic-workflow/
├── AGENTS.md                            # Canonical repo instructions (CLAUDE.md is a symlink to it)
├── .agents/rules/                       # Glob-scoped rules, the only copy (linked by scripts/sync-rules.sh)
├── providers/                           # Per-provider adapters
│   └── <claude|codex|cursor>/           #   install.sh (skills, MCP, config) + install-hooks.sh
├── skills/                              # Native skills (51, plus bootstrap/), shared by all providers
│   ├── review/                          # /review — multi-agent PR review orchestrator
│   │   ├── SKILL.md                     #   skill manifest + 7-step orchestration flow
│   │   ├── triage-prompt.md             #   subagent prompt: classify files → reviewer agents
│   │   └── reviewer-prompt.md           #   subagent prompt: domain-specific code review
│   ├── postReview/                      # /postReview — publish findings to GitHub
│   │   └── SKILL.md                     #   reads review state, posts batched PR reviews
│   ├── addressReview/                   # /addressReview — implement review fixes
│   │   ├── SKILL.md                     #   orchestrator: triage → parallel implementers
│   │   ├── address-triage-prompt.md     #   subagent prompt: group issues → impl agents
│   │   └── implementer-prompt.md        #   subagent prompt: fix code, commit, reply
│   ├── enhancePrompt/                   # /enhancePrompt — context-aware prompt rewriter
│   │   └── SKILL.md                     #   discovers docs, enriches user prompt
│   ├── rootCause/                       # /rootCause — 4-phase systematic debugging
│   │   └── SKILL.md                     #   investigate → analyze → hypothesize → implement
│   ├── bugHunt/                         # /bugHunt — fix-and-verify loop
│   │   └── SKILL.md                     #   3 tiers, atomic commits, regression tests
│   ├── bugReport/                       # /bugReport — read-only health audit
│   │   └── SKILL.md                     #   health scores, bug classification, no fixes
│   ├── testAudit/                       # /testAudit — test authoring gate + low-value test pruning
│   ├── shipRelease/                     # /shipRelease — sync, test, push, PR
│   │   └── SKILL.md                     #   pre-flight → sync → test → push → PR → syncDocs
│   ├── syncDocs/                        # /syncDocs — post-ship doc updater
│   │   └── SKILL.md                     #   README, ARCHITECTURE, CHANGELOG, AGENTS.md, .agents/rules/
│   ├── weeklyRetro/                     # /weeklyRetro — weekly retrospective
│   │   └── SKILL.md                     #   per-person breakdown, shipping streaks, insights
│   ├── officeHours/                     # /officeHours — YC-style brainstorming
│   │   └── SKILL.md                     #   6 forcing questions → design doc
│   ├── productReview/                   # /productReview — founder/product lens review
│   │   └── SKILL.md                     #   4 modes: mvp, growth, scale, pivot
│   ├── archReview/                      # /archReview — engineering architecture review
│   │   └── SKILL.md                     #   mandatory diagrams, edge case analysis
│   ├── design-analyze/                  # /design-analyze — platform dispatcher (routes to -web or -ios)
│   ├── design-analyze-web/              # /design-analyze web — extract design tokens from web reference sites
│   ├── design-analyze-ios/              # /design-analyze ios — extract design tokens from iOS reference apps
│   ├── design-language/                 # /design-language — define brand personality and aesthetic direction
│   ├── design-evolve/                   # /design-evolve — platform dispatcher (routes to -web or -ios)
│   ├── design-evolve-web/               # /design-evolve web — merge new web reference into existing design language
│   ├── design-evolve-ios/               # /design-evolve ios — merge new iOS reference into existing design language
│   ├── design-mockup/                   # /design-mockup — platform dispatcher (routes to -web or -ios)
│   ├── design-mockup-web/               # /design-mockup web — generate HTML mockup using design language
│   ├── design-mockup-ios/               # /design-mockup ios — generate SwiftUI mockup using design language
│   ├── design-implement/                # /design-implement — platform dispatcher (routes to -web or -ios)
│   ├── design-implement-web/            # /design-implement web — generate React/Next.js production code from mockup
│   ├── design-implement-ios/            # /design-implement ios — generate SwiftUI production code from mockup
│   ├── design-refine/                   # /design-refine — dispatch Impeccable refinement commands
│   ├── design-verify/                   # /design-verify — platform dispatcher (routes to -web or -ios)
│   ├── design-verify-web/               # /design-verify web — screenshot diff web implementation vs mockup
│   ├── design-verify-ios/               # /design-verify ios — screenshot diff iOS implementation vs mockup
│   ├── verify-app/                      # /verify-app — platform dispatcher (routes to verify-web or verify-ios)
│   ├── verify-web/                      # /verify-web — standalone web app verification using Playwright
│   ├── verify-ios/                      # /verify-ios — standalone iOS app verification using XcodeBuildMCP
│   ├── _preamble.md                     # Shared preamble — every SKILL.md references it
│   ├── _design-preamble.md              # Shared design preamble — design skills reference it
│   └── _shared/                         # Shared fragments, resolved via ~/.agentic-workflow/toolkit/skills/_shared
│       ├── capabilities.md             # Capability → per-provider tool map
│       └── skill-lock.sh               # Shared lock script sourced by platform sub-skills to prevent concurrent runs
├── bootstrap/                           # /bootstrap — repo documentation generator
│   └── SKILL.md                         #   audits 17 Pivot-pattern docs, generates missing
├── config/                              # Configuration archive (Claude Code settings, MCP config, hooks)
│   ├── settings.json                    #   model, plugins, permissions, statusLine command, PreToolUse + SessionStart hook registrations
│   ├── statusline.sh                    #   adaptive two-line statusline (5 tiers: FULL/MEDIUM/NARROW/COMPACT/COMPACT-S; Live column at ≥141 cols (replaces Lines at 141–155))
│   ├── mcp.json                         #   MCP server registrations (xcodebuildmcp)
│   └── hooks/                           #   Canonical hook scripts (Claude protocol), installed per provider
│       ├── adapters/                    #     codex.sh, cursor.sh — normalize provider hook stdin/exit codes
│       ├── block-destructive.sh         #     PreToolUse — blocks rm -rf, git reset --hard, git push --force, etc.
│       ├── block-push-main.sh           #     PreToolUse — blocks git push to main/master
│       ├── detect-secrets.sh            #     PreToolUse — blocks AWS keys, GitHub tokens, Bearer tokens
│       ├── rtk-rewrite.sh              #     PreToolUse — rewrites eligible bash commands to rtk equivalents for token compression (runs 4th)
│       ├── git-context.sh              #     SessionStart — injects current branch, recent commits, working tree status
│       └── prism-context.sh            #     SessionStart — one-line warning if the prism-mcp dashboard is unreachable
├── src/ · tests/                        # The C# build: judge (claude-cli, codex-cli, cursor-cli), Sindri, evidence, bugfix, packets, readiness, meta, forge
├── scripts/                             # Utility scripts
│   ├── sync-rules.sh                    #   Emit .claude/rules/, .cursor/rules/*.mdc, AGENTS.md rules index
│   └── serena-docker                    #   Serena MCP wrapper — mounts repo into Docker at invocation time
├── Dockerfile.serena                    # Serena base image (TS, Python, Go, Rust)
├── Dockerfile.serena-csharp             # Serena C# extension image (adds OmniSharp + .NET SDK)
├── .serena/                             # Serena LSP per-repo configuration
│   └── project.yml                      #   Language servers, ignored_paths, read_only flag
├── .claude/
│   ├── rules/                           # Generated from .agents/rules/ (auto-loaded by Claude Code)
│   │   ├── csharp.md                    #   C# build: package shape, dependency rules, types, errors, DI, AOT
│   │   ├── csharp-testing.md            #   C# tests: TDD order, lanes and budgets, fakes, builders
│   │   ├── design.md                    #   Design pipeline, artifact formats, design principles
│   │   ├── hooks.md                     #   Hook protocols, PreToolUse/SessionStart patterns, adding new hooks
│   │   ├── mcp-servers.md               #   MCP server usage guide (Serena vs Grep/Read decision table)
│   │   ├── skills.md                    #   Skill structure, preamble format, repo slug, output dirs
│   │   └── testing.md                   #   Test infrastructure, shared helpers, coverage policy
│   └── settings.json                    #   disableBypassPermissionsMode, enabledMcpjsonServers
├── .dockerignore                        # Excludes node_modules, dist, *.db from Docker build context
├── setup.sh                             # One-command installer: setup.sh [--providers claude,codex,cursor]
├── .gitignore                           # Ignores node_modules, dist, *.db, .env, .review-cache
└── README.md                            # Project overview, setup instructions, env vars
```

### Centralized Output Directory

```
~/.agentic-workflow/<repo-slug>/
├── design/           # /design-mockup, /design-verify baselines and diffs
├── reviews/          # /review, /postReview, /addressReview state files
├── investigations/   # /rootCause investigation reports
├── qa/               # /bugHunt and /bugReport reports; /testAudit ledgers in qa/test-audit/
├── plans/            # /officeHours, /productReview, /archReview design docs
├── releases/         # /shipRelease and /syncDocs reports
└── retros/           # /weeklyRetro retrospectives
```

The repo slug is derived from `git remote get-url origin` (e.g., `org-name-repo-name`), falling back to the directory name. This directory persists across sessions and branches, enabling cross-skill artifact discovery.

## Component 1: Skills (skills/, bootstrap/)

### Overview

Forty-four native skills defined as Markdown SKILL.md files with YAML frontmatter. The same files serve every provider. Skills are invoked as `/<name>` (Claude Code, Cursor) or `$<name>` (Codex) and executed as structured workflows. They use the **Spawn a subagent** / **Dispatch in parallel** capabilities for parallel subagents (mapped per provider in `skills/_shared/capabilities.md`) and the `gh` CLI for GitHub API access. Every skill references one shared preamble file (`skills/_preamble.md`) that lists all native skills, points to the centralized output directory, and checks bootstrap status. Seven design pipeline skills (design-analyze, design-language, design-evolve, design-mockup, design-implement, design-refine, design-verify) share a separate design preamble for brand context and design token management. Six of the skills (design-analyze, design-evolve, design-mockup, design-implement, design-verify, and verify-app) are thin platform dispatchers: they **auto-detect** the platform by checking for iOS indicators (Package.swift, *.xcodeproj) or web indicators (package.json with a web framework dependency) and route to the appropriate sub-skill automatically. When detection is ambiguous, the dispatcher asks the user to clarify. Manual sub-skill invocation (e.g., `/design-mockup-web`, `/design-mockup-ios`) is available as an escape hatch. The shared utility `skills/_shared/skill-lock.sh` is sourced by all dispatcher sub-skills to prevent concurrent platform invocations from the same repo.

### Review Pipeline (skills/review/, postReview/, addressReview/)

A three-phase PR review workflow with a shared state file (`~/.agentic-workflow/<repo-slug>/reviews/{number}.json`) as the coordination mechanism:

**Phase 1 — `/review`:** Fetches PR diff and metadata via `gh`, spawns a triage subagent to classify changed files into domain-specific reviewer assignments (from a catalog of 12+ specialist agents like `security-sentinel`, `kieran-typescript-reviewer`, `performance-oracle`). Triage includes SQL safety checks and LLM trust boundary analysis. All reviewers run in parallel (**Dispatch in parallel**). Each returns structured JSON with severity-tagged issues (`blocking`, `issue`, `suggestion`, `nit`) including `diff_position` for inline placement. Results are written to the reviews directory.

**Phase 2 — `/postReview`:** Reads the state file and publishes findings to GitHub as batched PR reviews (one `gh api` call per reviewer agent). Captures posted comment IDs back into the state file. Marks `posted: true`.

**Phase 3 — `/addressReview`:** Reads the state file, fetches any new human comments from GitHub since `reviewed_at`, runs an address-triage subagent to group all unresolved issues by implementation concern, then spawns parallel implementer subagents. Implementers fix code, commit, push, and reply to every comment. The state file is updated with `addressed: true` and commit SHAs. Can be re-run iteratively.

### Investigation & QA (skills/rootCause/, bugHunt/, bugReport/, bugFixOrchestrator/)

**`/rootCause`** — 4-phase systematic debugging: investigate (reproduce), analyze (read source, map call chain), hypothesize (rank 2-3 causes), implement (fix and verify). Auto-freezes scope to the module boundary after analysis to prevent scope creep. Writes investigation report to `investigations/`. With `--investigate-only` it stops after confirming a hypothesis (status `diagnosed`) and never edits code.

**`/bugHunt`** — Fix-and-verify loop with 3 tiers (quick: lint+typecheck, standard: unit+integration, exhaustive: full suite). Makes atomic commits for fixes and regression tests. Retries up to 3 times on verification failure. Writes QA report to `qa/`.

**`/bugReport`** — Read-only health audit. Runs linters, typecheckers, and test suites, classifying findings as bug/tech-debt/test-gap/false-positive. Computes health scores (test 40%, type 30%, lint 30%). Never modifies source code. Writes audit report to `qa/`.

**`/bugFixOrchestrator`** — Drives a bug ticket (Linear or pasted text) to a proven resolution without editing code itself: `/rootCause --investigate-only` → a check (`ui-evidence` script, or a regression test) that must fail on the unfixed code → implementer subagents (one, one per area, or two competing in worktrees after a failed attempt; 3 attempts max) → the same, hash-frozen check must pass **and** `judge resolution-check` must return `resolved` for the ticket brief. Every phase change goes through the `bugfix-state` CLI (`skills/bugFixOrchestrator/src/`), the only writer of `bugfix/<ticket-slug>/state.json`, which refuses steps the rules don't allow (exit 3). Design: `docs/superpowers/specs/2026-09-29-bug-fix-orchestrator-design.md`.

```
ticket ─► rootCause --investigate-only ─► check FAILS (baseline) ─► implementer(s) ─► same check PASSES ─► judge resolution-check ─► resolution.md
                                              ▲                                              │ partial / unresolved / failed (attempt < 3)
                                              └───────────────── bugfix-state gates every arrow ◄┘
```

### Release & Retro (skills/shipRelease/, syncDocs/, weeklyRetro/)

**`/shipRelease`** — Pre-flight checks (clean tree, branch exists), fetch and rebase on base, run tests, audit coverage, push, open PR via `gh`, then auto-invoke `/syncDocs`. Writes release report to `releases/`.

**`/syncDocs`** — Post-ship documentation updater. Spawns parallel agents to update README, ARCHITECTURE.md, CHANGELOG, AGENTS.md, and the `.agents/rules/` rule files with targeted edits based on recent git changes. Commits updates. Writes sync report to `releases/`.

**`/weeklyRetro`** — Analyzes git history for per-person breakdowns (commits, lines, areas of activity), shipping streaks, test health trends, and generates actionable insights. Compares to previous retros if available. Writes retrospective to `retros/`.

### Planning (skills/officeHours/, productReview/, archReview/)

**`/officeHours`** — YC-style brainstorming with 6 forcing questions (problem, user, current state, unfair advantage, smallest version, success metrics). Outputs a structured design doc to `plans/`.

**`/productReview`** — Founder/product lens review with 4 scope modes: MVP (cut scope), Growth (growth levers, retention), Scale (operational bottlenecks, unit economics), Pivot (what to kill, adjacent opportunities). Delivers SHIP/ITERATE/RETHINK verdict. Writes to `plans/`.

**`/archReview`** — Engineering architecture review with mandatory mermaid diagrams (component, data flow, sequence). Edge case analysis at every component boundary. Scores complexity, scalability, maintainability (1-10). Delivers SOUND/NEEDS WORK/REDESIGN verdict. Writes to `plans/`.

### Design Pipeline (skills/design-*)

A seventeen-skill pipeline for extracting brand identity from reference sites and generating pixel-accurate UI implementations for web and iOS. Design artifacts are written to `~/.agentic-workflow/<repo-slug>/design/`. Five of the skills are platform dispatchers that route to web or iOS sub-skills based on the first argument.

**`/design-analyze [web|ios]`** — Dispatcher that routes to `design-analyze-web` or `design-analyze-ios`. The web variant runs the Dembrandt CLI against reference URLs to extract design tokens (colors, typography, spacing, motion) into `design-tokens.json` in W3C DTCG format. The iOS variant inspects reference iOS apps via XcodeBuildMCP.

**`/design-language`** — Interactive session that synthesizes token analysis into a brand personality document. Asks forcing questions about tone, target audience, and aesthetic direction, then writes `.impeccable.md` with the brand context and updates `planning/DESIGN_SYSTEM.md` with the full component catalog and strategic decisions.

**`/design-evolve [web|ios]`** — Dispatcher that routes to `design-evolve-web` or `design-evolve-ios`. Merges a new reference into the existing design language, diffs against the current `design-tokens.json`, and updates `.impeccable.md` and `DESIGN_SYSTEM.md` without discarding prior decisions.

**`/design-mockup [web|ios]`** — Dispatcher that routes to `design-mockup-web` or `design-mockup-ios`. The web variant generates a self-contained HTML mockup; the iOS variant generates a SwiftUI mockup. Both write to `~/.agentic-workflow/<repo-slug>/design/` and serve as the pixel-accurate baseline for downstream verification.

**`/design-implement [web|ios]`** — Dispatcher that routes to `design-implement-web` or `design-implement-ios`. Generates production-quality code: React/Next.js with Tailwind for web, SwiftUI for iOS. Aligns component structure, spacing, and color usage to the design tokens.

**`/design-refine`** — Dispatches Impeccable refinement commands with the full design context injected. Used to iteratively tighten spacing, typography, and visual hierarchy in the implementation against the mockup baseline.

**`/design-verify [web|ios]`** — Dispatcher that routes to `design-verify-web` or `design-verify-ios`. Takes screenshots of both the mockup and the live implementation, performs a pixel diff, and writes a verification report to `design/`. Reports deviation percentage and highlights mismatched regions.

Pipeline: `design-analyze [web|ios] → design-language → design-mockup [web|ios] → design-implement [web|ios] → design-refine → design-verify [web|ios]` (`design-evolve [web|ios]` can run anytime to incorporate new references)

Design artifacts: `.impeccable.md` (brand context for AI tools), `design-tokens.json` (W3C DTCG token set), `planning/DESIGN_SYSTEM.md` (component catalog and strategic decisions).

### App Verification (skills/verify-app/, verify-web/, verify-ios/)

**`/verify-app`** — Thin platform dispatcher. Auto-detects the platform by checking for iOS indicators (Package.swift, *.xcodeproj) or web indicators (package.json with a web framework dependency) and routes to `verify-web` or `verify-ios` automatically. When detection is ambiguous, asks the user to clarify. Can be used anytime — it does not depend on the design pipeline.

**`/verify-web`** — Standalone web app verification. Uses Playwright to navigate the running web app, take screenshots, and verify UI behaviour against expected outcomes. Writes a verification report to `~/.agentic-workflow/<repo-slug>/verification/`.

**`/verify-ios`** — Standalone iOS app verification. Uses XcodeBuildMCP to launch the app on an iOS Simulator, capture `snapshot_ui` trees and screenshots, and verify screen state against expected outcomes. Supports a `--visual` flag to trigger pixel-level screenshot comparison. Writes a verification report to `~/.agentic-workflow/<repo-slug>/verification/`.

### Prompt Enhancer (skills/enhancePrompt/)

A utility skill that discovers project documentation files (AGENTS.md / CLAUDE.md, planning/, docs/), reads those relevant to the user's current request, and rewrites the prompt with injected context before execution. Used by `/bootstrap` as its first step.

### Bootstrap (bootstrap/)

Orchestrates generation of up to 17 Pivot-pattern planning documents (ARCHITECTURE, ERD, API_CONTRACT, TESTING, etc.) plus a canonical `AGENTS.md` (navigation doc only) with `CLAUDE.md` as a symlink to it, a `.agents/rules/` directory of glob-scoped rule files inferred from the repo's actual structure (symlinked into `.claude/rules` and `.cursor/rules/` by `sync-rules.sh`), and a `.serena/project.yml` config for Serena LSP integration. Audits existing coverage by searching for docs under flexible name patterns, then spawns batched subagents (4-5 at a time) to research and write missing docs. Adapts content to the target repo's actual tech stack. Suggests relevant skills from the full skill pipeline as next steps.

### Serena LSP Integration

Serena is a Dockerized LSP MCP server that provides symbol navigation (find definitions, usages, call hierarchy) for any repo. It is registered globally with every installed provider (`claude mcp add --scope user`, `codex mcp add`, `~/.cursor/mcp.json`) and is available in every session. Prerequisite: Docker Desktop must be installed and running.

- **`scripts/serena-docker`** — Wrapper script that launches the Serena Docker container, mounting the repo root read-only with writable bind mounts for `.serena/cache`, `.serena/logs`, and `.serena/memory`. Auto-selects the C# image when `.serena/project.yml` declares `csharp` in its languages list. Installed to `~/.local/bin/serena-docker` by `setup.sh`.
- **`.serena/project.yml`** — Per-repo config (language servers, `ignored_paths`, `read_only: true`). Generated by `/bootstrap` and committed to the repo. `ignored_paths` controls LSP indexing only — Serena's `read_file` tool is not constrained by it.
- **`mcp-servers.md` rule** — Project-scoped rule (canonical in `.agents/rules/`, emitted per provider) loaded into every session. Documents all registered MCP servers and provides a decision table for when to use Serena vs Grep/Read.
- **Two-image strategy** — The base image (`serena-local:latest`) supports TypeScript, Python, Go, Rust, and most other languages. The C# extension image (`serena-local:latest-csharp`) adds OmniSharp and .NET SDK; it is auto-built by `setup.sh` when `.csproj`/`.cs` files are detected, or manually via `BUILD_CSHARP=1 ./setup.sh`.

## Component 2: Config Archive (config/, .claude/)

Archived configuration for replication across machines. `settings.json`, the statusline, and plugin marketplaces are Claude Code only; hooks and MCP servers are installed for every provider via `providers/<name>/`:

- **config/settings.json** — Sets model to `opus`, enables plugins (github, superpowers, compound-engineering, swift-lsp, playwright), enables experimental agent teams flag, sets effort level to `high`.
- **config/mcp.json** — Registers the `xcodebuildmcp` MCP server (`npx -y xcodebuildmcp@2.3.0 mcp`) for iOS Simulator control.
- **`.claude/settings.json`** — Project-level settings: disables bypass-permissions mode (`"disable"` string, not boolean per Claude Code 1.x schema).
- **`.agents/rules/`** — Canonical glob-scoped rule files. `scripts/sync-rules.sh` emits them as `.claude/rules/` (Claude Code), `.cursor/rules/*.mdc` (Cursor), and a rules index in `AGENTS.md` (Codex). Detailed domain rules were moved out of the monolithic instructions file into these files: `csharp.md`, `csharp-testing.md`, `design.md`, `hooks.md`, `mcp-servers.md`, `skills.md`, `testing.md`.

## Sindri

**Direction (2026-10-11).** Sindri is new in C# as one application (factory addendum
`docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` §17.29, decision Q7), not ported module by module. `sindri`
starts a control-plane process per host, with a UI app as its client, and three engines: the control plane (ledger,
command layer, scheduler and lease, outbox, API, webhooks), a workflow engine that runs every piece of work from a
declarative recipe built from registered C# blocks (§17.28), and a context retrieval engine (§17.3). Background work is
scheduled inside that process as services. Agent work stays in Claude Code sessions in the
human's terminal; the human interjects by attaching or with `sindri open <item>`, and sessions reach Sindri through
the CLI and a Sindri MCP server, as requests, never decisions. Overview: `docs/sindri/README.md`.

Sindri is C# (`src/Sindri`), installed by `scripts/install-cs.sh` as `sindri` (framework-dependent; the Temporal worker cannot be trimmed) and `sindri-ui` (NativeAOT). State lives in `$AW_SINDRI_HOME` (ledger, views, the Temporal dev server's file); the reference for each part is `docs/csharp/sindri.md` (app shell), `docs/csharp/sindri-workflow.md` (the workflow engine and recipes), `docs/csharp/sindri-context.md` and `docs/sindri/context.md` (context retrieval), `docs/csharp/code-index.md` (the code index) and `docs/csharp/sindri-ui.md` (the app). The TypeScript Sindri (profile, ledger, observe, index, scope, evolve) is gone; its observe, scope and evolve ideas return as recipes, not commands.

## Key Rules

1. **Skills are stateless Markdown.** Each skill is a SKILL.md with YAML frontmatter (`name`, `description`, `allowed-tools`, `disable-model-invocation`). The Markdown body is the prompt, and the host agent (Claude Code, Codex, or Cursor) executes it step by step. Skill text names capabilities, not provider tools. No runtime code, no build step.

2. **All skill outputs go to the centralized directory.** `~/.agentic-workflow/<repo-slug>/` is the persistent output directory shared across all skills. Subdirectories: `design/`, `reviews/`, `investigations/`, `qa/`, `plans/`, `releases/`, `retros/`. The repo slug is derived from `git remote get-url origin` or falls back to the directory name.

3. **Every skill references the shared preamble.** The preamble lists all native skills, points to the output directory, and checks bootstrap status (skills installed for the running provider, repo rules present). If not bootstrapped, it prompts the user to run `setup.sh`. Design pipeline skills additionally include a design-specific preamble for brand context.

4. **Subagents run in parallel (Dispatch in parallel).** Both `/review` (reviewer subagents) and `/addressReview` (implementer subagents) spawn all agents at once using the provider's parallel-dispatch mechanism (see `skills/_shared/capabilities.md`). Triage always runs sequentially first to determine the agent assignments.

5. **Setup is symlink-based.** `setup.sh` creates symlinks from each installed provider's skills directory into this repo, plus the stable `~/.agentic-workflow/toolkit` symlink, rather than copying files. Changes to skill definitions take effect immediately without re-running setup. Setup also creates the `~/.agentic-workflow/` base directory, builds the Serena Docker image(s), installs the `serena-docker` wrapper to `~/.local/bin/`, and registers Serena and the other MCP servers with every selected provider.

6. **Zero telemetry.** No session tracking, no analytics logging, no external service calls. Skills do not phone home. All data stays local.

7. **Tests are earned.** C# tests follow `.agents/rules/csharp-testing.md`; nothing is hidden with an ignore annotation.
