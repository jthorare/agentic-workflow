using System.Net;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ids;

/// <summary>
/// ItemId binds from a route and a query through the request-delegate generator's IParsable path, never as a body
/// (docs/csharp/typed-ids.md): valid text → 200 with the id's value, refused text → 400, an empty ?id= → 400 through the
/// ValueObjectValidation middleware of the real pipeline (LoopbackBinding + Pipeline.Configure, as Program.cs runs it).
/// </summary>
public sealed class ItemIdRouteBindingTests
{
    private const string Valid = "ITM-0192f0c1a2b37c4d8e9f00112233aabb";

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
    [InlineData("/__probe/items/%20")]
    [InlineData("/__probe/items?id=%20")]
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
        _ = app.MapGet("/__probe/items/{id}", static (ItemId id) => id.Value);
        _ = app.MapGet("/__probe/items", static (ItemId id) => id.Value);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient Client(WebApplication app) => new()
    {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.Single()
            ?? throw new InvalidOperationException("the host has no address")),
    };
}
