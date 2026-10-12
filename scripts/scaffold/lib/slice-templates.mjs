// scripts/scaffold/lib/slice-templates.mjs — the files /new-slice writes (docs/csharp/scaffolds/new-slice.md).
// `s` is the resolved input:
//   { pkg, shape, host, slice, behavior, surface, command, method, path, routeIds: [Id], ids: [{name, prim}],
//     requestIds: [{name, prim}], ports: [name], newPorts: [name], featuresNs, testsNs, visibility }
// Small shape: the records, the service, the validator and the builder are internal (App is an Exe; CA1515).

import { camel, lower } from "./common.mjs";
import { usingsBlock } from "./package-templates.mjs";

const CONVERTER = { string: "StringIdJsonConverter", Guid: "GuidIdJsonConverter", int: "Int32IdJsonConverter" };
const SAMPLE = {
  string: (id) => `"${lower(id)}-1"`,
  Guid: () => "new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1)",   // 00000000-0000-0000-0000-000000000001 (MA0176: no string parse)
  int: () => "1",
};
const INVALID = { string: '""', Guid: '"not-a-guid"', int: '"one"' };
const ROUTE_GOOD = { string: (id) => `${lower(id)}-1`, Guid: () => "00000000-0000-0000-0000-000000000001", int: () => "7" };
const ROUTE_BAD = { string: "%20", Guid: "not-a-guid", int: "x" };
const TEXT = { string: "id.Value", Guid: 'id.Value.ToString("D", CultureInfo.InvariantCulture)', int: "id.Value.ToString(CultureInfo.InvariantCulture)" };
const BLANK = { string: "string.IsNullOrWhiteSpace(value)", Guid: "value == Guid.Empty", int: "value <= 0" };
const BLANK_TEXT = { string: "must not be blank", Guid: "must not be empty", int: "must be positive" };

const params = (ids) => ids.map((i) => `${i.name} ${i.name}`).join(", ");
const idsNs = (s) => `Aw.${s.pkg}.Core.Ids`;
const portsNs = (s) => `Aw.${s.pkg}.Core.Ports`;

// ---------------------------------------------------------------- Features

export const request = (s) => `${s.requestIds.length ? `${usingsBlock([idsNs(s)])}\n\n` : ""}namespace ${s.featuresNs};

/// <summary>The ${s.slice} request.</summary>
${s.visibility} sealed record ${s.slice}Request(${params(s.requestIds)});
`;

export const response = (s) => `namespace ${s.featuresNs};

/// <summary>The ${s.slice} response.</summary>
${s.visibility} sealed record ${s.slice}Response();
`;

/**
 * --slice-only: nothing in the assembly uses the stub and validator until the branch adds the registration by hand,
 * and MA0182 (an unused internal type) is an error. The suppression goes with that registration.
 */
const unregistered = (s) => (s.sliceOnly ? ["System.Diagnostics.CodeAnalysis"] : []);
const unregisteredMark = (s) =>
  s.sliceOnly ? `[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "--slice-only: unused until this branch registers the slice; remove with that registration")]\n` : "";

export const service = (s) => `${usingsBlock([...unregistered(s), "Aw.Kernel", "Aw.Kernel.Services"])}

namespace ${s.featuresNs};

${unregisteredMark(s)}internal sealed class ${s.slice}Service : IService<${s.slice}Request, ${s.slice}Response>
{
    public ValueTask<Result<${s.slice}Response>> Handle(${s.slice}Request request, CancellationToken cancellationToken)
        => throw new NotSupportedException("${s.slice}: make ${s.slice}Tests pass");
}
`;

export const validator = (s) => `${usingsBlock([...unregistered(s), "Aw.Kernel", "Aw.Kernel.Services"])}

namespace ${s.featuresNs};

${unregisteredMark(s)}internal sealed class ${s.slice}Validator : IValidator<${s.slice}Request>
{
    public Result<Unit> Validate(${s.slice}Request value) => Unit.Value;
}
`;

/** The registration block /new-slice inserts into Add<Pkg>Features(), before `return services;`. */
export const registration = (s) => `        _ = services
            .AddScoped<${s.slice}Service>()
            .AddScoped<IValidator<${s.slice}Request>, ${s.slice}Validator>()
            .AddScoped<IService<${s.slice}Request, ${s.slice}Response>>(static sp =>
                new ValidatingService<${s.slice}Request, ${s.slice}Response>(
                    sp.GetRequiredService<${s.slice}Service>(),
                    sp.GetRequiredService<IValidator<${s.slice}Request>>()));
`;

