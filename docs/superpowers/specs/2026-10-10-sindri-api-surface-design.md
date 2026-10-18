# Sindri API surface: commands, queries, the event stream and the MCP server

Status:
- Approved in conversation, decision by decision, on 2026-10-10.
- Revised the same day after `/autoplan`: rev 2 after review pass 1, rev 3 after pass 2, an edit after pass 3, an edit
  after pass 4, and a final edit after pass 5. The owner allowed five passes, and all five lenses passed pass 5. §13 lists every finding and its resolution.
- Revised 2026-10-11 in the Sindri app epic (`docs/sindri/README.md`, X1–X17): one MCP endpoint shared with the
  context engine (§8), `ItemId` is always `ITM-…` (§3), the engine adopts this spec's callers and classes (§3.5),
  one OS notifier (§5.1), the context routes' classes (§3.7), launch targets for `sindri open` (P10), engine item
  states (P11), and exit code 75 (§9). A second batch (X19–X33, after the epic's /autoplan) renumbers the new
  codes past S1's (`SND-API-011`, `-012`), adds the `step-report` class and the agent runtime's launch port (§4, §8,
  P10), the nonce and challenge carve-outs (§2.5), the engine-to-item state map (§3.6), launch-time MCP config for
  Codex and Cursor (§8), and the `human` token before the app's first write (§6). A third batch (X35–X42, after the
  epic's second /autoplan) adds the launch contract (§8.1: `session wrap`, the launch secret, two target kinds (rev 8 made it one, tmux-owned),
  liveness), builds the `human` token in P1, fixes the queue groups and adds `answerKind` (§3.6), and adds the
  `issue` kind to §4.
- **The UI app design spec (`2026-10-11-sindri-ui-design.md`, approved) amends this spec** through its §6.7 edits
  E-1…E-19 and §18.1, which say where each lands. Where they differ from the text here (the launch surface,
  `ItemView.launch`, `NeedsYouRow.actions` and `label`, catalog fields, `health`, `GET /events/catalog`, golden JSON),
  the UI spec wins (README X52).
- **Revision note (2026-10-14):** the UI screens spec's (`2026-10-13-sindri-ui-screens-design.md`) edits E-20, E-22 (API
  half), E-23, E-24, E-25, E-28, E-29 (API half) and E-30 (API half) are written in; sections: §3.1 (`GET /status`), §3.4
  (`session.usage`), §3.5 (ownership rows), **§3.8 (new: the instance, recipe, stats, engine, dependency and budget
  views)**, §3.6 (the Agents note), §8.1 (usage sampling, budgets), §9 (`SND-LAUNCH-010`, `SND-API-013`,
  `SND-ENGINE-001`, `SND-ENGINE-404`), §11 (the Agents note) and the Phases table (P2, P10a and P11 extended; **P12 recipe
  stats** and **P13 engine** appended). Where this text differed, the UI spec wins (README X52) and the older text was
  changed, not kept beside it: the engine spec's `{instanceId}` and `{id}@{hash}` routes stay valid but the addressed
  forms are the documented ones (§3.8); §8.1's "usage is read when the session ends" became periodic samples as well,
  so a live session counts against a budget; the Agents view's "fed later" line now excepts the budget panel. E-21,
  E-26, E-27 and E-31 are the workflow and context specs' edits, and the ledger column for E-30 and the doctor's
  `consumers` declaration for E-29 are cross-referenced, not written here.
- This document is the written spec for review.

The spec designs the surface every Sindri client uses: the `sindri` CLI, the Avalonia app (`SindriUi`), the Sindri MCP
server (`/sindri/mcp`), and the workflow engine's calls into the command layer.
- `/specToProvenPR` takes the **Phases** at the end and turns them into PRs, using an agent team.
- The UI design builds on §3, §5 and §3.6.

**Builds on:**
- The factory addendum (`2026-10-09-sindri-factory-addendum.md`):
  - §17.27.1, one command layer;
  - §17.27.2, action classes;
  - §17.27.4, the command registry;
  - §17.21, the event envelope and the API;
  - §17.29, the application, `sindri open`, and MCP calls being requests;
  - §17.12 and §17.23.7, decisions.
