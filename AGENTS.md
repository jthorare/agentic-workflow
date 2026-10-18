# AGENTS.md — Agentic Workflow

> Agentic Workflow — provider-agnostic agent workflow toolkit for Claude Code, Codex, and Cursor: 53 native skills + 3 fetched external packs (impeccable, emil-design-eng, taste-skill), config archive, repo bootstrapper, and a token-efficiency tool (rtk).

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

**C# build — in progress, not installed until the cutover.** The toolkit is moving to C# (spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4a–§4e). The tool ports stop after `Judge` (#156) and `Evidence` (#158); `scorer` and `sindri` are not ported. Sindri is **built new in C# as an application** (control plane, workflow engine, context retrieval engine; factory addendum §17.29, decision Q7; overview in `docs/sindri/README.md`), the finished tool ports become its blocks, and the TS `sindri/` stays the reference and keeps running until the switch. This is a clean cutover and a **reimplementation in idiomatic C#, not a translation**: the TS package is a guideline for what a package does, never for how (no TS formatting, quirks or workarounds; type-driven JSON through `AwJson`; contracts defined by the C# tests; `.agents/rules/csharp.md` "Porting a TS package"). The C# build keeps no byte or state compatibility with the TS build, and no TS state is migrated. The one exception is the **hook contract**: only what the bash hooks and statusline that keep running read (the commands and arguments they call, the JSON fields they `jq`, the exit codes and stderr prefixes they branch on, and the files they write, such as `judge/sessions`, `judge/outbox` and `judge/teammate-names.jsonl`; spec §4c). It stays frozen until the switch, checked in CI by running the real hook scripts against the C# binaries. The installed toolkit stays TypeScript until the C# build is complete; then one switch moves the install. Before that, nothing in `setup.sh`, the providers, MCP registration or launchd points at the C# build. Separately, `aw-readiness` and `aw-packets` run before the switch as standalone NativeAOT CLIs that write only under `readiness/` and `packets/` (spec §4c), and so does `aw-meta` (the `/meta` harness), which keeps its state under `meta/` and appends to its team's running log (its gate and merge commands also act on the team's checkout and PRs: `git fetch`, a gate worktree, the configured gate steps, `gh pr merge`, and removing finished worktrees with `git worktree remove` on merge, close and sweep triggers, never by age and never forced); it is one of two C# tools installed before the switch, and aw-meta is only opt-in through `./setup.sh --with-meta`, on Linux and macOS (macOS reads memory, swap and the process table through `sysctl`, `vm_stat`, `ps` and `lsof` where Linux reads `/proc`; a macOS tool that can't run makes that figure `AW-META-010` "unavailable on macOS", never a guess; any other OS fails the install loudly; `docs/csharp/meta.md` "OS support") (owner, 2026-10-14; macOS 2026-10-17). The other is `sindri-app` (`scripts/sindri-app.sh`, owner-approved), which publishes the C# Sindri for this platform and runs it; setup.sh does not install it, you run the script yourself.

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
├── skills/        # 52 provider-neutral skills (linked into each provider's skills dir)
├── bootstrap/     # /bootstrap skill — repo documentation generator
├── config/        # Settings, MCP config, statusline, safety hooks, sindri-nudge SessionStart hook, meta Stop/outbox hooks, non-Claude lane hooks (meta-lane-gate, session-id-report) (+ hooks/adapters/ per provider)
├── providers/     # Per-provider installers (claude, codex, cursor)
├── scorer/        # Daily cost/involvement report from provider session transcripts
├── judge/         # Cheap-agent-harness judge CLI (decisions, config, health)
├── sindri/        # Sindri in TypeScript (runs until the switch; reference for the C# app): profile, ledger, lock, scrubber, observe, index, scope, evolve, doctor (CLI)
├── mods/          # Claude Code mods (in-process function hooks): aw-live /live pane. Claude-only
├── planning/      # Project documentation
├── src/           # C# build (in progress): Aw.Kernel (+ .Data, .Telemetry), Redaction, Aw.Analyzers, the tool ports (Packets, Readiness, BugFix; Judge, Evidence), Meta (aw-meta, the /meta harness), Forge (the forge port with GitHub and file adapters) and later the new Sindri app — each Core · Features · Infrastructure · Host
├── tests/         # C# tests: Aw.<Pkg>.Tests (by feature), Aw.<Pkg>.Testing (fakes, builders, contract bases), Aw.Architecture.Tests
├── global.json · AgenticWorkflow.slnx · Directory.Build.props · Directory.Build.targets · Directory.Packages.props · nuget.config · .editorconfig · CodeMetricsConfig.txt · BannedSymbols.txt
├── .agents/rules/ # Glob-scoped domain rules (.claude/rules and .cursor/rules link here)
├── .serena/       # Serena LSP project configuration
├── scripts/       # Utility scripts (sync-rules.sh, check-deps.mjs, serena-docker, probe.sh, install-scorer.sh)
├── schemas/       # JSON Schemas for repo config (deps.schema.json, rule-record.schema.json)
├── rules/         # Rule records (factory addendum §17.6): rules/engineering/csharp/*.yaml, one per §4b rule
├── deps.yaml      # Declared package graph: tiers and every project/compose/cli/db/file edge (spec §4b.1a)
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
dotnet run --project src/Packets/Aw.Packets.App -- <validate|freeze|hash> --kind scoping <packet-dir> [--repo-slug <owner/name> --instance <slug>] [--json]   # the scoping kind (P1): schema-only PKT-SCP-001..005, the canonical hash, freeze under packets/<owner>/<name>/<instance>/frozen/; `--kind` defaults to build (docs/csharp/packets.md)
dotnet run --project src/Packets/Aw.Packets.App -- <validate|freeze|hash> --kind design|plan|phase|proof|review|handoff <packet-dir> [--repo <dir>] [--json]   # the line kinds (LN-PKT1): packet.json links every file and index.json, schema-only PKT-DSN/PLN/PHS/PRF/REV/HND-* plus PKT-BASE-001 (and PKT's task rules for a plan), the canonical hash, freeze under packets/<owner>/<name>/<id>/frozen/ (docs/csharp/packets.md)
dotnet run --project src/Packets/Aw.Packets.App -- freeze <packet-dir> [--repo <dir>] [--json]     # Record the packet's canonical hash under $AW_STATE_DIR/packets/<owner>/<name>/<id>/frozen/
bash src/Packets/aot-smoke/run.sh        # aw-packets under NativeAOT: publish (on a copy), run against a temp repo, compare with expected.txt
bash src/Judge/aot-smoke/run.sh          # judge under NativeAOT: publish (on a copy), run offline on a temp AW_STATE_DIR, compare with expected.txt
dotnet run --project src/Evidence/Aw.Evidence.Host -- <run|parity|parity-plan|parity-comment|parity-stale|parity-put|publish-plan|doctor|provenance|install-browser|api-run|db-run> …   # ui-evidence, C# build (docs/csharp/evidence.md; api-run: HTTP and CLI contract checks, summary.json on stdout, `--at base|head`, `--host` for requests, targets from <repo>/.agentic-workflow/evidence.json; db-run: read-only SQLite (a copy) or Postgres (identity and role checked) row assertions, same summary contract, Npgsql): exit 0 ok, 1 usage/invalid/refused, 2 a check failed, 3 review pending, 4 the run errored, 75 a lock stayed busy
dotnet run --project src/BugFix/Aw.BugFix.Host -- <command> --state <dir> [options]   # bugfix-state, the C# /bugFixOrchestrator helper (docs/csharp/bugfix.md); exit 0 ok, 1 bad input, 2 usage, 3 refused
bash src/BugFix/aot-smoke/run.sh         # bugfix-state under NativeAOT: publish (on a copy), run a bug fix in a temp repo, compare with expected.txt
dotnet run --project src/Forge/Aw.Forge.Host -- <parse|open|pull|threads|seed thread|seed checks|seed advance> …   # aw-forge-fake, the C# forge port's CLI (docs/csharp/forge.md); adapter by AW_FORGE_ADAPTER (file default | github), hosts AW_FORGE_HOSTS, credential AW_FORGE_CREDENTIAL (env:NAME | command:argv, default env:AW_FORGE_TOKEN); one JSON document on stdout; exit 0 ok, 1 invalid or not found, 2 usage, 3 refused or forge unavailable
dotnet test --project tests/Aw.Forge.Tests --filter-trait "Lane=heavy"   # Owner's GitHub check: the read-side forge contract against a scratch repo; skips unless AW_FORGE_GITHUB_REPO=owner/name and AW_FORGE_TOKEN are set (docs/csharp/forge.md)
dotnet run --project src/Sindri/Aw.Sindri.Host -- [--headless | serve | status [--follow] [--json] | stop | profile validate|approve | views rebuild [--no-start] | queue [--json] | items [--state S] [--repo R] [--json] | item add <title> [--repo R] | item close <item> [--cancelled] [--version N] | ledger [--after N] [--item X] [--type T] [--max N] [--json] | doctor | services [--json] | services run <name> [--detach] [--json] | services pause|resume <name> | jobs [--state S] [--kind K] [--json] | jobs show <id> [--json] | jobs cancel <id> | agents [--provider P] [--state S] [--repo R] [--json] | agents show <provider>/<id> [--json]]   # the C# Sindri app (S1 app shell, docs/csharp/sindri.md; `views rebuild` replays the ledger into views.db, the fix when the projector stalls; `agents` lists every live agent session on the box, read-only and never starting serve: exit 0 ok, 1 serve is down or a source failed, 2 error); state in AW_SINDRI_HOME (default $AW_STATE_DIR/sindri-app); exit 0 ok, 1 attention, 2 error
dotnet run --project src/Sindri/Aw.Sindri.Host -- recipe validate <file> [--json]   # check a workflow recipe in-process, no serve needed (docs/csharp/sindri-workflow.md); exit 0 pass, 1 fail, 2 usage or tool error
dotnet run --project src/Sindri/Aw.Sindri.Host -- recipe eval <file> [--json] | --scaffold <file> [--out <dir>]   # run a recipe's eval cases over the pure engine; --scaffold --out writes one case per path of a route recipe (docs/csharp/sindri-workflow.md); exit 0 pass, 1 a case fails, 2 usage or tool error; --record needs the server (P4)
dotnet run --project src/Sindri/Aw.Sindri.Host -- workflow start|status|list|events|answer|command|cancel|complete …   # the recipe instances: all ten workflow verbs (these eight and takeover|handback on the next line) go through the running control plane (reads never start it: exit 1 SND-API-001 when it is down); only `recipe validate` and `recipe eval` run in process (docs/csharp/sindri-workflow.md; `sindri help` has each line); exit 0 ok, 1 not running, 2 invalid, refused or error
dotnet run --project src/Sindri/Aw.Sindri.Host -- workflow takeover|handback <address> [--nonce <n>] [--json]   # P5: take an instance over (its timer pauses) or hand it back (its timer resumes); `sindri handback <item-or-address>` is the short form; exit 0 applied, 1 not running, 2 invalid, refused or error (docs/csharp/sindri-workflow.md)
# Sandbox (not built yet: contracts only until SB-1; docs/csharp/sindri-sandbox.md, spec 2026-10-18-sindri-sandbox-design.md): `sindri sandbox check|show|denials|explain|exec|login|learn` arrive in SB-5a to SB-8; what works inside a Step is in the sandbox doc
bash src/Sindri/aot-smoke/run.sh         # sindri, framework-dependent (the Temporal SDK has no NativeAOT; needs scripts/binaries-lock.sh fetch temporal): publish (on a copy), drive start, a refused second serve, the approve preview and its non-terminal refusal, a session's approve and stop refused (SND-AUTH-002), the human's typed approval and stop under a pty, views rebuild refused to a session and requested by the human, status (its views and health lines, and --json's apiVersion), /sindri/mcp's initialize and tools/list, stop and doctor (on a scratch HOME) in a temp home, then recipe validate on the shipped recipes and an invalid fixture (each call bounded to 30 s), compare with expected.txt
dotnet run --project src/Sindri/Aw.Sindri.Ui.Desktop       # the Sindri app (S1b shell, U0 theme and shell; docs/csharp/sindri-ui.md): top bar, Home, Health, Settings, tray, single instance, live from the event stream; bare `sindri` opens it (sindri-ui next to sindri, or AW_SINDRI_UI)
bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh [<dir>]   # sindri-ui under NativeAOT: publish (on a copy) and fail on any IL2xxx/IL3xxx warning or a missing binary; copies it and its native libraries to <dir> when given

