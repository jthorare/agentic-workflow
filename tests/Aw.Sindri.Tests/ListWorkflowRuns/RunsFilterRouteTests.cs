using System.Globalization;
using System.Net;
using System.Text.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ListWorkflowRuns;

/// <summary>
/// #4334 on the real host: GET /sindri/workflows/{slug}/runs?status=&amp;since=&amp;until=&amp;before= and MCP workflow_runs with the same arguments reach the same query over the SQLite store,
/// past the newest 50, and both refuse a value they can't read with SND-GRAPH-134.
/// </summary>
public sealed class RunsFilterRouteTests : IAsyncLifetime
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private GoldenHost? _host;

    private GoldenHost Host => _host ?? throw new InvalidOperationException("the host isn't started");

    private static WorkflowRunId Id(int occurrence) => WorkflowRunId.Of(_slug, 1, occurrence);

    // Run n started n minutes after the host's clock; every fifth run failed (5, 10, ..., 60), the rest succeeded.
    private DateTimeOffset At(int occurrence) => Host.Now().AddMinutes(occurrence);

    private static string Instant(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _host = await GoldenHost.Start(temporary: true, ct, SeededMcp.Profile);
        var spec = new GraphSpec(Version: 1, "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        var workflows = Host.App.Services.GetRequiredService<IWorkflowStore>();
        _ = (await workflows.Create(_id, _slug, spec, "owner", ct)).Value;
        _ = (await workflows.Activate(_id, 1, ct)).Value;
        var runs = Host.App.Services.GetRequiredService<IWorkflowRunStore>();
        for (var n = 1; n <= 60; n++)
        {
            var status = n % 5 == 0 ? WorkflowRunStatus.Failed : WorkflowRunStatus.Succeeded;
            _ = (await runs.Insert(new WorkflowRun(Id(n), _id, 1, "{\"kind\":\"schedule\"}", status, Outputs: null, Error: null, ItemId: null, At(n), EndedAt: null), ct)).Value;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_route_and_the_tool_find_a_failed_run_older_than_the_newest_fifty_by_since_and_until()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mcp = await McpSession.Open(Host, repo: null, ct);

        var http = await Route($"?status=failed&since={Instant(At(1))}&until={Instant(At(10))}");
        var tool = await mcp.Call("workflow_runs", $"{{\"slug\":\"morning-check\",\"status\":\"failed\",\"since\":\"{Instant(At(1))}\",\"until\":\"{Instant(At(10))}\"}}", ct);

        Assert.NotEqual(true, tool.IsError);
        Assert.Equal([Id(5).Value], Ids(http), StringComparer.Ordinal);
        Assert.Equal(Ids(http), Ids(tool.StructuredContent!.Value), StringComparer.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_route_and_the_tool_page_past_the_cap_with_before_and_next()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mcp = await McpSession.Open(Host, repo: null, ct);

        var first = await Route(string.Empty);
        var next = first.GetProperty("next").GetString()!;
        var http = await Route("?before=" + Uri.EscapeDataString(next));
        var tool = await mcp.Call("workflow_runs", $"{{\"slug\":\"morning-check\",\"before\":\"{next}\"}}", ct);

        Assert.Equal((50, true, Id(11).Value), (first.GetProperty("runs").GetArrayLength(), first.GetProperty("more").GetBoolean(), next));
        Assert.Equal(Enumerable.Range(1, 10).Reverse().Select(static n => Id(n).Value), Ids(http), StringComparer.Ordinal);
        Assert.Equal(JsonValueKind.Null, http.GetProperty("next").ValueKind);
        Assert.Equal(Ids(http), Ids(tool.StructuredContent!.Value), StringComparer.Ordinal);
    }

    [Theory(Timeout = 60_000), Trait("Lane", "integration")]
    [InlineData("?status=done", "{\"slug\":\"morning-check\",\"status\":\"done\"}")]
    [InlineData("?since=yesterday", "{\"slug\":\"morning-check\",\"since\":\"yesterday\"}")]
    [InlineData("?since=2026-11-24T00:00:00Z&until=2026-11-23T00:00:00Z", "{\"slug\":\"morning-check\",\"since\":\"2026-11-24T00:00:00Z\",\"until\":\"2026-11-23T00:00:00Z\"}")]
    public async Task The_route_and_the_tool_refuse_a_filter_they_cant_read_with_SND_GRAPH_134(string query, string arguments)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var mcp = await McpSession.Open(Host, repo: null, ct);

        var http = await Host.Http.Ask(Get($"/sindri/workflows/{_slug.Value}/runs{query}", Human(Host.Token.Value)));
        var tool = await mcp.Call("workflow_runs", arguments, ct);

        Assert.Equal((HttpStatusCode.BadRequest, SindriErrors.GraphRunsFilterInvalid.Code), (http.Status, http.Code));
        Assert.Equal((true, SindriErrors.GraphRunsFilterInvalid.Code), (tool.IsError == true, Code(tool)));
    }

    private async Task<JsonElement> Route(string query)
    {
        var answer = await Host.Http.Ask(Get($"/sindri/workflows/{_slug.Value}/runs{query}", Human(Host.Token.Value)));
        Assert.True(answer.Status == HttpStatusCode.OK, answer.Body);
        return JsonDocument.Parse(answer.Body).RootElement.Clone();
    }

    private static string[] Ids(JsonElement list) => [.. list.GetProperty("runs").EnumerateArray().Select(static r => r.GetProperty("id").GetString()!)];

    private static string? Code(CallToolResult result) => result.StructuredContent is { } s && s.TryGetProperty("code", out var code) ? code.GetString() : null;
}
