# `/new-slice` — scaffold spec

Status: **built** (R1b). This file is the contract; the generator is `scripts/scaffold/new-slice.mjs`, driven by the
`/new-slice` skill (`skills/new-slice/SKILL.md`), and `/new-package` calls it for the first slice. Rules it must satisfy: `.agents/rules/csharp.md`,
`.agents/rules/csharp-testing.md`, spec §4b.2 and §4b.5, and the root build settings from the .NET solution PR #89.

## Purpose

Add one use case (a slice) to an existing package, starting with a failing test that names the behavior. The recipe
it encodes: **test (with compiling stubs) → service → endpoint or CLI command → registration.**

## Inputs

| Input | Form | Rule |
|---|---|---|
| `package` | an existing package name, e.g. `Readiness` | `src/<package>/` exists and is in `deps.yaml` |
| `slice` | PascalCase verb-first use case, e.g. `CheckRepo`, `StartItem` | `^[A-Z][A-Za-z0-9]{2,40}$`; no existing `Features/<slice>/` folder |
| `behavior` | snake_case sentence for the first test, e.g. `reports_level_one_for_an_empty_repo` | becomes `<Slice>_<behavior>` |
| `surface` | `cli`, `http` or `none` | must match the package's host; `cli` adds a command, `http` adds an endpoint |
| `route` / `command` | e.g. `POST /items/{itemId}/start` or `check` | `route` is required for `http`. Each `{name}` segment names a typed id (from `ids` or already in `Core`) by its camelCase name (`{itemId}` → `ItemId`); `{id}` is accepted when the slice has exactly one id. The generator rewrites each segment to the id's camelCase name, because the request-delegate generator binds a route value to the lambda parameter of the same name. Otherwise `NS-ROUTE`: ask the user which id. `command` (cli, kebab-case) defaults to the slice name in kebab case (`CheckRepo` → `check-repo`) |
| `ids` | list of typed ids the request uses, each `Name:primitive`, e.g. `ItemId:string` (primitive is `string`, `Guid` or `int`; the name ends in `Id`) | an id already declared in `Core/Ids/` is reused (its primitive must match, else `NS-IDS`) |
| `ports` | list of `Core` port interfaces the service needs | a port with a file `<IPort>.cs` anywhere under `Core` (`Core/Ports/`, the root, a subfolder) is reused as it is, and its fake and contract base are left alone; any other port is new: the generator adds an empty interface in `Core/Ports/` plus its fake and contract base |

The implemented service always takes `TimeProvider`, so it counts toward the limit: **`ports` + 1 ≤ 5** constructor
dependencies (§4b.2 split signal). The red stub takes none; the limit is checked on the declared `ports`. More is refused with `NS-DEPS: <n> constructor dependencies (ports + TimeProvider); the limit is 5
— split the use case`. **Ask the user** for missing inputs.

Refusals (exit 1, nothing written): `NS-PACKAGE` (no scaffolded `src/<package>/` declared in `deps.yaml`), `NS-NAME`,
`NS-BEHAVIOR`, `NS-SURFACE`, `NS-EXISTS`, `NS-COMMAND` (malformed or already in the command table), `NS-ROUTE`,
`NS-IDS`, `NS-PORTS` (not an `I…` interface name, or a new port whose `InMemory<Port>.cs` or `<Port>Contract.cs`
already exists in `Aw.<Pkg>.Testing`), `NS-DEPS`, `NS-SLICE-ONLY` (`--slice-only` only, below).

```bash
node scripts/scaffold/new-slice.mjs --package Demo --slice StartItem --behavior parks_when_budget_is_exhausted \
  --surface http --route "POST /items/{itemId}/start" --ids ItemId:string,OwnerId:Guid --ports IItemStore
```

## Generated files