dotnet run --project src/Meta/Aw.Meta.Host -- <command> [--json]   # aw-meta, the /meta harness CLI (docs/csharp/meta.md): obligations (inbox-sync, stop-check, outbox-record, inbox-*), lanes-*, gate-run, gate-rerun, merge-check, merge-run, owner-*, health, watch, log-append, status, version, team-*, role-*; exit 0 ok, 1 attention, 2 usage or tool error
bash src/Meta/aot-smoke/run.sh           # aw-meta under NativeAOT: publish (on a copy), drive the obligation flow in a temp state dir, compare with expected.txt
scripts/install-meta.sh                  # Publish aw-meta (NativeAOT, stamped with the commit and build time) to $AW_STATE_DIR/bin, install its hooks, link aw-meta and aw-meta-launch, print the installed version (or ./setup.sh --with-meta; Linux and macOS, other OSes fail loudly; aw-meta-launch warns when the binary is older than the Meta sources)
scripts/meta-launch.sh [--lanes a,b] [--no-open] [--dry-run]   # The team in one Warp window: a pane per lane on tmux session <team>-<lane>, resumed by id (team config: aw-meta team-init)
# Rules (edit .agents/rules/, then regenerate)
scripts/sync-rules.sh                   # Link .claude/rules, .cursor/rules, CLAUDE.md; refresh Rules Index
scripts/sync-rules.sh --check           # Exit 1 if links or the Rules Index are out of date

