# Sindri (C#) — the workflow engine

Stage S2 of the Sindri app (factory addendum §17.28): recipes, building blocks and the workflow engine that runs
recipe instances inside the control plane S1 built (`docs/csharp/sindri.md`). The engine is the one place a work item
moves: a step reports a **signal**, the engine checks the recipe's gates and evidence, and commits the transition, its
effects (as outbox commands) and the instance row in one transaction under the host lease. It never acts directly.

Code: `src/Sindri/Aw.Sindri.Core/{Recipes,Blocks,Workflow}`, `Aw.Sindri.Infrastructure/{Recipes,Workflow}`,
`Aw.Sindri.Features/<Slice>/` (one folder per use case below), `Aw.Sindri.Host/Endpoints/WorkflowEndpoints.cs`,
`Aw.Sindri.Host/Commands/Workflow*.cs`.

## Recipes

A recipe is declarative YAML, read as plain data (no anchors, aliases, tags or duplicate keys), converted to JSON and
bound to the `Recipe` record through AwJson: an unknown key is an error that names it. The JSON Schema in
`schemas/sindri/recipe.schema.json` is generated from the record (`RecipeSchemaTests`; regenerate with
`AW_GOLDEN_UPDATE=1`).

```yaml
id: workflow.step            # dotted lower-case words, at most 64 characters
version: 1                   # the recipe's own version, from 1
description: "one line"
initial: produce
states:                      # by name, lower-case kebab
  produce: { kind: active, step: step.produce, timeout: 2h, onTimeout: park, next: "what the human or agent does now" }
  checks:  { kind: active, step: step.checks, timeout: 30m, onTimeout: retry }
  running: { kind: wait, child: workflow.other, timeout: 1d }    # a wait state may run a child workflow
  done:    { kind: terminal }
transitions:                 # tried in order
  - { from: produce, to: checks, on: produced }
  - { from: checks, to: done, on: passed, gates: [gate.checks.passed], evidence: [test@head], effects: [effect.notify.human] }
  - { from: checks, to: produce, on: failed, loop: revise }
loops: { revise: { cap: 3 } }                # 1 to 50 rounds; past the cap the instance parks (rounds)
budget: { tokens: 400000, turns: 60 }        # spent past it, the instance parks (budget); a state may have its own
hash: <sha256>                               # optional: a frozen recipe's hash must match its content
```

| Key | Meaning |
|---|---|
| `kind` | `active` (a step works; `step` names the step block), `wait` (the instance waits for a signal or its child), `terminal` |
| `timeout` | `30s`, `10m`, `4h`, `2d`; required on active and wait states |
| `onTimeout` | `park` (default; reason `timeout`), `escalate`, `retry` (restart the timer once; a second timeout parks) |
| `child` | the recipe a wait state starts as a child; the parent receives `child.done` when it completes |
| `evidence` | `kind`, `kind@head` or `kind@base`: registered for the instance and bound to the transition's sha (or the start's base sha) |
| `gates` / `effects` | block ids; every gate must pass; effects become outbox commands in the transition's commit |

The hash is the SHA-256 of the canonical JSON: what the engine runs and nothing else (no `hash` field, no
`description`, no state `next` text, absent optional fields not written, states and loops sorted by name, transitions in
order). A golden test pins the built-in recipes' hashes. An instance is pinned to the hash it started with: a changed
recipe never applies to a running instance (SND-WF-008). Such a **stranded** instance still takes the human's commands
(cancel, park, takeover, handback, evidence; unpark needs the recipe and is refused), and the next tick escalates it
once ("recipe changed: …"), so it leaves the due batch and waits for the human to cancel it and start a new attempt.

**Validation** (`RecipeValidator`, every problem names its check and path): `schema`, `shape` (states, transitions,
loops and the initial state refer to each other), `block` (every id is registered with the kind its place needs),
`input` (every block input is produced upstream on every path, with the same type: a must-analysis over the graph),
`capability` (capability-bearing blocks only in approved recipes), `reachable`, `exit` (every non-terminal state has a
way out), `timeout`, `loop` (every cycle passes through a counted loop transition, and every loop has a cap),
`effect` (allowlisted command kinds only), `hash`.