// ---------------------------------------------------------------- Core: ids and ports

export const typedId = (s, id) => `${usingsBlock(["System.Text.Json.Serialization", "Aw.Kernel.Ids", "Aw.Kernel.Json", "Vogen"])}

namespace ${idsNs(s)};

/// <summary>${id.name}: the id of a ${s.pkg} ${lower(id.name.replace(/Id$/, "")) || "thing"}.</summary>
[ValueObject<${id.prim}>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(${CONVERTER[id.prim]}<${id.name}>))]
public readonly partial struct ${id.name} : ITypedId<${id.name}, ${id.prim}>
{
    private static Validation Validate(${id.prim} value) =>
        ${BLANK[id.prim]} ? Validation.Invalid("${id.name} ${BLANK_TEXT[id.prim]}") : Validation.Ok;
}
`;

export const port = (s, name) => `namespace ${portsNs(s)};

/// <summary>${name}: a port ${s.slice} needs. Declare its members, then keep ${name.slice(1)}Contract, the fake and every adapter in step.</summary>
public interface ${name};
`;

// ---------------------------------------------------------------- Host: CLI command and http endpoint

export const command = (s) => `${usingsBlock(["System.Text.Json", s.featuresNs, "Aw.Kernel.Services", "Microsoft.Extensions.DependencyInjection", ...(s.requestIds.length ? [idsNs(s), "System.Globalization"] : [])])}

namespace ${s.commandsNs};

/// <summary>The '${s.command}' command: binds its arguments, calls ${s.slice} and maps the result to an exit code.</summary>
internal static class ${s.slice}Command
{
    internal const string Name = "${s.command}";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != ${s.requestIds.length}${s.requestIds.map((id, i) => `\n            || !${id.name}.TryParse(args[${i}], CultureInfo.InvariantCulture, out var ${camel(id.name)})`).join("")})
        {
            return CommandOutput.Usage("${s.command}${s.requestIds.map((id) => ` <${camel(id.name)}>`).join("")}");
        }

        var service = services.GetRequiredService<IService<${s.slice}Request, ${s.slice}Response>>();
        var result = await service.Handle(new ${s.slice}Request(${s.requestIds.map((id) => camel(id.name)).join(", ")}), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
`;

/** The command-table arm /new-slice inserts above the `_ =>` arm. */
export const commandArm = (s) => `                ${s.slice}Command.Name => await ${s.slice}Command.Run(rest, scope.ServiceProvider, json).ConfigureAwait(false),\n`;

/** A body record only when the method takes one and the request has fields the route doesn't carry. */
export const hasBody = (s) => ["POST", "PUT", "PATCH"].includes(s.method) && s.requestIds.some((id) => !s.routeIds.includes(id.name));
const bodyIds = (s) => s.requestIds.filter((id) => !s.routeIds.includes(id.name));

export const endpoint = (s) => {
  const lambdaParams = [
    ...s.routeIds.map((id) => `${id} ${camel(id)}`),
    ...(hasBody(s) ? [`${s.slice}Body body`] : []),
    `IService<${s.slice}Request, ${s.slice}Response> service`,
    "CancellationToken ct",
  ].join(", ");
  const routeArgs = s.routeIds.map((id) => camel(id)).join(", ");
  // Without a body every request field is a route value (resolveSlice refuses others), in the request's own order.
  const make = hasBody(s) ? `body.ToRequest(${routeArgs})` : `new ${s.slice}Request(${s.requestIds.map((id) => camel(id.name)).join(", ")})`;
  return `${usingsBlock([s.featuresNs, "Aw.Kernel.Services", ...(s.routeIds.length ? [idsNs(s)] : [])])}

namespace Aw.${s.pkg}.Host.Endpoints;

internal static class ${s.slice}Endpoint
{
    internal static RouteGroupBuilder Map${s.slice}(this RouteGroupBuilder group)
    {
        _ = group.Map${s.method[0] + s.method.slice(1).toLowerCase()}("${s.path}", static async (${lambdaParams})
            => (await service.Handle(${make}, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
`;
};

