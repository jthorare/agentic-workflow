# API P1 — CQRS spine: implementation plan

Spec: `docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md` (API), amended by
`docs/superpowers/specs/2026-10-11-sindri-ui-design.md` §6.7/§18.1 (UI wins, README X52). Branch
`feat/sindri-api-p1-cqrs` (worktree `.claude/worktrees/sindri-api-p1`), stacked on P0 `feat/sindri-api-p0-slice-only`
(PR #174) on main with S1 #163. Paths below are repo-relative. "§" = API spec unless prefixed (UI §, WF §, README X).

Forced deviations from the spec text (details in §6): `AddCommand` takes 3 type parameters, not 4 (AW1009); `Guid`
fields become typed ids `CommandId`/`CorrelationId` (AW1001); the CQRS rule tests live in `tests/Aw.Sindri.Tests/Cqrs/`
(only that project sees Host internals), so their rule records use check type `test`, not `archunit`.

---

## 1. Map of S1 today (what P1 touches)

### Host (`src/Sindri/Aw.Sindri.Host`, internal everything, no PublicAPI files)
| File | Today | P1 change |
|---|---|---|
| `Endpoints/EndpointTable.cs` | `MapSindriEndpoints(this WebApplication)`: `var group = app.MapGroup("/sindri")`, then 5 inline lambdas: `MapGet("/status")` → `IService<GetStatusRequest,GetStatusResponse>`; `MapGet("/ping")` → `IService<PingProcessRequest,Ping>`; `MapPost("/stop")` builds `new StopProcessRequest(StopReason.Stop)`; `MapPost("/profile/approve")` binds `ApproveProfileRequest` body; `MapGet("/events", EventStream.Stream)` (method group) | Restructure to two sub-groups + one line per slice (contract unit). `/new-slice`'s normal-mode anchor (`_ = app.MapGroup(…)` chain) never matched Sindri; `--slice-only` doesn't edit it |
| `Endpoints/EventStream.cs` | `Stream(HttpContext, IService<StreamEventsRequest,StreamEventsResponse>, TimeProvider, IShutdown, long? after)`; `after` beats `Last-Event-ID` (C4, P5's) | Unchanged; mapped from `StreamEventsEndpoint` |
| `Commands/CommandTable.cs` | `Run(args, environment, clock)`: `["serve"]` → `ServeCommand.Run`; else builds a provider from `Composition.Compose`, `Dispatch` switch: `[]`/`["--headless"]` → `StartCommand`, `status [--json]`, `stop`, `profile validate [--json]`, `profile approve [<hash>]`, `doctor [--json]`, `help`, `_ => UnknownVerb` (no `_ => CommandOutput.Usage` anchor). `Interactive()` = stdin and stdout not redirected | Anchor comments, one arm per verb (contract); CLI caller detection, no-start rule, `--no-start`, serve refusal (unit D) |
| `Commands/Verbs.cs` | `Verbs(ControlPlaneClient, IControlPlaneProbe, Output, IServiceProvider)`: `Status`, `Stop` (POST `/sindri/stop`, body null), `Validate`, `Doctor` (in-process), `Approve(hashText, interactive, input)` (prompt for first 6 chars, then POST `{hash, confirmed}`) | `--json` prints raw body; SND-AUTH-002 suffix naming the agent signal (D) |
| `Commands/Usage.cs` | One `string.Join('\n', …)` with 7 verb lines + exit-code line | One line per verb under an anchor (contract); `--no-start` (D) |
| `Commands/ControlPlaneClient.cs` | `Get<T>(route)`, `Post<TBody,T>(route, body)`; `Send` probes, sends **no headers**, deserializes success into `T` (re-serialized later by `Output.Json` — C5), `Problem()` maps every non-5xx to `ErrorKind.Invalid` | Headers from a `CallerHeaders` value; keep raw body (`ApiAnswer<T>(T Value, byte[] Body)`); 403 → `Forbidden` (D) |
| `Commands/Output.cs` | `Json<T>(value, exit)` re-serializes via AwJson; `Error()` exit 1 for `SND-API-001`, else 2 | `Raw(byte[] body, exit)` (D) |
| `Commands/ServeCommand.cs` | `Run(environment, clock, error, configure?)`: port check → `LoopbackBinding.Build` + `AddSindriServe` → `ServePort.Listen` → `Call<StartProcessRequest,…>` → `Listen` (on failure `Call<StopProcessRequest>(PortInUse/ListenFailed)`) → `RunUntilStopped` (on signal, if `HeldLease.Current` != null, `Call<StopProcessRequest>(StopReason.Signal)`; removes `control.json`). `Call<TReq,TRes>` resolves `IService<,>` in a scope with **no caller context** | Every `Call` runs inside `ICallerContextAccessor.EnterService("host")`; mint the human token after StartProcess succeeds, before `StartAsync`; remove it with `control.json` (B) |
| `Commands/StartCommand.cs` | `FindOrLaunch` launches when probe says `SND-API-001` | Not launched in an agent session (D) |
| `Server/SchedulerHost.cs` | 5 ctor deps (`IServiceScopeFactory, IApprovals, HeldLease, TimeProvider, ILogger`); `RunOne(schedule, run)` creates a scope per run | Wrap each run in `EnterService(<service name>)` (+1 dep = 6, AW1008 suggestion only) (B) |
| `Pipeline.cs` | `UseLoopbackGuard().UseApiProblems().UseValueObjectValidation().MapSindriEndpoints()` | Insert `UseApiVersionHeader()` first after the guard, `UseCallerContext()` and `UseCommandOriginRefusal()` after `UseApiProblems()` (B) |
| `LoopbackGuard.cs` | Bare 400 for non-loopback Host, bare 403 for non-loopback `Origin` (no body) | Unchanged (C3: stays for queries; a loopback `Origin` on a command now meets SND-API-008) |
| `ApiProblems.cs` | `Write(context, code, status, detail)` ProblemDetails helper; 500 → `SND-API-005`; bodiless 400 → `SND-CLI-003` | Reused by B's middlewares |
| `HttpResults.cs` | `ToHttpResult<T>(Result<T>)`; `Forbidden` → 403 already mapped | Unchanged |
| `SindriJsonContext.cs` | 16 `[JsonSerializable]` lines above `internal sealed partial class SindriJsonContext` | + P1 types (contract) |
| `Registration/SindriHostServiceCollectionExtensions.cs` | `AddSindriHost()`: `ProcessShutdown`, `IShutdown`, `IExitCode`; `AddSindriServe()`: `ServePort`, 3 hosted services | + `ICommandCatalog → CommandCatalog` (contract) |
| `Composition.cs` | clock, AwJson options, telemetry, `AddSindriCore/Features/Infrastructure(environment)/Host` | Unchanged |
| `LoopbackBinding.cs` | `Build(args, environment, clock, Action<IServiceCollection>? configure)` — the test seam (override `IIdGenerator`, `IProcessProbe`) | Unchanged |

### Features (`src/Sindri/Aw.Sindri.Features`, records public, services internal)
| File | Today | P1 change |
|---|---|---|
| `SindriFeaturesServiceCollectionExtensions.cs` | `AddSindriFeatures()`: plain `AddScoped<IService<X,Y>, XService>()` for GetStatus, StartProcess, StopProcess, RecordHeartbeat, StreamEvents, PingProcess, ValidateProfile, ApproveProfile, RunDoctor, `ProfileFacts`, `IScheduledService → HeartbeatService`; **no validators, no decorators**; anchor `return services;` | Commands via `AddCommand`, queries via `AddQuery` (contract); StartProcess/RecordHeartbeat/ValidateProfile/RunDoctor stay plain (internal / `local`) |
| `StopProcess/StopProcessRequest.cs` | `public sealed record StopProcessRequest(StopReason Reason)` | `: ICommand<StopProcessResponse>` |
| `StopProcess/StopProcessResponse.cs` | `(int Pid, long Epoch)` | `(int Pid, long Epoch, long? Sequence)` — `process.stopped`'s seq; null without a lease |
| `StopProcess/StopProcessService.cs` | `(HeldLease, ILease, ILedgerStore, EventFactory, IShutdown)`; appends `process.stopped:<epoch>`, clears, shuts down, releases | Fill `Sequence` from `LedgerAppend.Sequence` |
| `ApproveProfile/ApproveProfileRequest.cs` | `(ProfileHash? Hash, bool Confirmed)` | `: ICommand<ApproveProfileResponse>` |
| `ApproveProfile/ApproveProfileResponse.cs` | `(ProfileHash Hash, IReadOnlyList<string> Changes, bool Approved)` | `+ long? Sequence` — the `profile.approved` seq; null for a preview / already approved |
| `ApproveProfile/ApproveProfileService.cs` | `(IProfileSource, IApprovals, HeldLease, ILedgerStore, EventFactory)`; `Record()` returns `Unit` | `Record()` returns the sequence |
| `GetStatus/GetStatusRequest.cs` / `Response.cs` | `GetStatusRequest()`; response `(Running, Pid, Epoch, LeaseExpiresAt, ProfileStatus Profile, IReadOnlyList<ServiceStatus> Services)`; service takes `IApprovals` + `ILedgerStore` (see §6 G1) | `: IQuery<GetStatusResponse>`; `+ int ApiVersion` (B) |
| `PingProcess/PingProcessRequest.cs` | `PingProcessRequest` | `: IQuery<Ping>` |
| `StreamEvents/StreamEventsRequest.cs` | `(long After, int Max = 100)`; service takes `ILedgerStore` (G1) | `: IQuery<StreamEventsResponse>` |

All Features PublicAPI lines are in `PublicAPI.Unshipped.txt` (Shipped is `#nullable enable` only), so record changes edit Unshipped only (remove the old lines RS0017 reports, add RS0016's).

### Core (`src/Sindri/Aw.Sindri.Core`)
| File | Today | P1 change |
|---|---|---|
| `Events/EventFactory.cs` | `public sealed class EventFactory(EventScope scope, TimeProvider clock, IIdGenerator ids, JsonSerializerOptions json)`; `Entry<T>(type, payload, key)` → fresh correlation per event, `scope.Actor` (`host:sindri`), `Item: null`; `Payload<T>(LedgerEvent)` | C6: `+ ICallerContextAccessor callers`; actor and correlation from `callers.Current` else `host:sindri` + fresh id; overload `Entry<T>(type, payload, key, string? item)` (contract) |
| `Events/EventScope.cs` | `EventScope.ControlPlane = ("host:sindri", "local", 0)` | Unchanged (the no-context stamp) |
| `Events/EventTypes.cs` | `ProcessStarted`, `ProcessStopped`, `ProfileApproved`, `ServiceRun(name)` | `+ CommandHandled = "command.handled"` |
| `Ledger/*` | `ILedgerStore.Append/ReadAfter/Latest/Schema`; `LedgerEntry(EventEnvelope<string>, IdempotencyKey, string? Item)`; `LedgerAppend(Sequence, AlreadyRecorded)` | Unchanged (Head()/ILedgerReader are P2, C9) |
| `SindriErrors.cs` | 20 codes; `All` list | `+ SND-AUTH-001/002, SND-API-007/008/009` (contract) |
| `Registration/SindriCoreServiceCollectionExtensions.cs` | `EventScope.ControlPlane`, `EventFactory`, `HeldLease` singletons | `+ ICallerContextAccessor`, `HeldHumanToken` (contract) |
| `Aw.Sindri.Core.csproj` | no `RS0041` NoWarn | add it (first `Guid` ids) |
| `Ids/` | `ProfileHash`, `BootId`, `HostId`, `ServiceName` (string, Vogen) | `+ CommandId`, `CorrelationId` (Guid), `ActionId`, `RepoName` (string) |

### Infrastructure
`SindriSettings(Home, ProfilePath)` with `LedgerPath/ApprovalsDirectory/EndpointPath`; `SettingsFromEnvironment.Read(environment)`; `OwnerOnly.WriteAtomically` (0600 temp + rename), `EnsureDirectory` (0700); `PosixProcessProbe` (Process API + sysctl); `AddSindriInfrastructure(environment)` registers 11 adapters. P1 adds `FileHumanToken` (B), `PosixProcessAncestry` and `GitRepoLocator` (D), settings `TokenPath` and `ProverLabelAllowed` (B).

### Tests
- `tests/Aw.Sindri.Tests/SliceHarness.cs` constructs `new EventFactory(EventScope.ControlPlane, Clock, new SequentialIdGenerator(), Json)` — the only call site; changes with C6.
- `HostTests.Every_registered_service_resolves_in_a_scope` resolves every `IService<,>` from `Composition.Compose` — covers the chain registrations.
- `Server/ServeTests.cs`, `ServeStopTests.cs`, `ServePortTests.cs`, `EventStreamTests.cs` POST `/sindri/stop` / approve with **no headers** — they turn 403 once A's chain and B's middleware land; they need the human helper (contract) and a held lease (audit rows need a grant).
- `StartProcess/StartProcessTests.cs:24` asserts actor `host:sindri` (no context: stays true).
- `tests/Aw.Sindri.Testing/A.cs` is absent (S1 dropped it); `tests/Aw.Sindri.Tests/Ids/IdsJsonContext.cs` is absent (`IdJsonRoundTripTests` uses a reflection resolver). `new-slice --slice-only` creates either when missing, so parallel branches would collide on creating them.
- `Aw.Sindri.Tests.csproj` lacks `<AssemblyMetadata Include="AwGoldenSourceRoot" Value="$(MSBuildProjectDirectory)" />` (needed by `Aw.Kernel.Testing.Golden.Match`).
- AOT smoke `src/Sindri/aot-smoke/run.sh` runs every verb with `</dev/null` (no terminal) from a work dir under `artifacts/` (not `$TMPDIR`); `expected.txt` covers doctor, `--headless`, a refused second serve, approve preview, approve refusal, status, stop, status, doctor.

### Kernel facts that constrain the design
`IService<in TRequest, TResponse> where TRequest : notnull`; `Decorated<TReq,TRes>(inner)` with **protected** `Inner`;
`ValidatingService<TReq,TRes>(inner, IValidator<TReq>)` sealed; `IValidator<T>.Validate → Result<Unit>`;
`EventContext.Create(actor, tenant, ring, correlationId, traceId?)`; `Golden.Match(actual, assembly, relPath, Normalization)`.

---

## 2. Contract unit (runs alone, first)

Goal: every shared type, port, anchor and shared-file line exists; the build is at 0 warnings; **all S1 tests stay green**; exactly one test is red (`ListCommands`). Nothing changes behavior yet except C5 `sequence` and C6 `EventFactory` (both tiny and shared).

### 2.1 Core — `Aw.Sindri.Core.Commands` (`src/Sindri/Aw.Sindri.Core/Commands/`)
```csharp
public interface ICommand<TResult>;                 // ICommand.cs — marker; served by IService<TCommand, TResult>
public interface IQuery<TView>;                     // IQuery.cs
public interface IIssue<TResult>;                   // IIssue.cs — the issue kind (X26 carve-outs); same chain as commands
public interface IClassifiedCommand                 // IClassifiedCommand.cs — §2.1 / UI E-12
{
    string Variant { get; }                         // e.g. "answer-text"; resolved to row "<Command>:<Variant>"
    bool CarriesProtectedFields { get; }            // grant/grantTokens/accept present → classified protected (fail-closed)
}
public enum CallerClass { Human, Session, Service } // CallerClass.cs, JSON kebab names
public enum ActionClass { Request, StepReport, Pause, Job, Decision, Desk, Protected, Local, Issue }   // "step-report" etc.
public enum ClientLabel { None, Cli, Ui, Prover, Mcp }
public enum CommandOutcome { Accepted, Refused, Invalid, Failed }
public sealed record CallerContext(CallerClass Class, bool Terminal, string Actor, CommandId CommandId,
    CorrelationId CorrelationId, RepoName? Repo)    // CallerContext.cs; Actor composed here, never taken from a header
{
    public static CallerContext Http(CallerClass caller, bool terminal, ClientLabel client, RepoName? repo, CommandId id, CorrelationId correlation);
        // human:cli|ui|prover|http, session:cli|mcp|http; Terminal forced false unless Human
    public static CallerContext Service(string name, CommandId id, CorrelationId correlation);   // service:<name>
}
public interface ICallerContextAccessor             // ICallerContextAccessor.cs (C6)
{
    CallerContext? Current { get; }
    IDisposable Enter(CallerContext context);       // restores the previous value on Dispose
    IDisposable EnterService(string service);       // fresh ids from IIdGenerator
}
internal sealed class AsyncLocalCallerContextAccessor(IIdGenerator ids) : ICallerContextAccessor;   // real, not a stub
public sealed record CommandAccepted(CommandId CommandId, long Sequence);
public sealed record CliForm                         // CliForm.cs (E-16); contract: Parse accepts the pattern; C hardens
{   public string Text { get; }  public static Result<CliForm> Parse(string text); }
public sealed record CommandRow(ActionId Action, Type? CommandType, string? Variant, ActionClass Class,
    IReadOnlyList<string> ServiceGrants, bool NeedsTerminal, bool SelfAudited, bool Destructive, bool ReadsViews,
    string? Shortcut, string? Method, string? Route, CliForm Cli, string Ui);   // Ui: "ready" | "pending:<phase>" | "cli-only:<reason>"
public interface ICommandCatalog                    // port; Host implements (routes and CLI forms are Host facts)
{   IReadOnlyList<CommandRow> Rows { get; }  IReadOnlyList<CommandRow> For(Type commandType); }
public static class SindriApi { public const int Version = 1; }   // SindriApi.cs (status field + header)
```
`CommandRow` keeps 14 positional fields (record constructors aren't checked by AW1006). If review objects, group
`(Destructive, ReadsViews, Shortcut)` into `RowHints`.

### 2.2 Core — other folders
- `Ids/CommandId.cs`, `Ids/CorrelationId.cs`: Vogen `Guid`, `GuidIdJsonConverter<>`, `ITypedId<,>` (typed-ids.md template); `Ids/ActionId.cs`: string, `^[A-Z][A-Za-z]{2,40}(:[a-z][a-z0-9-]{1,40})?$`; `Ids/RepoName.cs`: string, the profile repo-name rule. Add `<NoWarn>$(NoWarn);RS0041</NoWarn>` to `Aw.Sindri.Core.csproj`.
- `Auth/HumanToken.cs`: `public sealed record HumanToken(string Value) { public bool Matches(string? presented); }` (`CryptographicOperations.FixedTimeEquals` over UTF-8). `Auth/IHumanTokenStore.cs` (port): `ValueTask<Result<HumanToken>> Mint(CancellationToken)` (new random value, writes the file), `ValueTask<Result<HumanToken?>> Read(CancellationToken)`, `ValueTask<Result<Unit>> Remove(CancellationToken)`. `Auth/HeldHumanToken.cs`: singleton like `HeldLease` (`Current`, `Hold(token)`, `Clear()`).
- `Events/EventTypes.cs`: `CommandHandled`. `Events/CommandHandled.cs`: `public sealed record CommandHandled(CommandId CommandId, ActionId Command, ActionClass Class, string Caller, CommandOutcome Outcome, string? Code)`. `Events/EventCatalogEntry.cs`: `(string Type, bool Public, Type? PublicPayload, string KeyShape)`. `Events/EventCatalog.cs`: `public static class EventCatalog { public static IReadOnlyList<EventCatalogEntry> All { get; } }` with rows (anchor comment `// one row per event type; phases append`): `process.started` (public, `process.started:<epoch>`), `process.stopped` (public), `profile.approved` (public, `profile.approved:<hash>:after:<seq>`), `service.heartbeat` (public, `service.heartbeat:<epoch>:<slot>`), `command.handled` (not public, `command.handled:<commandId>`).
- `Events/EventFactory.cs` (C6, real): ctor `+ ICallerContextAccessor callers`; actor/correlation from `callers.Current`, else `scope.Actor` + `ids.NewId()`; `Entry<T>(type, payload, key, string? item)` overload. Update `SliceHarness` (`new AsyncLocalCallerContextAccessor(ids)` is internal → expose a `Callers` property using a Testing helper, or make the accessor `public sealed`).
- `SindriErrors.cs`: `AuthTokenRequired` (`SND-AUTH-001`, Forbidden, "A human caller needs the per-start token"), `AuthRefused` (`SND-AUTH-002`, Forbidden), `CallerUnknown` (`SND-API-007`, Invalid), `OriginRefused` (`SND-API-008`, Forbidden), `CommandBusy` (`SND-API-009`, Unavailable); add to `All`.
- Registration: `AddSindriCore()` `+ .AddSingleton<ICallerContextAccessor, AsyncLocalCallerContextAccessor>() .AddSingleton<HeldHumanToken>()`.

### 2.3 Features
- `Features/Commands/AcceptAllValidator.cs`: `internal sealed class AcceptAllValidator<T> : IValidator<T>`.
- `Features/Commands/SindriCommandServiceCollectionExtensions.cs` (name ends in `ServiceCollectionExtensions` for ARCH-DI-001):
  ```csharp
  public static IServiceCollection AddCommand<TCommand, TResult, TService>(this IServiceCollection services)
      where TCommand : ICommand<TResult> where TService : class, IService<TCommand, TResult>;
  public static IServiceCollection AddQuery<TQuery, TView, TService>(this IServiceCollection services)
      where TQuery : IQuery<TView> where TService : class, IService<TQuery, TView>;
  public static IServiceCollection AddIssue<TIssue, TResult, TService>(this IServiceCollection services)
      where TIssue : IIssue<TResult> where TService : class, IService<TIssue, TResult>;
  ```
  The slice registers its own `IValidator<TCommand>` (its generated validator, or `AcceptAllValidator<T>`). **Contract
  bodies register `TService` + `ValidatingService` only** (today's behavior plus validation), so nothing turns red; A
  inserts the gate, audit and authorization.
- `AddSindriFeatures()`: StopProcess and ApproveProfile through `AddCommand` + `AcceptAllValidator`; GetStatus, PingProcess, StreamEvents through `AddQuery`; anchors `// ---- commands (one line per slice) ----` and `// ---- queries (one line per slice) ----` above `return services;`.
- Markers on the 5 S1 request records (§1 table); `sequence` on `StopProcessResponse`/`ApproveProfileResponse` filled by their services (C5).
- **ListCommands** via `node scripts/scaffold/new-slice.mjs --slice-only --package Sindri --slice ListCommands --behavior lists_every_catalog_row_with_its_class --surface http --route "GET /commands"`. Then shape the records: `ListCommandsRequest() : IQuery<ListCommandsResponse>`; `ListCommandsResponse(IReadOnlyList<CommandListRow> Rows)`; `Features/ListCommands/CommandListRow.cs`: `(ActionId Action, ActionClass Class, IReadOnlyList<string> AllowedCallers, bool Terminal, string? Method, string? Route, string Cli, string Ui, bool Destructive, bool ReadsViews, string? Shortcut)`. Stub service throws (red test stays red). Names follow the generator; the spec's `CommandList(rows)` is the same shape.

### 2.4 Host
- `Endpoints/EndpointTable.cs`:
  ```csharp
  var group = app.MapGroup("/sindri");
  var queries = group.MapGroup(string.Empty);    // GET: ARCH-CQRS-001 query routes
  var commands = group.MapGroup(string.Empty);   // POST: B adds the command metadata/Origin refusal on this line
  // ---- queries: one line per slice, sorted ----
  _ = queries.MapGetStatus();
  _ = queries.MapPingProcess();
  _ = queries.MapStreamEvents();
  // ---- commands: one line per slice, sorted ----
  _ = commands.MapApproveProfile();
  _ = commands.MapStopProcess();
  ```
  Each `Endpoints/<Slice>Endpoint.cs` holds `internal static RouteGroupBuilder Map<Slice>(this RouteGroupBuilder group)` with today's lambda moved verbatim (the generator's endpoint template). `StreamEventsEndpoint` maps `EventStream.Stream`.
- `Commands/CommandTable.cs`: anchor comments `// ---- verbs: one arm per verb ----` above `_ => UnknownVerb`; `Commands/Usage.cs`: one array element per verb under `// ---- one line per verb ----`. No behavior change.
- `Host/SindriHeaders.cs`: `internal static class SindriHeaders` constants `Caller = "X-Sindri-Caller"`, `Terminal`, `Client`, `Repo`, `Token = "X-Sindri-Token"`, `ApiVersion = "Sindri-Api-Version"` (shared by B, D and tests).
- `Host/CommandCatalog.cs`: `internal sealed class CommandCatalog : ICommandCatalog` concatenating row sources: `Host/Catalog/ShellRows.cs` (S1, real) and `Host/Catalog/PendingRows.cs` (only if the owner accepts G7; empty otherwise). Rows:

  | Action | Class | Callers | Terminal | Self-audited | Destructive | Method, route | CLI | Ui |
  |---|---|---|---|---|---|---|---|---|
  | `StopProcess` | desk | + `service:host` | no | yes | yes (UI §7) | `POST /sindri/stop` | `sindri stop` | `ready` |
  | `ApproveProfile` | protected | — | yes | no | no | `POST /sindri/profile/approve` | `sindri profile approve <hash>` | `cli-only:typed-confirmation` |
  | `RunDoctor` | local | — | no | no | no | — | `sindri doctor` | `cli-only:local` |
  | `ValidateProfile` | local | — | no | no | no | — | `sindri profile validate` | `cli-only:local` |

  `ReadsViews` false on all four. Register `ICommandCatalog` in `AddSindriHost()`.
- `SindriJsonContext.cs`: `+ CommandHandled, CommandAccepted, ListCommandsRequest, ListCommandsResponse, CommandListRow` (+ enums come through their records).

### 2.5 Tests and test support
- `tests/Aw.Sindri.Testing/A.cs` (restored): `public static class A` with `public static CallerContextBuilder Caller() => new();` (the generator appends before the closing brace). `CallerContextBuilder.cs`: `.Human()`, `.AtTerminal()`, `.Client(ClientLabel)`, `.Session()`, `.Service(name)`, `.InRepo(name)`, `Build()` with `SequentialIdGenerator` ids.
- `tests/Aw.Sindri.Testing/InMemoryHumanTokenStore.cs` + `HumanTokenStoreContract.cs` (B binds `FileHumanToken` to it); `InMemoryCommandCatalog.cs` (rows given) + `CommandCatalogContract.cs` (unique action ids; `For(type)` returns exactly that type's rows; every row's `Cli` parses).
- `tests/Aw.Sindri.Tests/Ids/`: `CommandIdTests`, `CorrelationIdTests`, `ActionIdTests`, `RepoNameTests` + holders + **`IdsJsonContext.cs`** (generator templates, so later `--slice-only` runs append to it).
- `tests/Aw.Sindri.Tests/Server/Callers.cs`: `HttpRequestMessage AsHuman(this HttpRequestMessage, WebApplication app, bool terminal = true)` (reads `HeldHumanToken` from `app.Services`), `AsSession(...)`.
- `tests/Aw.Sindri.Tests/Aw.Sindri.Tests.csproj`: `<AssemblyMetadata Include="AwGoldenSourceRoot" Value="$(MSBuildProjectDirectory)" />`.
- `tests/Aw.Sindri.Tests/Events/EventFactoryTests.cs`: `Entry_takes_the_actor_and_correlation_from_the_caller`, `Entry_without_a_caller_keeps_host_sindri_and_a_fresh_correlation`, `Entry_sets_the_item_when_given`.
- `StopProcessTests`/`ApproveProfileTests`: assert `Sequence`.
- `docs/csharp/sindri.md`: empty headings each unit fills: `## Callers and auth` (B, D), `## Command chain` (A), `## Command catalog` (C), `## Proving it` pointer (E). Error table rows for the 5 codes (contract).

### 2.6 End state of the contract unit
- Compiles at 0 warnings (`dotnet build tests/Aw.Sindri.Tests -warnaserror`); `dotnet format --verify-no-changes` clean; PublicAPI lines recorded from RS0016 (Core: commands, ids, auth, events; Features: ListCommands records, `AddCommand/AddQuery/AddIssue`).
- Red: exactly `ListCommandsTests.ListCommands_lists_every_catalog_row_with_its_class`. Everything else green, including S1's integration lane (no authorization yet).
- Commit sequence: `refactor: anchor EndpointTable, CommandTable and Usage one line per slice` · `feat: CQRS markers, caller context and catalog ports for the command chain` · `feat: EventFactory reads the caller context (C6)` · `feat: sequence on stop and approve responses (C5)` · `test: red ListCommands test (lists_every_catalog_row_with_its_class)`.

---

## 3. Fan-out units (at most 3 implementers at once)

Order: **wave 1: A, B, D** in parallel → **C** starts when A lands (it renders allowed callers through A's table) →
**E** starts when the next wave-1 unit lands (script + bash test + golden harness at once; goldens recorded after A, B
and C are merged into its branch). Every unit builds only `tests/Aw.Sindri.Tests` (and `tests/Aw.Architecture.Tests`
when touched) under a heavy-lock slot; the full-solution gate runs once, at integration.

### Unit A — the command chain (unit lane heavy)
Owns: `src/Sindri/Aw.Sindri.Features/Commands/*` (except the contract's two files' signatures), `src/Sindri/Aw.Sindri.Core/Commands/Authorization.cs`, `Core/Commands/ActionClassifier.cs`, `Core/Commands/AuthorizationDecision.cs`, `tests/Aw.Sindri.Tests/Commands/Chain/*`, `tests/Aw.Sindri.Tests/Commands/Authorization/*`.
- `Authorization.Decide(ActionClass cls, CallerContext? caller, CommandRow row) → AuthorizationDecision` (§4.2 table as data: no caller → refused as `none`; session: query/request/pause yes, step-report refused until P10's launch proof, job/decision/desk/protected no; human without terminal: everything but protected; service: query/request/pause/job yes, desk only if `row.ServiceGrants` has its name, decision/protected no; issue: per row). `Authorization.CallersFor(ActionClass, IReadOnlyList<string> grants) → IReadOnlyList<string>` (C renders it). Keep CA1502 ≤ 10: a lookup table, not nested ifs.
- `ActionClassifier.Classify(ICommandCatalog, object command) → CommandRow`: plain command → its single row; `IClassifiedCommand` → row `<Command>:<Variant>`; unknown variant → a synthetic `protected` row; `CarriesProtectedFields` on a non-protected row → `protected` (E-12 fail-closed).
- Decorators (`internal sealed`, each exposing `internal IService<TReq,TRes> Next => Inner;` for the order test):
  - `CommandGate<TReq,TRes>(inner, CommandGateLock gate, TimeProvider clock)` — `CommandGateLock` singleton wraps `SemaphoreSlim(1)`; wait with `new CancellationTokenSource(TimeSpan.FromSeconds(5), clock)` → `SND-API-009`; records `sindri.command.gate_wait`.
  - `AuditingService<TReq,TRes>(inner, AuditTrail audit, ICallerContextAccessor callers, ICommandCatalog catalog, ILogger<…>)` — `AuditTrail` (internal, Features/Commands) wraps `HeldLease + ILedgerStore + EventFactory` and appends `command.handled` keyed `command.handled:<commandId>`; outcome = `CommandOutcome` from the result (`SND-AUTH-*` → refused, `Invalid` kind → invalid, other error → failed); skip on an accepted `SelfAudited` row; audit failure after accept → log `SND-LEDGER-002`, return the result; on refused/invalid/failed → `SND-LEDGER-002` (503). Span `sindri.command <action>` tagged class, caller, outcome; counter `sindri.commands` (`Meter("Aw.Sindri")`, `ActivitySource("Aw.Sindri")`).
  - `AuthorizingService<TReq,TRes>(inner, ICallerContextAccessor callers, ICommandCatalog catalog)` — `SND-AUTH-002 <Command> is a human decision. Ask the human to run it in a terminal or the Sindri app: <row.Cli>` (protected from the app: "…in a terminal: …").
  - Real `AddCommand`/`AddIssue` bodies: `Gate(Audit(Authorize(Validating(TService))))`; `AddQuery`: `Validating(TService)` plus a `sindri.query <name>` span (a tiny `QuerySpan` decorator).
- No open-generic registrations (`typeof(IValidator<>)`); every decorator is closed in the factory lambda.
- After rebasing on B: update S1 integration tests (`ServeTests`, `ServeStopTests`, `EventStreamTests`, `ServePortTests` where they POST) to use `Callers.AsHuman` and hold the lease.

Tests — unit: `Authorization_matches_the_table_for_every_class_caller_and_terminal` (Theory, 9 × 4 × 2), `No_caller_context_is_refused`, `Service_host_may_stop_and_nothing_else_desk`, `Step_report_is_refused_until_launch_proof_exists`, `Unknown_variant_is_classified_protected`, `Protected_fields_override_the_variant`, `Known_variant_takes_its_rows_class`, `Every_registered_command_is_wrapped_gate_audit_authorize_validate_outermost_first` (walks `Next` from every `IService<TCmd,_>` where `TCmd : ICommand<>` in `Composition.Compose`), `Every_registered_command_has_a_validator`, `Queries_are_wrapped_in_validation_only`, `Refused_command_writes_only_command_handled_refused`, `Invalid_command_writes_only_command_handled_invalid`, `Accepted_command_appends_its_event_then_command_handled_accepted`, `Failed_command_is_audited_with_its_code`, `Accepted_stop_is_self_audited_and_writes_no_command_handled`, `Refused_stop_is_audited`, `Gate_times_out_after_5_seconds_with_SND_API_009`, `Commands_run_one_at_a_time`, `Audit_failure_after_accept_returns_the_result_and_logs_SND_LEDGER_002`, `Audit_failure_on_a_refusal_is_SND_LEDGER_002`, `Command_span_and_counter_carry_class_caller_and_outcome`, `Refusal_names_the_command_and_its_cli`. Integration: `Session_approve_is_403_and_only_command_handled_is_written` (real SQLite, after B).
Depends on: contract. Integration step depends on B (headers → context).

### Unit B — Host: callers, token, Origin, version, service contexts
Owns: `src/Sindri/Aw.Sindri.Host/CallerContextMiddleware.cs`, `Host/CallerHeaderParsing.cs` (pure), `Host/CommandOriginRefusal.cs`, `Host/ApiVersionHeader.cs`, `Host/ProverHome.cs` (pure), `Pipeline.cs`, `Commands/ServeCommand.cs`, `Server/SchedulerHost.cs`, the `commands` group line in `EndpointTable.cs`, `Infrastructure/Auth/FileHumanToken.cs`, `SindriSettings` (+ `TokenPath = <home>/auth/human.token`, `ProverLabelAllowed`), `SettingsFromEnvironment` (reads `TMPDIR`, `AW_STATE_DIR`), `Features/GetStatus/*` (`ApiVersion`), tests `tests/Aw.Sindri.Tests/Callers/*`, `tests/Aw.Sindri.Tests/Auth/*`.
- `CallerHeaderParsing.Parse(caller?, terminal?, client?, repo?) → Result<ParsedCaller>`: missing/`session` → session; `human` → human; other → `SND-API-007` (logged). Terminal only with human. Unknown client label → `None` (G12). Invalid `X-Sindri-Repo` → `SND-API-007` (or omit; see G12).
- `UseCallerContext()` middleware (after `UseApiProblems`): parse → for `human`: `X-Sindri-Token` must match `HeldHumanToken.Current` (`HumanToken.Matches`), else 403 `SND-AUTH-001` "restart the app or rerun the command"; `X-Sindri-Client: prover` with `human` only when `ProverLabelAllowed` (realpath of home under realpath(`$TMPDIR` or `Path.GetTempPath()`) or under `<AW_STATE_DIR>/<slug>/proof/`, G4), else `SND-AUTH-001` "the prover label is honoured only for a temp or proof home". Then `using accessor.Enter(CallerContext.Http(...))` around `next`. Applies to every `/sindri/*` request, queries included (§6: token "required with human"). Not audited (before the chain); logged.
- Command endpoints: the `commands` group gets `.WithMetadata(CommandRouteMetadata.Instance)`; `UseCommandOriginRefusal()` refuses any `Origin` on an endpoint carrying it with 403 `SND-API-008` (logged), before body binding (so a malformed body can't turn it into a 400). Issue routes get the same metadata later.
- `UseApiVersionHeader()` sets `Sindri-Api-Version: 1` on every response (right after the loopback guard); `GetStatusResponse.ApiVersion = SindriApi.Version`.
- `FileHumanToken : IHumanTokenStore` — 32 random bytes (`RandomNumberGenerator`), base64url, `OwnerOnly.EnsureDirectory(<home>/auth)` (0700) + `OwnerOnly.WriteAtomically` (0600); `Read` returns null when absent; `Remove`.
- `ServeCommand`: all `Call`s inside `EnterService("host")`; after `StartProcess` succeeds (so a refused second serve never overwrites the running one's token) `Mint` → `HeldHumanToken.Hold`; on stop remove it with `control.json`. `SchedulerHost.RunOne`: `EnterService(schedule.Service.Value)`.

Tests — unit: `Missing_caller_header_is_session`, `Human_and_session_values_map_to_their_classes`, `Unknown_caller_value_is_SND_API_007`, `Terminal_is_honoured_only_with_human`, `Client_label_sets_the_actor`, `Unknown_client_label_counts_as_none`, `Prover_home_rule_accepts_temp_and_proof_folders_only`, `Human_token_match_refuses_other_values`. Integration: `FileHumanTokenContractTests` (binds the contract base: `Minted_token_file_is_0600_in_a_0700_folder`, `A_new_mint_replaces_the_old_token`), `Human_without_a_token_is_403_SND_AUTH_001`, `Human_with_a_stale_token_is_403_SND_AUTH_001`, `Human_query_with_the_token_answers`, `Session_query_needs_no_token`, `Prover_label_is_refused_when_the_home_is_not_temporary`, `Prover_label_is_honoured_for_a_temp_home`, `Command_with_an_Origin_header_is_403_SND_API_008`, `Query_with_a_loopback_Origin_still_answers`, `Every_response_carries_Sindri_Api_Version_1`, `Status_reports_api_version_1`, `Serve_mints_the_token_only_after_taking_the_lease`, `Ctrl_C_on_serve_records_process_stopped_as_service_host_and_releases_the_lease` (passes before and after A), `Start_and_heartbeat_events_carry_service_actors`, `A_command_and_its_events_share_the_callers_actor_and_one_correlation`.
Depends on: contract.

### Unit D — CLI: caller detection, headers, no-start rule, raw `--json`
Owns: `Host/Commands/CallerHeaders.cs`, `Host/Commands/AgentSession.cs`, `Commands/CommandTable.cs` (dispatch arms), `Commands/Verbs.cs`, `Commands/StartCommand.cs`, `Commands/ControlPlaneClient.cs`, `Commands/Output.cs`, `Commands/Usage.cs`, `Core/Control/IProcessAncestry.cs` + `Core/Control/ProcessAncestor.cs`, `Core/Control/IRepoLocator.cs`, `Infrastructure/Control/PosixProcessAncestry.cs`, `Infrastructure/Control/GitRepoLocator.cs`, their fakes and contract bases in `tests/Aw.Sindri.Testing/`, `scripts/env-scan-ignore.txt`, tests `tests/Aw.Sindri.Tests/Commands/Cli*`.
- `AgentSession.Detect(environment, IProcessAncestry, interactive) → AgentSignal?`: variables `CLAUDECODE`, `AW_SINDRI_CHILD`, `CODEX_SANDBOX`, `CURSOR_AGENT` (§6; engine P2's probe later pins the list in a golden); ancestry basenames `claude`, `codex`, `cursor-agent` (provisional, G9); `AW_SINDRI_PROVER=1` skips only the ancestry check. Unreadable ancestry → ignored (G10).
- `CallerHeaders.For(...)`: human iff interactive and no signal → `X-Sindri-Caller: human`, `X-Sindri-Terminal: 1`, `X-Sindri-Client: cli` (`prover` when `AW_SINDRI_PROVER=1`), `X-Sindri-Token` from `IHumanTokenStore.Read` (never read when a signal is set); else `session` + `Client: cli`, no token. `X-Sindri-Repo` from `IRepoLocator` in every mode (§3.1: parent of `git rev-parse --path-format=absolute --git-common-dir`, realpath, longest approved-profile repo path that contains it; omitted outside).
- `IProcessAncestry.Ancestors(int pid, int max) → IReadOnlyList<ProcessAncestor(int Pid, string Name)>`: Linux `/proc/<pid>/stat`; macOS `proc_pidinfo(PROC_PIDTBSDINFO)` through `[LibraryImport("libproc")]` (no `ps` spawn per level). `IRepoLocator` runs `git` through `Process` (Infrastructure only).
- `ControlPlaneClient(probe, options, CallerHeaders headers, timeout?)`: adds headers; returns `Result<ApiAnswer<T>>` with the raw body; 403 → `ErrorKind.Forbidden`. `--json` prints the body unchanged (`Output.Raw`). Stop/approve text unchanged.
- No-start rule (§7): bare `sindri`/`--headless` in an agent session → `SND-API-001 Sindri isn't running; ask the human to run 'sindri'` (exit 1), never launch; when running, print the summary as today. `serve` in an agent session → refused the same way unless `AW_SINDRI_PROVER=1` (G6). `profile approve` from a human starts the process if needed (`--no-start` prints `SND-API-001`) (G8). `status`, `stop` unchanged (never start).
- A refused command from a session prints the server's `SND-AUTH-002 …` plus ` (this CLI ran as a session: <CLAUDECODE is set | parent process claude | no terminal>)` (WF §4.2).
- `scripts/env-scan-ignore.txt`: `CODEX_SANDBOX # set by Codex in its sandboxed sessions; sindri's CLI reads it to class itself as a session`, `CURSOR_AGENT # …`, `AW_SINDRI_PROVER # set by scripts/sindri-prove.sh human; exempts the CLI from the parent-process check`.
- Record in `docs/csharp/sindri.md` (Callers and auth): the four variables, the ancestry names, and real-run results: Claude Code (agent can check: `env | grep CLAUDECODE` here); Codex via `codex exec` in `read-only`, `workspace-write` and `danger-full-access` if the login allows, else an owner step; Cursor's agent is not installed on this Mac → owner step, recorded as "unverified".

Tests — unit: `Cli_is_human_only_at_a_terminal_with_no_agent_signal` (Theory over the four variables and "no terminal"), `A_harness_parent_makes_the_cli_a_session` (Theory: claude, codex, cursor-agent), `Prover_marker_skips_the_parent_check_but_not_the_variables`, `Human_cli_sends_caller_terminal_client_and_token`, `Session_cli_sends_no_token_and_no_terminal`, `Agent_cli_never_reads_the_token_file`, `Repo_header_comes_from_the_main_checkout_and_the_longest_profile_path`, `Outside_any_profile_repo_the_repo_header_is_omitted`, `Bare_sindri_in_an_agent_session_prints_SND_API_001_and_launches_nothing`, `Serve_in_an_agent_session_is_refused_unless_the_prover_marker_is_set`, `Profile_approve_starts_the_control_plane_unless_no_start`, `Json_prints_the_api_body_unchanged`, `Refused_command_names_the_agent_signal_it_saw`, `A_403_is_read_as_forbidden`. Integration: `PosixProcessAncestryContractTests` (`Ancestors_of_this_process_end_at_pid_1_or_launchd`), `GitRepoLocatorContractTests` (`A_worktree_resolves_to_its_main_checkout`, temp repo + `git worktree add`), `Cli_status_json_equals_the_raw_api_body` (CLI against a `LoopbackBinding` host).
Depends on: contract (ports, `SindriHeaders`, token store fake). Its end-to-end tests need B merged in its branch.

### Unit C — catalogs, ListCommands, E-8/E-12/E-16, ARCH-CQRS rules, rule records
Owns: `Features/ListCommands/*`, `Host/Endpoints/ListCommandsEndpoint.cs` + its `EndpointTable` line, the `AddQuery<ListCommandsRequest,…>` line, `Host/CommandCatalog.cs` + `Host/Catalog/*` (refinement), `Core/Commands/CliForm.cs` (validation), `Core/Events/EventCatalog.cs` (tests), `tests/Aw.Sindri.Tests/ListCommands/*`, `tests/Aw.Sindri.Tests/Catalog/*`, `tests/Aw.Sindri.Tests/Cqrs/*`, `rules/engineering/csharp/sindri-cqrs-*.yaml`, `rules/engineering/csharp.inventory.yaml`, `docs/csharp/rule-records.md` (one line naming the new records' source).
- `ListCommandsService(ICommandCatalog catalog)` maps rows → `CommandListRow`, `AllowedCallers` from `Authorization.CallersFor` (strings `human+terminal`, `human`, `session`, `service:<name>`), sorted by action id. Endpoint: `queries.MapListCommands()`.
- `CliForm.Parse` (E-16): `^sindri( [a-z][a-z-]*| <[a-zA-Z]+>| \[[^\]\r\n]+\])*$`, no control, bidi or invisible characters; a later `Fill(params ITypedId…)` substitutes only typed ids (P4 uses it for `NeedsYouRow.cli`).
- Catalog rules (unit): every row's cli parses; reserved keys `G J K O A T B Y C R / ?` never used as `shortcut`; `cli-only:<reason>` only on terminal-bound rows (local rows exempt, G16); no `pending:<phase>` names a shipped phase (`ShippedPhases = ["S1","P0","P1"]` constant, each phase appends); ready rows' routes exist (integration, with ARCH-CQRS-001).
- E-12 support: variant rows keyed `<Command>:<variant>`; a test fixture `FixtureApply : IClassifiedCommand` with rows `FixtureApply:answer-text` (desk), `:answer-grant` (protected) in an `InMemoryCommandCatalog` proves the fail-closed lookup end to end. **The engine's `ApplyCommand` rows are not registered by P1** (§6 G11).
- ARCH-CQRS-001 (`Cqrs/EndpointCqrsRules.cs` pure `Check(EndpointDataSource, ICommandCatalog) → IReadOnlyList<string>` + tests): read each endpoint's `IParameterBindingMetadata` (RDG), find `IService<TReq,_>`; GET → `TReq : IQuery<>`; POST → `TReq : ICommand<>`, or `TReq : IIssue<>` **and** the route is one of the four carve-outs (`POST /sindri/workflow/instances/{a}/nonce`, `POST /sindri/workflow/challenges`, `POST /sindri/sessions/{launch}/heartbeat`, `POST /sindri/sessions/{launch}/exit`) **and** its catalog row's class is `issue`. ARCH-CQRS-002: no service whose request is `IQuery<>` takes `ILedgerStore`, `ILease`, `IApprovals` (projection writers join in P2); S1 allowlist per G1. ARCH-CQRS-003: every `ICommand<>`/`IIssue<>` type in Features has a row; every `IClassifiedCommand` type has at least one variant row.
- Rule records: `sindri-cqrs-endpoints.yaml` (ARCH-CQRS-001), `sindri-cqrs-query-ports.yaml` (002), `sindri-cqrs-catalog.yaml` (003); `section: "4b.2"`, `source: { type: adr, ref: "docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md#25-architecture-tests-testsawarchitecturetests" }`, checks `type: test` with `id: "ARCH-CQRS-00n"` and `ref` = the test file (G17); inventory lines; `node scripts/check-rules.mjs` green.

Tests — unit: `ListCommands_lists_every_catalog_row_with_its_class` (the contract's red), `Allowed_callers_follow_the_authorization_table`, `Every_command_and_variant_has_a_catalog_row`, `Every_catalog_cli_is_built_from_checked_parts`, `Catalog_shortcuts_avoid_the_reserved_keys`, `Cli_only_rows_are_terminal_bound`, `No_pending_row_names_a_shipped_phase`, `Stop_is_destructive_and_no_shell_row_reads_views`, `Every_event_type_is_in_the_event_catalog` (every `EventTypes` constant + `service.heartbeat`), `Command_handled_is_not_public`, `Query_handlers_take_no_write_ports`, `Variant_rows_resolve_through_the_fail_closed_rule`, `CommandCatalog` binding of `CommandCatalogContract`. Integration: `Every_get_resolves_a_query_and_every_post_a_command`, `Cqrs_rule_flags_a_get_command_a_post_query_and_an_issue_route_outside_the_carve_outs` (fixture hosts), `Every_ready_row_route_is_mapped`, `Commands_route_lists_every_action_for_a_session_caller`.
Depends on: contract; A's `Authorization.CallersFor` (starts after A lands).

### Unit E — `scripts/sindri-prove.sh`, goldens, AOT smoke
Owns: `scripts/sindri-prove.sh`, `scripts/lib/with-tty.sh` (new; shared by the prover and the smoke), `scripts/tests/sindri-prove.test.sh`, `tests/Aw.Sindri.Tests/Golden/Api/**`, `src/Sindri/aot-smoke/run.sh`, `src/Sindri/aot-smoke/expected.txt`, `.env.example` (`# SINDRI=` line), the AGENTS.md bash-test line.
- `sindri-prove.sh`:
  - `sindri-prove.sh <verb> [args…]` → `$SINDRI <verb>` as the calling agent (env untouched: `session`).
  - `sindri-prove.sh human <verb> [args…]` → `env -u CLAUDECODE -u AW_SINDRI_CHILD -u CODEX_SANDBOX -u CURSOR_AGENT AW_SINDRI_PROVER=1` under a pty (`with_tty`: BSD `script -q /dev/null cmd…`, util-linux `script -qec "<quoted cmd>" /dev/null`), stdin passed through (for `printf '<6>\n' | … human profile approve <hash>`), CR stripped.
  - `sindri-prove.sh curl [--no-terminal] [--no-token | --token T] [-H 'Name: value']… <METHOD> <path> [json]` → URL from `$AW_SINDRI_HOME/control.json`, headers `X-Sindri-Caller: human`, `X-Sindri-Terminal: 1` (unless `--no-terminal`), `X-Sindri-Client: prover`, `X-Sindri-Token: $(cat $AW_SINDRI_HOME/auth/human.token)` (unless overridden), `Content-Type: application/json` with a body; prints the body, then `HTTP <status>` on stderr (G5, G6').
  - `sindri-prove.sh url` → the base URL (for session `curl`s with no caller header, which provers may write by hand).
  - Guard first: `AW_SINDRI_HOME` set, and its realpath under realpath(`${TMPDIR:-/tmp}`) or `<AW_STATE_DIR>/<slug>/proof/`; else exit 2 `sindri-prove: AW_SINDRI_HOME must be under $TMPDIR or a proof folder (got …)`. Exports `AW_SINDRI_PORT=0`. `SINDRI` defaults to `dotnet run --project src/Sindri/Aw.Sindri.Host --`; a built binary is recommended for proofs (signals reach it directly).
- `scripts/tests/sindri-prove.test.sh` (stub `SINDRI` script printing args, `[ -t 0 ]`, selected env; stub `curl` on `PATH`): `refuses_an_unset_home`, `refuses_the_default_home`, `refuses_a_home_outside_tmp`, `accepts_a_temp_home`, `accepts_a_proof_folder`, `verb_mode_keeps_the_environment`, `human_mode_unsets_agent_variables_sets_the_marker_and_has_a_tty`, `human_mode_strips_cr`, `curl_mode_reads_url_and_token_and_adds_headers`, `curl_flags_drop_terminal_and_token`, `port_is_zero`. Picked up by `scripts/ci/bash-tests.sh` automatically (git-tracked `*.test.sh`).
- Golden harness `tests/Aw.Sindri.Tests/Golden/Api/ApiGolden.cs` + `GoldenHost.cs`: `LoopbackBinding.Build` with `FakeTimeProvider(KnownInstants.Epoch)`, `SequentialIdGenerator`, `FakeProcessProbe` (fixed pid), a temp home with a fixed profile, the lease held (StartProcess under `service:host`), the human token held; body re-indented as served; normalization only for ProblemDetails `traceId` (G18). One shared host per test class (lane budget). `ApiGoldenTests` (Lane=integration), one Theory row per file:
  - `status/{running-unapproved,running-approved,human-no-token,human-stale-token,unknown-caller,prover-refused}.json`
  - `ping/running.json` · `commands/catalog.json` · `events/process-started.json` (the first event's `data` JSON)
  - `stop/{accepted,refused-session,refused-origin}.json`
  - `profile-approve/{preview,approved,already-approved,refused-session,refused-no-terminal,refused-origin,stale-hash,bad-body}.json`
  - Route slug rule (for later phases): the path after `/sindri/`, `/` → `-`, each `{id}` → `id` (`items-id`).
- AOT smoke: work dir from `mktemp -d` under `$TMPDIR`; run with the four agent variables unset and `AW_SINDRI_PROVER=1`; human steps (`--headless`, `profile approve` preview, `stop`) through `with_tty`, CR stripped; keep one `</dev/null` session run each of `profile approve` and `stop` to show `SND-AUTH-002` (exit 2); add `status --json` piped to a normalizer for `apiVersion`. Re-record `expected.txt` from a real run (new lines: two `SND-AUTH-002`, the session `stop` refusal followed by the human `stop`).
Depends on: contract for the harness; goldens need A, B, C merged into its branch; smoke needs B and D.

---

## 4. Integration

- **Merge order** into `feat/sindri-api-p1-cqrs`: contract → B → A (rebased on B; finishes its integration step) → D → C → E. A unit that finishes out of order still merges at its turn; the integrator rebases.
- **Shared-file conflict points:** `Features/SindriFeaturesServiceCollectionExtensions.cs` (only C adds a line; A edits the helper file); `Host/Endpoints/EndpointTable.cs` (B: the `commands` group line; C: the `ListCommands` line); `Host/SindriJsonContext.cs` (contract has every P1 type; flag any unit that adds one); `Host/Catalog/*` (contract rows, C refinements); `Core/SindriErrors.cs` (contract only); `tests/Aw.Sindri.Testing/A.cs` (contract; E may append golden builders); `tests/Aw.Sindri.Tests/Server/Callers.cs` (contract; A uses); `PublicAPI.Unshipped.txt` in Core (contract, D's two ports) and Features (contract, B's `GetStatusResponse` change); `docs/csharp/sindri.md` sections; `scripts/env-scan-ignore.txt` (D only); `.env.example`, AGENTS.md, `expected.txt` (E only).
- **PublicAPI regeneration:** after the last merge, `dotnet build src/Sindri/Aw.Sindri.Core` then `…Features`; append RS0016 symbols (the `new-slice.md` sed recipe), delete lines RS0017 reports, sort within the file's existing order, review.
- **AOT smoke:** `bash src/Sindri/aot-smoke/run.sh` once (heavy slot); fail on any IL2xxx/IL3xxx; diff `expected.txt`.
- **Docs (`docs/csharp/sindri.md`):** routes table + `GET /sindri/commands`; a "Callers and auth" section (the five headers, the `human` token file `auth/human.token` 0600 in `auth/` 0700, minted per start after the lease, `SND-AUTH-001`, the prover label rule, `Origin` refusal, `Sindri-Api-Version`); the binary table (no-start rule, `--no-start`, `serve` in an agent session, exit 2 for `SND-AUTH-002`); Files table (`auth/human.token`); events (`command.handled`, not public; actors `human:*`, `session:*`, `service:host|heartbeat`); the 5 codes; the agent-variable check results; "Hook contract: unchanged". `docs/csharp/rule-records.md`: the CQRS records' source. AGENTS.md: `bash scripts/tests/sindri-prove.test.sh` in the bash list. `planning/API_CONTRACT.md` waits for v0 (§12).
- **deps.yaml:** no new edges expected (`git`, `script`, `curl` aren't graph nodes; nothing new between packages). Run `node scripts/check-deps.mjs`; optionally extend the Sindri node's note with "API P1: command chain, callers".
- **Full merge gate** (AGENTS.md): `dotnet build -warnaserror`; unit + integration lanes with `--ignore-exit-code 8`; `node scripts/ci/lane-budgets.mjs` after a Release build; `dotnet format --verify-no-changes`; `scripts/dotnet-verify-packages.sh`; `scripts/sync-rules.sh --check`; `node scripts/check-deps.mjs`; `scripts/check-env-example.sh`; `node scripts/check-rules.mjs`; `bash scripts/ci/bash-tests.sh`; TS packages unchanged (run anyway, CI is off); `./setup.sh --providers claude,codex,cursor --dry-run`.

---

## 5. Verification plan draft (the prover runs it on the integrated branch)

Setup (one temp home per run, a built binary):
```bash
dotnet build src/Sindri/Aw.Sindri.Host -c Debug           # heavy slot
export SINDRI="$PWD/src/Sindri/Aw.Sindri.Host/bin/Debug/net10.0/sindri"
export AW_SINDRI_HOME="$(mktemp -d)/home"; mkdir -p "$AW_SINDRI_HOME/profile"
printf 'schemaVersion: 1\nuser: prover\nservices:\n  heartbeat: { every: 5s }\n' > "$AW_SINDRI_HOME/profile/profile.yaml"
P=scripts/sindri-prove.sh; L="sqlite3 -readonly $AW_SINDRI_HOME/ledger.db"
$P human --headless                                        # starts serve as the owner
H=$($SINDRI profile validate --json | jq -r .hash); U=$($P url)
```

| # | Lens | Step | Expect | Independent cross-check |
|---|---|---|---|---|
| 1 | Auth boundary | `curl -s -XPOST -H 'Content-Type: application/json' $U/sindri/profile/approve -d "{\"hash\":\"$H\",\"confirmed\":true}"` (no caller header) | 403, `title` `SND-AUTH-002` | `$L "select type,actor,json_extract(payload_json,'$.outcome') from events order by seq desc limit 2"` → `command.handled|session:http|refused`; `select count(*) from events where type='profile.approved'` → 0 |
| 2 | Auth boundary | `$P curl --no-terminal POST /sindri/profile/approve "{\"hash\":\"$H\",\"confirmed\":true}"` | 403 `SND-AUTH-002` | ledger: `command.handled` refused, actor `human:prover`, no `profile.approved` |
| 3 | Error state | `$P curl -H 'Origin: http://localhost:3000' POST /sindri/stop` | 403 `SND-API-008` | process still running (`$P status` exit 0/1, not "not running"); no new `command.handled` row (refused before the chain); serve log has the `SND-API-008` line |
| 4 | Functional | `$P curl GET /sindri/commands \| jq '.rows[] \| {action,class,ui}'` | every action with its class: `StopProcess` desk ready, `ApproveProfile` protected `cli-only:typed-confirmation`, `RunDoctor`/`ValidateProfile` local | same body as a session `curl -s $U/sindri/commands` (`diff <(…) <(…)`); equals `commands/catalog.json` modulo ids/times |
| 5 | Auth boundary | `$P stop` (as the agent: CLAUDECODE set) | stderr `SND-AUTH-002 … (this CLI ran as a session: CLAUDECODE is set)`, exit 2 | ledger `command.handled` refused actor `session:cli`; `$P status` still running |
| 5b | Auth boundary | `$P --headless` after a human stop | `SND-API-001 … ask the human to run 'sindri'`, exit 1 | `test ! -e $AW_SINDRI_HOME/control.json`; `pgrep -f "$SINDRI serve"` empty |
| 6 | Functional | `env -u CLAUDECODE -u AW_SINDRI_CHILD -u CODEX_SANDBOX -u CURSOR_AGENT AW_SINDRI_PROVER=1 $SINDRI serve & pid=$!`; wait for `control.json`; `kill -INT $pid; wait $pid; echo $?`; then `$P human --headless` | exit 0; restart within 2 s, `epoch N+1` | `$L "select type,actor,json_extract(payload_json,'$.reason') from events where type like 'process.%' order by seq"` → `process.stopped|service:host|signal`, then `process.started|service:host`; no `command.handled` for the self-audited stop |
| 7 | Error state (token) | `$P curl --no-token GET /sindri/status` | 403 `SND-AUTH-001` | `curl -s $U/sindri/status` (session, no token) → 200 |
| 8 | Error state (token) | `T=$(cat $AW_SINDRI_HOME/auth/human.token)`; `$P human stop`; `$P human --headless`; `$P curl --token "$T" GET /sindri/status` | 403 `SND-AUTH-001` (stale) | `stat -f '%Lp' $AW_SINDRI_HOME/auth/human.token` → 600 (`stat -c %a` on Linux); `stat -f '%Lp' $AW_SINDRI_HOME/auth` → 700; the new token differs from `$T` |
| 9 | Error state (prover) | home `N=$HOME/.cache/aw-p1-nontemp-$$` (not the owner's): start `env -u … AW_SINDRI_PROVER=1 AW_SINDRI_HOME=$N AW_SINDRI_PORT=0 $SINDRI serve &`; raw `curl` with `X-Sindri-Caller: human`, `X-Sindri-Client: prover`, `X-Sindri-Token: $(cat $N/auth/human.token)` to `GET /sindri/status`; then `kill -INT`, `rm -rf $N` | 403 `SND-AUTH-001` (prover label refused) | the same request without `X-Sindri-Client` → 200; integration test `Prover_label_is_refused_when_the_home_is_not_temporary` green. (The prover script itself refuses `$N`: show `AW_SINDRI_HOME=$N $P status` → exit 2 guard message.) |
| 10 | Error state | `curl -s -H 'X-Sindri-Caller: admin' $U/sindri/status` | 400 `SND-API-007` | serve log line |
| 11 | Contract | `curl -sD - $U/sindri/ping -o /dev/null \| grep -i sindri-api-version` | `Sindri-Api-Version: 1` | `$P curl GET /sindri/status \| jq .apiVersion` → 1 |
| 12 | Contract | `diff <($P human status --json \| tr -d '\r' \| jq -S 'del(.leaseExpiresAt)') <($P curl GET /sindri/status \| jq -S 'del(.leaseExpiresAt)')` | no diff (raw `--json`, C5) | — |
| 13 | Functional | `printf '%s\n' "${H:0:6}" \| $P human profile approve $H`; `$P curl POST /sindri/stop` | approved; stop body has `sequence` | `$L` shows `profile.approved` actor `human:prover` + `command.handled` accepted with the same correlation; stop's `sequence` = the `process.stopped` seq |
| 14 | Contract (goldens) | `find tests/Aw.Sindri.Tests/Golden/Api -name '*.json' \| sort` vs §3 E list; `dotnet test --project tests/Aw.Sindri.Tests --filter-trait Lane=integration` | every route × scenario present; golden tests green | `jq -e . <each file>` parses; no file contains a real pid, port or wall-clock time |
| 15 | AOT | `bash src/Sindri/aot-smoke/run.sh` | `aot-sindri-smoke: ok`, no IL warnings | `expected.txt` diff reviewed in the PR |

Evidence: commands + raw output + ledger excerpts in `$AW_DIR/proof/sindri-api-p1/` (`manual-test.md`, `evidence.md`); the run's temp home path recorded; each claim paired with its cross-check output side by side.

---

## 6. Spec gaps and questions

**True gaps (ask the owner):**

- **G1 — ARCH-CQRS-002 can't pass on S1's queries in P1.** §2.5: "No query handler's constructor takes `ILedgerStore`, `ILease`, `IApprovals` or a projection writer", and P1 ships the rule. But S1's `GetStatusService(HeldLease, IApprovals, IProfileSource, ILedgerStore, EventFactory)` and `StreamEventsService(ILedgerStore)` are queries (§2.5: "S1's `GET /ping` and `GET /status` are queries, and `GET /events` is the stream query"), and the read-only port arrives in P2: C9 "P2 adds `Head()` to `ILedgerStore` … and `ILedgerReader`". **Recommend:** P1's test carries an explicit allowlist of those two handlers with "removed in P2 (C9)"; P2 deletes it when `ILedgerReader` and a read-only approvals port land. Alternative: pull a minimal `ILedgerReader` into P1 (adds a port, fake, contract and adapter to P1).
- **G7 — Who pre-registers `pending` catalog rows?** UI §7 shows controls as "`pending:P3` → ready", U1's signal is "the palette lists every catalog row with its status", and §3.3 lets `doctor` list "the rows still `pending`". So rows have to exist before their phase ships, but §12 step 2 has each phase add "the catalog rows" for its own slices. **Recommend:** P1 registers pending rows for every API action of P2–P10a (RebuildViews, CreateItem, `CloseItem:done`/`:cancelled`, FileRequest, Decide, RunService, PauseService, ResumeService, CancelJob, `OpenItem:open`/`:attach`/`:resume`, ProbeLaunchTargets), with class, route and CLI from the spec tables and no command type. Each phase then flips its rows to `ready` and binds the type. Engine rows come from the engine lane and context rows from the context lane. This needs the other lanes' leads to agree.
- **G6 — Does `sindri serve` obey the no-start rule?** D5 says "Nothing run inside an agent session starts it", while §7's table lists only `sindri`, `--headless`, commands and queries (not `serve`). §12 says provers start the process through `sindri-prove.sh`. **Recommend:** `serve` inside an agent session is refused unless `AW_SINDRI_PROVER=1`. The AOT smoke and proof step 6 set the marker.
- **G4 — "a proof folder" isn't defined.** §6 says "honours that marker only when its own home is under `$TMPDIR` or a proof folder", and §12 says "the phase's proof folder". `/specToProvenPR` writes proof to `$AW_DIR/proof/<stage-slug>`. **Recommend:** a home whose realpath is under `<AW_STATE_DIR>/<repo-slug>/proof/`, checked the same way by the server and by `sindri-prove.sh`. Also confirm the marker reaches the server as `X-Sindri-Client: prover`. §6 never says how the server sees an env var the CLI reads; the label is the only signal on the wire, and the server refuses it with `SND-AUTH-001` outside a temp or proof home.

**Choices with an obvious default (no answer needed unless you disagree):**

- **G2 — AW1009:** §2.1's `AddCommand<TCommand, TResult, TService, TValidator>()` has 4 type parameters, and AW1009 caps them at 3. Default: `AddCommand<TCommand, TResult, TService>()`, and the slice registers its own `IValidator<TCommand>` (the generator already prints that line). The chain test asserts every command has one.
- **G3 — AW1001:** `CallerContext(…, Guid CommandId, Guid CorrelationId)` and `CommandAccepted(Guid CommandId, long Sequence)` are banned shapes in public Core. Default: typed ids `CommandId` and `CorrelationId` (Vogen `Guid`), plus the `RS0041` NoWarn. `ActionId` and `RepoName` become typed strings for the same reason.
- **G5 — §12's prover `curl` header list predates X40:** it lists Caller, Terminal, Client and Content-Type but no token. Default: the script also sends `X-Sindri-Token` from the guarded home.
- **G6' — Signal bullet 2 vs "Provers never hand-roll a human curl":** the bullet needs a `human` call without a terminal, and the token cases need a missing or stale token. Default: `sindri-prove.sh curl --no-terminal | --no-token | --token T | -H …`, still behind the home guard.
- **G8 — §7 vs S1's doc:** §7 says human commands "Start it, then run", while S1's doc says `profile approve` "needs it running … exit 1". Default: follow §7. `profile approve` starts the process if it isn't running, and `--no-start` opts out. Update the doc table.
- **G9 — Agent list before the engine probe:** §6 says "The list comes from the engine's harness probe (engine §6.2, P2)", but engine P2 runs after API P1. Default: hard-code §6's four variables and the ancestry basenames `claude`, `codex`, `cursor-agent`. Engine P2 replaces both with its golden.
- **G10 — Ancestry can't be read:** default is to ignore it (the env vars still apply), under the honest-mistake model. Fail-closed would refuse the owner's own CLI.
- **G11 — E-12 rows (owner's question):** P1 lands the mechanism: `IClassifiedCommand` with `Variant` and `CarriesProtectedFields`, the fail-closed lookup (unknown variant → protected, protected fields → protected), variant rows `<Command>:<variant>` in the catalog, and fixture tests. **The `ApplyCommand` variant rows are registered by the engine lane, not P1.** Engine spec Phase 2 adds "the engine's catalog rows on API P1's chain", and UI §18.1 puts "E-12's engine rows" in WF P4, with UI §7 saying "their catalog rows must land with WF-P4". The engine lane should settle whether that's its P2 or P4.
- **G12 — Header edge cases:** an unknown `X-Sindri-Client` value counts as no label and is logged. A malformed `X-Sindri-Repo` is `SND-API-007`. P1 only parses `X-Sindri-Repo` into the context; P3 enforces the scope.
- **G13 — Audit coverage:** `SND-AUTH-001`, `SND-API-007` and `SND-API-008` are refused before the chain, so they're logged, not audited, like binding failures (§2.1).
- **G14 — `sequence` when nothing is appended:** for an approve preview, an already-approved hash, or a stop without a lease, the value is null (`long?`).
- **G15 — The approve preview is a `protected` POST:** sessions and the app can no longer see it through `POST /profile/approve` (they get `SND-AUTH-002`). P2's `GET /profile` serves the read, so the AOT smoke's session preview now prints `SND-AUTH-002`.
- **G16 — `local` rows (`doctor`, `profile validate`):** UI status `cli-only:local`, and they're exempt from "cli-only only on terminal-bound rows".
- **G17 — Rule records:** `check-rules.mjs` accepts `archunit` checks only "under `tests/Aw.Architecture.Tests`", and the inventory's `section` must be a readiness-spec heading. The CQRS tests need Host internals, which only `Aw.Sindri.Tests` sees (csharp.md: Host declares `InternalsVisibleTo` "for `Aw.<Pkg>.Tests` only"). Default: the tests live in `tests/Aw.Sindri.Tests/Cqrs/`, the records use `type: test` with `section: "4b.2"` and an ADR source pointing at API §2.5, and `rule-records.md` names this third source. This departs from P1's file list (`tests/Aw.Architecture.Tests/Cqrs*`).
- **G18 — Golden stability:** E-17 says "every field is stable", but `AddProblemDetails` adds a random `traceId` to every problem body. Default: the golden normalization replaces `traceId`; everything else is fixed by `FakeTimeProvider`, `SequentialIdGenerator` and `FakeProcessProbe`.
- **G19 — Numbering:** UI §13 "X26" (goldens) and README "X26" (nonce and challenge carve-outs) are different rows. The brief's "X26" for the `issue` kind means README X26. Worth renumbering the UI row.
- **G20 — Spec naming:** the slice's response is `ListCommandsResponse(Rows)`, the generator's name, where §3.1 says `CommandList(rows)`. The shape is the same.
- **G21 — The `Origin` refusal** covers every POST route (commands and the later `issue` routes), through endpoint metadata plus a middleware that runs before binding. C3 says "an endpoint filter", but a filter runs after binding, so a malformed body would answer 400 instead of 403.

---

## 7. Risks

- **NativeAOT:** decorators are closed generics built in factory lambdas, so they're AOT-safe. Don't use open-generic registrations (`typeof(IValidator<>)`) or `MakeGenericType` outside tests. Reflection (assembly scans for ARCH-CQRS-003 and endpoint metadata) runs only in tests. The catalog holds `Type` values (`typeof`, safe). Enums use `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` like `StopReason`. Endpoint metadata and middleware work with RDG. `CancellationTokenSource(TimeSpan, TimeProvider)` and `AsyncLocal` are safe. Use `[LibraryImport("libproc")]`, not `DllImport` marshalling, for the macOS ancestry. ListCommands rows go through the source-generated context. The AOT smoke catches any IL2xxx/IL3xxx; run it before E records `expected.txt`.
- **Analyzers:**
  - AW1009 (3 type parameters: G2) and AW1001 (typed ids: G3).
  - AW1007/AW1008 on decorators. `AuditingService` has 5 dependencies only because `AuditTrail` bundles the lease, ledger and factory. `SchedulerHost` goes to 6, which is suggestion-level.
  - CA1502 ≤ 10: write the authorization table as data.
  - MA0051 ≤ 40 lines: split the middlewares.
  - AW1005 ≤ 300 lines: split catalog rows into `Host/Catalog/*` static classes, one type per file (MA0048).
  - MA0182: unused internal stubs and validators, removed once registered.
  - CA1040: empty marker interfaces. It's off by default, but if it fires, add a justified suppression on the three markers.
  - BannedSymbols in Core: no `Environment`, `Stopwatch` or `Guid.NewGuid`. Token randomness lives in Infrastructure; the accessor takes `IIdGenerator`.
  - RS0016/RS0017 churn on four Features records; CA2007 in library code; IDE0005.
- **Lane budgets:** about 35 new integration tests plus about 20 goldens, each host taking around 0.3–1 s. Share a host per class (`IClassFixture`), use one real-SQLite temp home per class, and keep the Ctrl-C test bounded (30 s deadline as in `ServeTests`). Check `node scripts/ci/lane-budgets.mjs` after a Release build. The unit lane has the 72-case authorization Theory, which is pure and cheap.
- **S1 test churn:** once A and B land, every S1 integration test that POSTs without headers gets 403, and refusals without a held lease become `SND-LEDGER-002` (503), because the audit needs a grant. A owns those updates through `Callers.AsHuman` and a held lease.
- **Hook contract: unchanged.** The only hook that calls `sindri` is `config/hooks/sindri-nudge.sh`, which runs the TS `sindri repo status --nudge` on `PATH`. The C# binary isn't installed, and P1 changes no file, field, exit code or stderr prefix that hook reads. Note for the switch: inside SessionStart the C# CLI would class itself as `session`, which is fine for a read.
- **Ctrl-C vs the gate:** a SIGINT stop waits up to 5 s for the gate. If an HTTP command holds it past that, the stop fails with `SND-API-009`, `process.stopped` isn't recorded, and the lease is freed on restart by the dead-holder rule. Document it; the test covers the normal path.
- **`script` portability:** BSD and util-linux take different arguments, the output has CRLF, and piped stdin into `script` must be checked on macOS first (E's first task). CI runs Linux (currently off), so `with-tty.sh` handles both.
- **`dotnet run` as `SINDRI`:** it rebuilds on each call and sits between the shell and the process, so `kill -INT $!` signals dotnet, not sindri. Use a built binary for proofs, as §5 does.
- **Heavy-lock budget:** two slots are shared by five lanes. Units build and test only `tests/Aw.Sindri.Tests` under a slot. The full-solution gate, the Release lane-budget run and the AOT publish happen once each, at integration. Don't run units' builds while the AOT smoke runs.
- **External probes:** Codex probing needs a logged-in `codex exec` and costs tokens. `cursor-agent` isn't installed, so it's an owner step, recorded as "unverified" in `docs/csharp/sindri.md` (allowed by §13 pass-3 LOW).

## 8. Lead decisions (2026-10-11)
- G1: allowlist GetStatusService + StreamEventsService in ARCH-CQRS-002, comment "removed in P2 (C9)"; P2 deletes it.
- G7: P1 registers `pending:<phase>` rows for every API action of P2–P10a (no command type bound); each phase flips its rows. Engine and context rows stay with their lanes.
- G6: `serve` inside an agent session is refused unless AW_SINDRI_PROVER=1.
- G4: proof folder = realpath under `<AW_STATE_DIR>/<repo-slug>/proof/`; the server sees the prover only as `X-Sindri-Client: prover`, refused (SND-AUTH-001) outside $TMPDIR or a proof folder.
- G2, G3, G5, G6', G8–G21: take the stated defaults. G11: P1 lands the E-12 mechanism only; engine lane registers ApplyCommand rows.
- G11 confirmed by engine lane via orchestrator (2026-10-11).

## Deviations (contract unit, 2026-10-11)
- **Behavior name:** the red test is `ListCommands_lists_every_action_id_with_its_class` (the lead's brief), not §2.3's `lists_every_catalog_row_with_its_class`. Unit C keeps that name.
- **`CallerContext.Http` takes 5 parameters, not 6 (AW1006):** `Http(CallerClass, bool terminal, ClientLabel, RepoName?, IIdGenerator ids)` mints the `CommandId` and `CorrelationId` itself; `Service(string name, IIdGenerator ids)` likewise. A test that needs fixed ids uses `with { CommandId = … }`. Actor map: human `cli|ui|prover`, else `http`; session `cli|mcp`, else `http`; a `Service` class over HTTP is read as a session.
- **`CommandCatalog` lives in `Host/Catalog/CommandCatalog.cs` (namespace `Aw.Sindri.Host.Catalog`)**, not `Host/CommandCatalog.cs`: in the Host root namespace, `Registration → Host` plus `Host (Composition) → Registration` is an ARCH-CYCLE-001 cycle.
- **`CliForm.Parse` also accepts a bare fixed flag (` --[a-z][a-z-]*`)** beside §2.3's pattern, so variant rows can show their flag (`sindri item close <itemId> --cancelled`, `sindri open <itemId> --attach [--agent <agent>]`). Still checked parts only (E-16); unit C hardens control/bidi characters and adds `Fill`.
- **Pending rows (G7), provisional fields:** spec quotes are dropped from CLI forms (`sindri item add <title> [--repo <repo>]`); `ProbeLaunchTargets` has no route or CLI in either spec, so it carries `POST /sindri/launch/targets/probe` and `sindri launch probe` until P10a decides. `ReadsViews` is set on CloseItem, FileRequest, Decide, CancelJob and OpenItem (E-1); `CloseItem:cancelled` is destructive (E-8); no row has a `shortcut`. Each phase corrects its own rows when it flips them.
- **`RepoName` rule:** the profile has no repo-name rule, so `RepoName` is `^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$` (no separator, space or control character).
- **`SindriHeaders`** carries a justified `MA0182` suppression until B and D use it. `SindriApi` sits in `Core/Commands/` (namespace `Aw.Sindri.Core.Commands`).
- **Query validators:** each query line registers `AcceptAllValidator<T>` too (`AddQuery` wraps `ValidatingService`, which needs one).
- **`new-slice --slice-only`:** it worked first time (8 files, exactly one red test, the full checks green). One inaccuracy in its printed hints: for `EndpointTable` it says "on the `_ = app.MapGroup(…)` chain: `.MapListCommands()`", where Sindri's table needs `_ = queries.MapListCommands();` under the queries anchor. Its registration hint prints the plain `ValidatingService` block (with the reminder to use `AddQuery` instead).
