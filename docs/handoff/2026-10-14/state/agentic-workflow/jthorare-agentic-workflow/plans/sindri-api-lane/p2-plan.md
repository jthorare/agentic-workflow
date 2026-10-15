# API P2 — Projector, read-your-writes and the head: implementation plan

Spec: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (API) §2.1 (waiting for the views), §2.2–§2.4,
§3.1 (`GET /services`, `GET /profile`), §3.2 (`RebuildViews`), §3.4 (`views.rebuilt`, `service.ran`, `profile.checked`),
§9 (`SND-API-010/011`, `SND-VIEW-001`), §10 C8/C9, §12, Phases P2 row; amended by the UI spec
`2026-10-11-sindri-ui-design.md` §6.7 E-14 (`health`), E-17/§18.1 (goldens), §18.2 (stall trigger) — UI wins (README X52).
Branch `feat/sindri-api-p2-projector` (worktree `.claude/worktrees/sindri-api-p2`), stacked on
`feat/sindri-api-p1-cqrs` (PR #181) at `9855605e`. "§" = API spec unless prefixed (UI §, README X).

## 0. Goal

Sindri gets a read side. A projector folds the ledger into `views.db`; every view answer carries `asOf`; a query can
ask for its own writes with `X-Sindri-After`; the ledger exposes its head (`ILedgerStore.Head()`) and a read-only port
(`ILedgerReader`). The scheduler records `service.ran` for every run and a `profile-watch` hosted service records
`profile.checked` on drift (C8). Three slices ship: `GET /sindri/services`, `GET /sindri/profile`,
`POST /sindri/views/rebuild` (`sindri views rebuild`). `GET /status` gains `views` and `health` (E-14), `doctor` gains a
`views` line, and the codes `SND-API-010`, `SND-API-011`, `SND-VIEW-001` are registered.

Phase signals (spec P2 row), each mapped to the verification plan:
1. Delete `views.db` and restart: `GET /services` matches the earlier body apart from `asOf`.
2. During `views rebuild`, a query shows `"rebuilding": true`.
3. `X-Sindri-After: <head+1000>` gets 503 `SND-API-011` after 2 s.
4. A test service that fails once then succeeds: `lastRun.outcome` `failed`, then `ok` (see Q1).
5. Editing the live profile makes `GET /profile` show `changed` within 30 s; reverting clears it.
Plus E-14 (`health` on `GET /status`) and doctor's `views` line.

---

## 1. Contract unit (runs first, committed alone)

Goal: every shared type, port, record, catalog row, error code and JSON-context entry exists; build at 0 warnings;
all P1 tests green; **exactly the three generated slice red tests are red** (ListServices, GetProfile, RebuildViews).
The engine and UI lanes code against this commit.

### 1.1 Core — ledger (C9)
```csharp
// Ledger/ILedgerStore.cs — additive member (+ PublicAPI line)
public ValueTask<Result<long>> Head(CancellationToken cancellationToken);   // the last sequence; 0 when empty or no file

// Ledger/ILedgerReader.cs — the read-only port (ReadLedger P5, the projector, queries); adapter opens ledger.db read-only
public interface ILedgerReader
{
    ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken);
    ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken);
    ValueTask<Result<long>> Head(CancellationToken cancellationToken);
}
```
`ILedgerStore` does **not** inherit `ILedgerReader` (separate adapters: the reader never migrates or creates the file).
Contract: `LedgerStoreContract` gains `Head_is_the_last_sequence_and_zero_when_empty` (passes for `InMemorySindriStore`
and `SqliteLedger`: Head implemented in both in the contract unit — one SQL line, `SELECT COALESCE(MAX(seq),0)`).
New `LedgerReaderContract` base (`A_reader_sees_what_the_store_wrote`, `A_reader_on_a_missing_ledger_is_empty_and_creates_nothing`);
`InMemorySindriStore` implements `ILedgerReader`; the SQLite reader binding comes in unit A.

### 1.2 Core — read-only approvals
`Profile/IApprovalReader.cs`: `ValueTask<Result<LoadedProfile?>> Latest(CancellationToken)`; `IApprovals : IApprovalReader`
keeps `Save` (Latest moves to the base; PublicAPI lines move). Registered as
`AddSingleton<IApprovalReader>(sp => sp.GetRequiredService<IApprovals>())`.

### 1.3 Core — views (`Aw.Sindri.Core.Views`)
```csharp
public sealed record AsOf(long Cursor, long LedgerHead, DateTimeOffset? ProjectedAt, bool Rebuilding, bool Stalled);  // §2.2, JSON camelCase
public sealed record ViewsStatus(long Cursor, long LedgerHead, DateTimeOffset? ProjectedAt, bool Rebuilding, bool Stalled,
    long? StalledAt, string? StalledType)                    // GET /status `views`; doctor; health
{ public AsOf AsOf => new(Cursor, LedgerHead, ProjectedAt, Rebuilding, Stalled); }
public sealed record ProjectorState(long Cursor, int SchemaVersion, DateTimeOffset? ProjectedAt, bool Rebuilding,
    long? StalledAt, string? StalledType, bool Fresh);       // the projector_state row; Fresh: views.db was just created
public interface IProjection                                 // §2.3: a fold over ledger events into view rows
{
    IReadOnlyList<string> Views { get; }                     // the view tables it writes (a rebuild empties them)
    bool Handles(string eventType);
    ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken);
}                                                            // an Error (or a throw) stalls the projector at that event (SND-VIEW-001)
public interface IViewRows                                   // the open page transaction (one views.db transaction per page)
{
    ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken ct) where TRow : class;
    ValueTask<Result<Unit>> Put<TRow>(string view, string key, TRow row, CancellationToken ct) where TRow : class;
    ValueTask<Result<Unit>> Delete(string view, string key, CancellationToken ct);
}
public interface IViewReader                                 // queries' read port (read-only connection)
{
    ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken ct) where TRow : class;
    ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken ct) where TRow : class;  // ordered by key
    ValueTask<Result<ProjectorState?>> State(CancellationToken ct);   // null when views.db doesn't exist (doctor); never creates it
}
public interface IViewStore                                  // the projector's writer (a "projection writer", ARCH-CQRS-002)
{
    ValueTask<Result<ProjectorState>> Open(int schemaVersion, CancellationToken ct);   // migrate/create views.db (0600); Fresh when created
    ValueTask<Result<PageApplied>> Apply(IReadOnlyList<LedgerEvent> page, IReadOnlyList<IProjection> projections,
        DateTimeOffset at, CancellationToken ct);            // one transaction; savepoint per event; stops at a failing event,
                                                             // commits the events before it and records the stall
    ValueTask<Result<ProjectorState>> Reset(IReadOnlyList<string> views, int schemaVersion, DateTimeOffset at, CancellationToken ct); // empty views, cursor 0, rebuilding
    ValueTask<Result<Unit>> FinishRebuild(CancellationToken ct);
}
public sealed record PageApplied(ProjectorState State, ViewStall? Stall);
public sealed record ViewStall(long Sequence, string Type, string Code, string Detail);
public interface IProjectorStatus                            // §2.2: queries read the head from here, not the ledger
{
    ViewsStatus Current { get; }
    ValueTask<Result<Unit>> WaitFor(long sequence, TimeSpan limit, CancellationToken ct);   // SND-API-011 (cursor, target) past the limit,
}                                                                                           // at once while stalled
public interface IProjectorControl { void RequestRebuild(); }   // RebuildViews; sets Rebuilding at once
public static class ViewNames { public const string Services = "services"; public const string Profile = "profile"; }
public static class ViewSchema { public const int Version = 1; }   // bump when a projection's fold changes → rebuild
public sealed record ServiceRow(ServiceName Name, LastRun? LastRun, bool Paused, long Sequence);
public sealed record LastRun(DateTimeOffset Slot, ServiceOutcome Outcome, string? Code, DateTimeOffset RanAt);
public sealed record ProfileRow(ProfileHash? Approved, DateTimeOffset? ApprovedAt, ProfileHash? Live, DateTimeOffset? CheckedAt,
    bool Changed, IReadOnlyList<string> ChangedPaths, IReadOnlyList<ServiceName> ApprovedServices, long Sequence);
public static class EmptyText                                // §3.6, the P2 part; P3 adds the item lists
{ string Services(); string Rebuilding(long cursor, long head); string Stalled(long at); string? For(AsOf asOf, bool empty, Func<string> emptySentence); }
```
`ProjectorProgress : IProjectorStatus, IProjectorControl` — the real in-memory implementation in Core (internal sealed,
`TimeProvider`-based waits, a `TaskCompletionSource` pulse), registered in `AddSindriCore()`. Contract base
`ProjectorStatusContract` (`WaitFor_returns_once_the_cursor_reaches_the_target`, `WaitFor_times_out_with_SND_API_011_naming_cursor_and_target`,
`WaitFor_fails_at_once_while_stalled`, `RequestRebuild_shows_rebuilding_at_once`) bound to `ProjectorProgress` in the contract unit
(stubs throw; unit B makes it green — so these are red too: allowed, listed in the commit message).

Contract-unit projections are **not** written (unit C): only the row records, `ViewNames`, `ViewSchema`.

### 1.4 Core — events (C8) and errors
- `EventTypes`: `ServiceRan = "service.ran"`, `ProfileChecked = "profile.checked"`, `ViewsRebuilt = "views.rebuilt"`.
- `Events/ServiceOutcome.cs` `{ Ok, Failed }` (kebab JSON); `Events/ServiceRan.cs`
  `(CommandId CommandId, ServiceName Service, DateTimeOffset Slot, ServiceOutcome Outcome, string? Code)`;
  `Events/ProfileChecked.cs` `(CommandId CommandId, ProfileHash? Approved, ProfileHash? Live, bool Drifted,
  IReadOnlyList<string> ChangedPaths, IReadOnlyList<ServiceName> ApprovedServices)`;
  `Events/ViewsRebuilt.cs` `(CommandId CommandId, long CursorBefore, long EventsReplayed)`.
- `EventCatalog` rows: `service.ran` public `service.ran:<name>:<epoch>:<slot>`; `profile.checked` public
  `profile.checked:<previousCheckSequence>`; `views.rebuilt` not public `views.rebuilt:<commandId>`.
- `SindriErrors`: `ViewsRebuilding` `SND-API-010` Unavailable "Views are rebuilding; the command wasn't run, retry";
  `ViewsBehind` `SND-API-011` Unavailable "The views didn't reach the sequence in time"; `ViewsStalled` `SND-VIEW-001`
  Unavailable "The projector stalled on an event"; added to `All`.
- Health (E-14): `Health/HealthWord.cs` `{ Healthy, Degraded, Stalled, Rebuilding }`; `Health/HealthProblem.cs (string Code, string Text)`;
  `Health/Health.cs (HealthWord Word, IReadOnlyList<HealthProblem> Problems)`; `Health/HealthSummary.cs`
  `public static Health For(bool running, ViewsStatus views, IReadOnlyList<ServiceRow> services, bool profileNeedsApproval, bool profileChanged)`
  (contract body throws `NotSupportedException`; unit E's red test).

### 1.5 Features
- `node scripts/scaffold/new-slice.mjs --slice-only --package Sindri --surface http` for:
  `ListServices` (`--behavior lists_each_approved_service_with_its_last_run --route "GET /services"`),
  `GetProfile` (`--behavior shows_approved_and_live_hash_and_the_changed_paths --route "GET /profile"`),
  `RebuildViews` (`--behavior appends_views_rebuilt_and_asks_the_projector_to_rebuild --route "POST /views/rebuild"`).
  Shape: `ListServicesRequest() : IQuery<ListServicesResponse>`, `ListServicesResponse(IReadOnlyList<ServiceView> Services, string? Empty, AsOf AsOf)`,
  `ServiceView(ServiceName Name, string Every, bool Paused, LastRun? LastRun, DateTimeOffset? NextSlot)`;
  `GetProfileRequest() : IQuery<GetProfileResponse>`, `GetProfileResponse(ProfileHash? Approved, DateTimeOffset? ApprovedAt,
  ProfileHash? Live, DateTimeOffset? CheckedAt, bool Changed, IReadOnlyList<string> ChangedPaths, AsOf AsOf)`;
  `RebuildViewsRequest() : ICommand<CommandAccepted>` (response `CommandAccepted`). (Spec names `ServiceList`/`ProfileView`;
  generator names win, same shape — P1 G20.)
- Internal (no route, like `RecordHeartbeat`): `RecordServiceRun/{RecordServiceRunRequest(ServiceName, ScheduledRun, Result<Unit>), …Response(long Sequence), …Service}`
  and `CheckProfile/{CheckProfileRequest(LeaseGrant), CheckProfileResponse(long? Sequence), CheckProfileService}`; contract: records + stubs.
- `GetStatusResponse`: `+ ViewsStatus? Views { get; init; }`, `+ Health? Health { get; init; }` (init props like `ApiVersion`;
  filled by unit E — null in the contract unit, goldens re-recorded in unit F).
- Registration lines at the anchors: `AddQuery` ListServices, GetProfile; `AddCommand` RebuildViews (+ `AcceptAllValidator`).

### 1.6 Host
- `SindriHeaders.After = "X-Sindri-After"`; `Endpoints/QueryRouteMetadata.cs` (singleton marker on the `queries` group, like
  `CommandRouteMetadata`).
- Endpoint lines: `_ = queries.MapGetProfile(); _ = queries.MapListServices(); _ = commands.MapRebuildViews();`.
- `SindriJsonContext`: every new record/enum above (rows, events, AsOf, ViewsStatus, Health, slices).
- Catalog: `RebuildViews` moves from `PendingRows` to `ShellRows` as `ready` (desk, `typeof(RebuildViewsRequest)`,
  `POST /sindri/views/rebuild`, `sindri views rebuild`, `ReadsViews: false`, `Destructive: false`, `SelfAudited: false`);
  `CatalogRules.ShippedPhases` += `"P2"` (no pending row names P2 after the flip).
- `ARCH-CQRS-002`: `WritePorts += typeof(IViewStore)`; `IApprovals` stays forbidden, `IApprovalReader` allowed.

### 1.7 Testing support
`InMemoryViewStore : IViewStore, IViewReader` (rows as JSON text through the injected `JsonSerializerOptions`, so it
round-trips exactly like SQLite) + `ViewStoreContract` (`Apply_folds_each_event_through_the_projections_that_handle_it`,
`A_failing_event_stops_the_page_there_and_keeps_the_events_before_it`, `Reset_empties_the_views_and_the_cursor`,
`Open_on_a_new_file_is_fresh`, `Rows_round_trip_through_json`); `A.cs` builders from the generator (ListServicesRequestBuilder etc.).

### 1.8 Commit sequence (contract unit)
`feat: ILedgerStore.Head, ILedgerReader and IApprovalReader (C9)` · `feat: views ports, AsOf, rows and the projector status contract` ·
`feat: service.ran, profile.checked and views.rebuilt events; SND-API-010/011 and SND-VIEW-001` ·
`test: red ListServices, GetProfile and RebuildViews slices` · `feat: RebuildViews catalog row is ready; P2 is shipped`.

---

## 2. Units after the contract (sequential in this worktree; B and C may run as two worktrees off the contract commit)

### Unit A — the ledger read side (C9)
`Infrastructure/Ledger/SqliteLedgerReader.cs` (read-only connection via `SqliteConnectionFactory.OpenReadOnly`; missing
file → empty / 0, never creates), `SqliteLedger.Head`, registrations (`ILedgerReader`). `GetStatusService` →
`IApprovalReader` + `ILedgerReader`; `StreamEventsService` → `ILedgerReader`. **Delete the G1 allowlist** (QueryPortRules,
the stale-entry test turns into "the allowlist is empty and gone"). Tests: `SqliteLedgerReaderContractTests`,
`SqliteLedgerStoreContractTests` gains Head; `Query_handlers_take_no_write_ports` passes with no allowlist.

### Unit B — views.db, the projector, rebuild and stall (§2.3)
- `Infrastructure/Views/ViewsDatabase.cs` (like `LedgerDatabase`: `views.db`, 0600, WAL, `Views/Migrations/V001_views.sql`:
  `projector_state(id=1, cursor, schema_version, projected_at, rebuilding, stalled_at, stalled_type)`,
  `view_services(key TEXT PRIMARY KEY, row_json TEXT NOT NULL, seq INTEGER NOT NULL)`, `view_profile(…same…)`).
  `SindriSettings.ViewsPath`. `SqliteViewStore : IViewStore`, `SqliteViewReader : IViewReader` (read-only), table name =
  `view_<name>` from a fixed allow-set (`ViewNames`), never interpolated from input.
- `Core ProjectorProgress` real (red contract → green).
- `Host/Projector.cs` (singleton, internal): `CatchUp(ct)` — under a `SemaphoreSlim(1)`: open (rebuild when `Fresh` or
  `SchemaVersion != ViewSchema.Version`, or a rebuild was requested), read `Head()` from `ILedgerReader`, publish, then
  `ReadAfter(cursor, 100)` → `IViewStore.Apply` page by page until caught up; on a stall publish `stalled`, log
  `SND-VIEW-001 <seq> <type>`; a stalled projector retries every 30 s. Runs inside `EnterService("projector")`.
  Rebuild: `Reset`, replay, `FinishRebuild`; queries answer meanwhile with `rebuilding: true`.
  `Host/Server/ProjectorHost.cs` (`BackgroundService`, `AddSindriServe`): loop `CatchUp`; when caught up sleep 1 s on
  `TimeProvider` (P5 adds `ILedgerSignal`), stalled sleep 30 s. Telemetry: gauge `sindri.projector.lag` (head − cursor).
- Tests (unit, in-memory): `Projector_folds_pages_of_100_until_it_reaches_the_head`, `A_throwing_projection_stalls_at_that_sequence_and_logs_SND_VIEW_001`,
  `A_stalled_projector_retries_every_30_seconds`, `A_rebuild_resets_the_cursor_and_replays`, `A_schema_version_change_rebuilds`,
  `Rebuild_clears_a_stall`. Integration (real SQLite, temp home): `SqliteViewStoreContractTests`,
  `Deleting_views_db_and_restarting_rebuilds_the_same_views` (bodies equal apart from `asOf`), `Views_db_is_0600`.

### Unit C — C8 events and the two projections
- `RecordServiceRunService` (internal): appends `service.ran` keyed `service.ran:<name>:<epoch>:<slot:O>` under the
  service's caller context (actor `service:<name>`, `CommandId` from the context). `SchedulerHost.RunOne` calls it after
  every run: ok, failed (`result.Error.Code`), a throw (`SND-SCHED-001`), and an **unknown service** (failed `SND-SCHED-001`, G8).
  `SchedulerHost` resolves it from the run's scope (no new constructor dependency; stays at 6).
- `CheckProfileService` (internal) + Core `ProfileCheck.Decide(ProfileChecked? previous, LoadedProfile? approved, Result<LoadedProfile> live)`
  → `ProfileChecked?` (null when the (approved, live) pair is unchanged); key `profile.checked:<previous seq or 0>`.
  `Host/Server/ProfileWatch.cs` (`BackgroundService`, `AddSindriServe`): when the lease is held, under
  `EnterService("profile-watch")`, check at start then every 30 s (`TimeProvider`); on restart the previous pair comes
  from `ILedgerReader.Latest(profile.checked)`.
- `Core/Views/ServicesProjection.cs` (folds `service.ran` → `ServiceRow` by name; pure static `Fold(ServiceRow?, ServiceRan, LedgerEvent)`)
  and `Core/Views/ProfileProjection.cs` (folds `profile.approved` → approved hash/at, `changed` recomputed against the
  last live hash; `profile.checked` → live, changed, paths, approved services). Registered as `IProjection` singletons.
- Tests: `Every_run_appends_service_ran_ok_or_failed_with_its_code`, `A_throwing_service_is_recorded_failed_SND_SCHED_001`,
  `An_unknown_service_is_recorded_failed`, `Service_ran_carries_the_service_actor`, `Profile_check_appends_only_when_the_pair_changes`,
  `A_revert_writes_drifted_false`, `Drifting_back_to_an_old_hash_writes_again`, `Profile_watch_resumes_from_the_last_check_after_a_restart`,
  `Services_fold_keeps_the_last_run`, `A_failure_then_a_success_reads_ok` (signal 4), `Profile_fold_*` (approve, check, revert),
  `Replaying_the_same_events_gives_the_same_rows`. Integration: `A_test_service_that_fails_once_then_succeeds_shows_failed_then_ok`
  (a fake `IScheduledService` registered through `LoopbackBinding`'s configure seam; `FakeTimeProvider`).

### Unit D — the slices and read-your-writes
- `ListServicesService(IApprovalReader, IViewReader, IProjectorStatus)`: rows from the approved profile's services
  (name, cadence `Every`), joined with the `services` view (`LastRun`, `Paused` false until P6), `NextSlot = lastRun.Slot + cadence`
  (null before the first run, G5); `Empty` per `EmptyText` (null when there are rows and the views are fresh); `AsOf` from
  `IProjectorStatus.Current`.
- `GetProfileService(IViewReader, IProjectorStatus)`: the `profile` row (nulls / `changed: false` before any event).
- `RebuildViewsService(IProjectorStatus, IProjectorControl, ILedgerStore, HeldLease, EventFactory)`: appends `views.rebuilt
  {commandId, cursorBefore: status.Cursor, eventsReplayed: Head()}`, then `RequestRebuild()`; returns `CommandAccepted(commandId, seq)`.
  No lease → `SND-LEASE-002`. Never waits for the head.
- `Host/ReadYourWrites.cs` middleware (`UseReadYourWrites()`, after `UseCallerContext()`): on an endpoint with
  `QueryRouteMetadata` and an `X-Sindri-After` header: malformed → 400 `SND-CLI-003`; else `IProjectorStatus.WaitFor(after, 2 s)`;
  on error the 503 ProblemDetails (`SND-API-011 views at cursor N, waiting for M`; while stalled at once, naming the stall). `/ping` and
  `/events` included (every query, §2.4).
- `Features/Commands/AwaitingViews<TReq,TRes>` decorator, innermost in `AddCommand` (gate → audit → authorize → validate →
  await-views → service): no-op unless the classified row has `ReadsViews`; else stalled → `SND-VIEW-001`, rebuilding →
  `SND-API-010`, else `WaitFor(ILedgerStore.Head(), 2 s)` → `SND-API-011`. Proven with a fixture command whose
  `InMemoryCommandCatalog` row sets `ReadsViews` (no P2 route reads views; G7). Chain-order test updated.
- CLI: `sindri views rebuild` (CommandTable arm, `Verbs.RebuildViews` POST, Usage line); human prints
  `views rebuild requested (sequence N)`, exit 0; an agent session prints the server's `SND-AUTH-002 …` + the signal (exit 2);
  starts the process unless `--no-start` (human command, §7).
- Folded follow-ups from #181 (they touch the chain): **csharp-1** the gate test holds at the handler (the chain is
  async), **csharp-2** an accepted `service:host` attempt test, **LEDGER-002 tidy** (one `SND-LEDGER-002` prefix; no
  "restart sindri" for non-accepted outcomes; a `Lost`-style log message).
- Tests: the three red slice tests go green; `Lists_services_from_the_approved_profile_with_their_last_run`, `Next_slot_is_last_slot_plus_cadence`,
  `Empty_text_when_no_services`, `Rebuilding_and_stalled_answers_carry_their_sentence`, `Profile_shows_changed_and_paths`,
  `Rebuild_is_desk_and_refused_for_a_session`, `Rebuild_runs_while_stalled`, `Rebuild_doesnt_wait_for_the_head`,
  `After_a_command_a_query_with_its_sequence_sees_the_write`, `After_past_the_head_is_503_SND_API_011_after_2_seconds`,
  `Malformed_after_is_400`, `A_reads_views_command_fails_at_once_while_stalled_SND_VIEW_001`, `…while_rebuilding_SND_API_010`,
  `Cli_views_rebuild_posts_as_human`.

### Unit E — status, health (E-14), doctor
- `GetStatusService(HeldLease, IApprovalReader, IProfileSource, ILedgerReader, EventFactory, IProjectorStatus, IViewReader)` = 7 deps
  (AW1008 suggestion only; if review objects, group the two view ports into a `ViewsSnapshot` helper): `Views = status.Current`,
  `Health = HealthSummary.For(running, views, services rows, needsApproval, changedSinceApproval)`.
- `HealthSummary.For` (Core, pure): precedence stalled > rebuilding > degraded > healthy; problems in fixed order:
  views (`SND-VIEW-001` with `EmptyText.Stalled(at)`, or `SND-API-010` with `EmptyText.Rebuilding(…)`), each failed service
  (`<code>`, "Service <name> failed at <slot>"), profile (`SND-PROFILE-005` "Not approved: run `sindri profile approve`" /
  `SND-PROFILE-003` "The live profile changed since approval: run `sindri profile approve`"). Not running → `degraded`
  with `SND-API-001` (only reachable in-process; G10).
- CLI `sindri status` text gains `views: cursor N of M` / `views: rebuilding (cursor N of M)` / `views: stalled at N: run sindri views rebuild`
  and `health: <word>` + one line per problem; exit 1 when `health.word` isn't `healthy` (G11).
- `DoctorRules.Views(Result<ProjectorState?>, Result<long> head)` → `views` line after `ledger`: ok `cursor N of head M`;
  warn `no views yet` (fix `sindri`); warn `rebuilding (cursor N of M)`; fail `stalled at N (<type>)` fix `sindri views rebuild`;
  fail on a read error. `RunDoctorService` + `IViewReader` (7 deps); head from `ILedgerStore.Head()`.
- Tests: `Health_word_follows_the_precedence` (Theory), `Health_lists_problems_in_order`, `Status_reports_views_and_health`,
  `Doctor_views_line_*` (each state, exact text), `Status_text_prints_the_views_line_and_exits_1_when_not_healthy`.

### Unit F — goldens, docs, smoke, gate
- Goldens (below), `docs/csharp/sindri.md` (routes, verbs, `views.db` in Files, events, codes, `X-Sindri-After`, `asOf`,
  `health`, doctor line, goldens list), `AGENTS.md` (`views rebuild` in the C# sindri command line), `deps.yaml`
  (`sindri-app/views.db` in Sindri's `db`; note "API P2: projector, views"), `.env.example` unchanged (no new variables).
- AOT smoke: `views rebuild` (human + session refusal), the doctor `views` line, `status` `views`/`health` lines;
  re-record `expected.txt` from a real run.
- Follow-up **arch-0 run 3**: move the human-token removal from `ServeCommand` into `StopProcessService` next to
  `endpoints.Remove`, keeping the compare-and-delete `Remove(mine)` (already landed); red test first.
- Follow-up **with_tty `set +e` scope** in `scripts/lib/with-tty.sh` for a failing command under errexit (bash test first).
- Full merge gate.

---

## 3. Gaps between spec and code (G-numbers) and decisions

| # | Gap | Decision (default unless listed under Questions) |
|---|---|---|
| G1 | The "test service that fails once then succeeds" (signal 4) doesn't exist in the real binary, and UI §18.2 names "a test service that fails" without defining it | **Question Q1.** Workaround: the integration test proves it with a fake `IScheduledService` through the `LoopbackBinding` seam; the hand proof shows the failed half with an unknown service in the profile (G8) and cross-checks the ok half against the test |
| G2 | "Each view is a table keyed by its id" vs projections that are pure `Core` folds | One table per view, `view_<name>(key, row_json, seq)`; rows are Core records serialized through the injected `JsonSerializerOptions` (`SindriJsonContext`), so `IProjection` lives in Core and any lane can add one. Migrations V0xx (P2) |
| G3 | §2.3 writes the fold as `Apply(row?, event) → row?`; one projection may write several views | `IProjection.Apply(LedgerEvent, IViewRows, ct)`; each projection's pure `Fold(row?, payload) → row?` is a static method unit-tested from event lists |
| G4 | `views.rebuilt` carries "events replayed", but a handler does no slow work | `EventsReplayed` = the ledger head at the request (what the replay will cover); `CursorBefore` = the projector's cursor. Automatic rebuilds (missing `views.db`, schema change) log only, no event |
| G5 | `GET /services` needs cadence and next slot; `profile.checked` carries names only and next slot is scheduler memory | Cadence from the approved snapshot through `IApprovalReader` (read-only); `NextSlot = lastRun.Slot + cadence`, null before the first run; `Paused` false until P6; services leave the list when an approval drops them |
| G6 | `X-Sindri-After` placement and malformed values | Host middleware on query routes (`QueryRouteMetadata`); malformed → 400 `SND-CLI-003`; while stalled and behind → 503 `SND-API-011` at once (the detail names the stall) |
| G7 | `SND-API-010` and `SND-VIEW-001` belong to commands that read views; no P2 route reads views | P2 lands the `AwaitingViews` decorator (innermost, runtime no-op unless the row has `ReadsViews`) and proves it with a fixture command; P3's `CloseItem` is its first real user. `SND-VIEW-001` also names the stall in logs, status, health and doctor |
| G8 | "`service.ran` for every run": is an unknown service (approved profile names one this build lacks) a run? | Yes: its slot is recorded `failed`, `SND-SCHED-001` (the log line stays). It gives the human a visible failure for a misconfigured profile |
| G9 | `drifted` when there is no approval or the live file is unreadable | `drifted = approved != null && live != approved`; an unreadable live file is `live: null` (drifted when approved); paths = `ProfileDiff.Between` when both load, else `[]` |
| G10 | E-14 lists the words but not the inputs or problem codes | Inputs: views, failed services (last run), profile (needs approval → `SND-PROFILE-005`; changed → `SND-PROFILE-003`). Precedence stalled > rebuilding > degraded > healthy |
| G11 | `status` exit code with `health` | Exit 1 (attention) when `health.word` isn't `healthy`, beside S1's not-running and needs-approval cases |
| G12 | The projector's wake-up before P5's `ILedgerSignal` | 1 s poll on `TimeProvider`; tests and goldens drive `Projector.CatchUp` directly (`FakeTimeProvider` never sleeps the test) |
| G13 | `EmptyText` is listed under P3 but P2's list view returns `empty` | P2 adds `EmptyText` with the services, rebuilding and stalled sentences; P3 adds the item and queue sentences |
| G14 | `GET /status` services `lastRun` | Unchanged (S1, through `ILedgerReader`); `health` reads the `services` view for failures |
| G15 | Doctor reads views state while the process may be stopped | `IViewReader.State` reads `projector_state` read-only; a missing `views.db` is `warn no views yet`; the line sits after `ledger` |
| G16 | "Every domain payload carries `commandId` (or the `service:<name>` run id)" | `ServiceRan`/`ProfileChecked` carry the `CommandId` of the service's caller context (`EnterService` mints one per run) |
| G17 | A second rebuild request while rebuilding | Accepted; the projector restarts the replay |
| G18 | `ILedgerReader` on a missing ledger | Empty / head 0; never creates the file (doctor and queries before the first start) |

## 4. Questions for the lead
- **Q1 (G1): the failing test service.** Options: (a) a built-in `canary` service, registered only when
  `AW_SINDRI_TEST_SERVICES=1` (a new `.env.example` line), failing while `<home>/canary.fail` exists — a production seam
  UI §18.2 avoids elsewhere; (b) prove signal 4 only in the integration test and by the unknown-service failure in the
  hand test (the current workaround); (c) defer the hand half to U4/P6's `services run`. I proceed with (b) and leave
  the seam out unless you choose (a).

## 5. Not in this PR
- #181 follow-ups not touched by P2: **csharp-3** (fake-git cancellation test for `GitRepoLocator`), **csharp-4 pt2**
  (single argv read in `PosixProcessAncestry`), **spec-docs-1** (E-12 wrong-variant refusal; engine lane G11, check at P5).
- `ILedgerSignal` and the projector's millisecond wake-up (P5); `GET /events/catalog` and `invalidates` (E-15, P5).
- Needs-you `failure` and `profile` rows over `service.ran`/`profile.checked` (P4); `service.paused`/`resumed`, `Paused` (P6).
- CLI read verbs for services and profile (`sindri services`, `sindri profile show`): no spec row; the app reads the routes.

## 6. Goldens P2 publishes (`tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json`)
| Route | Scenarios |
|---|---|
| `GET /services` | `unapproved` (empty + sentence), `approved` (heartbeat, no run yet), `ran-ok`, `ran-failed`, `rebuilding`, `stalled`, `after-not-reached` (503 `SND-API-011`), `after-malformed` (400) |
| `GET /profile` | `unapproved`, `approved`, `changed` (paths), `stalled` |
| `POST /views/rebuild` (`views-rebuild`) | `accepted`, `accepted-while-stalled`, `refused-session` (403 `SND-AUTH-002`), `refused-origin` (403 `SND-API-008`) |
| `GET /status` | re-recorded `running-unapproved`, `running-approved` (+ `views`, `health`); new `views-stalled`, `views-rebuilding`, `service-failed` (`degraded`) |
Status/doctor text lines are asserted exactly in unit tests and in `src/Sindri/aot-smoke/expected.txt`. Every scenario
runs on `FakeTimeProvider(KnownInstants.Epoch)`, `SequentialIdGenerator`, the golden pid, `Projector.CatchUp` before the
request; the stall comes from appending a `service.ran` with an unparseable payload (UI §18.2's trigger, no seam).

## 7. Task list (red first; each behaviour = red commit, then fix)
1. Contract unit (§1) — commits listed in §1.8. Gate: `dotnet build tests/Aw.Sindri.Tests -warnaserror`; unit lane shows only the three slice reds + `ProjectorStatusContract` binding.
2. A: red `SqliteLedgerReaderContractTests` → reader; red Head in SQLite contract binding → Head; status/stream move to read ports; delete G1 allowlist.
3. B: red `ProjectorProgress` contract → real; red `SqliteViewStoreContractTests` → views.db; red projector unit tests → `Projector`; red rebuild integration → `ProjectorHost`.
4. C: red folds → projections; red scheduler `service.ran` tests → `RecordServiceRun`; red profile-check tests → `CheckProfile` + `ProfileWatch`.
5. D: slices green (ListServices, GetProfile, RebuildViews + catalog + CLI); red read-your-writes → middleware; red `AwaitingViews` → decorator; follow-ups csharp-1, csharp-2, LEDGER-002.
6. E: red `HealthSummary` → health; red status views/health → GetStatus; red doctor views → DoctorRules + RunDoctor; status text/exit.
7. F: goldens; token-removal move (red → fix); with_tty (red → fix); docs, AGENTS.md, deps.yaml; AOT smoke re-record; full gate.

## 8. Risks
- **Lane budgets:** projector and view-store integration tests use one temp home per class; goldens share one host per
  class (P1 pattern). Check `node scripts/ci/lane-budgets.mjs` after the Release build.
- **AOT:** row and payload (de)serialization only through `AwJson` with `SindriJsonContext`; generic `IViewRows.Get<TRow>`
  resolves `JsonTypeInfo` from the options (no reflection fallback). The AOT smoke catches IL2xxx/IL3xxx.
- **SQLite:** `views.db` is a second WAL file; the projector is its only writer; queries open read-only. Savepoints per event.
- **Analyzers:** AW1007/1008 on `GetStatusService`/`RunDoctorService` (7 = suggestion); CA1502/MA0051 on the projector
  loop (split `CatchUp`/`RebuildIfNeeded`/`ApplyPage`); AW1005 ≤ 300 lines.
- **Hook contract: unchanged.** No hook reads `views.db`, `status` JSON or doctor text from the C# build.

## Deviations (contract unit, 2026-10-12; rebased onto main 1027a13f after #181 merged)
- Contract unit commits: c01ae47a, 91c6c32a, de9cccdf, 9b9c2f98, 62bfa0e9 (tip 62bfa0e9).
- `Health` record is `HealthReport` (MA0049: type named like its namespace `Aw.Sindri.Core.Health`); JSON field still `health`.
- Events/errors commit precedes the views commit (`LastRun` needs `ServiceOutcome`).
- `ProjectorProgress` has no ctor params yet; unit B adds `TimeProvider` and the waits. It has `Publish(ViewsStatus)`; registered once for `IProjectorStatus` and `IProjectorControl`.
- `EmptyText` is real: services sentence "No services are approved: run `sindri profile approve`."; `For()` = stalled > rebuilding > empty; stall reported at cursor + 1 (AsOf carries no stall seq).
- Extras: `ViewNames.All`; test helpers `FixtureProjection`, `ProjectorSubject`, `InMemoryViewRows`; `LedgerReaderContract` asks bindings for `Reader()` and `LedgerExists()`.
- Status goldens carry `"views": null, "health": null` until unit E fills them; `commands/catalog.json` shows RebuildViews `ready`.

## Deviations (units A and B, 2026-10-12)
- A: commits 9421f886..20e48974. `SqliteLedgerReader(LedgerDatabase)` uses only `Schema()` + `Sessions.BeginRead` (read-only sessions), never `Writable`; `SqliteLedger` delegates its three reads to it. The stale-allowlist test became `The_query_port_allowlist_is_empty_and_gone`; `QueryPortRules.Check` lost its allowlist parameter. `ReadFailingLedger` also implements `ILedgerReader`.
- A/B: the contract unit's CS8619 in `InMemoryViewRows` was fixed in 9421f886 and re-fixed for IDE0001 in c1a95108 (a `Found<TRow>(TRow?)` helper; `Results.Success<TRow?>` trips IDE0001).
- B: commits 32c7235f..35569c9e. New public Core port `IProjectorFeed { Publish(ViewsStatus); bool TakeRebuildRequest(); }` (ProjectorProgress implements it; registered from the same singleton), because the Host's Projector can't see Core internals. `ProjectorProgress(TimeProvider)`; a pending rebuild request keeps `Rebuilding` true across publishes until the projector takes it.
- B: migration file is `Views/Migrations/0001_views.sql` (the ledger's numbering), not `V001_views.sql`; views.db migrations take no backup (derived data). `ViewTable` record holds each view's statements; table names only from `ViewSql`'s fixed map.
- B: views.db store/read failures map to `SND-VIEW-001` with a `views.db: <AW-DATA code>` message (no separate views-unavailable code); a throwing fold is `SND-VIEW-001 <ExceptionType>: <message>` as the stall's code. `InMemoryViewStore` catches throws too (SuppressMessage CA1031 with justification), and `ViewStoreContract` gained `A_throwing_projection_stalls_like_a_failing_one`.
- B: the 30 s stall retry lives in `Projector` (`StallRetry`; a pass while stalled is a no-op until 30 s after the stall, a rebuild request bypasses it); `ProjectorHost` always ticks 1 s. A store/ledger failure is `ProjectorPass.Failed`, logged once per distinct message, retried next tick. The rebuild log names its reason (`requested`, `views.db created`, `schema version changed`); no `views.rebuilt` event for automatic rebuilds (G4).
- B: `Projector` is registered in `AddSindriHost` (so tests/goldens can drive `CatchUp`), `ProjectorHost` in `AddSindriServe`. Projector has 7 ctor deps (ILedgerReader, IViewStore, IEnumerable<IProjection>, IProjectorFeed, ICallerContextAccessor, TimeProvider, ILogger) and is IDisposable (its gate). `ProjectorHost` takes an ILogger too.
- B: the delete-and-restart test compares the `services` rows, not `GET /services` bodies (no projection or slice yet); unit D/F should add the body comparison.

## Deviations (unit C, 2026-10-12)
- C: commits 76d2d283..0ebdcb51 (76d2d283 red folds, a17f10f1 projections, f2d6f738 red scheduler service.ran, 213699b8 RecordServiceRunService, 5fc427f2 red check/watch, 839e055c ProfileCheck + CheckProfile + ProfileWatch; main merged at 4e8e749e; 0ebdcb51 signal-4 integration test).
- `ServicesProjection`/`ProfileProjection` are internal in Core/Views, registered as `IProjection` singletons in `AddSindriCore`; the profile row's key is `"profile"`.
- `ProfileCheck.Decide` (public, Core/Profile) takes a 4th argument, the `CommandId`. `CheckProfileService` (6 deps) reads the previous check through `ILedgerStore.Latest`, not `ILedgerReader`.
- `ProfileWatch` idles 1 s without the lease and checks every 30 s with it; a failing check is logged once per distinct message under `SND-SCHED-001`. `AddSindriServe` now registers 5 hosted services (HostTests updated).
- The scheduler logs a failed `service.ran` append as `SND-SCHED-001 service 'x' ran but its service.ran wasn't recorded: <code> <detail>`.
- `ProfileProjection` on `profile.approved` recomputes `changed` against the last live hash and clears the paths when not changed.
- Extra tests: approve-clears-changed, unreadable live file, nothing approved, the watch without the lease.
- Signal 4 (`FlakyServiceTests`): a fake `IScheduledService` added through `LoopbackBinding`'s configure seam; one `SchedulerHost` (made with `ActivatorUtilities` on a `SteppedClock`) runs across both slots, because a new scheduler starts its schedule at "now"; the projector is driven with `Projector.CatchUp`. Unit D adds the `GET /services` assertion.
