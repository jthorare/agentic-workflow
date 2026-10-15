# Engine lane — common brief for every unit, integrator and finalizer agent

Repo: /Users/thor/repos/agentic-workflow. Spec: `docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`
(rev 8.1 on origin/main; read it from `git show origin/main:<path>`), amended by the UI spec's E-6/E-9/E-13/E-19
(`docs/superpowers/specs/2026-10-11-sindri-ui-design.md` §6.7, §18.1). Cross-spec decisions: `docs/sindri/README.md` X1–X61.
The spec wins over the reference branches #166/#167/#169.

## Where and how
- Work only in the worktree named in your task, under `.claude/worktrees/`. Never switch branches in the main checkout
  (the live toolkit links to it). Never touch other worktrees.
- Read `AGENTS.md`, `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md` (and any rule whose globs match).
- Scratch files: `~/.agentic-workflow/jthorare-agentic-workflow/scratch/<your-task>/` (the old /tmp scratchpad was wiped).
- Every `dotnet build|test|format|publish` runs as `AW_HEAVY_SLOTS=2 ~/.agentic-workflow/bin-r1b/heavy <cmd…>` (rules since
  the 2026-10-14 crash: the whole lane session shares ONE heavy slot; whole-solution build/test, publish, aot-smoke
  and browsers also share 2 box-wide slots). Prefer project-scoped builds and tests
  (`heavy dotnet test --project tests/Aw.Sindri.Tests ...`); run the whole-solution gate only once, at the end.
  Never run builds or tests outside `heavy`. Never delete lock directories. At most 2 builder subagents per lane.
- Never touch the real `$AW_SINDRI_HOME`, `setup.sh`, the providers or MCP registration. Proofs use
  `scripts/sindri-prove.sh` (`<verb>` = agent session; `human <verb>` = owner; pipe typed challenge text into human mode),
  with `AW_SINDRI_HOME=$TMPDIR/sindri-dev-<phase>` (fresh) and `AW_SINDRI_PORT=0`. Built host: framework-dependent,
  `DOTNET_ROOT=$HOME/.dotnet`, apphost under `artifacts/bin/Aw.Sindri.Host/debug/`.
- Pinned binaries: `scripts/binaries-lock.sh fetch temporal|temporal-test-server`; tests use `ExistingPath`.
  Time-skipping caveats (spec §11 F1–F3): no time skip while an activity is open; sticky-queue restarts; workflow code
  uses `ConfigureAwait(true)` and `Cancel` not `CancelAsync`.

- **Tightened again (2026-10-14, swap full at load 128):** every build — `dotnet build|publish|format`, `dotnet test`
  without `--no-build`, `npm ci|test` — takes one of 2 box-wide slots. Pattern: `heavy dotnet build tests/Aw.Sindri.Tests`
  once, then `heavy dotnet test --project tests/Aw.Sindri.Tests --no-build …` for every test run. Never let
  `dotnet test` build implicitly. Details: top of `~/.agentic-workflow/orchestrator/resume-2026-10-14.md`.

## Commits and pushes
- TDD: commit the red test first, then the implementation. Format `type: description`. End every commit message with:
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_019oQXkfxoLc3prCFtJoNGkM
- Push your branch after each meaningful commit (owner rule). Push only your own branch. Never push main.
- Hotspots (`PublicAPI.Unshipped.txt`, `Add…()` registrations, JSON contexts, catalog rows, command tables, docs):
  append only; integrators take the union.

## Gate (before you report done)
- `dotnet build AgenticWorkflow.slnx -warnaserror` zero warnings; `dotnet format --verify-no-changes` clean;
  `node scripts/check-deps.mjs`, `node scripts/check-rules.mjs`, `scripts/sync-rules.sh --check` pass; the unit and
  integration lanes pass except named red tests and known pre-existing failures (prove them on origin/main or the
  owning branch).
- Finalizers run the full AGENTS.md "Merge Gate" list. If scorer fails with "Cannot find module './database'", run
  `npm ci --prefix judge && npm ci --prefix scorer` first; bash-tests need `.github/ci-tools` installed and judge built.

## Disk
- If free space (`df -h /Users`) is under 5 GB, stop and report. Never `git clean`. Between builds, clear only build
  output (`dotnet clean`, or delete `bin`, `obj`, `artifacts` directories), after pushing.

## Reviews (finalizers)
- `/review` until a run has 0 blocking and 0 issue findings, at most 5 runs. Blocking = every issue, data loss, broken
  behaviour on a real path, a security/privacy hole, a failing check, a false claim in a doc people rely on. Threat
  model: honest mistakes, not hostile agents. Shell/setup.sh nits are non-issues.
- PR comments are blocked by hooks this week: record every finding and its resolution in
  `~/.agentic-workflow/jthorare-agentic-workflow/reviews/<pr>.json` and a "Review resolutions" section of the stage's
  `evidence.md` (embedded in the PR body).

## If blocked
- A spec ambiguity that needs a spec change: stop and report it; never edit the spec unless your task says an owner
  decision allows it.
- If your context grows very large, write a handoff digest to `~/.agentic-workflow/digests/<task>.md` and stop cleanly.

## Report (concise)
Head SHA, files, contract/shared changes, gate output, open questions (real spec questions first).
- Denylist check (#208): run as `AW_DENYLIST=file:/Users/thor/.agentic-workflow/denylist.txt scripts/denylist-check.sh <files>`; without the variable it exits 2. Never open or print the list itself.
- Every test wait is bounded (timeout or WaitAsync), red tests included: a hung test holds a heavy slot for every lane. If you were stopped (usage limit, crash), kill any `dotnet test` left running from your worktree before you start another.
