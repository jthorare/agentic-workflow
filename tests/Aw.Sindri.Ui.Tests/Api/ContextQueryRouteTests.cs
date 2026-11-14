using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The free-text context query over the canned handler: the question travels as one escaped query value, and the two anchor lists come back.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextQueryRouteTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task QueryAnchors_sends_the_question_as_one_escaped_q_value_and_reads_both_lists()
    {
        var server = new FakeSindriApi { QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody(["feature:a"], ["feature:b"]) };
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(A.HumanToken), TimeProvider.System);

        var answer = await client.QueryAnchors(_api, "drag & drop shifts", Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/context/query"), (seen.Method, seen.Path));
        Assert.StartsWith("?q=drag%20%26%20drop%20shifts", seen.Query, StringComparison.Ordinal);
        Assert.Equal(["feature:a"], answer.Value.Anchors);
        Assert.Equal(["feature:b"], answer.Value.SuggestedAnchors);
    }

    // The anchor lookup reads only the anchors, so it asks for a window of one record: the host then fetches and cleans one record, not all of them (#3524).
    [Fact]
    public async Task QueryAnchors_asks_for_a_window_of_one_record()
    {
        var server = new FakeSindriApi { QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody(["feature:a"], []) };
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(A.HumanToken), TimeProvider.System);

        _ = await client.QueryAnchors(_api, "drag", Token);
        _ = await client.QueryAnchors(_api, "drag", Token, concept: "concept:scheduling");

        Assert.Equal(["?q=drag&limit=1", "?q=drag&concept=concept%3Ascheduling&limit=1"], handler.Requests.Select(static r => r.Query), StringComparer.Ordinal);
    }
}