Paths are for `shape: full`; in a `small` package, `Features/` and the Host files live inside `Aw.<Pkg>.App/`, and the
request and response are `internal sealed record`s (`App` is an Exe under `host: cli`, where `CA1515` rejects public
types; tests reach them through `App`'s `InternalsVisibleTo`).

```
src/<Pkg>/Aw.<Pkg>.Features/<Slice>/
├── <Slice>Request.cs        # public sealed record <Slice>Request(<typed ids / value objects>), /// summary
├── <Slice>Response.cs       # public sealed record <Slice>Response(...), /// summary
├── <Slice>Service.cs        # internal sealed class, no constructor parameters, Handle throws NotSupportedException
└── <Slice>Validator.cs      # internal sealed class, IValidator<<Slice>Request>; accepts everything until a rule is added
src/<Pkg>/Aw.<Pkg>.Host/
├── Endpoints/<Slice>Endpoint.cs   # http: internal static, Map<Slice>(RouteGroupBuilder) — bind route ids + body, one service call, ToHttpResult()
├── Endpoints/<Slice>Body.cs       # http, when the method takes a body (POST, PUT, PATCH) and the request has fields the
│                                  # route doesn't carry: internal sealed record <Slice>Body(...) with ToRequest(<route ids>)
└── Commands/<Slice>Command.cs     # cli: internal static, binds args → one service call → exit code
tests/Aw.<Pkg>.Tests/<Slice>/
└── <Slice>Tests.cs          # the red test (below)
src/<Pkg>/Aw.<Pkg>.Core/Ids/      # one file per entry in `ids` (R9)
└── <Id>.cs                  # Vogen [ValueObject<prim>], [JsonConverter(typeof(<Conv><<Id>>))], ITypedId<<Id>, prim>, Validate
tests/Aw.<Pkg>.Tests/Ids/
├── <Id>Tests.cs             # JSON round-trip through AwJson; an invalid, missing or null value → AW-KERNEL-001 from AwJson.Deserialize; Parse/TryParse (Lane=unit)
├── <Id>Holder.cs            # public sealed record <Id>Holder(<Id> Id): lets the tests send a missing or null property
├── IdsJsonContext.cs        # the id tests' source-generated context; one [JsonSerializable] per holder
└── <Id>RouteBindingTests.cs # http, when a `{name}` in `route` uses the id: route/query binding, refused → 400, `?id=` → 400 (Lane=integration)
tests/Aw.<Pkg>.Testing/
├── <Slice>RequestBuilder.cs # public sealed class with deterministic defaults; A.<Slice>Request() returns it
│                            # (small: both internal, since the record is; Testing's InternalsVisibleTo reaches Tests)
├── InMemory<Port>.cs        # per new port: the one fake (`IItemStore` → `InMemoryItemStore`)
└── <Port>Contract.cs        # per new port: the contract base the fake and every adapter derive from (`ItemStoreContract`)
src/<Pkg>/Aw.<Pkg>.Core/Ports/
└── <IPort>.cs               # per new port: an empty public interface (namespace Aw.<Pkg>.Core.Ports); its members arrive with the implement step
```

`<Conv>` by primitive: `string` → `StringIdJsonConverter`, `Guid` → `GuidIdJsonConverter`, `int` → `Int32IdJsonConverter`
(all in `Aw.Kernel.Json`). `<Id>Tests` reads with `AwJson.Deserialize`, which catches the converter's `JsonException`
and returns `AW-KERNEL-001` (`KernelErrors.Invalid`) in a failed `Result`, so the test asserts that error for an
invalid, a missing and a null value (spec §4b.3), never a thrown exception.

The endpoint lives in `Host`, never in `Features`: `Host` is the Web SDK project where the request-delegate generator
runs, so the minimal-API lambda is AOT-safe there, while in an `IsAotCompatible` `Features` library it would raise
IL2026/IL3050 (errors under warnings-as-errors). The test reaches the `internal` service through the
`InternalsVisibleTo` that `/new-package` declares.

### Files edited

| File | Change |
|---|---|
| `src/<Pkg>/Aw.<Pkg>.Features/<Pkg>FeaturesServiceCollectionExtensions.cs` | Registers the service, its validator, and the validation decorator as the `IService<,>` (template below) |
| `src/<Pkg>/Aw.<Pkg>.Features/PublicAPI.Unshipped.txt` | `shape: full` only: the request and response records and all their members, compiler-generated ones included, annotated — recorded from the build's `RS0016` diagnostics (below). In a `small` package the records are `internal`, so `App`'s PublicAPI files stay at `#nullable enable` and this step is skipped |
| `src/<Pkg>/Aw.<Pkg>.Host/Endpoints/EndpointTable.cs` or `Commands/CommandTable.cs` | One line mapping the route or command |
| `src/<Pkg>/Aw.<Pkg>.Host/<Pkg>JsonContext.cs` | `[JsonSerializable]` for `<Slice>Request`, `<Slice>Response`, and `<Slice>Body` (http) |
| `src/<Pkg>/Aw.<Pkg>.Core/PublicAPI.Unshipped.txt` | When `ids` is non-empty or a new port interface is added. Each new id adds its type and its Vogen-generated members (about 17 lines for a string id), and each new port its interface, all recorded from the build's `RS0016` diagnostics (below; `docs/csharp/typed-ids.md`, "Public ids and PublicAPI files") and reviewed |
| `tests/Aw.<Pkg>.Testing/A.cs` | One factory method: `public static <Slice>RequestBuilder <Slice>Request() => new();` |
| `tests/Aw.<Pkg>.Testing/` | A fake and a contract base per new port, one type per file. The contract base lives here, not in `Aw.Kernel.Testing`, and derives from a Kernel base where one fits (`ReadRepositoryContract`); the fake and every real adapter pass it (`.agents/rules/csharp.md`, Seams) |

No other slice is read or referenced. Don't copy logic from another slice (`ARCH-SLICE-001` fails a cross-slice
reference): move shared logic into `Core` first (second use → generalize), then scaffold the slice.

