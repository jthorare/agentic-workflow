using Aw.Context.Core.Extract;
using Aw.Context.Core.Facade;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>A scoping Step's MCP session (issue 1005): X-Sindri-Instance and X-Sindri-Run scope context_node, context_neighbors and context_query to the run's graph and log each read; a session without them reads as before.</summary>
public sealed class ScopedReadsMcpTests : IDisposable
{
    private const string Instance = "ITM-1/line/3/scoping.1";
    private const string Run = "1.1";
    private const string InGraph = "feature:in";
    private const string NotInGraph = "feature:out";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static string? Code(CallToolResult result) => result.StructuredContent is { } s && s.TryGetProperty("code", out var code) ? code.GetString() : null;

    private static (string Name, string Value)[] RunHeaders() => [(SindriHeaders.Instance, Instance), (SindriHeaders.Run, Run)];

    // A map of two unconnected features, each implemented by a file of the repo so a session can see it; the run's graph holds only 'feature:in'.
    private async Task<GoldenHost> Host()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await ContextHosts.Approved(_repo, sessions: true, ct, s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        var file = Path.Combine(host.Home, "scoped-map.yaml");
        await File.WriteAllTextAsync(file, string.Join('\n',
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            $"  - {{key: \"{InGraph}\", label: \"In scope\"}}",
            $"  - {{key: \"{NotInGraph}\", label: \"Out of scope\"}}",
            "  - {key: \"code:src/in.ts\", label: \"in.ts\", provenance: deterministic}",
            "  - {key: \"code:src/out.ts\", label: \"out.ts\", provenance: deterministic}",
            "edges:",
            $"  - {{from: \"code:src/in.ts\", to: \"{InGraph}\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:aw-split-panes/src/in.ts\"}}]}}",
            $"  - {{from: \"code:src/out.ts\", to: \"{NotInGraph}\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:aw-split-panes/src/out.ts\"}}]}}",
            "aliases:",
            $"  - {{alias: \"feature:old.in\", target: \"{InGraph}\"}}",
            $"  - {{alias: \"feature:old.out\", target: \"{NotInGraph}\"}}") + "\n", ct);
        await ContextMapApiTests.Import(host, file);
        await using var scope = host.App.Services.CreateAsyncScope();
        var node = new ScopeGraphNode(ScopeGraphNodeId.From(InGraph), Kind: "feature", Path: null, Symbol: null, Summary: "In scope", Rank: 1, Lens: "product", LensItem: "L-product-1", Band: "own", Citations: []);
        var graph = new ScopeGraphView(Version: 1, MapVersion: "v1", Anchors: [InGraph], Depth: 1, Hops: [], Nodes: [node], Edges: [], Limits: []);
        var kept = await scope.ServiceProvider.GetRequiredService<IContextEngine>().RememberScopeGraph(Instance, graph, ct);
        Assert.True(kept.IsSuccess, kept.IsError ? kept.Error.Message : null);
        return host;
    }

    private static async Task<IReadOnlyList<GraphRead>> Reads(GoldenHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var reads = await scope.ServiceProvider.GetRequiredService<IContextEngine>().GraphReads(Instance, Run, TestContext.Current.CancellationToken);
        Assert.True(reads.IsSuccess, reads.IsError ? reads.Error.Message : null);
        return reads.Value;
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_run_session_reading_a_node_in_its_graph_succeeds_and_the_read_is_logged_for_each_tool()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct, RunHeaders());

        var node = await mcp.Call("context_node", $$"""{"key":"{{InGraph}}"}""", ct);
        var neighbors = await mcp.Call("context_neighbors", $$"""{"key":"{{InGraph}}"}""", ct);
        var query = await mcp.Call("context_query", $$"""{"node":"{{InGraph}}"}""", ct);

        Assert.All([node, neighbors, query], r => Assert.NotEqual(true, r.IsError));
        Assert.Equal([("context_node", false), ("context_neighbors", false), ("context_query", false)], (await Reads(host)).Select(r => (r.Tool, r.Outside)));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_run_session_reading_a_node_outside_its_graph_is_refused_with_154_and_not_logged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct, RunHeaders());

        var node = await mcp.Call("context_node", $$"""{"key":"{{NotInGraph}}"}""", ct);
        var query = await mcp.Call("context_query", $$"""{"node":"{{NotInGraph}}"}""", ct);

        Assert.Equal(("SND-CONTEXT-154", "SND-CONTEXT-154"), (Code(node), Code(query)));
        Assert.Empty(await Reads(host));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Outside_true_with_a_reason_steps_outside_and_the_read_is_logged_with_outside_and_the_reason()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct, RunHeaders());

        var node = await mcp.Call("context_node", $$"""{"key":"{{NotInGraph}}","outside":true,"reason":"the brief names it"}""", ct);

        Assert.NotEqual(true, node.IsError);
        var logged = Assert.Single(await Reads(host));
        Assert.Equal((NotInGraph, "context_node", true, "the brief names it"), (logged.Node, logged.Tool, logged.Outside, logged.Reason));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Outside_true_without_a_reason_is_022_and_nothing_is_logged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct, RunHeaders());

        var node = await mcp.Call("context_node", $$"""{"key":"{{NotInGraph}}","outside":true}""", ct);

        Assert.Equal("SND-CONTEXT-022", Code(node));
        Assert.Empty(await Reads(host));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_session_without_run_headers_reads_any_node_as_before_and_nothing_is_logged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var node = await mcp.Call("context_node", $$"""{"key":"{{NotInGraph}}"}""", ct);

        Assert.NotEqual(true, node.IsError);
        Assert.Empty(await Reads(host));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_run_header_for_an_instance_with_no_remembered_graph_is_not_scoped()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct, (SindriHeaders.Instance, "ITM-9/line/1/scoping.1"), (SindriHeaders.Run, Run));

        var node = await mcp.Call("context_node", $$"""{"key":"{{NotInGraph}}"}""", ct);

        Assert.NotEqual(true, node.IsError);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task An_alias_of_a_node_in_the_graph_is_allowed_on_all_three_tools_and_logged_under_the_node_key_and_an_alias_of_one_outside_is_154()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct, RunHeaders());

        var inside = await mcp.Call("context_node", """{"key":"feature:old.in"}""", ct);
        var neighbors = await mcp.Call("context_neighbors", """{"key":"feature:old.in"}""", ct);
        var outside = await mcp.Call("context_node", """{"key":"feature:old.out"}""", ct);
        var query = await mcp.Call("context_query", """{"node":"feature:old.in"}""", ct);
        var queryOutside = await mcp.Call("context_query", """{"node":"feature:old.out"}""", ct);

        Assert.All([inside, neighbors, query], r => Assert.NotEqual(true, r.IsError));
        Assert.Equal(("SND-CONTEXT-154", "SND-CONTEXT-154"), (Code(outside), Code(queryOutside)));
        Assert.Equal([InGraph, InGraph, InGraph], (await Reads(host)).Select(r => r.Node), StringComparer.Ordinal);
    }
}