export const body = (s) => {
  const fields = bodyIds(s);
  const routeParams = s.routeIds.map((id) => `${id} ${camel(id)}`).join(", ");
  const args = s.requestIds.map((id) => (s.routeIds.includes(id.name) ? camel(id.name) : id.name)).join(", ");
  const needIds = s.routeIds.length > 0 || fields.length > 0;
  return `${usingsBlock([s.featuresNs, ...(needIds ? [idsNs(s)] : [])])}

namespace Aw.${s.pkg}.Host.Endpoints;

internal sealed record ${s.slice}Body(${params(fields)})
{
    internal ${s.slice}Request ToRequest(${routeParams}) => new(${args});
}
`;
};

/** The endpoint-table call /new-slice adds: `.Map<Slice>()` on the package's route group. */
export const endpointCall = (s) => `.Map${s.slice}()`;

// ---------------------------------------------------------------- tests

export const redTest = (s) => `${usingsBlock([s.featuresNs, `Aw.${s.pkg}.Testing`])}

namespace ${s.testsNs};

public sealed class ${s.slice}Tests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ${s.slice}_${s.behavior}()
    {
        var service = new ${s.slice}Service();
        var request = A.${s.slice}Request().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert ${s.behavior}");
    }
}
`;

export const requestBuilder = (s) => {
  const ids = s.requestIds;
  const members = ids.length
    ? [
        ids.map((id) => `    private ${id.name} _${camel(id.name)} = ${id.name}.From(${SAMPLE[id.prim](id.name)});`).join("\n"),
        ...ids.map(
          (id) => `    /// <summary>Sets ${id.name}.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public ${s.slice}RequestBuilder With${id.name}(${id.name} value)
    {
        _${camel(id.name)} = value;
        return this;
    }`,
        ),
        `    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ${s.slice}Request Build() => new(${ids.map((id) => `_${camel(id.name)}`).join(", ")});`,
      ]
    : [
        `    private readonly ${s.slice}Request _request = new();`,
        `    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ${s.slice}Request Build() => _request;`,
      ];
  return `${usingsBlock([s.featuresNs, ...(ids.length ? [idsNs(s)] : [])])}

namespace Aw.${s.pkg}.Testing;

/// <summary>Builds a ${s.slice}Request with deterministic defaults.</summary>
${s.visibility} sealed class ${s.slice}RequestBuilder
{
${members.join("\n\n")}
}
`;
};

export const builderFactory = (s) => `    /// <summary>A ${s.slice}Request builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ${s.slice}RequestBuilder ${s.slice}Request() => new();
`;

export const fake = (s, name) => `using ${portsNs(s)};

namespace Aw.${s.pkg}.Testing;

/// <summary>The in-memory ${name}: the one fake, kept honest by ${name.slice(1)}Contract.</summary>
public sealed class InMemory${name.slice(1)} : ${name};
`;

export const contract = (s, name) => `using ${portsNs(s)};

namespace Aw.${s.pkg}.Testing;

/// <summary>
/// The ${name} contract: the same tests run against the in-memory fake and every real adapter (.agents/rules/csharp.md,
/// Seams). Add one abstract test per member as the port grows; derive from a Aw.Kernel.Testing base where one fits.
/// </summary>
public abstract class ${name.slice(1)}Contract
{
    /// <summary>Creates the implementation under test.</summary>
    /// <returns>A fresh ${name}.</returns>
    protected abstract ${name} Create();
}
`;

export const idTests = (s, id) => `${usingsBlock(["System.Text", "System.Text.Json", idsNs(s), "Aw.Kernel", "Aw.Kernel.Json"])}

namespace ${s.testsRoot}.Ids;

public sealed class ${id.name}Tests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void Round_trips_through_the_Kernel_profile()
    {
        var id = ${id.name}.From(${SAMPLE[id.prim](id.name)});

        var back = AwJson.Deserialize<${id.name}Holder>(AwJson.Serialize(new ${id.name}Holder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\\"id\\":${INVALID[id.prim].replace(/"/g, '\\"')}}")]
    [InlineData("{}")]
    [InlineData("{\\"id\\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<${id.name}Holder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parses_valid_text_and_refuses_invalid_text()
    {
        Assert.True(${id.name}.TryParse("${ROUTE_GOOD[id.prim](id.name)}", provider: null, out _));
        Assert.False(${id.name}.TryParse(${INVALID[id.prim] === '""' ? '" "' : INVALID[id.prim]}, provider: null, out _));
    }
}
`;

