using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// QUERY /sindri/links/resolve (messages spec §9.3, M1g) on the real pipeline: the body {urls[]} to one answer per url, the registered store answering 'none'
/// until Home's rows land, a store that holds the row answering its fields, a bad body refused, and an Origin header refused like every body read.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ResolveLinksRouteTests
{
    private const string Route = "/sindri/links/resolve";
    private const string Body = """{"urls":["https://github.com/acme/harbor/pull/3120","https://example.com/x"]}""";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact(Timeout = 60_000)]
    public async Task The_registered_store_answers_none_for_every_url_until_home_rows_land()
    {
        await using var host = await GoldenHost.Start(temporary: true, Cancel);

        var answer = await host.Http.Ask(Query(Route, Body, Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var links = JsonNode.Parse(answer.Body)!["links"]!.AsArray();
        Assert.Equal(2, links.Count);
        Assert.All(links, l => Assert.Null(l!["rowKey"]?.GetValue<string>()));
    }

    [Fact(Timeout = 60_000)]
    public async Task A_row_the_store_holds_answers_its_key_and_fields()
    {
        await using var host = await GoldenHost.Start(temporary: true, Cancel, extra: static s => s.AddSingleton<ILinkRows>(new OneRow()));

        var answer = await host.Http.Ask(Query(Route, Body, Human(host.Token.Value)));

        var links = JsonNode.Parse(answer.Body)!["links"]!.AsArray();
        Assert.Equal(("gh:pr:acme/harbor#3120", "Fare table uses 90 s", "open", "passing"),
            ((string)links[0]!["rowKey"]!, (string)links[0]!["title"]!, (string)links[0]!["state"]!, (string)links[0]!["ci"]!));
        Assert.Null(links[1]!["rowKey"]?.GetValue<string>());
    }

    [Fact(Timeout = 60_000)]
    public async Task More_than_twenty_urls_are_refused_with_api_013()
    {
        await using var host = await GoldenHost.Start(temporary: true, Cancel);
        var urls = new JsonArray([.. Enumerable.Range(1, 21).Select(static i => (JsonNode?)JsonValue.Create("https://github.com/acme/harbor/pull/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))]);

        var answer = await host.Http.Ask(Query(Route, new JsonObject { ["urls"] = urls }.ToJsonString(), Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-API-013"), (answer.Status, answer.Code));
    }

    [Fact(Timeout = 60_000)]
    public async Task An_origin_header_is_refused_like_every_body_read()
    {
        await using var host = await GoldenHost.Start(temporary: true, Cancel);

        var answer = await host.Http.Ask(Query(Route, Body, [.. Human(host.Token.Value), ("Origin", "https://evil.example")]));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-API-008"), (answer.Status, answer.Code));
    }

    // why: Home's row store isn't built, so the test registers a store that holds one PR row
    private sealed class OneRow : ILinkRows
    {
        public ValueTask<Result<IReadOnlyDictionary<string, LinkRow>>> Find(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
        {
            var row = new LinkRow("gh:pr:acme/harbor#3120", "Fare table uses 90 s", "open") { Ci = "passing" };
            Result<IReadOnlyDictionary<string, LinkRow>> found = keys.Contains(row.Key, StringComparer.Ordinal)
                ? new Dictionary<string, LinkRow>(StringComparer.Ordinal) { [row.Key] = row }
                : new Dictionary<string, LinkRow>(StringComparer.Ordinal);
            return ValueTask.FromResult(found);
        }
    }
}
