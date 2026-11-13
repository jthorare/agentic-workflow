# AGENTS.md — Agentic Workflow

> Agentic Workflow — provider-agnostic agent workflow toolkit for Claude Code, Codex, and Cursor: 59 native skills + 5 fetched external packs (impeccable, emil-design-eng, taste-skill, tdd-artifact, ui-sdk), config archive, repo bootstrapper, and a token-efficiency tool (rtk).

Domain-specific rules live in `.agents/rules/` — one file per rule, the only copy. `.claude/rules` and `.cursor/rules/*.mdc` are symlinks to it (auto-loaded by Claude Code and Cursor), and `CLAUDE.md` is a symlink to this file. Codex reads the Rules Index at the bottom of this file. After adding or removing a rule, run `scripts/sync-rules.sh`.

## Required Context

| Document | Purpose |
|----------|---------|
| `planning/ARCHITECTURE.md` | System components and data flow |
| `planning/PROVIDERS.md` | Claude Code / Codex / Cursor support matrix and canonical layout |
| `planning/API_CONTRACT.md` | Sindri CLI output contract |
| `planning/CODE_STYLE.md` | Code style: C# by its rule files |
| `planning/TESTING.md` | Test strategy and coverage targets |
| `planning/ERD.md` | Sindri ledger and code-index SQLite schemas |
| `skills/_shared/capabilities.md` | Capability → tool map used by all skill text |

## Tech Stack

| Layer | Technology |
|-------|------------|
| LSP (Serena) | Docker (Dockerized Serena MCP server via `scripts/serena-docker`) |
| Node | Setup prerequisite only (`.nvmrc`): `dembrandt`, `prism-mcp` and `xcodebuildmcp` through `npx`, and Ajv for the schema checkers in `scripts/package.json`. No toolkit package is built with it |