# Dependency graph (deps.yaml + schemas/deps.schema.json)
node scripts/check-deps.mjs             # Schema, tiers, cycles, fan-out, compose (DEPS-COMPOSE-001), undeclared cli/db/file edges; exit 1 on drift
node scripts/check-deps.mjs --mermaid [--base-file F]   # Also print the Mermaid graph and the edge list (added/removed vs F)

# Pinned external binaries (binaries.lock: the temporal CLI and the time-skipping test server; engine spec rev 8 §0)
scripts/binaries-lock.sh check          # Shape of binaries.lock: six fields, one version per name, all four platforms
scripts/binaries-lock.sh fetch <name>   # Download this platform's row, verify its sha256, install under $AW_STATE_DIR/cache/bin/<name>-<version>/

# External tools, once per box (setup.sh runs it): uv, graphify at sindri's pin, both Temporal binaries, system packages (installed when missing)
scripts/install-deps.sh                 # AW_DRY_RUN=1 prints only; missing system packages are installed (brew on macOS; sudo + apt-get, dnf, yum, pacman or zypper on Linux); AW_NO_SYSTEM_INSTALL=1 prints the commands and exits 1 instead

# Rule records (rules/engineering/csharp/*.yaml + schemas/rule-record.schema.json; docs/csharp/rule-records.md)
node scripts/check-rules.mjs [--json]   # Every §4b rule has a record; every active check exists; pending ones wait on their PR

