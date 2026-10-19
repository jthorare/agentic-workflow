# Sindri workflow engine: recipes, blocks, validator and engine

**Status:** design, **revision 8** (2026-10-11): the engine runs on Temporal (§0, §5). Revision 7 was the epic
reconciliation after five /autoplan passes; 7.1 applied the owner's decisions from PR #179's review (absent optional
fields stay `null` in the hash, §3.1; block ids take an optional area, §3.3; `RCP-NAME-001`, §3.4, §9.1); 7.2 applied
the owner's decisions from PR #180's review and is folded in here (no progress from identical findings and a repeated
failure reason until P8, §3.7; an answer's re-run past the loop cap parks `rounds`, §3.8, §5.8; `--reject` on a
command's escalation, §3.8, §5.8; `--scaffold`'s rule, §6.3; child lines, engine triggers and recorded content in the
eval format, §9.2). **Revision 8.1** (2026-10-12) applies the owner's decisions of that day: Temporal is decided and
there is no spike; the code written to measure it lands as **P4a, the Temporal foundation**, whose acceptance checks
carry the measured numbers (Phases), and P4 builds on it; a desk Step is a short activity and the report is the
`complete` Update (§5.3); the ledger mirror is keyed by the `RecordEvent` activity id (§5.6); the Temporal test
findings F1–F3 are in §11. Each is marked *rev 8.1* inline. **Revision 8.2** (2026-10-13) records what P4 settled:
a desk timeout parks with reason `timeout` (§5.3), and the `Repo` search attribute comes from `fact:item`'s `repo`
(§5.1). Each is marked *rev 8.2* inline. **Owner decisions of 2026-10-14** (PR #192) align two texts with rev 8.2: a
held item event is re-sent only to instances that started before it, and only within 1 minute (§5.4); a desk timeout
always parks and `then:` doesn't apply when it fires (§3.4, §3.8). Each is marked *owner, 2026-10-14* inline
· **Date:** 2026-10-10 · **Builds:** factory addendum §17.28 (one workflow
state machine) and §17.29.4 step 2 · **Next:** P4a (the Temporal foundation), then `/specToProvenPR` takes the Phases at
the end to PRs.

**Revision note (2026-10-19, CI apply, proposed):** the CI apply amendment (`2026-10-19-sindri-ci-apply-design.md`) edits W8 and W9 (CI-5), §4.1 (CI-6) and the action-class table (CI-7): a hand-written recipe that a listed merger merged into a CI repo's branch, with its required checks green and within its capability ceiling, is approved by the `ci-apply` service (`via: ci`); revoke, drift re-approval and the grants stay `protected`.

**Revision note (2026-10-16, DT-C):** a child's declared `outputs` are the facts of its parent's `wait.satisfied` (§3.2, §5.8, §9.2), `produces` gains the long form `{ fact, from }` (§3.5), `RCP-WIRE-003` joins §9.1 and `RCP-OUT-002` is an error, not information, when the fact only arrives through a child (§9.1). Each is marked *DT-C, 2026-10-16* inline.

**Revision note (2026-10-14):** the UI screens spec's (`2026-10-13-sindri-ui-screens-design.md`) edits E-21 (recipe
`urgency`), E-22 (engine half: `outputs`, the fact types, `inputs[].kind`), E-30 (the ledger column that links Temporal
history to ledger sequences) and E-31 (the scoping recipe's draft proposals), and the states path order (its §5.1), are
written in. Sections: §3.1, §3.2 (top-level table, `inputs`, `urgency`, `outputs`), §3.3 (fact types, subject kinds),
§3.10 (new: path order of states), §5.6, §6.3 (verbs and routes), §9.1, §12 (the scoping recipe's requirements), W29
in §1 and Phases 2, 4 and 7. Where they contradict older text the UI spec wins (README X52): W29 and §5.6 said nothing
reads Temporal history but the engine and its tests; the API now reads it for `GET /engine` (R8-12), the UI only through
the API, and the evolve loop, `workflow events` and the item timeline still read the ledger. Existing rule ids and phase
numbers are unchanged.

This doc makes §17.28 concrete. It doesn't reopen §17.28 or §17.29, which are settled (decisions Q6 and Q7). It
settles the block contract, the recipe schema, approval and invariant 13, the instance and its events, the CLI and
API surface, the boundary with S1's app shell, and the first recipes. §13 maps every review finding to its fix:
C1–C32 from pass 1, E1–E24 from pass 2, G1–G18 from pass 3, H1–H20 from pass 4 and I1–I12 from pass 5. Revision 7 applied the epic
reconciliation (`docs/sindri/README.md`, X1–X42). **Revision 8** (owner decision Q8, README X53–X59) replaces the
hand-built durable-execution mechanics of §5 with Temporal, relaxes NativeAOT for the Sindri host (X54), and puts every
agent session under tmux so the human can join it live (X55); §0 states the decision and the mapping, and every other
amendment is marked *rev 8* inline.

References:
- the factory addendum `2026-10-09-sindri-factory-addendum.md` (A) and the core design `2026-10-07-sindri-design.md` (D);
- `docs/csharp/bugfix.md`, the reference for gates and state;
- `.agents/rules/csharp.md` and `csharp-testing.md`;
- S1, the app shell on `feat/sindri-s1-app-shell`, whose C# catalogue, verbs and exit codes are in
  `docs/csharp/sindri.md`. Its types are named as they are on that branch: `ILedgerStore`, `ILease`, `LeaseGrant`,
  `LeaseDecision.Holds`, `IScheduledService`, `EventFactory`, `EventScope`, `Output`, `SindriErrors` and
  `LedgerDatabase`.

## 0. Revision 8: Temporal is the substrate (owner decision, 2026-10-11)

**Decision.** The engine runs its instances on [Temporal](https://temporal.io) through the .NET SDK (`Temporalio`),
against a local Temporal server that Sindri starts and keeps alive beside its own process. Sindri no longer
implements durable execution itself: no instance store, no fencing, no inbox and router, no deadline table, no
nonce table, no event fold. One Sindri workflow type, the **recipe interpreter**, runs every recipe. The recipe
format, the validator, approval and invariant 13, the callers and action classes, the CLI and API surface, the
error catalogue, the first recipes and the eval format are unchanged. §5 is rewritten; the rest of this document
is amended where it named a mechanism §5 used to own (each amendment is marked *rev 8*).

**Why.** The design walkthrough of 2026-10-11 found that §5 of revision 7 hand-built the feature set of a durable
execution runtime (replay, timers that survive sleep, idempotent triggers, child propagation, held events, pending
parks) without a written buy-or-build decision, while the context spec had one for Onyx. Weighed against the .NET
options (Temporal, Microsoft Durable Task, Dapr Workflow, Microsoft Agent Framework workflows, Elsa), Temporal is
the only one that runs locally on its own SQLite file, needs no cloud backend or sidecar, has a GA .NET SDK, and
covers every mechanism in rev 7 §5. Its costs are accepted: one more always-on process, no NativeAOT for the
process that hosts the Temporal worker (the SDK has an open trimming issue, temporalio/sdk-dotnet#286), and a
deterministic-code rule for workflow code.

**What Temporal gives, and what stays Sindri's:**

| Rev 7 mechanism | Temporal primitive | Stays Sindri's |
|---|---|---|
| Instance row, version, `Commit`, event fold | A workflow execution per instance; its event history is the engine's private truth | The `workflow.*` events mirrored into `ledger.db` (§5.6), which stay the one public log |
| Trigger-keyed idempotency, nonces, `SND-WF-009` | Workflow **Updates** with the nonce as the update id (deduplicated, validated before they are accepted) | The nonce endpoint, the challenge, the action classes, the API chain |
| Deadlines, `retry`, `takeoverIdle`, sleep shift | `Workflow.DelayAsync` in cancellation scopes | The park and escalate rules the timers trigger (§3.8) |
| Inbox, router, cursor, held and expired rows | **Signals**, buffered until the workflow consumes them | The ledger-to-Temporal bridge service that turns registered ledger events into signals (§5.4) |
| Children, propagation, forwarded answers | **Child workflows** with a parent-close policy; signals up; an Update on the child | The address grammar and `onBehalfOf` |
| Run token, desk and agent reports | An **activity with async completion**: the task token is completed by `sindri workflow complete` *(rev 8.1: a desk Step is a short activity and the report is the `complete` Update, §5.3; rev 8.2, provisional owner default Q1: an agent Step's report is the same `complete` Update, checked at once, and a long heartbeating activity only watches the session)* | The launch secret, the report validation (§5.5), the outcomes and outputs contract |
| Outbox commands, dispatch delay, recheck | **Activities** with retry policies, after `DelayAsync(dispatchDelay)` and a revoke recheck | Effect identity, the effect allowlist, the ledger row every effect writes |
| Fencing, lease epoch, `VersionConflict`, `SND-WF-002` | Task-queue ownership; Updates serialize per workflow | Nothing. `SND-WF-002` is retired |
| `workflow list`, `--needs-me` | Visibility **search attributes** (`Item`, `Recipe`, `State`, `NeedsYou`, `Repo`) | `GET /needs-you` stays the one list, folded from the ledger (API §3.6) |
| Eval cases, timer tests | The **time-skipping test server**; the replayer for recorded histories | The eval JSONL format (§9.2) and `recipe eval` |
| Drift pins | Workflow **patching** for interpreter code changes | Recipe approval by hash, pins and the drift park (§4) |

**Not Temporal's job:** items, recipes and `RCP-*`, approval and the challenge, callers and `SND-AUTH-002`, rung
gates, the ledger as the public event log, the API, the UI, MCP, the context engine, and authorization of any kind
(the server listens on loopback with no auth; Sindri's API chain is the only front door, §5.2).

**Operations.**
- The server is the `temporal` CLI's dev server, pinned by version and checksum in `binaries.lock`:
  `temporal server start-dev --db-filename $AW_SINDRI_HOME/temporal.db --ip 127.0.0.1 --port <p> --ui-port <p>
  --namespace sindri`. `sindri serve` starts it when it isn't running and records its pid in `control.json`;
  launchd keeps both processes alive on the always-on box. `sindri doctor` gains a `temporal` line (reachable,
  version matches the pin, namespace present). The Temporal Web UI on loopback is a debugging view, never a surface
  the human decides from.
- The worker runs inside `sindri serve`, on task queue `sindri`, namespace `sindri`. In P2 of the §17.20 ladder a
  second worker on another host joins the same task queue; nothing in the recipes changes.
- The Temporal adapter lives in its own project, `Aw.Sindri.Workflows` (Infrastructure tier, `AwAotCompatible=false`
  with the reason), so every other library keeps `IsAotCompatible=true`. The Sindri host publishes
  framework-dependent (csharp.md "AOT scope", README X54). The Avalonia app stays NativeAOT.
- `Temporalio` is a reviewed package in `Directory.Packages.props`, locked, from nuget.org; its native library ships
  inside the package for `osx-arm64`, `osx-x64`, `linux-x64` and `linux-arm64`.

**Re-decided in rev 8.** Rev 7 made downtime count as sleep (a laptop shut for three days fired no timeouts).
Temporal's timers are server-side wall-clock, so an expired timer fires when the server wakes. Rev 8 accepts that:
a timeout that fires on wake parks or escalates, which is harmless and visible, and a takeover still pauses timers.
If dogfooding shows wake-storms, the bridge service gets a `shift` signal that extends deadlines by the measured
gap. Recorded as README X58.

**Phases.** P4a (the Temporal foundation, *rev 8.1*) is new: Temporal is decided (owner, 2026-10-12), and P4a lands
the substrate with its acceptance checks measured; P4 and later build on it. P1 (merged, #179) and P2 are unchanged.
P3 (#180) keeps its pure decisions and drops the frame, fold and store work. P4 through P8 are rewritten at the end.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| W1 | **A block is a DI-registered class that implements its kind interface (`IGate`, `IEvidenceProvider`, `IStep`, `IEffectKind`) and exposes a static `BlockDescriptor`**, registered generically (`services.AddGate<T>()`). `BlockCatalog` (descriptors) and `BlockRuntime` (from `IEnumerable<…>`) are built from those registrations | The validator stays pure, registration is AOT-safe, and there's no service locator (`ARCH-DI-001`) |
| W2 | **Wiring is checked on every path** | A fact produced on only some paths is a latent refusal at run time |
| W3 | **Recipe schema v1 is closed** (§3). The reader normalizes short forms to the long form, and the schema and the hash describe the long form | Generated recipes can't slip logic in, and no union converters are needed |
| W4 | **The rung comes from the block's descriptor.** Rung-2 and rung-3 gates must have a `threshold` | A recipe can't promote a judge question to deterministic |
| W5 | **Fail closed.** An unknown block is a validation error. An unavailable gate or evidence runtime refuses. A Step with no runtime parks with `SND-WF-010`, unless its descriptor declares `deskCapable`, in which case it runs on the desk runtime (W20) | Nothing passes silently, and the desk fallback is opt-in per block |
| W6 | **Outcomes are a closed set:** `resolved`, `unresolved`, `alreadySatisfied` and `failed` for recipe terminals, plus the engine's `cancelled` | Receipts count outcomes |
| W7 | **Grants are explicit and need a terminal.** `--grant n` (1 ≤ n ≤ the loop's cap) answers a `rounds` or `no-progress` park. `--grant-tokens n` (at most the original budget, once per park) answers a `budget` park | Every park has a useful answer (E10). A session can never grant itself anything |
| W8 | **Every recipe needs a human approval to run**, or a CI approval: the merged, checked PR stands in for the typed confirmation (CI-5). Approvals are stored keyed by `(id, version, hash)`, never in the file | §17.19: workflow templates are approval-tier |
| W9 | **Hand-written recipes are approved with `sindri recipe approve`**, through a server-issued challenge, **or by CI apply** when a listed merger merges the file into a CI repo's branch with its required checks green, within its capability ceiling (CI-5; CI apply spec §3.4). **Generated recipes are approved by the packet freeze**, from a `base` that Sindri code picks | Invariant 13 |
| W10 | **`RCP-CAP-001`–`007` keep a generated recipe at or below its base** (§4.3), comparing guards by path and loops by state | C16, C17, E19 |
| W11 | **Approval and pins are checked on every `Commit` and at dispatch.** Approval pins the descriptor hashes of the blocks the recipe names and the hashes of its children. A revoke cascades to the generated recipes approved from it, and wakes running instances so they park at once. `cancel`, `takeover`, `handback`, `takeoverIdle` and engine-owned notifications are exempt from the check. Drift is fixed by re-approving the same hash with the new pins | Drift and revokes take effect immediately, and every instance can still be cancelled or resumed (C13, E15, G2, G7) |
| W12 | **A transition is keyed by its trigger:** `wf:{instance}:{trigger}`. `Commit` checks, in order: the caller (API's chain), the key, the nonce, the run token and state, the fence, the approval, the version. Only **decisions** (applied, or refused by a gate or evidence) are recorded under the key and replayed; a validation refusal (actor, nonce, token, fence, version, malformed report) records nothing and consumes nothing | Replays are no-ops, and a bad request never burns a task's key (C1, G6, H3)  *(rev 8: the key is the Update id; the validator rejects without recording; §5.2)* |
| W13 | **One `Commit` per trigger**, in one SQLite transaction, with one event per instance it changes (the instance's own event, plus `workflow.started` for a child it creates): the event(s), the version bump, the instance row (and a new child row), deadlines, outbox commands with their id map, inbox rows and consumed nonces. Every change to an instance row goes through it | A 17.28.1; E13  *(rev 8: one ledger event per instance per transition, written by `RecordEvent`; no SQLite transaction of the engine's own; §5.6)* |
| W14 | **Each refusal consumes its trigger, and what follows is defined** (§5.5) | C2 |
| W15 | **External events reach instances through the engine inbox** (§5.7) | C3 |
| W16 | **Engine-owned states are frames on a stack**, at most two deep. A park, an escalation or a takeover suspends the current frame (`entry`, run, child, deadlines) together with its **resume action**, and the answer that pops it runs that action (§5.8). Deadline ids carry an arm sequence | Inbox rows, run tokens and children survive a park or takeover; an answer always does something (E1, G1)  *(rev 8: the stack is `suspension` and `pendingPark` on the interpreter; same behaviour; §5.8)* |
| W17 | **Children are created in the parent's `Commit`** and propagate both ways through the inbox (§5.7), keeping the actor of a forwarded command | C6, E14 |
| W18 | **Timers are deadlines applied by built-in services.** Sleep and downtime count as time when nothing ran. They're detected through an `IMonotonicClock` port alongside `TimeProvider`, and applied per instance as keyed `shift` triggers | Replayable and versioned (C19)  *(rev 8: timers are `Workflow.DelayAsync`; downtime fires on wake, X58; §5.5)* |
| W19 | **A takeover pauses deadlines; a park stops them** (its answer re-arms them fresh). Each takeover arms a `takeoverIdle` deadline that notifies the human once and clears | D §9.3  *(rev 8: kept; implemented with cancellation scopes and `remaining`; §5.5)* |
| W20 | **The desk runtime runs `deskCapable` Steps until the agent runtime (§6.5, P8) runs them.** The state stays `active` with an open task. Its timeout is at least the profile's `workflow.deskTimeout` (default 2d) and always ends in `park`. The human reports with `sindri workflow complete --run <token>`. Reports are validated synchronously. Outcomes are labeled `via: desk` | Every phase reaches a real outcome without letting the agent-sized timeouts park the human's own work (C7, E11) |
| W21 | **Every event carries the full post-state delta** | Folding the log rebuilds the row (C20)  *(rev 8: kept for the mirrored ledger events; the engine's own replay is Temporal's history)* |
| W22 | **Callers and permissions follow the API surface spec** (API §4, §6): a caller class (`human`, `session`, `service`) with a terminal flag, and an action class per command or variant. Every engine command runs through API's command chain (gate, audit, authorization). A `protected` action also needs the server challenge (§4.2) typed at a TTY (§6.2) | One permission model for every surface; text typed into a session is never a decision (E2) |
| W23 | **The engine lives in `Aw.Sindri.Core/Workflow` and in Sindri's Features, Infrastructure and Host. Phase 1 is stacked on `feat/sindri-s1-app-shell`.** Stacked phases add files and append registrations; changes to S1's behaviour wait until S1 merges | A 17.28.3; the S1 session's work isn't disturbed  *(rev 8: the Temporal adapter is its own project, `Aw.Sindri.Workflows`; §0, §7.1)* |
| W24 | **The first recipes are `workflow.item` and `workflow.step`** (§12). Dev recipes in `recipes/dev/` use real, harmless blocks for the phase signals | No fixture code ships in the binary |
| W25 | **The JSON Schema is generated from the long-form records**, with a hand-written editor overlay. Staleness tests guard both | The `aw-packets` precedent |
| W26 | **Each S1 change is owned by one engine phase** (§7.2) | No dependency without an owner |
| W27 | **Exit codes:** the checker verbs (`recipe validate`, `recipe eval`) return 0 for pass, 1 for fail and 2 for usage or tool errors, like `aw-packets`. Every other verb uses S1's `Output.Error`: 1 when Sindri isn't running, 2 for any other error, with the line `CODE message (fix: …)` | Consistent with the repo's checkers and with S1 (E8) |
| W28 | **Temporal is the substrate** (owner, 2026-10-11). One `RecipeWorkflow` interprets every recipe; instances are workflow executions, commands are Updates, Steps are activities with async completion, events are signals, children are child workflows, timers are `DelayAsync` | §0: the only .NET option that runs locally on SQLite with a GA SDK and covers every rev 7 mechanism |
| W29 | **The ledger stays the public log.** Every transition is mirrored into `ledger.db` by an idempotent local activity; the API reads Temporal history only for the read-only `GET /engine` family (§5.6), the UI reads it only through the API (R8-12), and the evolve loop, `workflow events` and the item timeline read the ledger | One log for the UI, the API and the evolve loop (X16, API §5); UI screens E-30 |
| W30 | **Authorization never reaches Temporal.** The API chain is the only writer of Updates and starts; the server is loopback with no auth | API §4; the honest-mistake threat model (A §17.28.3) |
| W31 | **tmux owns every agent session; Warp attaches.** `attach` joins the live session; typing is a takeover | README X55; the human can step onto the line at any Step (API §8.1, UI §6.2) |
| W32 | **The Sindri host is framework-dependent;** the Temporal adapter opts out of `IsAotCompatible` with its reason; the Avalonia app stays NativeAOT | temporalio/sdk-dotnet#286; README X54; csharp.md "AOT scope" |
| W33 | **Temporal is decided; there is no spike** (owner, 2026-10-12, *rev 8.1*). P4a, the Temporal foundation, lands the substrate with five acceptance checks, each measured | The numbers were taken before any other engine phase depends on Temporal |

## 2. The block contract

```csharp
public enum BlockKind { Gate, EvidenceProvider, Step, Effect, StepInput }

[Flags]
public enum Capability { None = 0, Tools = 1, Effects = 2, AutonomyTier = 4, WriteScope = 8, Network = 16, Budget = 32 }

public enum Rung { Deterministic = 1, JudgeQuestion = 2, BoundedModel = 3 }

public sealed record BlockDescriptor(
    BlockId Id, BlockKind Kind, int Version,
    IReadOnlyList<FactKey> Inputs,
    IReadOnlyList<OutputSpec> Outputs,            // (fact, requiredOn: outcomes); checked against every result and desk report
    IReadOnlyList<OutcomeName> Outcomes,
    Capability Capabilities,
    IReadOnlyList<ParameterSpec> Parameters,
    Rung? Rung,                                   // gates only
    bool DeskCapable);                            // Steps only: may run on the desk runtime (W5, W20)

public sealed record ParameterSpec(string Name, ParameterType Type, bool Required, bool IsCapabilityField);

public sealed record EventDescriptor(
    EventKind Kind,                               // claim.created, item.routed
    string Producer,                              // the actor that may write it (a service or adapter id)
    EventRoute Route,                             // Causation (only to the frame of the command it answers) or Item
    IReadOnlyList<OutcomeName> Outcomes,          // read from the event's `outcome` field
    IReadOnlyList<FactKey> Facts);                // facts the event records

public interface IDescribedBlock { public static abstract BlockDescriptor Descriptor { get; } }
```

- **Parameter-declared outcomes and inputs (decision-tree spec §8.1, DT-V1):** `BlockDescriptor` also has optional
  `OutcomesFrom` and `InputsFrom` (a list parameter's name, or `<stateParam>.<listParam>` for a twin), and `OutputSpec`
  optional `Every` and `EngineFilled`. Each is written only when set, so a descriptor without them hashes as before.
  Outcomes and inputs of a Step in a recipe are read through `StepOutcomes.Of` and `StepInputs.Of`.
- **Capability-bearing:** `Capabilities != None`, a parameter with `IsCapabilityField`, or (in a generated recipe) a
  child.
- **Runtime interfaces:**
  - `IGate.Evaluate(GateContext) → GateResult(pass|fail|uncertain, score?, reasons)`;
  - `IEvidenceProvider`, per A 17.11;
  - `IStep.Start(StepContext)` and `IStep.Complete(StepRun, StepReport) → StepResult`;
  - `IEffectKind.Build(EffectContext) → payload`;
  - `IStepInput.Fill(StepInputContext) → InjectionSections`: fills sections of a Step's injection plan (A §17.8) before
    `Start`, for example the context engine's `input.context.retrieve` (context spec Phase 10). Declared in P1; the
    first runtime comes with that phase.
- **`Start` runs after the `Commit`** that opened the run. The engine's post-commit loop calls it, keyed by the run
  token, so a re-run after a crash is a no-op.
- **Registration and placement:**
  - `services.AddGate<T>() where T : class, IGate, IDescribedBlock`, and likewise `AddStep`, `AddEffect`,
    `AddEvidence` and `AddEvent(descriptor)`. Blocks are scoped; the catalog is a singleton.
  - Pure blocks live in `Features/Blocks/<Kind>/`, I/O blocks in `Infrastructure/Blocks/<Kind>/`.
  - A block over a lower-tier package (Judge, Evidence, Packets, BugFix's rules) lives in Sindri and reaches that
    package through its `Core` or CLI, under a declared `deps.yaml` edge.
- **Descriptor hash:** SHA-256 over the RFC 8785 canonical JSON of the descriptor. A behaviour change to a block
  bumps `Version` (reviewed; §13 S2-L-c).
- **Tests:** `BlockContract` in `Aw.Sindri.Testing` is the base each block's contract tests derive from. It runs once
  per block.
- **Guide:** `docs/csharp/sindri-workflow.md` (P1) holds the hand-written recipe for a block and the recipe
  reference.

## 3. Recipe schema v1

### 3.1 Reading

- **Refused YAML** (`RCP-SCHEMA-003`): duplicate keys, anchors, aliases, merge keys, explicit tags, more than 256 KiB,
  or nesting deeper than 32.
- **Scalars:** the YAML 1.2 core schema, so `on` stays a string.
- **Errors:** every node keeps its line and column, and every schema error is reported, not just the first.
- **Normalization:** short forms become long forms, then typed binding runs with
  `JsonUnmappedMemberHandling.Disallow`.
- **Hash:** `sha256:` over RFC 8785 (JCS) of the normalized document without `hash`, so a short form and its long
  form hash the same. Hashes are shown as 12 hex characters, and input accepts a prefix of 8 or more. The normalized
  document writes every absent optional value as `null` and every absent list or map as empty (owner decision, 2026-10-11), so adding an optional field to the
  format re-hashes every recipe, and each needs re-approval. Adding `outputs`, `urgency`, `name`, an input's `kind` or an input's `generate` to an
  already approved recipe is therefore a new hash and a re-approval too: the registry's approved hash covers the whole
  normalized file, and these fields are part of it (they are presentation, but the app renders from the approved
  document). `name` and `generate` are hashed because they change what an instance is called and which value it starts with. The fields themselves land in P2 for this reason (Phases, P2).

### 3.2 Top level

| Field | Req. | Rule |
|---|---|---|
| `schema` | yes | `1` |
| `id` | yes | `workflow.<name>` |
| `version` | yes | `RCP-VERSION-001` (same id and version, different hash); `-002` (not above the highest registered) |
| `description` | no | |
| `base` | generated only | Written by Sindri's generator. A hand-written recipe with `base` is `RCP-CAP-004` |
| `urgency` | no | `incident \| triage \| work`, default `work` (UI E-21). The app orders Home's Workflows strip and the Workflows list's groups by it, `incident` first. It changes no engine behaviour: no timer, escalation or notification reads it. `RCP-URG-001` (any other value). The normalized document writes the default, so an absent `urgency` and `urgency: work` hash the same |
| `inputs` | **yes** | At least one (`RCP-INPUT-002`). The long form is `{ key, label?, type?, kind, generate? }`. `kind` is one of the subject kinds in §3.3 and is required (`RCP-INPUT-001`). `type` is one of the fact types in §3.3 and defaults to the type `kind` pairs with. `generate` is a template (§3.11) that supplies the value when the start doesn't. The bare-string short form (`[fact:item]`) normalizes to `{ key }` with no `kind`, so it fails `RCP-INPUT-001`. The **first** declared input is the instance's subject (UI §5.4). `recipe show` and `GET /workflow/recipes` return the inputs so the app can render a start form and a subject line |
| `name` | no | A template (§3.11) for the instance's name, rendered once at start. The name becomes `subject.label`. `RCP-NAME-002`–`006` |
| `outputs` | no | What the recipe produces for a human, in the long form `[{ key, label?, type?, from }]` (UI E-22). `key` is a fact key; `type` is one of §3.3's fact types (default `text`); `from` names the state that produces the fact. The app places the fact under that state in the states band and in the facts panel (UI §5.2). Declarations only: they add no wiring (`produces` and Step outputs still do), except on a child: *DT-C, 2026-10-16*: a child's declared `outputs` are the facts of the parent's `wait.satisfied` from it (decision-tree spec §8.4). An undeclared fact is still recorded and still shown, as `payload`. `RCP-OUT-001`–`003`, `RCP-TYPE-001`–`003` (§9.1) |
| `initial` | yes | An `active` or `wait` state |
| `states` | yes | At least one non-terminal and one terminal state; reserved names refused |
| `transitions` | yes | |
| `loops`, `budget` | no | |
| `evals` | to approve | Paths under `evals/<id>/`, inside the recipe's directory (§9.2) |
| `hash` | no | Checked when present |

Reserved state names: `cancelled`, `escalated`, `parked`, `taken-over`.

### 3.3 Value grammars

| Grammar | Form |
|---|---|
| Fact key | `fact:<name>` or `evidence:<kind>@<base\|head>` |
| Block id | `<gate\|step\|effect\|evidence\|input>.[<area>.]<name>` (`step.produce`, `gate.budget.available`) |
| Event kind | `<area>.<name>`, registered as an `EventDescriptor` |
| Duration | `<n>m`, `<n>h` or `<n>d`, at least `1m` |
| Tokens | int, or with a `k`/`M` suffix |
| Fact type | `text \| file \| item \| link \| scope-map \| packet \| evidence \| decision \| payload` (UI E-22, §5.2). `text`, `file` and `item` are the input types of rev 7; the rest are for outputs and for inputs that carry a structured value. The type picks a viewer in the app and nothing else: the engine stores every fact as a hashed blob, and a type it doesn't know is `RCP-TYPE-001`, not a runtime error |
| Subject kind | `item \| linear-project \| webhook \| file \| text \| link \| packet \| payload` (UI E-22, §5.4; `packet` and `payload` added by DT-R1, owner decision 2026-10-16). Each input declares one (`RCP-INPUT-001`). `item` pairs with type `item`, `file` with `file`, `text` with `text`, `link` with `link`, `packet` with `packet`, and `linear-project`, `webhook` and `payload` with `payload`. Any other pairing is `RCP-TYPE-003` |
| `with:` value | A literal (string, number, bool or a list of these), `{ fact }` or `{ profile }`. Capability parameters take only literals or `{ profile }` (`RCP-CAP-007`) |

### 3.4 States

Every state has `kind: active | wait | terminal`. State, loop and parameter (`with:` key) names match
`[a-z][a-z0-9-]{0,63}` (`RCP-NAME-001`), so a node path is never ambiguous.

| Field | `active` | `wait` | `terminal` |
|---|---|---|---|
| `step` | **required** `{ use, with?, inputs? }`; `inputs` lists `{ use: input.…, with? }` blocks run before the Step starts | — | — |
| `waitFor` | — | **required** | — |
| `timeout` | **required** `{ after, then: park\|escalate\|retry }` | **required** | — |
| `loop` | optional | optional | — |
| `budget` | optional | — | — |
| `next` | **required** | **required** | optional |
| `outcome` | — | — | **required** |

`waitFor` takes one of four forms:
- `{ event: <kind> }`;
- `{ answer: { id, question } }`, satisfied by `workflow answer` with outcome `answered`, which records `fact:answer`;
- `{ resource: heavy | slot }`. `slot` comes from the P7 stub. `heavy` is allocated by Sindri's heavy-job lock port, the same
  port the context engine's `IHeavyJobLock` adapts (context spec §4). Until that port lands it parks with `SND-WF-010`,
  and the validator reports that;
- `{ child: { recipe, inputs, onParked: propagate|ignore } }`. The child's `escalated` and `cancelled` always
  propagate.

`retry` stays in the **same frame entry**, so inbox rows and the run token stay valid. It re-arms the deadline
(`arm + 1`) and re-emits the effects of the transition that entered the state. Each re-emitted effect gets a new
command id but keeps its stable effect identity (§5.4), and the effect block's existence check makes the repeat
harmless. A second timeout parks with `timeout`. In desk mode a timeout always parks with `timeout` and `then:` doesn't
apply when it fires (W20, §5.3; *rev 8.2*, *owner, 2026-10-14*). `retry` isn't
allowed on a `child` wait (`RCP-TIME-002`).

### 3.5 Transitions

| Field | Req. | Rule |
|---|---|---|
| `from` | yes | A non-terminal state, or a list of them |
| `to` | yes | A recipe state |
| `on` | yes | `step.completed` or `step.failed` from `active`; `wait.satisfied` from `wait`; `command.<verb>` from any non-terminal state (class `desk`, fired with `sindri workflow command <address> <verb>`); verbs match `[a-z][a-z-]{1,31}`, and the reserved list in §6.3 is refused (`RCP-TRIG-002`). `RCP-TRIG-001` |
| `when` | no | §3.6 |
| `gates` | no | `{ use, threshold?, with? }`, run in order |
| `evidence` | no | `{ kind, at, expect: pass\|fail }` |
| `effects` | no | `{ use, with? }` |
| `produces` | no | Each must be an output or fact of the trigger's source (`RCP-WIRE-002`); from a child wait that is one of the child's declared `outputs`. *DT-C, 2026-10-16:* an entry is a fact key, or `{ fact, from }` to record a child's output under another key (both required; `{ fact: X, from: X }` is the bare key `X`, and a bare key still hashes as a plain string). Only a child's output can be renamed, and no fact is written twice on one transition (`RCP-WIRE-002`). Produced from a child on an outcome that doesn't carry it on every path inside the child, or on `cancelled`, is `RCP-WIRE-003` |

The first transition whose `when` holds is the matched one. If its gates refuse, that is a refusal (§5.5), not a
fall-through. A `step.failed` with no transition parks with `needs-answer`.

### 3.6 `when` (closed)

`{ outcome }`, `{ fact, is: present|absent }`, `{ loop, below: cap }`, `{ all }`, `{ any }` and `{ not }`. Outcome
names come from the trigger's source: a Step, an event, or a child's terminal outcomes plus `cancelled`.

### 3.7 Loops, budgets, `next`

- **Loops:** `{ cap ≥ 1, noProgress }`. `noProgress` defaults to `[findings, emptyDiff, sameFailure]` and can't be
  empty; the rule is A §17.23.13. Popping a frame never counts a round. **P3–P7 (owner decision, 2026-10-11, rev
  7.2):** no progress is detected only from identical findings (the digest of the whole findings value) and a repeated
  failure reason (a `step.failed` or a gate refusal); there is no superset check, and `emptyDiff` never fires. The
  agent runtime (P8) adds finding keys and a diff signal to Step reports, and the full A §17.23.13 rule lands there.
- **Budgets:** exhausting one is `→ parked(budget)`.
- **Recipe `next` variables:** `{item}`, `{instance}`, `{state}`, `{user}`, `{child}`, `{loop.<n>}`,
  `{loop.<n>.cap}`, `{budget.tokens.left}`.
- **Engine-only variables** (in §3.8 texts): `{reason}`, `{question}`, `{gate}`, `{score}`, `{threshold}`,
  `{since}`, `{run}`, `{after}`, `{from}`, `{to}`, `{effects}`, `{block}`, `{marker}`, `{effect}`, `{code}`, `{recipe}`,
  `{reasons}`, `{childNext}`.
- **Voice:** waits are descriptions ("Waiting for …"); human-action states are instructions.
- **No approval-tier verbs** may appear in `next` (`RCP-NEXT-002`).

### 3.8 Engine-owned states

| State | Entered by | `next` | Ways out (resume action, §5.8) |
|---|---|---|---|
| `parked` (`rounds`, `no-progress`) | A loop cap or no progress, either on the transition back into a looped state (kind `loop`) or on a gate refusal at the cap (kind `refusal`); the frame records which | "Parked ({reason}): {question}. Grant more rounds at a terminal, or cancel." | `answer --grant n` (protected): kind `loop` performs the withheld loop transition, and kind `refusal` re-runs the Step. Either way, the round it starts is the first of the n. `answer --text` on `no-progress`: the same, once per loop (§13). `takeover`, `cancel` |
| `parked` (`budget`) | A budget, or a `gate.budget.*` refusal | "Parked (budget): {question}. Grant tokens at a terminal, or cancel." | `answer --grant-tokens n` (protected): re-runs the Step, or re-evaluates the withheld transition. A re-run that would pass the loop's cap parks `rounds` (kind `refusal`) instead; `--grant n` is the way on (W7, §5.8, §13). `takeover`, `cancel` |
| `parked` (`needs-answer`, Step) | A refusal without a loop, `step.failed` | "Parked (needs-answer): {question}. Answer with --text; the Step runs again with your answer." | `answer --text`: re-runs the Step with `fact:answer`. A re-run that would pass the loop's cap parks `rounds` (kind `refusal`) instead; `--grant n` is the way on (W7, §5.8, §13). `takeover`, `cancel` |
| `parked` (`needs-answer`, wait) | A gate refused the transition out of a wait | "Parked: {gate} refused {from} -> {to} ({reasons}). Fix the cause, then resume to check again, or cancel." | `answer --resume [--text]`: re-checks every gate and evidence at the current head under the answer's key (§5.8). `takeover`, `cancel` |
| `parked` (`timeout`) | `then: park`, or any timeout in desk mode, whatever `then:` says (*rev 8.2*, *owner, 2026-10-14*) | "Parked (timeout): {state} ran past {after}. Resume to give it the full time again, or cancel." | `answer --resume`: back to the frame (the same run token) with a fresh deadline. `takeover`, `cancel` |
| `parked` (`spec-conflict`: revoked) | `recipe revoke` of this instance's own recipe | "{recipe} was revoked. Cancel, then start a new attempt from an approved version." | `cancel` |
| `parked` (`spec-conflict`: child revoked) | The child hash this recipe pins was revoked | "{child recipe} (pinned by {recipe}) was revoked. Re-approve {recipe} so it pins an approved child; the approval resumes this instance." | The parent's drift approval resumes it (§4.1). `cancel` |
| `parked` (`spec-conflict`: drift) | A pinned block changed in this build | "{block} changed since {recipe} was approved. Re-approve it (`sindri recipe approve {recipe}`, shown as `id@hash12`); the approval resumes this instance." | The drift approval resumes every instance it lists, and re-sends any of their commands refused for the drift (§4.1). `cancel` |
| `parked` (`spec-conflict`: dispatch) | `outbox.refused` with `SND-WF-011` (not allowlisted) | "{effect} isn't on the profile's effect allowlist. Add it and approve the profile, then resume to send it again." | `answer --resume`: re-emits the effect. `cancel` |
| `parked` (`SND-WF-010`) | A Step or resource with no runtime | "No runtime for {block} in this build. Cancel, or resume in a build that has it." | `answer --resume`: starts the Step again (a new run), or waits for the resource again. `cancel` |
| `escalated` (gate) | A top-rung gate below threshold or uncertain | "Escalated: {gate} scored {score}, below {threshold}. Accept to override it (the other checks still run before {to}), or reject (the Step re-runs within its loop, otherwise this parks)." | `--accept` (protected) overrides only that gate, then evaluates the remaining gates and evidence at the current head. `--reject` is a refusal. On a command's escalation, `--reject` (or an `--accept` another check refuses) pops the escalation and the command is refused with `SND-WF-004`. `cancel` |
| `escalated` (timeout) | `then: escalate`, outside desk mode (a desk timeout parks, *owner, 2026-10-14*) | "Escalated: {state} ran past {after}. Resume to give it the full time again, or cancel." | `--resume`, `cancel` |
| `escalated` (child) | The child escalated | "Escalated in {child}: {childNext}" | The child's `answer` variants, forwarded (§5.7), and shown as the child's `allowedCommands`. `cancel` and `takeover` act on the parent |
| `taken-over` | `takeover` (allowed on a parked or escalated instance as well) | "With you since {since}. Run `sindri workflow handback {instance}` when you're done." | `handback` (pops back to whatever it covered), `cancel` |
| `cancelled` | `cancel` from any state, the tracker, a cancelled parent | (terminal) | outcome `cancelled` |

### 3.9 Left out of v1

- **Fan-out children** come in schema v2, with the Step graph and stacks (A 17.28.6 steps 3 and 6).
- **Per-transition timeouts** come only if a recipe needs one.
- **A free expression language:** never.
- `name` and `generate` (§3.11) are closed templates: literals and fixed placeholders. They have no operators,
  functions or conditions.
- **Variables and includes:** not planned.

### 3.10 Path order of states (UI E-20, §5.1)

The app shows a recipe's states left to right in **path order**. The engine computes it and serves it, so the app never
parses YAML. It is derived from the normalized document, is not part of the hash, and is computed by a pure function in
`Core` (`StatePathOrder`) on the transition graph that the validator already builds for `RCP-REACH-001` and
`RCP-EXIT-001`. It is returned by `recipe show`, `ListRecipes`, `ShowRecipe` and the instance view as `states[].order`,
`states[].branchOf` and `loops[]`:

1. Walk the graph depth-first from `initial`. From each state, follow its outgoing transitions in the order the
   recipe lists them (§3.5); the **first** one that reaches an unvisited state is the main path. The transition list is
   the tie-break everywhere.
2. A transition back to a state still on the walk's stack is a **loop edge**, not a path step. Each becomes one entry in
   `loops[]`: `{ name?, from, to }`, with `name` set when the looping state declares `loop:` (§3.7). The two states
   are drawn adjacent with `⇄`.
3. A later transition from a state to an unvisited state is a **branch**: that state and what only it reaches get
   `branchOf: <the state the branch leaves from>` and `order: null`, and are folded under it. A state reached by
   both a main-path transition and a branch keeps its main-path place.
4. Main-path states get `order` 1, 2, 3… in walk order; **terminal** states come last, in the order the walk found
   them. The engine-owned states of §3.8 are not in the order: they are overlays on whatever state they cover.
5. `orderedBy` is `path`, or `transition-list` when `initial` has more than one distinct first target (several initial
   paths), in which case the first listed transition from `initial` leads and the band says "ordered by transition
   list". The field is returned beside `states[]`.

A state the walk never reaches is already `RCP-REACH-001`, so every state of a valid recipe has an `order` or a
`branchOf`.

### 3.11 Instance names and generated inputs (P4c)

**Grammar.** A template is literal text with placeholders in braces. `{{` and `}}` write literal braces. These
are the placeholders:

| Placeholder | Renders | In `name` | In `generate` |
|---|---|---|---|
| `{fact:<name>}` | The value of a declared input, rendered by its kind (below) | yes | no |
| `{start:date}` | The start time in UTC, as `yyyy-MM-dd` | yes | yes |
| `{start:time}` | The start time in UTC, as `HH:mm` | yes | yes |

Example: `name: "{fact:scope} · {start:date}"` and `generate: "triage {start:date}"`.

**Rendering by kind** (for `{fact:…}`):
- `item`: its `ITM-` id (not the title).
- `text`: the first non-empty line, trimmed.
- `file`: the file name.
- `link`: the `title` field, else the URL's host.
- `linear-project`, `webhook`, `payload` and `packet`: the payload's `id` field, else `number`, else the bare key (`SubjectRefs.PayloadId`, the same text a subject label shows).

Each value has its control characters and newlines replaced by a space. The value is then cut to 40 characters
and the whole name to 80 characters. Both cuts use `SubjectRefs.Cut`, which never splits a surrogate pair and
adds no ellipsis.

**Missing values.** `StartInstance` refuses a missing input, and `generate` fills the absent ones. So the only
value that can be missing at render time is content that can't be read: a fact that isn't stored, a payload
without an `id`, or an item whose title is unavailable after the id fallback. Such a placeholder renders as the
input's bare key (`scope`). The literals, the date and the other values stay in the name, and the reader sees
which part is missing. If the rendered name is empty after trimming, the instance address is used.

**When `name` is rendered.** The workflow renders it, on every replay, from deterministic inputs: the text each
input's `ReadSubject` local activity returned (one recorded marker per fact) and `InstanceStart.StartedAt`. The
activity returns the subject text of one fact, not the name, so the history keeps the texts and `workflow.started`
records the rendered result (`subject`). Because the inputs are fixed at start, the name is the same on every replay
and is never re-read from the item or the facts: renaming the item or editing the fact later doesn't change it.
`InstanceNames.Render` runs inside the workflow, so any later change to what it returns for the same inputs needs a
`Workflow.Patched` guard (it would otherwise change the name of a running instance on replay).

**`generate`.** It is allowed only on an input of `kind: text`. An item, file or link refers to something that
already exists, and a `linear-project` or `webhook` payload comes from outside, so none of these is made from a
template. `StartInstance` renders `generate` when the start supplies no value for that input. It uses the start
time it stamps on the instance and runs before the workflow starts. The result is stored as that input's fact,
like any supplied fact. Its producer is `engine:generate`, so the ledger, `facts[]` and `status`'s `inputs` line
show that the value was generated. A supplied value always wins. A scheduler passes only the start time and no
facts. An input with `generate` is served with `required: false`. `generate` reads only the start time and
literals, never another input (owner answer, 2026-10-15), and the time is UTC (owner answer, 2026-10-15).

**Subject.** `subject.kind` and `subject.key` are those of the first input, as before (the key rules of
`SubjectRefs`). `subject.label` is the rendered name when the recipe has `name`, and otherwise the per-kind rule
of UI §5.4. A `link` subject's label is the `title`, else the URL's host, and its `href` is the URL (it matches
`FactRow.ref {url, host, title}`). The `Subject` search attribute stays `subject.key`. `--subject` and `?subject=`
match the key exactly or the label, which is the name, as a case-insensitive substring. The views do this
filtering, as they do today, so no new search attribute is needed. Every instance has a subject on the wire.

**A pre-P4c instance.** A recipe registered before input kinds has an input with no `kind` (or no input at all), so
nothing names its subject. Its approved row still loads and an instance already running on it keeps running, with the
commands it made before P4c (a replay must match the history it recorded, so it still reads its first input once and
drops the text). That instance is not named: it has no `name` and its `subject` is the item itself, `kind: item`,
`key` the item id, `label` the item's title, else the instance address, which is what the list row shows too (the
detail and the list agree). It has no `inputs[]`, and its `detail` names `RCP-INPUT-001` or `-002` and the fix. A new
start on such a recipe is refused (`SND-RECIPE-001`, before a pin-drift refusal: re-approving can't fix it), and so is a first
approval of it (before the no-evals check). A row already approved can still be re-approved or woken, so an instance parked on it can resume. `recipe list` marks the row `legacy-shape`, approved or not.

## 4. Approval and invariant 13

### 4.1 Store and events

`IRecipeApprovals` records:
- the recipe: `id`, `version`, `hash`;
- who and how: `approver`, `approvedAt`, `via: desk|freeze|ci`, `base?`; for `ci` (CI-6) also `repo`, `sha`, `blob`, `pr` and `mergedBy`, with `approver` `ci:<host>/<owner>/<name>@<sha>`;
- what it pins: `descriptorHashes` and `children { id: hash }`.

`IRecipeStore` keeps the normalized text by hash. Register, approve and revoke write `recipe.registered`,
`recipe.approved` and `recipe.revoked` events. Approvals live in `ledger.db` and travel with a host handover.
`recipe list --stale` lists approved recipes whose pins no longer match the build, or that are `legacy-shape` (registered before input kinds: register the current file at a new version and approve it; an unapproved legacy row shows `legacy-shape` in `recipe list` but not under `--stale`, which lists approved recipes only). Re-approving the **same hash**
appends a new approval row and event with the new pins (a **drift approval**; a revoked hash can't be re-approved
this way, `SND-RECIPE-005`). It re-runs the validator and evals, and its preview shows:
- which blocks changed, with a capability diff;
- the instances parked on that drift, which the same approval resumes, with one notice per recipe.

Each resume is its own system trigger, `recipe.approved:{seq}` on that instance, with caller `service:engine` on behalf of
the approver. It pops only `spec-conflict` drift and child-revoked parks, never a `rounds` or `budget` park, and it
re-sends that instance's commands that were refused for the drift. Challenges live in the server process and die on
restart, so pins can't be recorded from a preview the human didn't see.

**Child pins:** a child is created from the hash its parent pinned. Approving a newer child version, or revoking
the pinned one, marks every parent that pins it as `child-stale` in `recipe list --stale`; re-approving the parent
pins the latest approved child. Creating a child whose pinned hash is revoked is refused, and the parent parks with
`spec-conflict` (child revoked), which re-approving the parent resumes. The revoke wake reaches those parents too.
A changed child is never drift: it only marks the parent `child-stale`.

### 4.2 The challenge (shared by every `protected` command)

1. The CLI asks the server for a challenge for one action, such as `approve workflow.step 1`,
   `revoke workflow.step@…`, `grant 1 T-1/step/1` or `accept T-1/step/1`. The server returns a one-time **challenge**, valid for 5 minutes and bound to the subject (the recipe hash and
   version plus a digest of its pins, bound on every approval so a pin that moves before the answer makes it stale; or the instance address and version, including the
   child's address and version for a forwarded answer), together with the preview. If the subject changes before the answer, the
   challenge is refused.
2. The human types the action text at a TTY, for example `grant-tokens 100k T-1/step/1`. The CLI returns it with
   the challenge.
3. The request's verb and arguments must equal the challenge's action text: `--grant 3` with a `grant 1` challenge is
   refused. The server refuses a request without a valid challenge:
   - `SND-AUTH-002` (API §4.2) when the caller isn't `human` with a terminal, before any challenge is checked. From a
     session, the message names the agent variable or parent process it saw;
   - `SND-RECIPE-009` (recipe challenges) or `SND-WF-013` (workflow challenges) when it has expired. The fix text
     says "run it again".

**Previews:**
- **`approve`** shows:
  - the id, version, hash and base;
  - a diff against the last approved version;
  - the capability uses, with block, transition, capabilities and parameters;
  - the effect kinds and their allowlist status;
  - the pinned children and blocks;
  - the eval result (from P4 on).

  Failing or missing evals refuse the approval with `SND-RECIPE-008`.
- **`revoke`** shows the running instances that will park, the takeovers it will end, and the generated recipes the
  cascade revokes.
- **`grant`** and **`accept`** show the park or escalation, and what will run next.

An architecture test checks that the Avalonia client and the MCP server reference none of these routes.

### 4.3 Rules

| Rule | Checks | When |
|---|---|---|
| `RCP-CAP-001` | A generated recipe's capability uses (block plus `from → to`) and children are a subset of its base's | validate, freeze |
| `RCP-CAP-002` | Capability parameter values equal the base's | validate, freeze |
| `RCP-CAP-003` | The hash is approved and not revoked, and its pins match the build (`SND-RECIPE-003`, `005`, `006`) | every `Commit`, dispatch |
| `RCP-CAP-004` | A hand-written recipe has no `base` | validate |
| `RCP-CAP-005` | For each kept capability use, the guards that **dominate** it (the gates and evidence on every path from `initial` to it, plus its own) are a superset of the base's: each gate with the same `with:` and a threshold at or above the base's, and each evidence entry with the same `at` and `expect` | validate, freeze |
| `RCP-CAP-006` | Each loop, matched by the state it's on, has a cap at or below the base's. Each budget the base sets is present and at or below it | validate, freeze |
| `RCP-CAP-007` | Capability parameters are literals or `{ profile }` only, and are intersected with the profile at run time | validate, run |

The freeze screen shows the diff against the base. Recipe changes are always approval-tier. Timeouts aren't
capability fields (§13).

## 5. The instance on Temporal (rev 8)

This section replaces rev 7's §5.1–§5.8 (the `WorkflowInstance` row, `Commit`, nonces and outbox ids, timers,
the router and inbox, and the frame stack). The **behaviour** those sections required is kept and restated here as
what the interpreter must do; the **mechanism** is Temporal's. Rev 7's text is in the git history of this file
(`git log -p -- docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`, revision 7).

### 5.1 The interpreter

- **One workflow type, `RecipeWorkflow`**, in `Aw.Sindri.Workflows`. Its input is the recipe `(id, version, hash)`,
  the item, the attempt, the input facts and the tenant and ring keys. It loads the normalized recipe through a local
  activity (`LoadRecipe`, from `IRecipeStore`), then interprets it: enter `initial`, run the state's kind, decide
  transitions with the pure `TransitionDecision` from `Aw.Sindri.Core/Workflow` (P3), and repeat until a terminal
  state.
- **Workflow id** is the address: `{item}/{recipe-name}/{attempt}` for a root and `{parent}/{child-recipe}.{entry}` for
  a child (§6.1). `StartInstance` uses `WorkflowIdConflictPolicy.UseExisting`, so an existing `(recipe, item,
  attempt)` returns that execution (W-start semantics). `--attempt next` is refused while an earlier attempt is live
  (`SND-WF-012`, checked against `workflow_claims`).
- **Instance state** is the workflow's in-memory state, rebuilt by replay: `frame { state, entry, run?, child? }`,
  `suspension?` (a park, escalation or takeover with its reason, question and resume action, §5.8), `pendingPark?`,
  `loops`, `spent`, `facts { ref, sha256, headSha }`. Rev 7's two-deep frame stack is these two optional fields.
- **Determinism.** Workflow code calls no I/O, clock or random except through `Workflow.*`. Every read of a store, every
  gate, every effect and every ledger write is an activity. The SDK's analyzer and the replayer test (§10) enforce it.
  *Versioning:* P4b's start now also runs the local activity `ReadSubject` (it names the `Subject` attribute) and has no
  `Workflow.Patched` guard: a workflow started before it would not replay. That is accepted before the cutover, as nothing
  is installed; from the cutover on, a change to what start runs is guarded with `Workflow.Patched`. P4c's start also renders the
  recipe's `name` (§3.11) in the workflow from the per-fact `ReadSubject` markers and `StartedAt`; the no-`Patched` acceptance is unchanged, and from the cutover on a change to `InstanceNames.Render` is guarded with `Workflow.Patched`.
- **Search attributes**, set on start and on every transition: `Item`, `Recipe`, `State`, `NeedsYou` (true while
  parked, escalated or holding an open desk task), `Repo`, `Tenant`, `Ring`, `Urgency`, `Root`, and `Subject` (the subject key of §3.11; never the name). *Rev 8.2:* `Repo` is the `repo` field of
  the instance's `fact:item`, read once on start; an instance without that fact, or whose item names no repo, leaves
  `Repo` unset.
- **Queries.** `GetInstance` (§6.3) is a workflow **Query** returning the state, the rendered `next`, `allowedCommands`,
  the open question or task with the Step's declared outcomes and outputs, the run token and the children.

### 5.2 Commands are Updates

Every write verb in §6.3 that targets an instance (`answer` in all its variants, `complete` from the desk, `cancel`,
`takeover`, `handback`, `command <verb>`) is a Temporal **Update** on the workflow.
- **Order.** The API chain runs first (caller, audit, authorization, validation; API §2.1). The handler then checks
  the recipe's approval and pins (`RCP-CAP-003`) against the registry and sends the Update. The handler does no slow
  work: an Update is one round trip to the local server.
- **Update id = the nonce** from `POST /workflow/instances/{a}/nonce`. Temporal deduplicates: a repeated id returns
  the first outcome, which the CLI prints as `already applied` (exit 0) or as the recorded refusal (exit 2). A
  different command with a used nonce is refused by the nonce check in the API handler (`SND-WF-009`), before any
  Update is sent. Nonces live 10 minutes in the API's own table.
- **Validator.** The Update's `[WorkflowUpdateValidator]` rejects, without touching state or history: a trigger not
  allowed in the current state (`SND-WF-003`), a stale run token (`SND-WF-005`), a malformed report (`SND-WF-008`),
  an answer that doesn't fit the park reason (`SND-WF-003` with the fix naming the right answer). A rejected Update
  records nothing and consumes nothing, which is rev 7's W12 kept.
- **Handler.** The accepted Update applies the trigger: it may run gates (activities), and it writes one ledger event
  per instance it changes (§5.6). Updates serialize per workflow, so there is no version check and no
  `VersionConflict`; `SND-WF-002` is retired.
- **Exempt from the approval check**, as in rev 7: `cancel`, `takeover`, `handback`, `takeoverIdle` and the
  `open-request` trigger (UI E-6). A mismatch on any other human trigger returns `SND-RECIPE-005` or `-006` from the
  API handler and sends nothing.
- **Takeover and `open-request`.** Both are Updates the engine itself sends with caller `service:engine`:
  `open-request` has id `wf:{instance}:open:{launchId}` and is a no-op when already taken over (UI E-6, E-19).

### 5.3 Steps are activities; a desk report is an Update (rev 8.1)

An `active` state starts an activity `RunStep` with a **run token** `{entry}.{n}` in its activity id, so the token
stays a public counter in `status` and in the CLI.
- **Desk runtime (W20; *rev 8.1*, owner decision 2026-10-12).** `RunStep` is a **short** activity: it records the open
  desk task and returns. The workflow then waits for the `complete` **Update** (§5.2), which the validator checks
  synchronously like every other command: a stale run token is `SND-WF-005`, a malformed report `SND-WF-008`, and a
  rejected report records nothing. The human reports with `sindri workflow complete <address> --run <token> --outcome
  <o> --output …`; the API sends the Update. The state's timeout, at least `workflow.deskTimeout` in desk mode, is the
  wait's `Workflow.DelayAsync` (§5.5); expiry always parks with reason `timeout` (W20, §3.8), whatever the state's
  `then:` says (*rev 8.2*; rev 8.1 said `step.failed(timeout)`, which would let an agent-sized timeout fail the
  human's own work). *Why:* finding F1
  (§11): the time-skipping server skips no time while an activity is open, so a desk Step held open as an
  async-completion activity can't have its timeout tested there; with the Update the 4 h timeout fired in 3 ms of wall
  time. Rev 8 stored the task token on `step.started` and completed the activity through it; that is gone. The shipped
  code (P4a) still has rev 8's async-completion desk activity; it is P4's starting point, and **P4 moves it to the
  Update model** (unit (c)).
- **Agent runtime (§6.5).** *Rev 8.2 amendment (provisional owner defaults Q1, Q2): the report is the `complete` Update, validated synchronously like a desk report (a wrong report is refused and the run goes on), and the launcher service's liveness (120 s of silence, `session.ended(lost)`) is what ends a dead session, so the engine watches it by polling instead of routing wrap's beat into Temporal; the worst case is about 125 s, inside 150 s. The text below is rev 8's.* `RunStep` asks `ILauncher` for the launch and returns pending. `session wrap`'s heartbeat
  becomes an **activity heartbeat**; a missed heartbeat timeout (120 s) fails the activity with `lost`, and a wrapped
  exit without a report fails it with `exit`, so a closed tab or a crash ends the run as `step.failed` within 150 s,
  as rev 7 required. The launched session, and only it, completes the activity through `complete --run`, proven by
  the launch secret (`X-Sindri-Launch-Secret`, `SND-AUTH-003`); a taken-over instance invalidates the secret.
  *Rev 8.1:* agent Steps (P8) keep this heartbeating activity, so their timeout tests run on the dev server
  (`StartLocalAsync`, short durations), not on the time-skipping server (F1, §11).
- **Reports** are validated as in rev 7 §5.5: an undeclared outcome, a missing required output or an undeclared fact
  is `SND-WF-008`, rejected before any decision, and uses no round. Output files travel as content (≤ 1 MiB each) and
  are stored `0600` by the API before completion.
- **`Start` after commit** holds by construction: the activity is scheduled only once the transition into the state
  is in history, and a replay never re-launches a run whose activity is already recorded.

### 5.4 Events are signals; the bridge replaces the router

Registered events (`EventDescriptor`, §2) reach instances as Temporal **Signals**.
- **The bridge service** (`workflow.events`, a built-in service with its own loop, S1-D8 and S1-D10) reads
  `ledger.db` from its cursor, accepts an event only when the envelope's actor is the descriptor's `producer`,
  resolves the target workflow id through `causation` (through the causing event's own payload, which names its instance; no separate `workflow_commands` map is kept) or
  through `item` and attempt, and signals it. Deliveries of engine-origin effects signal nothing, so the engine's own
  notification can never satisfy a recipe's wait. A `route: causation` event signals only the frame entry its command
  was written for; the interpreter drops it if that entry has ended.
- **Held item events** (*owner, 2026-10-14*). The bridge re-sends an item-routed event only to instances of that item
  that started at or before the event occurred, and only within 1 minute of its first delivery (the visibility lag of a just-started
  instance). This replaces the rev 8.1 wording that held item events for `workflow.eventHold`.
- **Buffering.** Signals that arrive before the matching wait are buffered by Temporal and consumed when the workflow
  reaches the wait, so rev 7's "early row" hold is native. The interpreter discards a buffered signal that doesn't fit
  the current wait once it is older than `workflow.eventHold` (default 24 h), with a `workflow.expired` ledger event; a
  signal that fits the wait satisfies it, however late it arrived.
- **Signal-with-start** is how a webhook or intake source will start a standing workflow later (README follow-ups).

### 5.5 Timers, sleep, takeover

- Every `active` and `wait` state arms `Workflow.DelayAsync(timeout)` inside a cancellation scope. `then: retry`
  re-arms once in the same entry and re-emits the entering transition's effects (new activity ids, same effect
  identity, §5.7); a second expiry parks with `timeout`. `then: park` and `then: escalate` do what they say.
- **Takeover** cancels the scope, records `remaining` from `Workflow.UtcNow`, and arms the `takeoverIdle` timer (2 h),
  which emits `notify.desk` ("hand back {item}?") once and clears; it never hands back. **Handback** re-arms the state's
  timer with `remaining`. **An answer** to a park re-arms it with the full timeout. Rev 7's `arm` counter is gone: each
  arming is its own timer in history.
- **Downtime** fires expired timers on wake (§0, X58).

### 5.6 The ledger stays the public log

- Every transition, refusal, shift, notification and expiry the interpreter applies writes **one ledger event per
  instance it changes** through a local activity `RecordEvent`, with the rev 7 payloads: trigger `kind`, `id` and
  `actor`; gate, evidence, grant or override data; the effect ids; and the post-state delta. The event kinds are
  unchanged: `workflow.started`, `workflow.transition`, `workflow.refused`, `workflow.shifted`, `workflow.notified`,
  `workflow.expired`, `recipe.*`. A child's `workflow.started` is written by the parent's transition.
- *Rev 8.1 (owner decision 2026-10-12).* `RecordEvent`'s ledger key is `workflow:{workflow id}:{run id}:{activity
  id}`. Every Temporal activity, local or regular, has a deterministic `ActivityInfo.ActivityId`, the same on every
  replay. No activity sees a history event id: `ActivityInfo` has no scheduled-event-id field, so rev 8's `(workflow
  id, run id, history event id)` can't be built. `RecordEvent` stays a **local** activity, so it costs one marker per
  row; a replay reads the marker and never runs it again, and the one case that runs it twice (a worker that dies
  after the append, before the workflow task completes) has the same activity id, so S1's ledger refuses the repeated
  key and records nothing. The key is read inside the activity, not counted by the workflow. Its retry policy is
  unbounded with backoff: the workflow does not proceed until the ledger has the event, which keeps the ledger a
  complete record.
- The ledger writer runs the payload through `Aw.Redaction`. `causation` is the trigger's source; `EventFactory` takes
  the item, the causation and the request's actor (S1-D7, API P1).
- **Temporal event id (UI E-30).** Every mirrored row records the id of the `RecordEvent` activity it was written for
  (not a Temporal history event id; rev 8.2, owner 2026-10-14), in a nullable `temporal_event_id` column on the ledger row (null on every row the engine didn't mirror; documented with
  the ledger columns in `docs/csharp/sindri.md`, "Ledger"). It is the third part of `RecordEvent`'s idempotency key, so
  the column and the key never disagree. The instance is identified by the row's payload (the instance id), so
  `GET /engine/instances/{address}/history` joins a history page's event ids to ledger sequences with one query on
  `(instance, temporal_event_id)` and the UI draws "ledger ↔ 4231 step.round-started" beside the line. The column is
  added in P4, with the mirror (S1's migrations own the ledger schema). *Rev 8.2 (owner decision 2026-10-14).*
  `temporal_event_id` is the `RecordEvent` activity id; the history route joins on `idempotency_key`, which carries the
  run id. On `AlreadyRecorded` with different content (a workflow-task replay), the new event is recorded under
  `<key>:<content hash>` and a warning is logged.
- **Who reads Temporal (R8-12).** Only the engine, its tests and the API's read-only `GET /engine`,
  `GET /engine/instances` and `GET /engine/instances/{address}/history?page=`, which answer from the SDK's describe,
  list and history queries and scrub the payloads through `Aw.Redaction`. The UI reads Temporal's state and history
  **only through those routes**, never from the dev server (which stays loopback-private), and the evolve loop reads
  the ledger. `workflow events` and the item timeline read the ledger (X16).

### 5.7 Effects are activities

- A transition's `effects` run as activities after the transition's ledger event, each after
  `Workflow.DelayAsync(workflow.dispatchDelay)` (default 10 s, the undo window) and a local-activity recheck of the
  recipe's approval and the profile's effect allowlist. A recheck refusal writes `outbox.refused` with its code
  (`SND-RECIPE-005`, `-006`, `SND-WF-011`) and parks the instance with `spec-conflict` of that kind, or absorbs it
  when the instance is already parked for the same revoke.
- **Effect identity** stays `{instance}:{entry}:{effect index}` in the payload, and `effect.claim.ref` keys its
  identity by item through `workflow_claims (item, owner instance, attempt)` in `ledger.db`. Activity retries and
  `retry` re-emissions are harmless because the effect block's existence check uses this identity.
- **Origin decides the exemption, not the kind:** the engine's own `notify.desk` (parks, escalations, desk tasks,
  `takeoverIdle`) is `origin: engine`, exempt from the approval check and the allowlist, and signals nothing. A
  recipe's `effect.notify.desk` is `origin: recipe` and checked like any effect.
- *P7, 2026-10-18 (IDEA-033):* after any ending, `cancelled` included, the engine frees the item's claim in a detached scope: it deletes `refs/sindri/<user>/<item>` and the `workflow_claims` row if this instance still owns the claim (a claim another instance took over is left alone). There is no recipe field. The same seam is where per-run worktree removal joins.
- Kernel's outbox (`outbox_commands`, `IOutbox.MarkRefused`) is no longer used by the engine. The API spec keeps it for
  its own commands if it needs one; S1-D3 and K-D1 drop from this spec's S1 changes (§7.2).

### 5.8 Children, parks and resumes (behaviour kept from rev 7)

- A `child` wait starts a **child workflow** with `ParentClosePolicy.Terminate`, so a cancelled parent cancels its
  children. The child's terminal outcome arrives as the child workflow's result; `parked (propagate)` and `escalated`
  arrive as signals the child sends to its parent, and the parent pushes a mirroring suspension (reason, question,
  `childNext`). Any later signal or result from the child pops that mirror first, so a child cancelled at its own
  address, or accepted straight to `done`, still releases its parent.
- *DT-C, 2026-10-16:* the child's result carries its declared `outputs` present at its end, under its own keys; `cancelled` carries none. The validator (`RCP-WIRE-002`, `-003`), `recipe eval` and the pure engine's `produces` (which applies a rename) are DT-C; the interpreter filling the result's outputs from the child's facts is P7.
- *P7, 2026-10-18:* the child's workflow id is its address, `{parent}/{recipe}.{entry}`, started from the hash the parent's approval pinned. Workflow code can't send an Update, so a parent's ending (a cancel or any other ending) signals each live child (`parentCancel`, origin `parent`); the child records its own `cancelled` and frees its claim, and the parent waits up to 30 s before the parent-close terminate backstops. The parent's view lists its live children and status reads each child's state live (`unreachable` when none answers); the child's end carries its declared outputs on every outcome but `cancelled`.
- **Forwarded answers.** An `answer` to a parent whose suspension mirrors a child is an Update on the **child**: the
  nonce endpoint resolves the forwarding and mints the nonce for the child; the challenge for a `protected` answer is
  bound to the child's address. The CLI prints the child's result and the parent follows through the child's signal.
  `cancel` and `takeover` act on the parent.
- **Refusal precedence** (rev 7 §5.5) is unchanged: a `gate.budget.*` refusal parks with `budget` and releases held
  resources; an uncertain or below-threshold top-rung gate escalates; from `active` the Step re-runs below the loop's
  cap, parks with `rounds` at the cap, or parks with `needs-answer` with no loop; from a wait the event is stored as the
  withheld trigger and the instance parks with `needs-answer` (wait); a command gets `SND-WF-004`.
- **Resume actions** (rev 7 §5.8) are unchanged and are tested by name (§10): `transition` for `rounds` and
  `no-progress`, `rerun` for a Step budget park or `needs-answer` from a Step (a re-run that would pass the loop's cap
  parks `rounds`, kind `refusal`, instead, and `--grant n` is the way on; rev 7.2), `reevaluate` (or `rewait`) for a
  wait refusal or a budget gate refusal, `rearm` for `timeout` and drift, `restart` for `SND-WF-010`, `reemit` for a
  dispatch refusal, and `--accept` or `--reject` for an escalated gate (on a command's escalation, `--reject`, or an
  `--accept` another check refuses, pops the escalation and the command is refused with `SND-WF-004`; rev 7.2). A
  resume runs its gates afresh. `answer --text` on a `no-progress` park works once per loop (§13).
- **A park that arrives while parked or taken over** is recorded as `pendingPark` and takes effect when the current
  suspension pops. A revoke's `spec-conflict` park replaces the suspension, ending any takeover. A drift wake on a
  parked instance is recorded on the suspension and refused (`SND-RECIPE-006`) until re-approved.
- **Revoke and drift wakes** are signals the registry sends to every live instance of the recipe (or of a parent
  pinning the child) on `recipe.revoked` and on startup when the build's descriptor hashes differ from an approval's
  pins. A drift approval signals the instances it lists to resume and re-sends their refused commands (§4.1).

### 5.9 What is removed

The tables `workflow_instances`, `workflow_deadlines`, `workflow_inbox`, `workflow_cursor`, `workflow_gate_results`
and `workflow_commands` are not built; `workflow_nonces` moves to the API (nonces are its concern). `recipes`,
`recipe_approvals` and `workflow_claims` stay in `ledger.db`. Kept in Core from P3: `TransitionDecision`,
`RefusalRules`, `LoopRules`, `BudgetRules`, `ChildPropagation`, `WorkflowErrors`, the eval format. Dropped from P3:
`FrameRules` (now `suspension` and `pendingPark` on the interpreter), `TimerDecision`, `EventFold`, the in-memory
store and `WorkflowStoreContract`, `IWorkflowStore`, `IMonotonicClock`. Rev 7.2's trigger-key table additions
(resource grants, tracker and parent cancels, `start`) and its ids derived from the trigger key are superseded: a
child's workflow id is its address (§5.1), activity ids carry the run token (§5.3), and a resource grant is a signal.

## 6. CLI and API surface

### 6.1 Process model, addressing, dev loop

- `recipe validate` and `recipe eval <file>` run in-process (the binary's catalog, plus the profile when there is
  one). Every other verb is a client of `sindri serve`.
- **Addresses:**
  - `<item>` is an `ItemId` (`ITM-…`, API §3) or one of its tracker-key aliases; examples use `T-3` for short.
  - A root is `<item>/<recipe-name>/<attempt>`.
  - A child adds `/<child-recipe>.<entry>`, for example `T-3/item/1/step.4`. `T-3/item/1/step` is accepted while only
    one live child of that recipe exists.
  - A bare `T-3` means the root of the item's latest attempt. When that is ambiguous, `SND-WF-006` lists the
    candidates.
- **`start`** runs the latest approved version of the id, unless it is given `id@hash`. An existing
  `(recipe, item, attempt)` returns that instance. `--attempt next` starts a new attempt.
- **Files:** the CLI sends file contents for `--fact` and `--output`, at most 1 MiB each, and the server never opens
  a client path. Artifacts are stored owner-only (`0600`).
- **Dev loop:**

  ```
  export AW_SINDRI_HOME="$TMPDIR/sindri-dev-p<N>"     # one fixed path per phase under $TMPDIR (sindri-prove.sh's guard)
  export AW_SINDRI_PORT=0                              # a free port, so parallel phases and the owner's Sindri never clash
  dotnet build src/Sindri/Aw.Sindri.Host               # once; sindri-prove.sh runs the built binary, never the TS `sindri`
  alias sindri='scripts/sindri-prove.sh'               # runs as the agent (a session); owner verbs: `sindri human <verb>`
  sindri stop; rm -rf "$AW_SINDRI_HOME"                # a clean home for every run of the runbook
  ```

  The alias is for a human at the desk; the Prover calls `scripts/sindri-prove.sh` directly and sets the same two
  variables on every command.

  Then, in order: copy `recipes/dev/profile.yaml` to S1's profile location (`docs/csharp/sindri.md`); start
  `sindri serve` in its own terminal and leave it running for the whole phase (owner); and run
  `sindri human profile approve <hash>`, which is an API client of the running server. The verbs go in another terminal.
  The Prover reaches the same server over loopback. Its sandbox needs `127.0.0.1` in the network allowlist, set
  once by the owner; nothing is run outside the sandbox. Each phase that adds a `workflow:` profile key (S1-D11) adds it to the dev
  profile in the same PR, so the dev profile always matches the schema of its phase.

### 6.2 Callers and action classes (W22)

The engine adopts the API surface spec's model unchanged (API §4.1, §4.2, §6). How a caller is classified:

| Channel | Caller |
|---|---|
| The CLI at a terminal (stdin and stdout), with no agent variable set and no agent-harness parent process | `human` + terminal |
| The Sindri app (Avalonia) | `human`, no terminal |
| The CLI with an agent variable or harness parent, or without a terminal; the MCP endpoint; any HTTP call without `X-Sindri-Caller: human` | `session` |
| Engine services, the router, the dispatcher and adapters, in-process only | `service:<name>` |

Action classes of the engine's commands (API §4.1), with the fail-closed `IClassifiedCommand` rule (API §2.1):

| Class | Engine commands |
|---|---|
| `local` | `recipe validate`, `recipe eval` (in-process, never routes) |
| query | `recipe show`, `recipe list`, `workflow status`, `workflow list`, `workflow events`, `outbox list` |
| `pause` | `ApplyCommand:takeover`. A session may take over (the attach hook runs inside the session); handing back is `desk` |
| `desk` | `recipe register`, `StartInstance` (start, including scoping and any other workflow), `ApplyCommand:answer` (text, resume, reject), `ApplyCommand:handback`, `ApplyCommand:cancel`, `ApplyCommand:command-<verb>`, `CompleteStep` |
| `protected` | `recipe approve`, `recipe revoke`, `ApplyCommand:answer-grant`, `ApplyCommand:answer-grant-tokens`, `ApplyCommand:answer-accept`. Each also needs the §4.2 challenge |
| `ci` (CI-7) | `ApproveRecipeCiRequest` (its own request type, no route), within the capability ceiling (CI apply §3.4). In-process as `service:ci-apply` only; no challenge. Revoke, drift re-approval and a recipe past its ceiling stay `protected` |

So the app is a full desk client: it starts workflows (scoping included), answers parks, takes over, hands back,
cancels, completes desk tasks and hands work off to a session (API P10, `OpenItem`). `protected` actions show the
CLI to copy until passkeys exist.

**The agent variables are probed, not guessed.** P2 records what each harness actually sets (Claude Code, Codex in
each sandbox mode, Cursor's agent and its integrated terminal) and pins the result in a golden test. That list is the
one API §6 reads. The probe records the whole process ancestry, not just names: Claude Code can run as `node`, and
Cursor's app hosts both the human's terminal and its agent, so the parent-process check matches the ancestry pattern
the probe pinned. A variable that humans also see is excluded. `sindri doctor` re-probes Claude Code, and **warns**
(never `ok`) when the Codex or Cursor version differs from the pinned probe; those probes are **(owner)** steps in P2,
run with `sindri-prove.sh doctor --probe` inside each harness. A refusal (`SND-AUTH-002`) names the variable or
process it found, so a human in an IDE terminal knows why. The table is honest-mistake grade (A 17.28.3).

### 6.3 Verbs

| Verb | Class | Prints |
|---|---|---|
| `recipe validate <file> [--json]` | local | Rule lines, then the hash |
| `recipe eval <file> [--json]` · `--scaffold <file>` · `--record <address> --out <file>` | local · local · query | Case results · a generated happy-path eval: from the initial state, the first declared outcome whose transition the engine applies into a state not yet visited, until a terminal state or one nothing moves on from (an answer wait is answered with a `text` line) · an instance's history as an eval |
| `recipe register <file>` | desk | `registered workflow.step@9ab2d40c1e77 (unapproved)` |
| `recipe show <id@hash>` | query | The preview, which now also prints `urgency`, `inputs` (key, type, kind), `outputs` (key, type, `from`) and the path order of the states (`states[].order` and `loops`, §3.10); `--json` returns them as fields |
| `recipe approve <file\|id@hash>` | protected | The preview, the challenge, then `approved …` |
| `recipe revoke <id@hash>` | protected | The preview, the challenge, then `revoked …; N instances parked` |
| `recipe list [--stale] [--json]` | query | One line per recipe, or `no recipes registered` |
| `workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]…` | desk | The status header. A finished attempt adds `start a new one with --attempt next` |
| `workflow status <address> [--json]` | query | §6.4 |
| `workflow list [--needs-me \| --item \| --state] [--json]` | query | One status header per line. `--needs-me` reads API's `GET /needs-you` (one list for every surface) and shows two sections, **Decisions** (parked, escalated, idle takeovers) and then **Tasks** (open desk tasks). Empty: `nothing needs you` |
| `workflow answer <address> [--text …\|--file …] [--grant n \| --grant-tokens n \| --accept \| --reject \| --resume]` | desk (grants, accept: protected) | The new status header and the resume action it ran |
| `workflow command <address> <verb>` | desk | Fires a recipe `command.<verb>` trigger. Verbs match `[a-z][a-z-]{1,31}`. Reserved (`RCP-TRIG-002`): `answer`, `accept`, `reject`, `resume`, `grant`, `grant-tokens`, `cancel`, `takeover`, `handback`, `complete`, `start`, `approve`, `revoke`, `override`, `freeze`, `register`, `command` |
| `workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]…` | desk | The new status header and what happened (`-> checks`, `refused: re-running produce (round 2/3)`, `parked: rounds`) |
| `workflow cancel <address>` · `takeover` · `handback` | desk · pause · desk | The new status header. `sindri handback <item>` stays as D §9.3's short form |
| `workflow events <address> [--follow] [--json]` | query | One line per event: the instance-level filter of the item timeline (API `GET /items/{itemId}/timeline`) |
| `outbox list [--pending \| --dispatched] [--json]` | query | One line per command |

- Every write verb takes `--nonce <n>`, for scripts and tests. Without it, the CLI asks the server for one and
  prints it with `--verbose`. `--json` output always includes the nonce it used.
- `GET /workflow/instances/{instanceId}` (routes below) returns the instance, the rendered `next`, `allowedCommands`
  (each with its action class), the open question or task (with its input facts), the run token, the children, and what `--accept`
  would fire.
- **Notifications** go out as `notify.desk` outbox commands, each written to `$AW_SINDRI_HOME/notifications.jsonl` and shown
  as a macOS notification when one is available:
  - parks, escalations and `takeoverIdle` interrupt, within D §10.1's budget;
  - an instance's **first** desk task interrupts once, so starting `workflow.item` gives a cue;
  - later desk tasks are non-interrupting `info`, collected into a daily digest.
- `notify.desk` is the **only** source of OS notifications. The app shows the needs-you badge and pulse from the
  event stream and never notifies on its own, before or after P6 (UI spec X5), so nothing fires twice (API §5.1).
- **`--needs-me`** lists a parent and its mirrored child once, under the child.
- **Empty states:** `nothing needs you` (`--needs-me`), `no instances match` (other filters), `no events`, `no
  commands`, and `no evals declared` (eval).

**Routes** (under S1's `/sindri` group; each is one slice through API's chain; commands and issuing routes have a catalog row, queries don't (API ARCH-CQRS-003; UI spec rev 7 amendment, 2026-10-12)):

| Route | Slice | Class |
|---|---|---|
| `GET /workflow/recipes` · `GET /workflow/recipes/{id}@{hash}` | `ListRecipes` (with each recipe's `inputs` incl. `kind`, `outputs`, `urgency`, and `states[]` with `order`, `branchOf` and `loops[]` per §3.10, for the app's start form and rendering) · `ShowRecipe` (the preview, with the same fields) | query |
| `GET /engine` · `GET /engine/instances` · `GET /engine/instances/{address}/history?page=` | `GetEngine` · `ListEngineInstances` · `GetEngineHistory` (UI E-30; the API spec owns their shapes; history lines carry the ledger sequence from `temporal_event_id`, §5.6) | query |
| `POST /workflow/recipes` | `RegisterRecipe` (body: the recipe text) | desk |
| `POST /workflow/recipes/{id}@{hash}/approve` · `/revoke` | `ApproveRecipe` · `RevokeRecipe` (body: the challenge and its typed text) | protected |
| `POST /workflow/challenges` | `IssueChallenge` (body: the action and its subject) | issue |
| `GET /workflow/instances?address=&item=&state=&needsMe=` | `ListInstances`; `address` resolves a CLI address (`T-3/item/1`) to an instance id | query |
| `POST /workflow/instances` | `StartInstance` (body: `recipe`, `item`, `attempt?`, `facts { key: { name, contentBase64 } }`, each ≤ 1 MiB) | desk |
| `GET /workflow/instances/{instanceId}` | `GetInstance`: the instance, rendered `next`, `allowedCommands`, the open question or desk task **with the Step's declared outcomes and outputs**, children | query |
| `GET /workflow/instances/{instanceId}/events?after=` | `GetInstanceEvents` (the item timeline filtered to the instance) | query |
| `POST /workflow/instances/{instanceId}/nonce` | `IssueNonce` | issue |
| `POST /workflow/instances/{instanceId}/commands` | `ApplyCommand` (body: `verb`, `nonce`, `text?`, `fileBase64?`, `grant?`, `grantTokens?`, `accept?`, `reject?`, `resume?`, `challenge?`) | by variant (§6.2) |
| `POST /workflow/instances/{instanceId}/runs/{run}/complete` | `CompleteStep` (body: `outcome`, `outputs { fact: contentBase64 }`, `nonce`) | desk; `step-report` with `X-Sindri-Launch-Secret` |
| `GET /outbox?state=` | `ListOutbox` | query |

Instance ids are opaque (`WFI-…`, a typed id); the CLI's `/`-separated addresses travel only as the `address` query.

### 6.4 `status` layouts

The header is always
`<address>  <recipe>@<hash12>  v<n>  <state>[: <detail>]  since <hh:mm> (<age>)`. Then come labelled lines; the
order is fixed and empty lines are left out. When the recipe has `name`, the first labelled line is
`name      <rendered name>` (the CLI reads it from `subject.label`; `status.header` has no name field). The `inputs` line marks a generated value `(generated)`.

```
T-3/item/1  workflow.item@3f1c09a2b4d1  v7  parked: no-progress (child)  since 11:54 (12m)
  next      Parked (no-progress) in T-3/item/1/step.4: checks failed twice with the same findings.
            Grant more rounds at a terminal, or cancel.
  child     T-3/item/1/step.4  parked: no-progress
  answer    sindri workflow answer T-3/item/1 --grant 1      (at a terminal; forwarded to the child)
  commands  answer (forwarded), takeover, cancel
```

```
T-3/item/1/step.4  workflow.step@9ab2d40c1e77  v4  produce: desk task  since 10:42 (1h 12m)
  next      Produce the Step output for T-3 (round 1/3), then run:
            sindri workflow complete T-3/item/1/step.4 --run 1.1 --outcome done --output fact:output=<file>
  inputs    fact:spec  spec.md  sha256:1c0e…
  timeout   parks in 1d 22h 48m (desk)
  budget    tokens 0 / 200k (state), 0 / 400k (instance)
  commands  complete, takeover, cancel
```

```
T-1/step/1  workflow.step@9ab2d40c1e77  v9  parked: budget  since 11:54 (3m)
  next      Parked (budget): produce used 200k of 200k tokens.
  answer    sindri workflow answer T-1/step/1 --grant-tokens 100k     (at a terminal; once per park)
  commands  answer, takeover, cancel
```

```
T-6/step/1  workflow.step@9ab2d40c1e77  v5  escalated: gate  since 09:10 (2h)
  next      Escalated: gate.verify.judge scored 0.62, below 0.8. Accept to override it (the other checks still run
            before done), or reject (the Step re-runs within its loop, otherwise this parks).
  answer    sindri workflow answer T-6/step/1 --accept     (at a terminal)
  commands  answer, takeover, cancel
```

```
T-2/dev-timeout/1  workflow.dev-timeout@77aa01c3d9e2  v3  taken-over  since 12:01 (5m)
  next      With you since 12:01. Run `sindri workflow handback T-2/dev-timeout/1` when you're done.
  paused    timeout 40s left
  commands  handback, cancel
```

```
T-3/item/1  workflow.item@3f1c09a2b4d1  v12  done: resolved  since 14:20 (1m)
```

The answer line depends on the park reason:
- `timeout` and `SND-WF-010` suggest `--resume`;
- `spec-conflict` names its kind and next step (§3.8);
- `rounds` and `no-progress` suggest `--grant 1`;
- `budget` suggests `--grant-tokens`;
- `needs-answer` from a Step suggests `--text`; from a wait it suggests `--resume [--text]`;
- a drift recorded on a parked frame adds a `drift` line naming the block, and says the answer needs the
  re-approval first;
- a park mirroring a child names the child.

Durations are shown as `1d 22h 48m`, or `40s` under a minute. `since` adds the date (`since 10-09 11:54`) once the
age is a day or more. Plain output is ASCII (the exception is the `name` line: it renders the recipe's name template, and the values it names, such as a scope text, may be non-ASCII), and `commands` always lists the instance's own verbs first, in the
order `answer`, `complete`, `handback`, `takeover`, `cancel`. Golden files pin every layout in §6.4, plus one per
§3.8 row.

### 6.5 The agent runtime (owner decision, 2026-10-11)

The runtime that runs a Step in an agent session and takes its result back, so the factory line can run unattended.
It uses API §8.1's launch contract; nothing here launches anything itself.
- **Launch.** For a Step whose descriptor names an agent runtime, the post-commit loop asks `ILauncher` for a
  launch (rev 8: a tmux session, with no Warp tab on the always-on `--headless` box; see "tmux owns the session" below), with only ids: the instance, the run token, the repo,
  and a fresh per-run worktree (`<repo>/.claude/worktrees/sindri-<instance>-<run>`). The target and its permission
  mode come from the profile (`workflow.agent.target`: an agent id; *rev 8.2, provisional owner default Q8: the `workflow.agent.permissionMode` key is dropped, because each Step's policy sets the mode*).
  The session pulls its injection plan, context and packet through the MCP endpoint. `run.runtime` is `agent` and
  `run.launch` names the launch.
- **Report.** The launched session, and only it, reports with `sindri workflow complete --run <token>`. The CLI reads
  `AW_SINDRI_LAUNCH_SECRET` from its environment (set by `session wrap`) and sends it as `X-Sindri-Launch-Secret`;
  the server accepts `step-report` only when the secret matches the open run's launch and the instance isn't taken
  over (`SND-AUTH-003` otherwise). The run token stays a public counter. The report goes through the same validation
  as a desk report (§5.5). The session reports; the engine decides through the recipe's gates.
- **Tokens.** Usage is read by the target's usage reader when the session ends (API §8.1) and recorded on
  `session.ended`; budgets (§3.7) count it, and an over-budget run parks with `budget`. Sessions never report their
  own usage.
- **Ends.** `session.ended` without a report ends the run as `step.failed` (reason `exit` or `lost`), at most 150 s
  after a crash. The Step's `timeout` still applies. A human may take the instance over at any time (`OpenItem`), which
  invalidates the launch secret for `step-report`.
- **Desk Steps stay.** A `deskCapable` Step whose descriptor names no agent runtime keeps running on the desk runtime
  (W20).
- **tmux owns the session (rev 8, X55).** `ILauncher` starts every agent Step's CLI **interactively** inside a tmux
  session named `snd-<run>` (host tmux now; the container's tmux later, D §8.2), with the seed as the initial prompt
  and `AW_SINDRI_LAUNCH_SECRET` in its environment through `session wrap`. The `process` target kind of API §8.1 is
  retired: unattended and attended runs are the same tmux session, so "attach" (`sindri open <item> --attach`, or the
  app's button) opens a Warp tab running `tmux attach -t snd-<run>` and the human types into the live agent. The
  `UserPromptSubmit` hook inside the session classifies that turn as a `takeover` (D §9.3), which pauses the instance
  (§5.5) and invalidates the launch secret for `step-report` until `handback`. Non-interactive runs (`claude -p`,
  `codex exec`) are never used for a Step, because they cannot be joined. `tmux capture-pane` gives the UI an unscrubbed,
  read-only, human-only preview (owner, 2026-10-17; the collector spec's CA-9).

**Amended by the UI app design spec (approved):** its E-6 adds the `open-request` trigger (id and key
`wf:{instance}:open:{launchId}`, applied in `Commit` wherever `takeover` is allowed, no nonce, exempt from the approval
check like `takeover`, caller `service:engine`, in P5), E-13 makes each `allowedCommands` entry `{ actionId, cli }`
(P4), E-19 starts an instance taken over while a launch is pending (P5), and E-9 gives `CompleteStep` its catalog row
(P4). The UI spec's §18.1 says where each lands; where it differs from this text, it wins (README X52).

## 7. Placement and S1 changes

### 7.1 Placement

| Layer | Holds |
|---|---|
| `Aw.Sindri.Core/Workflow` | Recipe records and typed ids; descriptors, `BlockCatalog`, `IDescribedBlock`, the kind interfaces; `RecipeHash`; `RecipeValidator` with `ValidationContext` (as data); `TransitionDecision`, `RefusalRules`, `FrameRules`, `LoopRules`, `BudgetRules`, `TimerDecision`, `ChildPropagation`, `EventFold`; the ports `IWorkflowStore`, `IRecipeStore`, `IRecipeApprovals`, `IMonotonicClock`; `WorkflowErrors` (a second catalogue class beside S1's `SindriErrors`, validated together) |
| `Aw.Sindri.Features` | Slices: `ValidateRecipe`, `EvalRecipe`, `RegisterRecipe`, `ShowRecipe`, `ApproveRecipe`, `RevokeRecipe`, `ListRecipes`, `IssueChallenge`, `StartInstance`, `ApplyCommand`, `CompleteStep`, `IssueNonce`, `GetInstance`, `ListInstances`, `TickTimers`, `RouteEvents`, `DrainInbox`, `RunPostCommit`. Also `BlockRuntime`, the desk runtime and the pure blocks. Each service takes at most 5 dependencies; validation and telemetry are decorators |
| `Aw.Sindri.Infrastructure` | `YamlRecipeReader`; the SQLite stores and migrations for `recipes`, `recipe_approvals` and `workflow_claims` (*rev 8:* the instance, deadline, inbox, cursor, gate-result and command tables are not built, §5.9; nonces are the API's); the I/O blocks |
| `Aw.Sindri.Workflows` (*rev 8*) | The Temporal adapter: `RecipeWorkflow`, the activities (`RunStep`, `RecordEvent`, `LoadRecipe`, the effect and gate activities), the worker host, the bridge service's signal sender, search attributes. Infrastructure tier, `AwAotCompatible=false` (temporalio/sdk-dotnet#286); references `Aw.Sindri.Core` and `Features` only |
| `Aw.Sindri.Host` | The `recipe`, `workflow` and `outbox` verb groups, each with its own group command table; endpoints; the agent-variable probe; JSON context entries. The command classes, the endpoints and the group tables are written by hand, following the slice recipe. Slices are scaffolded with `/new-slice --slice-only` (API P0), then wired at the anchors API P1 adds |

### 7.2 S1 changes and their owners (W26)

| # | Change | Found on S1's branch | Owner |
|---|---|---|---|
| S1-D1 | S1 merged | in progress | P2 and later start after it |
| S1-D2 | ~~A ledger insert inside an outer `SqliteUnitOfWork`~~ *rev 8: dropped; `RecordEvent` is its own idempotent append* | — | — |
| S1-D3 | ~~The outbox dispatcher~~ *rev 8: dropped; effects are activities (§5.7). The API spec owns a dispatcher if its own commands need one* | — | — |
| S1-D5 | The server challenge. P2 builds it for the `recipe` and `workflow` verbs, and it replaces `profile approve`'s `confirmed` body flag too, so every `protected` action confirms the same way (API §3.2) | `profile approve` trusts a `Confirmed` flag | P2 |
| S1-D6 | An item or instance filter on `/sindri/events` | no filter | P4 |
| S1-D7 | `EventFactory` takes item, causation and the request's actor (not the process-wide `EventScope` actor) | `Item: null`, one process-wide actor | API P1 (its C6); P4 uses it |
| S1-D8 | Built-in services without a profile entry | only profile-named services run | P4 |
| S1-D9 | Pin the outbox migration at version 3: list it explicitly after S1's two files, so Sindri's later files are 4 and up (*rev 8:* the engine adds only P2's `recipes` and `recipe_approvals` and P6's `workflow_claims`) | appended as `own.Count + 1`, so the next migration would renumber it | P2 (the first phase with a migration) |
| S1-D10 | One loop per service | services run one after another | P4 |
| S1-D4 | Cadence below a minute | Met: `Cadence` accepts `5s`–`24h` | — |
| K-D1 | ~~Kernel `IOutbox.MarkRefused`~~ *rev 8: dropped with S1-D3* | — | — |
| S1-D11 | Profile keys under `workflow:`: `deskTimeout` and `eventHold` (P4), `effects` and `dispatchDelay` (P6), `route` and `budget.tokens` (P7, §12). Until `workflow.effects` exists, `RCP-EFFECT-001` reports `skipped`, and effects are refused at run time | S1's profile schema is closed | P4, P6, P7 |

Outside S1:
- `ILauncher` belongs to the API surface spec (P10, `OpenItem`), with its launch targets (Warp plus Claude Code, Codex
  or Cursor) set by the UI design spec. The agent runtime (§6.5) runs Steps that name one; the desk runtime (W20) keeps the rest.
- The API chain (API P1) and the challenge are prerequisites of P2: P2 starts once API P1 has merged.
- The router and slot stubs come in P7.
- Judge (#156) and Evidence (#158) become blocks with the worker recipes.

## 8. Error catalogue (in `docs/csharp/sindri.md`, class `WorkflowErrors`)

| Code | Kind | Meaning | Fix |
|---|---|---|---|
| SND-RECIPE-001 | invalid | Validation failed, or the recipe was registered before input kinds (a start or a first approval of it) | Fix each `RCP-*` line; for a legacy row, register the current file at a new version and approve it |
| SND-RECIPE-002 | notFound | No recipe with that id or hash | `sindri recipe list` |
| SND-RECIPE-003 | conflict | Not approved | `sindri recipe approve` |
| SND-RECIPE-004 | — | Retired: a caller without a terminal gets API's `SND-AUTH-002` | — |
| SND-RECIPE-005 | conflict | Revoked | Approve a new version |
| SND-RECIPE-006 | conflict | A pinned block changed since approval | Re-approve (`recipe list --stale`) |
| SND-RECIPE-007 | conflict | Same id and version, other content | Raise `version` |
| SND-RECIPE-008 | conflict | Evals missing or failing | `sindri recipe eval <file>` |
| SND-RECIPE-009 | conflict | A recipe challenge expired, or its subject changed since the preview | Run it again and re-read the preview |
| SND-RECIPE-010 | notFound | The recipe file can't be read | Check the path |
| SND-WF-001 | notFound | No such instance | `sindri workflow list` |
| SND-WF-002 | — | Retired (rev 8): Updates serialize per workflow, so there is no version conflict | — |
| SND-WF-003 | conflict | The trigger isn't allowed here | `allowedCommands` in `status` |
| SND-WF-004 | conflict | A gate refused | Act on the reasons |
| SND-WF-005 | conflict | A stale run token | Use the token in `status` |
| SND-WF-006 | invalid | An ambiguous address | Use a listed address |
| SND-WF-007 | — | Retired: authorization is API's `SND-AUTH-002`, which names the agent variable or process found | — |
| SND-WF-008 | invalid | A malformed desk report | Use a declared outcome and the required outputs |
| SND-WF-009 | conflict | The command's nonce was used, expired or is for another version | Run the command again (the CLI fetches a new nonce) |
| SND-WF-010 | unavailable | No runtime for a named block or resource | Cancel, or a build with it |
| SND-WF-011 | forbidden | The effect kind isn't on the profile's effect allowlist | Add it under `workflow.effects` and approve the profile |
| SND-WF-012 | conflict | An earlier attempt of this item is still live | Finish or cancel it first |
| SND-WF-013 | conflict | A workflow challenge expired, or its subject changed | Run it again and re-read the preview |
| SND-WF-014 | invalid | `--item` and `fact:item`'s `id` differ | Use the same id in both |
| SND-WF-015 | unavailable | An effect couldn't be sent (an undeclared reply outcome, or every retry failed) | Fix the block or its service, then resume the dispatch park |
| SND-WF-016 | unavailable | A Step input has a descriptor but no runtime in this build: refused at `recipe approve` and `workflow start`; if the runtime is gone after approval, the instance parks (`NoRuntime`) and the question names this code | Install the block pack or rebuild, then restart |
| SND-WF-017 | conflict | The text a pull (`workflow injection`) refilled differs from what the run recorded; nothing is served | Re-run the Step |
| SND-WF-018 | conflict | A Step input, re-run on a cache miss, differs from what the run recorded; the Step does not run on different data | Re-run the Step |
| SND-WF-019 | invalid | A Step input filled an invalid plan: a bad section name, a section or typed result over 256 KiB, or a plan over 1 MiB; the Step fails | Fix the input block |

## 9. Validator and eval format

### 9.1 Rules mapped to §17.28.5

Line format: `RCP-…: <what> at <file>:<line>:<col> (<path>); <fix>`. Each rule has an invalid fixture.
In-process validation resolves a `child` recipe from the registered recipes when served, or from the same
directory when not (so `workflow.item.yaml` finds `workflow.step.yaml`).

| §17.28.5 | Rules |
|---|---|
| Schema | `RCP-SCHEMA-001`–`003`, `RCP-STATE-001`, `RCP-INIT-001`, `RCP-DUR-001`, `RCP-NAME-001` |
| Blocks registered | `RCP-BLOCK-001`–`003`, `RCP-EVID-001`, `RCP-EVENT-001` |
| Inputs upstream | `RCP-WIRE-001`, `-002`, `-003` *(DT-C)*, `RCP-WHEN-001`, `RCP-OUTCOME-001`, `RCP-CHILD-001` |
| Capability | `RCP-CAP-001`–`007` |
| Reachable | `RCP-REACH-001` |
| A way out | `RCP-EXIT-001` |
| Timeouts | `RCP-TIME-001` |
| Loop caps | `RCP-LOOP-001`–`003` |
| Allowlisted effects | `RCP-EFFECT-001` (`skipped` without the profile key) |
| Hash | `RCP-HASH-001` |
| Added | `RCP-TRIG-001`, `RCP-WHEN-002`, `RCP-THRESH-001`, `-002`, `RCP-NEXT-001`, `-002`, `RCP-CHILD-002` (cycle), `-003` (depth > 4), `RCP-VERSION-001`, `-002`, `RCP-TRIG-002` (reserved verb), `RCP-TIME-002` (`retry` on a child wait), `RCP-RES-001` (information: a resource with no allocator in this build) |
| Added (UI E-21, E-22) | `RCP-URG-001` (`urgency` is not `incident`, `triage` or `work`) · `RCP-OUT-001` (`outputs[].from` names no state of the recipe) · `RCP-OUT-002` (`outputs[].from` names a terminal or engine-owned state, or a state whose Step descriptor and transitions never produce `key`; *DT-C, 2026-10-16:* when the fact only arrives through a child it is an error against the resolved child's declared `outputs`, not information) · `RCP-OUT-003` (duplicate output `key`, or an output `key` that is also an input `key`) · `RCP-TYPE-001` (a fact `type`, on an input or an output, is not one of the nine in §3.3) · `RCP-TYPE-002` (an input `kind` is not one of the six in §3.3) · `RCP-TYPE-003` (a `kind` and `type` that don't pair, §3.3) |
| Added (P4c) | `RCP-INPUT-001` (an input declares no `kind`) · `RCP-INPUT-002` (the recipe declares no input) · `RCP-NAME-002` (`name` or `generate` is empty, only whitespace, or longer than 200 characters) · `RCP-NAME-003` (an unknown placeholder, or an unbalanced `{` or `}`) · `RCP-NAME-004` (`{fact:x}` in `name` names no declared input) · `RCP-NAME-005` (a `{fact:…}` placeholder in `generate`) · `RCP-NAME-006` (`generate` on an input whose kind isn't `text`) |

Error lines for the P4c rules, in this section's format (`RCP-NAME-001` is taken by state names, §3.4, so the template rules start at `002`):

```
RCP-INPUT-001: input 'fact:spec' declares no kind at workflow.step.yaml:4:9 (inputs[0]); fix: add kind: item | link | linear-project | webhook | file | text | packet | payload, paired with its type (item/item, file/file, text/text, link/link, packet/packet; payload, linear-project and webhook pair with payload)
RCP-INPUT-002: the recipe declares no input at workflow.x.yaml:1:1 (inputs); fix: declare at least one input; a scheduled recipe gives it generate: "<template>"
RCP-NAME-002: the name template is empty or longer than 200 characters at …:5:7 (name); fix: write literal text with {fact:<input>} or {start:date|time}
RCP-NAME-003: unknown placeholder '{owner}' at …:5:7 (name); fix: use {fact:<input>}, {start:date} or {start:time}, and write {{ or }} for a literal brace
RCP-NAME-004: '{fact:scope}' names no declared input at …:5:7 (name); fix: declare fact:scope under inputs or remove the placeholder
RCP-NAME-005: generate may not read inputs ('{fact:item}') at …:7:9 (inputs[0].generate); fix: use only literals, {start:date} and {start:time}
RCP-NAME-006: generate on an input of kind 'item' at …:7:9 (inputs[0].generate); fix: only a text input can be generated; supply this input at start
```

### 9.2 Eval format

`evals/<recipe id>/<case>.jsonl`:
- line 1: `{ "start": { "inputs": { … } } }`;
- each later line: `{ "trigger": { "kind", "outcome"?, "outputs"?, "park"?, "variant"?, "amount"? }, "gates"?: { "<block>":
  "pass|fail|uncertain" }, "expect": { "state", "park"?, "outcome"? } }`.

The owner extended the format on 2026-10-11 (rev 7.2):
- **Child lines.** A child's park, resume and escalation are `{"kind":"child.parked","park":"<reason>"}`,
  `{"kind":"child.resumed"}` and `{"kind":"child.escalated"}`. The reason is spelled as `expect.park` spells it,
  because a park reason isn't an outcome (§3.6). A child's end stays `wait.satisfied` with its outcome (§5.8). *DT-C, 2026-10-16:* it may give `outputs`, only the child's declared ones, under the child's own keys (not the parent's renamed ones); none on `cancelled`.
- **Engine triggers.** `variant` says which one a line stands for: an answer's option (`grant`, `grant-tokens`,
  `accept`, `reject`, `resume`, `text`) with `kind: "answer"`, or `takeover`, `handback`, `cancel`, `deadline` and
  `command`, which go with the kind of the same name (`command.<verb>` for a command) and may be left out there.
  `amount` is the n of `--grant n` or `--grant-tokens n`. A `text` answer's text is its `outputs` `fact:answer`. A
  `deadline` line fires the current state's next running timer (rev 8: on the time-skipping server, a time skip to it).
  So grants, timeouts and takeovers are written as evals, like any other trigger.
- **Recorded content.** `--record` takes each line's outputs from the facts its trigger reported, as the interpreter
  saved them, and their content from the stored facts, never from the events: an event holds references, and its
  delta leaves out a fact reported again unchanged.

`recipe eval` folds the decisions over the triggers and reports the first line that diverges, with the eval file, the
line, and what was expected and what happened (`happy fail at evals/workflow.step/happy.jsonl:3: expected checks, got
verify`). A line the engine rejects, or one out of this format, fails at that line. `--scaffold`
generates a happy-path case for a new recipe, so the first eval isn't hand-written. It marks line 1 with
`"scaffold": true`, and the approve preview marks each scaffolded eval, and says `scaffold only` when every eval is one. `--record` writes a live
instance's history in this format.

## 10. Engine properties mapped to tests

*Rev 8:* the first three rows (replay, idempotent triggers, stale epoch or version) and the frame, timer-during-takeover
and "events wait for their instance" rows are now Temporal's guarantees. They stay as tests, but as **integration**
tests on the time-skipping server that exercise the interpreter (kill the worker mid-Step and resume; send the same
Update id twice; a signal before its wait), not as unit tests of Sindri code. `Commit_refuses_a_stale_epoch` /
`_version` and `Failed_outbox_write_rolls_back_the_transition` are retired. Every behaviour row (refusals, grants,
resume actions, children, callers, pending parks, drift) is unchanged and runs against the pure decisions in the unit
lane and the interpreter in the integration lane.


| Property | Test | Lane | Phase |
|---|---|---|---|
| Replay reproduces the instance | `Folding_the_events_rebuilds_the_row` (on every store contract run), `Eval_cases_reach_their_expected_states` | unit, integration | 3, 4 |
| A trigger applied twice is a no-op | `Same_trigger_twice_records_one_event`, `Used_nonce_returns_already_applied` | unit, integration | 3, 4 |
| A stale epoch or version is refused | `Commit_refuses_a_stale_epoch` / `_version` | unit, integration | 4 |
| A stale run token changes nothing | `Late_result_from_an_earlier_entry_is_dropped` | unit | 3 |
| Frames survive push and pop | `Takeover_of_a_parked_instance_returns_to_the_park`, `Pop_keeps_entry_run_child_and_inbox`, `Rearmed_deadline_has_a_new_arm` | unit | 3 |
| A timer during a takeover does nothing | `Deadline_during_takeover_is_refused`, `Tick_skips_paused_deadlines` | unit, integration | 5 |
| Child propagation (§5.7) | `Child_<change>_<parent effect>`, `Forwarded_grant_keeps_the_terminal_actor` | unit, integration | 3, 7 |
| A refusal is never a dead end | `Refused_step_reruns_within_the_loop`, `Refused_wait_parks_needs_answer`, `Budget_refusal_parks_budget` | unit | 3 |
| Fail closed | `Unavailable_gate_refuses`, `Step_without_runtime_and_not_desk_capable_parks` | unit | 3 |
| Desk reports | `Malformed_report_is_rejected_without_a_round`, `Desk_timeout_never_retries` | unit | 4 |
| Grants | `Grant_adds_n_rounds_and_needs_a_terminal`, `Token_grant_once_per_park` | unit | 3 |
| Timers fire on wake, and a paused timer keeps its remainder (rev 8, X58) | `Expired_timer_fires_on_resume`, `Handback_rearms_remaining` (time-skipping server) | integration | 5 |
| Events wait for their instance | `Event_for_a_parked_instance_applies_after_the_answer`, `Early_event_applies_on_entering_the_wait`, `Held_event_expires` | unit, integration | 4 |
| Revoke and drift take effect at once | `Revoke_parks_running_instances`, `Pin_drift_parks_on_next_commit`, `Revoked_effect_is_refused_at_dispatch` | integration | 2, 6 |
| One transaction | `Failed_outbox_write_rolls_back_the_transition` | integration | 4, 6 |
| Callers and classes | `Session_cannot_complete_grant_or_accept` (`SND-AUTH-002`), `Session_may_take_over_but_not_hand_back`, `App_runs_desk_but_not_protected`, `Every_engine_variant_has_a_catalog_row`, `Agent_variables_match_the_probe` (golden) | unit | 2, 4 |
| Every answer does something | `Answer_<reason>_runs_<resume action>` for each §5.8 row | unit | 3 |
| Exits work under a revoke | `Cancel_is_exempt_from_the_approval_check`, `Parent_cancel_cancels_a_drifted_child` | unit, integration | 3, 4 |
| Outbox ids are unique per commit | `Two_parks_in_one_entry_get_distinct_command_ids` | unit | 6 |
| Live frames keep their rows | `Child_result_survives_a_long_parent_park` | integration | 7 |
| Resumes do what they say | `Grant_performs_the_withheld_loop_transition`, `Resume_reevaluates_gates_under_the_answer_key`, `Resource_wait_resume_rewaits` | unit | 3 |
| A child that ends releases its parent | `Child_cancelled_at_its_own_address_releases_the_parent`, `Child_accepted_to_done_releases_the_parent` | unit | 3, 7 |
| A bad request never burns a key | `Stale_token_report_records_nothing`, `Malformed_report_records_nothing` | unit | 4 |
| Claims across attempts | `Next_attempt_refused_while_earlier_is_live`, `Claim_owned_by_ended_attempt_reads_created` | integration | 6, 7 |
| Notifications can't satisfy waits | `Engine_notification_delivery_routes_by_causation_only`, `Engine_origin_delivery_writes_no_inbox_row` | unit | 6 |
| Grants never skip a gate | `Grant_on_refusal_kind_park_reruns_the_step` | unit | 3 |
| Drift recovery | `Drift_approval_resumes_listed_instances_and_resends_refused_commands`, `Batch_resume_pops_only_drift_parks`, `Child_revoked_parks_parent_resumed_by_parent_approval`, `New_child_version_marks_parent_child_stale` | integration | 2, 4 |
| Pending parks | `Park_on_a_parked_instance_waits_as_pending` | unit | 3 |
| Pass-4 security fixes | `Engine_origin_limited_to_notify_desk`, `Drift_challenge_binds_pins_digest`, `Forwarded_answer_must_match_challenge_action`, `Human_trigger_on_revoked_recipe_gets_005_and_records_nothing`, `Ui_is_refused_on_every_write_route` | unit | 2, 4, 6, 7 |

## 11. Test strategy by lane, and telemetry

| Lane | Budget (per package) | Holds |
|---|---|---|
| `unit` | < 10 s | The rules and fixtures; normalization, hash and schema staleness; the YAML features; every pure decision; the eval cases; a bounded seeded property run; the fake-store contract bases; `BlockContract`; architecture tests |
| `integration` | < 60 s | The registry's contract bases on SQLite; migrations on a copy of the previous schema; loopback endpoints; CLI golden files (every §6.4 layout, empty states); *rev 8:* the interpreter, Updates, timers, children and eval cases on the SDK's time-skipping test server, and the bridge and CLI goldens against `StartLocalAsync` (both binaries pinned in `binaries.lock`) |
| `heavy` | nightly | The Avalonia app's AOT smoke; the Sindri host's framework-dependent publish with `Aw.Sindri.Workflows` (*rev 8*); `recipe validate` on both |

**Temporal test findings (*rev 8.1*, found while measuring P4a; measurements in `docs/csharp/sindri-workflow.md`,
"Temporal foundation").**
- **F1. The time-skipping server skips no time while an activity is open.** A timer can't fire on it while any
  activity is scheduled or open, and an activity left open (even in a terminated instance) keeps time skipping off for
  that server's life. So desk Steps wait for the `complete` Update (§5.3), timeout tests with an open activity (agent
  Steps, P8) run on `StartLocalAsync` with short durations, and a test that needs time skipping gets a server no other
  test left an activity open on.
- **F2. The time-skipping server never moves a task off a dead worker's sticky queue.** Worker-restart tests there run
  the first worker with `MaxCachedWorkflows = 0`, or run on `StartLocalAsync`, where a new worker resumes the instance.
- **F3. Workflow code awaits with `ConfigureAwait(true)`.** `ConfigureAwait(false)`, which `CA2007` asks for in library
  code, leaves the workflow's deterministic scheduler; `RecipeWorkflow` writes `ConfigureAwait(true)`. For the same
  reason workflow code calls `CancellationTokenSource.Cancel`, not `CancelAsync` (`MA0042`).

**Telemetry:**
- spans per `Commit` (`Aw.Sindri.Workflow`);
- metrics:
  - transitions;
  - refusals by block;
  - parks by reason;
  - escalations and overrides;
  - session refusals;
  - version conflicts;
  - inbox depth;
  - deadline lag;
  - desk task age;
  - **park-to-answer latency**;
  - **time to outcome per recipe**.

These feed A §17.2, and all of it is scrubbed.

## 12. The first recipes

- **Real recipes:** `recipes/sindri/workflow.item.yaml` and `workflow.step.yaml`, with evals in
  `recipes/sindri/evals/`.
- **Dev recipes:** `recipes/dev/`, with `profile.yaml`:
  - each dev recipe has one eval in `recipes/dev/evals/<id>/happy.jsonl`, so its approval passes;
  - `workflow.dev-timeout`: a desk Step and a `1m` timeout (desk floor off in the dev profile:
    `workflow.deskTimeout: 1m`);
  - `workflow.dev-notify`: `ask` (a desk Step), then `ask -> sent` with `effect.notify.desk`, then `sent` waits on
    `notify.delivered` (producer `system:dispatcher`) and goes to `done`. A dispatch refusal arrives while it is in
    `sent`, so it can park.
- **Validate-only fixtures** (tests, with a test catalog):
  - `workflow.bugfix`: modes A and C, with block ids in the `step.bugfix.*` grammar;
  - `workflow.direction`.
- **Blocks:**

| Block | Runtime |
|---|---|
| `step.produce`, `step.checks`, `step.verify` (`deskCapable`) | Desk |
| `gate.budget.available` | Real, pure |
| `gate.route.startable` | A stub over `workflow.route` (P7) |
| `effect.claim.ref` | Real (P6), in the item's repo. Ownership is in `workflow_claims` (§5.4); the ref is a plain marker |
| `effect.notify.desk` | Real (P6) |

- **Events:**

| Event | Producer | Outcomes | Facts |
|---|---|---|---|
| `item.routed` | `service:router-stub` | `startable`, `notStartable` | `fact:stepSpec` |
| `claim.created` | `system:dispatcher` (route: causation) | `created`, `claimExists` | — |
| `notify.delivered` | `system:dispatcher` (route: causation) | `delivered` | — |

- **`fact:item`** is `{ id, repo, spec }`, where `id` is the `ItemId` (API §3), `repo` is a key of the profile's `repos`
  and `spec` is the Step spec text. `effect.claim.ref` creates
  `sindri/{user}/{item}` in that repo, pointing at its default branch's head, so the repo needs one commit.
- *P7, 2026-10-18:* the `slot` stub is the interpreter itself: it grants a slot the moment an instance waits for one, as the resource allocator's `wait.satisfied` with no outcome; only the router holds an item back. The router stub (producer `service:router-stub`, the actor the scheduler records) is a hosted service over the ledger.
- **`workflow.route`** (P7) is `{ startable: all | none }`. The `service:router-stub` service fires on each
  `workflow.started` of `workflow.item` and emits `item.routed` with `outcome` taken from that key, and with
  `fact:stepSpec` copied from `fact:item`'s spec.

```yaml
schema: 1
id: workflow.item
version: 1
inputs: [{ key: fact:item, label: "Item", type: item, kind: item }]
initial: triaged
states:
  triaged:   { kind: wait, waitFor: { event: item.routed }, timeout: { after: 7d, then: escalate },
               next: "Waiting for the router to size and route {item}." }
  startable: { kind: wait, waitFor: { resource: slot }, timeout: { after: 3d, then: escalate },
               next: "Waiting for a session slot for {item}." }
  claimed:   { kind: wait, waitFor: { event: claim.created }, timeout: { after: 10m, then: retry },
               next: "Waiting for the claim ref sindri/{user}/{item}." }
  running:   { kind: wait, timeout: { after: 14d, then: park },
               waitFor: { child: { recipe: workflow.step, inputs: { "fact:spec": { fact: fact:stepSpec } }, onParked: propagate } },
               next: "Waiting for {child}." }
  done:      { kind: terminal, outcome: resolved }
  dropped:   { kind: terminal, outcome: unresolved }
transitions:
  - { from: triaged,   to: startable, on: wait.satisfied, when: { outcome: startable },
      gates: [gate.route.startable], produces: [fact:stepSpec] }
  - { from: triaged,   to: dropped,   on: wait.satisfied, when: { outcome: notStartable } }
  - { from: startable, to: claimed,   on: wait.satisfied, gates: [gate.budget.available], effects: [effect.claim.ref] }
  - { from: claimed,   to: running,   on: wait.satisfied, when: { outcome: created } }
  - { from: claimed,   to: dropped,   on: wait.satisfied, when: { outcome: claimExists } }
  - { from: running,   to: done,      on: wait.satisfied, when: { outcome: resolved } }
  - { from: running,   to: dropped,   on: wait.satisfied, when: { outcome: cancelled } }
evals: [evals/workflow.item/happy.jsonl, evals/workflow.item/child-parks.jsonl, evals/workflow.item/claim-exists.jsonl]
```

```yaml
schema: 1
id: workflow.step
version: 1
inputs: [{ key: fact:spec, label: "Spec", type: text, kind: text }]
initial: produce
states:
  produce: { kind: active, step: step.produce, loop: revisions, timeout: { after: 4h, then: park },
             budget: { tokens: 200k }, next: "Produce the Step output for {item} (round {loop.revisions}/{loop.revisions.cap})." }
  checks:  { kind: active, step: step.checks, timeout: { after: 30m, then: retry },
             next: "Run the Step's deterministic checks." }
  verify:  { kind: active, step: step.verify, timeout: { after: 1h, then: park },
             next: "Review the output as the verifier." }
  done:    { kind: terminal, outcome: resolved }
transitions:
  - { from: produce, to: checks,  on: step.completed, when: { outcome: done }, produces: [fact:output] }
  - { from: checks,  to: verify,  on: step.completed, when: { outcome: passed } }
  - { from: checks,  to: produce, on: step.completed, when: { outcome: failed },   produces: [fact:findings] }
  - { from: verify,  to: done,    on: step.completed, when: { outcome: accepted } }
  - { from: verify,  to: produce, on: step.completed, when: { outcome: rejected }, produces: [fact:findings] }
loops: { revisions: { cap: 3 } }
budget: { tokens: 400k }
evals: [evals/workflow.step/happy.jsonl, evals/workflow.step/no-progress.jsonl, evals/workflow.step/rounds.jsonl]
```

**Step descriptors:**
- `step.produce`: outcome `done`, required output `fact:output`;
- `step.checks`: outcomes `passed` and `failed`, output `fact:findings` with `requiredOn: [failed]`;
- `step.verify`: outcomes `accepted` and `rejected`, output `fact:findings` with `requiredOn: [rejected]`.
- The P6 and P7 blocks (`effect.*`, `gate.route.startable`) ship in P1 as descriptors with unavailable runtimes,
  so P1 can validate both recipes.

The child's outcomes are `resolved` and `cancelled`, and `workflow.item` covers both.

### The scoping recipe (requirements from UI E-31, R8-11)

The scoping recipe (`workflow.scoping`, a child of `workflow.project`; its own spec is still to be written, UI phase U13
waits on it) is not defined here. These are the requirements this spec puts on it, so that its spec starts from them:

- **Draft proposals, never writes.** Its `deliver` state writes every new surface the run found (a surface with no
  node in the current map) as a **draft proposal to the next map version**, with the run (the instance address and its
  scoping evidence) as the proposal's evidence (CTX §8.7). It never writes to the current map; promoting a draft stays
  the `protected` map command. The write is an `effect.*` block on the `deliver` transition, so it is an activity with
  an effect identity (§5.7) and a retry repeats harmlessly.
- **The `scope-map` fact carries graph status.** The recipe's output `scope-map` (declared `type: scope-map`, `from:
  deliver`) is a fact whose rows each carry a graph status (`mapped`, `new` or `unmapped`, UI §5.2), so the app's
  scope-map table and its counts come from the fact, not from a second lookup.
- **Declared for the app.** It declares `urgency: work`, a `linear-project` subject as its first input, and `outputs`
  for the scope map and the packet (`type: packet`, `from` the state that freezes it), so U13's viewers render from the
  recipe alone.
- **Desk round trip.** Export of the scope map and import of the design output are two `desk` states of
  `workflow.project` (UI R8-10), not logic in the recipe's blocks; `aw-packets` is a block of the import state.

## 13. Review resolutions

/autoplan ran five passes (2026-10-10 and 2026-10-11), each over five lenses. No pass found a CRITICAL. Passes 1
and 2 came back NEEDS_WORK. In pass 3 security passed, and in pass 4 product and security passed. In pass 5
product, design and security passed, and arch and devex had two HIGHs each (one shared), which revision 6 fixes. The consolidated reviews are in `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-workflow-engine/`
(`consolidated-review.md` and `-r2.md` to `-r5.md`). The owner capped the loop at five passes, so revision 6 isn't
reviewed again. Its fixes are listed below for the owner's review.

### Pass 1 (C1–C32)

| Id | Fixed in |
|---|---|
| C1 | W12, §5.3, §5.4 |
| C2 | W14, §5.5 |
| C3 | W15, §5.7 |
| C4 | §2 `EventDescriptor`, `RCP-EVENT-001`, §12 |
| C5 | W16, §5.1 frames |
| C6 | W17, §5.7 |
| C7 | W20 |
| C8 | §3.8 |
| C9 | §4.2, §6.3, §6.4 |
| C10 | Phases |
| C11 | §6.1, S1-D8 |
| C12 | W1, §2 |
| C13 | W11, §4.1 |
| C14 | §8, which lives in `docs/csharp/sindri.md`, never in the generated TS `docs/sindri/errors.md` |
| C15 | W26, §7.2 |
| C16 | `RCP-CAP-005`/`006`, W9 |
| C17 | `RCP-CAP-007` |
| C18 | W22, §6.2 |
| C19 | W18, §5.6 |
| C20 | W21 |
| C21 | §9.1 |
| C22 | §3.4, §5.4 |
| C23 | §4.2 |
| C24 | W11, §9.1 |
| C25 | §3.1 |
| C26 | §4.1, §5.2 |
| C27 | §9.2 |
| C28 | §6.3 |
| C29 | W27, §6.1, §7.1, §9.2, §11 |
| C30 | W19 |
| C31 | §3.7, §3.8, §6.3 |
| C32 | Below |

### Pass 2 (E1–E24)

| Id | Fixed in |
|---|---|
| E1 | W16, §5.1 |
| E2 | W22, §6.2, §4.2 |
| E3 | Phase graph (P4 needs P2; evals ship in P1; P4 wires approve to run them), `--nonce`, the P6 order |
| E4 | §12 (child outcomes, `item.routed` facts) |
| E5 | S1-D9; migrations are numbered in sequence, and the integrator of the phase that merges second renumbers its files at rebase |
| E6 | S1-D11 |
| E7 | Hotspot rules (Phases) |
| E8 | W27 |
| E9 | §3.8 escalated rows |
| E10 | W7 `--grant-tokens` |
| E11 | W20; §6.3 notification classes; §5.5 desk reports; §2 `Start` after the commit |
| E12 | §5.4 outbox ids |
| E13 | W13, §5.2 |
| E14 | §5.7 forwarding, cancel in every state |
| E15 | W11, §5.4 `outbox.refused` |
| E16 | §5.4 nonces |
| E17 | §3.8, §6.4 |
| E18 | §6.3 `--run`, §5.5, W20 `via: desk` |
| E19 | `RCP-CAP-005`/`006` |
| E20 | §5.2, §10 |
| E21 | §3.4 answer waits, §6.3, §6.4, §8, §4.2 |
| E22 | W5 `deskCapable`, `recipe list --stale`, `eval --scaffold` |
| E23 | §7.1, W23, §6.1 |
| E24 | Below |

### Pass 3 (G1–G18)

| Id | Fixed in |
|---|---|
| G1 | W16, §5.8 resume actions |
| G2 | W11, §5.3 step 5 exemptions |
| G3 | §5.4 UUIDv8 ids, effect identity |
| G4 | §5.7 live-frame rows, §12 `running` 14d |
| G5 | Phases: signal steps marked **(agent)** or **(owner)**, and the owner runbook |
| G6 | §5.3 order, §5.4 report key, §8 `SND-WF-009` |
| G7 | §3.8 drift row, §4.1 same-hash re-approval, §5.4 claim identity by item |
| G8 | §3.8 `timeout` and `dispatch` rows |
| G9 | §4.2 challenge bound to its subject |
| G10 | `RCP-CAP-005` |
| G11 | `RCP-TRIG-002`, `workflow command` |
| G12 | §6.2 probed markers, parent process, named refusals |
| G13 | §5.1 stack rules |
| G14 | §5.4 `MarkRefused` (K-D1), engine-owned commands exempt |
| G15 | §5.7 `onBehalfOf`, §3.4 `retry` keeps its entry, §12 `dev-notify` |
| G16 | §2 `requiredOn`, §9.1 child resolution, §6.1 dev profiles, §12 route and repo, P2 owns the actor table, §7.1 `/new-slice`, the P3 wave |
| G17 | §6.4 layout 1, §3.8 texts, §4.2 and §8 naming, `step.checks` (below) |
| G18 | Below |

### Pass 4 (H1–H20)

| Id | Finding | Fixed in |
|---|---|---|
| H1 | (arch H1) The grant re-ran the wrong state | §5.8 `transition`; §3.8 rounds row |
| H2 | (arch H2) A parent stuck on a child that already ended | §5.7 "any row pops the mirror first" |
| H3 | (arch H3) A refused report burns its key | W12, §5.3 (only decisions are recorded) |
| H4 | (design J1, arch M1) Resume reuses the consumed key and gate answers | §5.8 answer key, §5.3 gate cache |
| H5 | (devex HIGH) The owner and the Prover don't share a server | §6.1 fixed `AW_SINDRI_HOME`, a built binary, the owner starts `serve` |
| H6 | (devex HIGH) An engine notification completes `dev-notify` | §5.7 `route: causation` |
| H7 | (sec M4-1, arch M2) The exemption was by kind | §5.3 step 6 and §5.4 `origin` |
| H8 | (sec M4-2, product) Two live attempts both own the claim | §5.4 owner in the ref, `SND-WF-012` |
| H9 | (sec M4-3) A drift approval didn't bind the pins | §4.2 pins digest; §4.1 re-validates, appends, batch-resumes |
| H10 | (sec M4-4, arch M9, design J3) Forwarding scope and proof | §5.7 synchronous forwarding of `answer` only, bound challenge |
| H11 | (product) Child pin lifecycle | §4.1 child pins |
| H12 | (product) Drift recovery for each instance | §4.1 batch resume |
| H13 | (arch M3) Drift on startup and on parked instances | §5.8 `rearm`, recorded on the suspended frame |
| H14 | (arch M4) A grant gave one round too many | §3.8, §5.8 |
| H15 | (arch M5) The entry recorded for commands | §5.4 entered frame |
| H16 | (arch M6, sec L4-5) Creating a revoked child | §4.1 |
| H17 | (arch M7) Replaying a slot grant | §5.5, §5.8 `rewait` |
| H18 | (arch M8, design J4) Dispatch refusals: code, text, ended frames | §5.4, §3.8, `SND-WF-011` |
| H19 | (design J2, J5, J6) Texts, variables, layouts | §3.7, §3.8, §6.4, `SND-WF-013` |
| H20 | Remaining devex, arch and security LOWs | Below |

### Pass 5 (I1–I12)

| Id | Finding | Fixed in |
|---|---|---|
| I1 | (arch Q1, devex HIGH) P4 step 3 replays an applied token | P4 signal uses `--run 4.1`; the report key adds a digest (§5.3) |
| I2 | (arch Q2) A grant on a gate-refusal park skips the gate | §3.8 park kinds `loop` and `refusal` |
| I3 | (devex HIGH) Dev-loop binary path, shell function, order | §6.1 `artifacts/bin/…/sindri` on `PATH`, `serve` before `profile approve` |
| I4 | (devex) Port clash, stale home, sandbox | §6.1 `AW_SINDRI_PORT=0`, clean home, loopback in the sandbox allowlist |
| I5 | (arch P1) The forwarded nonce is for the parent | §5.7 the nonce is minted for the child |
| I6 | (arch P2) Causation rows and early holds | §5.7 exact frame only; engine-origin deliveries write no row |
| I7 | (arch P3, product, design K4) Revoked child pin dead-ends | §3.8 child-revoked row, §4.1 |
| I8 | (arch P4, sec L5-3, product) The batch drift resume | §4.1 `recipe.approved:{seq}`, drift parks only, re-send; §10 tests |
| I9 | (arch P5, design K1, K3) Parks on parked or taken-over instances | §5.1 pending parks; §6.4 drift line |
| I10 | (arch P6, sec L5-2) A ref can't hold an owner | §5.4 `workflow_claims` |
| I11 | (design K2, K5) Answer lines, layouts, texts | §6.4, §3.8, §4.2 |
| I12 | Remaining LOWs | Below and inline (`RCP-TRIG-002` list, `SND-WF-014`, the engine-origin kind, `doctor` warnings, the forwarded action match, `005` vs `006`, `route` on `EventDescriptor`, the slot release, the counting wording, the `40s` format) |

### Written answers

- **P7 (host move):** approvals live in `ledger.db` and move with the A 17.19 handover snapshot.
- **P8 / product L9 (metrics):** §11.
- **Design LOWs:** one duration format and ASCII (§6.4); the fix appears on rule lines (§9.1); `--version` is gone
  in favour of nonces (§5.4); short hashes (§3.1); `takeover` from a park (§3.8); engine variables (§3.7); the
  `SND-WF-010` text (§3.8); empty states (§6.3).
- **Arch R11, R12, R14, R16:**
  - Early events are held (§5.7).
  - Gate answers are saved in their own transaction (§5.3).
  - Downtime counts as sleep **on purpose**: a laptop shut down for three days hasn't run the work, so its timeouts
    shouldn't fire. This reverses pass-1 F9(c) deliberately. `IMonotonicClock` makes it testable.
  - Each service runs on its own loop (S1-D10).
- **A-F20–F25:** gate answers are reused (§5.3); outbox ids are derived (§5.4); only `RCP-EFFECT-001` reads the
  profile; at most 3 re-decisions on a conflict, and Kernel's `OutboxRetryPolicy` for dispatch; each migration
  declares its indexes, covered by migration tests.
- **Product (pass 2):**
  - **`ILauncher`** gets its own design next, because it's the session-launch half of §17.29.3 and needs the
    container and attach work.
  - **Bugfix mode B** (two candidates in parallel) needs schema v2's fan-out. Until then the worker recipe runs
    modes A and C, and mode B stays in `bugfix-state`.
  - **The `/new-block` scaffold** is filed by the P7 integrator as an issue, once three real blocks exist.
  - **Pin drift** parks instances with `SND-RECIPE-006`. `recipe list --stale` names what to re-approve, and each
    re-approval is typed on purpose.
- **Security LOWs:**
  - **S2-L-a (nonce lifetime):** 10 minutes, cleaned up by the timers service.
  - **S2-L-b (who may write an event):** `producer` on the event descriptor.
  - **S2-L-c (behaviour changes):** a descriptor hash pins the declared contract, not the behaviour. A behaviour
    change bumps `Version` by review rule, and `BlockContract` tests pin the behaviour.
  - **S2-L-d (file paths):** the CLI sends content.
  - **S2-L-e (metrics):** session refusals and overrides are counted (§11).
  - **S-L1:** typed item ids.
  - **S-L2:** aliases are refused, and eval paths stay inside the recipe directory.
  - **S-L3:** the actor table (§6.2).
  - **S-L5:** the model proxy is the budget backstop.
  - **S-L6:** `RCP-NEXT-002`.
  - **S-L8:** the egress proxy.
- **Timeouts aren't capability fields** (`RCP-CAP-006` doesn't compare them): a shorter timeout only parks sooner,
  and a longer one only waits longer. Neither grants anything.
- **Pass 3 answers:**
  - **`step.checks` with a real runtime** (product M3): a runtime that runs the check command is the `run-test`
    evidence block lifted from `bugfix-state` (A 17.28.2). It belongs to the worker-recipes design together with
    Judge and Evidence. Until then the desk outcome is labeled `via: desk` in receipts.
  - **Scaffolded evals** (product L4): a scaffold proves only the happy path, and the approve preview marks
    `scaffold only` so the human sees how thin it is.
  - **Budget park on `workflow.item`** (arch N7): `gate.budget.available` reads the profile budget, and
    `--grant-tokens` raises the instance's allowance against it. The slot is released at the park (§5.5).
  - **Arch L1–L8:** the nonce step is in `Commit` (§5.3); UUIDv8 (§5.4); the arm counter's scope (§5.1); child
    start events (§5.2); the drift source (§5.4); the run token in the layout (§6.4); `RCP-TIME-002`; the
    dispatcher actor `system:dispatcher`.
  - **Design G7 and G8:** the start hint, the engine variables, empty states, `takeover` from escalated, the
    grant-tokens text and the `--needs-me` dedupe are in §3.7, §3.8, §4.2 and §6.3.
  - **Devex LOWs:**
    - the notifications path is `$AW_SINDRI_HOME/notifications.jsonl` (rev 7; rev 4 named the TS folder);
    - child inputs use `{ fact: <key> }`;
    - P7 passes `fact:item`;
    - the P2 signal shows `<hash>`, not a fixed value;
    - the `workflow` group table is P4's contract unit;
    - `approve <file>` registers first when needed;
    - S1-D4 has a row;
    - the MCP architecture test applies once an MCP project exists;
    - `RCP-EFFECT-001` reads the approved profile.
  - **Security L-1 and L-2:** the 1 MiB caps and `0600` (§6.1); the `ui` class (§6.2).
- **Pass 4 answers:**
  - **Branches and `/specToProvenPR`:** this doc stacks P1 and P3 on S1's branch and overlaps the waves. The plan's
    `stages.md` records each stage's base branch. When a parent stage's code changes under a stacked stage, that
    stage's proof is run again after the rebase.
  - **Units and red tests:** each implementer runs only its own unit's test classes (`--filter-class`), because
    other units' red tests are expected to fail. The integrator runs the whole lane.
  - **Hotspots:** add `docs/csharp/sindri.md`'s error table, and append switch arms before the default arm.
  - **The session cap** is 3 implementers at once, with at most 2 building.
  - **Runbooks** start with their setup: the profile approval, approving each recipe used, and creating the input
    files.
  - **`recipe register` stays `script+`:** registering grants nothing, and approval is terminal-class.
  - **A nonce is fetched after the challenge is typed**, just before sending (arch L1).
  - **`workflow.request`** writes an event but changes no row, so it doesn't bump the version (arch L10).
  - **`MarkRefused` and `outbox.refused`** run in one transaction (arch L11).
  - **A drifted generated recipe** is re-frozen at the desk once a generator exists. None is built in these
    phases.
- **The eval format** (owner decision, 2026-10-11, rev 7.2): child lines, `variant` and `amount` for the engine
  triggers, and `--record` reading content from the stored facts (§9.2).
- **`answer --text` on a `no-progress` park works once per loop** (§3.8; the lane lead's decision, 2026-10-11, not
  the owner's). `--text` is a desk action and grants are protected (W7), so a `--text` that worked once per park would
  let a desk caller add rounds without end. After the one `--text`, more rounds need `--grant n` at a terminal.
- **Crafted bypasses** are outside the threat model (A 17.28.3; `docs/csharp/bugfix.md` "Threat model"):
  - direct SQLite writes;
  - unauthenticated loopback callers and port squatting;
  - hand-built challenge requests;
  - unsetting the harness markers.

  Gates stop honest mistakes. The sandbox, the owner-only state directory and the loopback bind are the boundary.

## Phases (rev 8)

Each phase is one PR. The order across all three specs is the one table in `docs/sindri/README.md` ("Build order");
the dependencies below follow it. Signals write `T-n` for the `ITM-…` id that `sindri item add "<title>"` printed:
every signal creates its item first (API P3). Every **(owner)** step runs through `scripts/sindri-prove.sh human` (API
§12), so the Prover runs both halves and the ledger records `human:prover`. Every phase updates
`docs/csharp/sindri-workflow.md` and `docs/csharp/sindri.md`. Sizes: S is a few files, M one subsystem slice, L a
worker or several slices.

**Status at rev 8:** P1 merged (#179). P3 open as #180: it keeps its contract unit and units (a), (c), (d) and (f),
drops (b) `FrameRules` and (e) `EventFold` and the store contract, and lands once rebased on this revision. P2 is
unchanged. *Rev 8.1:* P4a (the Temporal foundation) is new; it needs only P3, and P4 needs it.

### Parallel execution with an agent team

```
P1 model + validator (merged) ──┬──► P2 registry + approval (S1, API P1) ──────────────────────┐
                                └──► P3 pure decisions + evals ──► P4a Temporal foundation ────┴──► P4 interpreter, worker, desk Steps, bridge (API P3) ──┬──► P5 timers, takeover ─┐
                                                                                                                                                          └──► P6 effects ─────────┴──► P7 children + item ──► P8 agent runtime (API P10, Judge #156)
```

| Wave | Parallel | Starts when |
|---|---|---|
| 0 | P2 ∥ P3 | Now. P2 also needs S1 merged and API P1 |
| 1 | P4a | P3 merged; nothing else |
| 2 | P4 | P4a, P2 and P3 merged, API P3 merged |
| 3 | P5 ∥ P6 | P4 merged |
| 4 | P7 | P5 and P6 merged |
| 5 | P8 | API P10a merged (rev 8.2, provisional owner default Q10: P7 and Judge #156 are not needed; the agent runtime calls no Judge provider) |

These engine-local waves sit inside the cross-spec order in `docs/sindri/README.md`, which decides what starts when.

**Units:** each phase starts with one contract unit (records, ports, stubs, committed red). The other units then fan
out, one implementer each:

| Phase | Contract unit | Parallel units |
|---|---|---|
| P4a | None: the Temporal foundation lands as one PR from the branch its numbers were measured on (head `1e586964`) | — |
| P2 | Ports, fakes, contract bases, the challenge shell, the engine's catalog rows on API P1's chain, the agent-variable probe | (a) SQLite adapters for `recipes` and `recipe_approvals`, S1-D9 · (b) register, show, list, `--stale` · (c) approve (including drift re-approval), revoke with previews, cascade; the revoke and drift **signals** are stubs until P4 · (d) pins, `RCP-VERSION-*`, registry events, the architecture test · (e) the harness probe and its golden test |
| P3 | Instance state records, triggers, decision signatures | (a) `TransitionDecision`, `RefusalRules` · (c) `LoopRules`, `BudgetRules`, grants · (d) `ChildPropagation` · (f) `recipe eval`, `--scaffold`, `--record` format. Units (b) and (e) of rev 7 are dropped |
| P4 | `RecipeWorkflow` skeleton with the Query and the Update signatures, the worker host inside `sindri serve`, `RecordEvent`, the `workflow` group table | (a) the interpreter's state loop over `TransitionDecision`, search attributes · (b) start, Updates, nonces (API-side table), the §5.8 resume actions · (c) desk runtime: `RunStep` records the open task, the `complete` Update and its validator (*rev 8.1*, §5.3) · (d) the bridge service (S1-D8, S1-D10) · (e) status, list (visibility), events (S1-D6), golden layouts, approve running the evals, S1-D11 (`deskTimeout`, `eventHold`) |
| P5 | The timer scopes and `remaining` on the suspension | (a) state timers, `retry`, downtime behaviour (X58) · (b) takeover, handback, `takeoverIdle` |
| P6 | `IEffectKind` as activities, the recheck local activity | (a) effect activities, `dispatchDelay`, effect identity, `retry` re-emission · (b) the recheck and `outbox.refused` parks · (c) `notify.desk`, notifications, `outbox list` (reads effect rows from the ledger) · (d) `effect.claim.ref`, `workflow_claims`, S1-D11 (`effects`, `dispatchDelay`) |
| P7 | Child workflow start, parent-close policy, the mirror suspension | (a) child creation and propagation signals, forwarded Updates · (b) router and slot stubs, `route` key, both gates · (c) `workflow.item` end to end, filing the `/new-block` issue |
| P8 | The agent runtime: `RunStep` for agent Steps, tmux ownership in `ILauncher`, heartbeats | (a) launch and tmux session naming · (b) `step-report` with the launch secret, activity heartbeat from `session wrap` · (c) usage on `session.ended`, `via: agent` |

**Hotspots** (`PublicAPI.Unshipped.txt`, `WorkflowErrors`, `Add…()` methods, the group command tables, the JSON
context, `Directory.Packages.props`, `binaries.lock`): units **append** their own lines, so every unit commit builds.
The integrator resolves the unions when it merges the units.

**Roles and limits** are rev 7's: an orchestrator that writes no product code, one implementer per unit in its own
worktree, one integrator per phase, a Prover that runs both halves of every signal, and reviewers until no blocking or
issue findings remain; at most 3 implementers at once and two building (the heavy-job lock's two slots).

**The Temporal dev server in tests.** The `unit` lane uses no server: the pure decisions and the recipe reader.
The `integration` lane uses `WorkflowEnvironment.StartTimeSkippingAsync` (the SDK's time-skipping server, a pinned
download recorded in `binaries.lock`, cached under `$AW_STATE_DIR/cache`) for the interpreter, the Updates, the
timers and the eval cases, and `StartLocalAsync` for the bridge and the CLI goldens. The `heavy` lane runs the AOT
smoke for the Avalonia app and the framework-dependent publish of the Sindri host.

### Phase 1: the recipe model and validator

Merged (#179). Unchanged by rev 8.

### Phase 2: registry and approval

- **Goal:** recipes are registered by hash and approved or revoked through a previewed, challenged desk action, with
  pins and cascade. The revoke and drift **wakes** are stubs (a port with a no-op adapter) until P4 supplies the signal
  sender.
- **Files:** ports and slices; SQLite adapters and migrations for `recipes` and `recipe_approvals` (with S1-D9); the
  challenge; registry events; contract bases; the architecture test; docs.
- **Size:** M.
- **UI screens edits E-21 and E-22 land here (2026-10-14).** The recipe format gains `urgency`, `outputs`, the nine
  fact types and `inputs[].kind` (§3.2, §3.3), `StatePathOrder` (§3.10) and the rules `RCP-URG-001`, `RCP-OUT-001`–`003`
  and `RCP-TYPE-001`–`003`, with `recipe show` and `ListRecipes` returning them. They land in P2, **before the hash and
  the approval format are pinned**: the registry's approved hash covers the whole normalized file (§3.1), so a field
  added after P2 re-hashes every approved recipe and forces a re-approval of each, which is cheap now and costly once
  recipes are approved in daily use. (P1 is merged, so these are a follow-up to its model, owned by unit (b) and (d).)
- **S1 dependency:** S1 merged, and API P1 (the command chain) merged; S1-D5, S1-D9.
- **Signal:** rev 7's, unchanged: **(owner)** `sindri recipe approve recipes/sindri/workflow.step.yaml` prints the
  preview and asks for `approve workflow.step 1`, then `sindri recipe list` shows `workflow.step@<hash> approved
  (desk)`; **(owner)** the harness probes inside Codex and Cursor; **(agent)** the same approve exits 2 with
  `SND-AUTH-002`; **(owner)** `< /dev/null` exits 2 with `SND-AUTH-002`.

### Phase 3: the pure decisions and recipe evals

- **Goal:** every decision the interpreter takes is pure Core, proven by the eval cases.
- **Files:** `Core/Workflow/Engine/**` (`TransitionDecision`, `RefusalRules`, `LoopRules`, `BudgetRules`,
  `ChildPropagation`, the instance state records); `EvalRecipe`, `--scaffold`, `--record`; docs.
- **Size:** M (was L; the frame, fold and store work is dropped).
- **S1 dependency:** none.
- **Signal** (all **(agent)**): `sindri recipe eval recipes/sindri/workflow.step.yaml` prints `happy pass`,
  `no-progress pass`, `rounds pass` and exits 0; a copy with one edited `expect` exits 1 and names the line that
  diverged; `sindri recipe eval --scaffold recipes/dev/workflow.dev-timeout.yaml` prints a case that then passes. In
  P3 `recipe eval` folds the pure decisions over the triggers in-process; from P4 it also runs them through the
  interpreter on the time-skipping server (the two must agree).

### Phase 4a: the Temporal foundation (*rev 8.1*)

- **Goal:** the Temporal substrate that P4 and every later phase build on, landed with its acceptance checks measured.
  There is no spike: Temporal is decided (owner, 2026-10-12), and the code written to measure it lands as this phase.
- **Ships:** the `Temporalio` pin in `Directory.Packages.props`; `binaries.lock` (the `temporal` CLI's version and
  sha256); `Aw.Sindri.Workflows` (`AwAotCompatible=false`); the framework-dependent Sindri host; the dev server under
  `sindri serve` and `doctor`; the worker; a first `RecipeWorkflow`; `workflow complete` and `workflow list`; the
  ledger mirror (`RecordEvent`). P4's units rebuild or extend each of these to this spec. The desk Step is still rev
  8's async-completion activity; P4 moves it to the Update model (§5.3).
- **Size:** L (one PR from the branch the numbers were measured on, head `1e586964`).
- **Dependencies:** P3 merged; nothing else.
- **Acceptance (Temporal):** each line measured on that branch; see `docs/csharp/sindri-workflow.md`, "Temporal
  foundation", for the numbers and how they were taken.
  1. The server starts and stops with `sindri serve`, on loopback only, its file under `$AW_SINDRI_HOME`, with
     adopt-or-replace after a crash, and survives a pause.
  2. Async completion, timers and kill-and-resume work. The evals take 46 ms in total, and the 4 h timeout fires in
     3 ms of wall time on the time-skipping server with the Update model.
  3. The ledger holds one row per transition after a replay.
  4. Footprint: the dev server's physical footprint has a median of 129 MB idle and 133 MB with ten instances (RSS
     164 and 181 MB); worker overhead is at most 59 MB physical; the server is ready in ≤ 1.1 s. The thresholds stay
     ≤ 300 MB, ≤ 500 MB, ≤ 150 MB and ≤ 5 s.
  5. The AOT split builds with zero warnings; the host publishes framework-dependent; the Avalonia app still
     publishes NativeAOT. The app line was checked in a merge with U0's branch and is re-checked once U0 is on main.
- **Signal** (all **(agent)**): the five acceptance lines, each with its measurement, in
  `docs/csharp/sindri-workflow.md` under "Temporal foundation", and a green `dotnet build -warnaserror`.

### Phase 4: the interpreter, the worker, desk Steps and the bridge

- **Goal:** instances run as `RecipeWorkflow` executions on the local Temporal server; commands are Updates; desk
  Steps wait for the `complete` Update (*rev 8.1*, §5.3); registered ledger events reach instances as signals; every
  transition is mirrored into the ledger.
- **Files:** `Aw.Sindri.Workflows` (`RecipeWorkflow`, `RunStep`, `RecordEvent`, `LoadRecipe`, the worker host); the
  slices for start, command, complete, nonce, get, list, the bridge; the actor table; S1-D6, D7, D8, D10 and D11
  (`deskTimeout`, `eventHold`); golden layouts; approve running the evals; the revoke and drift signal sender behind
  P2's port; docs.
- **Size:** L.
- **Starts from** P4a's code (*rev 8.1*): its units rebuild or extend the foundation to this spec.
- **Dependencies:** P4a, P2 and P3 merged, API P3 (items) merged.
- **UI screens edit E-30 (2026-10-14).** The interpreter's mirror (`RecordEvent`) writes the Temporal history event id
  into the nullable `temporal_event_id` ledger column (§5.6; migration owned by S1, column documented in
  `docs/csharp/sindri.md`, "Ledger"), so the API's later `GET /engine/instances/{address}/history` (API "P13 engine")
  can link history lines to ledger sequences. Agent signal 6 below checks it.
- **Signal:**
  1. **(owner)** With `workflow.step` approved, `sindri workflow start workflow.step --item T-1 --fact fact:spec=spec.md`,
     then `sindri workflow status T-1/step/1`, shows the desk-task layout with `--run 1.1`.
  2. **(owner)** `sindri workflow complete T-1/step/1 --run 1.1 --outcome done --output fact:output=out.md --json`
     prints `-> checks` and the nonce it used. Two more completes (`passed`, `accepted`) print `done: resolved`.
     `sindri workflow events T-1/step/1` lists every event, read from the ledger.
  3. **(owner)** Re-running the first `complete` exactly exits 0 with `already applied` and no new ledger event
     appears. `complete T-1/step/1 --run 4.1 --outcome passed --nonce <the nonce from step 2>` exits 2 with
     `SND-WF-009` and records nothing.
  4. **(owner)** `kill -9` of `sindri serve` between steps 1 and 2, then `sindri serve` again: `status` shows the same
     run token, and step 2 completes normally.
  5. **(agent)** The same `complete`, from the agent session, exits 2 with `SND-AUTH-002`. `sindri workflow events
     T-1/step/1 --json` matches the owner transcript.
  6. **(agent)** After step 2, `sqlite3 ledger.db "select count(*), count(temporal_event_id) from events where type like
     'workflow.%'"` returns equal counts (every mirrored row has its event id), and replaying the same history adds
     no row.

### Phase 5: timers, takeover and handback

- **Goal:** timeouts fire as workflow timers, takeovers pause them, handback resumes them with the remaining time.
- **Files:** the timer scopes; `remaining` on the suspension; takeover, handback, `takeoverIdle`; docs.
- **Size:** S.
- **Dependencies:** P4.
- **Signal** (dev profile, `workflow.deskTimeout: 1m`; **(owner)**, then the Prover checks `workflow events`):
  `sindri workflow start workflow.dev-timeout --item T-2`, wait at least 75 s, and `status` shows `parked: timeout`.
  A second instance on `T-8`, taken over within 20 s, shows `taken-over` and `paused timeout 40s left` (±5 s); nothing
  fires until `handback`, and it parks after roughly the remaining time. `sindri workflow answer T-2/dev-timeout/1
  --resume` puts the first instance back in its desk task with the same run token. The integration test runs the same
  three cases on the time-skipping server in under a second.

### Phase 6: effects as activities

- **Goal:** effects run as activities after the dispatch delay and a revoke recheck; refusals park the instance; parks
  notify.
- **Files:** the effect activities, the recheck, effect identity and `retry` re-emission; `notify.desk`,
  notifications, `outbox list` over the ledger's effect rows; `effect.claim.ref` and `workflow_claims`; S1-D11
  (`effects`, `dispatchDelay`); docs.
- **Size:** M.
- **Dependencies:** P4.
- **Signal** (dev profile with `effect.notify.desk` allowlisted and `dispatchDelay: 60s`; **(owner)**, then the Prover
  checks `outbox list --json` and the events):
  1. `sindri workflow start workflow.dev-notify --item T-4`, then `complete` its desk Step. After the delay,
     `sindri outbox list --dispatched` shows the effect, the notifications file has the line, and the instance reaches
     `done` through the `notify.delivered` signal.
  2. On `T-5`: `complete`, then within the 60 s window `sindri recipe revoke workflow.dev-notify@<hash>` with its
     challenge. `outbox list` shows the recipe effect `refused (SND-RECIPE-005)` and the engine's own first-task notice
     `dispatched` (origin `engine`). `status T-5` shows `parked: spec-conflict` (revoked; only `cancel` offered), and
     `cancel` works.

### Phase 7: children and the item lifecycle end to end

- **Goal:** `workflow.item` runs with a `workflow.step` child workflow; every child change propagates.
- **Files:** child start and the mirror suspension, propagation signals, forwarded Updates; the router and slot stubs,
  the `route` key; `gate.route.startable`, `gate.budget.available`; `workflow.item` evals; docs.
- **Size:** M.
- **Dependencies:** P5 and P6.
- **UI screens edit E-31 (2026-10-14).** The scoping recipe's requirements are recorded in §12; its spec and recipe land
  with `workflow.project`, after this phase, and do not change P7's scope.
- **Signal** (dev profile naming a temp repo with one commit, `workflow.route: { startable: all }`, `effect.claim.ref`
  allowlisted, `workflow.budget.tokens: 1M`; **(owner)**, then the Prover checks the events): `sindri workflow start
  workflow.item --item T-3 --fact fact:item=item.json` shows `triaged -> startable -> claimed -> running` in
  `sindri workflow events T-3`, and `git -C <repo> show-ref sindri/<user>/T-3` finds the claim ref. Completing the
  child's three desk Steps moves the parent to `done: resolved`. On `T-7`, completing `checks` as `failed` twice with the
  same findings parks the child (`no-progress`) and the parent shows `parked: no-progress (child)`;
  `sindri workflow answer T-7 --grant 1`, with its challenge typed, is forwarded to the child with `onBehalfOf`,
  performs the withheld `checks -> produce` transition, and resumes both.

### Phase 8: the agent runtime

- **Goal:** a Step runs in a launched agent session that the human can join live; the session reports its own
  result; the factory line runs unattended.
- **Files:** `RunStep` for agent Steps; tmux ownership in `ILauncher` (API §8.1, rev 8); the `step-report` check with
  the launch secret; activity heartbeats from `session wrap`; `session.launched` and `session.ended`; usage from the
  target's usage reader; the `workflow.agent.target` profile key; finding keys and a diff signal in Step reports, and
  with them the full A §17.23.13 no-progress rule (§3.7, rev 7.2); docs.
- **Size:** M.
- **Dependencies:** P7, API P10a (`ILauncher` and §8.1), Judge's provider port (#156). This phase adds Sindri's
  `deps.yaml` edge to Judge. It needs no fan-out exception: the owner removed the per-package cap (DEPS-FANOUT-001)
  on 2026-10-12.
- **Signal:** with `workflow.agent.target: claude-code`, starting `workflow.step` on a new item launches Claude Code
  **interactively inside a tmux session named `snd-<run>`**, in a per-run worktree, with the seed as the initial
  prompt. **(owner)** `sindri open <item> --attach` opens a Warp tab attached to that tmux session; typing one line
  into it records a `takeover` and `status` shows `taken-over`; `sindri workflow handback` resumes it. The session's
  `complete --run` moves the instance to `checks`, and the event records `via: agent` and the token usage. The same
  `complete` from another session exits 2 with `SND-AUTH-002`. Killing the tmux session parks the Step through
  `step.failed(exit)` within 150 s.

> DT-R1 note: block descriptors gain optional `Runtime` (`desk|block|agent`) and `HumanOnly` fields (written only when set; DT spec §8.5, scoping-station W3); `workflow complete` on a human-only Step needs a human caller.
