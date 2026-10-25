using System.Net;
using System.Text.Json;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The graph routes (S-3b): GET /sindri/context/node, /neighbors, /path and the node shape of /query, over the imported fixture map.</summary>
public sealed class ContextGraphApiTests
{
    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Node_neighbors_path_and_a_node_query_answer_a_human_and_a_session_stays_in_its_repo_scope()
    {
        await using var host = await ContextMapApiTests.Approved(sessions: true, TestContext.Current.CancellationToken);
        await ContextMapApiTests.Import(host);
        (string, string)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

        var node = await host.Http.Ask(Get("/sindri/context/node?key=feature%3Alayout.orientation", Human(host.Token.Value)));
        var neighbors = await host.Http.Ask(Get("/sindri/context/neighbors?key=thing%3Apane-layout&depth=1", Human(host.Token.Value)));
        var path = await host.Http.Ask(Get("/sindri/context/path?from=feature%3Alayout.split-pane.orientation&to=Tabs", Human(host.Token.Value)));
        var query = await host.Http.Ask(Get("/sindri/context/query?node=feature%3Alayout.split-pane.orientation", Human(host.Token.Value)));
        var outOfScope = await host.Http.Ask(Get("/sindri/context/node?key=thing%3Apane-layout", session));
        var inScope = await host.Http.Ask(Get("/sindri/context/node?key=feature%3Atabs.tab-manager", session));
        var deep = await host.Http.Ask(Get("/sindri/context/neighbors?key=thing%3Apane-layout&depth=-1", Human(host.Token.Value)));
        var word = await host.Http.Ask(Get("/sindri/context/neighbors?key=thing%3Apane-layout&depth=abc", Human(host.Token.Value)));
        var queryWord = await host.Http.Ask(Get("/sindri/context/query?node=feature%3Alayout.split-pane.orientation&depth=abc", Human(host.Token.Value)));
        var budgetWord = await host.Http.Ask(Get("/sindri/context/query?node=feature%3Alayout.split-pane.orientation&budget=abc", Human(host.Token.Value)));
        var budgetZero = await host.Http.Ask(Get("/sindri/context/query?node=feature%3Alayout.split-pane.orientation&budget=0", Human(host.Token.Value)));
        var budgetOk = await host.Http.Ask(Get("/sindri/context/query?node=feature%3Alayout.split-pane.orientation&budget=4000", Human(host.Token.Value)));
        var origin = await host.Http.Ask(Get("/sindri/context/node?key=feature%3Alayout.orientation", [.. Human(host.Token.Value), ("Origin", "http://evil.example")]));

        Assert.Equal(HttpStatusCode.OK, node.Status);
        Assert.Equal("feature:layout.split-pane.orientation", JsonDocument.Parse(node.Body).RootElement.GetProperty("node").GetProperty("key").GetString());
        Assert.Equal(4, JsonDocument.Parse(neighbors.Body).RootElement.GetProperty("nodes").GetArrayLength());
        Assert.True(JsonDocument.Parse(path.Body).RootElement.GetProperty("found").GetBoolean());
        Assert.Contains("\"node\":\"feature:tabs.tab-manager\",", query.Body, StringComparison.Ordinal);
        Assert.Contains("\"reads action:tab-manager.restore-layout\"", query.Body, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (outOfScope.Status, outOfScope.Code));
        Assert.Equal(HttpStatusCode.OK, inScope.Status);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (deep.Status, deep.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (word.Status, word.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (queryWord.Status, queryWord.Code));
        Assert.Equal(HttpStatusCode.OK, budgetWord.Status);   // why: there is no budget; the parameter is read and ignored, never validated
        Assert.Equal(HttpStatusCode.OK, budgetZero.Status);
        Assert.Equal(HttpStatusCode.OK, budgetOk.Status);
        Assert.Equal(HttpStatusCode.Forbidden, origin.Status);
    }
}
