using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Protocol;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>MCP's context_node and context_neighbors (context spec §8.5, S-3M): the services HTTP uses, under the session caller, on a small imported map.</summary>
public sealed class ContextGraphMcpTests : IDisposable
{
    private const string Key = "feature:in";
    private const string InRecord = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02";
    private const string OutRecord = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000b02";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static (string Name, string Value)[] Session() => [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));

    private async Task<GoldenHost> Host(bool sessions = true)
    {
        var host = await ContextHosts.Approved(_repo, sessions, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        var file = Path.Combine(host.Home, "graph-map.yaml");
        await File.WriteAllTextAsync(file, string.Join('\n',
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:in\", label: \"In scope\"}",
            "  - {key: \"feature:out\", label: \"Out of scope\"}",
            "  - {key: \"code:src/in.ts\", label: \"in.ts\", provenance: deterministic}",
            "  - {key: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\", label: \"Mentions in\"}",
            $"  - {{key: \"{OutRecord}\", label: \"OUTONLY-TITLE-ZEBRA\"}}",
            "edges:",
            "  - {from: \"code:src/in.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in.ts\"}]}",
            "  - {from: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\", to: \"feature:in\", type: mentions}",
            $"  - {{from: \"{OutRecord}\", to: \"feature:out\", type: mentions}}") + "\n", TestContext.Current.CancellationToken);
        await ContextMapApiTests.Import(host, file);
        return host;
    }

    private static async Task<JsonNode> Http(GoldenHost host, string route)
    {
        var answer = await host.Http.Ask(Get(route, Session()));
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        return JsonNode.Parse(answer.Body)!;
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Tools_list_gives_node_a_required_key_and_neighbors_a_key_and_a_depth_of_at_least_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: ct);

        Assert.Equal(["context_query", "context_sources", "context_lenses", "context_node", "context_neighbors"], tools.Select(t => t.Name).Take(5), StringComparer.Ordinal);
        var node = tools.Single(t => t.Name == "context_node");
        var neighbors = tools.Single(t => t.Name == "context_neighbors");
        Assert.All([node, neighbors], t =>
        {
            Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint);
            Assert.False(t.JsonSchema.GetProperty("additionalProperties").GetBoolean());
        });
        Assert.Equal(["key"], node.JsonSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        Assert.Equal(["key"], node.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(["key"], neighbors.JsonSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        var depth = neighbors.JsonSchema.GetProperty("properties").GetProperty("depth");
        Assert.Equal(("integer", 0), (depth.GetProperty("type").GetString(), depth.GetProperty("minimum").GetInt32()));
        Assert.Equal(["key", "depth"], neighbors.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_node_equals_the_http_node_for_a_session_fences_labels_and_summarises_counts_only()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var result = await mcp.Call("context_node", $$"""{"key":"{{InRecord}}"}""", ct);
        var http = await Http(host, "/sindri/context/node?key=" + Uri.EscapeDataString(InRecord));

        Assert.NotEqual(true, result.IsError);
        Assert.True(JsonNode.DeepEquals(http, JsonNode.Parse(result.StructuredContent!.Value.GetRawText())));
        Assert.Equal("<untrusted source=\"map\">Mentions in</untrusted>", result.StructuredContent.Value.GetProperty("node").GetProperty("label").GetString());
        var outs = http["out"]!.AsArray().Count;
        var ins = http["in"]!.AsArray().Count;
        var summary = Text(result);
        Assert.Contains(InRecord, summary, StringComparison.Ordinal);
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"{outs} out and {ins} in"), summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Mentions in", summary, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_neighbors_at_depth_2_equals_the_http_neighbors_and_the_summary_has_counts_only()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var result = await mcp.Call("context_neighbors", $$"""{"key":"{{Key}}","depth":2}""", ct);
        var http = await Http(host, "/sindri/context/neighbors?key=" + Uri.EscapeDataString(Key) + "&depth=2");
        var defaulted = await mcp.Call("context_neighbors", $$"""{"key":"{{Key}}"}""", ct);
        var httpDefault = await Http(host, "/sindri/context/neighbors?key=" + Uri.EscapeDataString(Key));

        Assert.NotEqual(true, result.IsError);
        Assert.True(JsonNode.DeepEquals(http, JsonNode.Parse(result.StructuredContent!.Value.GetRawText())));
        Assert.True(JsonNode.DeepEquals(httpDefault, JsonNode.Parse(defaulted.StructuredContent!.Value.GetRawText())));
        var summary = Text(result);
        Assert.Contains(Key, summary, StringComparison.Ordinal);
        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"{http["nodes"]!.AsArray().Count} nodes and {http["edges"]!.AsArray().Count} edges"), summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Mentions in", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("In scope", summary, StringComparison.Ordinal);
        Assert.Contains(result.StructuredContent.Value.GetProperty("nodes").EnumerateArray(), n => n.GetProperty("node").GetProperty("label").GetString() == "<untrusted source=\"map\">Mentions in</untrusted>");
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_node_linked_only_elsewhere_or_unknown_is_003_a_depth_out_of_range_is_006_and_sessions_off_is_020_without_leaking_the_other_record()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);
        await using var off = await Host(sessions: false);
        await using var offMcp = await McpSession.Open(off, FixtureMap.Repo, ct);

        var outside = await mcp.Call("context_node", $$"""{"key":"{{OutRecord}}"}""", ct);
        var unknown = await mcp.Call("context_neighbors", """{"key":"no-such-node"}""", ct);
        var deep = await mcp.Call("context_neighbors", $$"""{"key":"{{Key}}","depth":-1}""", ct);
        var http = await host.Http.Ask(Get("/sindri/context/neighbors?key=" + Uri.EscapeDataString(Key) + "&depth=-1", Session()));

        Assert.Equal(("SND-CONTEXT-003", "SND-CONTEXT-003", "SND-CONTEXT-006"), (Code(outside), Code(unknown), Code(deep)));
        Assert.Equal(http.Code, Code(deep));
        Assert.DoesNotContain("ZEBRA", outside.StructuredContent!.Value.GetRawText() + unknown.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
        Assert.Equal("SND-CONTEXT-020", Code(await offMcp.Call("context_node", $$"""{"key":"{{Key}}"}""", ct)));
        Assert.Equal("SND-CONTEXT-020", Code(await offMcp.Call("context_neighbors", $$"""{"key":"{{Key}}"}""", ct)));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Missing_or_blank_key_an_unknown_argument_and_a_wrong_type_are_022_with_a_fix_hint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var calls = new[]
        {
            await mcp.Call("context_node", "{}", ct),
            await mcp.Call("context_node", """{"key":" "}""", ct),
            await mcp.Call("context_node", $$"""{"key":"{{Key}}","draft":true}""", ct),
            await mcp.Call("context_node", """{"key":5}""", ct),
            await mcp.Call("context_neighbors", "{}", ct),
            await mcp.Call("context_neighbors", """{"key":""}""", ct),
            await mcp.Call("context_neighbors", $$"""{"key":"{{Key}}","draft":true}""", ct),
            await mcp.Call("context_neighbors", $$"""{"key":"{{Key}}","depth":"2"}""", ct),
            await mcp.Call("context_neighbors", $$"""{"key":"{{Key}}","depth":1.5}""", ct),
            await mcp.Call("context_neighbors", """{"key":7,"depth":1}""", ct),
        };

        Assert.All(calls, r =>
        {
            Assert.Equal(true, r.IsError);
            Assert.Equal("SND-CONTEXT-022", Code(r));
            Assert.Contains("fix:", r.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
        });
    }

    private static string? Code(CallToolResult result) =>
        result.StructuredContent is { } s && s.TryGetProperty("code", out var code) ? code.GetString() : null;
}