Every edit to an existing file inserts at a fixed anchor (`return services;`, the `_ => CommandOutput.Usage` arm, the
`_ = app.MapGroup(…)` chain, the context class, `A`'s closing brace); a missing anchor (a hand-edited file) is refused
with `NS-EDIT`, never skipped. The first `Guid` or `int` id also adds `<NoWarn>$(NoWarn);RS0041</NoWarn>` to `Core`'s
csproj (Vogen's `#nullable disable` around that id's generated `ToString(string, IFormatProvider)`; `typed-ids.md`).
A `GET` or `DELETE` slice has no body, so every id it takes must be a route segment (`NS-ROUTE` otherwise); an id
listed twice (`NS-IDS`, `NS-PORTS`, `NS-ROUTE`) is refused too. A package with no `A.cs` (Sindri's S1 dropped its
empty one) gets the builder root written again, holding the new factory.

## `--slice-only`

For slices scaffolded into one package in parallel branches (the Sindri API spec, C7 and §12). The generator writes
only what belongs to the slice:

- the slice folder, the red test, the request builder;
- new ids (`Core/Ids/<Id>.cs`, `<Id>Tests.cs`, `<Id>Holder.cs`) and a `<Id>RouteBindingTests.cs` for each route id that
  lacks one;
- new ports with their fake and contract base;
- the slice's own lines in three shared files: `A.cs` (its factory method), `IdsJsonContext.cs` (one line per new id)
  and each project's `PublicAPI.Unshipped.txt` (from the build's `RS0016` output, as above).

