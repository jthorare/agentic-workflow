# S2 — the workflow engine: implementation plan

Spec: factory addendum §17.28 (engine), §17.28.5 (validation and engine properties), §17.28.6 step 1 (item lifecycle and
Step contract first), §17.23.13 (no-progress), §17.20–17.21 (seams), design spec §6, §7.1, §9.2–9.4. Builds on S1
(`docs/csharp/sindri.md`, frozen): `ILedgerStore` and its events table, `ILease`/`LeaseGrant`/`HeldLease`, `EventFactory`,
`YamlToJson`, `IScheduledService`, Kernel's outbox. Branch `feat/sindri-s2-engine`, stacked on `feat/sindri-s1-app-shell`.

## Shape (all inside the Sindri package; no new package, §17.28.3)

| Layer | New namespaces | What |
|---|---|---|
| Core | `Recipes` | `Recipe` record (YAML → AwJson, unknown keys refused), `RecipeId`, `RecipeHash`, `RecipeDuration`, `RecipeValidator` (every §17.28.5 check), `RecipeProblem`, `RecipeHashing`, `IRecipeCatalog` port, `CatalogRecipe` |
| Core | `Blocks` | `BlockId`, `BlockKind`, `PortSpec`/`PortType`, `BlockSpec`, `IBlock` + `IGateBlock`/`IEffectBlock`, `GateContext`/`GateVerdict`/`GateRung`, `EffectContext`/`EffectCommand`, `IBlockRegistry` + `BlockRegistry`, `EffectAllowlist` |
| Core | `Blocks.Builtin` | the S2 blocks the two recipes name: step declarations (triage, claim, produce, checks, verify), fact gates (triage complete, router startable, claim ref, checks passed, verifier passed), `evidence.test`, effects `notify.human` and `pr.draft` (capability) |
| Core | `Workflow` | `InstanceId`, `ItemRef`, `HeadSha`, `WorkflowInstance`, `InstanceHold`, `ParkReason`, `Spend`, `WorkflowChange` (+ kinds), `InstanceFold` (replay), `WorkflowDecisions` (pure: which transition, gates, loops, no-progress, budget, timers, universal states), `NoProgress`, `WorkflowKeys`, `IWorkflowStore` port (fenced, idempotent, versioned multi-instance commit with outbox commands), `WorkflowRunner` (orchestrates: load, gates through the registry, decide, propagate child → parent, commit) |
| Features | `StartWorkflow`, `SignalWorkflow`, `CancelWorkflow`, `ParkWorkflow`, `UnparkWorkflow`, `TakeOverWorkflow`, `HandBackWorkflow`, `RegisterEvidence`, `ListWorkflows`, `ShowWorkflow`, `TickWorkflowTimers`, `ListRecipes`, `ListBlocks`, `ValidateRecipe` | one service each, thin over `WorkflowRunner` / catalog / registry |
| Infrastructure | `Workflow` | migration `0004_workflow.sql` (S1's 1–3 untouched: `LoadMigrations` keeps the outbox at 3), `SqliteWorkflowStore` (one `BEGIN IMMEDIATE`: fence, key, version, event rows with `instance`, instance upsert, `SqliteOutbox` enqueue), `RecipeFiles` (built-in YAML resources + `$AW_SINDRI_HOME/recipes/*.yaml`), `RecipeReading` (YamlToJson → AwJson → hash) |
| Host | endpoints `/sindri/workflows…`, `/sindri/recipes`, `/sindri/blocks`; CLI `sindri workflow …`; `WorkflowTimerService` (`workflow-timers`, an `IScheduledService`) | |
| Testing | `InMemoryWorkflowStore`, `WorkflowStoreContract`, `RecipeCatalogFake`, test blocks, builders | |

## Key decisions
- **Transition key** `wf:<recipe>:<item>:<attempt>:<from>><to>@v<version>:<sha|->` — the §17.20 tuple, with the
  transition occurrence named by the version it applies to, so a retried or replayed event is a no-op and a later loop
  round is a new key. Timers: `...:timeout:<state>@v<version>`. The key check runs before the version check, so an exact
  retry answers "already recorded".
- **Instance id** `wf-` + 12 hex of SHA-256(tenant, recipe, item, attempt): deterministic, so replay and a retried start
  give the same id.
- **Replay**: every change is a `WorkflowChange` payload (one event); `InstanceFold.Apply` is the only code that turns a
  change into the next instance, used by the runner and by replay; the store keeps the folded row (`snapshot_json` plus
  indexed columns).
- **Composition**: a `wait` state with `child: <recipe>` starts the child in the same commit as the transition that enters
  it (child attempt = the parent's new version). Child completed → parent signal `child.done`; child parked/escalated →
  parent parked (same reason) / escalated; child unparked → parent unparked; child cancelled → parent parked
  `needs-answer`. One commit holds the child's and the parent's events.
- **Gates**: evaluated by the registry's gate blocks over a `GateContext` (instance facts, evidence, request sha); the
  rung, score, threshold and detail of each are recorded with the transition. A failed gate on the top rung (3) escalates;
  any other failure refuses (`SND-WF-006`, nothing recorded).
- **Typed ports**: a block declares `inputs`/`outputs` as `name:type`; the validator runs a must-analysis over the state
  graph so a gate's or step's input is produced on every path to it, with the same type. At run time a `signal` may only
  set the current state's step outputs, parsed by type.
- **Recipes**: built-ins are approved (embedded); files in `recipes/` are unapproved (capability blocks refused). An
  invalid file is listed with its problems and can't be started. `sindri workflow validate <file>` runs the validator
  in-process as an unapproved recipe. The JSON Schema `schemas/sindri/recipe.schema.json` is generated from the record
  (golden test, as `schemas/packets/`).
- **Timers**: a deadline on the row; the tick (`workflow-timers` service) applies due timeouts (`park` → parked(timeout),
  `escalate`, `retry` once then park). Takeover pauses the deadline (remaining time kept); handback restores it; a clock
  jump > 3 ticks extends deadlines by the gap first.
- **Loops**: a transition with `loop: <name>` increments it; past the cap → parked(rounds). No-progress (§17.23.13): the
  signal's findings equal or a superset of the previous round's, or `emptyDiff` → parked(no-progress). Unpark resets
  the loop that parked it.
- **Budget**: tokens/turns per instance and per state; a signal's spend that exceeds either parks (budget) instead of
  transitioning. Unpark may add budget.

## Order (TDD, red first per slice)
1. Core ids + recipe model + duration + hashing; recipe YAML reading; schema golden test.
2. Block registry + built-ins; validator (one test per check, plus both built-in recipes valid).
3. Instance model, change, fold, decisions (pure tests: loops, no-progress, budget, timers, universal states).
4. `IWorkflowStore` + in-memory fake + contract; migration 0004 + SQLite store + contract binding + migration test.
5. `WorkflowRunner` + engine property tests (replay, twice no-op, stale epoch, stale version, timer in takeover,
   child park/escalate propagation).
6. Slices; endpoints + route-binding test; CLI verbs + text; timer service; DI test.
7. Docs `docs/csharp/sindri-engine.md`, `docs/csharp/sindri.md` pointer, `deps.yaml` unchanged (no new edges), AGENTS.md
   command line; AOT smoke extension; hand test with the published binary.