- The core design (`2026-10-07-sindri-design.md`): §9.3 (takeover) and §10.3 (the CLI).
- The dashboard spec (`2026-10-09-sindri-dashboard-design.md`): what the UI shows.
- The workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`): it owns instances, park, takeover and
  handback (§3.5).
- `.agents/rules/csharp.md` and `docs/csharp/scaffolds/new-slice.md`.

**Fixed, from stage S1** (the app shell; branch `feat/sindri-s1-app-shell`; contract `docs/csharp/sindri.md`):

| Area | What S1 fixes |
|---|---|
| Routes | `GET /sindri/status`, `GET /sindri/ping`, `POST /sindri/stop`, `POST /sindri/profile/approve` and `GET /sindri/events` |
| Host | `LoopbackBinding`, `LoopbackGuard` and `HttpResults` |
| `Core` types | Those under `Control`, `Ledger`, `Lease`, `Profile`, `Scheduling` and `Doctor`, including `EventFactory` |
| `SindriErrors` | `SND-LEASE-001…003`, `SND-PROFILE-001…005`, `SND-LEDGER-001/002`, `SND-API-001…005`, `SND-CLI-001/003` and `SND-SCHED-001` |
| Process model | `sindri serve`, `control.json`, the spawn, and the pid and epoch check against `/ping` |
| CLI-local verbs | `doctor` and `profile validate` |

This design adds to these. Where it has to change one of them, §10 lists the conflict.

## 1. Decisions

| # | Question | Decision |
|---|---|---|
| D1 | Resource model | **CQRS** over resource paths:<ul><li>commands are `POST`s that append one domain event, plus the audit row, and return `CommandAccepted`;</li><li>queries are `GET`s that read projections;</li><li>one route = one slice = one `IService` (§2, §3).</li></ul> |
| D2 | Commands vs queries, decisions vs requests | <ul><li>Every command declares an **action class**.</li><li>Every caller has a **caller class**, and humans also have a **terminal** flag.</li><li>One table authorizes the combination.</li><li>Sessions may run queries, file **requests** and **pause** automation, but never make decisions (§4).</li></ul> |
| D3 | Event stream | **Server-Sent Events**, `GET /events?after=`. The cursor is the ledger sequence, and events go out in a public envelope. It's a "go re-read" signal for live clients, never the source of truth (§5) |
| D4 | Local auth | **v0 is local-only, with one token:**<ul><li>the caller class comes from `X-Sindri-Caller`, and a missing header counts as `session`;</li><li>only the CLI at a terminal and the app send `human`, and `human` also needs the per-start `X-Sindri-Token` (P1, §6);</li><li>the CLI also sends `X-Sindri-Terminal: 1`;</li><li>commands refuse any `Origin` header.</li></ul>Session-class tokens, passkeys and the tailnet come later (§11) |
| D5 | Finding or starting the process | S1's `control.json`, spawn and ping check, plus one rule for which verbs may start the process. Nothing run inside an agent session starts it (§7) |
| D6 | MCP server | **One MCP endpoint, `/sindri/mcp`**: Streamable HTTP in stateless mode on the API port (the MCP C# SDK), shared with the context engine's `context_*` tools. Every call is stamped `session`. It offers queries and `sindri_request` only. **It is proven by a real Claude Code subagent**, run as a fresh `claude -p` process (§8) |
| D7 | Webhooks | **Cut for now** (§11) |
| D8 | Errors, versioning, JSON | <ul><li>S1's `Result` → `ProblemDetails` mapping stays unchanged, plus extension members (§9).</li><li>New codes are reserved per phase and documented in `docs/csharp/sindri.md`.</li><li>No version in the path in v0.</li><li>One JSON context.</li><li>`--json` prints the API body unchanged (§9).</li></ul> |

## 2. The CQRS model

### 2.1 Write side: commands

**What a command is.**
- A command is a request record that implements the marker `ICommand<TResult>` (`Aw.Sindri.Core.Commands`).
- `TResult` is either `CommandAccepted`, or a record that carries it plus the ids the command created.
- It is served by an `IService<TCommand, TResult>`, like every slice.
- A command whose class depends on its arguments implements `IClassifiedCommand.ActionClass`, with one fail-closed
  rule: **protected fields override the variant, and an unknown variant is `protected`**. For the engine's
  `ApplyCommand`:
  - any request carrying `grant`, `grantTokens` or `accept` is `protected` (`answer-grant`, `answer-grant-tokens`,
    `answer-accept`), and those fields are invalid on any variant but `answer`;
  - `CompleteStep` is `desk` from a human, and `step-report` when the launched agent session for that run reports it (§4.1);
  - `takeover` is `pause`;
  - `answer`, `handback`, `cancel` and `complete` are `desk`.

  A unit test covers every variant, and the engine spec adopts the rule (§3.5).

**What a handler does.**
1. It reads the state it needs from projections (§2.4 says when it waits for them).
2. It decides with a pure method on a `Core` type.
3. It appends **exactly one domain event** under the process's lease grant (S1's `ILedgerStore.Append`).

Everything that follows from that event, such as a request's pending decision, is derived in the projections. The
audit row (below) is a separate append.

**Handlers never do slow work.**
- A handler decides and appends; it never calls a model, a judge, git or the network.
- Slow work runs as a job (§3.2, P6) or as an engine Step runtime after the command is accepted.

**`CommandAccepted(Guid CommandId, long Sequence)`.** `Sequence` is the ledger sequence the domain event was written
at. It's the read-your-writes token in §2.4.

**Commands run one at a time.** A process-wide `CommandGate` serializes every command. It's a `SemaphoreSlim(1)`,
acquired with a 5 s limit; past that, the command fails with `SND-API-009` ("busy", 503). Commands decide and append
only, so the gate is never held for long.

**Waiting for the views.**
- A command that reads views (catalog flag `ReadsViews`) waits, inside the gate, until the projector's cursor reaches
  the ledger head. It reads the head from `ILedgerStore.Head()` itself, so it never decides on views that miss the
  previous command's event. The limit is 2 s, after which it fails with `SND-API-011`. While the projector is `stalled`,
  it fails at once with `SND-VIEW-001` instead of holding the gate; during a rebuild, with `SND-API-010` (§2.3).
- `StopProcess` and `RebuildViews` read no views and **don't wait**. So the fix for a stalled projector, and stopping
  the process, both always work.

**Fact keys make conflicts lose in the ledger.** Each event's idempotency key names the *fact*, not the request:

| Event | Key |
|---|---|
| `decision.recorded` | `decision.recorded:<decisionId>` |
| `item.state-changed` | `item.state-changed:<itemId>:v<newVersion>` |
| `request.filed` | `request.filed:<requestId>` |

- If two writers race anyway, S1's `Append` returns `AlreadyRecorded` with the earlier event's sequence.
- The handler then reads that event. Every domain payload carries `commandId`, so when the event's `commandId` isn't
  the handler's own, the result is `SND-ITEM-409`: "decided elsewhere", or "changed since you looked".
- There is **no client `Idempotency-Key` header in v0** (§11). Commands that create things aren't deduplicated, and the
  CLI never retries a command on its own.

**The command chain.** One helper, `AddCommand<TCommand, TResult, TService, TValidator>()` in `AddSindriFeatures()`,
registers these decorators, so no command can skip them. Every command needs a validator; the default
`AcceptAllValidator<T>` is allowed. A test resolves every registered `ICommand` and asserts this order, outermost
first:

| # | Decorator | What it does |
|---|---|---|
| 1 | `CommandGate` | Serializes commands, as above |
| 2 | `AuditingService` | <ul><li>Wraps everything inside it.</li><li>Appends `command.handled {commandId, command, class, caller, outcome, code?}`, keyed `command.handled:<commandId>`.</li><li>Writes a row for every attempt that reaches the chain: accepted, refused, invalid or failed.</li></ul> |
| 3 | `AuthorizingService` | Checks the command's action class against the caller (§4). It runs before validation, so an unauthorized caller learns nothing about valid input. A refusal returns `SND-AUTH-002` and writes nothing except the audit row |
| 4 | `ValidatingService` (Kernel) | Runs the slice's `IValidator` |

Requests the host can't bind never reach the chain. That covers malformed JSON, a bad id, a missing `Content-Type`,
and a body over 64 KiB. They get 400 or 415, and the host logs them; they aren't audited.

**When the audit can't be written.**
- **`StopProcess` is self-audited on its accepted path.** Its handler releases the lease, so no row could be written
  after it. It is flagged `SelfAudited` in the catalog, and S1's `process.stopped` event is its audit. A **refused**
  `stop` is audited like any other command, because the lease is still held.
- **Host-internal callers.** S1 calls the stop handler in-process on SIGTERM, SIGINT and a taken port (`ServeCommand`).
  After P1 that call goes through the chain with the caller `service:host`, which the catalog allows for `StopProcess`
  and nothing else. So Ctrl-C on `sindri serve` still records `process.stopped` and releases the lease (a P1 test).
  Only `ServeCommand` sets `service:host`, explicitly.
- **No context is refused.** `AuthorizingService` refuses a command with no caller context (`SND-AUTH-002`, audited as
  caller `none`). Every other in-process writer sets its own `service:<name>`: the scheduler per service, the projector,
  `profile-watch` and the job runner (`service:jobs`).
- **Events written with no context** (for example by S1's internal services below) keep S1's stamp, `host:sindri`.
  `EventFactory` never makes up a `service:` or `human:` actor.
- **S1's internal services stay outside the chain.** `StartProcess` and `RecordHeartbeat` are called by the host itself
  (`ServeCommand`, the scheduler). They aren't commands, have no route, and keep writing through `EventFactory` with
  `service:host` and `service:heartbeat`.
- **An audit write fails after an accepted command.** The command's result is still returned, because its domain event
  is already recorded, and the failure is logged as `SND-LEDGER-002`.
- **An audit write fails on a refused or invalid command.** The caller gets 503 `SND-LEDGER-002`.

**The caller context.**
- `CallerContext(CallerClass Class, bool Terminal, string Actor, Guid CommandId, Guid CorrelationId)` is filled by the
  Host from the request (§6). In-process callers, the engine and the scheduler, fill it themselves.
- It is reached through a singleton `ICallerContextAccessor`, which is `AsyncLocal`-based like
  `IHttpContextAccessor`. That way S1's singleton `EventFactory` can read it (conflict C6).
- `Actor` is composed by the host, never taken from the caller (the C# Seams rule). Its values are:
  - `human:cli`, `human:ui`, `human:prover`, `human:http`;
  - `session:mcp`, `session:cli`, `session:http`;
  - `service:<name>`.

### 2.2 Read side: queries and projections

**What a query is.** A request record that implements `IQuery<TView>`, served by `IService<TQuery, TView>`.

**What a query handler reads.**
- Projections only, through read ports such as `IItemViews`, `IQueueView`, `INeedsYouView` and `IDecisionViews`.
- Their adapters open a read-only connection (`SqliteConnectionFactory.OpenReadOnly`).
- A query handler takes no write port and appends nothing.
- The one exception is `ReadLedger` (§3.1). It reads through `ILedgerReader`, a read-only port whose adapter opens
  `ledger.db` read-only.

**Where projections live.**
- In their own SQLite file, `$AW_SINDRI_HOME/views.db` (0600), separate from `ledger.db`.
- Each view is a table keyed by its id.
- A `projector_state` row holds the cursor, the schema version and the stall state.

**Freshness: every view response carries `asOf`.**
- It holds `{ cursor, ledgerHead, projectedAt, rebuilding, stalled }`.
- `ledgerHead` comes from the projector's in-memory status (`IProjectorStatus`), not from the ledger store. `views.ledgerHead` on GET /status is the exception: it is the ledger's current head, read fresh; `cursor` is the projector's, so the gap is the lag. Query answers' `asOf` keep the projector's view.
- `projectedAt` is when the cursor last moved. It is the clock behind the UI's "updated Ns ago".
- With `asOf`, a client can tell "empty" from "still loading" from "views are broken" (§3.6).

**Times, not ages.** Views carry `since` and `updatedAt` timestamps, never an `age`. That keeps a view byte-stable;
clients compute ages themselves.

### 2.3 The projector

**How it runs.** It is an in-process hosted service in the Host, not an S1 `IScheduledService`. In a loop it:
1. calls `ReadAfter(cursor, 100)`;
2. applies each event to every registered `IProjection` whose types match;
3. stores the new cursor.

Each page is one `views.db` transaction. After a full page it continues at once; it sleeps on the ledger signal (§5.5),
or for 1 s, only once it has caught up. While a rebuild runs, commands that read views fail at once with `SND-API-010`
("views rebuilding, retry") instead of waiting. The command wasn't run, so clients show "retry", not "done".

**Where the ledger head comes from.**
- S1's `ILedgerStore` gains an additive `Head()` member (conflict C9) with a contract test. The projector calls it each
  loop and publishes the value through `IProjectorStatus`.
- Queries read the head from `IProjectorStatus`. Commands read it from `ILedgerStore.Head()` (§2.1), which is always
  current.

**Projections are pure folds.**
- Each is `Apply(row?, event) → row?`, unit-tested from lists of events.
- One projection may write rows in several views from one event. For example, `request.filed` creates both the request
  row and its pending decision row.

**Rebuild.** Three things trigger a rebuild: `sindri views rebuild` (desk class), a schema-version change, and a missing
`views.db` at startup.
- A rebuild sets `rebuilding`, empties the view tables, resets the cursor to 0 and replays the ledger.
- While it runs, queries still answer, with `asOf.rebuilding = true`. The UI shows its loading state, and the CLI
  prints the `EmptyText` "rebuilding" sentence (§3.6).
- A new or changed read model is therefore a replay, never a data migration.

**Failure.** A projection that throws on an event stops the projector at that sequence:
- it sets `stalled`, logs `SND-VIEW-001` with the sequence and event type, and retries every 30 s;
- when the projector can't run at all (views.db can't be opened, written or reset, or the ledger can't be read), no event is to blame: it publishes `stalled` with a null `stalledAt` and a `stalledType` of `<code>: <detail>`, the failing call's own code (`SND-VIEW-001` for views.db, `SND-LEDGER-00x` for the ledger), which clears with the next pass that works (health `stalled`, `views.text` "The views can't be updated (...): run `sindri views rebuild`", or run `sindri doctor` when the ledger is what can't be read). Every reader says the same: the CLI's `views:` line, the `empty` sentence of the list answers and the SND-VIEW-001 and SND-API-011 messages name "can't be updated (...)", never an event number. A `GET /status` that can't read the services view adds a `SND-VIEW-001` problem to `health` ("the views can't be read") instead of reporting no services (unless the views are already stalled, which is the views problem);
- `GET /status` reports `views` in the same shape as `asOf`, plus the stall: `projectedAt`, `cursor`, `ledgerHead`, `rebuilding`, `stalled`, `stalledAt` and `stalledType`. It also carries `views.text`, the rebuilding or stalled sentence (§3.6), which is `null` when the views are fresh. It also carries `health` (UI spec E-14): `word` (`stalled`, then `rebuilding`, then `degraded`, else `healthy`), `problems` (each `code`, `text` and `clause`, in a fixed order: not running, views, failed services, profile) and `sentence`, "Sindri is degraded: A, B, and C." built from the problems' `clause`s (`null` when healthy). `sindri status` exits 1 unless the word is `healthy`;
- `sindri doctor` gains a `views` line (after `ledger`): `cursor N of head M` when ok, a warn while rebuilding or before the first build, and a fail with the fix `sindri views rebuild` when stalled;
- queries answer with `asOf.stalled = true`, and the UI shows its error banner;
- `RebuildViews` doesn't wait for the head (§2.1), so the fix always runs.

**Why asynchronous.** Commands are serialized and wait for the head, so the async projector gives the same answers an
inline one would. Inline projection stays a later option (§11).

### 2.4 Read-your-writes

**Queries.** Every query accepts `X-Sindri-After: <sequence>`. The handler waits on the projector's progress signal
until the cursor is at or past that sequence. After 2 s it returns `SND-API-011` (503), naming the cursor and the
target.

**Commands** that read views wait for the head before deciding (§2.1).

**Clients.**
- The CLI sends the `Sequence` of its own previous command when one verb chains a command and a query.
- The UI sends the last `Sequence` it received. If it gets `SND-API-011` after an accepted command, it keeps its "done"
  confirmation, shows the view as stale, and re-queries on the next event.
- When the UI is on the event stream, it marks views stale if no event or ping has arrived for 30 s.

### 2.5 Architecture tests (`tests/Aw.Architecture.Tests`)

| Id | Rule |
|---|---|
| `ARCH-CQRS-001` | <ul><li>Every `MapGet` endpoint resolves an `IService<TQuery, TView>` where `TQuery : IQuery<TView>`.</li><li>Every `MapPost` resolves an `IService<TCommand, _>` where `TCommand : ICommand<_>`.</li><li>S1's `GET /ping` and `GET /status` are queries, and `GET /events` is the stream query.</li><li>Its only carve-outs are the engine's two issuing routes, which append no domain event: `POST /workflow/instances/{a}/nonce` (allowed to any caller whose class may run a command on that instance, so a session can get one for `takeover`) and `POST /workflow/challenges` (`human` + terminal only). `session wrap`'s heartbeat and exit calls (`POST /sessions/{launch}/heartbeat`, `/exit`, proven by the launch secret) are the third and fourth. All have catalog rows of kind `issue` (§4.1) and are audited by `command.handled`.</li><li>It is checked over the endpoint data source in an integration test, because minimal-API lambdas aren't visible to ArchUnitNET.</li></ul> |
| `ARCH-CQRS-002` | No query handler's constructor takes `ILedgerStore`, `ILease`, `IApprovals` or a projection writer |
| `ARCH-CQRS-003` | Every `ICommand` (and every action variant) has a row in the command catalog (§3.3) |

Each rule gets a rule record under `rules/engineering/csharp/` in P1 (`node scripts/check-rules.mjs`).

## 3. Resources, endpoints and commands

All routes sit under S1's group `/sindri`; the tables below show them without it.
- Request and response records live in `Aw.Sindri.Features.<Slice>`, and every one is registered in
  `SindriJsonContext`.
- **Class** is the action class (§4).
- **Slice** is the slice name (§12 says how it is scaffolded).
- **Phase** is the PR that adds it.

New typed ids are strings: a prefix plus a compact GUIDv7 from `IIdGenerator`. Each follows `docs/csharp/typed-ids.md`
and gets its route-binding test.

| Id | Format |
|---|---|
| `ItemId` | Always `ITM-…`. A tracker key such as `ENG-142` is an alias: routes that take an `ItemId` also accept a linked key and resolve it, and responses carry the `ITM-…` id. Linear keys change when an issue moves team, so they are never the id (context spec D16) |
| `RequestId` | `REQ-…` |
| `DecisionId` | `DEC-…` |
| `JobId` | `JOB-…` |
| `RepoName` | The repo's name |

### 3.1 Queries

Every list view also returns `empty`, the `EmptyText` sentence (§3.6) for its current state.

| Route | Slice | Request | Response | Phase |
|---|---|---|---|---|
| `GET /status` | `GetStatus` (S1) | — | S1's response, plus `apiVersion`, `health` (§2.3) and `views` (§2.3: the `asOf`-shaped `projectedAt`, `cursor`, `ledgerHead`, `rebuilding`, `stalled`, `stalledAt`, `stalledType`, and `text`, the rebuilding or stalled sentence, `null` when fresh) (`dependencies`, the dependency graph with health and the impact closure of what is failing, E-29, §3.8, is not in P2: it lands with E-29, the rev-8 API follow-up after #190) | S1; extended in P1 and P2 |
| `GET /ping` | `PingProcess` (S1) | — | S1's `{pid, epoch}` | S1 |
| `GET /commands` | `ListCommands` | — | `CommandList(rows)`. Each row: <ul><li>action id (`<Command>` or `<Command>:<variant>`);</li><li>class;</li><li>allowed callers;</li><li>whether it needs a terminal;</li><li>method and route;</li><li>CLI form;</li><li>UI status: `ready`, `pending:<phase>` or `cli-only:<reason>`.</li></ul> | P1 |
| `GET /services` | `ListServices` | — | `ServiceList(services, empty, asOf)`. Each service: <ul><li>name;</li><li>cadence;</li><li>paused;</li><li>last run (slot, outcome, code);</li><li>next slot;</li><li>`job`: the running job of the service (a manual run), or null **(provisional owner default, 2026-10-15)**.</li></ul> | P2, P6 |
| `GET /profile` | `GetProfile` | — | `ProfileView(asOf)`: <ul><li>approved hash and when it was approved;</li><li>live hash, as of the last drift check;</li><li>whether it changed since approval;</li><li>the changed key paths.</li></ul> | P2 |
| `GET /queue` | `GetQueue` | — | `QueueView(groups, empty, asOf)`: the §3.6 groups in their fixed order, each a list of `ItemSummary` | P3 |
| `GET /items?state=&repo=` | `ListItems` | `ListItemsQuery(ItemState?, RepoName?)` | `ItemList(items, empty, asOf)`. Without `state` it lists open items only (a closed item shows only when `state` names `done` or `cancelled`), and its empty sentence then reads "No open items[ in <repo>].". Each item: <ul><li>id, title, repo, state, step;</li><li>`since`;</li><li>`primaryAction`: an action id from `actions`, or null;</li><li>`actions`: the action ids allowed now;</li><li>`owner`: reserved for multi-user (README X61), always null for now.</li></ul> | P3 |
| `GET /items/{itemId}` | `GetItem` | `GetItemQuery(ItemId)` | `ItemView`: <ul><li>the summary, plus version and who created it;</li><li>`body` (scrubbed) and `link`: on this view only, never on a list row, the queue, a timeline line or a public event;</li><li>what it's waiting on (a decision id);</li><li>the engine instance id, once S2 exists;</li><li>timestamps;</li><li>`actions`;</li><li>`owner`: reserved for multi-user (README X61), always null for now;</li><li>`asOf`.</li></ul> | P3 |
| `GET /items/{itemId}/timeline?after=` | `GetItemTimeline` | `GetItemTimelineQuery(ItemId, long?)` | `Timeline(entries, next, asOf)`. Each entry: sequence, time, type, actor, and one scrubbed line | P3 |
| `GET /needs-you` | `GetNeedsYou` | — | `NeedsYouList(rows, empty, asOf)` (§3.6) | P4 |
| `GET /requests?state=` | `ListRequests` | `ListRequestsQuery(RequestState?)` | `RequestList(requests, empty, asOf)`. Each request: <ul><li>id, kind, item;</li><li>text (scrubbed, at most 2,000 characters);</li><li>actor, state, decision id.</li></ul> | P4 |
| `GET /decisions?pending=&item=` | `ListDecisions` | `ListDecisionsQuery(bool?, ItemId?)` | `DecisionList(decisions, empty, asOf)`. Each decision: <ul><li>id, kind, class, item, item version;</li><li>question, options and the recommended option;</li><li>state, and who decided it;</li><li>`recordedOnly`;</li><li>the CLI command.</li></ul> | P4 |
| `GET /events?after=&item=&types=` | `StreamEvents` (S1, extended) | `StreamEventsQuery(long, ItemId?, TypeFilter?)` | An SSE stream of `PublicEvent` (§5) | S1; extended in P5 |
| `GET /ledger?after=&item=&type=&max=` | `ReadLedger` | `ReadLedgerQuery(long, ItemId?, string?, int)`, with `max` ≤ 500 | `LedgerPage(events, next)`: public envelopes with sanitized payloads. It's an audit read, so `human` callers also get the non-public types. For `session` callers it is repo-scoped like every other query (only events whose item is in the session's repo, or that have no item) | P5 |
| `GET /jobs/{jobId}` | `GetJob` | `GetJobQuery(JobId)` | `JobView`: <ul><li>id, kind, key, item?, repo?, service?;</li><li>state: `running`, `succeeded`, `failed` or `cancelled`, `startedBy`, and when it started and ended;</li><li>the last 200 progress lines, each `{round, line, at}` (cleaned when written: scrubbed, escaped, one line, at most 200 characters), and `rounds`;</li><li>result summary, `code`, `result` (the work's bounded, scrubbed result) and `cancelledBy`;</li><li>`actions` (`CancelJob` while running) and `cli` (`sindri jobs cancel JOB-…` while running);</li><li>`asOf`.</li></ul> | P6 |
| `GET /jobs?state=&kind=` | `ListJobs` | `state?`, `kind?` | `JobList`: `{jobs: [JobSummary], empty, asOf}`, newest start first (never latest activity; a tie goes by job id); a `JobSummary` is the `JobView` without its lines, result, actions and cli. **(provisional owner default, 2026-10-15)**: the route, `sindri jobs` and `ServiceView.job` are an additive owner default awaiting confirmation | P6 |

**Session callers are scoped to one repo.**
- A `session` caller's list and item queries are filtered to the repo named in `X-Sindri-Repo`, which the CLI sets from
  the working directory and each repo's MCP registration carries (§8).
- An item in another repo answers `SND-ITEM-404`.
- A `session` query without `X-Sindri-Repo` answers only items that have no repo.
- The same scope applies to session **commands**. `FileRequest` and `pause` (takeover) reach only items in the session's
  repo. A filed request records that repo, so the request caps and the request and decision rows are scoped too.
- The CLI sets `X-Sindri-Repo` from its working directory in every mode.
- **The repo name** is found from the main checkout, so a worktree such as `.claude/worktrees/x` resolves to its
  repository, not to the worktree's folder:
  1. Take the main checkout's directory: the parent of `git rev-parse --path-format=absolute --git-common-dir`.
  2. Resolve it to its real path.
  3. Pick the **approved** profile's repo entry whose real path (with `~` expanded) contains it. When several match, take
     the longest.

  A submodule resolves to its superproject. Outside any profile repo, the header is omitted.
- **`CreateItem` without `--repo`** uses `X-Sindri-Repo`, so items created from inside a repo belong to it.
- `human` callers see everything.

**CLI-local verbs.** `sindri doctor` and `sindri profile validate` run inside the CLI and read local files, as in S1.
They're in the catalog with class `local` and are never routes. `doctor` gains a `views` line in P2.

### 3.2 Commands

| Route | Slice | Body (beyond the route) | Response | Class | CLI | Phase |
|---|---|---|---|---|---|---|
| `POST /stop` | `StopProcess` (S1) | — | S1's response, plus `sequence` | desk (self-audited) | `sindri stop` | S1; class in P1 |
| `POST /profile/approve` | `ApproveProfile` (S1) | S1's body | S1's response, plus `sequence` | **protected** | `sindri profile approve [<hash>]` | S1; class in P1 |
| `POST /views/rebuild` | `RebuildViews` | — | `CommandAccepted` | desk | `sindri views rebuild` | P2 |
| `POST /items` | `CreateItem` | `title`, `repo?`, `body?`, `link?` (a tracker record key, e.g. `linear:issue/<uuid>`) | `ItemCreated(accepted, ItemId)` | desk | `sindri item add "<title>" [--repo R]` | P3 |
| `POST /items/{itemId}/close` | `CloseItem` | `reason` (`done` or `cancelled`), `itemVersion` | `CommandAccepted` | desk | `sindri item close <item> [--cancelled] [--version <n>]` | P3 |
| `POST /requests` | `FileRequest` | `kind` (`rescope`, `add-task`, `note` or `handback`), `itemId?`, `text` | `RequestFiled(accepted, RequestId, DecisionId)` | **request** | `sindri request <kind> [--item X] "<text>"` | P4 |
| `POST /decisions/{decisionId}` | `Decide` | `choice`, `note?` | `CommandAccepted` | decision | `sindri decide <decisionId> <choice> [--note]` | P4 |
| `POST /services/{name}/run` | `RunService` | — | `JobStarted(accepted, JobId, links)`; `links` is `{job, cancel, events}` (paths under the origin) | job | `sindri services run <name>` | P6 |
| `POST /services/{name}/pause` | `PauseService` | — | `CommandAccepted` | desk | `sindri services pause <name>` | P6 |
| `POST /services/{name}/resume` | `ResumeService` | — | `CommandAccepted` | desk | `sindri services resume <name>` | P6 |
| `POST /jobs/{jobId}/cancel` | `CancelJob` | — | `CommandAccepted` | job | Ctrl-C while following; `sindri jobs cancel <id>` | P6 |
| `POST /items/{itemId}/open` | `OpenItem` | `mode` (`open`, `attach` or `resume`), `target?` (`claude-code`, `codex` or `cursor`), `itemVersion` (UI E-1) | `LaunchRequested(accepted, LaunchId)` | desk | `sindri open <item> [--agent claude-code\|codex\|cursor] [--attach \| --resume]` | P10a |

Notes:
- **`ApproveProfile` keeps S1's body.** The `protected` class means only a `human` caller **with a terminal** reaches
  the handler (§4). How the human confirms is a separate question:
  - The engine spec's server-issued challenge (its §4.2, S1-D5) replaces S1's `confirmed` flag, so every `protected`
    action confirms the same way.
  - Until the challenge lands, S1's flag stays behind this class check.
- **`CloseItem` voids the item's pending decisions.** Their rows clear, and a later `Decide` on one of them gets
  `SND-ITEM-409`. Once S2 runs, `CloseItem` is refused with `SND-ITEM-409` while the item has an active instance; cancel
  that with `sindri workflow cancel`.
- **Item versions.**
  - `Decide` reads the item version from the decision's own row.
  - `CloseItem` sends the version the human saw.
  - Conflicts are settled by fact keys (§2.1).
- **The engine spec owns the instance commands**, together with their routes and the verbs `sindri workflow …`, which
  keep `sindri handback` as the short alias from §9.3:
  - `StartInstance`;
  - `ApplyCommand` (answer, with an optional `grant` or `accept`; takeover; handback; cancel);
  - `CompleteStep`;
  - `GetInstance`;
  - `ListInstances`.

  See §3.5. The view shapes their routes return are §3.8 (E-20…E-25).

### 3.3 The command catalog

`ICommandCatalog` is a port in `Core`. The Host implements it as `CommandCatalog`, because routes and CLI forms are Host
facts.

**Rows.** There is one row per action id: a command, or a command variant such as `ApplyCommand:handback` or
`ApplyCommand:answer-grant`. Each row has:
- the command type and variant;
- the action class;
- the allowed callers;
- whether it needs a terminal;
- `SelfAudited`;
- the method and route;
- the CLI form;
- the UI status: `ready`, `pending:<phase>`, or `cli-only:<reason>`. `cli-only` is accepted only for
  terminal-bound rows, for example `cli-only:typed-confirmation`.

**Who uses it.**
- `ListCommands` serves it.
- The UI renders its buttons and forms from it, keyed by action id (§17.27.4).
- `doctor` lists the rows still `pending`.
- The engine spec registers its rows here too.

**The test.** A unit test fails when:
- a command or variant has no row;
- a row's route isn't in the endpoint table;
- a `pending:<phase>` names a phase that has already shipped;
- a `cli-only` row isn't terminal-bound.

### 3.4 Events these phases add

Every event is a `LedgerEntry` (S1) with the Kernel `EventEnvelope`. The phase that adds an event registers it in
`EventCatalog` (`Core`), with its type, whether it's public, its public payload type and its fact-key shape. The
catalog itself lands in P1. Every domain payload carries `commandId` (or the `service:<name>` run id).

| Type | Payload (besides `commandId`) | Key | Public | Phase |
|---|---|---|---|---|
| `command.handled` | command, class, caller, outcome (`accepted`, `refused`, `invalid` or `failed`), code? | `command.handled:<commandId>` | no | P1 |
| `views.rebuilt` | cursor before, events replayed | `views.rebuilt:<commandId>` | no | P2 |
| `service.ran` | service, slot, outcome (`ok` or `failed`), code? | `service.ran:<name>:<epoch>:<slot>` | yes | P2 (C8) |
| `profile.checked` | approved hash, live hash, drifted, changed paths, the approved profile's service names | `profile.checked:<previousCheckSequence>` | yes | P2 (C8) |
| `item.created` | item, title, repo, link?, body? (never public), owner? (reserved, README X61) | `item.created:<itemId>` | yes | P3 |
| `item.state-changed` | item, from, to, version (the new one), reason? | `item.state-changed:<itemId>:v<version>` | yes | P3 |
| `request.filed` | request, decision, kind, item?, repo? (the item's repo, else the session's `X-Sindri-Repo`), text (scrubbed, at most 2,000 characters) | `request.filed:<requestId>` | yes (text left out) | P4 |
| `decision.recorded` | decision, choice, note?, by, repo? (the decision's repo, copied from its row) | `decision.recorded:<decisionId>` | yes (note left out; a human's ledger read keeps it) | P4 |
| `job.started` | job, kind, key, item?, repo?, service? | `job.started:<jobId>` | yes | P6 |
| `service.ran` (manual) | as above, plus `job`; `slot` is the run's start time, and the key carries the job instead | `service.ran:<name>:manual:<jobId>` | yes | P6: a `RunService` job appends it when it ends, so a manual run updates `lastRun` and clears a failure row |
| `job.progress` | job, round, line, item?, repo? | `job.progress:<jobId>:<n>` | yes | P6 |
| `job.finished` | job, outcome, summary, code?, result?, item?, repo? | `job.finished:<jobId>` | yes (result left out of the public form) | P6 |
| `job.cancelled` | job, by, item?, repo? | `job.cancelled:<jobId>` | yes | P6 |
| `service.paused`, `service.resumed` | service | `service.paused:<name>:<commandId>` | yes | P6 |
| `item.linked` | item, record key, alias | `item.linked:<itemId>:<record>` | yes | context Phase 6 only (`service:context-sync`); a `CreateItem` with `link` stores it on `item.created` and gives no alias, so `GET /items/<tracker key>` resolves only after a later `item.linked` |
| `item.open-requested` | `launchId`, item, mode, target, chosenBy | `item.open-requested:<launchId>` | yes | P10 |
| `launch.started`, `launch.failed`, `launch.probed`, `session.ended` | as UI E-1, E-3 and launcher L-1, L-10: every launch payload names the launch `launchId`; `launch.failed` carries `code`, `message` and an optional `detail` (`stage`: `host`/`backstop`; for SND-LAUNCH-012 also `reason`, `current`, `approved`); `session.ended`: `launchId`, reason (`exit`, `lost`), exit code, and the final `inputTokens`/`outputTokens` when the target's reader had a transcript | `launch.outcome:<launchId>` (`launch.started` and `launch.failed` share it), `session.ended:<launchId>` | yes | P10a (written by `service:launcher`; the engine's agent runtime uses the same events) |
| `session.usage` | `launchId`, `sample`, `target`, `model` (the one with the most tokens; null when the reader named none), `inputTokens`, `outputTokens` (cumulative for the launch) | `session.usage:<launchId>:<n>` | no | P10a (E-28; sampled every 60 s while the session is live by the target's usage reader, and once more at its end) |

**How a request becomes a decision** (one domain event per command):
1. `FileRequest` appends `request.filed`, with both ids chosen by the handler.
2. The projections derive the request row (`open`) and one pending decision for it:
   - kind `accept-request`;
   - options `accept` and `decline`;
   - class `decision`.
3. `Decide` appends `decision.recorded`.
4. The projections mark the decision as decided, and the request as `accepted` or `declined`.

**Once the engine runs,** an accepted request reaches the item's instance as `decision.recorded` (kind
`accept-request`), a registered event a recipe may wait on; the engine's `workflow.request` event is dropped.
**Before the engine (S2) exists,** an accepted request is **recorded only**. Its decision row and needs-you row carry
`recordedOnly: true`, shown as "recorded; acted on once the workflow engine runs".

**Engine parks and gates aren't `Decide` decisions.** The engine answers them with `ApplyCommand`. The needs-you
projection folds the engine's park and gate events into rows of kind `park`, whose `primaryAction` is the engine's
action id, for example `ApplyCommand:answer-resume` (an open park serves one `answer-<variant>` entry per fitting variant, primary first).

### 3.5 Ownership with the workflow engine spec

| Concern | Owner | The other spec's part |
|---|---|---|
| Instances: their states, park reasons, takeover, handback, answering a park (`grant`, `accept`), timers, `CompleteStep` | **Engine**: `StartInstance`, `ApplyCommand`, `CompleteStep`, `GetInstance`, `ListInstances`; engine P4 and P5 | This spec gives their catalog rows a class and allowed callers:<ul><li>start, answer, handback, cancel and complete are `desk`;</li><li>answer with `grant` or `accept` is `protected` (terminal);</li><li>takeover is `pause` (§4.1).</li></ul> |
| The verbs `sindri workflow start`, `answer`, `takeover`, `handback`, `cancel` and `complete`, plus the `sindri handback` alias | **Engine** | — |
| One "needs you" list | **This spec**: `GET /needs-you`, with engine parks as kind `park` | The engine's `workflow list --needs-me` reads `GET /needs-you` (engine §6.3) |
| Item rows, the queue, requests and decisions | **This spec** | The engine emits the events these views fold. Once S2 runs, an item's `state` reflects its instance state |
| The item filter on `/sindri/events` (engine S1-D6) | **This spec**, in P5 | The engine's `workflow events` uses it. If engine P4 merges first, it adds the filter and P5 keeps it |
| `ILauncher` | **This spec**, in P10 (`OpenItem`), with launch targets from the UI design spec | The engine replaces its desk runtime with it (engine W20) |
| The shapes of the instance list and view, the recipe definition, stats and list, and the status fields (E-20, E-22…E-25) | **This spec**, §3.8: the screens spec asks for them, and they are served by the engine spec's `ListInstances`, `GetInstance`, `ListRecipes` and `ShowRecipe` | The engine spec computes path order, `loops`, `next` and the status layout (WF §3.5, §6.4) and registers the `outputs` and `inputs[].kind` schema (E-22). The CLI's `workflow status` and `recipe show` print from the same renderers |
| The engine in depth (E-30): `GET /engine`, `/engine/instances`, `/engine/instances/{id}/history` | **This spec**, §3.8, P13, served from the Temporal SDK through Sindri | The engine spec's mirror writes the Temporal event id on each ledger row (WF §5.6, ERD), which the history lines join on |
| The authorization model: caller, terminal flag, action classes, the chain, and `SND-AUTH-002` | **This spec**, in P1 | Every engine command goes through the same chain. The engine spec adopted it in its rev 5 (§6.2): its own caller classes and `SND-WF-007` are retired, takeover is `pause`, the `IClassifiedCommand` rule applies, its harness probe supplies the agent variables (§6), and idle takeovers and desk tasks are needs-you rows of kind `park`. Engine commands never evaluate gates in the handler (engine §5.3) and may append one event per instance they change (a parent and the child it creates) |
| `EventFactory` reading the caller (C6) and the engine's S1-D7 | **This spec**, P1 | The engine uses it from P4 |
| Starting the process from a verb | **This spec**, §7: never from inside an agent session | The engine spec's "starts if needed" follows §7 |

This spec has no item `hold`, and no item-level park, unpark or handback: those are engine instance commands.

### 3.7 Context routes (owned by the context retrieval spec)

The context engine's routes sit under `/sindri/context/` and follow this spec's rules: queries are `GET`s, every `POST`
is a command with a catalog row and an action class, and every route goes through the chain.

| Route | Class |
|---|---|
| `GET /context/query`, `/context/node`, `/context/neighbors`, `/context/path`, `/context/map`, `/context/map/export`, `/context/map/review`, `/context/map/review/history`, `/context/map/review/export`, `/context/map/proposals`, `/context/lenses`, `/context/filemap`, `/context/sources` | query (session callers repo-scoped, §3.1) |
| `POST /context/map/build`, `POST /context/sources/sync` | `job` |
| `POST /context/map/import`, `POST /context/map/promote` (both make content `current`) | `protected` |
| `POST /context/map/review`, variants `accept`, `reject`, `rename`, `move`, `accept-deterministic`, `undo`, `drop`, `repoint` (one reversible `map_override` row, with Undo; README X63) | `desk` |
| `POST /context/map/review`, variants `apply-file`, `merge`, `add`, `delete` | `protected` |
| `POST /context/map/proposals` (a scoping run's surfaces as draft proposals; synchronous until API P6) | `job` |

### 3.6 Views the UI renders: states, groups and needs-you

**Item states and their queue groups.** Groups appear in this order. Within a group, the oldest `since` comes first.

| Group | `ItemState` values |
|---|---|
| `waitingOnYou` | `needs-decision`, `needs-answer`, `parked` |
| `withYou` | `taken-over` |
| `running` | `running` |
| `queued` | `new`, `queued` |
| (not in the queue) | `done`, `cancelled` |

How items move through the states:
- **Before S2:**
  - an item is `new`;
  - it is `needs-decision` while a pending decision points at it, and back to `new` once the decision is recorded;
  - it is `done` or `cancelled` after `CloseItem`.
- **Once S2 runs:**
  - `needs-answer`, `taken-over`, `parked` and `running` come from the item projection folding the engine's
    `workflow.*` events, which P11 adds; `queued` waits for the router phase (no event yet);
  - `needs-answer` is produced only by the engine.

**Engine state → item state** (P11 folds these; the needs-you row kind is `park` for every row below that has one):

| Engine (instance frame, engine §3.8) | `ItemState` | Queue group | Needs-you row and primary action |
|---|---|---|---|
| `active` with an agent run | `running` | `running` | — |
| `active` with an open desk task | `needs-answer` | `waitingOnYou` | "Desk task" → `CompleteStep` (UI form), or `OpenItem` |
| `wait` (event, child, resource) | `running` | `running` | — |
| `parked` (any reason) | `parked` | `waitingOnYou` | Reason and question → the §3.8 answer's action id (`cli-only` when `protected`). A park mirroring a child whose fit the child decides (needs-answer, budget, spec-conflict) is the escalated-child case below |
| `escalated` (gate) | `parked` | `waitingOnYou` | `answerKind: accept` → `ApplyCommand:answer-accept` (`cli-only`); reject is `ApplyCommand:answer-reject` |
| `escalated` (timeout) | `parked` | `waitingOnYou` | `answerKind: resume` → `ApplyCommand:answer-resume` |
| `escalated` (child) | `parked` | `waitingOnYou` | Forwarded to the child, whose fit this build does not know: `answerKind: text`, action `ApplyCommand:answer-text` with the placeholder cli `<the child's answer>`, a pointer to the child and not an answer form, marked on the status `answer` record as `via: "child"` and `child: <the child's address>` (both omitted otherwise; `child` is present when the view knows the mirrored child, so until P7 wires child propagation into the served view expect `via: "child"` without `child`); the needs-you row carries the same `via`; `allowedCommands` has no `answer` entry for it. The same holds for a mirrored needs-answer, budget or spec-conflict park |
| `taken-over` | `taken-over` | `withYou` | After `takeoverIdle`: "hand back?" → `ApplyCommand:handback` |
| terminal | `done` or `cancelled` | — | — |
| no instance yet, started by a router (router phase; P11 does not produce it) | `queued` | `queued` | — |

`parked` therefore sits in `waitingOnYou`; there is no separate `parked` group. `needs-answer` is an item state; park reasons are shown as the row's `reason`, never as an item state.

**`GET /needs-you`** is one list across every source, built by the needs-you projection. Each `NeedsYouRow` has:

| Field | What it holds |
|---|---|
| `id` | A stable id, such as `decision:DEC-…` or `failure:service:heartbeat`. The UI keys focus and the "seen" pulse on it |
| `kind` | `decision`, `park`, `failure` or `profile` |
| `subject` | `{kind, id}`: a reference to an item, decision, instance, job, service or profile. A `profile` row's subject is `{profile, <live hash>}` (owner decision, 2026-10-14) |
| `title`, `reason` | Scrubbed text |
| `repo?` | The repo the row belongs to |
| `urgency` | `pulse`, `colour` or `calm` (dashboard spec §5.1) |
| `since` | When the row appeared |
| `recordedOnly` | Whether the decision is recorded only (before S2) |
| `notice` | The sentence that says so, served by the API (`recorded; acted on once the workflow engine runs`) so no client keeps its own copy; `null` when `recordedOnly` is false. A `DecisionView` voided by closing its item carries the voided sentence (`voided: the item was closed`) in `notice` and `cleared` |
| `answerKind` | For `park` rows: `text`, `resume`, `grant`, `grantTokens`, `accept` (with reject), or `cancel-only`, taken from the engine's resume action (engine §5.8), so the app shows the right control |
| `via?` | `"child"` when `cli` points at a child that decides the answer; omitted otherwise |
| `primaryAction` | A catalog action id (no P4 row is `cli-only`, so none serves `copy:<cli>`). Per kind: <ul><li>`decision` → `Decide`;</li><li>`park` → the engine's action id;</li><li>`failure` → `RunService` (served since P6, with `cli` `sindri services run <name>`), or **null** for a failure of a service this build doesn't have (`SND-SVC-404`: `actions` is `[PauseService]` and `cli` is `sindri profile validate`; golden `needs-you/unknown-service`);</li><li>`profile` → `ApproveProfile` (protected: terminal-only, so the app copies the row's `cli`, `sindri profile approve <hash>`, instead of calling it).</li></ul> |
| `cli` | The CLI form |

**Rows by kind.** A filed request appears **once**, as its `decision` row; there is no separate request row.

| Kind | Source | Urgency | Cleared when |
|---|---|---|---|
| `decision` | Pending decisions, including filed requests (P4) | `pulse` | `decision.recorded`, or the item closing (the decision is voided) |
| `park` | Engine parks and gates, idle takeovers, and desk tasks (S2) | `pulse` | The engine resumes the instance, a `handback`, or `CompleteStep` |
| `failure` | `service.ran` with `failed` (C8, P2) | `colour` | The service's next `service.ran` with `ok` (scheduled or manual), or a `profile.checked` whose approved service names no longer include the service (it was removed and approved) |
| `profile` | `profile.checked` with `drifted: true` (C8, P2) | `calm` | The next `profile.checked` with `drifted: false`, after an approval or a revert |

- The collector adds more sources later (§11): sessions waiting on approval or input, stale indexes, judge health and
  PRs. Each is one more projection writing rows of the same shape.
- An explicit acknowledge (`POST /needs-you/{id}/ack`) comes later too.
- Rows are ordered by urgency, then oldest first.

**Empty, loading and error text.** One `EmptyText` table in `Core` holds the sentences for each list: its empty
sentence (for example "Nothing needs you. 3 running, 1 queued."), plus `rebuilding` ("Rebuilding views (cursor
N of M)…"), `stalled` ("Views are stalled at N: run `sindri views rebuild`") and, for a projector that can't run, "The views can't be updated (<code>: <detail>)" with `sindri views rebuild` or `sindri doctor` as its fix.
- The CLI prints them, and every list view returns the right one in `empty`. The UI never keeps its own copy.
- The CLI exits 1 (attention) when a view answers `rebuilding` or `stalled`, so a script can tell those apart from an
  empty list (exit 0).
- For errors, `ProblemDetails.detail` already carries S1's `(fix: …)` text, and the UI shows `detail` as it is.

**Agent text.**
- Text from agents or trackers is scrubbed, then escaped with the shared sanitizer, when the projection writes it. The
  sanitizer covers control characters, invisible characters, and OSC and ANSI sequences.
- `ReadLedger` passes payloads through the same sanitizer.
- The API never returns raw control characters, and the UI renders text as plain text only.

**Dashboard views fed by v0 (P1–P5):**
- **Needs you**, except its session, PR, index and judge sources;
- the queue, and item detail;
- decisions and requests;
- the services part of **Automatic work**;
- live refresh.

**Fed later (§11):** **Agents** (except its budget panel, fed by `GET /launch/targets` in P10a, E-28), **Repos**, the rest of **Automatic work**, and **Today**.

### 3.8 Workflow, recipe and engine views (UI screens spec, E-20…E-25, E-28…E-30)

The screens spec (`2026-10-13-sindri-ui-screens-design.md`, §4.2, §4.3, §4.5, §4.6, §5) renders any recipe from data the
API serves, so the app never parses YAML or recomputes an order (README X52: the UI spec wins).
- **Ownership (§3.5).** The workflow engine spec owns the slices `ListInstances`, `GetInstance`, `ListRecipes` and
  `ShowRecipe` and their routes. This section owns the **view shapes** they return, and the engine spec's `{instanceId}`
  and `{id}@{hash}` forms stay valid beside the addressed forms below.
- **Address.** An address is the engine's form (WF §6.1), everywhere, with no display forms: a root is
  `<item>/<recipe-name>/<attempt>` (`ITM-01JK3/project/1`), a child is `<parent>/<child-recipe>.<n>`
  (`ITM-01JK3/project/1/step.2`), and every root has an item. The routes take the opaque instance id (`WFI-…`):
  `GET /workflow/instances/{id}`, and the facts route is `/workflow/instances/{id}/facts/{key}`. An address is given only
  as `?address=` (URL-encoded). The app's deep link is `workflow/<address>`; that is a path of the app, not an address.
- **Callers.** Every route here is a query. A `session` caller is repo-scoped as in §3.1: instances whose item is in
  another repo are left out, and an address in another repo answers `SND-ITEM-404`.
- **Text and ids.** All text is scrubbed and escaped as in §3.6. Times are ISO 8601 UTC; durations are whole seconds,
  and the app formats them.

| Route | Slice | Request | Response | Class | Phase |
|---|---|---|---|---|---|
| `GET /workflow/instances?root=&open=&recipe=&subject=&state=&needsMe=` | `ListInstances` (engine) | `root=true` keeps top-level instances; `open=true` keeps those whose state is not terminal; `recipe` is a recipe id; `subject` matches the subject key exactly, or the subject label (the rendered `name` when the recipe has one, WF §3.11) as a case-insensitive substring | `InstanceList(instances, counts, empty, asOf)` (E-20, below) | query | P11 |
| `GET /workflow/instances/{id}` (or `?address=`) | `GetInstance` (engine) | — | `InstanceView` (E-20, E-23, below) | query | P11 |
| `GET /workflow/instances/{id}/facts/{key}` | `GetInstanceFact` | `key` is the fact row's key in its `fact:` form (`fact:scope`) | `FactView{key, type, body, truncated}`: the scrubbed body behind one fact row (the scope-map table with its graph and items columns, the packet's tasks and `PKT-*` check results, a payload tree). `body` is an object for `scope-map`, `packet`, `evidence` (`{packs[], done, total}`: the packs produced so far) and `payload` and a string for `text`. Large bodies are capped at 256 KiB with `truncated: true`; a truncated object loses its tail entries and stays an object. An undeclared key answers `SND-ITEM-404` | query | P11 |
| `GET /workflow/recipes?window=` | `ListRecipes` (engine) | `window`: `7d`, `30d` (default) or `90d` | `RecipeList(recipes, window, empty, asOf)` (E-25) | query | P12 |
| `GET /workflow/recipes/{id}` and `{id}@{hash}` | `ShowRecipe` (engine) | `{id}` resolves to the approved hash | `RecipeView` (E-22) | query | P11 |
| `GET /workflow/recipes/{id}/stats?window=&hash=` | `GetRecipeStats` | `window` as above; `hash` re-scopes to runs under one approved version | `RecipeStats` (E-24) | query | P12 |
| `GET /engine` | `GetEngine` | — | `EngineView` (E-30) | query | P13 |
| `GET /engine/instances?state=&stuck=` | `ListEngineInstances` | — | `EngineInstanceList(instances, pending, asOf)` (E-30) | query | P13 |
| `GET /engine/instances/{id}/history?page=` (or `?address=`) | `GetEngineHistory` | `page` from 1; 50 events a page, newest first | `EngineHistory(events, page, total, next)` (E-30) | query | P13 |

**The instance list (E-20).** `InstanceList.instances` is ordered by this key list, each key breaking the ties of the one before: (1) unfinished before finished; (2) not parked or escalated before parked or escalated (only parked and escalated rows move to the Needs-you panel; a `DECISION` row counts as needs-you but stays in the strip, and a `WITH YOU` row is neither); (3) the recipe's `urgency` (`incident`, then `triage`, then `work`; WF E-21); (4) `subject.label` (ordinal; the fixture's rows are not age-ordered, so the label is the stable tiebreak). The Home strip (screens §4.1) is the first five rows of that order, which reproduces the screens fixture's strip, and the Workflows page groups the same rows by `recipe.id`. `counts` is `{open, needsYou, withYou, running, done}`, computed over the whole root set before `recipe` or `open` narrows the rows, so every head count comes from rows. **`counts.open` is the rows that are not terminal** (owner decision, 2026-10-14): the Workflows head and the nav print `counts.open`, and the finished rows show as one collapsed "N done today" line, not as open ones. `counts.needsYou` counts the `PARKED`, `ESCALATED` and `DECISION` rows, workflow rows only; Home's own needs-you figure counts the whole needs-you list. Each `InstanceSummary`:

| Field | What it holds |
|---|---|
| `address`, `id`, `itemId?` | The CLI address; the engine's `WFI-…` instance id, which the id routes take; and the `ITM-…` id when the instance is an item's |
| `recipe` | `{id, hash, version, urgency}` |
| `state`, `since` | The current state id, and when the instance entered it |
| `chip` | `{word, tone}`: the one state chip (`PARKED`, `ESCALATED`, `WITH YOU`, `DECISION`, `QUEUED`, `DONE`, `RUNNING`) and its colour class `attn`, `fail`, `ok` or `neutral`; the same record is on `ChildRow` |
| `engineOwned` | `null`, or `{kind: parked \| escalated \| taken-over, reason}`: the badge that replaces the state's glyph |
| `subject` | `{kind, key, label, href?}`, from the recipe's first input (screens §5.4); `kind` is `inputs[0].kind` (E-22) and is never null (WF `RCP-INPUT-001`); `label` is the instance's rendered `name` when the recipe has one (WF §3.11), otherwise the per-kind rule of screens §5.4; a `link` subject's label is the title, else the host, and its `href` is the URL. An instance on a recipe registered before input kinds (WF §3.11, "A pre-P4c instance") has no input to take a kind from: its subject is `{kind: item, key: <item id>, label: <item title, else the address>}`, as its list row shows, and its `inputs[]` is empty |
| `inputs[]` | `{key, label, kind, required, present}`, in declaration order, for the INPUTS column. `kind` is the input's declared kind exactly as `RecipeView.inputs[].kind` serves it (the subject adapter; never null). **A supplied input is `required: true`; an input with `generate` is `required: false`; every input is `present: true` once the instance has started** (StartInstance refuses a missing input and fills a generated one before the workflow starts). The missing render is a preview with data in one golden, `workflow-instances/preview-missing-input` (ITM-01JKH, a marked preview like the fan-out one) |
| `states[]` | The compact `StateCell` list below, so the list renders every stage cell without a second call |
| `now` | Its own short sentence of at most 80 characters, composed by the engine's layout for the row (screens fixture, NOW column); it is not a shortening of `next` (the full `next` is on the view). One pattern per chip: `DECISION` reads `needs your <answer word> (desk answer)` (the answer word is the first output the report must carry, else the state's name); `PARKED` and `ESCALATED` read `needs your answer` when the view has an answer line (a Step park, a gate or review escalation) and otherwise `parked (<detail>)` or `escalated (<detail>)`, or the bare word when there is no detail; `WITH YOU` reads `with you: <activity>`, the first clause of the desk Step's `next`; `RUNNING` reads `engine waiting in <item>` (a wait), `engine running in <item>` (no step) or `agent <gerund> in <item>` (an agent step: `agent producing in ITM-01JL2`; the gerund is of the step id's last word); `QUEUED` reads `queued`; `DONE` reads the outcome, or `done` when it has none. Where a part is missing: `WITH YOU` reads the state's id when the Step has no activity, and `RUNNING` reads the instance's address in place of `<item>` when it has no item. The engine cuts it at 80 characters, surrogate-safe, with `...` inside the 80 |
| `answer?` | The same `StatusAnswer` as `current.status.answer`, including `atTerminal`, so the row can offer `[Answer]` |
| `actions` | The action ids allowed now, as on `ItemView` |

**`StateCell` and the full `states[]` (E-20).** One entry per state of the recipe, in **path order** (screens §5.1).
The engine computes the order once per recipe hash and serves it, so a client never walks the graph.

| Field | What it holds |
|---|---|
| `id` | The state id |
| `order` | The 1-based position in path order on the main path; `null` for a branch state (WF §3.10) |
| `branchOf?` | The state this one is folded under, for a branch (the app draws `⤷`) |
| `glyph` | `done`, `active` or `pending` (the app draws ●, ◐, ○). A terminal state is `done` once reached |
| `badge?` | `{kind: parked \| escalated \| taken-over, reason}` on the state the instance is interrupted in; it replaces the glyph |
| `round?` | `{n, max}` for the current state of a loop: the round it is in now (`r2/3`) |
| `loops` | `[{with: <stateId>, n, max}]`: the loop pairs this state belongs to (`mitigate ⇄ verify`). Loops belong to the recipe hash, so every instance of a hash serves the same pairs (`n` is the **rounds completed** in that loop so far, 0 where the instance has not looped; `max` is the recipe's cap, the same for every instance of the hash). A self-loop is `with` = the state itself (`scoping`, round 2 of 3) |
| `desk` | `true` for a desk state |
| `child` | `true` when the state waits on children |
| `children?` | `{done, total}`: progress for a children state (`4/9`) |
| `outcome?` | A recorded outcome worth showing on the band (`sev2`) |
| `terminal` | `true` for a final state |

The view's top level adds `orderedBy`: `"path"`, or `"transition-list"` when the graph has more than one initial path
(screens §5.1), so the band can say so.

```jsonc
// GET /workflow/instances/WFI-b8af4435…   (InstanceView; E-20, E-23)
{
  "address": "ITM-01JK3/project/1", "id": "WFI-b8af4435…", "parent": null,   // parent?: {id, address, title, task?, workstream?}
  "itemId": "ITM-01JK3", "title": "Offline sync",
  "recipe": { "id": "workflow.project", "hash": "a8c21e7f…", "version": 12, "urgency": "work" },
  "state": "build", "since": "2026-10-14T09:14:00Z",
  "engineOwned": null,
  "subject": { "kind": "linear-project", "key": "APP", "label": "APP · Offline sync", "href": "https://linear.app/…" },
  "orderedBy": "path",
  "states": [
    { "id": "intake", "order": 1, "glyph": "done", "loops": [], "desk": false, "child": false, "terminal": false },
    { "id": "build",  "order": 4, "glyph": "active", "loops": [], "desk": false, "child": true,
      "children": { "done": 4, "total": 9 } }
    // … every state, in path order
  ],
  "current": {                      // the expanded state (the engine's status layout, WF §6.4, as fields)
    "state": "build",
    "step": { "kind": "wait", "ref": "workflow.step", "declaration": "one child per packet task" },   // StepDeclaration, below
    "gates": [ { "id": "gate.evidence.all", "on": "exit", "threshold": null, "description": "…" },
               { "id": "gate.judge.verify", "on": "exit", "threshold": 0.8, "description": "…" } ],
    "desk": null,                    // or {run:"3.1", outputs:[{key,type,required}], checks:[…], completeAction:"CompleteStep"}
    "status": { /* StatusFields, E-23, below */ }
  },
  "facts": [ /* FactRow, below */ ],
  "children": [ /* ChildGroup, below */ ],
  "counts": { "timeline": 41, "decisions": 3, "requests": 1, "context": 14 },   // context: null until CTX serves the lenses
  "actions": [ "ApplyCommand:takeover", "ApplyCommand:cancel" ],
  "asOf": "…"
}
```

**`StatusFields` (E-23).** The WF §6.4 layout as fields. The CLI prints its text form from the **same renderer**, and
`text` carries that form unchanged, so the app and `sindri workflow status` cannot disagree; a golden test renders
every §6.4 layout both ways. Lines the layout leaves out are `null` or `[]`.

| Field | What it holds |
|---|---|
| `header` | `{address, recipe, hash12, version, state, detail?, since, ageSeconds}`: the header line's parts, as WF §6.4 prints them (`<state>[: <detail>]`). `state` is the reserved engine word while the engine owns the instance (`parked`, `escalated`, `taken-over`) and the recipe state otherwise; `detail` is the reason (`budget`, `no-progress (child)`, `gate`, `desk task`) and never contains the engine word. **`detail` is `null` for a running instance** (no `running`, round or children text: the engine never produces those) and for a finished one whose outcome equals its state. The recipe state the instance is covered in stays on the view (`state`) and on the `states[]` badge |
| `next?` | The rendered `next` sentence, whole; `null` when the layout has none (a finished instance) |
| `answer?` | `{kind: text \| resume \| grant \| grantTokens \| accept \| cancel-only, actionId, class, cli, atTerminal}`; `class` is `desk` or `protected`, and `cli` is the exact command the layout prints (a wait's is `--resume [--text <answer>]`). Only `accept` is served for an escalated gate: `reject` is CLI-only for now. **A drift park has no answer** (`null`): re-approving the changed block is the protected command `approve` (`sindri recipe approve <id>@<hash12>`, class `protected`) in `commands[]` beside `takeover` and `cancel`, and the approval resumes the instance. **A desk task has no answer row** either: `complete` is its first command (`--output fact:<key>=<file>`), because desk completion is the action, not an answer |
| `child?` | `{id, address, state, reason?}` when the park mirrors a child |
| `drift?` | `{block, needsReapproval}` for a recorded drift (the line reads `<block> changed since <id>@<hash12> was approved`) |
| `inputs[]` | `{key, name, hash?}` for the Step's inputs. `hash` is `sha256:<64 hex>`, as on every hash field; text forms shorten it |
| `timeout?` | `{kind: parks \| escalates, inSeconds, on}`. `on` is the id of the state the timeout belongs to, which is the current state: one meaning, never the state the timeout fires to. The text form prints the owner of an open desk task as `(desk)`, as the shipped `StatusLayout` does (`parks in 1d 22h 48m (desk)`, with `on: produce`); the `on` field is what clients read. A retry timeout (`TimeoutAction.Retry`, as workflow.item's `claimed`) is served as `parks`, its eventual effect: the first fire retries, a second parks |
| `budget?` | `{scope: state, used, limit?, instance: {used, limit?}}` in tokens. `limit` is `null` when the state (or the instance) has no token cap; `used` is still served, and the status text then prints `<used> used` with no `/ <limit>` (e.g. `tokens 84k used (state)`) |
| `paused?` | `{timeoutLeftSeconds}` on a taken-over instance |
| `commands[]` | The instance's own verbs first, in `answer, complete, handback, takeover, cancel, approve` order: `{verb, actionId, class, cli, confirm?}`; `verb` is that closed set (the engine's open recipe-command verbs are not served here), each a row of the catalog (§3.3). `confirm` is `null` except on `cancel`, where it is `Cancel <address>? <N> running steps stop; <M> children are cancelled.` (owner decision, 2026-10-14; N is the open run's running steps and M the open children; an open desk run is not a running step, so N excludes it). The sentence is singular at 1 (`1 running step stops; 1 child is cancelled`): a provisional owner default (2026-10-14 night) |
| `text` | The CLI's text form, ASCII only (WF §6.4: hashes are cut with `...`, and a line is joined with `,`, never a middle dot) |

**`FactRow` (E-20).** One per input and output the recipe declares, in declaration order (screens §5.2).

| Field | What it holds |
|---|---|
| `key`, `label`, `type` | `key` carries the `fact:` prefix everywhere it appears (declarations, rows, refs, the facts route); `label` and `type` equal the recipe's declaration of the key (`RecipeView.inputs[]` and `outputs[]`); `type` is `text`, `file`, `item`, `link`, `scope-map`, `packet`, `evidence`, `decision` or `payload`; an unknown type is served as `payload`; an undeclared type is served as the declared default `text` (engine §3.3); `FactTypeJsonConverter`'s unknown-name → payload rule is separate (a wire name the client doesn't know) |
| `direction` | `input` or `output` |
| `from?` | The state that produces it (E-22 `outputs[].from`), which places the band's ◐ mark |
| `status` | `produced`, `in-progress` or `pending` |
| `summary` | The one-line summary the viewer shows |
| `at?`, `by?` | When it was produced, and by whom. `by` is the actor string (README X61, never parsed by the API): `human:<client>`, `step.<id>`, `agent:<name>`, `service:<name>` (a webhook is `service:webhook`) or `engine:generate` (an input the start generated from its template, WF §3.11); the UI renders `human:*` as "you" |
| `ref?` | The per-type handle: `{name, size, hash, path}` for `file`; `{itemId}`; `{url, host, title}`; `{factKey}` for the viewers that call `GetInstanceFact` |
| `progress?` | `{done, total}` for `evidence` (`4 of 9 packs`) |
| `pending?` | On a pending output of an open desk state: `{required, checks[], completeAction}`, which the complete form needs |

**`ChildGroup` (E-20).** `children` is a list of groups, one per state that declares `child` or runs `step.children`
(screens §5.5), in path order. `recipe` is the `workflow.*` id of the recipe the children run. A child's `address` is the engine's form, `<parent>/<child-recipe>.<n>` (`ITM-01JK3/project/1/step.2`; the screens spec's `builder.feature` is illustrative: children run `workflow.step`). A child that has not started shows its recipe's first state (`produce`), glyph `pending`, chip `QUEUED`, and offers `OpenItem` only; a done child offers `OpenItem` only. **`mirroredPark`** is a child's park shown on the parent's band; it does not park the parent, which keeps running:

```jsonc
{ "state": "build", "recipe": "workflow.step", "done": 4, "total": 9,
  "workstreams": [                     // [] unless the state's input is a packet; in the packet's order
    { "id": "ws-1", "label": "UI",
      "rows": [ { "id": "WFI-205dbef9…", "address": "ITM-01JK3/project/1/step.2", "itemId": "ITM-01JE7", "title": "Sync status pill",
                  "glyph": "active", "state": "produce", "chip": { "word": "DECISION", "tone": "attn" }, "round": null,
                  "agent": null, "ageSeconds": 360,
                  "engineOwned": null, "mirroredPark": null, "actions": [ "OpenItem", "ApplyCommand:takeover" ] } ] } ],   // illustrative: a workflow.step child is a desk task until P8 (below)
  "rows": [],                          // used instead of workstreams when there are none
  "more": 6 }                          // rows beyond the first 20; "Open all in Work" follows the items filter
