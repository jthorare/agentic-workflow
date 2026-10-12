# AGENTS.md — Agentic Workflow

> Agentic Workflow — provider-agnostic agent workflow toolkit for Claude Code, Codex, and Cursor: 52 native skills + 3 fetched external packs (impeccable, emil-design-eng, taste-skill), config archive, repo bootstrapper, and token-efficiency tools (rtk + headroom).

Domain-specific rules live in `.agents/rules/` — one file per rule, the only copy. `.claude/rules` and `.cursor/rules/*.mdc` are symlinks to it (auto-loaded by Claude Code and Cursor), and `CLAUDE.md` is a symlink to this file. Codex reads the Rules Index at the bottom of this file. After adding or removing a rule, run `scripts/sync-rules.sh`.

## Required Context

| Document | Purpose |
|----------|---------|
| `planning/ARCHITECTURE.md` | System components and data flow |
| `planning/PROVIDERS.md` | Claude Code / Codex / Cursor support matrix and canonical layout |
| `planning/API_CONTRACT.md` | Sindri CLI output contract |
| `planning/CODE_STYLE.md` | Code style: C# by its rule files, TypeScript for bug fixes |
| `planning/TESTING.md` | Test strategy and coverage targets |
| `planning/ERD.md` | Sindri ledger and code-index SQLite schemas |
| `skills/_shared/capabilities.md` | Capability → tool map used by all skill text |

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | Node.js >= 22 (`.nvmrc`; better-sqlite3 13 requires it), ES2022 target |
| Language | TypeScript 5.7, strict mode |
| Database | SQLite via better-sqlite3, WAL mode |
| LSP (Serena) | Docker (Dockerized Serena MCP server via `scripts/serena-docker`) |
| Validation | Zod 3 |
| Test | Vitest (in-memory SQLite) |
| Build | tsc (ESM, Node16 module resolution) |

**C# build — in progress, not installed until the cutover.** The toolkit is moving to C# (spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4a–§4e). The tool ports stop after `Judge` (#156) and `Evidence` (#158); `scorer` and `sindri` are not ported. Sindri is **built new in C# as an application** (control plane, workflow engine, context retrieval engine; factory addendum §17.29, decision Q7; overview in `docs/sindri/README.md`), the finished tool ports become its blocks, and the TS `sindri/` stays the reference and keeps running until the switch. This is a clean cutover and a **reimplementation in idiomatic C#, not a translation**: the TS package is a guideline for what a package does, never for how (no TS formatting, quirks or workarounds; type-driven JSON through `AwJson`; contracts defined by the C# tests; `.agents/rules/csharp.md` "Porting a TS package"). The C# build keeps no byte or state compatibility with the TS build, and no TS state is migrated. The one exception is the **hook contract**: only what the bash hooks and statusline that keep running read (the commands and arguments they call, the JSON fields they `jq`, the exit codes and stderr prefixes they branch on, and the files they write, such as `judge/sessions`, `judge/outbox` and `judge/teammate-names.jsonl`; spec §4c). It stays frozen until the switch, checked in CI by running the real hook scripts against the C# binaries. The installed toolkit stays TypeScript until the C# build is complete; then one switch moves the install. Before that, nothing in `setup.sh`, the providers, MCP registration or launchd points at the C# build. Separately, `aw-readiness` and `aw-packets` run before the switch as standalone NativeAOT CLIs that write only under `readiness/` and `packets/` (spec §4c).

