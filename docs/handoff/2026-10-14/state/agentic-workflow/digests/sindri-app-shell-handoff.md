# Handoff: build the Sindri app — stage S1, the app shell

Paste this into a fresh session in ~/repos/agentic-workflow.

---

/specToProvenPR — Sindri app stage S1: the app shell. Build `src/Sindri` as a new C# application (not a port of the
TS `sindri/`).

**Read first, in full:**
- `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` §17.29 (Sindri as an application) and §17.28 (the
  workflow engine; S2 builds it, but S1's ledger, command layer and lease must suit it), plus §17.17 Q6 and Q7.
- §17.20 (seams: `LedgerStore`, `Lease`, `Scheduler`, tenant and ring keys), §17.21 (event envelope, outbox, API),
  §17.22 D2 (the host lease), §17.27.1 (one command layer, CLI and UI as clients).
- `docs/superpowers/specs/2026-10-07-sindri-design.md` §5.2 storage, §9.1 singleton and fencing, §10.3 CLI, §11.1
  profile.
- `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4a (re-planned 2026-10-11: Sindri is built new, not
  ported) and §4b; `.agents/rules/csharp.md` and `csharp-testing.md`.
- `docs/sindri/README.md` if the app-direction docs PR has landed.
- TS reference, for **what** each part does, never **how**: `sindri/src/profile`, `sindri/src/ledger`,
  `sindri/src/lock`, `sindri/src/doctor`, `sindri/src/cli.ts`.

**S1 scope: the app shell. Everything later plugs into it.**
1. **`sindri` CLI:**
   - Bare `sindri` prints a banner (ASCII art) and starts the control-plane process if it isn't running, then the
     startup summary (§17.29.1).
   - `sindri --headless` starts it without the UI.
   - Every other verb (`status`, `stop`, `profile …`, `doctor`) is a client call to the running process.
2. **The control-plane process:**
   - One per host, held by the host lease with a fencing epoch in the ledger (§9.1, §17.22 D2).
   - Starts, stops and recovers cleanly; launchd only keeps it alive.
3. **Profile:** load, validate and approve by hash (§11.1), typed through AwJson. YAML vs JSON is open (readiness R14)
   and needs an owner decision.
4. **Ledger:** SQLite through `Aw.Kernel.Data` behind a `LedgerStore` port, with a contract-test suite. New C#
   schema with migrations. Every row carries tenant and ring keys, and every event the §17.21 envelope with an
   idempotency key. A transactional outbox table goes in now, even if empty.
5. **Command layer and API:**
   - Typed commands with one handler each (§17.27.1); `SND-*` errors.
   - A loopback HTTP API plus an event stream (minimal API, `CreateSlimBuilder`). The CLI is a client of it.
6. **Service scheduler:**
   - Runs background services on the profile's cadence, each a registered service with a port.
   - S1 ships the scheduler plus one trivial service (a heartbeat) to prove it. Index, intake and scorer come later
     as blocks.
7. **UI shell:** the Avalonia desktop app (`Aw.Sindri.Ui` + `.Desktop`; add a `SindriUi` node above `Sindri` in `deps.yaml`, readiness §4a "UI") launched
   by `sindri`. It connects to the API and shows status. Split this into its own stage if S1 gets large.
8. **`sindri doctor`:** checks the lease, ledger schema, profile approval and API reachability.

**Out of scope for S1:**
- the workflow engine and recipes (S2), context retrieval (S3), blocks and workflows;
- the MCP server and `sindri open` (later stages);
- anything in `setup.sh` or the providers: the clean cutover is unchanged, and the TS toolkit keeps running.

**Rules and constraints:**
- **Shape:** full shape, scaffolded with `/new-package` (`node scripts/scaffold/new-package.mjs`). `deps.yaml`
  already has the `Sindri` node (tier 3, `src/Sindri`); update its edges as the code needs. Slices via `/new-slice`
  (`--files-only`; copy RS0016 lines from the build output). Host is NativeAOT for the CLI.
- **Threat model** (`docs/csharp/bugfix.md`): guard honest mistakes. Exploit-style review findings are answered in
  writing against the threat model, not looped on. Review loop: fix issues and real-harm findings; resolve
  suggestions and nits in writing.
- **In flight:** Judge #156 and Evidence #158 are finishing in other sessions. Don't touch them; they become blocks
  later.
- **Process:** own worktree and branch `feat/sindri-s1-app-shell` off `refs/remotes/origin/HEAD`. Own stage file
  `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-app/stages.md` (create it, with stages S1 app shell,
  S2 engine, S3 context retrieval, S4+ blocks and workflows). Record the clock (verification plan → ready) and the
  C# lines there.
- **Autonomy:** run to ready, never merge. Builds and tests under `~/.agentic-workflow/bin-r1b/heavy`. GREEN = the
  full local merge gate (Actions billing-blocked). Lane-budget and statusline flakes under machine load are known;
  re-run alone and record them.
- **Tool quirks:**
  - A hook blocks any command containing the substring "main" (also "remaining", "domain", "maintain"). Put commit
    messages and PR bodies in files (`-F`, `--body-file`), and don't pass `--base`.
  - Another hook blocks `rm` with `-r`/`-f`; use `find -delete` or `unlink`.
- **Shared one-line files** (`AgenticWorkflow.slnx`, `tests/Aw.Architecture.Tests/Packages.cs` and its csproj,
  `deps.yaml`, `AGENTS.md`, `changed-areas.sh`): expect conflicts with other PRs; check with
  `git merge-tree --write-tree` and resolve with a merge commit.

**First deliverables before any code:**
- the stage file;
- an S1 implementation plan;
- the verification plan (journeys: start `sindri`, see the banner and summary, `sindri status` from a second
  terminal, a second `sindri` refused while one holds the lease, `stop`/restart recovery, `doctor` green and red);
- the open owner question on profile format (YAML vs JSON).

Ask the owner that one, and only that, up front.
