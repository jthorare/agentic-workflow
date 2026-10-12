# Sindri workflow engine: recipes, blocks, validator and engine

**Status:** design, **revision 8** (2026-10-11): the engine runs on Temporal (§0, §5); revision 7 was the epic
reconciliation after five /autoplan passes · **Date:** 2026-10-10 · **Builds:** factory addendum §17.28 (one workflow
state machine) and §17.29.4 step 2 · **Next:** P0 (the Temporal spike), then `/specToProvenPR` takes the Phases at the
end to PRs.

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
| Run token, desk and agent reports | An **activity with async completion**: the task token is completed by `sindri workflow complete` | The launch secret, the report validation (§5.5), the outcomes and outputs contract |
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

**Phases.** P0 (the Temporal spike) is new and gates P4 and later. P1 (merged, #179) and P2 are unchanged. P3
(#180) keeps its pure decisions and drops the frame, fold and store work. P4 through P8 are rewritten at the end.

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
| W8 | **Every recipe needs a human approval to run.** Approvals are stored keyed by `(id, version, hash)`, never in the file | §17.19: workflow templates are approval-tier |
| W9 | **Hand-written recipes are approved with `sindri recipe approve`**, through a server-issued challenge. **Generated recipes are approved by the packet freeze**, from a `base` that Sindri code picks | Invariant 13 |
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
| W29 | **The ledger stays the public log.** Every transition is mirrored into `ledger.db` by an idempotent local activity; nothing reads Temporal history but the engine and its tests | One log for the UI, the API and the evolve loop (X16, API §5) |
| W30 | **Authorization never reaches Temporal.** The API chain is the only writer of Updates and starts; the server is loopback with no auth | API §4; the honest-mistake threat model (A §17.28.3) |
| W31 | **tmux owns every agent session; Warp attaches.** `attach` joins the live session; typing is a takeover | README X55; the human can step onto the line at any Step (API §8.1, UI §6.2) |
| W32 | **The Sindri host is framework-dependent;** the Temporal adapter opts out of `IsAotCompatible` with its reason; the Avalonia app stays NativeAOT | temporalio/sdk-dotnet#286; README X54; csharp.md "AOT scope" |
| W33 | **P0 is a three-day spike with numbered pass lines**; a failed spike returns rev 7's §5, cut down | Nothing depends on Temporal until it is measured |

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
  form hash the same. Hashes are shown as 12 hex characters, and input accepts a prefix of 8 or more.

### 3.2 Top level

| Field | Req. | Rule |
|---|---|---|
| `schema` | yes | `1` |
| `id` | yes | `workflow.<name>` |
| `version` | yes | `RCP-VERSION-001` (same id and version, different hash); `-002` (not above the highest registered) |
| `description` | no | |
| `base` | generated only | Written by Sindri's generator. A hand-written recipe with `base` is `RCP-CAP-004` |
| `inputs` | no | Fact keys. The long form is `{ key, label?, type?: text \| file \| item }`; `recipe show` and `GET /workflow/recipes` return them so the app can render a start form |
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
| Block id | `<gate\|step\|effect\|evidence\|input>.<area>.<name>` |
| Event kind | `<area>.<name>`, registered as an `EventDescriptor` |
| Duration | `<n>m`, `<n>h` or `<n>d`, at least `1m` |
| Tokens | int, or with a `k`/`M` suffix |
| `with:` value | A literal (string, number, bool or a list of these), `{ fact }` or `{ profile }`. Capability parameters take only literals or `{ profile }` (`RCP-CAP-007`) |

### 3.4 States

Every state has `kind: active | wait | terminal`.

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
harmless. A second timeout parks with `timeout`. In desk mode `retry` is replaced by `park` (W20). `retry` isn't
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
| `produces` | no | Each must be an output or fact of the trigger's source (`RCP-WIRE-002`) |

The first transition whose `when` holds is the matched one. If its gates refuse, that is a refusal (§5.5), not a
fall-through. A `step.failed` with no transition parks with `needs-answer`.

### 3.6 `when` (closed)

`{ outcome }`, `{ fact, is: present|absent }`, `{ loop, below: cap }`, `{ all }`, `{ any }` and `{ not }`. Outcome
names come from the trigger's source: a Step, an event, or a child's terminal outcomes plus `cancelled`.

### 3.7 Loops, budgets, `next`

- **Loops:** `{ cap ≥ 1, noProgress }`. `noProgress` defaults to `[findings, emptyDiff, sameFailure]` and can't be
  empty; the rule is A §17.23.13. Popping a frame never counts a round.
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
| `parked` (`rounds`, `no-progress`) | A loop cap or no progress, either on the transition back into a looped state (kind `loop`) or on a gate refusal at the cap (kind `refusal`); the frame records which | "Parked ({reason}): {question}. Grant more rounds at a terminal, or cancel." | `answer --grant n` (protected): kind `loop` performs the withheld loop transition, and kind `refusal` re-runs the Step. Either way, the round it starts is the first of the n. `answer --text` on `no-progress`: the same, once. `takeover`, `cancel` |
| `parked` (`budget`) | A budget, or a `gate.budget.*` refusal | "Parked (budget): {question}. Grant tokens at a terminal, or cancel." | `answer --grant-tokens n` (protected): re-runs the Step, or re-evaluates the withheld transition. `takeover`, `cancel` |
| `parked` (`needs-answer`, Step) | A refusal without a loop, `step.failed` | "Parked (needs-answer): {question}. Answer with --text; the Step runs again with your answer." | `answer --text`: re-runs the Step with `fact:answer`. `takeover`, `cancel` |
| `parked` (`needs-answer`, wait) | A gate refused the transition out of a wait | "Parked: {gate} refused {from} -> {to} ({reasons}). Fix the cause, then resume to check again, or cancel." | `answer --resume [--text]`: re-checks every gate and evidence at the current head under the answer's key (§5.8). `takeover`, `cancel` |
| `parked` (`timeout`) | `then: park` | "Parked (timeout): {state} ran past {after}. Resume to give it the full time again, or cancel." | `answer --resume`: back to the frame (the same run token) with a fresh deadline. `takeover`, `cancel` |
| `parked` (`spec-conflict`: revoked) | `recipe revoke` of this instance's own recipe | "{recipe} was revoked. Cancel, then start a new attempt from an approved version." | `cancel` |
| `parked` (`spec-conflict`: child revoked) | The child hash this recipe pins was revoked | "{child recipe} (pinned by {recipe}) was revoked. Re-approve {recipe} so it pins an approved child; the approval resumes this instance." | The parent's drift approval resumes it (§4.1). `cancel` |
| `parked` (`spec-conflict`: drift) | A pinned block changed in this build | "{block} changed since {recipe} was approved. Re-approve it (`sindri recipe approve {recipe}`, shown as `id@hash12`); the approval resumes this instance." | The drift approval resumes every instance it lists, and re-sends any of their commands refused for the drift (§4.1). `cancel` |
| `parked` (`spec-conflict`: dispatch) | `outbox.refused` with `SND-WF-011` (not allowlisted) | "{effect} isn't on the profile's effect allowlist. Add it and approve the profile, then resume to send it again." | `answer --resume`: re-emits the effect. `cancel` |
| `parked` (`SND-WF-010`) | A Step or resource with no runtime | "No runtime for {block} in this build. Cancel, or resume in a build that has it." | `answer --resume`: starts the Step again (a new run), or waits for the resource again. `cancel` |
| `escalated` (gate) | A top-rung gate below threshold or uncertain | "Escalated: {gate} scored {score}, below {threshold}. Accept to override it (the other checks still run before {to}), or reject (the Step re-runs within its loop, otherwise this parks)." | `--accept` (protected) overrides only that gate, then evaluates the remaining gates and evidence at the current head. `--reject` is a refusal. `cancel` |
| `escalated` (timeout) | `then: escalate` | "Escalated: {state} ran past {after}. Resume to give it the full time again, or cancel." | `--resume`, `cancel` |
| `escalated` (child) | The child escalated | "Escalated in {child}: {childNext}" | The child's `answer` variants, forwarded (§5.7), and shown as the child's `allowedCommands`. `cancel` and `takeover` act on the parent |
| `taken-over` | `takeover` (allowed on a parked or escalated instance as well) | "With you since {since}. Run `sindri workflow handback {instance}` when you're done." | `handback` (pops back to whatever it covered), `cancel` |
| `cancelled` | `cancel` from any state, the tracker, a cancelled parent | (terminal) | outcome `cancelled` |

### 3.9 Left out of v1

- **Fan-out children** come in schema v2, with the Step graph and stacks (A 17.28.6 steps 3 and 6).
- **Per-transition timeouts** come only if a recipe needs one.
- **A free expression language:** never.
- **Variables and includes:** not planned.

## 4. Approval and invariant 13

### 4.1 Store and events

`IRecipeApprovals` records:
- the recipe: `id`, `version`, `hash`;
- who and how: `approver`, `approvedAt`, `via: desk|freeze`, `base?`;
- what it pins: `descriptorHashes` and `children { id: hash }`.

`IRecipeStore` keeps the normalized text by hash. Register, approve and revoke write `recipe.registered`,
`recipe.approved` and `recipe.revoked` events. Approvals live in `ledger.db` and travel with a host handover.
`recipe list --stale` lists approved recipes whose pins no longer match the build. Re-approving the **same hash**
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
   version plus a digest of the pins for a drift approval; or the instance address and version, including the
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
- **Search attributes**, set on start and on every transition: `Item`, `Recipe`, `State`, `NeedsYou` (true while
  parked, escalated or holding an open desk task), `Repo`, `Tenant`, `Ring`.
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

### 5.3 Steps are activities with async completion

An `active` state starts an activity `RunStep` with a **run token** `{entry}.{n}` in its activity id, so the token
stays a public counter in `status` and in the CLI.
- **Desk runtime (W20).** `RunStep` records the desk task and returns pending (async completion). The human reports
  with `sindri workflow complete <address> --run <token> --outcome <o> --output …`; the API maps the token to the
  activity's task token (stored on `step.started` in the ledger) and completes it. `ScheduleToClose` is the state's
  timeout, at least `workflow.deskTimeout` in desk mode; expiry is `step.failed(timeout)` and follows §3.4.
- **Agent runtime (§6.5).** `RunStep` asks `ILauncher` for the launch and returns pending. `session wrap`'s heartbeat
  becomes an **activity heartbeat**; a missed heartbeat timeout (120 s) fails the activity with `lost`, and a wrapped
  exit without a report fails it with `exit`, so a closed tab or a crash ends the run as `step.failed` within 150 s,
  as rev 7 required. The launched session, and only it, completes the activity through `complete --run`, proven by
  the launch secret (`X-Sindri-Launch-Secret`, `SND-AUTH-003`); a taken-over instance invalidates the secret.
- **Reports** are validated as in rev 7 §5.5: an undeclared outcome, a missing required output or an undeclared fact
  is `SND-WF-008`, rejected before any decision, and uses no round. Output files travel as content (≤ 1 MiB each) and
  are stored `0600` by the API before completion.
- **`Start` after commit** holds by construction: the activity is scheduled only once the transition into the state
  is in history, and a replay never re-launches a run whose activity is already recorded.

### 5.4 Events are signals; the bridge replaces the router

Registered events (`EventDescriptor`, §2) reach instances as Temporal **Signals**.
- **The bridge service** (`workflow.events`, a built-in service with its own loop, S1-D8 and S1-D10) reads
  `ledger.db` from its cursor, accepts an event only when the envelope's actor is the descriptor's `producer`,
  resolves the target workflow id through `causation` (via the `workflow_commands` map an effect activity writes) or
  through `item` and attempt, and signals it. Deliveries of engine-origin effects signal nothing, so the engine's own
  notification can never satisfy a recipe's wait. A `route: causation` event signals only the frame entry its command
  was written for; the interpreter drops it if that entry has ended.
- **Buffering.** Signals that arrive before the matching wait are buffered by Temporal and consumed when the workflow
  reaches the wait, so rev 7's "early row" hold is native. The interpreter discards a buffered signal older than
  `workflow.eventHold` (default 24 h) with a `workflow.expired` ledger event.
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
- `RecordEvent` is idempotent on `(workflow id, run id, history event id)`, so a replay never duplicates a row. Its
  retry policy is unbounded with backoff: the workflow does not proceed until the ledger has the event, which keeps
  the ledger a complete record.
- The ledger writer runs the payload through `Aw.Redaction`. `causation` is the trigger's source; `EventFactory` takes
  the item, the causation and the request's actor (S1-D7, API P1).
- Temporal's own history is never read by the UI, the API or the evolve loop. `workflow events` and the item timeline
  read the ledger (X16).

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
- Kernel's outbox (`outbox_commands`, `IOutbox.MarkRefused`) is no longer used by the engine. The API spec keeps it for
  its own commands if it needs one; S1-D3 and K-D1 drop from this spec's S1 changes (§7.2).

### 5.8 Children, parks and resumes (behaviour kept from rev 7)

- A `child` wait starts a **child workflow** with `ParentClosePolicy.Terminate`, so a cancelled parent cancels its
  children. The child's terminal outcome arrives as the child workflow's result; `parked (propagate)` and `escalated`
  arrive as signals the child sends to its parent, and the parent pushes a mirroring suspension (reason, question,
  `childNext`). Any later signal or result from the child pops that mirror first, so a child cancelled at its own
  address, or accepted straight to `done`, still releases its parent.
- **Forwarded answers.** An `answer` to a parent whose suspension mirrors a child is an Update on the **child**: the
  nonce endpoint resolves the forwarding and mints the nonce for the child; the challenge for a `protected` answer is
  bound to the child's address. The CLI prints the child's result and the parent follows through the child's signal.
  `cancel` and `takeover` act on the parent.
- **Refusal precedence** (rev 7 §5.5) is unchanged: a `gate.budget.*` refusal parks with `budget` and releases held
  resources; an uncertain or below-threshold top-rung gate escalates; from `active` the Step re-runs below the loop's
  cap, parks with `rounds` at the cap, or parks with `needs-answer` with no loop; from a wait the event is stored as the
  withheld trigger and the instance parks with `needs-answer` (wait); a command gets `SND-WF-004`.
- **Resume actions** (rev 7 §5.8) are unchanged and are tested by name (§10): `transition` for `rounds` and
  `no-progress`, `rerun` for a Step budget park or `needs-answer` from a Step, `reevaluate` (or `rewait`) for a wait
  refusal or a budget gate refusal, `rearm` for `timeout` and drift, `restart` for `SND-WF-010`, `reemit` for a
  dispatch refusal, and `--accept` or `--reject` for an escalated gate. A resume runs its gates afresh.
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
store and `WorkflowStoreContract`, `IWorkflowStore`, `IMonotonicClock`.

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

So the app is a full desk client: it starts workflows (scoping included), answers parks, takes over, hands back,
cancels, completes desk tasks and hands work off to a session (API P10, `OpenItem`). `protected` actions show the
CLI to copy until passkeys exist.

**The agent variables are probed, not guessed.** P2 records what each harness actually sets (Claude Code, Codex in
each sandbox mode, Cursor's agent and its integrated terminal) and pins the result in a golden test. That list is the
one API §6 reads. The probe records the whole process ancestry, not just names: Claude Code can run as `node`, and
Cursor's app hosts both the human's terminal and its agent, so the parent-process check matches the ancestry pattern
the probe pinned. A variable that humans also see is excluded. `sindri doctor` re-probes Claude Code, and **warns**
(never `ok`) when the Codex or Cursor version differs from the pinned probe; those probes are **(owner)** steps in P2,
run with `sindri-prove.sh human doctor --probe` inside each harness. A refusal (`SND-AUTH-002`) names the variable or
process it found, so a human in an IDE terminal knows why. The table is honest-mistake grade (A 17.28.3).

### 6.3 Verbs

| Verb | Class | Prints |
|---|---|---|
| `recipe validate <file> [--json]` | local | Rule lines, then the hash |
| `recipe eval <file> [--json]` · `--scaffold <file>` · `--record <address> --out <file>` | local · local · query | Case results · a generated happy-path eval (first matching transitions, first declared outcome) · an instance's history as an eval |
| `recipe register <file>` | desk | `registered workflow.step@9ab2d40c1e77 (unapproved)` |
| `recipe show <id@hash>` | query | The preview |
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

**Routes** (under S1's `/sindri` group; each is one slice through API's chain, with a catalog row):

| Route | Slice | Class |
|---|---|---|
| `GET /workflow/recipes` · `GET /workflow/recipes/{id}@{hash}` | `ListRecipes` (with each recipe's `inputs`, for the app's start form) · `ShowRecipe` (the preview) | query |
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
order is fixed and empty lines are left out.

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
age is a day or more. Plain output is ASCII, and `commands` always lists the instance's own verbs first, in the
order `answer`, `complete`, `handback`, `takeover`, `cancel`. Golden files pin every layout in §6.4, plus one per
§3.8 row.

### 6.5 The agent runtime (owner decision, 2026-10-11)

The runtime that runs a Step in an agent session and takes its result back, so the factory line can run unattended.
It uses API §8.1's launch contract; nothing here launches anything itself.
- **Launch.** For a Step whose descriptor names an agent runtime, the post-commit loop asks `ILauncher` for a
  `process` launch (the always-on `--headless` box has no Warp), with only ids: the instance, the run token, the repo,
  and a fresh per-run worktree (`<repo>/.claude/worktrees/sindri-<instance>-<run>`). The target and its permission
  mode come from the profile (`workflow.agent.target`: Claude Code, Codex or Cursor; `workflow.agent.permissionMode`).
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
  `codex exec`) are never used for a Step, because they cannot be joined. `tmux capture-pane` gives the UI a scrubbed,
  read-only preview later (the collector spec, README follow-ups).

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
| SND-RECIPE-001 | invalid | Validation failed | Fix each `RCP-*` line |
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

## 9. Validator and eval format

### 9.1 Rules mapped to §17.28.5

Line format: `RCP-…: <what> at <file>:<line>:<col> (<path>); <fix>`. Each rule has an invalid fixture.
In-process validation resolves a `child` recipe from the registered recipes when served, or from the same
directory when not (so `workflow.item.yaml` finds `workflow.step.yaml`).

| §17.28.5 | Rules |
|---|---|
| Schema | `RCP-SCHEMA-001`–`003`, `RCP-STATE-001`, `RCP-INIT-001`, `RCP-DUR-001` |
| Blocks registered | `RCP-BLOCK-001`–`003`, `RCP-EVID-001`, `RCP-EVENT-001` |
| Inputs upstream | `RCP-WIRE-001`, `-002`, `RCP-WHEN-001`, `RCP-OUTCOME-001`, `RCP-CHILD-001` |
| Capability | `RCP-CAP-001`–`007` |
| Reachable | `RCP-REACH-001` |
| A way out | `RCP-EXIT-001` |
| Timeouts | `RCP-TIME-001` |
| Loop caps | `RCP-LOOP-001`–`003` |
| Allowlisted effects | `RCP-EFFECT-001` (`skipped` without the profile key) |
| Hash | `RCP-HASH-001` |
| Added | `RCP-TRIG-001`, `RCP-WHEN-002`, `RCP-THRESH-001`, `-002`, `RCP-NEXT-001`, `-002`, `RCP-CHILD-002` (cycle), `-003` (depth > 4), `RCP-VERSION-001`, `-002`, `RCP-TRIG-002` (reserved verb), `RCP-TIME-002` (`retry` on a child wait), `RCP-RES-001` (information: a resource with no allocator in this build) |

### 9.2 Eval format

`evals/<recipe id>/<case>.jsonl`:
- line 1: `{ "start": { "inputs": { … } } }`;
- each later line: `{ "trigger": { "kind", "outcome"?, "outputs"? }, "gates"?: { "<block>": "pass|fail|uncertain" },
  "expect": { "state", "park"?, "outcome"? } }`.

`recipe eval` folds the decisions over the triggers and reports the first line that diverges. `--scaffold`
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
| `item.routed` | `router.stub` | `startable`, `notStartable` | `fact:stepSpec` |
| `claim.created` | `system:dispatcher` (route: causation) | `created`, `claimExists` | — |
| `notify.delivered` | `system:dispatcher` (route: causation) | `delivered` | — |

- **`fact:item`** is `{ id, repo, spec }`, where `id` is the `ItemId` (API §3), `repo` is a key of the profile's `repos`
  and `spec` is the Step spec text. `effect.claim.ref` creates
  `sindri/{user}/{item}` in that repo, pointing at its default branch's head, so the repo needs one commit.
- **`workflow.route`** (P7) is `{ startable: all | none }`. The `router.stub` service fires on each
  `workflow.started` of `workflow.item` and emits `item.routed` with `outcome` taken from that key, and with
  `fact:stepSpec` copied from `fact:item`'s spec.

```yaml
schema: 1
id: workflow.item
version: 1
inputs: [fact:item]
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
inputs: [fact:spec]
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
unchanged. P0 is new and gates P4.

### Parallel execution with an agent team

```
P0 Temporal spike ──────────────────────────────────────────────┐
P1 model + validator (merged) ──┬──► P2 registry + approval (S1, API P1) ──┤
                                └──► P3 pure decisions + evals ───────────┴──► P4 interpreter, worker, desk Steps, bridge (API P3) ──┬──► P5 timers, takeover ─┐
                                                                                                                                    └──► P6 effects ─────────┴──► P7 children + item ──► P8 agent runtime (API P10, Judge #156)
```

| Wave | Parallel | Starts when |
|---|---|---|
| 0 | P0 ∥ P2 ∥ P3 | Now. P2 also needs S1 merged and API P1 |
| 1 | P4 | P0 passed, P2 and P3 merged, API P3 merged |
| 2 | P5 ∥ P6 | P4 merged |
| 3 | P7 | P5 and P6 merged |
| 4 | P8 | P7, API P10a and Judge (#156) merged |

These engine-local waves sit inside the cross-spec order in `docs/sindri/README.md`, which decides what starts when.

**Units:** each phase starts with one contract unit (records, ports, stubs, committed red). The other units then fan
out, one implementer each:

| Phase | Contract unit | Parallel units |
|---|---|---|
| P0 | The spike worktree, `Temporalio` in `Directory.Packages.props`, `Aw.Sindri.Workflows` project skeleton, `binaries.lock` entry for the `temporal` CLI | (a) the dev server under `sindri serve` and `doctor` · (b) a throwaway interpreter for `workflow.step` with a desk Step as an async activity · (c) the ledger mirror and the eval cases on the time-skipping server · (d) the AOT split and footprint measurement |
| P2 | Ports, fakes, contract bases, the challenge shell, the engine's catalog rows on API P1's chain, the agent-variable probe | (a) SQLite adapters for `recipes` and `recipe_approvals`, S1-D9 · (b) register, show, list, `--stale` · (c) approve (including drift re-approval), revoke with previews, cascade; the revoke and drift **signals** are stubs until P4 · (d) pins, `RCP-VERSION-*`, registry events, the architecture test · (e) the harness probe and its golden test |
| P3 | Instance state records, triggers, decision signatures | (a) `TransitionDecision`, `RefusalRules` · (c) `LoopRules`, `BudgetRules`, grants · (d) `ChildPropagation` · (f) `recipe eval`, `--scaffold`, `--record` format. Units (b) and (e) of rev 7 are dropped |
| P4 | `RecipeWorkflow` skeleton with the Query and the Update signatures, the worker host inside `sindri serve`, `RecordEvent`, the `workflow` group table | (a) the interpreter's state loop over `TransitionDecision`, search attributes · (b) start, Updates, nonces (API-side table), the §5.8 resume actions · (c) desk runtime: `RunStep` async completion, `complete`, task-token map on `step.started` · (d) the bridge service (S1-D8, S1-D10) · (e) status, list (visibility), events (S1-D6), golden layouts, approve running the evals, S1-D11 (`deskTimeout`, `eventHold`) |
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

### Phase 0: the Temporal spike (3 days)

- **Goal:** prove, with numbers, that Temporal meets the three constraints the walkthrough named before any engine
  code depends on it. Thrown away afterwards except `binaries.lock`, the package pin and the measurements, which land in
  `docs/csharp/sindri-workflow.md`.
- **Files:** a worktree on `main`; `Directory.Packages.props` (+ `Temporalio`); `src/Sindri/Aw.Sindri.Workflows/`
  (skeleton, `AwAotCompatible=false`); `binaries.lock` (+ `temporal` CLI version and sha256); the spike's notes.
- **Size:** M (3 days, one builder with agent help).
- **Day 1:** `sindri serve` starts `temporal server start-dev` on loopback with its SQLite file under
  `$AW_SINDRI_HOME`, records the pid, and `doctor` reports it. A throwaway `RecipeWorkflow` runs `workflow.step` with
  the three desk Steps as async-completion activities; `sindri workflow complete --run 1.1` completes the first one.
- **Day 2:** the ledger mirror (`RecordEvent`) writes `workflow.*` rows the API's timeline can read; the three
  `workflow.step` eval cases (`happy`, `no-progress`, `rounds`) run on the time-skipping server; a `4h` timeout fires in
  under a second of wall time; the worker is killed mid-Step and the instance resumes with the same run token.
- **Day 3:** the AOT split: `dotnet publish` of the Sindri host framework-dependent with `Aw.Sindri.Workflows`
  referenced, and `IsAotCompatible` still true on every other library (zero `IL*` warnings outside the adapter).
  Footprint: RSS of the dev server and of `sindri serve` with the worker idle and with ten open instances, measured for
  an hour beside Ollama; the start-to-ready time of the server. Visibility: `workflow list` as a search-attribute query.
- **Pass:** every line below holds, written into the notes with its number.
  1. The server starts and stops with `sindri serve`, loopback only, its file under `$AW_SINDRI_HOME`, and survives a
     host sleep with the instance intact.
  2. Async completion, timers and the kill-and-resume case work as stated; the eval cases pass on the time-skipping
     server in under 10 s total.
  3. The ledger holds one `workflow.transition` per transition after a replay (no duplicates).
  4. Dev server RSS ≤ 300 MB idle and ≤ 500 MB with ten instances; worker overhead inside `sindri serve` ≤ 150 MB;
     server ready in ≤ 5 s.
  5. The AOT split builds with zero warnings and the host publishes framework-dependent; the Avalonia app still
     publishes NativeAOT.
- **Fail:** any line fails and can't be fixed inside the three days. Then rev 7's §5 returns as the design, cut to
  what `workflow.item` and `workflow.step` need: no two-deep stack and pending parks, no drift wakes, no forwarded
  child answers in v1. The owner decides; the spike notes record which line failed and why.
- **Signal** (all **(agent)**): the five pass lines, each with its measurement, in `docs/csharp/sindri-workflow.md`
  under "Temporal spike (P0)", and a green `dotnet build -warnaserror`.

### Phase 1: the recipe model and validator

Merged (#179). Unchanged by rev 8.

### Phase 2: registry and approval

- **Goal:** recipes are registered by hash and approved or revoked through a previewed, challenged desk action, with
  pins and cascade. The revoke and drift **wakes** are stubs (a port with a no-op adapter) until P4 supplies the signal
  sender.
- **Files:** ports and slices; SQLite adapters and migrations for `recipes` and `recipe_approvals` (with S1-D9); the
  challenge; registry events; contract bases; the architecture test; docs.
- **Size:** M.
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

### Phase 4: the interpreter, the worker, desk Steps and the bridge

- **Goal:** instances run as `RecipeWorkflow` executions on the local Temporal server; commands are Updates; desk
  Steps complete through async activity completion; registered ledger events reach instances as signals; every
  transition is mirrored into the ledger.
- **Files:** `Aw.Sindri.Workflows` (`RecipeWorkflow`, `RunStep`, `RecordEvent`, `LoadRecipe`, the worker host); the
  slices for start, command, complete, nonce, get, list, the bridge; the actor table; S1-D6, D7, D8, D10 and D11
  (`deskTimeout`, `eventHold`); golden layouts; approve running the evals; the revoke and drift signal sender behind
  P2's port; docs.
- **Size:** L.
- **Dependencies:** P0 passed, P2 and P3 merged, API P3 (items) merged.
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
  target's usage reader; the `workflow.agent.target` profile key; docs.
- **Size:** M.
- **Dependencies:** P7, API P10a (`ILauncher` and §8.1), Judge's provider port (#156). This phase adds Sindri's
  `deps.yaml` edge to Judge with a recorded fan-out exception (Sindri is at 4), reviewed like Context's.
- **Signal:** with `workflow.agent.target: claude-code`, starting `workflow.step` on a new item launches Claude Code
  **interactively inside a tmux session named `snd-<run>`**, in a per-run worktree, with the seed as the initial
  prompt. **(owner)** `sindri open <item> --attach` opens a Warp tab attached to that tmux session; typing one line
  into it records a `takeover` and `status` shows `taken-over`; `sindri workflow handback` resumes it. The session's
  `complete --run` moves the instance to `checks`, and the event records `via: agent` and the token usage. The same
  `complete` from another session exits 2 with `SND-AUTH-002`. Killing the tmux session parks the Step through
  `step.failed(exit)` within 150 s.