**Catalog** (`IRecipeCatalog`; `FileRecipeCatalog`, fake `InMemoryRecipeCatalog`, contract `RecipeCatalogContract`):
the built-ins `workflow.item` and `workflow.step` are embedded and **approved**; each `$AW_SINDRI_HOME/recipes/*.yaml` is
read on every call and is **unapproved** (a config change is a new workflow, invariant 13). A file whose id is already
registered, or that fails validation, is listed with its problems and can't run. Approving a file recipe is a later
stage.

## Blocks — the registry contract (for lane E and every later block)

A block is a typed C# module registered once and reused by every recipe that names its id. The engine knows a block
only through this contract, which is public API in `Aw.Sindri.Core.Blocks`:

| Type | Contract |
|---|---|
| `IBlock` | `BlockSpec Spec { get; }` — every block |
| `IGateBlock : IBlock` | `ValueTask<Result<GateVerdict>> Evaluate(GateContext context, CancellationToken ct)`; an error refuses the transition |
| `IEffectBlock : IBlock` | `Result<EffectCommand> Command(EffectContext context)`; pure, no I/O: the engine commits the command to the outbox |
| `BlockSpec` | `(BlockId Id, BlockKind Kind, string Summary, IReadOnlyList<PortSpec> Inputs, IReadOnlyList<PortSpec> Outputs, bool Capability = false, GateRung? Rung = null, string? CommandKind = null)` |
| `BlockKind` | `Gate`, `Evidence`, `Step`, `Effect` |
| `PortSpec` | `(string Name, PortType Type)`; `PortType` is `Text`, `Number`, `Flag` (true/false), `Verdict` (pass/fail), `Size` (XS..XL), `Sha` |
| `GateRung` | `Deterministic` (1), `Judge` (2), `Model` (3, the top rung) |
| `GateContext` | `(InstanceId Instance, RecipeId Recipe, ItemId Item, int Attempt, string State, string To, string On, IReadOnlyDictionary<string,string> Facts, IReadOnlyList<EvidenceRef> Evidence, HeadSha? Sha)`: `State` → `To` on `On` is the transition the gate judges |
| `GateVerdict` | `(bool Passed, string Detail, double? Score = null, double? Threshold = null)` — recorded with the transition (`GateRecord`) |
| `EffectContext` | `(InstanceId Instance, ItemId Item, RecipeId Recipe, string From, string To, IReadOnlyDictionary<string,string> Facts)` |
| `EffectCommand` | `(string Kind, string PayloadJson)`; `Kind` must be the block's declared `CommandKind`, else the transition is refused (SND-WF-001) and nothing is recorded |
| `EvidenceRef` | `(string Kind, HeadSha Sha, string Digest, DateTimeOffset RegisteredAt)` |

**Registration.** A project adds its blocks as `IBlock` singletons in its own `Add…()` method
(`services.AddSingleton<IBlock>(new MyGate(...))`); `BlockRegistry` collects every `IBlock` in the container. A broken
registration is a bug and throws when the registry is built: two blocks with one id, a gate that isn't an `IGateBlock`
or has no rung, a rung on a non-gate, an effect that isn't an `IEffectBlock` or whose command kind isn't in
`EffectAllowlist.Kinds` (`notify.human`, `pr.draft`), an evidence kind whose id isn't `evidence.<kind>`.

**Ids and naming.** `step.<name>`, `gate.<name>`, `effect.<name>`, `evidence.<kind>` (dotted lower-case words). A
step's `Outputs` are the facts a signal for its state may set (anything else is SND-WF-009, a wrong type too); a
gate's and an effect's `Inputs` are facts that must be produced upstream (the `input` check).

**The decision ladder.** For each candidate transition the engine first checks the cheap preconditions: the signal's
outputs (SND-WF-009, before any candidate) and the transition's required evidence (SND-WF-007). Only then do its gates
run, cheapest rung first (deterministic, judge, model; recipe order within a rung), stopping at the first failure, so a
judge or model call is never spent on a signal refused for a cheap reason. A failed gate below the top rung refuses the
transition and records nothing (SND-WF-006); a failed `Model` gate, reached only when every cheaper gate passed,
escalates the instance (invariant 4) in the same commit. The transition records the gates that ran.