# Environment and dev container
scripts/check-env-example.sh            # Every env var the code reads is in .env.example (ENV-EXAMPLE-001..004); exit 1 on drift
# .devcontainer/ is for humans only, never the agent sandbox (no Docker-in-Docker); see docs/devcontainer.md
# Label scheme: .github/labels.yml (type:, area:, size:, ring:). Applying it to the repo is an owner settings change; until then
# the issue forms' labels don't exist and GitHub drops them silently

# Bash tests (CI: scripts/ci/bash-tests.sh runs every tracked suite, independent ones in parallel and
# its SERIAL list, the timing-sensitive ones, one at a time afterwards; --serial prints them with reasons)
bash scripts/ci/bash-tests.sh   # each suite runs with its own fresh temp HOME and AW_STATE_DIR and without the lane environment (AW_META_LANE*, AW_SINDRI_CHILD, TMUX, ...) (tools keep working through GIT_CONFIG_GLOBAL, DOTNET_ROOT, NUGET_PACKAGES, npm_config_cache)
bash providers/tests/install.test.sh
bash scripts/tests/sync-rules.test.sh
bash scripts/tests/test-grep.test.sh
bash scripts/tests/check-deps.test.sh
bash scripts/tests/check-rules.test.sh
bash scripts/tests/check-launcher.test.sh   # launcher.json schema, fixtures, built-ins and argvHash goldens (node scripts/check-launcher.mjs; Ajv is a dev dependency of scripts/)
bash scripts/tests/check-fleet.test.sh      # fleet state schema, its valid and invalid fixtures, and the /proc-shaped sessions tree (node scripts/check-fleet.mjs; Ajv as above)
bash scripts/tests/check-scoping-schemas.test.sh # schemas/scoping/: every schema compiles, its examples get their verdicts, seeds.json matches the recipe's intake (node scripts/check-scoping-schemas.mjs; Ajv as above)
bash scripts/tests/check-profile-doc.test.sh # docs/csharp/sindri-profile.md names every SindriProfile key (node scripts/check-profile-doc.mjs)
bash scripts/tests/scaffold.test.sh
bash scripts/tests/check-env-example.test.sh
bash scripts/tests/binaries-lock.test.sh
bash scripts/tests/install-deps.test.sh
bash scripts/tests/denylist-check.test.sh
bash scripts/tests/owner-check.test.sh
bash scripts/tests/repo-templates.test.sh
bash scripts/tests/probe.test.sh
bash scripts/tests/find-duplicate-skills.test.sh
bash scripts/tests/dotnet-verify-packages.test.sh
bash scripts/tests/install-scorer-audit.test.sh
bash scripts/tests/install-sindri.test.sh
bash scripts/tests/sindri-prove.test.sh
bash scripts/tests/sindri-ui-fixture.test.sh
bash scripts/tests/forge-fixture.test.sh    # scripts/forge-fixture.sh over the real aw-forge-fake (dotnet build once): the PR babysitter's fixture PR set, every variant, the guard, --github refusals and dry run
bash scripts/tests/sindri-app.test.sh
bash scripts/tests/readiness-baseline.test.sh
bash scripts/tests/agent-token.test.sh
bash scripts/tests/ci-changed-areas.test.sh
bash scripts/tests/ci-bash-tests.test.sh
bash scripts/tests/ci-lane-budgets.test.sh
bash scripts/tests/ci-no-any.test.sh
bash scripts/tests/tmux-isolation.test.sh   # scripts/ci/tmux-isolation.sh: no test runs a tmux outside scripts/test-tmux.sh, -L, -S or AW_TMUX
bash scripts/tests/test-tmux.test.sh
bash scripts/tests/sindri-smoke-normalize.test.sh
bash scripts/tests/scoping-fixture.test.sh
bash scripts/tests/line-fixture.test.sh
bash scripts/tests/meta-launch.test.sh      # Linux and macOS (golden transcript; needs cc on macOS)
bash scripts/tests/meta-version.test.sh     # the installed aw-meta's version and the launcher's staleness warning (Linux and macOS)
bash scripts/tests/install-meta.test.sh
bash scripts/tests/meta-teams.test.sh
bash config/hooks/tests/codex-adapter.test.sh
bash config/hooks/tests/cursor-adapter.test.sh
bash config/hooks/tests/provider-install-hooks.test.sh
bash config/hooks/tests/probe-log.test.sh
bash config/hooks/tests/judge-health.test.sh
bash config/hooks/tests/sindri-nudge.test.sh
bash config/hooks/tests/failopen.test.sh
bash config/hooks/tests/meta-hooks.test.sh
for t in config/lib/tests/*.test.sh; do bash "$t" || echo "FAILED: $t"; done  # merge-hook, levers, installers, and judge-cs-hook-surface (the hook contract: real hooks vs the C# judge, and the TS judge when judge/dist is built; needs dotnet)

# Levers and probe (default provider: claude)
scripts/install-<lever>.sh --provider claude|codex|cursor
scripts/probe.sh [--provider claude|codex|cursor] on|off|status
scripts/install-scorer.sh               # Build scorer, install CLI + launchd job
sindri doctor                           # Sindri health checks (ok / warn / fail + fix)
sindri index setup && sindri index build    # code index (Ollama + graphify, offline)
sindri evolve init && sindri evolve check --changed   # registry + module eval suites (heavy: takes a heavy-job slot)
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
AW_DENYLIST=<file:ABSPATH|env:VAR> scripts/denylist-check.sh <file>...   # Work identifiers in PR text; the list is injected through AW_DENYLIST (0600-or-stricter file or env var), never an argument: prints only file:line and a hit count; exit 0 none, 1 hits, 2 fails closed (unset/invalid pointer, bad or missing list; see the script header)
scripts/owner-check.sh <stage> up [--seed] [--services a,b] [--labeling] [--session] [--repo NAME] | down   # Owner only: the scratch Sindri instance (port 7480) for a stage's owner check (context spec, owner-check outline steps 1-4 and 7)
scripts/sindri-app.sh [--rebuild] [<sindri args>]   # `sindri-app`: the C# Sindri in one command; publishes sindri (framework-dependent) + sindri-ui (NativeAOT) for this platform into $AW_STATE_DIR/bin/sindri-app/<stamp>/ when the source changed, then runs it (bare: start the control plane and open the app); links ~/.local/bin/sindri-app (AW_SINDRI_APP_NO_LINK=1 skips). The TS `sindri` is untouched
scripts/forge-fixture.sh up --fake <comments|ciRed|ciFlaky|conflict|stacked|noDocs|sourceFix|foreignPush|secret|all> | event merge-base | status | list | down [--all]   # The PR babysitter's fixture PR set (tests/fixtures/forge/pr-set/) loaded into the file-backed fake forge in a guarded temp home; prints the PR link and the answer key (PB-X). `up --github <owner/repo> <variant>` is the owner's, on a scratch repo (FORGE_FIXTURE_GITHUB_SCRATCH confirms it)
scripts/sindri-ui-fixture.sh up|stall|unstall|drift|down   # The Sindri UI's proof fixture in a guarded temp AW_SINDRI_HOME, through sindri-prove.sh (UI spec §18.2; --large reserved)

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
| `csharp` | C# build (in progress) — package shape, dependency rules, types, errors, DI, JSON, AOT scope, analyzers | `src/**`, `**/*.cs`, `**/*.csproj`, `**/*.slnx`, `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `nuget.config`, `BannedSymbols*.txt`, `deps.yaml`, `schemas/deps.schema.json`, `**/.editorconfig`, `CodeMetricsConfig.txt`, `**/PublicAPI.*.txt`, `**/packages.lock.json` | `.agents/rules/csharp.md` |
| `design` | Design pipeline artifacts — tokens, .impeccable.md, DESIGN_SYSTEM.md, design-* skills | `design-tokens.json`, `.impeccable.md`, `planning/DESIGN_SYSTEM.md`, `skills/design-*/**` | `.agents/rules/design.md` |
| `hooks` | Safety and context hooks — per-provider adapters, protocols, installation | `config/hooks/**`, `config/settings.json` | `.agents/rules/hooks.md` |
| `mcp-servers` | MCP servers registered globally with every provider — available in every session | always | `.agents/rules/mcp-servers.md` |
| `skills` | Skill structure, preamble, output directories, pipeline, and installation | `skills/**`, `bootstrap/**` | `.agents/rules/skills.md` |
| `testing` | Test infrastructure, coverage policy, and test count baseline | `**/*.test.ts`, `**/*.spec.ts`, `**/vitest.config.ts` | `.agents/rules/testing.md` |
<!-- === RULES INDEX END === -->