| Layer | Technology (C# build) |
|-------|------------|
| Runtime | .NET 10 (LTS), SDK pinned in `global.json`; NativeAOT for hook and CLI entry points. The Sindri host is framework-dependent (its Temporal worker can't be trimmed; README X54) |
| Language | C# (latest), nullable on, warnings as errors |
| HTTP (an http host) | ASP.NET Core minimal APIs (`CreateSlimBuilder`), loopback only |
| UI (the Sindri app) | [Avalonia UI](https://avaloniaui.net/) (MIT core only, no paid add-ons), MVVM with compiled bindings; a client of Sindri's HTTP API with no logic of its own. Desktop (macOS, Linux, Windows) first; iOS, Android and browser heads later from the same views. Spec §4a "UI" |
| Database | `Microsoft.Data.Sqlite` with its bundled `e_sqlite3` (shipped beside each AOT tool), new C# schemas, WAL mode |
| MCP | Official MCP C# SDK (approved in the AOT spike; no package uses it since the bridge was retired) |
| Workflow engine (Sindri) | [Temporal](https://temporal.io) through the `Temporalio` .NET SDK (pinned, nuget.org): a local `temporal server start-dev` on loopback with its own SQLite file under `$AW_SINDRI_HOME`, started by `sindri serve`; one `RecipeWorkflow` interprets every recipe; every transition is mirrored into the ledger (engine spec §0, §5; decision Q8, 2026-10-11) |
| JSON | System.Text.Json, one source-generated context per package |
| Logging | `Aw.Kernel.Telemetry`: JSON lines on stderr (`AW_LOG_LEVEL`), OpenTelemetry spans and metrics, exporting off by default, scrubbed by `Aw.Redaction` |
| Analyzers | NetAnalyzers, Meziantou, Roslynator, BannedApiAnalyzers, PublicApiAnalyzers (one owner per rule, `.editorconfig`) |
| Test | xUnit v3 on Microsoft.Testing.Platform (decided, R7), lanes `unit` / `integration` / `heavy` |
| Packages | Central Package Management (`Directory.Packages.props`), locked restore, nuget.org only (`nuget.config`) |

## Directory Structure

```
agentic-workflow/
├── skills/        # 51 provider-neutral skills (linked into each provider's skills dir)
├── bootstrap/     # /bootstrap skill — repo documentation generator
├── config/        # Settings, MCP config, statusline, safety hooks, sindri-nudge SessionStart hook (+ hooks/adapters/ per provider)
├── providers/     # Per-provider installers (claude, codex, cursor)
├── scorer/        # Daily cost/involvement report from provider session transcripts
├── judge/         # Cheap-agent-harness judge CLI (decisions, config, health)
├── sindri/        # Sindri in TypeScript (runs until the switch; reference for the C# app): profile, ledger, lock, scrubber, observe, index, scope, evolve, doctor (CLI)
├── mods/          # Claude Code mods (in-process function hooks): aw-live /live pane. Claude-only
├── planning/      # Project documentation
├── src/           # C# build (in progress): Aw.Kernel (+ .Data, .Telemetry), Redaction, Aw.Analyzers, the tool ports (Packets, Readiness, BugFix; Judge, Evidence) and later the new Sindri app — each Core · Features · Infrastructure · Host
├── tests/         # C# tests: Aw.<Pkg>.Tests (by feature), Aw.<Pkg>.Testing (fakes, builders, contract bases), Aw.Architecture.Tests
├── global.json · AgenticWorkflow.slnx · Directory.Build.props · Directory.Build.targets · Directory.Packages.props · nuget.config · .editorconfig · CodeMetricsConfig.txt · BannedSymbols.txt
├── .agents/rules/ # Glob-scoped domain rules (.claude/rules and .cursor/rules link here)
├── .serena/       # Serena LSP project configuration
├── scripts/       # Utility scripts (sync-rules.sh, check-deps.mjs, serena-docker, probe.sh, install-scorer.sh)
├── schemas/       # JSON Schemas for repo config (deps.schema.json, rule-record.schema.json)
├── rules/         # Rule records (factory addendum §17.6): rules/engineering/csharp/*.yaml, one per §4b rule
├── deps.yaml      # Declared package graph: tiers and every project/cli/db/file edge (spec §4b.1a)
└── setup.sh       # One-command setup for every installed provider
```

## Commands

```bash
# TypeScript packages (each has test, test:coverage, typecheck, build)
cd scorer && npm test                   # Vitest
cd judge && npm test                    # Vitest
cd sindri && npm test                   # Vitest
(cd skills/ui-evidence && npm test)     # Vitest (skill package; includes a real-browser test)
(cd skills/bugFixOrchestrator && npm test)  # Vitest (bugfix-state CLI)
claude plugin validate mods/aw-live && claude plugin test mods/aw-live  # the aw-live mod (needs claude >= 2.1.289)
scorer --since 7d [--provider claude|codex|cursor|all]  # Report to ~/.agentic-workflow/scorer/reports/
scorer live --session <id> [--cwd DIR] [--json]  # This session's numbers (the aw-live mod calls it)
scorer probe                            # Summarize the hook-input probe
scorer audit [--since 60d; default 1d] [--items FILE] [--max-size XS] [--label N]   # Human-turn baseline → ~/.agentic-workflow/audit/; --label sends sampled turn text to your Claude login's provider

# C# build (in progress; the merge-gate lines below apply once src/ has projects)
dotnet build -warnaserror               # Whole solution (AgenticWorkflow.slnx); zero warnings
# Lane runs (xUnit v3 on Microsoft.Testing.Platform, MTP options only). Exit 8 = no test matched the filter in some
# project (e.g. an integration-only project in the unit lane): ignored for filtered runs only. The unfiltered
# `dotnet test --solution AgenticWorkflow.slnx` keeps exit 8 as a failure.
dotnet test --solution AgenticWorkflow.slnx                                                     # All lanes; exit 8 (a project with no tests) fails
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8          # Unit lane (< 10 s per package)
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=integration" --ignore-exit-code 8   # Temp SQLite files, WebApplicationFactory (< 60 s per package)
dotnet test --solution AgenticWorkflow.slnx --filter-not-trait "Lane=heavy" --ignore-exit-code 8     # Everything except containers, browsers and index builds
node scripts/ci/lane-budgets.mjs        # After a Release build: each package's unit/integration lane wall clock vs its budget (LANE-PKG-001)
dotnet format --verify-no-changes       # Style and analyzer fixes are applied (CI and pre-commit)
scripts/dotnet-verify-packages.sh       # Lock files present and unchanged, signature-verified locked restore, dotnet nuget verify --all
# Add a package: /new-package (node scripts/scaffold/new-package.mjs; contract docs/csharp/scaffolds/new-package.md)
# Add a use case: /new-slice (node scripts/scaffold/new-slice.mjs; contract docs/csharp/scaffolds/new-slice.md) — test, request and response, service, endpoint, registration
dotnet run --project src/Readiness/Aw.Readiness.App -- check [--json] [--root DIR] [--no-remote]   # Readiness level vs readiness.yaml's target: exit 0 holds, 1 not, 2 tool error (docs/readiness/README.md)
bash src/Readiness/aot-smoke/run.sh     # Publish aw-readiness NativeAOT and smoke-test the native binary (CI's AOT step)
bash scripts/scaffold/e2e.sh            # The generators end to end: scaffold, build and test a small cli and a full http package in throwaway worktrees, NativeAOT-publish the cli one (~2 min; needs the NativeAOT toolchain)
dotnet run --project src/Packets/Aw.Packets.App -- validate <packet-dir> [--repo <dir>] [--json]   # aw-packets: the packet checks (docs/csharp/packets.md); exit 0 pass, 1 fail, 2 tool error; writes nothing
dotnet run --project src/Packets/Aw.Packets.App -- freeze <packet-dir> [--repo <dir>] [--json]     # Record the packet's canonical hash under $AW_STATE_DIR/packets/<owner>/<name>/<id>/frozen/
bash src/Packets/aot-smoke/run.sh        # aw-packets under NativeAOT: publish (on a copy), run against a temp repo, compare with expected.txt
dotnet run --project src/Evidence/Aw.Evidence.Host -- <run|parity|parity-plan|parity-comment|parity-stale|parity-put|publish-plan|doctor|provenance|install-browser> …   # ui-evidence, C# build (docs/csharp/evidence.md): exit 0 ok, 1 usage/invalid/refused, 2 a check failed, 3 review pending, 4 the run errored, 75 a lock stayed busy
dotnet run --project src/BugFix/Aw.BugFix.Host -- <command> --state <dir> [options]   # bugfix-state, the C# /bugFixOrchestrator helper (docs/csharp/bugfix.md); exit 0 ok, 1 bad input, 2 usage, 3 refused
bash src/BugFix/aot-smoke/run.sh         # bugfix-state under NativeAOT: publish (on a copy), run a bug fix in a temp repo, compare with expected.txt

# Rules (edit .agents/rules/, then regenerate)
scripts/sync-rules.sh                   # Link .claude/rules, .cursor/rules, CLAUDE.md; refresh Rules Index
scripts/sync-rules.sh --check           # Exit 1 if links or the Rules Index are out of date

# Dependency graph (deps.yaml + schemas/deps.schema.json)
node scripts/check-deps.mjs             # Schema, tiers, cycles, fan-out, undeclared cli/db/file edges; exit 1 on drift
node scripts/check-deps.mjs --mermaid [--base-file F]   # Also print the Mermaid graph and the edge list (added/removed vs F)

# Rule records (rules/engineering/csharp/*.yaml + schemas/rule-record.schema.json; docs/csharp/rule-records.md)
node scripts/check-rules.mjs [--json]   # Every §4b rule has a record; every active check exists; pending ones wait on their PR

# Environment and dev container
scripts/check-env-example.sh            # Every env var the code reads is in .env.example (ENV-EXAMPLE-001..004); exit 1 on drift
# .devcontainer/ is for humans only, never the agent sandbox (no Docker-in-Docker); see docs/devcontainer.md
# Label scheme: .github/labels.yml (type:, area:, size:, ring:). Applying it to the repo is an owner settings change; until then
# the issue forms' labels don't exist and GitHub drops them silently

# Bash tests (CI: scripts/ci/bash-tests.sh runs every tracked suite, independent ones in parallel and
# its SERIAL list, the timing-sensitive ones, one at a time afterwards; --serial prints them with reasons)
bash scripts/ci/bash-tests.sh
bash providers/tests/install.test.sh
bash scripts/tests/sync-rules.test.sh
bash scripts/tests/check-deps.test.sh
bash scripts/tests/check-rules.test.sh
bash scripts/tests/scaffold.test.sh
bash scripts/tests/check-env-example.test.sh
bash scripts/tests/repo-templates.test.sh
bash scripts/tests/probe.test.sh
bash scripts/tests/find-duplicate-skills.test.sh
bash scripts/tests/dotnet-verify-packages.test.sh
bash scripts/tests/install-scorer-audit.test.sh
bash scripts/tests/install-sindri.test.sh
bash scripts/tests/readiness-baseline.test.sh
bash scripts/tests/agent-token.test.sh
bash scripts/tests/ci-changed-areas.test.sh
bash scripts/tests/ci-bash-tests.test.sh
bash scripts/tests/ci-lane-budgets.test.sh
bash scripts/tests/ci-no-any.test.sh
bash config/hooks/tests/codex-adapter.test.sh
bash config/hooks/tests/cursor-adapter.test.sh
bash config/hooks/tests/provider-install-hooks.test.sh
bash config/hooks/tests/probe-log.test.sh
bash config/hooks/tests/judge-health.test.sh
bash config/hooks/tests/sindri-nudge.test.sh
bash config/hooks/tests/failopen.test.sh
for t in config/lib/tests/*.test.sh; do bash "$t" || echo "FAILED: $t"; done  # merge-hook, levers, installers

# Levers and probe (default provider: claude)
scripts/install-<lever>.sh --provider claude|codex|cursor
scripts/probe.sh [--provider claude|codex|cursor] on|off|status
scripts/install-scorer.sh               # Build scorer, install CLI + launchd job
sindri doctor                           # Sindri health checks (ok / warn / fail + fix)
sindri index setup && sindri index build    # code index (Ollama + graphify, offline)
sindri evolve init && sindri evolve check --changed   # registry + module eval suites (heavy: run alone)
sindri evolve status                                   # artifacts, proposals, merge rate, corpus
sindri evolve reflect --pr <n>                         # proposals from a merged PR
sindri evolve weekly                                   # the Monday job (launchd runs it)
sindri evolve publish                                  # staged proposals -> this week's plan file (on a branch)
sindri repo onboard [<path>]            # add + approval check + pre-commit hook + first index build (never approves)
sindri scope <brief.md> --out DIR            # cited scope map; --backtest linear:<project> for recall, precision and a baseline
sindri shape report                      # record-only shape signals and their outcomes (read-only)
sindri shape reconcile                   # move the spool into the ledger and label outcomes now (observe does it hourly)
sindri observe                          # Backlog with sizes; records to the ledger when the profile is approved
scripts/install-sindri.sh               # Build sindri, install the CLI wrapper (or ./setup.sh --with-sindri)
GH_TOKEN="$(scripts/agent-token.sh [--repo NAME])"  # Owner/session launcher only, never inside an agent session: 1h agent App token

# Setup (from repo root)
./setup.sh --providers claude,codex,cursor --dry-run   # Print every change, write nothing
./setup.sh [--providers ...]            # Default: detect installed CLIs. Links skills, installs
                                        # hooks + levers + MCP servers per provider (and removes retired ones), builds
                                        # judge, scorer, Serena image
```

After installing for Codex, open `codex` and run `/hooks` to trust the `aw:*` hooks.

## Merge Gate

Before merging any PR:
1. `npm run typecheck` passes with zero errors in `scorer`, `judge`, `sindri`, `skills/ui-evidence`, and `skills/bugFixOrchestrator`
2. `npm test` passes in `scorer`, `judge`, `sindri`, `skills/ui-evidence`, and `skills/bugFixOrchestrator`, every bash test listed above passes, and `claude plugin validate mods/aw-live` and `claude plugin test mods/aw-live` pass
3. `scripts/sync-rules.sh --check` passes (rule links and Rules Index match `.agents/rules/`)
4. `node scripts/check-deps.mjs` passes (`deps.yaml` declares every edge the code has; a new edge is a reviewed change to it); `scripts/check-env-example.sh` passes (`.env.example` lists every environment variable the code reads)
5. `./setup.sh --providers claude,codex,cursor --dry-run` runs cleanly
6. No `/* v8 ignore */` annotations in source files (prohibited — write the test instead)
7. No `any` types in TypeScript source (`node scripts/ci/no-any.mjs`; existing ones are held in `scripts/ci/no-any-baseline.json`, which only shrinks)
8. C# build (active once `src/` has projects): `dotnet build -warnaserror` reports zero warnings; `dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8` and `dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=integration" --ignore-exit-code 8` pass within their budgets (`node scripts/ci/lane-budgets.mjs`, LANE-PKG-001; the heavy lane runs nightly); `dotnet format --verify-no-changes` is clean, `scripts/dotnet-verify-packages.sh` passes, and the ratchet baseline block at the end of `.editorconfig` didn't grow
9. `node scripts/check-rules.mjs` passes (every §4b rule has a rule record, and every check a record calls active exists in the tree)

## C# slice recipe

To add a use case to a C# package: **test, request and response, service, endpoint, registration.**

1. Write the failing test that names the behavior (`StartItem_parks_when_budget_is_exhausted`) in `tests/Aw.<Pkg>.Tests/<Slice>/`, together with the request and response records and a service stub with no constructor parameters whose `Handle` throws `NotSupportedException`, in `src/<Pkg>/Aw.<Pkg>.Features/<Slice>/`. Commit it red: it compiles with zero warnings and fails when run.
2. Write the real assertion; add the ports the service needs to its constructor.
3. Implement the service (`IService<TRequest, TResponse>`); pure decisions live on the domain type in `Core`.
4. Map the endpoint (`Host/Endpoints/`) or CLI command (`Host/Commands/`): bind, call one service, map the `Result`.
5. Register it explicitly in the project's `Add…()` method; add the types to the package's JSON context.

`/new-slice` generates step 1 and the stubs for steps 4–5. Full rules: `.agents/rules/csharp.md` and `.agents/rules/csharp-testing.md`.

## Commit Conventions

Format: `type: short description`

Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`

Keep commits atomic — one logical change per commit. See `planning/COMMIT_STRATEGY.md` for details.

<!-- === RULES INDEX START (generated by sync-rules.sh — edit .agents/rules/, then run scripts/sync-rules.sh) === -->
## Rules Index

Domain rules live in `.agents/rules/`. Before editing a file, read every rule below whose globs match it ("always" rules apply to every task). Claude Code and Cursor load them automatically through `.claude/rules` and `.cursor/rules/` links; Codex does not — read them yourself.

| Rule | Description | Applies to | Path |
|------|-------------|------------|------|
| `csharp-testing` | C# tests — TDD order, lanes and time budgets, fakes with contract tests, builders, golden files, architecture tests | `tests/**`, `**/*Tests.cs`, `**/*.Tests.csproj`, `**/*.Testing.csproj` | `.agents/rules/csharp-testing.md` |
| `csharp` | C# build (in progress) — package shape, dependency rules, types, errors, DI, JSON, AOT scope, analyzers | `src/**`, `**/*.cs`, `**/*.csproj`, `**/*.slnx`, `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `nuget.config`, `BannedSymbols.txt`, `deps.yaml`, `schemas/deps.schema.json`, `**/.editorconfig`, `CodeMetricsConfig.txt`, `**/PublicAPI.*.txt`, `**/packages.lock.json` | `.agents/rules/csharp.md` |
| `design` | Design pipeline artifacts — tokens, .impeccable.md, DESIGN_SYSTEM.md, design-* skills | `design-tokens.json`, `.impeccable.md`, `planning/DESIGN_SYSTEM.md`, `skills/design-*/**` | `.agents/rules/design.md` |
| `hooks` | Safety and context hooks — per-provider adapters, protocols, installation | `config/hooks/**`, `config/settings.json` | `.agents/rules/hooks.md` |
| `mcp-servers` | MCP servers registered globally with every provider — available in every session | always | `.agents/rules/mcp-servers.md` |
| `skills` | Skill structure, preamble, output directories, pipeline, and installation | `skills/**`, `bootstrap/**` | `.agents/rules/skills.md` |
| `testing` | Test infrastructure, coverage policy, and test count baseline | `**/*.test.ts`, `**/*.spec.ts`, `**/vitest.config.ts` | `.agents/rules/testing.md` |
<!-- === RULES INDEX END === -->