**Capability.** `Capability = true` for a block with tools, outward effects or autonomy: only approved recipes may
name it.

Built-ins: steps `step.triage`, `step.claim`, `step.produce` (capability), `step.checks`, `step.verify`; evidence
`evidence.test`; fact gates `gate.triage.complete`, `gate.router.startable`, `gate.claim.ref`, `gate.checks.passed`
(deterministic), `gate.verifier.passed` (judge); effects `effect.notify.human`, `effect.pr.draft` (capability).
`sindri workflow blocks` lists them with their ports and flags.

## The engine

`IWorkflowEngine` (Core; internal `WorkflowRunner`): `Start`, `Signal`, `Apply(WorkflowCommand)`, `Tick`. Every change
is decided by pure functions in Core (`SignalDecisions`, `GateOutcome`, `TimerDecisions`, `CommandDecisions`,
`ParentDecisions`, `NoProgress`) and replayable: an instance is the fold of its events (`InstanceFold`).

- **Instance id**: `wf-` + 12 hex of SHA-256 over (tenant, recipe, item, attempt). Starting the same triple again is a
  no-op that returns the instance (`AlreadyRecorded`). A child's id is derived from its parent's id, its recipe and the
  parent's version when it entered the child wait state (that version is also the child's `attempt`), so each run of a
  wait state, in every parent attempt, gets its own child. Each instance also records its tree's `root`.
- **Signals**: `on`, optional `expectedVersion` (else SND-WF-003 on a mismatch), `sha`, outputs (`--set name=value`),
  findings, `emptyDiff`, spend. A loop round with no new findings or an empty diff parks (`no-progress`). A signal sent
  again with the `expectedVersion` it was first applied at is a no-op (`AlreadyRecorded`): the engine finds that
  signal's transition key at that version. A signal sent again **without** `expectedVersion` is a new signal against
  the current state. Registering evidence that is already registered (same kind, sha and digest) records nothing.
- **Holds**: park (`rounds`, `budget`, `no-progress`, `timeout`, `needs-answer`, `spec-conflict`) and escalate keep the
  state; unpark may grant extra budget. Takeover pauses the timer, handback resumes it with the time that was left; an
  unpark during a takeover restarts the timer paused at the state's full timeout. Cancel ends the instance and its
  whole subtree in one commit. Park, escalate, cancel and unpark propagate up the tree in the same commit (at most 8
  levels); a completed child sends the parent `child.done` (its gates apply, and a top-rung failure escalates the
  parent). A parent that is held or taken over when its child completes applies `child.done` when it is unparked or
  handed back.
- **Timers**: the scheduled service `workflow-timers` (profile: `services: { workflow-timers: { every: 5s } }`) ticks
  `Due` instances. After a clock jump (a slot far later than the cadence: `TickPace`) deadlines are extended by the
  jump instead of firing.
- **Commit** (`IWorkflowStore.Commit(WorkflowCommit, LeaseGrant)`; `SqliteWorkflowStore`, fake `InMemoryWorkflowStore`,
  contract `WorkflowStoreContract`): events into the ledger's `events` table, the instance rows and Kernel's outbox on
  one `BEGIN IMMEDIATE`, fenced once by the lease epoch, idempotent by the first event's key, versioned per instance.