```

**What a served instance holds (invariants the goldens keep).**
- **`current.step` (`StepDeclaration`).** `{kind: active | wait, ref, declaration}`: `kind` is `active` for a state that runs a Step block and `wait` for a state that waits on a child recipe or on a person; `ref` is the block id (`step.produce`) for `active` and the child recipe id (`workflow.step`) for `wait`; `declaration` is the recipe's own one-line description of the state. The screens spec's `builder.feature` is illustrative and never served.
- **One current state.** Exactly one state of `states[]` is `active`, parked or escalated, as the instance's `state`; earlier path states are `done` and later ones `pending`. The screens fixture's INC-14 shows a comms child running beside `mitigate`; v1 cannot serve that (the golden shows `mitigate` only, `comms` pending), and the fixture's second live state is a schema v2 matter.
- **Children (v1).** A child exists only under the parent's current state when it is a child wait, and a v1 child's address entry is the parent's entry of the state that created it (the parent's path entry count on a linear walk; at least that where the instance looped). `children[].rows` of one group are sequential children, and v1 serves **at most one live child per wait** (WF §3.9). **Fan-out (schema v2 preview).** Several live children per wait appear only in the PRJ-sync (9 children) and project-many-children (24 children) goldens, which the screens fixture needs for UI wave 1: "schema v2 preview; v1 serves at most one live child per wait; re-recorded when v2 lands". In v2 the fan-out is sequential in the entry order.
- **Desk runtime until P8.** Until the agent runtime lands (WF §12), the deskCapable Steps of the recipes shipped under `recipes/` (`workflow.step` produce, checks and verify; the dev recipes' import) run as **desk tasks**: `current.desk` is set (`run` is `<entry of the state>.<n>`, `3.1`), the status detail is `desk task`, `complete` is the first command, the chip is `DECISION`, and a child row of such a state shows no agent. For a state whose block is in the shipped catalog, `current.desk.outputs` and the `--output <fact>=<file>` flags of the `complete` command are exactly that block's outputs required on the outcome the command reports (`step.produce`: `fact:output`, a file), and `current.desk.checks` are the checks the engine runs on a desk report before it accepts it: one per required output on that outcome, with id `<output name without "fact:">.<type>` (`fact:output` of type `file` is `output.file`, `fact:findings` of type `text` is `findings.text`), or `<name>.present` when the recipe declares no type; `facts[].pending.checks` is the one check of that fact, and the state's token budget is served as 0 used, since no agent runs. Fixture-only recipes (`incident-response`, `triage-alert`, `project`, `intake-linear`, `fork-demo`) stand for the P8-era product the screens fixture shows: their agent states name an agent-runtime block and are `RUNNING` with an agent, and their desk states stay desk tasks. P8 re-records the desk-runtime states.
- **Timeouts and budgets.** Every active or wait state serves the recipe's timeout (desk at least `DeskTimeouts.DefaultFloor`) and its budget where the recipe declares one.

**`RecipeView` (E-22).** `GET /workflow/recipes/{id}` and `sindri recipe show` return the same body: the engine spec's
definition (states, transitions, evals, pins, open instances placed per state, the registry's approved hashes) plus:

| Field | What it holds |
|---|---|
| `id`, `hash`, `version` | The recipe's stamp (the same one an instance carries in `recipe`), served now ahead of the engine's full body: its id, its `sha256:<64 hex>` hash and its version |
| `inputs[]` | `{key, label?, type, kind, required, generate?}`. `type` is a fact type (the nine of `FactRow.type`); **`kind`** is the subject adapter (`item`, `linear-project`, `webhook`, `file`, `text`, `link`, `packet`, `payload`) and is served for every input; `generate` is the template text, and `required` is false when it is present |
| `name` | The recipe's name template as written, or null |
| `outputs[]` | `{key, label?, type, from}`: the new output declarations (WF `outputs`, E-22), with the added fact types of screens §5.2 |
| `orderedBy` | `"path"`, or `"transition-list"` when the graph has more than one initial path: the same field and meaning as on `InstanceView` |
| `loops[]` | `{name?, from, to, cap}`: the back edge of each loop and its cap (the most rounds before it parks; part of the recipe hash), for 'loop ≤ 3 with verify' (screens §4.2, §4.3) |

The CLI prints them under the recipe header, one line per key: `input: <key> type=<type> kind=<kind> [generate="<template>"]` for an input (a quote or backslash in the template is escaped with a backslash) and `output: <key> type=<type> from=<state>` for an output. The CLI also prints `name:` under the recipe header when the template is present.
A recipe with no `outputs` returns `[]`.

**`RecipeStats` (E-24).** Counts over **ledger events** for the window, computed by a query over `workflow.*` events and
cached per `(recipe, hash?, window)` in `views.db`; the cache is invalidated when a run-ending event for that recipe
appends, and `views rebuild` recomputes it. A child-only recipe returns `childOnly: true` and `null` run figures; its
per-state rows appear under its parents' stats.

```jsonc
// GET /workflow/recipes/workflow.item/stats?window=30d
{ "recipe": "workflow.item", "hash": null, "window": "30d", "childOnly": false,
  "runs": 112, "done": 97, "failed": 6, "cancelled": 9,
  "outcomes": { "doneWithoutHuman": 81, "doneAfterPark": 12, "doneAfterTakeover": 4 },
  "humanRate": 0.19,                       // runs with a park answered, an escalation decided or a takeover
  "medianSeconds": 6000,
  "tokens": { "p50": 310000, "p90": 710000 },
  "cost": { "usdP50": 1.84, "usdTotal": 214.0, "basis": "price-table" },   // "tokens-only" when the table lacks a model
  "budgetHits": 2,
  "humanTime": { "parksAnswered": { "n": 16, "medianSeconds": 7800 },
                 "escalationsDecided": { "n": 11, "medianSeconds": 2520 },
                 "takeovers": { "n": 7, "medianSeconds": 1500 } },   // park or escalation to its answer; takeover to handback
  "byState": [ { "state": "produce", "medianSeconds": 3120,
                 "parks": [ { "reason": "no-progress", "n": 14 } ],
                 "escalations": [], "takeovers": 6,
                 "gateRefusals": null,
                 "loops": { "avg": 1.6, "max": 3 } },
               { "state": "gate.verify.judge", "medianSeconds": null, "parks": [],
                 "escalations": [ { "reason": "below 0.80", "n": 11 } ], "takeovers": 0,
                 "gateRefusals": { "refused": 11, "evaluated": 108 }, "loops": null } ],
  "buckets": [ { "weekStart": "2026-09-21", "runs": 20, "humanRate": 0.10 } /* … four, oldest first */ ],
  "asOf": "…" }