export const idHolder = (s, id) => `using ${idsNs(s)};

namespace ${s.testsRoot}.Ids;

/// <summary>Wraps a ${id.name} so the JSON tests can send a missing or null property.</summary>
public sealed record ${id.name}Holder(${id.name} Id);
`;

export const idsJsonContext = (s, holders) => `using System.Text.Json.Serialization;

namespace ${s.testsRoot}.Ids;

/// <summary>The id tests' source-generated JSON context.</summary>
${holders.map((h) => `[JsonSerializable(typeof(${h}))]`).join("\n")}
internal sealed partial class IdsJsonContext : JsonSerializerContext;
`;

export const routeBindingTests = (s, id) => `${usingsBlock([...(id.prim === "string" ? [] : ["System.Globalization"]), "System.Net", idsNs(s), `Aw.${s.pkg}.Host`, "Microsoft.AspNetCore.Builder", "Microsoft.AspNetCore.Hosting.Server", "Microsoft.AspNetCore.Hosting.Server.Features", "Microsoft.AspNetCore.Http.Metadata", "Microsoft.AspNetCore.Routing", "Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.Time.Testing"])}

namespace ${s.testsRoot}.Ids;

/// <summary>
/// ${id.name} binds from a route and a query through the request-delegate generator's IParsable path, never as a body
/// (docs/csharp/typed-ids.md): valid text → 200 with the id's value, refused text → 400, an empty ?id= → 400 through the
/// ValueObjectValidation middleware of the real pipeline (LoopbackBinding + Pipeline.Configure, as Program.cs runs it).
/// </summary>
public sealed class ${id.name}RouteBindingTests
{
    private const string Valid = "${ROUTE_GOOD[id.prim](id.name)}";

    [Theory, Trait("Lane", "integration")]
    [InlineData("/__probe/items/" + Valid)]
    [InlineData("/__probe/items?id=" + Valid)]
    public async Task Valid_text_binds_to_the_id(string url)
    {
        await using var app = await Started();
        using var client = Client(app);

        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Valid, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("/__probe/items/${ROUTE_BAD[id.prim]}")]
    [InlineData("/__probe/items?id=${ROUTE_BAD[id.prim]}")]
    [InlineData("/__probe/items?id=")]
    public async Task Refused_or_empty_text_is_400(string url)
    {
        await using var app = await Started();
        using var client = Client(app);

        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Binds_as_route_and_query_with_TryParse_never_as_a_body()
    {
        await using var app = await Started();
        var probes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => string.Equals(e.RoutePattern.RawText, "/__probe/items/{id}", StringComparison.Ordinal)
                || string.Equals(e.RoutePattern.RawText, "/__probe/items", StringComparison.Ordinal));

        var parameters = probes.SelectMany(e => e.Metadata.GetOrderedMetadata<IParameterBindingMetadata>()).ToList();

        Assert.Equal(2, parameters.Count);
        Assert.All(parameters, p => Assert.True(p.HasTryParse));
    }

    private static async Task<WebApplication> Started()
    {
        var app = LoopbackBinding.Build([], _ => null, new FakeTimeProvider());
        _ = Pipeline.Configure(app);
        _ = app.MapGet("/__probe/items/{id}", static (${id.name} id) => ${TEXT[id.prim]});
        _ = app.MapGet("/__probe/items", static (${id.name} id) => ${TEXT[id.prim]});
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient Client(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.Single()
            ?? throw new InvalidOperationException("the host has no address")),
    };
}
`;


/**
 * Added to Core's csproj with its first Guid or int id: Vogen 8.0.7 wraps such an id's generated
 * ToString(string, IFormatProvider) in #nullable disable, which RS0041 rejects with nothing to fix in user code.
 */
export const rs0041NoWarn = `    <!-- RS0041 (oblivious types in the public API): Vogen 8.0.7 wraps a Guid or int id's generated
         ToString(string, IFormatProvider) in #nullable disable, and .editorconfig sections can't reach generated files.
         RS0016 still tracks those members, declared with the '~' marker (docs/csharp/typed-ids.md). -->
    <NoWarn>$(NoWarn);RS0041</NoWarn>
`;