**Event types**: `workflow.started`, `workflow.transitioned`, `workflow.parked`, `workflow.unparked`,
`workflow.escalated`, `workflow.cancelled`, `workflow.taken-over`, `workflow.handed-back`, `workflow.retried`,
`workflow.evidence`, `workflow.deadline-extended`. Each carries the item and the instance (`events.instance`). Every
event of a workflow tree shares one correlation id, derived from the root instance id (so a replay is stable); an event
caused by another (a parent's `child.done`, a propagated park) names it as its causation, and an effect's outbox command
names the transition event that emitted it.

**Idempotency keys** (§17.20), prefixed by the instance id (which stands for tenant, recipe, item and attempt, or for a
child its parent and the parent's version): start `wf:<instance>:start`; transition
`wf:<instance>:<from>><to>:<on>@v<version>:<sha|->`; other changes `wf:<instance>:<what>@v<version>:-`. A replayed
commit (same first key) writes nothing; a timer or command key never counts as a signal's retry (only a key with
`<from>><to>` before the signal's suffix does).

**Storage** (migration `0004_workflow.sql`): `events.instance` (null for non-workflow events, indexed with `seq`) and
`workflow_instances` (`id`, `recipe`, `recipe_hash`, `item`, `attempt`, `parent`, `tenant`, `ring`, `version`, `state`,
`status`, `deadline`, `started_at`, `updated_at`, `snapshot_json`; unique on tenant, recipe, item, attempt and
`COALESCE(parent, '')`, so children are unique per parent). The row is a
cache of the fold; `snapshot_json` holds the whole instance.

## API and CLI

Routes under `/sindri` (loopback only, S1's pipeline; every instance route binds `{instance}` through `InstanceId`'s
validating `TryParse`, so malformed text is a 400 and a well-formed unknown id a 404):

| Route | Slice |
|---|---|
| `GET /workflows[?all=true]` | ListWorkflows (open instances; all with `all`) |
| `GET /workflows/{instance}` | ShowWorkflow (state kind, the signals it takes, next, children, history) |
| `POST /workflows` `{recipe, item, attempt?, baseSha?}` | StartWorkflow |
| `POST /workflows/{instance}/signal` `{on, expectedVersion?, sha?, outputs?, findings?, emptyDiff?, tokens?, turns?}` | SignalWorkflow |
| `POST /workflows/{instance}/cancel` `{reason?}` · `/park` `{reason, detail?}` · `/unpark` `{tokens?, turns?}` · `/takeover` · `/handback` | Cancel/Park/Unpark/TakeOver/HandBackWorkflow |
| `POST /workflows/{instance}/evidence` `{kind, sha, digest}` | RegisterEvidence |
| `GET /recipes` · `GET /blocks` | ListRecipes · ListBlocks |

`sindri workflow …` (`sindri workflow help` prints the usage): `list [--all]`, `show <id>`, `start <recipe> <item>
[--attempt N] [--base SHA]`, `signal <id> <on> [--set name=value]… [--finding F]… [--empty-diff] [--sha SHA]
[--expect-version N] [--tokens N] [--turns N]`, `evidence <id> <kind> <sha> <digest>`, `cancel <id> [--reason]`,
`park <id> <reason> [--detail]`, `unpark <id> [--tokens N] [--turns N]`, `takeover <id>`, `handback <id>`, `recipes`,
`blocks`, `validate <file.yaml>` (runs without the control plane). Every verb takes `--json`. Exit codes: 0 ok, 1
attention needed (parked, escalated, not running), 2 error.

## Errors

| Code | Kind | When |
|---|---|---|
| SND-WF-001 | invalid | a recipe fails validation |
| SND-WF-002 | not found | no recipe with that id, or it is invalid |
| SND-WF-003 | conflict | the instance's version isn't the one the caller expected |
| SND-WF-004 | not found | no workflow instance with that id |
| SND-WF-005 | conflict | the instance can't take that signal or command now |
| SND-WF-006 | forbidden | a gate refused the transition |
| SND-WF-007 | forbidden | required evidence isn't registered for this instance and sha |
| SND-WF-008 | conflict | the recipe changed under a running instance |
| SND-WF-009 | invalid | a signal sets an output the step doesn't declare, or a value of the wrong type |

## Checks

`tests/Aw.Sindri.Tests/Workflow/EnginePropertyTests.cs` holds the §17.28.5 properties: replaying the event log
reproduces the instance; the same signal sent again with its `expectedVersion` is a no-op; a stale epoch or a stale instance version is refused and
writes nothing; a timer firing during a takeover does nothing; a child's park, escalation and completion reach the
parent in one commit. Engine behavior, decisions, the store and catalog contracts, the validator, the
recipe reader and the schema golden are beside it; each slice has its folder; `Server/WorkflowApiTests.cs` runs the
routes on loopback; `src/Sindri/aot-smoke` drives the workflow verbs against the NativeAOT binary.