**The installed build is C#.** The toolkit moved to C# in one switch (spec `docs/superpowers/specs/2026-10-23-cutover-design.md`; earlier design `2026-10-09-agent-readiness-design.md` §4a–§4e). `scripts/install-cs.sh` (run by `setup.sh`) publishes the binaries `sindri`, `sindri-ui`, `judge`, `aw-packets`, `bugfix-state`, `ui-evidence` and `aw-readiness` into `$AW_STATE_DIR/bin/cs/<stamp>/` and links wrappers into `~/.local/bin`; `aw-meta` stays opt-in through `./setup.sh --with-meta` (`scripts/install-meta.sh`; Linux and macOS, any other OS fails the install loudly; `docs/csharp/meta.md` "OS support"). There is no Node-built toolkit package, no `scorer` and no TS `sindri`; `scripts/cutover-clean-ts.sh` removes what an older install left. Node stays a setup prerequisite for `dembrandt`, `prism-mcp`, `xcodebuildmcp` and the schema checkers only. Sindri is a C# **application** (control plane, workflow engine, context retrieval engine; factory addendum §17.29, decision Q7; overview in `docs/sindri/README.md`), and the finished tool ports are its blocks. The C# code is a **reimplementation in idiomatic C#, not a translation** (type-driven JSON through `AwJson`; contracts defined by the C# tests; `.agents/rules/csharp.md` "Porting a TS package" for any future port). No TS state is migrated. The **hook contract** stays frozen: only what the bash hooks and statusline read (the commands and arguments they call, the JSON fields they `jq`, the exit codes and stderr prefixes they branch on, and the files they write, such as `judge/sessions`, `judge/outbox` and `judge/teammate-names.jsonl`; spec §4c), checked in CI by running the real hook scripts against the C# binaries. `aw-readiness`, `aw-packets` and `aw-meta` write only under `readiness/`, `packets/` and `meta/` (`aw-meta` also appends to its team's running log, and its gate and merge commands act on the team's checkout and PRs: `git fetch`, a gate worktree, the configured gate steps, `gh pr merge`, and removing finished worktrees with `git worktree remove` on merge, close and sweep triggers, never by age and never forced; macOS reads memory, swap and the process table through `sysctl`, `vm_stat`, `ps` and `lsof` where Linux reads `/proc`, and a figure a macOS tool can't read is `AW-META-010` "unavailable on macOS", never a guess; owner, 2026-10-14; macOS 2026-10-17). `setup.sh` no longer runs Onyx; `--no-onyx` is accepted and does nothing. `--with-sindri` is accepted and does nothing: the C# Sindri is always installed.

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10 (LTS), SDK pinned in `global.json`; NativeAOT for hook and CLI entry points. The Sindri host is framework-dependent (its Temporal worker can't be trimmed; README X54) |
| Language | C# (latest), nullable on, warnings as errors |
| HTTP (an http host) | ASP.NET Core minimal APIs (`CreateSlimBuilder`), loopback only |
| UI (the Sindri app) | [Avalonia UI](https://avaloniaui.net/) (MIT core only, no paid add-ons), MVVM with compiled bindings; a client of Sindri's HTTP API with no logic of its own. Desktop (macOS, Linux, Windows) first; iOS, Android and browser heads later from the same views. Spec §4a "UI" |
| Database | `Microsoft.Data.Sqlite` with its bundled `e_sqlite3` (shipped beside each AOT tool), new C# schemas, WAL mode |
| MCP | Official MCP C# SDK (approved in the AOT spike; no package uses it since the bridge was retired) |
| Workflow engine (Sindri) | [Temporal](https://temporal.io) through the `Temporalio` .NET SDK (pinned, nuget.org): a local `temporal server start-dev` on loopback with its own SQLite file under `$AW_SINDRI_HOME`, started by `sindri serve`; one `GraphWorkflow` walks each run of a workflow graph (the recipe `RecipeWorkflow` is being retired, issue 2258); every transition is mirrored into the ledger (engine spec §0, §5; decision Q8, 2026-10-11) |
| JSON | System.Text.Json, one source-generated context per package |
| Logging | `Aw.Kernel.Telemetry`: JSON lines on stderr (`AW_LOG_LEVEL`), OpenTelemetry spans and metrics, exporting off by default, scrubbed by `Aw.Redaction` |
| Analyzers | NetAnalyzers, Meziantou, Roslynator, BannedApiAnalyzers, PublicApiAnalyzers (one owner per rule, `.editorconfig`) |
| Test | xUnit v3 on Microsoft.Testing.Platform (decided, R7), lanes `unit` / `integration` / `heavy` |
| Packages | Central Package Management (`Directory.Packages.props`), locked restore, nuget.org only (`nuget.config`) |

## Directory Structure

```
agentic-workflow/
├── skills/        # 59 provider-neutral skills (linked into each provider's skills dir)
├── bootstrap/     # /bootstrap skill — repo documentation generator
├── config/        # Settings, MCP config, statusline, safety hooks, sindri-nudge SessionStart hook, sindri-tasks UserPromptSubmit hook, meta Stop/outbox hooks, non-Claude lane hooks (meta-lane-gate, session-id-report), the opt-in aw:fleet session-state hook (fleet-state, `--with-fleet`), the reply voice (`voice/`, inlined into each provider's global instructions) (+ hooks/adapters/ per provider)
├── providers/     # Per-provider installers (claude, codex, cursor)
├── context/       # Pinned external deployments: code-index.lock (graphify, Ollama model), graphify.pin
├── planning/      # Project documentation
├── src/           # C# build: Aw.Kernel (+ .Data, .Telemetry), Redaction, Aw.Analyzers, the tool ports (Packets, Readiness, BugFix; Judge, Evidence), UiDriver (the generic Avalonia UI driver and `aw-ui-driver`), Meta (aw-meta, the /meta harness), Toolkit (the `Aw.Sindri.Toolkit` SDK that /new-toolkit toolkits reference; packed by install-cs.sh, never referenced by Sindri), Forge (the forge port with GitHub and file adapters), CodeIndex (the Sindri code index block: symbols, callers and callees per repo; library only) and later the new Sindri app — each Core · Features · Infrastructure · Host
├── evals/         # Skill evals (#1107): evals/<skill>/{routing,behaviour}/<case>, fixture.sh, graders/_selftest, status.md; _lib, mocks, plugin, stubs (gh, curl, git); run by scripts/skill-evals.sh, rule in .agents/rules/skills.md
├── tests/         # C# tests: Aw.<Pkg>.Tests (by feature), Aw.<Pkg>.Testing (fakes, builders, contract bases), Aw.Architecture.Tests
├── global.json · AgenticWorkflow.slnx · Directory.Build.props · Directory.Build.targets · Directory.Packages.props · nuget.config · .editorconfig · CodeMetricsConfig.txt · BannedSymbols.txt
├── .agents/rules/ # Glob-scoped domain rules (.claude/rules and .cursor/rules link here)
├── .serena/       # Serena LSP project configuration
├── scripts/       # Utility scripts (sync-rules.sh, check-deps.mjs, serena-docker, probe.sh, install-cs.sh, cutover-clean-ts.sh)
├── schemas/       # JSON Schemas for repo config (deps.schema.json, rule-record.schema.json) and the parsed design doc (library/, docs/conventions/design-docs.md)
├── rules/         # Rule records (factory addendum §17.6): rules/engineering/csharp/*.yaml, one per §4b rule
├── deps.yaml      # Declared package graph: tiers and every project/compose/cli/db/file edge (spec §4b.1a)
└── setup.sh       # One-command setup for every installed provider
```

## Commands

```bash

# C# build
dotnet build -warnaserror               # Whole solution (AgenticWorkflow.slnx); zero warnings
# Lane runs (xUnit v3 on Microsoft.Testing.Platform, MTP options only). Exit 8 = no test matched the filter in some
# project (e.g. an integration-only project in the unit lane): ignored for filtered runs only. The unfiltered
# `dotnet test --solution AgenticWorkflow.slnx` keeps exit 8 as a failure.
dotnet test --solution AgenticWorkflow.slnx                                                     # All lanes; exit 8 (a project with no tests) fails
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8          # Unit lane (< 10 s per package)
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=integration" --ignore-exit-code 8   # Temp SQLite files, WebApplicationFactory (< 60 s per package)
dotnet test --solution AgenticWorkflow.slnx --filter-not-trait "Lane=heavy" --ignore-exit-code 8     # Everything except containers, browsers and index builds
node scripts/ci/lane-budgets.mjs        # After a Release build: each package's unit/integration lane wall clock vs its budget (LANE-PKG-001)
dotnet format --verify-no-changes       # Style and analyzer fixes are applied (CI and the meta gate; no pre-commit hook runs it)
scripts/dotnet-verify-packages.sh       # Lock files present and unchanged, signature-verified locked restore, dotnet nuget verify --all
# Add a package: /new-package (node scripts/scaffold/new-package.mjs; contract docs/csharp/scaffolds/new-package.md)
# Add a use case: /new-slice (node scripts/scaffold/new-slice.mjs; contract docs/csharp/scaffolds/new-slice.md) — test, request and response, service, endpoint, registration
# Add a toolkit (outside the repo): /new-toolkit (node scripts/scaffold/new-toolkit.mjs --name <Name> --tool <snake_case> [--needs database:cache,disk:1g,connection:github:me] [--dir D]; contract docs/csharp/scaffolds/new-toolkit.md) — a Sindri toolkit project under $AW_SINDRI_HOME/toolkits/ with its own build rails and one red test; needs the Aw.Sindri.Toolkit package in $AW_SINDRI_SDK_FEED, which scripts/install-cs.sh packs; refusals NT-*
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
dotnet run --project src/UiDriver/Aw.UiDriver.Cli -- [--dir D] <ready|tree|search|props|click|type|key|scroll|wait-for|screenshot|window|action> [flags]   # aw-ui-driver (docs/csharp/ui-driver.md): drives any Avalonia app that calls UseAwUiDriver() and was started with AW_UI_DRIVER=1 (loopback, per-run secret in $AW_UI_DRIVER_DIR); exit 0 ok, 1 usage or invalid, 2 no element / not actionable / timeout, 4 unreachable or app failed
dotnet run --project src/Forge/Aw.Forge.Host -- <parse|open|pull|threads|seed thread|seed checks|seed advance> …   # aw-forge-fake, the C# forge port's CLI (docs/csharp/forge.md); adapter by AW_FORGE_ADAPTER (file default | github), hosts AW_FORGE_HOSTS, credential AW_FORGE_CREDENTIAL (env:NAME | command:argv, default env:AW_FORGE_TOKEN); one JSON document on stdout; exit 0 ok, 1 invalid or not found, 2 usage, 3 refused or forge unavailable
dotnet test --project tests/Aw.Forge.Tests --filter-trait "Lane=heavy"   # Owner's GitHub check: the read-side forge contract against a scratch repo; skips unless AW_FORGE_GITHUB_REPO=owner/name and AW_FORGE_TOKEN are set (docs/csharp/forge.md)
dotnet run --project src/Sindri/Aw.Sindri.Host -- [--headless | serve | status [--follow] [--json] | stop | profile validate | views rebuild [--no-start] | queue [--json] | items [--state S] [--repo R] [--json] | item add <title> [--repo R] | item close <item> [--cancelled] [--version N] | ledger [--after N] [--item X] [--type T] [--max N] [--json] | doctor | services [--json] | services run <name> [--detach] [--json] | services pause|resume <name> | jobs [--state S] [--kind K] [--json] | jobs show <id> [--json] | jobs cancel <id> | jobs resume <id> | agents [--provider P] [--state S] [--repo R] [--json] | agents show <provider>/<id> [--json] | setup [--json]]   # the C# Sindri app (S1 app shell, docs/csharp/sindri.md; `views rebuild` replays the ledger into views.db, the fix when the projector stalls; `agents` lists every live agent session on the box, read-only and never starting serve: exit 0 ok, 1 serve is down or a source failed, 2 error; `setup` prints where setting Sindri up stands, one line per step, read-only and never starting serve: exit 0 when every step is done or skipped, 1 when one is open or serve is down, 2 error); state in AW_SINDRI_HOME (default $AW_STATE_DIR/sindri-app); exit 0 ok, 1 attention, 2 error
dotnet run --project src/Sindri/Aw.Sindri.Host -- profile init [--json]   # write the default profile when none exists (no repos; refuses when one exists, exit 2; runs without the control plane): then `repo add <path>`; edits apply on save, there is no approval (SO-A3c).
dotnet run --project src/Sindri/Aw.Sindri.Host -- profile set <dotted.key> <value> [--json]   # set ONE scalar key of the live profile in place (#1740): comments, order and other quoting stay, missing parent maps are created, the edited text is validated before the atomic write, a flow-style parent or a map/list target is refused; applies on save; refused when the CLI has no readable per-start human token or is a launched session (SND-AUTH-002); runs without the control plane; exit 0 set or unchanged, 1 invalid or a layout it won't edit, 2 usage or refused
dotnet run --project src/Sindri/Aw.Sindri.Host -- repo onboard [<path>] [--detach] [--json]   # onboard a repo of the live profile by hand as a job and follow it: hook slot (no secret-check hook until the installer ships) and `repo.onboarded`; the setup service does the same on its own (exit 0 onboarded, 1 failed or serve down, 2 refused: SND-SETUP-004/007)
dotnet run --project src/Sindri/Aw.Sindri.Host -- projects [--all] [--json] | project show <project> [--json] | project add (--linear <key> | --name <text>) [--link URL]... [--state S] [--note T] [--target D] | project edit|remove|hide|unhide <project> [--version N] [...]   # the Projects page's owner writes (spec 2026-10-29-sindri-projects-page-design.md §5.3), through the running control plane: prj_ ids for edit and remove, a linear:project/<id> key for hide; without --version the write reads the project's version first (`projects` and `project show` are reads an agent session may run, with Linear text fenced); an agent session's writes are refused (SND-AUTH-002); exit 0 ok, 1 not running or views stalled, 2 refused
dotnet run --project src/Sindri/Aw.Sindri.Host -- context pin set <name> [--id ID] [--summary T] [--alias W]... [--feature K]... [--clear-aliases] [--clear-features] [--if-hash H] [--detach] [--no-start] [--json] | context pin set --from-file <list.txt> | context pin remove <id> [--with-folder-rules] | context pin list [--json]   # concept pins (concept pins spec §3.6; docs/csharp/sindri-context.md "Pinning concepts"): write `index.concepts` of the profile as the PinConcepts job (follows it like `repo add`; a session may run it, a human starts the control plane unless --no-start); it applies on save, the last line names the new hash; `list` reads the live profile in process; exit 0 written, 1 failed or not running or session refused, 2 invalid input
dotnet run --project src/Sindri/Aw.Sindri.Host -- skills [--stats] [--json]   # the skill blocks (skills/*/block.yaml, docs/csharp/sindri-skills.md): lists the loaded blocks and the path each came from (asks serve; exit 0 ok, 1 not running)
dotnet run --project src/Sindri/Aw.Sindri.Host -- library sync [--detach] [--json] [--no-start]   # the design-doc library feed's tick now (`index sync library` under the doctor row's name; PR review page spec 2026-11-04 §5.2; docs/csharp/sindri.md): reads the `aw` root and the vaults of `context.sources.library`; the `library` row of `sindri doctor` shows what the last pass found; exit 0 synced, 1 the source ended with a code or Sindri is not running, 2 invalid or refused
dotnet run --project src/Sindri/Aw.Sindri.Host -- context page list [--suggested] [--concept C] [--repo R] [--json]   # the product's pages with their actions and concepts through the running control plane (reads never start it): no flag = pages a person named, --suggested = pages the map offers that nobody named; GET /sindri/context/pages, MCP context_pages; exit 0 ok, 1 not running / no map / SND-CONTEXT-257,258, 2 error (docs/sindri/context.md "Pages")
dotnet run --project src/Sindri/Aw.Sindri.Host -- context page set <name> [--id ID] [--repo R] [--surface KEY] [--action "Name[=KEY][->c1,c2]"]... [--if-hash H] [--dry-run] [--detach] [--no-start] [--json] | page set --from-file <pages.yaml> | page remove <id>   # name or remove pages in the profile's index.pages as a job (POST /sindri/context/pages), checked against the current map; applies on save and prints the new hash; --dry-run checks like a write and writes nothing; exit 0 written, 1 attention (unknown name/key, no map, not running), 2 invalid or refused (docs/sindri/context.md "Pages")
dotnet run --project src/Sindri/Aw.Sindri.Host -- wf validate <file> [--json]   # check a workflow graph file (issue 2258, docs/csharp/sindri-graph.md) in process, no serve needed (it compiles each code node once into `<home>/code-cache`): one trigger, connected nodes, no cycle, upstream-only references, known block types and names; the block catalog is empty until PR 6, so every node type is unknown for now; exit 0 pass, 1 findings, 2 usage or tool error
dotnet run --project src/Sindri/Aw.Sindri.Host -- wf list [--json] | wf show <slug> [--json] | wf runs <slug> [--json] | wf run-show <run> [--json]   # the workflow reads (issue 2258, PR 8a, docs/csharp/sindri-graph.md "Reading workflows and runs"): GET /sindri/workflows[/{slug}[/runs]] and /sindri/workflow-runs/{run}, MCP workflow_list|get|runs|run_get; they read the routes and never start serve; exit 0 ok, 1 not running, 2 unknown or invalid
dotnet run --project src/Sindri/Aw.Sindri.Host -- wf create <slug> (--template <name> | --file <spec.json>) | wf edit <slug> --base <n> (--ops <json> | --ops-file <f> | --file <spec.json>) | wf validate --slug <slug> [--revision <n>] | wf activate <slug> [--revision <n>] | wf pause <slug> | wf delete <slug> | wf blocks | wf docs [<words>] [--page <slug>]   # the workflow writes (issue 2258, PR 8b, docs/csharp/sindri-graph.md "Writing workflows"): POST /sindri/workflows, PUT|DELETE /sindri/workflows/{slug}, POST .../ops|activate|pause|validate, MCP workflow_create|apply_ops|replace|validate|activate|pause|delete|run|cancel; a stale --base is refused with the current revision; they start serve like `wf run`; add `--json` to any; exit 0 ok, 1 not running, 2 refused
# Sandbox (built, SB-1 to SB-9; docs/csharp/sindri-sandbox.md, spec 2026-10-18-sindri-sandbox-design.md; Linux bubblewrap and macOS Seatbelt, `src/Sindri/Aw.Sindri.Infrastructure/Sandbox/SeatbeltSandbox.cs`): `sindri sandbox learn off [--repo R] [--json]` (ends network learn mode early, a tightening, no approval; through the running control plane), `sindri sandbox explain <path|host|command> [--plan F] [--json]` (local, reads only the plan file), `sindri sandbox denials [--run L] [--since T] [--json]` (a query through the running control plane), `sindri sandbox exec <launch|run-area> [--json] [-- <command...>]` (the command in a fresh read-only copy of the launch's sandbox, counted against the session cap) and `sindri sandbox login <claude-code|codex>` (makes the target's own agent home under `$AW_SINDRI_HOME/agent-homes/`, installs the toolkit hooks into it, runs the agent's sign-in on your terminal); an agent session may run all of them (PR 2 of the agent-first spec); what works inside a Step is in the sandbox doc
bash src/Sindri/aot-smoke/run.sh         # sindri, framework-dependent (the Temporal SDK has no NativeAOT; needs scripts/binaries-lock.sh fetch temporal): publish (on a copy), drive start, a refused second serve, the approve preview, a session's approve and stop and the human's approval under a pty (no typed challenge, q93), views rebuild requested by a session and by the human, status (its views and health lines, and --json's apiVersion), /sindri/mcp's initialize and tools/list, a one-node Code workflow made, activated and run through the CLI with `sindri-code-runner` published beside `sindri` (its node returns {"sum":3}), stop and doctor (on a scratch HOME) in a temp home, compare with expected.txt
dotnet run --project src/Sindri/Aw.Sindri.Ui.Desktop       # the Sindri app (S1b shell, U0 theme and shell; docs/csharp/sindri-ui.md): top bar, Home, Health, Settings, tray, single instance, live from the event stream; bare `sindri` opens it (sindri-ui next to sindri, or AW_SINDRI_UI)
bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh [<dir>]   # sindri-ui under NativeAOT: publish (on a copy) and fail on any IL2xxx/IL3xxx warning or a missing binary; copies it and its native libraries to <dir> when given

dotnet run --project src/Meta/Aw.Meta.Host -- <command> [--json]   # aw-meta, the /meta harness CLI (docs/csharp/meta.md): obligations (inbox-sync, stop-check, outbox-record, inbox-*), lanes-*, gate-run, gate-rerun, merge-check, merge-run, owner-*, health, watch, log-append, status, version, team-*, role-*; exit 0 ok, 1 attention, 2 usage or tool error
bash src/Meta/aot-smoke/run.sh           # aw-meta under NativeAOT: publish (on a copy), drive the obligation flow in a temp state dir, compare with expected.txt
scripts/install-fleet.sh [--provider claude] [--uninstall]   # The aw:fleet hook (Sindri session collector, CO-1): one state file per session under $AW_FLEET_DIR (or ./setup.sh --with-fleet); off: AW_FLEET_DISABLE=1 per session or --uninstall
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

# TLA+ specs (specs/tla/<Name>/; TLC through skills/formal-verify/scripts/tlc.sh)
scripts/tlc-check.sh [--root DIR] [--timeout SECONDS] [--witnesses] [<spec-dir>...]    # TLC on every specs/tla/*/ folder (or the given ones), once per *.cfg, on MC<Name>.tla when present; one summary line per check, TLC's output only when it isn't PASS; --root takes specs and models: paths from another repo, --timeout stops a check that runs too long (TIMEOUT line, exit 2), --witnesses also runs each <folder>/gaps/*.cfg and expects it to fail on the invariant its line 1 `\* witness: <Name>` names (WITNESS-CAUGHT, or WITNESS-WRONG / WITNESS-MISSED exit 1); CI job "TLA+ specs (TLC)"; exit 0 all pass, 1 a violation, 2 an error (or a folder without <Name>.tla or .cfg), 3 java or the jar missing (`bash skills/formal-verify/scripts/tlc.sh fetch`)

scripts/images-lock.sh check             # Shape of deploy/images.lock (every deployment image by index and per-platform digest; unresolved ones are `todo` rows) and that the Dockerfile names only pinned images; exit 0 ok, 1 a check failed

# Scoping benchmark (context scope spec §7, epic #2959): tests/fixtures/scoping/<repo>/questions.yaml, a question with its touch / read / decoy files
node scripts/scoping-bench.mjs --questions tests/fixtures/scoping/<repo>/questions.yaml [--k 25] [--field plain|question] [--only ids] [--out DIR] [--json]   # asks each question of the Sindri AW_SINDRI_HOME / AW_SINDRI_PORT name (`sindri context query --json`), prints touch recall, recall and P@K of the files in the top K, decoys hit, p50 / p95 seconds; exit 0 all answered, 1 a query failed, 2 usage
node scripts/scoping-bench.mjs --questions <file> --check [--root DIR]   # no query: every key path exists under DIR (default this repo; a clone at the key's commit for another repo) and none is listed twice; exit 1 on findings

# Old Onyx leftovers (#1598: Sindri no longer runs Onyx)
sindri onyx down --purge                # Once, on a box that ran the old `onyx up`: lists its labelled containers and volumes, the recorded keychain entry and onyx/, asks, then removes them; the human's verb (a session is refused, SND-AUTH-002); exit 0 done or nothing left, 1 a step failed, 2 refused

# External tools, once per box (setup.sh runs it): uv, graphify at sindri's pin, the code index's graphify environment and Ollama model from context/code-index.lock, both Temporal binaries, system packages (installed when missing)
scripts/install-deps.sh                 # AW_DRY_RUN=1 prints only; missing system packages are installed (brew on macOS; sudo + apt-get, dnf, yum, pacman or zypper on Linux); AW_NO_SYSTEM_INSTALL=1 prints the commands and exits 1 instead

# Code index pins (context/code-index.lock: graphify version, date and requirements hash, the Ollama embedding model and digest, disk need; context/code-index-requirements.txt is the hash-pinned requirements; spec docs/superpowers/specs/2026-10-19-sindri-code-index-design.md §9)
scripts/code-index-lock.sh check        # Shape of the lock, the requirements file's sha256, and parity with context/graphify.pin (the graphify pin install-deps.sh reads); exit 0 ok, 1 a check failed, 2 usage
scripts/code-index-lock.sh get <graphify-version|graphify-date|requirements-sha256|model|model-digest|disk-bytes>   # One pinned value (install-deps.sh reads these)

# Rule records (rules/engineering/csharp/*.yaml + schemas/rule-record.schema.json; docs/csharp/rule-records.md)
node scripts/check-rules.mjs [--json]   # Every §4b rule has a record; every active check exists; pending ones wait on their PR

# Environment and dev container
scripts/check-env-example.sh            # Every env var the code reads is in .env.example (ENV-EXAMPLE-001..004); exit 1 on drift
scripts/check-skills-toolkit.sh           # skills/, bootstrap/ and config/voice/ name the toolkit as ${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit} and the per-repo output dir as $AW_DIR (SKILLS-TOOLKIT-001, -002); exit 1 on drift, file:line per hit
scripts/check-prose.sh [--base REF] [--strict] [--counts] [path...]   # Vale (pinned in binaries.lock) with our own style (.vale.ini, .vale/styles/Aw: the reply voice's Structure rules and a slop list) on tracked Markdown in docs/, planning/, skills/, bootstrap/, config/voice/, AGENTS.md and README.md (issue 2919); `file:line:col: error|warning Rule: message` per hit; errors are vague verbs, hedges, slop, wordy forms, filler, dash runs and unparseable front matter, warnings the 80% rules (sentence and paragraph length, passive, noun stacks; --strict fails on them); --base keeps hits on lines added since REF (CI's gate job, so new slop can't land); --counts prints hits per directory; exit 0 no errors, 1 hits, 2 usage or vale missing
scripts/check-cs-control-bytes.sh       # tracked .cs files hold no control byte but tab, LF and CR (#2203: a raw 0x08 in a verbatim regex built clean and never matched); exit 1 with file:line and the byte in hex, 2 usage
scripts/check-parity.sh [--release] docs/parity/slack.md   # the Slack parity ledger (Messages spec W6, #3266): every row 1..N has a status with its detail (done + evidence, partial/differs + limit, planned Mn, missing #N, dropped: why, blocked + its P0 line); --release is the ship pass (only done, partial, missing #N, dropped); `file:line: row N: reason` per finding; exit 0 none, 1 findings, 2 usage
scripts/check-skills-git-mode.sh        # every skill that makes a worktree, branch, commit, PR or PR comment reads AW_GIT_MODE and refuses the write under `readonly` (skill-revamp spec §2.3); exit 1 on drift, file:line per hit
scripts/check-skills-worktree-path.sh   # every skill that runs `git worktree add` takes the path from skills/_shared/worktree-path.sh, the one worktree root per repo (#2765); exit 1 on drift, file:line per hit, 2 usage
scripts/check-skill-requires.sh [--skip-bins]   # every skill has a requires.yaml sidecar (bins, agents, mcp, skills; {} for none) naming things that exist, and exactly one Result block, holding the AW_RESULT gate line, and an opening that runs that gate before the first step (skills in scripts/check-skill-requires.unswept are skipped until swept; output contract: skills/_shared/output-contract.md); exit 1 with file: reason per finding, 2 usage; --skip-bins on a box without every binary
node scripts/check-skill-blocks.mjs [--root DIR]   # every skills/*/block.yaml (a Sindri block manifest, schemas/skill-block.schema.json, docs/csharp/sindri-skills.md): schema, unique block ids, args slots name declared inputs and cover the required ones, `success` is a mapped outcome, every output's `from` names a key or `artifacts[<file>]` of the SKILL.md's ```aw-result example, derived fact keys unique and <= 64 characters; exit 1 with file: reason per finding, 2 when Ajv or yaml is missing (`npm ci --prefix scripts`)
scripts/skills-toolkit-rewrite.sh [--dry-run]   # The scripted fix for that drift; idempotent, so a skill PR in flight rebases by re-running it
# .devcontainer/ is for humans only, never the agent sandbox (no Docker-in-Docker); see docs/devcontainer.md
# Label scheme: .github/labels.yml (type:, area:, size:, ring:). Applying it to the repo is an owner settings change; until then
# the issue forms' labels don't exist and GitHub drops them silently

# Bash tests: `bash scripts/ci/bash-tests.sh` runs every suite (CI's "Bash tests" job). The list, with what each suite covers, is
# docs/testing/bash-suites.md: read it when you gate a change to a script or hook, or add a suite.
bash scripts/ci/bash-tests.sh

# Levers and probe (default provider: claude)
scripts/install-<lever>.sh --provider claude|codex|cursor
scripts/probe.sh [--provider claude|codex|cursor] on|off|status
scripts/install-headroom.sh --provider claude|codex   # headroom as a proxy wrapper only (no MCP server): installs the CLI when missing and the `aw-headroom` launcher; setup.sh runs it per provider (Cursor: skipped), fail-open
scripts/install-tldraw.sh [--dry-run]   # tldraw offline, the canvas agents drive for diagrams: brew cask on macOS, the pinned AppImage on Linux; prints the app's Develop → Install Agent Skills step until a provider has its skill (setup.sh runs it; skip with --no-tldraw), fail-open
aw-headroom claude|codex [args]         # Run the tool through a loopback headroom proxy chained in front of its current upstream (e.g. EasyCLIProxyAPI); nothing on disk is edited; `aw-headroom upstream|status|stop`
sindri doctor                           # Sindri health checks (ok / warn / fail + fix)
sindri repo onboard [<path>]            # add + pre-commit hook + first index build
GH_TOKEN="$(scripts/agent-token.sh [--repo NAME])"  # Owner/session launcher only, never inside an agent session: 1h agent App token
AW_DENYLIST=<file:ABSPATH|env:VAR> scripts/denylist-check.sh <file>...   # Work identifiers in PR text; the list is injected through AW_DENYLIST (0600-or-stricter file or env var), never an argument: prints only file:line and a hit count; exit 0 none, 1 hits, 2 fails closed (unset/invalid pointer, bad or missing list; see the script header)
scripts/owner-check.sh <stage> up [--seed] [--services a,b] [--labeling] [--session] [--repo NAME] | down   # Owner only: the scratch Sindri instance (port 7480) for a stage's owner check (context spec, owner-check outline steps 1-4 and 7)
scripts/context-first-run.sh up|continue|status|down [--dry-run] [--no-ui] [--no-embeddings] [--no-graph] [--stop-after N]   # Owner only: the first real context run on this repo in a scratch home (port 7481): profile, approval stop, serve, first code index build, map publish and the app; docs/sindri/first-context-run.md
scripts/model-limit-check.sh up|resume [--cli]|limit|unlimit <claude-code|codex>|status|down [--dry-run]   # Owner only (#2112): the model-limit pause and Resume on fake claude and codex in a scratch home (port 7482): a map refresh pauses on model-limit, claude-code comes back, Resume, the map finishes with labels
scripts/install-cs.sh [--publish | --link] [--rebuild] [--dry-run]   # The one install of the C# binaries (also ships scripts/heavy.sh as <build>/heavy and scripts/meta-gate-reserve.sh as <build>/meta-gate-reserve; `--link` links $AW_STATE_DIR/bin-r1b/heavy and bin-r1b/meta-gate-reserve to bin/cs/current/<name>) (sindri, sindri-ui, judge, aw-packets, bugfix-state, ui-evidence, aw-readiness): publishes sindri (framework-dependent), ui-evidence (framework-dependent) and the rest (NativeAOT) for this platform into $AW_STATE_DIR/bin/cs/<stamp>/ when the source changed, `--link` points `bin/cs/current` at it and writes the ~/.local/bin wrappers. Bare `sindri` starts the control plane and opens the app
scripts/forge-fixture.sh up --fake <comments|ciRed|ciFlaky|conflict|stacked|noDocs|sourceFix|foreignPush|secret|all> | event merge-base | status | list | down [--all]   # The PR babysitter's fixture PR set (tests/fixtures/forge/pr-set/) loaded into the file-backed fake forge in a guarded temp home; prints the PR link and the answer key (PB-X). `up --github <owner/repo> <variant>` is the owner's, on a scratch repo (FORGE_FIXTURE_GITHUB_SCRATCH confirms it)
scripts/sindri-ui-fixture.sh up|stall|unstall|drift|down   # The Sindri UI's proof fixture in a guarded temp AW_SINDRI_HOME, through sindri-prove.sh (UI spec §18.2; --large reserved)
scripts/skill-evals.sh [--skills a,b | --changed | --all] [--family a,b] [--kind routing|behaviour|both] [--runs N] [--max-cost-usd N] [--family-max-cost-usd N] [--gate warn|block] [--preamble on|off] [--dry-run | --selftest | --lint-descriptions | --status]   # Skill evals (#1107; spec docs/superpowers/specs/2026-10-25-skill-revamp-evals-design.md): stages evals/ and skills/ outside any git repo, rewrites only the staged defaults (AW_PREAMBLE off unless --preamble on, AW_GIT_MODE readonly, AW_RESULT 1), runs `claude plugin eval` under `env -i` with stub gh/curl first on PATH, keeps the $60/day tally in $AW_STATE_DIR/skill-evals/spend.jsonl; bootstrap/ counts as a skill dir; a skill's family set (evals/_families/<family>/<member>/, first-Skill-call trace graders) runs too when its description changed or `--family` names it, under its own $12 cap; lane leads and the owner run it for real (it spends shared usage), builders only --selftest, --dry-run, --lint-descriptions, --status; exit 0 pass, 1 below threshold, 2 budget refused, 3 environment, 4 routing gate missed (--gate block), 64 usage

# Setup (from repo root)
./setup.sh --providers claude,codex,cursor --dry-run   # Print every change, write nothing
./setup.sh [--providers ...]            # Default: detect installed CLIs. Links skills, installs
                                        # hooks + levers + MCP servers per provider (and removes retired ones), builds
                                        # the C# binaries (install-cs.sh), Serena image
scripts/cutover-clean-ts.sh [--dry-run]  # Remove what a TS install left (wrappers, launchd jobs, TS state); setup.sh runs it before linking the C# binaries
```

After installing for Codex, open `codex` and run `/hooks` to trust the `aw:*` hooks.

## Merge Gate

Before merging any PR:
1. `dotnet build -warnaserror` reports zero warnings; `dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8` and `dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=integration" --ignore-exit-code 8` pass within their budgets (`node scripts/ci/lane-budgets.mjs`, LANE-PKG-001; the heavy lane runs only by hand, by a lane or the owner on their own box, never on CI (owner, 2026-10-29)); `dotnet format --verify-no-changes` is clean, `scripts/dotnet-verify-packages.sh` passes, and the ratchet baseline block at the end of `.editorconfig` didn't grow
2. Every bash suite passes (`bash scripts/ci/bash-tests.sh`; the list is in `docs/testing/bash-suites.md`)
3. `scripts/sync-rules.sh --check` passes (rule links and Rules Index match `.agents/rules/`)
4. `node scripts/check-deps.mjs` passes (`deps.yaml` declares every edge the code has; a new edge is a reviewed change to it); `scripts/check-env-example.sh` passes (`.env.example` lists every environment variable the code reads)
5. `./setup.sh --providers claude,codex,cursor --dry-run` runs cleanly
6. `node scripts/check-rules.mjs` passes (every §4b rule has a rule record, and every check a record calls active exists in the tree)
7. When `skills/`, `evals/`, `bootstrap/` or `scripts/skill-evals.sh` changed: `scripts/check-skills-toolkit.sh`, `scripts/check-skills-git-mode.sh` and `scripts/check-skills-worktree-path.sh` pass, and `scripts/skill-evals.sh --selftest --all` passes (no model, no cost)

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
| `testing` | Test infrastructure for the bash suites; C# tests have their own rule | `**/tests/*.test.sh` | `.agents/rules/testing.md` |
<!-- === RULES INDEX END === -->
