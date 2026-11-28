using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ListWorkflowRuns;

/// <summary>
/// GET /sindri/workflows/{slug}/runs?offset=&amp;limit= over the served pipeline (#3305): the app pages past the newest 50 runs with the offset, the way
/// 'sindri wf runs' and the workflow_runs tool already do; a value that is not a whole number is refused, not turned into the first page.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowRunsRouteTests : IAsyncLifetime
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private GoldenHost? _host;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GoldenHost Host => _host ?? throw new InvalidOperationException("not started");

    public async ValueTask InitializeAsync() => _host = await GoldenHost.Start(temporary: true, Token);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    private async Task Seed(int count)
    {
        var spec = new GraphSpec(Version: 1, "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        var workflows = Host.App.Services.GetRequiredService<IWorkflowStore>();
        _ = (await workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await workflows.Activate(_id, 1, Token)).Value;
        var runs = Host.App.Services.GetRequiredService<IWorkflowRunStore>();
        for (var occurrence = 1; occurrence <= count; occurrence++)
        {
            _ = (await runs.Insert(new WorkflowRun(WorkflowRunId.Of(_slug, 1, occurrence), _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, Host.Now().AddMinutes(occurrence), EndedAt: null), Token)).Value;
        }
    }

    private async Task<(HttpStatusCode Status, JsonNode Body)> Read(string query)
    {
        var answer = await Host.Http.Ask(Get($"/sindri/workflows/{_slug.Value}/runs{query}", Human(Host.Token.Value)));
        return (answer.Status, JsonNode.Parse(answer.Body)!);   // why: the route answers a JSON document
    }

    private static string[] Ids(JsonNode body) => [.. body["runs"]!.AsArray().Select(static r => r!["id"]!.GetValue<string>())];   // why: the route answers a JSON document with these fields

    [Fact]
    public async Task The_offset_skips_the_newest_runs_and_more_says_whether_older_ones_are_left()
    {
        await Seed(3);

        var (status, body) = await Read("?offset=1&limit=1");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["morning-check@1:2"], Ids(body), StringComparer.Ordinal);
        Assert.True(body["more"]!.GetValue<bool>());   // why: the route answers a JSON document with these fields
    }

    [Fact]
    public async Task The_last_page_says_no_more()
    {
        await Seed(3);

        var (_, body) = await Read("?offset=2");

        Assert.Equal(["morning-check@1:1"], Ids(body), StringComparer.Ordinal);
        Assert.False(body["more"]!.GetValue<bool>());   // why: the route answers a JSON document with these fields
    }

    [Fact]
    public async Task No_query_is_the_newest_page()
    {
        await Seed(3);

        var (_, body) = await Read(string.Empty);

        Assert.Equal(["morning-check@1:3", "morning-check@1:2", "morning-check@1:1"], Ids(body), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("?offset=soon")]
    [InlineData("?offset=-1")]
    [InlineData("?limit=0")]
    [InlineData("?limit=many")]
    public async Task An_offset_or_limit_that_is_not_a_whole_number_is_refused(string query)
    {
        await Seed(1);

        var (status, body) = await Read(query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("SND-CLI-003", body.ToJsonString(), StringComparison.Ordinal);
    }
}