Parallel branches append at the same anchors in those three files, so merging them gives adjacent-line conflicts. The
integrator resolves them by keeping both sides (the spec's "Parallel delivery"), deduplicating a line both branches
added: two slices that declare the same new id each add its `[JsonSerializable]` and PublicAPI lines, and a duplicate
PublicAPI line fails the build. An id more than one slice needs belongs in the contract unit.

It leaves out the registration, the `CommandTable` arm or `EndpointTable` call, the `<Pkg>JsonContext` lines, and
the surface files (`Endpoints/<Slice>Endpoint.cs`, `Endpoints/<Slice>Body.cs`, `Commands/<Slice>Command.cs`). After
the file list it prints a "Shared lines to add in your branch" block. It lists the lines the normal mode would have
inserted at each anchor, with the usings each needs, rather than a byte-exact splice (the `EndpointTable` call goes on
the `MapGroup` chain, before its `;`). It also lists the surface files to write by hand and, for Sindri only, a
reminder to mark the request `ICommand<…>` or `IQuery<…>`. The red test still compiles and fails on its own: it creates the stub directly, so it needs no
registration. Until the registration exists nothing in the assembly uses the stub or the validator, and `MA0182` (an
unused internal type) is an error, so both carry a `[SuppressMessage]` for it with a justification; the block says to
remove it with the registration.

`--slice-only` creates no shared file. A missing `A.cs`, or a missing `IdsJsonContext.cs` when the slice adds an id,
is refused with `NS-SLICE-ONLY`. Journal guards refuse any other edit to an existing file and any new file that isn't
the slice's own, so a new code path can't slip one in. A new `Guid` or `int` id when `Core`'s csproj lacks the
`RS0041` NoWarn is refused too. Fix: add it in the phase's contract unit, or run without `--slice-only`. Everything
written is reverted.

The normal mode writes a missing `A.cs` or `IdsJsonContext.cs`. With `--slice-only`, the phase's contract unit adds
them instead. It writes `A.cs` from the builder-root template (a `public static class A` with an empty body,
`internal` in the small shape). For `IdsJsonContext.cs`, a context with no `[JsonSerializable]` line doesn't compile
(`CS0534`), so it needs a holder: the contract unit writes a `<Id>Holder.cs` for one existing id (the holder template,
`public sealed record <Id>Holder(<Id> Id);`), an `IdsJsonContext.cs` that lists it, and a test that round-trips the
holder through the context, as the generator's id tests do. Running one slice with a new id in the normal mode also
adds both; a slice without a new id adds only `A.cs`.

Without `--files-only` it runs the same checks as the normal mode, under the heavy-job lock. With it, it writes the
files only (the unit tests).

For example, once the phase's contract unit has added Sindri's `A.cs` and `IdsJsonContext.cs` (until then this is
refused with `NS-SLICE-ONLY`):

```bash
node scripts/scaffold/new-slice.mjs --slice-only --package Sindri --slice GetJob --behavior returns_the_job \
  --surface http --route "GET /jobs/{jobId}" --ids JobId:string
```

### Template: `<Slice>Service.cs`

```csharp
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.<Pkg>.Features.<Slice>;

internal sealed class <Slice>Service : IService<<Slice>Request, <Slice>Response>
{
    public ValueTask<Result<<Slice>Response>> Handle(<Slice>Request request, CancellationToken cancellationToken)
        => throw new NotSupportedException("<Slice>: make <Slice>Tests pass");
}
```

The stub has **no constructor parameters**: an unread parameter is `CS9113` and `NotImplementedException` is `MA0025`,
both errors under the root settings. The `ports` (and `TimeProvider`) arrive in the implement step; the generator has
already created any new port interface and its fake, and prints the constructor to write.

### Template: a typed id (`Core/Ids/<Id>.cs`, R9)

```csharp
using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.<Pkg>.Core.Ids;

/// <summary><Id>: what it identifies.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<<Id>>))]
public readonly partial struct <Id> : ITypedId<<Id>, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("<Id> must not be blank") : Validation.Ok;
}
```

`ITypedId<TSelf, TValue>` declares `IParsable<TSelf>` in user code, and the converter reads with `TryFrom` and throws
`JsonException` (AW-KERNEL-001 through `AwJson.Deserialize`, a 400 in ASP.NET body binding). `ITypedId` is in `Aw.Kernel.Ids`, the `StringIdJsonConverter`/`GuidIdJsonConverter`/`Int32IdJsonConverter` are in
`Aw.Kernel.Json`, and the recipe is `docs/csharp/typed-ids.md`. The package's `VogenDefaults.cs` (from
`/new-package`) sets all six arguments of `docs/csharp/typed-ids.md` step 2, verbatim. **Before an id in this form binds a route** (an
http slice whose `route` has `{id}`), the generator adds an RDG route-binding test for it in
`tests/Aw.<Pkg>.Tests/Ids/<Id>RouteBindingTests.cs` (Lane=integration): an id in the `ITypedId` form is used behind an
endpoint only once that test passes; otherwise the id gets a hand-written `TryParse` instead. The test maps its own
`/__probe/items/{id}` and `/__probe/items?id=` endpoints (a prefix no package group can take) with the id on the real host (`LoopbackBinding.Build` + `Pipeline.Configure`,
as `Program.cs` runs it, listening on loopback; the test project runs the request-delegate generator), so the Host's
`ValueObjectValidation` middleware (from `/new-package`, `docs/csharp/typed-ids.md` step 4) is the one in the real
pipeline, and asserts: valid text →
200 with the id's value; text the type refuses → 400; an empty `?id=` → 400; and both endpoints' `IParameterBindingMetadata`
reports `HasTryParse`, so losing `IParsable` is a red test, not a production 500. The architecture check
for the package's ids is `TypedIdGuard` in `tests/Aw.Architecture.Tests` (`ARCH-ID-001`, `ARCH-ID-002`), which covers
every assembly `/new-package` registered in `Packages.cs`.

### Template: `<Slice>Validator.cs`

```csharp
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.<Pkg>.Features.<Slice>;

internal sealed class <Slice>Validator : IValidator<<Slice>Request>
{
    public Result<Unit> Validate(<Slice>Request value) => Unit.Value;
}
```

### Template: the registration (added to `Add<Pkg>Features()`, before its `return services;`)

