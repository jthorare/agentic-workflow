using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>/sindri/mcp tools/list after API P8: the seven context tools first, then the eleven sindri_* tools (API spec §8; sindri_projects from the Projects page spec §6.2; sindri_todos and sindri_todo from the todo spec §4.4), then the six connection_* tools (connections PR 10a), then the five memory_* tools (agent-first PR 13), then the four workflow reads and workflow_answer (agent-first PR 8 and PR 9).</summary>
public sealed class SindriToolsListTests(PlainMcpHost plain) : IClassFixture<PlainMcpHost>
{
    private static readonly string[] _expected =
    [
        "context_query", "context_sources", "context_lenses", "context_concepts", "context_pages", "context_node", "context_neighbors",
        "sindri_status", "sindri_queue", "sindri_item", "sindri_item_timeline", "sindri_pending_decisions", "sindri_job", "sindri_projects", "sindri_request", "sindri_handoff", "sindri_todos", "sindri_todo",
        "connection_list", "connection_get", "connection_add", "connection_signin", "connection_test", "connection_remove",
        "memory_list", "memory_create", "memory_search", "memory_write", "memory_forget",
        "workflow_list", "workflow_get", "workflow_runs", "workflow_run_get",
        "workflow_create", "workflow_apply_ops", "workflow_replace", "workflow_validate", "workflow_activate", "workflow_pause", "workflow_delete", "workflow_run", "workflow_cancel", "workflow_docs", "workflow_blocks",
        "workflow_answer",
        "resources_list", "resources_purge",
        "agents_start", "agents_worktree_remove", "agents_stop",
        "toolkit_list", "toolkit_create", "toolkit_build", "toolkit_start", "toolkit_stop", "toolkit_pause", "toolkit_resume", "toolkit_calls",
    ];

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Tools_list_names_the_context_tools_first_then_the_eleven_sindri_tools_then_the_six_connection_tools()
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
    public async Task Every_sindri_query_tool_is_read_only_and_the_request_and_todo_writes_are_not()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = plain.Host;
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var tools = (await mcp.Client.ListToolsAsync(cancellationToken: ct)).Where(t => t.Name.StartsWith("sindri_", StringComparison.Ordinal)).ToList();

        Assert.Equal(11, tools.Count);
        Assert.All(tools.Where(t => t.Name is not ("sindri_request" or "sindri_handoff" or "sindri_todo")), t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint, t.Name));
        var request = tools.Single(t => t.Name == "sindri_request").ProtocolTool.Annotations!;
        Assert.False(request.ReadOnlyHint);
        Assert.False(request.DestructiveHint);
        Assert.False(request.IdempotentHint);
        Assert.False(request.OpenWorldHint);
        var handoff = tools.Single(t => t.Name == "sindri_handoff").ProtocolTool.Annotations!;
        Assert.False(handoff.ReadOnlyHint);
        Assert.False(handoff.DestructiveHint);
        Assert.False(handoff.OpenWorldHint);
        var todo = tools.Single(t => t.Name == "sindri_todo").ProtocolTool.Annotations!;
        Assert.False(todo.ReadOnlyHint);
        Assert.True(todo.DestructiveHint);
        Assert.False(todo.OpenWorldHint);
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