```

An unknown `window` or `hash` format answers `SND-API-013`; an unknown recipe answers the engine's `SND-RECIPE-002`.
`failed` means ended in `cancelled` or a terminal failure state, and is counted apart from `cancelled`.

**`RecipeList` (E-25).** One `RecipeSummary` per recipe: `{id, hash, version, schema, status, statusDetail?, open,
childOnly, lastChange: {date, version}, stats}`.
- `status` is the registry's word (WF P2): `approved`; `changed` (approved, but a pinned block or the file changed since,
  `SND-RECIPE-006`, with `statusDetail` naming what); `draft` (never approved); or `revoked` (rendered like `draft`,
  with its word).
- `stats` is the `RecipeStats` subset the list shows, at the request's `window`: `runs`, `done`, `failed`,
  `humanRate`, `medianSeconds`, `tokens.p50`, `buckets`. It is `null` for a draft and for a child-only recipe.
- The list's header figures (recipes, window, runs, share done without a person) are `RecipeList.totals`.

**The engine in depth (E-30).** Served by Sindri from the Temporal .NET SDK's client (`DescribeWorkflowExecution`,
`GetWorkflowHistory`, the task-queue and worker descriptions) in `Aw.Sindri.Infrastructure.Engine`, never from Temporal
directly: the app, the CLI and the MCP server hold no Temporal client and never learn its port (an architecture test
forbids a `Temporalio` reference outside that project). The ledger side belongs to the engine spec: the mirror writes
the Temporal event id on each ledger row it mirrors (WF §5.6, ERD), so a history line can name its ledger sequence.

```jsonc
// GET /engine   (EngineView)
{ "server": { "kind": "temporal-dev", "namespace": "sindri", "upSeconds": 273600, "dbBytes": 50331648,
              "uiUrl": "http://127.0.0.1:8233" },             // uiUrl only while the dev server's UI is up (WF §6.1)
  "worker": { "count": 1, "identity": "sindri-48211", "taskQueue": "sindri", "polling": true,
              "activitiesRunning": 2, "backlog": 0, "lastHeartbeatSeconds": 1 },
  "mirror": { "lagEvents": 0, "lagMs": 0 },
  "pending": { "updates": 0, "signals": 0, "stuck": 0, "continueAsNew": 0 },
  "asOf": "…" }

// GET /engine/instances   (each EngineInstance)
{ "id": "WFI-29f1d759…", "address": "ITM-01JE9/item/1/step.4", "recipe": { "id": "workflow.step", "hash": "3f1c09…" }, "state": "checks",
  "waitingOn": { "kind": "activity", "name": "step.checks", "ageSeconds": 120 },   // activity | signal | update | child | timer
  "timers": [ { "name": "produce-timeout", "firesAt": "…", "kind": "timeout" } ],
  "retries": { "attempt": 2, "lastError": "exit 1" },
  "historyLength": 88, "stuck": false }

// GET /engine/instances/WFI-29f1d759…/history?page=1   (each event, newest first)
{ "eventId": 412, "time": "…", "type": "ActivityTaskScheduled", "name": "step.checks",
  "summary": "attempt 2 · retry after exit 1 (…) · backoff 10 s", "ledgerSequence": 4231 }   // null when not mirrored
