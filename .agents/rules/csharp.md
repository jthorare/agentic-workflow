---
description: C# build (in progress) — package shape, dependency rules, types, errors, DI, JSON, AOT scope, analyzers
globs:
  - "src/**"
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.slnx"
  - "global.json"
  - "Directory.Build.props"
  - "Directory.Build.targets"
  - "Directory.Packages.props"
  - "nuget.config"
  - "BannedSymbols*.txt"
  - "deps.yaml"
  - "schemas/deps.schema.json"
  - "**/.editorconfig"
  - "CodeMetricsConfig.txt"
  - "**/PublicAPI.*.txt"
  - "**/packages.lock.json"
paths:
  - "src/**"
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.slnx"
  - "global.json"
  - "Directory.Build.props"
  - "Directory.Build.targets"
  - "Directory.Packages.props"
  - "nuget.config"
  - "BannedSymbols*.txt"
  - "deps.yaml"
  - "schemas/deps.schema.json"
  - "**/.editorconfig"
  - "CodeMetricsConfig.txt"
  - "**/PublicAPI.*.txt"
  - "**/packages.lock.json"
alwaysApply: false
---

# C# Rules

The toolkit is C# on .NET 10 (spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md`, §4a–§4e; the spec's §4b is the source of truth for every number here; the one-way switch is `docs/superpowers/specs/2026-10-23-cutover-design.md`). The tool ports ended at `Judge` and `Evidence` (owner re-plan 2026-10-11, factory addendum §17.29, decision Q7): `scorer` was not ported, and Sindri was **built new** as a C# application, not ported ("Porting a TS package" below). **Clean cutover, and a reimplementation, not a translation:** each package was rebuilt as idiomatic C# with the TS package as a guideline for *what* it does, never for *how* ("Porting a TS package" below). The C# build keeps no backward compatibility with the TS build (no byte-compatible JSON, no golden replay against TS output, no migration of TS state); its contracts are defined by its own tests. The one exception is the **hook contract** (spec §4c; the `deps.yaml` Hooks and Statusline edges): only what the bash hooks and statusline that keep running read — the commands and arguments they call, the JSON fields they `jq`, the exit codes and stderr prefixes they branch on, and the files they write, such as `judge/sessions`, `judge/outbox` and `judge/teammate-names.jsonl`. It stays frozen, checked in CI by running the real hook scripts against the C# binaries; the rest of each command is the C# package's own design. The installed toolkit is C#: `scripts/install-cs.sh` (run by `setup.sh`) publishes `sindri`, `sindri-ui`, `judge`, `aw-packets`, `bugfix-state`, `ui-evidence` and `aw-readiness` and links them into `~/.local/bin`; `./setup.sh` also stands the pinned Onyx up by running `sindri onyx up` (`scripts/install-onyx.sh`; opt out with `--no-onyx`; owner, 2026-10-18; spec `2026-10-16-sindri-onyx-managed-design.md` P5). `aw-readiness`, `aw-packets` and `aw-meta` write only under `readiness/`, `packets/` and `meta/`. `aw-meta` (the `/meta` harness, `src/Meta`) keeps its state under `meta/` and appends to its team's running log (its gate and merge commands also act on the team's checkout and PRs), and is opt-in (`./setup.sh --with-meta` → `scripts/install-meta.sh`; owner, 2026-10-14). Sindri absorbs it later (`docs/csharp/meta.md`). There are no per-package shims.

Each rule names the check that enforces it. "Gap — R1b" means the check doesn't exist yet: follow the rule by hand and don't add new violations.

## Package shape (§4b.1)

```
Host ──► Features ──► Core ◄── Infrastructure
```

| Project | Holds | May reference |
|---|---|---|
| `Aw.<Pkg>.Core` | Domain types, typed ids, value objects, pure logic, **port interfaces**, errors | `Aw.Kernel` (+ `Microsoft.Extensions.DependencyInjection.Abstractions` for its `Add…()` method, and Vogen for its typed ids) |
| `Aw.<Pkg>.Features` | One folder per use case (slice): request/response records, service, validator | `Core`, `Aw.Kernel`, other packages' `Core` |
| `Aw.<Pkg>.Infrastructure` | Repositories over SQL, adapters (git, containers, model CLIs, HTTP), migrations, its own settings (internal, read from the environment `Composition` passes) | `Core`, `Aw.Kernel`, `Aw.Kernel.Data`, and other tier-0 packages (e.g. `Redaction`) through a declared `deps.yaml` edge |
| `Aw.<Pkg>.Host` | Composition root: minimal API app or CLI `Main`, the endpoints and CLI commands (one per slice), `Compose` (which passes the environment to `Add…Infrastructure`), the JSON context | everything in the package, plus `Aw.Kernel.Telemetry` and the tier-0 and lower-tier `Core`s it composes |

A package under ~1,500 lines may use two projects, `Core` and `App`, with the same direction; split into four past the threshold.

