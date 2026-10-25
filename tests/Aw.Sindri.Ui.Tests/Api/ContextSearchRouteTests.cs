using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The map search over the canned handler: the words travel as one escaped query value and the golden's hits come back in order.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextSearchRouteTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ContextSearch_sends_the_words_as_one_escaped_q_value_with_the_other_narrowing()
    {
        var server = new FakeSindriApi { ContextSearchAnswer = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("context-search", "get-tab-manager")) };
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(A.HumanToken), TimeProvider.System);

        var answer = await client.ContextSearch(_api, new MapSearchQuery("tab & manager?", Limit: 20, Area: "feature:tabs", Repo: "aw split"), Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/context/search", "?q=tab%20%26%20manager%3F&limit=20&area=feature%3Atabs&repo=aw%20split"), (seen.Method, seen.Path, seen.Query));
        Assert.Equal(["feature:tabs.tab-manager", "record:linear:issue/0e5a1b00-0000-4000-8000-000000000203"], answer.Value.Hits.Select(h => h.Key), StringComparer.Ordinal);
    }
}