```

- **Stuck** (screens §4.6): an instance whose only pending work is a signal or update older than 5 minutes with no
  timer. `pending.stuck` counts them, and a nonzero count adds a `degraded` contribution to `health.word` on
  `GET /status`.
- `ledgerSequence` joins the history event to the ledger row whose mirrored Temporal event id equals `eventId`; the UI
  opens that sequence through `GET /ledger?after=`.
- A server that cannot be reached answers `SND-ENGINE-001`; an address with no execution in Temporal (a retention
  edge) answers `SND-ENGINE-404`.

**The dependency graph on `GET /status` (E-29).** `GET /status` returns `dependencies` beside S1's checks and `health`
(screens §4.6):

```jsonc
"dependencies": {
  "nodes": [ { "id": "check:code-index", "label": "code index", "kind": "check",   // check | source | target | surface
               "layer": "source", "health": "failed", "since": "…",              // ok | degraded | failed | absent
               "detail": "nightly 03:00 failed (SND-INDEX-012)", "fix": "sindri index build" } ],
  "edges": [ { "from": "check:code-index", "to": "surface:context", "health": "failed" } ],
  "impact": [ { "node": "check:code-index",
                "consumers": [ { "id": "surface:context", "sentence": "symbols lens stale since 10-12", "degrades": true },
                               { "id": "recipe:builder.feature", "sentence": "input.context.retrieve degraded (cited results only)", "degrades": true } ],
                "openInstances": 2, "fixAction": "RunService" } ],
  "asOf": "…" }
```

- **Nodes** are the doctor's checks, the approved profile's sources and the launch targets (`GET /launch/targets`,
  P10a; absent targets show `absent`). The doctor's per-check **`consumers`** declaration, and reading the
  `deps.yaml` edges at build, are S1's doctor and `scripts/check-deps.mjs` (screens §8.1), not this spec's; this
  route only serves what they produce.
- **`impact`** has one entry per node that is `failed` or `degraded`, and `consumers` is the **transitive closure** of
  that node's consumers, each with its own sentence. `fixAction` is a catalog action id when a control exists
  (`RunService` for a service, `copy:<cli>` otherwise).
- The layer (`source`, `engine`, `surface`) is a field of the node, so the fixed layered drawing needs no layout logic.
- **Lands with E-29 after #190; acceptance (moved here from the P2 row):** with a fixture whose doctor declares the code-index check consumed by two surfaces, marking its last-run row failed makes `GET /status` show that node `failed` with an `impact` entry listing both consumers (and the consumers' consumers) within one doctor interval; fixing it clears the entry.

**Agent budgets (E-28).** `agents.<target>.dailyBudget` in the profile is a decimal amount in USD for one launch
target (`claude-code`, `codex`, `cursor`); `agents.softLimit` is a fraction in (0, 1] (default `0.8`), and a `dailyBudget` must be above 0; profile validation refuses anything else (provisional owner default, 2026-10-17). The hard limit is 100 %.
The key is part of the profile, so a change shows in `GET /profile` and needs the `protected` approval like any other
(the profile schema is S1's; this spec names the keys). `GET /launch/targets` (UI E-3) gains per target
`budget: {dailyUsd, spentUsd, fraction, state, basis}`, with `state` `none` (no budget set), `ok`, `soft` or `hard`, and
`basis` `price-table` or `tokens-only` (the scorer's price table lacks the model: tokens are shown and no limit is
applied); the response gains `budgetTotal` with the same fields over all targets. Spend is the day's token usage
(`session.usage` and `session.ended` events, §3.4) priced by Sindri's own price table (`Budgets`: USD per million tokens, matched by model-id prefix; the scorer reports tokens and keeps no prices), for the local calendar day. The
Agents page reads this, and the collector's `GET /sessions` (§11) carries each session's tokens.
- **At the hard limit,** `OpenItem` for that target is refused with `SND-LAUNCH-010`, and the launcher re-checks when
  it launches (the spend can move in the 30 s between) and appends `launch.failed` with the same code. The engine's
  agent runtime launches through the same port and gets the same refusal; how a Step parks on it is the engine spec's.
- **At the soft limit,** nothing is refused; `OpenItem`'s response carries `warnings: ["budget-soft"]` and the target's
  `state` is `soft`.

## 4. Commands vs queries, decisions vs requests

### 4.1 Action classes (one per command or variant)

| Class | Meaning | Commands |
|---|---|---|
| `request` | A fact filed for a human or the engine to act on; changes no item state | `FileRequest` |
| `step-report` | A launched agent session reports the result of the Step run it was launched for. Proven by the launch secret (§8.1), never by the public run token; the engine still decides through the recipe's gates | `CompleteStep` from the agent runtime's session (§8, engine §6.5) |
| `pause` | Only stops automation. It is safe in one direction: resuming is `desk` | The engine's `ApplyCommand:takeover` |
| `job` | Read-only work, possibly long-running; writes only its own job events and, for `RunService`, the run's `service.ran` | `RunService`, `CancelJob` |
| `decision` | A non-protected human decision (the §17.12 list) | `Decide` |
| `desk` | A human action that changes item or process state (§17.27.2) | `StopProcess`, `RebuildViews`, `CreateItem`, `CloseItem`, `PauseService`, `ResumeService`, `OpenItem`; the engine's start, answer, handback, cancel and complete |
| `protected` | The approval tier: a typed confirmation, bound to the content, at a terminal | `ApproveProfile`, and the engine's `ApplyCommand:answer-grant`, `answer-accept` and `recipe approve`. Later: evolve adopt, channel promote and tier raises |
| `local` | Runs in the CLI, never through the API | `doctor`, `profile validate` |
| `issue` | Issues a one-time value and appends no domain event (audited by `command.handled`). The only `POST`s that aren't commands (§2.5) | `IssueNonce` (any caller whose class may run a command on that instance, repo-scoped for sessions), `IssueChallenge` (`human` + terminal), and `session wrap`'s heartbeat and exit (the launch secret, §8.1) |

### 4.2 Callers and the authorization table

| Class \ caller | `human` + terminal | `human` (app, no terminal) | `session` | `service` (in-process only) |
|---|---|---|---|---|
| any query | yes | yes | yes, scoped to its repo (§3.1) | yes |
| `request` | yes | yes | **yes**: at most 20 open requests per repo, 50 in total (`SND-REQ-002`) | yes |
| `step-report` | no | no | **only** the session the agent runtime launched for that run, proven by its run-scoped token (§6) | no |
| `pause` | yes | yes | **yes** | yes |
| `job` | yes | yes | no: file a `request` | yes |
| `decision` | yes | yes | **no** | no: it may only *raise* a decision |
| `desk` | yes | yes | **no** | only the engine commands its recipe step owns (the engine spec lists them), and `service:host` → `StopProcess` (§2.1) |
| `protected` | yes | **no**: shown as `cli-only` | **no** | no |
| `issue` | per row in §4.1 | per row | per row | no |

**The app is a full desk client.** It starts workflows (scoping included), answers parks, takes over and hands back,
closes items, runs services and hands work off to a session in Warp with Claude Code, Codex or Cursor (`OpenItem`).
Only `protected` actions show the CLI to copy.

**Why `pause` is open to sessions.** Takeover is detected by the hook, which runs inside the agent session and is
therefore a `session` caller. Pausing an instance only stops automation, so it is safe in that direction; handing back
is `desk`. That's why a session may take over, but never hand back.

**`service` never arrives over HTTP.** The engine and the scheduler call handlers in-process with a `service:<name>`
caller context, so nothing on the wire can claim it.

**A refusal is a 403:** `SND-AUTH-002 <Command> is a human decision. Ask the human to run it in a terminal or the
Sindri app: sindri <verb> …`. Inside Claude Code, `!sindri …` runs with the agent's environment and is refused too.
Nothing is written except the audit row.

A question answered inside a session stays an `answer` turn, classified by the hook (§9.3). The API doesn't replace
that path.

## 5. The event stream

### 5.1 What it's for

It tells live clients "something changed, re-read":
- **The Avalonia app:** refreshes the affected view, and drives the needs-you pulse and the tray badge. OS
  notifications come only from the engine's `notify.desk` (engine §6.3); the app never raises one itself, before or
  after engine P6 (UI spec X5), so nothing fires twice.
- **The CLI's long-running verbs:** job progress and cancel.
- **`sindri status --follow`**.
- **Later:** the notifier, the phone head, and other instances.

It is not:
- the source of truth (the projections are);
- the engine's or the projector's input (they read the ledger by cursor, in-process);
- a way to send anything to Sindri.

### 5.2 Transport and filters

The endpoint is `GET /events?after=<sequence>&item=<itemId>&types=<glob,glob>`, sent as Server-Sent Events
(`TypedResults.ServerSentEvents`, as in S1).
- On a reconnect, `Last-Event-ID` wins over `after` (conflict C4).
- A read error ends the stream with `event: error` and the SND code, instead of being swallowed.
- A `session` caller's stream is filtered to its repo (§3.1). That filter, and the one on `ReadLedger`, also drop any
  event whose payload `repo` names another repo, such as a request filed elsewhere without an item, or its decision (`decision.recorded` carries the decision's `repo` for this).
- Not WebSocket: the stream is one-way, SSE resumes natively, it works with `curl -N`, and the Avalonia client reads it
  with a streaming `HttpClient`.

### 5.3 The public envelope

```
id: 4211
event: item.state-changed
data: {"sequence":4211,"id":"0192…","type":"item.state-changed","schemaVersion":1,"occurredAt":"2026-10-10T14:02:11Z","actor":"human:cli","tenant":"local","ring":0,"item":"ITM-0192…","correlationId":"0192…","causationId":null,"traceId":"00-…","payload":{"commandId":"0192…","item":"ITM-0192…","from":"new","to":"done","version":3}}
```

- `PublicEvent` is S1's `LedgerEvent`, flattened:
  - `sequence` and `item` come from the ledger row;
  - the rest comes from the Kernel `EventEnvelope`;
  - `payload` is the event's public payload record from `EventCatalog`.
- Event types that aren't public are never sent.
- Payloads carry ids, states and short scrubbed titles. They never carry packet, request or session text.
- Clients re-query projections when an event arrives. The UI never folds events into state itself (§17.27.1).

### 5.4 Back-pressure and limits

- **No server-side buffer.** The server reads one page at a time with `ReadAfter(cursor, 100)` and writes it out. A
  slow reader only slows its own loop, because writes wait on TCP.
- **No retention, so no `reset`.** The ledger never deletes events, so any `after` can be served. An `after` greater
  than the head is a 400. A `reset` event is reserved for when retention lands.
- **Keep-alive:** a `: ping` comment every 15 s.
- **Stream limits:** at most 16 concurrent streams, with 4 reserved for `human` callers, so `session` streams are
  capped at 12. Past a limit the stream gets `SND-API-012` (429), and the client falls back to re-querying every 5 s.

### 5.5 Latency

- S1 polls every 1 s.
- P5 adds `ILedgerSignal`, a port in `Core`. Its in-memory adapter is pulsed by the SQLite ledger adapter after a
  committed `Append`.
- P5 also connects it: the projector and the stream both wait on it, so they wake within milliseconds.
- The 1 s poll stays as the fallback.

## 6. Auth, v0

v0 is local-only. The loopback binding and S1's `LoopbackGuard` are the only network controls. There are **no tokens,
passkeys or tailnet listener** (§11).

What v0 keeps is the caller class, so "session calls are requests, never decisions" holds as a guard against honest
mistakes. That's the repo's threat model: gates guard against honest mistakes, not a hostile agent.

**The headers.**

| Header | Values | Meaning |
|---|---|---|
| `X-Sindri-Caller` | `human`; `session` or **none**; any other value | `human` gives `human`. `session`, or a missing header, gives `session`, which is the safe default for an agent's `curl`, a script it wrote, or a fetch tool. Any other value is `SND-API-007` (400) |
| `X-Sindri-Terminal` | `1` | Sent only by the CLI when stdin and stdout are a terminal. It's honoured only alongside `human` |
| `X-Sindri-Client` | `cli`, `ui`, `prover` | Labels the actor (`human:cli`, `human:ui`, `human:prover`). With no label, a human caller is `human:http` |
| `X-Sindri-Repo` | a repo name | Scopes a `session` caller (§3.1) |
| `X-Sindri-Token` | the per-start token | Required with `human` (P1); `SND-AUTH-001` when missing or stale |
| `X-Sindri-Launch-Secret` | a launch's secret | Proves `step-report` and `session wrap`'s calls for that launch (§8.1); `SND-AUTH-003` when it doesn't match an open launch |

**Who sends what.**
- **The CLI** sends `human` with `X-Sindri-Terminal: 1` only when stdin and stdout are a terminal **and** none of these
  agent-session variables is set:
  - `CLAUDECODE` (Claude Code);
  - `AW_SINDRI_CHILD` (Sindri);
  - `CODEX_SANDBOX` (Codex);
  - `CURSOR_AGENT` (Cursor's agent).

  Otherwise it sends `session`. The list comes from the engine's harness probe (engine §6.2, P2), golden-tested, and
  a parent process that is an agent harness also counts. These are variables the tools set, not ones Sindri reads as config. They go in
  `scripts/env-scan-ignore.txt`, next to the existing `CLAUDECODE` and `AW_SINDRI_CHILD` lines, not in `.env.example`.
- **The Avalonia app** sends `human` with no terminal.
- **The MCP endpoint** stamps `session` whatever the request's headers say, keeping only `X-Sindri-Repo`.

**Commands refuse any `Origin` header** with 403 `SND-API-008`.
- No first-party client sends one, so this stops a web page in a local browser from driving a command that has no body.
- A browser also can't send `X-Sindri-Caller` without a CORS preflight, which Sindri never answers, so its requests are
  `session` anyway.
- Queries keep S1's guard (conflict C3).
- Refusals with `SND-API-007` and `SND-API-008` are logged.

**Protected actions in the UI** show the CLI command to copy (`cli-only:typed-confirmation`), because the app has no
terminal until passkeys exist.

**The `human` token (P1).** The app is a full desk client, so the header alone is too thin. On each start `serve`
mints `auth/human.token` (0600) under `$AW_SINDRI_HOME`; the CLI and the app read it and send it as `X-Sindri-Token`.
A `human` header with a missing or stale token is refused with `SND-AUTH-001` ("restart the app or rerun the
command"), never silently downgraded. The CLI doesn't read the token when an agent variable is set. Launched sessions
prove `step-report` with their launch secret instead (§8.1). Session-class tokens and passkeys stay later (§11).

**The prover.** `sindri-prove.sh human` reads the token from the guarded temp home, and runs the CLI with
`AW_SINDRI_PROVER=1`, which exempts it from the parent-process check. The server honours that marker only when its
own home is under `$TMPDIR` or a proof folder, so the owner's real process never does.

**Out of scope for v0:** a session that deliberately reads the owner's token file (§11).

**When tokens arrive,** only the source of the caller class and the terminal flag changes. The authorization table, the
decorators and every test stay.

## 7. Finding or starting the process

S1's model stands:
- the server writes `control.json {url, pid, epoch}` (0600) once it listens;
- a client reads it, checks that `url` is a loopback address, and checks the pid and epoch against `GET /ping` with a
  2 s timeout, so a stale file reads as not running (`SND-API-001`);
- bare `sindri` spawns `sindri serve` detached and waits up to 10 s (`SND-CLI-002`).

This design adds which verbs start the process:

| Verb | When the process isn't running |
|---|---|
| `sindri`, `sindri --headless` | Start it (S1) |
| Commands and queries run by a human (`decide`, `item add`, `queue`, …) | Start it, then run. `--no-start` prints `SND-API-001` instead |
| **Any verb inside an agent session** (the §6 variables) | Never start it. Print `SND-API-001` ("Sindri isn't running; ask the human to run `sindri`") |
| `status`, `stop` | Never start it. As in S1, `status` prints `not running` and exits 1, and `stop` exits 1 |
| `doctor`, `profile validate` | Run locally (S1) |
| The MCP endpoint | Served by the process. When it isn't running, the session's MCP client reports the server as failed; nothing starts it |

## 8. The MCP server

One MCP endpoint serves every Sindri tool, this spec's and the context engine's:
- **`/sindri/mcp`** on the API port: Streamable HTTP in **stateless mode**, so a Sindri restart doesn't break a
  session, with the official MCP C# SDK (`ModelContextProtocol.AspNetCore`, approved in the readiness AOT spike);
- behind S1's `LoopbackGuard` and the command-side `Origin` refusal (§6);
- every call is stamped `session`. The repo comes from the `X-Sindri-Repo` header in the repo's own registration;
- tools are registered explicitly. Each `sindri_*` tool is exactly one API call through the same handlers; the
  context engine's `context_*` tools call `IContextEngine` (context spec §8.5);
- the endpoint holds no logic, and errors come back as tool errors carrying the SND code and message;
- repo scope is enforced by the server (§3.1), so the tools have no "all repos" argument.
- a call with no `X-Sindri-Repo` is refused (`SND-CONTEXT-021`), whatever the tool: every MCP session comes from a
  registration or config that carries the header, so §3.1's repo-less session view is HTTP only;
- the endpoint keeps only `X-Sindri-Repo` of the request's `X-Sindri-*` headers (the launch secret never rides MCP, §8.1)
  and sets `X-Sindri-Client: mcp`; its own refusals are `SND-CONTEXT-021`, `022` (a bad argument) and `023` (an unknown tool).

**Registration is per repo, by the owner, never user-wide.** `sindri mcp register` prints, and with `--apply` runs,
`claude mcp add --transport http --scope local --header "X-Sindri-Repo: <name>" sindri http://127.0.0.1:<port>/sindri/mcp`
inside the current repo (the name resolved as §3.1 says). P8 checks whether that local scope reaches sessions in the
repo's worktrees and records the answer.

**Codex and Cursor are never registered.** Codex keeps MCP servers only in user config, and Cursor's project file sits
inside the repo, so either would leak work context or get committed. Instead `ILauncher` passes the endpoint at
launch time, in the command line it builds (Codex: `-c` config overrides for one `mcp_servers.sindri` entry with the
repo header; Cursor: the launch-time equivalent its CLI offers, recorded in P10). Until P10 proves a target's form,
that target launches without Sindri MCP. `sindri doctor` warns on any user-wide `sindri` entry in `~/.claude.json`,
`~/.codex/config.toml` or `~/.cursor/mcp.json`. Personal repos never see work context. Nothing in `setup.sh` or the providers registers it before the switch.

| Tool | Arguments | API call | Kind |
|---|---|---|---|
| `sindri_status` | — | `GET /status` | query |
| `sindri_queue` | — | `GET /queue` | query |
| `sindri_item` | `itemId` | `GET /items/{itemId}` | query |
| `sindri_item_timeline` | `itemId`, `after?` | `GET /items/{itemId}/timeline` | query |
| `sindri_pending_decisions` | `itemId?` | `GET /decisions?pending=true` | query |
| `sindri_job` | `jobId` | `GET /jobs/{jobId}` | query, **from P6** |
| `sindri_request` | `kind`, `itemId?`, `text` | `POST /requests` (`FileRequest`) | request |
| `context_query`, `context_node`, `context_neighbors`, `context_sources` | context spec §8.5 | `IContextEngine` | query, **with S3**; off while `context.sessions.enabled` is false |

Every free-text field a `sindri_*` result returns that isn't Sindri's own wording (item titles and bodies, timeline
lines, decision questions and notes, job summaries and progress lines) is fenced and the object holding it carries
`"trust": "untrusted"`, as `context_*` results do (context spec §7), whoever wrote it. `sindri_request` waits (up to 2 s)
for the views to reach its own write before it answers, so a `sindri_pending_decisions` straight after it lists the
new decision. The tools are listed in this table's order, after the five `context_*` tools. There is no decision, desk, protected or job tool, so the MCP surface can't even attempt one. Query tools carry the MCP
`readOnlyHint` annotation. Whichever lands first, context Phase 1 (S-1M) or this spec's P8, creates the endpoint; the
other adds its tools to it. `sindri mcp register` ships with whichever creates the endpoint.