| Rule | Check |
|---|---|
| Dependency direction above | Project references (the build fails) + ArchUnitNET in `tests/Aw.Architecture.Tests`: `ARCH-DIR-001` (Core depends on nothing else in its package), `ARCH-DIR-002` (Features: Core and Features only), `ARCH-DIR-003` (Infrastructure: Core and Infrastructure only), `ARCH-DIR-004` (another package only through its Core; the one carve-out is a Host's use of a `compose` target, which `ARCH-COMPOSE-001` judges instead: only the target's public static `Add…()` registration methods in its Features and Infrastructure), `ARCH-DIR-005` (Core and Features use `Aw.Kernel` and no other tier-0 package; the tier-0 list is checked against `deps.yaml`); `ARCH-PLACE-001` (every type of a Core, Features or Infrastructure project sits in that project's layer namespace: not the bare root, an unknown folder, another layer's namespace or one outside the root; in a Host or App project anything outside the root counts as Host) |
| `Core` has no I/O: no `System.IO`, `System.Net`, `Microsoft.Data.*`, `System.Diagnostics.Process`, `Console`, `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow`, `Guid.NewGuid` — use `TimeProvider` and `IIdGenerator` ports | `BannedSymbols.txt` → `RS0030` (BannedApiAnalyzers, wired for `*.Core` in `Directory.Build.props`; another project opts in with `AwBannedSymbols=true`, as `Aw.Kernel` does) |
| A slice never references another slice's types; the second use moves the logic into `Core` | ArchUnitNET `ARCH-SLICE-001` (both shapes); `Aw.Analyzers` fast check `AW1003` (a type, attributes included, named from another `Features.<Slice>` namespace of the same assembly) |
| `Infrastructure` row/DTO types are `internal`; repositories return `Core` types | ArchUnitNET `ARCH-INFRA-001` (only the `*ServiceCollectionExtensions` registration class is public in Infrastructure); `ARCH-DIR-001` keeps Infrastructure types out of Core signatures |
| External technology sits behind ports (Seams below): no technology package or type (`Microsoft.Data.*`, `Aw.Kernel.Data`, `SQLitePCLRaw`, `Npgsql`, cache and queue clients, `System.Net.Http`, `System.Diagnostics.Process`, file I/O) in `Core` or `Features`; `Host` uses `Infrastructure` types only in `Composition`. In the small shape the rule applies by namespace: `Aw.<Pkg>.App.Features.*` and `Commands` use no technology type, and only `Composition` uses `Aw.<Pkg>.App.Infrastructure.*` | `Core`: `BannedSymbols.txt` → `RS0030` bans the BCL's I/O (`N:System.IO`, `N:System.Net` (so `System.Net.Http`), `N:Microsoft.Data`, `T:System.Diagnostics.Process`, `T:System.Console`, `T:System.Environment` whole) plus the clock and random-id APIs; `Aw.Kernel.Data`, `SQLitePCL`, `Npgsql` and cache or queue client packages aren't in that list. ArchUnitNET covers all of them by namespace in both shapes: `ARCH-PORT-001` (no technology type in `Core`, `Features` or the small shape's `App.Features` and `App.Commands`; the list is `tests/Aw.Architecture.Tests/Rules/Technology.cs`; in `Features` and `App.Commands` it also counts every `System.Environment` member that reads the host, all but pure ones such as `NewLine` and `ProcessorCount`; Core already bans the whole type) and `ARCH-PORT-002` (only `Composition` uses `Infrastructure`: the rest of `Host` and the small shape's `App` and `App.Commands` don't). ArchUnitNET doesn't see the body of a top-level-statement `Program` (`<Main>$`), so `Program` stays the scaffold's two calls (`Composition.Compose`, the command table) and review checks it |
| Packages reference other packages only through their `Core` public surface (the reference sits in `Features` (`App` in the small shape) or `Host`) or a CLI/HTTP contract | `node scripts/check-deps.mjs` against `deps.yaml` |
| No namespace cycles inside a project | ArchUnitNET `ARCH-CYCLE-001` (namespace graph per assembly) |
| No `Common`, `Shared`, `Utils`, `Helpers` project or namespace | ArchUnitNET `ARCH-NAME-001` (every registered namespace, every project in `AgenticWorkflow.slnx`) |

### The UI package (spec §4a "UI")

Sindri's UI is an [Avalonia UI](https://avaloniaui.net/) app, its own package `SindriUi` (`src/Sindri/Aw.Sindri.Ui*`), not a layer of `Sindri`:

- `Aw.Sindri.Ui`: views, view models, the typed client of Sindri's HTTP API, and ports for platform services (notifications, tray, opening apps, passkeys). `Aw.Sindri.Ui.Desktop` (later `.iOS`, `.Android`): a thin head with the `Main`, composition and one adapter per port. A head holds no views or view models.
- It references no `Sindri` project. It reaches Sindri only through the HTTP contract and two files under `AW_SINDRI_HOME`: the endpoint record it finds the API by (`control.json`) and the per-start human token it sends (`auth/human.token`) (declared `http` and `file` edges; check: `node scripts/check-deps.mjs` for the edges, `ARCH-UI-001` for Sindri types), and holds no logic of its own (factory addendum §17.27.1): every check and state change happens in Sindri's handlers.
- Compiled bindings only: `AvaloniaUseCompiledBindingsByDefault=true`, `x:DataType` on every view and template, no `ReflectionBinding`. View models carry no Avalonia types, so they test in the `unit` lane; views run headless (`Avalonia.Headless`) in the `integration` lane. Check: ArchUnitNET `ARCH-UI-001` (no UI type uses a Sindri type; a view model, which lives under `Aw.Sindri.Ui.ViewModels`, uses no `Avalonia.*` type; a head has no view or view model); compiled bindings: the build default, the NativeAOT publish (`src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh`) and review. The UI projects are declared unshaped in `tests/Aw.Architecture.Tests/Packages.cs`, so the layer rules skip them and only the global ones apply.
- MIT-licensed Avalonia packages only; no Avalonia XPF or paid add-ons.

## Dependency graph (§4b.1a, §4e)

- Tiers (`deps.yaml` is the tier table of record): 0 `Aw.Kernel` (+ `Data`, `Telemetry`, `Testing`), `Redaction` and `Analyzers` · 1 `Judge`, `Packets`, `Readiness`, `Context` · 2 `Scorer`, `Evidence`, `BugFix` · 3 `Sindri` · 4 `SindriUi` (the Avalonia app; no project edge to Sindri, only tier-0 ones; Sindri's HTTP API, its endpoint record `control.json` and its human token `auth/human.token`). **Project** edges point strictly down the tiers. Read-only **`db`** and **`file`** edges may point down or across, never up (e.g. Scorer reads Evidence's run files across tier 2). **`cli`** and **`http`** edges may point anywhere (a CLI or an HTTP API is a contract). Every edge is declared in `deps.yaml`. Tier 0 is the only tier with project references inside it, ordered by sublevel (0: `Aw.Kernel`, `Redaction`; 1: `Data`, `Telemetry`; 2: `Testing`); a tier-0 project references only a lower sublevel.
- Every edge is declared in `deps.yaml` (schema `schemas/deps.schema.json`) with its kind (`project`, `compose`, `cli`, `db`, `file`, `http`). An `http` edge calls another package's HTTP API, which that package declares as `routes` (route prefixes such as `/sindri/`); like `cli` it may point anywhere. Adding an edge is a reviewed change to `deps.yaml`. Check: `node scripts/check-deps.mjs`, which compares the declared edges with the project references, with the cli, db and file edges it scans in the TypeScript and shell source, and with the http, file and db edges it scans in the C# projects (a string literal starting with another package's route prefix, or every segment of another package's state path as literals in one file; `DEPS-KIND-004` for an http edge to a package with no routes).
- **Compose** (context spec §4): a `compose` edge lets a package's `Host` reference a lower-tier package's `Features` and `Infrastructure` projects to call their public static `Add…()` registration methods, and nothing else from them; everything else goes through the target's Core (Sindri composes `Context` this way, using the `IContextEngine` facade in `Aw.Context.Core`). It points down the tiers like a project edge and sits beside a project edge to the same package, through which the Host and Features reach the target's Core. Checks: `node scripts/check-deps.mjs` (`DEPS-COMPOSE-001`: a compose edge without its project edge or into a tier-0 package, or a Host reference into another package's Features or Infrastructure without a compose edge; any other project doing that is still `DEPS-PROJ-007`. The Host's compose references make the compose edge current; the project edge is current only through a Core reference) and `ARCH-COMPOSE-001` over the compiled Host.
- Fan-out: ≤ 4 in-repo project references (`limits.projectFanOut`) and ≤ 6 third-party packages (`limits.thirdPartyFanOut`) per project. `*.Host`, `*.Tests` and `*.Testing` projects are exempt; analyzers are excluded (packages with `PrivateAssets=all`, and project references with `OutputItemType=Analyzer`). Over the limit needs a recorded exception with a reason in `deps.yaml`. Check: `node scripts/check-deps.mjs` (`DEPS-FANOUT-002`/`003`). There is no cap on how many packages one package declares project edges to: the owner removed the per-package limit on 2026-10-12.
- **Test support:** `*.Testing` projects are test-support, like packages marked `test: true` in `deps.yaml` (e.g. `KernelTesting`). They may reference test-support packages without a declared edge, and only `*.Tests` and `*.Testing` projects may reference them. Production code never references test support. Check: `node scripts/check-deps.mjs`.
- **Analyzers:** `Aw.Analyzers` is declared as the `Analyzers` node in `deps.yaml`. It's referenced only with `OutputItemType="Analyzer"` (and `ReferenceOutputAssembly=false`), in a `.csproj` or a `Directory.Build` file, normally `Directory.Build.targets`. Such analyzer references are excluded from fan-out and from `DEPS-PROJ-001`; a runtime reference in a `Directory.Build` file fails `DEPS-PROJ-005`, and an analyzer reference without `OutputItemType="Analyzer"` fails `DEPS-PROJ-006`. Check: `node scripts/check-deps.mjs`. `Directory.Build.targets` wires `Aw.Analyzers` this way into every production project (not test code under `tests/`, not the analyzer itself); its rules are `AW1001`–`AW1009`, severities in the root `.editorconfig`, each message naming the measured value, the limit and the fix.

## Request flow (§4b.2)

- **Endpoints and CLI commands** live in `Host` (`Host/Endpoints/`, `Host/Commands/`). That keeps `Features` free of ASP.NET and of the trim/AOT warnings (`IL2026`, `IL3050`) that minimal-API lambdas raise outside the request-delegate generator, which only runs in the `Host` project. They bind input, call **one** service (resolved as `IService<TReq, TRes>`), and map the `Result` to `TypedResults`/`ProblemDetails` or to an exit code. No branching on domain state. Group with `MapGroup`; use `WebApplication.CreateSlimBuilder`. Check: `AW1004` for endpoints (`Aw.Analyzers`: a lambda passed to an `EndpointRouteBuilderExtensions.Map…` call makes exactly one `IService<,>.Handle` call; method-group handlers aren't checked); CLI commands: review (gap).
- **Services** implement `IService<TRequest, TResponse>`, one use case each, no "manager" services. > 5 constructor dependencies is a split signal, > 7 is an error. Check: `AW1008` (6–7, the split signal, at `suggestion`: advice, not a gate) and `AW1007` (over 7, error), on every declared constructor of a class (records are data and aren't checked).
- **Cross-cutting** (validation, logging, transactions, budgets) is a `Decorated<TReq, TRes>` decorator registered explicitly. **No MediatR, no AutoMapper** (commercial licences since 2025); mapping is hand-written. Check: not in `Directory.Packages.props` (review).
- **Repositories:** one interface per aggregate in `Core`, named by intent (`Get`, `SaveStarted`, `ListStartable`). `IReadRepository<TEntity, TId>` covers `Get`/`Exists`; no generic `IRepository<T>`, no `IQueryable`. Writes run in an `IUnitOfWork` (one database transaction; SQLite today).
- **SQL:** ADO.NET over `Microsoft.Data.Sqlite`, typed parameters, one `IRowMapper<T>` per row type; SQL in `const string` fields or embedded `.sql` files; numbered migration files, each tested on a copy of the previous schema. Data access is decided (R8): hand-written ADO.NET with `IRowMapper<T>`; no EF Core, no Dapper. SQLite is the bundled `e_sqlite3` that `Microsoft.Data.Sqlite` ships, as a second file next to each AOT tool that opens a database and listed in `binaries.lock`; never the host's library. Open connections through `Aw.Kernel.Data`'s `SqliteConnectionFactory`: read-write connections get WAL (checked; refused if not granted), `busy_timeout`, `foreign_keys=on` and `synchronous=NORMAL`; read-only ones (`OpenReadOnly`/`BeginRead`) get only `busy_timeout` and `query_only`, so they never rewrite a file's header (`docs/csharp/kernel-data.md`). The migration runner switches `foreign_keys` off around each migration and checks them before commit. Schemas are new C# schemas. **Keep the database replaceable** (the general port rule in Seams below): SQL, pragmas and `Sqlite*` types live only in `Infrastructure` (`App` in the small shape) and `Aw.Kernel.Data`, behind the `Aw.Kernel.Persistence` ports and the package's repository ports, so moving to another engine (e.g. PostgreSQL through Npgsql) is a new adapter plus migrations, not a change to `Core`, `Features` or `Host`. Inside an adapter, keep engine-specific SQL (pragmas, `VACUUM INTO`, `json_*`, `INSERT OR …`) in the migration files or named `const string` fields, not spread through query building.

## Types and errors (§4b.3)

- Every id and constrained primitive is a strongly typed value (`ItemId`, `RepoSlug`, `SizeClass`, `TokenBudget`). **Decided (R9):** Vogen generates validation and equality with its own STJ generation off, because source generators can't see each other's output (a Vogen converter is invisible to the STJ source generator, which then writes the id as `{}`; a Vogen-only parse method is invisible to the request-delegate generator). So:
  - every assembly that declares ids sets `[assembly: VogenDefaults(...)]` with all six arguments of `docs/csharp/typed-ids.md` step 2, verbatim (`Conversions.None`, the STJ factory and primitive equality `Omit`, both casts `CastOperator.None`, `TryFromGeneration.GenerateBoolMethod`); `TypedIdGuard.CheckDefaults` checks each one in every registered assembly that declares ids (`ARCH-ID-001`);
  - each id carries a user-code `[JsonConverter]`, e.g. `[JsonConverter(typeof(StringIdJsonConverter<ItemId>))]`, that reads with `TryFrom` and throws `JsonException` (a 400, never Vogen's exception);
  - `IParsable<T>` is declared in user code: through the Kernel's `ITypedId<TSelf, TValue>` (an id in that form goes behind an endpoint only once an RDG route-binding test passes for it) or a hand-written `TryParse`;
  - the Host maps Vogen's validation exception to a 400;
  - each id has a JSON round-trip test, and an architecture test checks every id follows these rules.
  `ITypedId` is in `Aw.Kernel.Ids`, the `StringIdJsonConverter`/`GuidIdJsonConverter`/`Int32IdJsonConverter` are in `Aw.Kernel.Json`, and the recipe is `docs/csharp/typed-ids.md`. No raw `string`/`Guid` ids in public `Core`/`Features` signatures. Check: `TypedIdGuard` in `tests/Aw.Architecture.Tests` runs over every registered `Aw.*` assembly (`ARCH-ID-001` defaults, `ARCH-ID-002` ids and id properties); the raw-id ban is `AW1001` (`Aw.Analyzers`: in a `*.Core` or `*.Features` assembly's public methods, constructors, operators and conversions, properties and indexers, fields, events, delegates and extension-block receivers, a `Guid` anywhere in the type (array elements and every type argument, so tuples and dictionaries too), or a `string` whose member is named as an id (`id`, `…Id`, `…Ids`), also inside arrays, nullables and one-argument generics; a typed id's own members are exempt; each slot (the return or a parameter) of an override or interface implementation is reported at its base when the base reports the same slot (public, in the same assembly, raw in its own declaration, with no `Guid` added by its type arguments), otherwise at the implementation; a partial member is reported once, at its defining part (a partial constructor at its implementing part); an extension block's receiver counts when the block has a public member).
- Expected failures are `Error` records with a stable catalogue code (`SND-…`, `AW-…`) returned in `Result<T>` from `Aw.Kernel`. Exceptions are for bugs and broken invariants only. An error may carry RFC 9457 extension members for an http host (`error.WithExtension("suggestedAnchors", value, MyContext.Default.AnchorList)`: camelCase names, not `type`/`title`/`status`/`detail`/`instance`/`traceId`, ordered, the value built with the slice's own source-generated type info, so no reflection); they are never part of the record's own JSON, a CLI ignores them, and a slice that uses one lists it in its code's row of the package doc. No `catch (Exception)` outside `Host` and `Infrastructure` boundary adapters. Check: `CA1031` at error in the root `.editorconfig`; `Host` and `Infrastructure` projects lower it in their own project-level `.editorconfig` (`/new-package` generates it).
- Generic types take ≤ 3 type parameters and are constrained (`where TId : struct, IEquatable<TId>`). Check: `AW1009` for the count (types, delegates, methods and local functions; an override or interface implementation is reported at its base); the constraints are review.
- Nullable is on everywhere (`Nullable=enable` + `TreatWarningsAsErrors`). A `!` needs a `// why:` comment. Check: compiler for nullability; `AW1002` for the comment (on the `!`'s line, or in the comment lines directly above the statement or member that holds it, or between its attributes and the declaration, with no blank line between and a non-empty reason).
- `required` members and primary constructors preferred; records immutable by default.
- Classes are `internal sealed` by default (`CA1852`, `CA1515`). Only what crosses an assembly is public: the `Add…()` methods, request and response records, port interfaces and errors in `Core`. In the small shape nothing outside the package references `App`, so `App`'s types, its `Add…()` methods and its slice request and response records are `internal` (with `host: cli` `App` is an Exe, and `CA1515` rejects public types there); `App` declares `InternalsVisibleTo` for `Aw.<Pkg>.Tests` and `Aw.<Pkg>.Testing`, and its PublicAPI files stay at `#nullable enable` only. Every **library** project keeps `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` (hosts don't: `Directory.Build.targets` turns the public-API analyzers off for `*.Host`, whose only public type is the generated `Program`): both start with `#nullable enable` (`RS0037`), and every symbol line carries nullability annotations (`RS0036`), e.g. `static Aw.Demo.Core.DemoCoreServiceCollectionExtensions.AddDemoCore(this Microsoft.Extensions.DependencyInjection.IServiceCollection! services) -> Microsoft.Extensions.DependencyInjection.IServiceCollection!`. A record lists its compiler-generated members too (`<Clone>$`, `Deconstruct`, `Equals`, `GetHashCode`, `ToString`, `==`/`!=`, each `get`/`init`); take the lines from the build's `RS0016` diagnostics rather than writing them by hand (the code fix changes nothing on .NET 10; `/new-package` and `/new-slice` record them). Every public member has a `///` summary. Tests reach internals through `InternalsVisibleTo`: `Core`, `Features` and `Infrastructure` declare it for `Aw.<Pkg>.Tests` and `Aw.<Pkg>.Testing`; `Host` for `Aw.<Pkg>.Tests` only. In the small shape `App` declares it for both, and `Aw.<Pkg>.Testing` declares it for `Aw.<Pkg>.Tests` because its builders over `App`'s internal records are internal. Check: `RS0016`/`RS0017`/`RS0036`/`RS0037` (PublicApiAnalyzers), `CS1591`.
- `PublicAPI.Unshipped.txt` merges with git's `union` driver (`.gitattributes`): two branches that add symbols at the same spot keep both sides' lines instead of conflicting, and the analyzers don't care about line order. When both sides change the *same* symbol (a record that gains a different constructor parameter on each branch), union keeps both old lines, so the merge is clean but the build reports `RS0016`/`RS0017`: replace them with the one line the merged code has.

## JSON (§4b.3)

- System.Text.Json with **one source-generated `JsonSerializerContext` per package**. One exception: an `Infrastructure` project may keep one `internal` context of its own for third-party wire formats whose records are `internal` (the host's context can't name them), used only through `AwJson.CreateOptions` (e.g. Judge's `ProviderWireJsonContext`). No reflection-based serialization. Check: trim/AOT analyzers (`IL2026`, `IL3050`) via `IsAotCompatible=true`.
- Use the shared **`AwJson` profile** from `Aw.Kernel.Json`: `AwJson.CreateOptions(<Name>JsonContext.Default)` once per host, then `AwJson.Serialize`/`Deserialize` with those options (camelCase, relaxed escaping, UTC `O` timestamps, `RespectNullableAnnotations`, `RespectRequiredConstructorParameters`). Never construct `JsonSerializerOptions` yourself or serialize through a context's `.Default` options directly. HTTP hosts register the same resolver and options in `ConfigureHttpJsonOptions`.

## Logging and telemetry (§4b.1, §4d)

- Hosts start `Aw.Kernel.Telemetry` once per process, in `Compose`: `services.AddKernelTelemetry(TelemetryOptions.FromEnvironment("<service>", environment, clock))` (level `AW_LOG_LEVEL`; an unknown level logs one `AW-TELEMETRY-001` warning and stays at information). `Program.cs` resolves `TelemetryRuntime` right after building the container, so the container owns it and flushes it on exit (an http host does it in `LoopbackBinding.Build`, which `Program.cs` and its tests share). Log lines are JSON (one object per line) on standard error by default; **exporting is off by default**, and an exporter is an adapter the host opts into. Recipe: `docs/csharp/telemetry.md`.
- Log through `[LoggerMessage]` source-generated methods (`CA1848`). Put structured values in fields, never secrets: `Aw.Redaction` scrubs every log line, span and metric before it leaves the process, withholds secret-named fields and tags whole, and drops what it can't rewrite (span events and links, metric tags). Check: the scrubbing tests in `tests/Aw.Kernel.Telemetry.Tests`.
- Use `ActivitySource`/`Meter` names under `Aw.` (the default listen pattern `Aw.*`). Metric tags are small fixed sets.

## Dependency injection (§4b.4)

- `Microsoft.Extensions.DependencyInjection`. **Each project owns one explicit, public extension method** that registers its own types (`services.AddSindriCore()`, `AddSindriFeatures()`, `AddSindriInfrastructure(environment)`); **only `Host` calls them and builds the container.** No assembly scanning (reflection, not AOT-safe).
- So `Core`, `Features` and `Infrastructure` may reference `Microsoft.Extensions.DependencyInjection.Abstractions` (for `Core`, alongside Vogen for its typed ids, its only packages besides `Aw.Kernel`). `Aw.Kernel` stays BCL-only: no `IConfiguration` in Kernel signatures. `Host` never builds an `Infrastructure` settings type: `Composition` passes the environment (a `Func<string, string?>`, `Environment.GetEnvironmentVariable` in `Program.cs`, a lambda in tests; never `IConfiguration`: `Aw.Kernel.Telemetry`'s OpenTelemetry closure brings `Microsoft.Extensions.Configuration` into every host, and nothing may use it) to `Add<Name>Infrastructure(environment)`, and `Infrastructure` reads its own settings (an `internal` `<Name>Settings` record: the state directory, a connection string). A technology swap changes `Infrastructure` and the one `Add…()` call in `Composition`, never the rest of `Host`.
- Decorators are registered in the **owning project's `Add…()` method** (outermost first), never in `Host`: `Compose` only calls the projects' `Add…()` methods (choosing one where `Infrastructure` offers several, Seams below), registers the clock, and passes the environment to the `Infrastructure` `Add…()` it picks. It may also register the package's AwJson options (`AwJson.CreateOptions(<Name>JsonContext.Default)`) as a singleton, for an `Infrastructure` adapter that persists JSON: the adapter takes `JsonSerializerOptions` and never references the host's context (which `ARCH-DIR-003` refuses).
- Each `Host` exposes one `internal static IServiceCollection Compose(IServiceCollection, Func<string, string?> environment, TimeProvider)` that `Program.cs` and the DI validation test both call, so the test covers the real composition.
- Constructor injection only; no service locator; no `IServiceProvider` outside `Host`, with **one exception**: the `IServiceProvider` parameter of a registration factory lambda inside a project's own `Add…()` method. M.E.DI has no decorator support, so that's how a decorator wraps its service:

  ```csharp
  services.AddScoped<StartItemService>();
  services.AddScoped<IValidator<StartItemRequest>, StartItemValidator>();
  services.AddScoped<IService<StartItemRequest, StartItemResponse>>(static sp =>
      new ValidatingService<StartItemRequest, StartItemResponse>(
          sp.GetRequiredService<StartItemService>(),
          sp.GetRequiredService<IValidator<StartItemRequest>>()));
  ```

  The lambda only resolves and constructs; it holds no logic. Check: ArchUnitNET `ARCH-DI-001`. It flags the locator types (`IServiceProvider`, `IKeyedServiceProvider`, `IServiceScopeFactory`, `IServiceScope`, `AsyncServiceScope`, `ActivatorUtilities`, `IServiceProviderIsService`, and the extension classes behind `GetService<T>`/`GetRequiredService<T>`) anywhere except `Host` (with `Program`; in the small shape the `App` root namespace and `App.Commands`) and `*ServiceCollectionExtensions` classes. It can't tell the factory lambda from another member of that class, so keep those classes to registrations. Avalonia's XAML compiler passes a markup `IServiceProvider` through code it generates (the `CompiledAvaloniaXaml` namespace, nested `XamlClosure_N` types, `!XamlIl…` members); that code is exempt, hand-written code in the same class is not.
- Lifetimes: services and repositories scoped per request/command; pure domain services singletons; `TimeProvider` singleton (`FakeTimeProvider` in tests).
- Every host has a DI validation test over `Compose`: it builds with `ValidateOnBuild` + `ValidateScopes`, then **resolves every registered `IService<,>` inside a scope**, because `ValidateOnBuild` can't see what a factory lambda resolves (a missing validator would otherwise fail only at run time). Production `Program.cs` skips the validation flags (CLI startup time); the test carries them. Check: that test (per host).

## Build settings and analyzers (§4b.6)

- `global.json` pins the SDK. `Directory.Build.props` sets `Nullable`, `LangVersion=latest`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `EnableNETAnalyzers=false` (the pinned NetAnalyzers package owns the CA rules), `GenerateDocumentationFile`, NuGet audit (high and critical advisories, `NU1903`/`NU1904`, fail the build), and `RestorePackagesWithLockFile` (locked mode in CI). Severity lives in `.editorconfig` (the bulk `error` baseline plus explicit lines); **never add `AnalysisLevel`**: with the SDK analyzers off it is silently ignored. Don't override these per project, except through the two role switches below.
- **Project roles** (from the name, in `Directory.Build.props`): `*.Tests` and `*.Testing` are test projects, `*.Host` hosts, `*.Analyzers` analyzers; **every other project is a library** (`Core`, `Features`, `Infrastructure`, a small package's `App`, Kernel, `Redaction`) and gets `IsAotCompatible=true`. A library opts out only with `AwAotCompatible=false` and a comment saying why (e.g. `Evidence`: Playwright for .NET has no NativeAOT). A non-`Core` project opts into the Core banned-symbol list with `AwBannedSymbols=true` (every production project already gets `BannedSymbols.All.txt`).
- **Lock files:** CI fails on a missing `packages.lock.json` (`AW0001`, `Directory.Build.targets`) and on a lock file restore changed or a package that isn't signed (`scripts/dotnet-verify-packages.sh`: lock files present and unchanged, signature-verified locked restore, `dotnet nuget verify --all`).
- `Directory.Packages.props`: Central Package Management with versions pinned as **`[x.y.z]`** (a bare `x.y.z` is only a minimum). **New packages are at least 14 days old** on nuget.org. Analyzers come in as `GlobalPackageReference` and get the same review as runtime dependencies (they run code in every build).
- `nuget.config` maps every package to nuget.org only. Don't add sources.
- **One owner per rule:** where analyzer packs overlap (for example `CA2007` vs `MA0004`), `.editorconfig` names the owner and disables the other. Every rule agents must obey has an explicit severity — never `suggestion`.
- **Ratchet:** existing violations live in the `#### Ratchet baseline (spec §4b.6) ####` block at the end of the root `.editorconfig`, below the sentinel line `# ---- ratchet-baseline: generated entries only below this line ----` (one section per file, lowering only the named rule); that part may only shrink. Not a `baseline.globalconfig`: a global config can't lower a rule that `.editorconfig` sets (editorconfig wins), and global-config sections need absolute paths. The growth check (W2) diffs the entries below the sentinel; it is a gap until W2 lands.
- **Size limits:** file ≤ 300 lines (`AW1005`, counted like `wc -l`; generated files skipped); method ≤ 40 lines (`MA0051`, `MA0051.maximum_lines_per_method = 40` in `.editorconfig`); cyclomatic complexity ≤ 10 (`CA1502`, error in `.editorconfig`, limit `CA1502: 10` in `CodeMetricsConfig.txt`); parameters ≤ 5 (`AW1006`: methods, local functions, indexers, operators, delegates and struct constructors; overrides and interface implementations are checked at their base declaration, class constructors by `AW1007`/`AW1008`; record constructors (data shapes) and lambdas aren't checked).
- **Test projects:** The test policy applies **by location**: every `*.Tests` and `*.Testing` project lives under `tests/` (including `tests/Aw.Kernel.Testing/`), and #89's `tests/.editorconfig` (`root = false`) covers everything there; outside `tests/` it doesn't apply. It sets: `CA1707` (underscore test names), `RS0016`, `RS0017`, `RS0036`, `RS0037` (no public-API tracking), `CA2007` and `CA1515` (xUnit needs public test classes; xUnit1030 forbids `ConfigureAwait(false)` in tests) are off; `GenerateDocumentationFile` stays `true` (IDE0005 needs it) with `CS1591` off.
- **`ConfigureAwait`:** `CA2007` owns it (`MA0004` is off) and, per #89, applies to libraries (`dotnet_code_quality.CA2007.output_kind = DynamicallyLinkedLibrary`). Library code writes `.ConfigureAwait(false)` on every `await`, every `await using` (`await using (x.ConfigureAwait(false)) { … }`, including `CreateAsyncScope()`) and every `await foreach`. Host templates use the same forms so code moved into a library stays compliant.
- **No TODO comments** (`MA0026`): finish the work or open a work item.
- **File name matches the type** (`MA0048`): one top-level type per file.
- Fix a warning; don't suppress it. A `#pragma warning disable` or `[SuppressMessage]` needs a `Justification` and is reviewed like an override.

## Style-guide rules (§4b.3, §4b.6; research 2026-10-16)

One record each in `rules/engineering/csharp/`; `.editorconfig` holds the severities. All are `error`; a site that is correct and that the rule can't see is suppressed in place with a `#pragma` and its reason.

- **Async and lifetimes:** never dispose what a running task uses (`CA2025`); `await` the task inside `using`/`try` instead of returning it (`RCS1229`); dispose what you create and own and dispose disposable fields (`CA2000`, `CA2213`; tests are off); no `async void` except a UI event handler with a `// why:` (`MA0155`); give `StartNew` and `ContinueWith` a scheduler (`CA2008`). `BannedSymbols.All.txt` (every production project): `new TaskCompletionSource` passes `TaskCreationOptions.RunContinuationsAsynchronously`; no `Thread.Sleep`, `Task.Wait`, `Task<T>.Result` or `GetResult()`. Every task-returning method on a Core interface takes a `CancellationToken` (`ARCH-ASYNC-001`).
- **SQL:** command text is a constant or reviewed literal, never concatenated; values go in parameters (`CA2100`; tests are off).
- **Bug classes:** `RCS1210`, `RCS1202`, `RCS1256`, `RCS1234`, `RCS1257`, `RCS1096`, `RCS1252`; no dead conditions (`CA1508`); one enumeration of an `IEnumerable` (`CA1851`); no dead statics (`CA1823`, `CA1810`, `CA2207`, and `CA1802`).
- **API shape (Framework Design Guidelines):** `CA2227`, `CA1065`, `CA1066`, `CA1815`, `CA1052`, `CA2214`, `CA1033`, `CA1044`, `CA1813`, `CA1027`, and `CA1819`, `CA1034`, `CA1063`. `CA1062` stays off.
- **Banned symbols:** the Core list is `BannedSymbols.txt`; `BannedSymbols.All.txt` goes to every production project, `BannedSymbols.NonHost.txt` (`Environment.Exit`, `GC.Collect`) to every production project but a Host; `BannedSymbols.Tests.txt` to test projects (no wall clock, `Guid.NewGuid` or `Thread.Sleep`: take a `FakeTimeProvider`, or `Aw.Kernel.Testing.RealTime` where a test truly needs real time). All are wired in `Directory.Build.props` (a project named `*.Desktop` or `*.AotSmoke` counts as non-Host); a record's `banned-symbols` check names its file in `ref`.

## AOT scope (§4b.6)

| Entry point | Build |
|---|---|
| Hook binaries; `judge`, `bugfix-state` CLIs | NativeAOT |
| The Sindri host (`sindri serve`, the CLI and the Temporal worker in it) | Framework-dependent (owner, 2026-10-11, README X54): the Temporal .NET SDK has no trimming support (temporalio/sdk-dotnet#286). The adapter `Aw.Sindri.Workflows` sets `AwAotCompatible=false` with that reason; every other Sindri library keeps `IsAotCompatible=true` |
| `aw-readiness`, `aw-packets`, `aw-meta` (standalone CLIs before the switch, §4c) | NativeAOT |
| `Evidence` (ui-evidence) | Framework-dependent (Playwright for .NET has no NativeAOT) |
| `Aw.Sindri.Ui.Desktop` (the Avalonia app) | NativeAOT; compiled bindings only (`AvaloniaUseCompiledBindingsByDefault`, `x:DataType` on every view, no `ReflectionBinding`) |

Every library project (see Project roles above: everything except `*.Tests`, `*.Testing`, `*.Host`, `*.Analyzers`) gets `IsAotCompatible=true` from the root build files; `Evidence`'s libraries opt out with `AwAotCompatible=false` and the reason. `PublishAot=true` only on the entry points above (a Host, or a small package's `App`), in their own project files, and only under a runtime identifier: `<AwAotEntryPoint>true</AwAotEntryPoint>` plus `<PublishAot Condition="'$(RuntimeIdentifier)' != ''">true</PublishAot>` (what `/new-package` generates). An unconditional `PublishAot` makes a plain restore put the host's ILCompiler pack under a host-RID section of `packages.lock.json`, so the committed lock file would differ between macOS and Linux and CI's locked restore would refuse it; `AwAotEntryPoint` keeps `InvariantGlobalization` and the trim, AOT and single-file analyzers on in every build, JIT included (`Directory.Build.targets`; a Host isn't a library, so `IsAotCompatible` doesn't cover it). AOT publishing (`dotnet publish -r <rid>`, with the committed lock files set aside; e.g. `src/Readiness/aot-smoke/run.sh`) runs in its own CI job and in releases; local builds and tests are JIT.

## Seams (§4b.6, factory addendum §17.20–17.21)

The C# schemas carry the distribution and multiplayer seams from their first commit: idempotent events, the event envelope (`actor`, `tenant`, `ring`, `causation`, `correlation`, `trace`) and a transactional outbox (`outbox_commands`, `IOutbox` in `Aw.Kernel`). The other seams are **planned, not in `Aw.Kernel` yet**: `ILedgerStore` (with a shared contract suite), `IBus`, `ISecrets`, `ILease`, `ILauncher` and `IScheduler`, in `Aw.Kernel`/`Aw.Kernel.Telemetry` (`Aw.Kernel.Telemetry` exists: its own port is `ILogSink`, with `LogSinkContract` in `tests/Aw.Kernel.Telemetry.Testing`). Until a planned port lands, a package that needs it declares it in its own `Core`; it moves to Kernel when a second package needs it. `actor` is set by the host process, never by the caller.

**Every external technology sits behind a port, so it can be swapped.** Database, cache, queue or bus, file system,
processes, HTTP clients, git, containers, model CLIs, secrets, clock and ids: `Core` declares the port as an interface,
or uses a Kernel one that exists today (`IReadRepository`, `IUnitOfWork`/`IUnitOfWorkFactory`, `IOutbox`,
`IIdGenerator`, and the BCL's `TimeProvider`); a planned one (above) is declared in the package's `Core` until it lands.
`Core` and `Features` never reference a technology package or its types (`Microsoft.Data.*`, `Npgsql`, a Redis or
queue client, `System.Net.Http`, `System.IO` file access, `System.Diagnostics.Process`); they see only the port.
In the small shape this holds by namespace: `Aw.<Pkg>.App.Features.*` and `Commands` use no technology type, and only
`Composition` uses `Aw.<Pkg>.App.Infrastructure.*`. Check: `ARCH-PORT-001` and `ARCH-PORT-002` (`tests/Aw.Architecture.Tests`).

**Picking an adapter.** Each real implementation of a port is an `Infrastructure` adapter. A second technology for the
same port lives in the same `Infrastructure` project behind its own registration method, `Add<Name><Tech>Infrastructure()`
(e.g. `AddItemsSqliteInfrastructure(environment)` and `AddItemsPostgresInfrastructure(environment)`); its package comes in through a
`Directory.Packages.props` review. `Composition` in the `Host` picks the adapter by which `Add…()` it calls, and
`Infrastructure` reads that adapter's own settings from the environment `Composition` passes (DI above). `Aw.Kernel.Data`
is SQLite-specific today; a second engine needs its own data package, `Aw.Kernel.Data.<Engine>`, added when needed.

**Contracts.** Every port has a contract test suite that the in-memory fake and every real adapter pass, so a new
adapter (PostgreSQL instead of SQLite, a remote queue instead of the outbox table) is proven by the same tests. A Kernel
port's suite is an `Aw.Kernel.Testing` base: `ReadRepositoryContract`, `UnitOfWorkContract` and `OutboxContract`, which the
in-memory fakes (`InMemoryUnitOfWorkFactory`, `InMemoryOutbox`) and the `Aw.Kernel.Data` adapters (`SqliteReadRepository`,
`SqliteUnitOfWorkFactory`, `SqliteOutbox`) pass, and `IdGeneratorContract`, which the fake `SequentialIdGenerator` and the
judge's `RandomIdGenerator` pass (the fake is deterministic by design, so its binding sets `Deterministic`: two fresh
fakes repeat one sequence, where two fresh real generators share no id). A package's own port's suite lives in `Aw.<Pkg>.Testing` (spec §4b.5) and derives from a Kernel base
where one fits. BCL ports (`TimeProvider`, faked by `FakeTimeProvider`) are exempt.

Name ports for what the code needs (`IItemStore`, `IJobQueue`), not for the technology (`ISqliteItems`,
`IRedisCache`). Don't add a port for something with one obvious BCL implementation and no I/O (collections,
`JsonSerializer` through AwJson, `CancellationToken`).

## Porting a TS package (spec §4a step 1)

This applies to the tool ports (`Judge`, `Evidence`, and the finished ones). **Sindri is built, not ported** (factory addendum §17.29.4): it is new C# design, app shell → workflow engine → context retrieval → blocks → workflows, and the TS `sindri/` (with `scorer`) is only a reference for what a part does. The "Don't" column below holds for it too.

A port is a **reimplementation in idiomatic C#**. Read the TS package to learn what it does (its use cases, rules,
failure modes and the reasons behind them), then design the C# package the way this file says, as if it were new.
The TS code is a guideline, not a spec, and never a target to match.

| Don't | Do |
|---|---|
| Emulate JavaScript (`JSON.parse`/`JSON.stringify`, number text, key order, escaping) or match TS output byte for byte | Serialize with the Kernel `AwJson` profile and the package's source-generated context; let the format be whatever that writes |
| Walk `JsonDocument`/`JsonElement` field by field, or copy TS's per-field leniency (wrong types silently defaulted) | Deserialize input into typed records through `AwJson` (required and nullable are enforced by the profile); a bad document is one clear error |
| Keep a TS quirk because "TS did it": a status smuggled into another field, a silent exit, an unknown argument treated as a known one, three output envelopes for one concept | Model it properly (a column, a field, an error from the catalogue), with one consistent output and exit-code scheme documented in `docs/csharp/<package>.md` |
| Mirror the TS file layout, function by function | Use the §4b shape: slices in `Features`, pure decisions on `Core` types, technology behind ports |
| Write plans, briefs or tests that say "exactly as TS", "same output as TS" or compare with TS output | State the C# contract and test it; compare with TS only on the hook contract, in the real-hook check |
| Port TS state formats or digests so they "line up" | Design new schemas and digests (§4c: the C# build starts with fresh state) |

The **hook contract** is the only exception, and it is narrow: list exactly what the hooks read (the commands and
arguments, the `jq` fields, the exit codes and stderr prefixes, the files) in the package doc, keep those, and design
everything around them freely. The tier-2 callers of a package's CLI (ui-evidence, bugfix) are ported to C# too, so
they follow the C# contract, not the TS one.

## Adding code

- **New package:** `/new-package` (generator `scripts/scaffold/new-package.mjs`; contract `docs/csharp/scaffolds/new-package.md`).
- **New use case:** `/new-slice` (generator `scripts/scaffold/new-slice.mjs`; contract `docs/csharp/scaffolds/new-slice.md`).
- **By hand**, the slice recipe is: (1) the failing test plus request and response records and a parameterless service stub whose `Handle` throws `NotSupportedException`, committed red but compiling; (2) the real assertion and the ports the service needs; (3) the service, with pure decisions on the domain type in `Core`; (4) the endpoint or CLI command in `Host`; (5) the explicit registration and the JSON context entries.
- Test rules: `.agents/rules/csharp-testing.md`.
- Rule records: every rule in spec §4b, and the rules this file and `csharp-testing.md` add under its headings (listed in `docs/csharp/rule-records.md`), has a record in `rules/engineering/csharp/` naming its check and gap. A PR that adds a check id to this file or `csharp-testing.md` adds it to the rule's record; `node scripts/check-rules.mjs` fails otherwise (`RULE-COVER-002`).