```csharp
_ = services
    .AddScoped<<Slice>Service>()
    .AddScoped<IValidator<<Slice>Request>, <Slice>Validator>()
    .AddScoped<IService<<Slice>Request, <Slice>Response>>(static sp =>
        new ValidatingService<<Slice>Request, <Slice>Response>(
            sp.GetRequiredService<<Slice>Service>(),
            sp.GetRequiredService<IValidator<<Slice>Request>>()));
```

The edit also adds `using Aw.<Pkg>.Features.<Slice>;`, plus `using Aw.Kernel.Services;` when the file doesn't have it yet (the first slice); the file already has `Microsoft.Extensions.DependencyInjection` for `IServiceCollection`. M.E.DI has no decorators, so the decorator is a factory registration. Its `IServiceProvider` parameter is the one
exception to "no `IServiceProvider` outside `Host`" (`.agents/rules/csharp.md`, DI): it lives only inside the project's
own `Add…()` method and only resolves and constructs. `HostTests` resolves every `IService<,>` in a scope, which covers
what `ValidateOnBuild` can't see inside the factory.

### Template: `Endpoints/<Slice>Endpoint.cs` (http)

```csharp
using Aw.<Pkg>.Core.Ids;
using Aw.<Pkg>.Features.<Slice>;
using Aw.Kernel.Services;

namespace Aw.<Pkg>.Host.Endpoints;

internal static class <Slice>Endpoint
{
    internal static RouteGroupBuilder Map<Slice>(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/items/{itemId}/start", static async (ItemId itemId, StartItemBody body,
                IService<StartItemRequest, StartItemResponse> service, CancellationToken ct)
            => (await service.Handle(body.ToRequest(itemId), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
```

Shown for `--route "POST /items/{itemId}/start" --ids ItemId:string,OwnerId:Guid` (generated on one line; wrapped
here). For each `{name}` in `route` the lambda takes one typed-id parameter named after the id (`ItemId itemId`; the
route segment is rewritten to that name, since the request-delegate generator binds by name), in route order, before
the body; the request is built from those ids plus the body with `<Slice>Body.ToRequest(<route ids>)`. A body exists
only when the method takes one and the request has ids the route doesn't carry; otherwise the lambda builds
`new <Slice>Request(<route ids>)`. A route with no `{name}` segment gives `ToRequest()`. Each file emits exactly the namespaces its own types use (new-package.md): `<Slice>Endpoint.cs` emits `using Aw.<Pkg>.Core.Ids;` when `route` has a `{name}` segment, and `<Slice>Body.cs` emits it when `route` has a `{name}` segment or any body field's type is in `Aw.<Pkg>.Core.Ids`. A missing using fails with CS0246, and an unused one fails the build too (`IDE0005` is an error). The lambda binds, calls one
service and maps the result; it has no branches, and the discard keeps `IDE0058` quiet. Route text binds through the
id's `IParsable` `TryParse` (Vogen-generated, runs `Validate`; refused text → 400). Body JSON goes through the AwJson
profile the Host applies in `ConfigureHttpJsonOptions`. An uninitialized id's `ValueObjectValidationException` is
mapped to 400 by the Host's `ValueObjectValidation` middleware, not here. `ToHttpResult()` is the Host's `HttpResults`
helper generated by `/new-package`; it maps only the returned `Result`.

### Template: `Endpoints/<Slice>Body.cs` (http)

```csharp
using Aw.<Pkg>.Core.Ids;
using Aw.<Pkg>.Features.<Slice>;

namespace Aw.<Pkg>.Host.Endpoints;

internal sealed record <Slice>Body(<body fields>)
{
    internal <Slice>Request ToRequest(<Id> id) => new(id, <body fields>);
}
```

The body carries only what isn't in the route; route ids never repeat in it.

### Template: the red test

```csharp
using Aw.<Pkg>.Features.<Slice>;
using Aw.<Pkg>.Testing;

namespace Aw.<Pkg>.Tests.<Slice>;

public sealed class <Slice>Tests
{
    [Fact, Trait("Lane", "unit")]
    public async Task <Slice>_<behavior>()
    {
        var service = new <Slice>Service();
        var request = A.<Slice>Request().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert <behavior>");
    }
}
```

It fails with the stub's `NotSupportedException`. The `Assert.Fail` line is the agent's first edit: replace it with the
real assertion, add the fakes and a `FakeTimeProvider` as the service gains its ports, see the test fail for the right
reason, then implement. No `TODO` comment (`MA0026`), no `ConfigureAwait` in the test, underscore names allowed: the
test policy set by #89 in `tests/.editorconfig` turns off `CA1707`, `RS0016`, `RS0017`, `RS0036`, `RS0037`, `CA2007`
and `CA1515` for test projects, with `GenerateDocumentationFile=true` and `CS1591` off.

