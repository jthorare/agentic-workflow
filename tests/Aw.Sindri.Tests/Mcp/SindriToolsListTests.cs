using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>/sindri/mcp tools/list after API P8: the six context tools first, then the seven sindri_* tools (API spec §8).</summary>
public sealed class SindriToolsListTests(PlainMcpHost plain) : IClassFixture<PlainMcpHost>
{
    private static readonly string[] _expected =
    [
        "context_query", "context_sources", "context_lenses", "context_concepts", "context_node", "context_neighbors",
        "sindri_status", "sindri_queue", "sindri_item", "sindri_item_timeline", "sindri_pending_decisions", "sindri_job", "sindri_request",
    ];

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Tools_list_names_thirteen_tools_context_first_then_the_seven_sindri_tools()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = plain.Host;
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: ct);

        Assert.Equal(_expected, tools.Select(t => t.Name), StringComparer.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Tools_list_matches_the_golden()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = plain.Host;
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: ct);

        var list = new JsonArray([.. tools.Select(t => (JsonNode?)new JsonObject
        {
            ["name"] = t.Name,
            ["description"] = t.Description,
            ["inputSchema"] = JsonNode.Parse(t.JsonSchema.GetRawText()),
            ["annotations"] = new JsonObject
            {
                ["readOnlyHint"] = t.ProtocolTool.Annotations?.ReadOnlyHint,
                ["destructiveHint"] = t.ProtocolTool.Annotations?.DestructiveHint,
                ["idempotentHint"] = t.ProtocolTool.Annotations?.IdempotentHint,
                ["openWorldHint"] = t.ProtocolTool.Annotations?.OpenWorldHint,
            },
        })]);
        Golden.Match(ApiGolden.Reindent(list.ToJsonString()), typeof(SindriToolsListTests).Assembly, "Golden/Mcp/tools-list/default.json", Normalization.None);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Every_sindri_query_tool_is_read_only_and_sindri_request_is_not()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = plain.Host;
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var tools = (await mcp.Client.ListToolsAsync(cancellationToken: ct)).Where(t => t.Name.StartsWith("sindri_", StringComparison.Ordinal)).ToList();

        Assert.Equal(7, tools.Count);
        Assert.All(tools.Where(t => t.Name != "sindri_request"), t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint, t.Name));
        var request = tools.Single(t => t.Name == "sindri_request").ProtocolTool.Annotations!;
        Assert.False(request.ReadOnlyHint);
        Assert.False(request.DestructiveHint);
        Assert.False(request.IdempotentHint);
        Assert.False(request.OpenWorldHint);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task No_tool_decides_approves_runs_a_job_or_reports_a_step()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = plain.Host;
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: ct);

        string[] verbs = ["decide", "approve", "grant", "accept", "run", "cancel", "complete", "handback", "takeover"];
        foreach (var tool in tools.Where(t => t.Name.StartsWith("sindri_", StringComparison.Ordinal) && t.Name != "sindri_request"))
        {
            Assert.DoesNotContain(verbs, v => (tool.Name + " " + tool.Description).Contains(v, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Sindri_queue_answers_the_queue()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = plain.Host;
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var result = await mcp.Call("sindri_queue", "{}", ct);

        Assert.NotEqual(true, result.IsError);
        Assert.True(result.StructuredContent!.Value.TryGetProperty("groups", out _));
    }
}
