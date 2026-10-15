# Handoff: Sindri S2 (workflow engine) — lane C, mid-stage

Read first: `~/.agentic-workflow/digests/sindri-lane-brief.md` (process rules; review cap is now **5** runs),
`~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-app/stages.md`, the S2 plan
`~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-app/s2-plan.md`, and the verification plan
`~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-s2-engine/verification-plan.md` (created 2026-10-11T06:16:36Z,
before any code). Spec: `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` §17.28.

Worktree `/Users/thor/repos/agentic-workflow/.claude/worktrees/sindri-s2-engine`, branch `feat/sindri-s2-engine`, stacked on
`origin/feat/sindri-s1-app-shell` (a7d2ada). PR base: `feat/sindri-s1-app-shell`. Builds/tests only via
`~/.agentic-workflow/bin-r1b/heavy` (it can wait 10+ min for a slot: run long ones with run_in_background).

## Done (all builds with zero warnings; see the WIP commit on the branch)
- **Orchestrator asks (S1 review seams, owned by S2):** `LedgerEntry.Item`/`LedgerEvent.Item` are now typed `ItemId?`
  (S1 mapper, SqliteLedger bind, LedgerStoreContract, InMemorySindriStore updated); `EventFactory` gained `Caused(cause, …)`
  (public: causation + the cause's correlation), `Correlated(Guid, …)` (internal: AW1001 bans Guid in public Core) and
  `Command(cause, kind, payload)` for outbox commands. The "one transaction" seam is answered by a new port
  `IWorkflowStore.Commit(WorkflowCommit, LeaseGrant)` (events into the same `events` table + instance rows + Kernel
  `SqliteOutbox` on one BEGIN IMMEDIATE, fenced once, idempotent by the first event key, versioned per instance) with
  `WorkflowStoreContract` (fake + SQLite). `ILedgerStore` itself is unchanged.
- **Core** `src/Sindri/Aw.Sindri.Core/`: ids (`RecipeId`, `BlockId`, `InstanceId` wf-+12hex derived from tenant/recipe/item/
  attempt, `ItemId`, `RecipeHash`, `HeadSha`); `Recipes/` (record, `RecipeValidator` = shape/block/capability/effect/input
  (must-analysis dataflow)/reachable/exit/timeout/loop(cap + every cycle counted)/hash checks; `IRecipeCatalog`);
  `Blocks/` (`IBlock`, `IGateBlock`, `IEffectBlock`, `BlockSpec`, `PortSpec`/`PortType`, `GateRung`, `BlockRegistry`,
  `EffectAllowlist` {notify.human, pr.draft}, `Builtin/` steps triage/claim/produce(capability)/checks/verify,
  evidence.test, fact gates triage.complete/router.startable/claim.ref/checks.passed/verifier.passed(judge rung),
  effects notify.human, pr.draft(capability)); `Workflow/` (`WorkflowInstance`, `WorkflowChange` + `InstanceFold` (replay),
  `SignalDecisions`, `GateOutcome` (top-rung failure escalates), `TimerDecisions` (park/escalate/retry-once, clock-jump
  extend), `CommandDecisions` (cancel/park/unpark/takeover/handback/evidence), `NoProgress`, `WorkflowKeys`
  (`wf:<recipe>:<item>:<attempt>:<from>><to>:<on>@v<version>:<sha|->`), `IWorkflowEngine` → internal `WorkflowRunner`,
  `SignalApplier`, `CommandApplier` (cancel cascades to children), `WorkflowTicker`, `Committer`, `ParentPropagation` +
  `ParentDecisions` (child done → parent `child.done`; parked/escalated/cancelled/unparked propagate in the same commit),
  `FollowUps` (effects as outbox commands caused by the transition event; a wait state with `child:` starts the child,
  attempt = parent's new version)). Registered in `AddSindriCore`. New `SND-WF-001..009` in `SindriErrors`.
- **Infrastructure:** migration `Ledger/Migrations/0004_workflow.sql` (events.instance column + `workflow_instances`);
  `LedgerDatabase.LoadMigrations` keeps the outbox at version 3 (S1's applied ones untouched); `Workflow/SqliteWorkflowStore`
  (+ `WorkflowSql`, `WorkflowRows`, `InstanceMapper`, `StatusWord`); `Recipes/` (`RecipeReading` YAML→AwJson→hash,
  `RecipeErrors`, `FileRecipeCatalog`: embedded built-ins approved + `$AW_SINDRI_HOME/recipes/*.yaml` unapproved, duplicates
  refused); built-in recipes `Recipes/Builtin/workflow.item.yaml` and `workflow.step.yaml`; `SindriSettings.RecipesDirectory`.
- **Features:** slices StartWorkflow, SignalWorkflow, Cancel/Park/Unpark/TakeOver/HandBackWorkflow, RegisterEvidence,
  ListWorkflows, ShowWorkflow, TickWorkflowTimers (+ `WorkflowTimerService` = IScheduledService `workflow-timers`, `TickPace`
  clock-jump memory), ListRecipes, ListBlocks, ValidateRecipe. All registered.
- **Host:** `Endpoints/WorkflowEndpoints.cs` (GET/POST `/sindri/workflows…`, `/sindri/recipes`, `/sindri/blocks`; hooked in
  `EndpointTable`), `Bodies/` (route body records; own namespace to avoid a Host↔Endpoints cycle), CLI `Commands/WorkflowVerbs.cs`,
  `WorkflowCommands.cs`, `WorkflowArgs.cs`, `WorkflowText.cs`, `BlockText.cs`; `CommandTable` has `workflow …`;
  `Usage.Workflow`; JSON context entries added.
- **Testing:** `InMemoryWorkflowStore`, `WorkflowStoreContract`, `WorkflowStores`, `AnInstance`, `UniqueIds`,
  `InMemoryRecipeCatalog`, `RecipeCatalogContract`.
- **Tests** `tests/Aw.Sindri.Tests/`: `Workflow/` (harness, EnginePropertyTests = the five §17.28.5 properties,
  EngineBehaviorTests, DecisionTests, contract bindings), `Recipes/` (validator, reading, schema golden, file catalog),
  `Blocks/BlockRegistryTests`, `Ledger/LedgerFileTests` (migration 4 on a copy of the S1 schema).
- PublicAPI lines: generated with `scratchpad/api.py` style (append RS0016 lines, drop RS0017; S1 order kept).

## Left (in order)
1. Run `dotnet test --project tests/Aw.Sindri.Tests --filter-not-trait "Lane=heavy"`; fix failures. Generate the schema
   golden once: `AW_GOLDEN_UPDATE=1` run of `RecipeSchemaTests` → commit `schemas/sindri/recipe.schema.json`.
2. Missing tests: one test file per new slice folder (`tests/Aw.Sindri.Tests/<Slice>/`), the loopback endpoint test
   (start → signal → show over HTTP) plus the **route-binding test** for `InstanceId` (valid → 404 SND-WF-004, `BAD` → 400,
   endpoint `ParameterBindingMetadata` hasTryParse; docs/csharp/typed-ids.md), CLI text/args tests, `TickPace` test, id JSON
   round trips for the six new ids (`Ids/IdJsonRoundTripTests.cs`), HostTests DI already resolves every IService.
3. Docs: `docs/csharp/sindri-engine.md` (recipe format, engine, keys, block registry contract for lane E, errors, routes,
   CLI); pointer from `docs/csharp/sindri.md` (Ledger: `events.instance`, migration 4); AGENTS.md command line; check
   `node scripts/check-deps.mjs`, `scripts/check-env-example.sh`, `node scripts/check-rules.mjs`, architecture tests
   (ARCH-CYCLE-001, ARCH-ID-002, ARCH-PLACE-001), file-length AW1005.
4. AOT: extend `src/Sindri/aot-smoke` (run.sh/expected.txt) with a `workflow recipes`/`start`/`signal` step; publish.
5. Hand test per the verification plan with the published binary (profile needs `workflow-timers: { every: 5s }`;
   journey 10 uses a recipe file in `$AW_SINDRI_HOME/recipes/` with short timeouts; after an unpark the child stays in
   `checks`, so journey 8 signals `passed` there), `manual-test.md`, `evidence.md`, merge S1's latest branch if updated
   (S1 is moving IScheduledService impls to Features, adding `AddSindriHost()`, `StopReason`, `EventTypes.ServiceRun`).
6. Full merge gate (AGENTS.md), draft PR (body lists the block registry contract), `/review` loop (≤5), ready, report.
- Process note: code was written before its tests (verification plan did predate code); say so in the PR, don't fake a red commit.

## Status at handoff (2026-10-11)
- WIP commit `b21d39a` on `feat/sindri-s2-engine` (not pushed). Everything builds with zero warnings.
- First test run (`dotnet test --project tests/Aw.Sindri.Tests --filter-not-trait "Lane=heavy"`, log was
  scratchpad `run.log`): all engine, contract, validator and catalog tests ran; 2 failures seen:
  1. `RecipeSchemaTests` — expected: generate `schemas/sindri/recipe.schema.json` with `AW_GOLDEN_UPDATE=1` and commit it.
  2. `RecipeReadingTests` case `id: Workflow Loop` — a bad typed id comes back as
     "Invalid JSON for Recipe: The value is not a valid …" without naming the key. Improve `RecipeErrors.Explain` (the
     id converter's JsonException message: add the JSON path, e.g. 'id: …') so the problem names the key, then keep the
     test's expectation ("recipe id" or the key path).
  The run completed: 198 total, 196 passed, only these 2 failed.