## Commands the generator runs

The public API lines come from the build, not from the `RS0016` code fix: on .NET 10 `dotnet format analyzers
--diagnostics RS0016` changes nothing, and it never covered source-generated members (Vogen's). For each library
project in reference order (`Core` when the slice adds an id or a port, then `Features` in `shape: full`; skipped for
`App`), the generator builds the project and appends every `RS0016` symbol the build reports for that project to its
`PublicAPI.Unshipped.txt`, sorted, after the lines already there (`declarePublicApi` in `scripts/scaffold/lib/checks.mjs`).
It edits the files through its journal, so a failed check restores them too. A build that fails with no `RS0016` to
declare is `SCAFFOLD-PUBLICAPI`.

```bash
# when `ids` is non-empty or a port was added (Core), then shape: full (Features): by hand, the same step is
dotnet build src/<Pkg>/Aw.<Pkg>.Core 2>&1 \
  | sed -n "s/.*RS0016: Symbol '\(.*\)' is not part of the declared public API.*/\1/p" | sort -u \
  >> src/<Pkg>/Aw.<Pkg>.Core/PublicAPI.Unshipped.txt                      # each id's type and Vogen members, each new port; review them
dotnet build src/<Pkg>/Aw.<Pkg>.Features 2>&1 | sed …  >> src/<Pkg>/Aw.<Pkg>.Features/PublicAPI.Unshipped.txt   # the new records
dotnet build tests/Aw.<Pkg>.Tests/Aw.<Pkg>.Tests.csproj -warnaserror   # 0 warnings; then the same for tests/Aw.Architecture.Tests (the whole solution is not built per call)
dotnet test --project tests/Aw.<Pkg>.Tests --filter-trait "Lane=unit" --ignore-exit-code 8   # exactly one failure: <Slice>Tests.<Slice>_<behavior>; exit 8 = empty lane; also run for tests/Aw.Architecture.Tests
dotnet format <each project holding a written .cs file> --verify-no-changes --include <the .cs files written or edited>   # one run per project
```

Under a slot of the heavy-job lock. Commit: `test: red <Slice> test (<behavior>)`.

## Done criteria

1. Build 0 warnings; the new test is the only failing test (the lane exits 2 with `failed: 1`); format clean;
   architecture tests green. A `/new-slice` while an earlier slice's test is still red is therefore refused
   (`SCAFFOLD-RED`): make that slice green first.
2. `git status` shows only the files listed above (`SCAFFOLD-STRAY` otherwise).
3. The generator prints the next steps in order: write the assertion → add the ports to the constructor → implement `<Slice>Service` (pure decisions on the
   domain type, not in the service) → add the integration test if the slice touches SQL → run the lane.

The generator's own test suite (`scripts/scaffold/e2e.sh`) makes the first slice of a package built by `/new-package`
green, scaffolds a second slice with a new id and a new port, and asserts criterion 1 against the real root files, in
both shapes: in `shape: full` the second slice's records and port need `Features`' and `Core`'s PublicAPI lines. It then
makes the second slice green and adds a third that reuses the port, which must leave the port, its fake and its
contract base as they are, and scaffolds a fourth with `--slice-only`, which must change only that slice's files and
leave exactly its test red. `scripts/tests/scaffold.test.sh` covers the port lookup, the `RS0016` parsing and
`--slice-only`'s file set without dotnet.

## Failure behavior

- A check fails other than the expected red test → revert every created or edited file and print the rule id, the
  measured value and the fix.
- `NS-EXISTS` when the slice folder exists; `NS-SURFACE` when `surface` doesn't match the package's host; `NS-DEPS` as
  above; `NS-EDIT` when an anchor is missing; `NS-SLICE-ONLY` when `--slice-only` would edit or create a shared file, or a shared file it adds a line to is missing; `SCAFFOLD-INTERNAL` for a generator bug (also reverted).
- SIGINT, SIGTERM or SIGHUP at any point of a run (including the heavy-lock wait) is caught: the generator stops the
  running command's whole process group (SIGINT, dotnet's cancel path, then SIGKILL after 10 s), fails that step with `SCAFFOLD-INTERRUPTED` whatever the command exited with, then
  reverts and releases the lock. The lock directory carries an `owner` file (pid, time, command).