**Proof.** The proof run starts a fresh Claude Code process as the subagent:
```
claude -p "<journey>" --mcp-config <tmp>/sindri-mcp.json --strict-mcp-config \
  --allowedTools 'mcp__sindri__*,Bash(curl:*)'
```
- The config is an `http` entry pointing at the phase's process (its `control.json` URL plus `/sindri/mcp`) with the
  test repo's `X-Sindri-Repo`.
- Nothing is registered with the provider, so the cutover rule holds and there is nothing to clean up.
- The user's own hooks may load; the journey doesn't depend on them.

### 8.1 The launch contract (`ILauncher`, P10)

One port launches every agent session: the human handoff (`OpenItem`) and the engine's agent runtime (engine §6.5).
- **One target kind, and tmux owns it (rev 8, README X55; replaces the earlier `interactive` and `process` kinds).**
  Every launch, for the human (`OpenItem`) and for the engine's agent runtime alike, starts the agent CLI
  **interactively** inside a tmux session Sindri owns, named `snd-<run>` (or `snd-<launch>` for a human open with no
  run), in the item's repo or the Step's worktree, with the seed as the initial prompt. **Warp is a viewer:** opening
  for a human also opens a Warp tab that runs `tmux attach -t <session>`; the `--headless` box opens no Warp and the
  session simply runs detached. So attended and unattended runs are the same process, Sindri can see the session
  (`tmux has-session`; the human-only live preview's read-only `capture-pane` is served unscrubbed and never stored, owner 2026-10-17, collector CA-9), and the human can join any Step live. Non-interactive runs (`claude -p`,
  `codex exec`) are never launched as a Step. The tmux socket is Sindri's own (`-L sindri`), so the human's own tmux
  is untouched.
- **Ids only at launch.** The command line carries the item id, the repo path and, for a Step, the run token. Context,
  packets and evidence are pulled through the MCP endpoint (Codex and Cursor get it as launch-time config, §8).
- **`sindri session wrap`.** Every launched command runs as `sindri session wrap --launch <id> -- <agent cli …>`. Wrap
  reads the launch secret from a one-time file (`$AW_SINDRI_HOME/launch/secrets/<id>.secret`, 0600, its path in `AW_SINDRI_LAUNCH_SECRET_FILE`), deletes it, and passes
  the secret to the agent only through its environment (`AW_SINDRI_LAUNCH_SECRET`), never argv or a shared file. It
  also sets `AW_SINDRI_CHILD`, so the CLI inside is a session.
- **Liveness.** Wrap sends a heartbeat every 30 s and an exit call when the agent exits (both `issue` kind, proven by
  the secret). `service:launcher` appends `session.ended` with reason `exit` and the exit code, or `lost` after 120 s
  without a heartbeat. So a closed tab or a crash always ends the session; the double-launch guard clears.
  Every accepted heartbeat is audited like any `issue` call (a `command.handled` row) and takes the command gate, so one
  live session writes about 2,900 audit rows a day; that volume is accepted for now. A self-audited flag for accepted
  heartbeats (no row, as for `StopProcess`) is the way out if the gate or the ledger's size ever matters (review of #295, I26).
- **Per-launch files.** A launch that opens Warp writes its own Warp launch configuration
  (`launch_configurations/sindri-<launch>.yaml`, 0600, no secret in it) whose one `exec` line is the `tmux attach`, and
  deletes it once Warp has read it. Paths are quoted. The agent command itself is started by Sindri with `tmux new-session
  -d`, never by Warp, so a Warp that fails to open leaves the session running and attachable.
- **Usage.** Token usage is read by the target's usage reader (Claude Code's session log, and the Codex and Cursor
  equivalents recorded in P10) when the session ends, and recorded on `session.ended`. It is never self-reported. While the session is live the same reader appends `session.usage` samples every 60 s (E-28), so a running session counts against its target's daily budget (§3.8); the samples are not public.
- **Budgets (E-28).** A target with `agents.<target>.dailyBudget` is refused at its hard limit with `SND-LAUNCH-010` by `OpenItem` and again by the launcher at launch time (§3.8).
- **`OpenItem` is one event.** The command decides and appends `item.open-requested`; it does no launching (UI E-1).
  A live instance is taken over by the engine's `open-request` trigger (engine §5.4, UI E-6) before the launcher opens
  anything; the launcher then performs the launch once within 30 s and appends `launch.started` or `launch.failed`.
  A second open while a session is live offers `attach` instead (UI E-2); a launch already in progress is
  `SND-LAUNCH-008`.
- **Attach** (rev 8) means joining the live session: a new Warp tab running `tmux attach -t snd-<run>` on Sindri's
  socket. It starts no agent process. The first turn the human types is classified `takeover` by the session's
  `UserPromptSubmit` hook (D §9.3) and pauses the instance (engine §5.5); until `handback`, `step-report` from that
  session is refused (`SND-AUTH-003`). Attaching to a session the human opened themselves is a no-op takeover. "Sindri
  can't see inside Warp" no longer applies: the session is Sindri's tmux session, and Warp only shows it.

## 9. Errors, versioning and JSON

### Errors

S1's `HttpResults` mapping is unchanged: `ErrorKind` sets the status, and `ProblemDetails` carries `title` = the SND
code and `detail` = the message with its `(fix: …)`. The CLI prints `SND-… message` and exits 0 for ok, 1 for attention,
2 for an error (S1), and 75 when a heavy-job lock stayed busy (as Evidence and the context engine do).

An error may carry extension members (RFC 9457 §3.2), named camelCase and written as top-level members of the problem body
after the standard ones; their string values and the member names inside objects are cleaned (scrubbed, escaped, one line, at most 200 characters), and an array is cut at 100 items, and each code
that uses them lists them in its row. The CLI ignores them.

The phase that reserves a code registers it in `SindriErrors` and its `All` list, and documents it in the error table
of **`docs/csharp/sindri.md`**. Not `docs/sindri/errors.md`, which is generated from the TS build.

| Code | Kind (status) | When | Phase |
|---|---|---|---|
| `SND-AUTH-002` | Forbidden (403) | The caller may not run the command's action class; also `CompleteStep` on a human-only Step (DT-R1) from a caller that isn't a human | P1 |
| `SND-API-007` | Invalid (400) | An unknown `X-Sindri-Caller` value | P1 |
| `SND-API-008` | Forbidden (403) | A command carried an `Origin` header | P1 |
| `SND-API-009` | Unavailable (503) | The command gate wasn't acquired within 5 s | P1 |
| `SND-API-011` | Unavailable (503) | The views didn't reach `X-Sindri-After`, or the head, within 2 s | P2 |
| `SND-API-010` | Unavailable (503) | A command that reads views arrived during a rebuild; it wasn't run, so retry | P2 |
| `SND-VIEW-001` | Unavailable (503) | The projector stalled on an event, or can't run because views.db or the ledger can't be used (status, doctor, logs); the fix is `sindri views rebuild`, or `sindri doctor` for a ledger cause | P2 |
| `SND-ITEM-404` | NotFound (404) | No such item, or it's outside a session's repo | P3 |
| `SND-ITEM-409` | Conflict (409) | The item changed since you looked, or the decision was decided elsewhere | P3, P4 |
| `SND-REQ-001` | Invalid (400) | An unknown request kind, or text that's empty or over 2,000 characters | P4 |
| `SND-REQ-002` | RateLimited (429) | Too many open requests (§4.2) | P4 |
| `SND-DEC-404` | NotFound (404) | No such decision | P4 |
| `SND-DEC-001` | Invalid (400) | A choice that isn't one of the decision's options | P4 |
| `SND-API-012` | RateLimited (429) | The stream limits (§5.4) | P5 |
| `SND-JOB-404` | NotFound (404) | No such job | P6 |
| `SND-SVC-404` | NotFound (404) | No such service in the approved profile | P6 |
| `SND-JOB-409` | Conflict (409) | Three causes: a job of that kind and key is already running (the only one with the extension `runningJob`); a scheduled run of the service is in flight (`RunService`, no job to name); `CancelJob` met a job that already ended | P6 |
| `SND-JOB-001` | Unavailable | The process stopped while the job ran; only ever a job's `code`, never an HTTP response | P6 |
| `SND-JOB-002` | RateLimited (429) | Too many jobs are running (8) | P6 |
| `SND-LAUNCH-001` | Unavailable (503) | The launcher couldn't open the terminal | P10 |
| `SND-LAUNCH-002…009` | — | As the UI spec's E-5 numbers them (no terminal, agent CLI missing, repo missing, nothing to resume or attach, interrupted, already in progress, timed out) | P10a |
| `SND-LAUNCH-010` | RateLimited (429) | The target's spend today has reached its `agents.<target>.dailyBudget` (E-28). Fix: raise the budget in the profile and approve it, or wait for the local day to roll over | P10a |
| `SND-LAUNCH-404` | NotFound (404) | No launch has that id, or a session caller may not see it (`GET /launches/{launchId}`) | P10a |
| `SND-API-013` | Invalid (400) | A query parameter outside its set: a stats or list `window` that isn't `7d`, `30d` or `90d`, a `hash` that isn't a recipe hash, or a history `page` below 1 (E-24, E-25, E-30) | P12, P13 |
| `SND-ENGINE-001` | Unavailable (503) | The Temporal server, or the SDK call to it, didn't answer (fix: `sindri doctor`; `sindri serve` starts the dev server) (E-30) | P13 |
| `SND-ENGINE-404` | NotFound (404) | No execution in Temporal for that address (E-30) | P13 |
| `SND-AUTH-001` | Forbidden (403) | `human` without a valid per-start token | P1 |
| `SND-AUTH-003` | Forbidden (403) | The launch secret doesn't match an open launch, or the run is taken over | P10 |

- `SND-API-006` is reserved for the idempotency key (§11).
- The engine spec reserves `SND-RECIPE-*` and `SND-WF-*`, and should not add its own authorization code (§3.5).

### Versioning

- There is no version in the path in v0. The CLI, the MCP endpoint and the UI are built from this repo and ship together.
- `GET /status` reports `apiVersion: 1`, and every response carries `Sindri-Api-Version: 1`. The UI checks it on
  connect and shows "Sindri and the app are different versions" on a mismatch.
- A breaking change later adds a `/sindri/v2` group and keeps `v1` for one release.
- Events carry their own per-type `schemaVersion`.

### JSON

- There is one context, S1's `SindriJsonContext`, used through `AwJson`.
- `--json` prints the API response body **exactly as received**. P1 changes S1's `ControlPlaneClient` to keep the raw
  body (conflict C5).

### Telemetry

Spans and metrics use the `Aw.Sindri` source and meter, as `docs/csharp/telemetry.md` describes.
- **Spans:** `sindri.command <name>`, tagged with class, caller and outcome; and `sindri.query <name>`.
- **Metrics:**
  - `sindri.commands`, a counter by class and outcome;
  - `sindri.command.gate_wait`, a histogram;
  - `sindri.projector.lag`, a gauge of head − cursor;
  - `sindri.streams`, a gauge by caller.

## 10. Conflicts with S1 (flagged, not redesigned)

| # | S1 today | What this design needs | Resolution |
|---|---|---|---|
| C1 | ~~`/sindri/sindri/status`~~ | — | Already fixed in S1 |
| C2 | No caller class: `ApproveProfile` trusts `confirmed` from any loopback caller | Only a `human` caller with a terminal reaches protected handlers | P1 adds the caller middleware and wraps S1's commands in the chain. Their handler bodies don't change. Their registrations move to `AddCommand`, and `StopProcessRequest` and `ApproveProfileRequest` gain the `ICommand<…>` marker, with their PublicAPI lines |
| C3 | `LoopbackGuard` allows a browser `Origin` on loopback | Commands refuse any `Origin` | P1 adds an endpoint filter on commands. S1's guard stays for queries |
| C4 | `EventStream` lets `after` beat `Last-Event-ID`, swallows read errors, and sends every type | `Last-Event-ID` wins; errors end the stream with their code; public types only; session repo filter | P5 changes S1's `EventStream` and `StreamEvents`. The route stays |
| C5 | Command responses have no `sequence`, and `ControlPlaneClient` re-serializes `--json` | `sequence` for read-your-writes, and the raw body printed | P1 adds `sequence` to `StopProcessResponse` and `ApproveProfileResponse`, and keeps the raw body. Both are additive |
| C6 | The singleton `EventFactory` stamps `host:sindri`, uses a new correlation id per event, and leaves `Item = null` | The actor from the caller, one correlation per command, the item set | P1 adds `ICallerContextAccessor` (an `AsyncLocal` singleton) and `EventFactory` reads it. The same change covers the engine's S1-D7 |
| C7 | `EndpointTable` and `CommandTable`/`Verbs` have no anchors, and `/new-slice` edits shared files at its own anchors even with `--files-only` | Slices can be scaffolded in parallel without touching shared files | P0 adds a `--slice-only` mode to `scripts/scaffold/new-slice.mjs`, with a case in `scripts/tests/scaffold.test.sh`. It writes the slice folder, the test, the builder and new ids, plus the slice's own lines in three generator-owned places: the unit's `PublicAPI.Unshipped.txt` lines, its `A.cs` factory method, and its `IdsJsonContext` entry. It touches no other shared file. P1 also restructures both tables to one line per slice under an anchor comment |
| C8 | The scheduler only logs a failed run (`SND-SCHED-001`), and a changed profile is a file on disk with no event | `service.ran` and `profile.checked` events for the views | <ul><li>P2 makes the scheduler append `service.ran` for every run that failed or did work, and the first ok after a failure; an idle run after an ok one writes nothing. A recorded row is keyed by service, epoch and slot.</li><li>A service reports an idle run (it did nothing and didn't fail) with `Idle: true` in its result. The scheduler keeps each service's last recorded outcome, and after a restart takes it from the service's latest `service.ran`.</li><li>P2 adds `profile-watch` as a **hosted service**. It isn't scheduled from the profile, so it always runs.</li><li>Every 30 s it hashes the live profile, and appends `profile.checked` only when the (approved, live) pair has changed since its last check.</li><li>So a revert or a re-approval writes `drifted: false`, and drifting back to an old hash writes again.</li><li>S1's `profile.approved` key already includes the previous hash (S1 `5c9c4a4`), so re-approving an old hash is recorded, and S1 needs no change.</li><li>On restart, `profile-watch` takes its last (approved, live) pair from the latest `profile.checked` event.</li></ul> |
| C9 | `ILedgerStore` has no way to read the head | The ledger head, for `asOf` and command waits | P2 adds `Head()` to `ILedgerStore` (additive, with a case in its contract suite) and `ILedgerReader` (the read-only port for `ReadLedger` and the projector) |

## 11. Later (designed in outline, not phased)

- **Auth:**
  - a session-class token (`auth/session.token`, 0600), beside P1's human token;
  - an `aw:sindri-auth` guard hook;
  - containerized factory sessions get only the session token;
  - passkeys through `IPasskeys` for the UI's desk and protected actions (§17.27.2);
  - a client `Idempotency-Key` with a durable command journal (`SND-API-006` when a key is reused with a different
    body);
  - an `Origin` allowlist, if a browser head of the app is ever built.
- **Tailnet:** a Unix-socket listener that `tailscale serve` proxies to, with caller class `remote`. Tailscale identity
  headers are trusted only there (§17.22 D10). It is query-only until passkeys.
- **Webhooks:**
  - `POST /webhooks/{source}` → `ReceiveWebhook`, with class `trigger` and the delivery id as the fact key;
  - profile `triggers:` match deliveries to standing workflows;
  - per-source signature checks.
- **Dashboard collector queries** (dashboard spec §4), served by the collector service inside the process:
  - `GET /sessions` and `/sessions/{provider}/{id}` (+ `/timeline`), for **Agents** (each session carries its tokens; the per-target budget bars come from `GET /launch/targets`, E-28, not from here);
  - `GET /repos`, for **Repos**;
  - `GET /work`, for the rest of **Automatic work**;
  - `GET /today`, for **Today**;
  - `POST /sessions/{provider}/{id}/attach` (desk);
  - `POST /needs-you/{id}/ack` (desk);
  - needs-you sources for sessions, stale indexes, judge health and PRs.

  These need their own spec once the collector is designed in C#.
- **Receipts and metrics:** `GET /items/{itemId}/receipt`, `POST …/receipt/verify` (§17.23.12), and
  `GET /metrics/today`.
- **Context retrieval:** owned by the context spec; its routes and classes are in §3.7, its tools in §8.
- **Inline projections:** a unit of work around `Append`, if read-your-writes waits ever matter in practice.
- **Retention:** if the ledger ever drops events, the stream's `reset` event (§5.4).

## 12. Test strategy, and how agents build and prove each phase

**Tests by layer.**

| Layer | Lane | What |
|---|---|---|
| Domain decisions (`Core`) | unit | <ul><li>Item transitions.</li><li>Request → decision derivation.</li><li>The authorization table: every class × caller × terminal combination.</li><li>`IClassifiedCommand` variants.</li><li>Fact-key composition.</li><li>Needs-you ordering and clearing rules.</li><li>`ItemActions.For(state)`.</li></ul> |
| Projections | unit | Each `IProjection` folded over event lists. Replaying the same events gives the same rows |
| Command chain | unit | <ul><li>The helper wraps every command in the §2.1 order.</li><li>A refused or invalid command writes only `command.handled`.</li><li>Losing a fact-key race gives `SND-ITEM-409`.</li><li>The gate times out with `SND-API-009`.</li><li>`StopProcess` and `RebuildViews` don't wait for the head.</li></ul> |
| Catalogs | unit | <ul><li>`ARCH-CQRS-003`.</li><li>Every route exists.</li><li>No stale `pending`.</li><li>`cli-only` only on terminal-bound rows.</li><li>Every event type is in `EventCatalog`.</li></ul> |
| Endpoints | integration | `WebApplicationFactory` over the real `Compose`, with S1's contract-tested in-memory fakes and `FakeTimeProvider`. For each endpoint: <ul><li>the happy path;</li><li>its error codes;</li><li>no header, or `session`, on every decision, desk and protected route → 403, and nothing written but the audit row;</li><li>`human` without a terminal on protected → 403;</li><li>an `Origin` on a command → 403;</li><li>a session repo scope test.</li></ul> `ARCH-CQRS-001` runs over the endpoint data source |
| Concurrency | integration | Two `Decide` calls on one decision, started together: exactly one `decision.recorded`, and the other gets `SND-ITEM-409` |
| Read-your-writes and freshness | integration | <ul><li>A command, then a query with `X-Sindri-After`, sees the write.</li><li>A stalled projector gives `SND-API-011` and `asOf.stalled`.</li><li>`views rebuild` still runs while the projector is stalled.</li></ul> |
| Projector rebuild | integration | Real SQLite in a temp dir: delete `views.db` and restart, and the same views come back (bodies equal, apart from `asOf`). During the rebuild, queries answer with `asOf.rebuilding` |
| Event stream | integration | <ul><li>Resume with `Last-Event-ID`.</li><li>The filters, including the session repo filter.</li><li>The stream limits.</li><li>Non-public types never appear.</li><li>An OSC or ANSI payload arrives escaped.</li></ul> |
| CLI | unit + integration | <ul><li>Verb routing.</li><li>The headers taken from the terminal and the environment.</li><li>The auto-start rules (§7).</li><li>`--json` equal to the API body.</li></ul> |
| Scaffold | bash | `scripts/tests/scaffold.test.sh` covers `--slice-only` (C7) |
| MCP server | **proof run** | A fresh `claude -p` subagent (§8, P8) |
| AOT | CI AOT job | S1's `aot-smoke`, extended in each phase with its new verbs |

**Scaffolding a slice into Sindri** (C7).
1. Run `node scripts/scaffold/new-slice.mjs --slice-only --package Sindri --slice <Name> --surface http …`. It writes
   the request and response records, the service stub, the validator, the red test, the builder and any new id. It
   declares their `RS0016` lines in the unit's own `PublicAPI.Unshipped.txt`, adds the slice's `A.cs` factory method and
   any `IdsJsonContext` entry, so the red test compiles. It touches no other shared file.
2. The implementer marks the request record `ICommand<…>` or `IQuery<…>` (the generator doesn't), then adds the
   slice's lines at the anchors in its own branch:
   - one `EndpointTable` line;
   - one `Verbs`/`CommandTable` line, plus its `Usage` line;
   - the `AddCommand` or `AddQuery` registration;
   - the `SindriJsonContext` entries;
   - the catalog rows;
   - the error codes;
3. The CLI verb is written by hand in `Host/Commands`.

**Proving a phase.** Every phase signal below assumes this recipe. The recipe lives in the plan and in this spec. It is
not in `docs/csharp/sindri.md`, because it isn't a user feature.
- **Golden JSON (UI E-17).** Every phase's integration tests write each route's response bodies per scenario to
  `tests/Aw.Sindri.Tests/Golden/Api/<route-slug>/<scenario>.json`, with a fixed `FakeTimeProvider` and seeded ids; the
  UI's fake API serves them.
- **Wrapper.** Every spec's proofs use `scripts/sindri-prove.sh`, added in P1 (the engine and context specs adopt it).
  `sindri-prove.sh <verb>` runs the C# CLI as the calling agent (`session`); `human <verb>` and `curl` act as the owner
  (below). It refuses to run unless `AW_SINDRI_HOME` is
  under `$TMPDIR` or the phase's proof folder, so it can never touch the owner's real ledger. It sets:
  - `AW_SINDRI_PORT=0`;
  - `SINDRI="dotnet run --project src/Sindri/Aw.Sindri.Host --"`, or the binary published to the phase's temp dir.
    Never the `sindri` on `PATH`, which is the TS CLI.
- **Isolation.** Every agent gets its own temp home. The process picks a free port and records it in `control.json`,
  so parallel agents never share a ledger, a lease or a port.
- **Acting as the human.** Implementers run inside Claude Code, so the CLI correctly classes them as `session`. A human
  step is run in one of two ways:
  - `sindri-prove.sh human <verb>`, which runs the CLI under `script -q /dev/null` with the agent variables unset.
    `script` provides the terminal; strip the CRLF from its output before comparing.
  - `sindri-prove.sh curl <method> <path> [json]`. It reads the URL from the guarded temp home's `control.json` and adds
    `X-Sindri-Caller: human`, `X-Sindri-Terminal: 1`, `X-Sindri-Client: prover` and `Content-Type`.

  Provers never hand-roll a human `curl`, so they can't reach the owner's real process.

  The ledger records these as `human:prover`, so the evidence shows that the prover stood in for the owner.
- **Seeding an approved profile.** Write the test profile, read its hash from `$SINDRI profile validate --json`, then
  run `printf '<first 6>\n' | sindri-prove.sh human profile approve <hash>`. This is the real terminal flow, so it works
  whichever confirmation S1 or the engine lands (C2).
- **Evidence.** Keep the commands, their output, and ledger excerpts. Before P5 ships `GET /ledger`, read them with
  `sqlite3 -readonly "$AW_SINDRI_HOME/ledger.db" 'select …'`. Store everything under the phase's proof folder.

**Docs per phase.**
- `docs/csharp/sindri.md` gains the phase's routes, verbs, events and codes.
- `docs/sindri/README.md` gets a pointer once v0 lands.
- `planning/API_CONTRACT.md` gets the API's contract once v0 lands, and `.agents/rules/mcp-servers.md` gets the `sindri`
  server in P8.
- `AGENTS.md`'s commands block changes only for a verb a human runs at the desk.
- Every phase runs the AGENTS.md merge gate.

## 13. Review resolutions (`/autoplan`, 2026-10-10; at most 3 passes)

The lens reviews are under `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-api-surface/`, with pass 1 in
`pass1/`, pass 2 in `pass2/`, and pass 3 at the top level.

### Pass 1 → rev 2

| Finding | Resolution |
|---|---|
| C-01 `SND-API-003` clash | New API codes start at 004 |
| C-02 a missing header counts as `human` | A missing header counts as `session` (§6) |
| C-03 a local browser page can drive commands | Commands refuse `Origin` (`SND-API-008`) |
| C-04 the audit is innermost | The audit wraps authorization and validation (§2.1) |
| C-05 the first-decision-wins and version race | Fact keys, the command gate, and waiting for the head |
| C-06 idempotent replay | No client key in v0; fact keys only (§11 keeps the journal) |
| C-07 no `POST /profile/validate` | `profile validate` and `doctor` are class `local` |
| C-08 park, takeover and handback have two owners | The engine owns them (§3.5) |
| C-09 needs-you is a queue field | It's now `GET /needs-you` (§3.6) |
| C-10 dashboard views aren't scoped | §3.6 says what v0 feeds, and §11 names the collector queries |
| C-11 no freshness signal | `asOf` (§2.2) |
| C-12 the wrong error doc | `docs/csharp/sindri.md` |
| C-13 `/new-slice` anchors | C7 (revised in rev 3) |
| C-14 agents can't act as the human | The prover recipe (§12) |
| C-15 shared home and port | Per-agent home and port |
| C-16 the MCP proof can't see its tools | A fresh `claude -p` |
| C-17 two events per command | One domain event per command |
| C-18 `EventFactory` | C6 (revised in rev 3) |
| C-19 the projector failing | `stalled` |
| C-20 missing dependencies | `EventCatalog` in P1, and the dependency fixes |
| C-21 the agent CLI auto-starts the process | Never inside an agent session |
| C-22 accepted requests do nothing before S2 | `recordedOnly` |
| C-23 building ahead | v0 = P1–P5 |
| C-24 design details | Adopted |
| C-25 cross-repo data | Repo scope (made server-side in rev 3) |
| C-26 `sindri open` injection | Ids only |
| C-27 stale S1 facts | Fixed |
| C-28 LOW items | Caps, stream reservation, the loopback URL check, the 429 fallback, `Last-Event-ID`, the 200-line view cap, audit per attempt |

### Pass 2 → rev 3

The pass-2 verdicts:

| Lens | Verdict |
|---|---|
| Product | PASS |
| Security | PASS |
| Architecture | NEEDS_WORK |
| Design | NEEDS_WORK |
| Devex | NEEDS_WORK |

Every HIGH and MEDIUM is fixed below.

| Finding (lens) | Resolution |
|---|---|
| The gate and the wait for the head have no limits, so the fix itself deadlocks (arch N1 HIGH, security N2) | The gate gets a 5 s limit (`SND-API-009`). Only commands that read views wait, for up to 2 s. `StopProcess` and `RebuildViews` never wait. Handlers never do slow work (§2.1) |
| Two authorization models across the specs (arch N2 HIGH) | One model, owned here: a terminal flag on `human`, per-variant classes through `IClassifiedCommand`, `protected` meaning human with a terminal, and `SND-AUTH-002`. The engine spec should drop `SND-WF-007` and `human-terminal` (§3.5, §4) |
| `--files-only` still edits shared files (devex H1) | C7 adds a real `--slice-only` mode with a scaffold test |
| P1's signal can't pass (devex H2) | The signal now uses the live hash, `Content-Type`, and `sqlite3 -readonly` evidence. The audit covers attempts that reach the chain, and binding failures are logged (§2.1) |
| Needs-you sources with no events (design N1 HIGH, arch N7) | C8: `service.run-failed` (P2) and `profile.drifted` (P4); replaced in the final edit by `service.ran` and `profile.checked`, both in P2 |
| No ledger head (arch N8) | C9: `ILedgerStore.Head()` and `IProjectorStatus` |
| `stop` can't be audited; audit failures undefined (arch N3) | `StopProcess` is self-audited, and the audit failure rules are in §2.1 |
| `EventFactory` is a singleton (arch N4) | `ICallerContextAccessor`, shared with the engine's S1-D7 (C6) |
| The takeover route can't be reached (arch N5, security N3, product #1) | The new class `pause` is open to sessions; handback stays `desk` (§4) |
| Two answer paths and two needs-me lists (arch N6) | Engine parks are rows of kind `park` answered by `ApplyCommand`. There's one list, `GET /needs-you` (§3.4, §3.5) |
| v0 items can't close (product #2) | `CloseItem` in P3 |
| A request's needs-you row is unclear (product #3, design N3) | One `decision` row per request, carrying `recordedOnly` |
| No clearing rule (design N2) | A clearing rule per kind (§3.6) |
| `actions` can't name a variant (design N4) | Action ids `<Command>:<variant>`, with one catalog row each |
| Item state before S2 (design N5) | Defined in §3.6 |
| CLI text for rebuilding and stalled (design N6) | `EmptyText` has both, and every list view has `empty` |
| `asOf` has no clock (design N7) | `projectedAt`, plus the 30 s stream staleness rule |
| Urgency per kind (design N8) | The table in §3.6 |
| The byte-match is flaky (design N9, arch L2) | Signals compare bodies with `asOf` removed (`jq 'del(.asOf)'`) |
| Repo scope is MCP-only and can be bypassed (security R1) | Enforced on the server for `session` callers, with no `all` argument |
| The prover recipe could touch the real ledger (security N1) | `sindri-prove.sh` refuses the default home; the `human:prover` actor label; the recipe kept out of the user docs |
| Shared-file rules contradict each other (devex) | Implementers add their anchor lines in their own branch, and the integrator resolves adjacent-line conflicts (Parallel delivery) |
| Evidence before P5 (devex) | `sqlite3 -readonly` |
| Variable names (devex) | `CLAUDECODE`, `AW_SINDRI_CHILD`, `CODEX_SANDBOX` and `CURSOR_AGENT`, listed in `scripts/env-scan-ignore.txt`. `CURSOR_TRACE_ID` (set in Cursor's normal terminal) and `CODEX_SESSION_ID` (unverified) are dropped |
| `claude -p` tool permissions (devex) | `--allowedTools 'mcp__sindri__*' 'Bash(curl:*)'` |
| `reset` can't happen (arch L3) | Dropped until retention exists |
| LOW items | <ul><li>Authorization runs before validation, and every command has a validator.</li><li>`ReadLedger` is sanitized, capped at `max` ≤ 500, and uses a read-only port.</li><li>The request cap is per repo and overall.</li><li>`SND-API-007` and `SND-API-008` are logged.</li><li>The body limit is 64 KiB.</li><li>P10 takes the repo path from the profile's repo entry.</li><li>The version key is unified as `v<version>`.</li><li>The engine's "starts if needed" follows §7.</li><li>An `Origin` allowlist waits for a browser head (§11).</li></ul> |

### Pass 3 → final edit (no fourth review, by the owner's cap)

The pass-3 verdicts:

| Lens | Verdict |
|---|---|
| Product | PASS |
| Design | PASS |
| Devex | PASS |
| Security | PASS |
| Architecture | NEEDS_WORK |

Both HIGHs and every MEDIUM are fixed in the text.

| Finding (lens) | Resolution |
|---|---|
| Ctrl-C on `serve` goes through the chain with no caller and is refused, so the lease stays held (arch R1, HIGH) | Host-internal stops use the caller `service:host`, which the catalog allows for `StopProcess` only, set explicitly by `ServeCommand`. A P1 signal and test cover it (§2.1) |
| No event for a successful run, so failure rows never clear (arch R2, HIGH) | `service.ran` with an outcome for every run, keyed with the epoch (which also fixes design R1). P2 and P4 signals cover "fail, then succeed" |
| A command reads a head that lags (arch R3) | Commands read `ILedgerStore.Head()`, and fail fast while the projector is stalled (security P3-3) |
| `profile-watch` is never scheduled; drift rows stick or are dropped (arch R4, design R2, R5) | `profile-watch` is a hosted service in P2, and `profile.checked` is written on every change, keyed by the previous check. Revert and re-approval clear the row, and S1 needs no change |
| The engine spec's caller model differs (arch R5, product #2) | §3.5 lists the exact edit the engine spec needs |
| Two classification rules for `ApplyCommand` (security P3-1) | One fail-closed rule: protected fields override the variant, and an unknown variant is protected (§2.1) |
| Raw human `curl` isn't tied to the temp home (security N1) | `sindri-prove.sh curl` (§12) |
| A request without an item has no repo (product #1) | `request.filed` carries `repo`, and session commands are repo-scoped (§3.1) |
| The `claude -p` prompt is swallowed by `--allowedTools` (devex #1) | The prompt goes first, and the tools are one comma-separated argument (§8) |
| A unit can't build without its PublicAPI lines (devex #2) | `--slice-only` declares them in the unit's branch (§12) |
| LOW items | <ul><li>Each kind's `primaryAction` is spelled out.</li><li>The CLI exits 1 on `rebuilding` or `stalled`.</li><li>`CloseItem` voids pending decisions, and is refused while an instance is active after S2.</li><li>A refused `stop` is audited.</li><li>The gate knows which commands read views from the `ReadsViews` flag.</li><li>The CLI always sends `X-Sindri-Repo`.</li><li>P1 is split: `--slice-only` ships first as P0.</li><li>Gaps in agent-session detection (`CODEX_SANDBOX` is unset in Codex full-access mode, and `CURSOR_AGENT` is unconfirmed) are accepted under the honest-mistake model; P1 checks both against real runs and records the result in `docs/csharp/sindri.md`.</li><li>`doctor` warnings for `human:http` or `human:prover` rows on the default home go to §11.</li><li>The per-phase docs also update `.agents/rules/mcp-servers.md` (P8) and `planning/API_CONTRACT.md` (v0).</li><li>C2 changes S1's registrations, adds `sequence` to two response records, and adds the `ICommand<…>` marker to `StopProcessRequest` and `ApproveProfileRequest`, with their PublicAPI lines.</li></ul> |

### Pass 4 → final text (pass 5 left in reserve)

The pass-4 verdicts were PASS from all five lenses (product, architecture, design, devex and security), with no HIGH
findings. These MEDIUM findings are fixed in the text:

| Finding (lens) | Resolution |
|---|---|
| The engine spec (rev 4) can still build a competing caller model (product, architecture P4-3, security P4-2) | A hard gate in the wave table: the engine's P2 contract unit waits for this spec's P1 contract unit. §3.5 names rev 4 |
| A failure row for a service removed from the profile never clears (design F2) | It also clears on a `profile.approved` that no longer names the service |
| `--slice-only` can't compile its red test (devex 1) | The generator also writes the slice's own `A.cs` and `IdsJsonContext` lines (C7, §12) |
| The repo scope breaks in worktrees, and P3's comparison mixes scopes (devex 2) | The repo name comes from the profile entry containing `git rev-parse --git-common-dir`; `CreateItem` defaults to the header repo; P3 compares two human calls |
| A manual run never clears a failure row (architecture P4-1) | `RunService` appends `service.ran` with slot `manual:<jobId>` |
| `StartProcess` and `RecordHeartbeat` aren't classified (architecture P4-2) | Internal services, outside the chain (§2.1) |

These LOW findings are fixed too:
- **Before P6:** the `failure` row points at `sindri status`.
- **`service:host`** is shown in §4.2 and set only by `ServeCommand`. A command with no caller context is refused, and
  in-process writers set their own `service:<name>`.
- **`ReadLedger`** is repo-scoped for sessions.
- **C2** text is corrected: the request records gain the marker.
- **§12 step 2** marks request records as commands or queries.
- **C7** matches §12.
- **S1's `profile.approved` key** (S1 `5c9c4a4`) is noted, along with `profile-watch` restart state.
- **The projector** doesn't sleep between full pages, and commands fail fast during a rebuild.

### Pass 5 → final text (the last pass the owner allowed)

All five lenses passed, with no HIGH findings. These MEDIUM findings are fixed in the text:
- **Failure rows can't clear through `profile.approved`** (design N1, architecture N1). S1's payload doesn't carry the
  service names. `profile.checked`, which P2 owns, now carries the approved service names, and the row clears on that
  event. S1 still needs no change.
- **The actor when there's no context** (architecture N2). Events written with no context keep S1's `host:sindri`, and
  the job runner is `service:jobs`.

These LOW findings are fixed too:
- **`SND-API-010`:** "views rebuilding" now has its own code, separate from `SND-API-011` (renumbered from 004 in the epic: S1 already uses 004 and 005).
- **`park` rows:** their sources and their clearing rule now cover takeovers and desk tasks.
- **Repo resolution:** it uses an absolute common dir, real paths and the longest match against the approved profile.
- **Stream and `ReadLedger` filters:** they also drop events whose payload names another repo.
- **The manual `service.ran` slot:** it's the run's start time.
- **The `job` class text:** updated to match.
- **The C2 row:** it now names the marker on S1's request records.
- **C7:** it now says P0.
- **§12 step 2:** it no longer has the implementer add `A.cs` lines.
- **The P0 signal:** it now matches what the generator writes.
- **The §13 pass-3 row:** the stale "defaults" wording is fixed.

**Left to the engine spec's owner:** the engine spec's own wave table has to show the gate, and its §6.2 and §6.3 edit
has to land (§3.5). Engine P1 mustn't define actor classes either.

## Phases

Each phase is one PR and follows the slice recipe, red first.
- **v0 is P1–P5.** After v0 the app has its queue, needs-you, decisions and live refresh.
- P6, P8 and P10 follow.
- Every phase depends on **S1 merged**, because conflicts C1–C9 assume S1's code is on the default branch. Until S1
  merges, phases stack on `feat/sindri-s1-app-shell`.
- Every signal uses §12's prover recipe.

| # | Goal | Files (main ones) | Size | Depends on | Observable signal | Unblocks |
|---|---|---|---|---|---|---|
| **P0** | **Scaffold mode.** `--slice-only` for `new-slice.mjs` (C7) | `scripts/scaffold/new-slice.mjs`, `scripts/tests/scaffold.test.sh` | S | — (it works on any package) | `bash scripts/tests/scaffold.test.sh` passes its `--slice-only` case: only the slice's files change, plus its own lines in `PublicAPI.Unshipped.txt`, `A.cs` and `IdsJsonContext`; the red test compiles and fails | All phases |
| **P1** | **CQRS spine.** <ul><li>The anchors (C7).</li><li>`ICommand`, `IQuery`, `IClassifiedCommand`.</li><li>The callers: `CallerContext`, `ICallerContextAccessor`, the four headers (no header means `session`), the `Origin` refusal.</li><li>The chain: `CommandGate`, `AuditingService`, `AuthorizingService`, `ValidatingService`, and the `AddCommand`/`AddQuery` helpers.</li><li>`CommandAccepted`.</li><li>The catalogs: `ICommandCatalog`/`CommandCatalog`, `EventCatalog`, `ListCommands`.</li><li>`apiVersion`.</li><li>S1's commands get their classes; plus `sequence` and the raw `--json` (C5), and `EventFactory` (C6).</li><li>The CLI headers and the no-start rule for agent sessions.</li><li>`scripts/sindri-prove.sh`.</li><li>`ARCH-CQRS-001..003`.</li><li>Codes: `SND-AUTH-002`, `SND-API-007/008/009`.</li></ul> | `scripts/sindri-prove.sh`, `Core/Commands/*`, `Core/Events/EventCatalog.cs`, `Features/Commands/*`, `Features/ListCommands/`, `Host/CallerContextMiddleware.cs`, `Host/CommandCatalog.cs`, `Host/Endpoints/EndpointTable.cs`, `Host/Commands/*`, `tests/Aw.Sindri.Tests/Commands/*`, `tests/Aw.Architecture.Tests/Cqrs*`, rule records, `docs/csharp/sindri.md`, `scripts/env-scan-ignore.txt` | M | S1 + P0 | <ul><li>With `H=$($SINDRI profile validate --json \| jq -r .hash)`, the call `curl -X POST -H 'Content-Type: application/json' …/sindri/profile/approve -d "{\"hash\":\"$H\",\"confirmed\":true}"` with **no caller header** gets 403 `SND-AUTH-002`. `sqlite3 -readonly ledger.db` shows a `command.handled` row with `refused` and no `profile.approved`.</li><li>The same call as `human` but with no `X-Sindri-Terminal` also gets 403.</li><li>Adding `-H 'Origin: http://localhost:3000'` gets `SND-API-008`.</li><li>`curl …/sindri/commands` lists every action id with its class.</li><li>In an agent session, `$SINDRI stop` gets `SND-AUTH-002`.</li><li>Ctrl-C on `sindri serve` records `process.stopped`, and an immediate restart takes the lease.</li></ul> | UI, engine |
| **P2** | **Projector, read-your-writes and the head.** <ul><li>`views.db`, `IProjection`, the projector, `IProjectorStatus`, `asOf`.</li><li>Rebuild and stall handling.</li><li>`X-Sindri-After`.</li><li>`ILedgerStore.Head()` and `ILedgerReader` (C9).</li><li>`service.ran` and `profile-watch` (C8).</li><li>The slices `RebuildViews`, `ListServices`, `GetProfile`.</li><li>doctor's `views` line.</li><li>Codes: `SND-API-011`, `SND-VIEW-001`.</li></ul> | `Core/Views/*`, `Infrastructure/Views/*` (+ migrations), `Infrastructure/Ledger/*` (Head, reader), `Host/Projector.cs`, `Features/{ListServices,GetProfile,RebuildViews}/` | M | P1 | <ul><li>Delete `views.db` and restart: `GET /services` matches the earlier body, apart from `asOf`.</li><li>During `views rebuild`, a query shows `"rebuilding":true`.</li><li>`X-Sindri-After: <head+1000>` gets 503 `SND-API-011` after 2 s.</li><li>A test service that fails once and then succeeds: after the failure, `lastRun.outcome` is `failed`; after the success, it is `ok`.</li><li>Editing the live profile makes `GET /profile` show `changed` within 30 s, and reverting the edit clears it.</li></ul> | UI, engine |
| **P3** | **Items and the queue.** <ul><li>`ItemId` and the item events.</li><li>The `item_view` and `queue_view` projections.</li><li>`ItemActions` and `EmptyText`.</li><li>The slices `CreateItem`, `CloseItem`, `GetQueue`, `ListItems`, `GetItem`, `GetItemTimeline`.</li><li>The CLI verbs `queue`, `items`, `item add` and `item close`.</li></ul> | `Core/Items/*`, `Features/{CreateItem,CloseItem,GetQueue,ListItems,GetItem,GetItemTimeline}/`, projections, `Host/Commands/*` | M | P2 | <ul><li>`sindri-prove.sh human item add "Try the queue"`, then `sindri-prove.sh curl GET /sindri/queue \| jq -S 'del(.asOf)'` equals `sindri-prove.sh human queue --json \| jq -S 'del(.asOf)'` (both human, so both see every repo).</li><li>`item close` with a stale version gets `SND-ITEM-409`.</li><li>An empty queue prints its `EmptyText`.</li><li>A `POST /items` with no header gets 403.</li></ul> | **UI** |
| **P4** | **Requests, decisions and needs-you.** <ul><li>`RequestId` and `DecisionId`.</li><li>The slices `FileRequest`, `ListRequests`, `ListDecisions`, `Decide`, `GetNeedsYou`.</li><li>The needs-you `failure` and `profile` rows over P2's events.</li><li>The CLI verbs `request`, `requests`, `decide` and `needs-you`.</li><li>Codes: `SND-REQ-*`, `SND-DEC-*`.</li></ul> | `Core/{Requests,Decisions,NeedsYou}/*`, `Features/{FileRequest,ListRequests,ListDecisions,Decide,GetNeedsYou}/`, projections, `Host/Commands/*` | M | P3 | <ul><li>`curl -X POST -H 'Content-Type: application/json' -H 'X-Sindri-Repo: demo' …/requests -d '{"kind":"rescope","text":"…"}'` (no caller header, so `session`) shows up as exactly one `decision` row in `GET /needs-you`, with `recordedOnly: true`.</li><li>The same `decide` with no caller header gets 403.</li><li>`sindri-prove.sh human decide DEC-… accept` clears the row.</li><li>Two `decide` calls started together: one succeeds and the other gets `SND-ITEM-409 decided elsewhere`.</li><li>Editing the live profile adds a `profile` row within 30 s, and reverting the edit clears it.</li><li>A service that fails adds a `failure` row, and its next good run clears it.</li><li>A service that fails, is removed from the profile, and has the profile approved also clears its row.</li></ul> | **UI, engine** |
| **P5** | **Event stream and ledger read.** <ul><li>Public types from `EventCatalog`, `PublicEvent`.</li><li>The `item` and `types` filters, the session repo filter, `Last-Event-ID`.</li><li>The ping and the stream limits (`SND-API-012`).</li><li>`ILedgerSignal`, wired to the projector and the stream.</li><li>`ReadLedger` and `sindri ledger`.</li><li>`sindri status --follow` (C4).</li></ul> | `Core/Events/*`, `Infrastructure/Ledger/LedgerSignal.cs`, `Features/{StreamEvents,ReadLedger}/`, `Host/Endpoints/EventStream.cs`, `Host/Projector.cs` | S | P1, plus P2 for the projector wiring (its signal uses `service.heartbeat`; the item filter is tested once P3 lands) | <ul><li>`curl -N '…/sindri/events?after=0&types=service.*'` shows each `service.heartbeat` within 100 ms of its append.</li><li>Reconnecting with `Last-Event-ID: n` resumes at n+1.</li><li>`command.handled` never appears.</li><li>`sindri-prove.sh human ledger --json --after 0` lists it.</li></ul> | **UI, engine** (S1-D6) |
| **P6** | **Jobs and services.** <ul><li>`JobId` and the job events.</li><li>The slices `GetJob`, `CancelJob`, `RunService` (as a job), `PauseService`, `ResumeService`; the scheduler honours pause and resume.</li><li>The CLI follows progress on the stream, and Ctrl-C cancels.</li><li>Codes: `SND-JOB-404`, `SND-SVC-404`.</li></ul> | `Core/Jobs/*`, `Features/{GetJob,CancelJob,RunService,PauseService,ResumeService}/`, a scheduler hook in Host | M | P2 + P5 | <ul><li>`sindri-prove.sh human services run heartbeat` prints progress and the result.</li><li>Ctrl-C during a long fake job prints `cancelled`, and the ledger shows `job.cancelled`.</li><li>After `services pause heartbeat`, there's no heartbeat for 3 cadences.</li><li>A run with no caller header gets 403.</li><li>A manual run that succeeds clears that service's `failure` row and sets `lastRun` to `ok`.</li></ul> | UI, **engine** |
| **P8** | **The `sindri_*` MCP tools.** <ul><li>On `/sindri/mcp` (created here, or by context S-1M if it landed first).</li><li>`sindri mcp register [--apply]`.</li><li>Every call stamped `session`, scoped by `X-Sindri-Repo`.</li></ul> | `Host/Mcp/*`, the SDK in `Directory.Packages.props` (pinned, at least 14 days old), `deps.yaml` | M | P4 + P6 | A fresh `claude -p "<journey>" --mcp-config … --strict-mcp-config --allowedTools …` subagent: <ul><li>lists the queue and an item;</li><li>files a `rescope` request, which shows up in `GET /needs-you`;</li><li>finds no tool to decide with;</li><li>gets `SND-AUTH-002` from a `curl` with no caller header.</li></ul> Its transcript is the evidence | — |
| **P10a** | **`sindri open` and the launch contract** (UI E-1…E-5, E-11 and §8.1; P10b, opening is a takeover, follows engine P5 with UI E-6 and E-19). <ul><li>`ILauncher` with one tmux-owned target (`snd-<run>` running Claude Code, Codex or Cursor; a Warp tab attaches to it when a human opened the item) (§8.1 rev 8, README X55).</li><li>`sindri session wrap`, the launch secret, heartbeats, `launch.started`/`launch.failed`, `session.ended`, per-launch files, usage readers.</li><li>`OpenItem` as one event; `service:launcher` takes over and launches after commit; a second open while a session is live offers attach (UI E-2), one already in flight is `SND-LAUNCH-008` (UI E-1/E-5, README X52); `SND-LAUNCH-001…013`, `SND-LAUNCH-404`, `SND-AUTH-003`.</li><li>Codex and Cursor launch-time MCP config, and their usage readers.</li><li>Budgets (E-28): `agents.<target>.dailyBudget` and `agents.softLimit` in the profile schema, `session.usage` samples, `budget` and `budgetTotal` on `GET /launch/targets`, `SND-LAUNCH-010` (§3.8).</li></ul> | `Core/Launch/*`, `Infrastructure/Launch/{Warp,Process}Launcher.cs`, `Host/Commands/SessionWrap*`, `Features/OpenItem/` | L | P3 + P8 | <ul><li>`sindri-prove.sh human open ITM-…` opens a Warp tab running Claude Code in the item's repo; `launch.started` is recorded; the session's first `sindri_item` call returns the item.</li><li>Closing the tab writes `session.ended` (`exit`); killing wrap writes it (`lost`) within 150 s; the item can be opened again.</li><li>A second open while live offers attach (UI E-2); one while a launch is in flight exits 2 with `SND-LAUNCH-008`.</li><li>The launch file and argv contain no secret and no item text.</li><li>With `agents.claude-code.dailyBudget: 0.01` approved and a fake usage reader reporting more than that, `GET /launch/targets` shows `budget.state: "hard"`, `open ITM-…` for that target exits 2 with `SND-LAUNCH-010` and writes no `item.open-requested`, and the same call for `codex` still opens; at 80 % the call succeeds with `warnings: ["budget-soft"]` (E-28).</li></ul> | engine P8 (the agent runtime), UI U11 |
| **P11** | **Engine item states, and the instance views.** <ul><li>The item projection folds `workflow.*` events into `needs-answer`, `taken-over`, `parked` and `running`; `queued` waits for the router phase, which has no event for it yet.</li><li>Engine parks, escalations, idle takeovers and desk tasks become needs-you rows of kind `park`.</li><li>`CloseItem` refuses while an instance is active.</li><li>The instance views of §3.8: `GET /workflow/instances?root=&recipe=&subject=` and `/{address}` with `states[]` (path order, glyph, loops, desk, children progress), `facts[]`, grouped `children[]` (E-20) and the status fields (E-23), `GET …/facts/{key}`, and `GET /workflow/recipes/{id}` with `outputs` and `inputs[].kind` (E-22, the schema is engine P2's).</li></ul> | `Features/Projections/*`, `Features/{ListInstances,GetInstance,GetInstanceFact,ShowRecipe}/` view shapes, `Host/Endpoints/*` | L | P4 + engine P2 + engine P4 | <ul><li>Starting `workflow.item` on an item (its `triaged` state is a wait state) moves it to `running` in `GET /queue`; starting `workflow.step` shows it under `waitingOnYou` as `needs-answer`, because the §3.6 table maps an open desk task to `needs-answer`.</li><li>A desk task and a park appear in `GET /needs-you` with the engine's action id.</li><li>On the fixture, a three-state triage and a desk round trip (`recipes/dev/triage-three-state.yaml`, `desk-round-trip.yaml`) both answer `GET /workflow/instances/{address}` in the same shape: `states[].order` follows the recipe's transition order, the loop pair carries `loops`, a children state carries `children: {done, total}`, and `facts[]` lists every declared input and output with its `status` (E-20).</li><li>For a parked instance, `status.text` equals the line block `sindri workflow status <address>` prints, and `status.answer.cli`, `.next`, `.timeout`, `.budget` and `.commands` equal the values parsed from it (E-23).</li><li>`curl …/workflow/recipes/<id>` returns `outputs` and each input's `kind`, and `sindri recipe show <id>` prints the same keys (E-22).</li></ul> | UI's live item states, U3', U8 |
| **P12** | **Recipe stats** (E-24, E-25). <ul><li>`GetRecipeStats` and the `RecipeList` stats block and status word (§3.8).</li><li>The `recipe_stats` cache in `views.db`, invalidated by a run-ending event.</li><li>`SND-API-013`.</li></ul> | `Features/{GetRecipeStats,ListRecipes}/` view shapes, `Core/Recipes/RecipeStats*`, `Infrastructure/Views/RecipeStats*` (+ migration `V5xx`) | M | P11 + engine P2, P3 (the registry and evals) | <ul><li>On the fixture ledger, `GET /workflow/recipes/workflow.item/stats?window=30d` reports `runs`, `done`, `failed`, `cancelled`, `byState[].parks` and `gateRefusals` equal to a `sqlite3 -readonly ledger.db` count over the same events (E-24).</li><li>`humanRate` counts a run with a park answered, an escalation decided or a takeover once, however many it had.</li><li>`window=7d` changes the counts; `window=1y` gets `SND-API-013`; a child-only recipe returns `childOnly: true`.</li><li>Deleting `views.db` and restarting returns the same stats body apart from `asOf`; a new run-ending event changes the next read.</li><li>`GET /workflow/recipes` lists a recipe whose pinned block changed as `changed` and a never-approved one as `draft`, each with the default-window stats (E-25).</li></ul> | UI U10 |
| **P13** | **Engine in depth** (E-30). <ul><li>`GetEngine`, `ListEngineInstances`, `GetEngineHistory` in `Aw.Sindri.Infrastructure.Engine`, over the Temporal SDK client (§3.8).</li><li>`pending.stuck` feeds `health.word` on `GET /status`.</li><li>`SND-ENGINE-001`, `SND-ENGINE-404`.</li><li>An architecture test: no project but this one references `Temporalio`, and the app and CLI hold no Temporal address.</li></ul> | `Infrastructure/Engine/*`, `Features/{GetEngine,ListEngineInstances,GetEngineHistory}/`, `Host/Endpoints/*`, `tests/Aw.Architecture.Tests/*` | M | P11 + engine P4 (the mirror writes the Temporal event id on the ledger row) | <ul><li>With the prover's dev server running and one instance waiting on a signal, `GET /engine` shows the worker polling and `GET /engine/instances` lists the instance with `waitingOn.kind: signal`.</li><li>Stopping the worker for more than 5 minutes marks that instance `stuck: true`, `pending.stuck` becomes 1 and `GET /status` reports `degraded` (E-30).</li><li>A `history` event whose `ledgerSequence` is 4231 matches `sqlite3 -readonly ledger.db` where the row's Temporal event id equals the event's `eventId`, and `GET /ledger?after=4230` returns that row.</li><li>Stopping the dev server makes `GET /engine` answer 503 `SND-ENGINE-001`; an address that never ran answers `SND-ENGINE-404`.</li><li>`grep -r Temporalio` over the CLI, the UI and the MCP projects finds nothing.</li></ul> | UI U12 |

Rev 1's P7 and P9 are gone; the engine spec owns them (§3.5).

**What each consumer needs:**
- **The UI's v0 views** (§3.6) need P1–P5.
- **The engine** needs:
  - P1: the chain, the callers and the catalogs;
  - P2: projections and the head;
  - P4: decisions and needs-you;
  - P5: the event filter;
  - P6: jobs;
  - P10: `ILauncher`.

## Parallel delivery with an agent team

An **agent team orchestrated with `/specToProvenPR`** builds the phases. The lead session only orchestrates, and keeps
every agent working. `/specToProvenPR`'s plan for this spec follows these rules.

### Dependency graph and waves

The cross-spec order is the derived wave table in `docs/sindri/README.md` ("Build order"); the graph below orders
only this spec's phases inside it. P11 follows P4 and engine P4; P12 and P13 follow P11 (P13 also needs engine P4's mirror column); P10 also needs the UI design spec.

```
S1 ──► P1 ──┬──► P2 ──► P3 ──► P4 ──┬──► P8 ──► P10
            │     │                  │     ▲
            └──► P5 (projector wiring after P2)
                  │                  └──► P6 ┘   (P6 needs P2 + P5; P8 needs P4 + P6)
```

| Wave | Runs at once | Stacked on | Notes |
|---|---|---|---|
| W0 | **P0** | — | It's small and package-agnostic, so it can merge before S1 |
| Gate | — | — | **The engine spec's P2 contract unit waits for this spec's P1 contract unit** (the shared caller model, §3.5), unless the engine spec's §6.2 and §6.3 edit lands first. Both wave tables must show this gate |
| W1 | **P1**, then **P2** ∥ **P5** once P1's contract unit is committed | P1 on S1; P2 and P5 on P1 | P5 wires `ILedgerSignal` into the projector after P2's projector lands, by rebasing onto P2 |
| W2 | **P3** ∥ **P6** | P3 on P2; P6 on P2 + P5 | |
| W3 | **P4** | P3 | Other agents review W2 or write P8's contract unit |
| W4 | **P8**, then **P10** | P8 on P4 + P6; P10 on P8 | |
| W5 | **P11**, then **P12** ∥ **P13** | P11 on P4 + engine P2 and P4; P12 and P13 on P11 | The screens spec's E-20…E-25 and E-30 (UI U8, U10, U12) |

- Each phase is one PR, based on its parent phase's branch (stacked, §17.10).
- A phase is **ready** for review once its parent is ready, not merged.
- Merges go in graph order, and each child rebases onto its merged parent.

### Units inside a phase

As in the engine spec, each phase starts with a **contract unit**: the records, ports, catalog rows and red stubs that
everything else compiles against. The contract unit runs alone. Then one implementer per slice fans out, each slice in
a folder of its own. P0 ships `--slice-only` first, and P1's contract unit holds the anchor restructuring (C7).

### The team

| Role | Who | Does | Never does |
|---|---|---|---|
| **Lead (orchestrator)** | The session running `/specToProvenPR` on this spec | Keeps the wave board, dispatches the next ready unit the moment an implementer frees up, rebases stacked branches, runs the merge order, and relays owner questions | Write phase code |
| **Implementer** (one per ready unit) | A subagent in its own worktree (`isolation: worktree`) | Runs the slice recipe red to green, adding its own lines at the shared anchors in its branch (§12), with its own temp home through `sindri-prove.sh` | Edit another unit's slice folder, or a shared line that isn't its own |
| **Integrator** (one per phase) | A subagent started when the phase's last unit lands | Merges the unit branches, resolves the adjacent-line conflicts at the anchors, regenerates `PublicAPI.Unshipped.txt` from `RS0016`, runs the full merge gate, and opens the draft PR | — |
| **Prover** (one per phase PR) | A subagent following §12's recipe; for P8, a fresh `claude -p` with the MCP config | Runs the phase's signal in the running app and records the evidence | Treat green tests as proof |
| **Reviewers** | A fresh subagent per round (`/review`) | Review loops until there are no blocking findings and no issues. Fixes go back to implementers as new units | Fix code itself |

**Throughput:**
- At most **3 implementers** and **2 builds** at once, because the heavy-job lock has two slots.
- An agent waiting on a review round is handed the next ready unit, or the next phase's contract unit.
- A unit whose parent changes rebases at its next green point, not mid-slice.

### Shared files

**What's shared.** These files take one line or row per slice, command, event or code. Implementers add only their own
lines, at the anchor, in their own branch. When unit branches merge, the integrator resolves the adjacent-line
conflicts.
- `Features/SindriFeaturesServiceCollectionExtensions.cs`;
- `Host/Endpoints/EndpointTable.cs`;
- `Host/Commands/{CommandTable,Verbs,Usage}.cs`;
- `Host/SindriJsonContext.cs`;
- `Host/CommandCatalog.cs`;
- `Core/Events/EventCatalog.cs`;
- `Core/SindriErrors.cs`, with its `All` list and the error table in `docs/csharp/sindri.md`;
- `tests/Aw.Sindri.Testing/A.cs`;
- the AOT smoke's `expected.txt`.

**Reserved so phases never collide.**
- Error codes are reserved per phase in §9.
- `views.db` migrations take the next free number: the migration runner refuses a gap, so a phase can't reserve a range.

**`PublicAPI.Unshipped.txt`**: each unit declares its own lines through `--slice-only`. The integrator re-derives the
file from the build's `RS0016` output only when merges conflict.

Every other file belongs to exactly one unit.

### Done

A phase is done when its PR is ready; no agent ever merges one. Ready means:
- the signal has been shown in the running app, with evidence;
- the merge gate is green;
- the review loop has reached zero blocking findings and zero issues, with every suggestion answered in writing.

The lead reports each phase as it becomes ready, and stops for the owner's merge.
