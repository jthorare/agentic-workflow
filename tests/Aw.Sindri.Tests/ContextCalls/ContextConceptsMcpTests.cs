using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Tests.ApiGoldens;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>MCP's context_concepts, and a concept id as an anchor of context_lenses and context_query (the concept layer's read side), on the concept map.</summary>
public sealed class ContextConceptsMcpTests : IDisposable
{
    private const string Concept = "concept:scheduling";
    private readonly MapTabsRepo _repo = MapTabsRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));

    private static string Code(CallToolResult result) => result.StructuredContent!.Value.GetProperty("code").GetString()!;

    private async Task<GoldenHost> Host(bool sessions = true)
    {
        var host = await ContextHosts.Approved(_repo.Path, sessions, TestContext.Current.CancellationToken, extra: null, MapTabsRepo.Repo);
        await ContextMapApiTests.Import(host, ConceptMapFile.Write(host.Home));
        return host;
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_concepts_lists_the_session_repos_concepts_and_the_summary_has_ids_and_counts_only()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, MapTabsRepo.Repo, ct);

        var result = await mcp.Call("context_concepts", "{}", ct);

        Assert.NotEqual(true, result.IsError);
        var view = result.StructuredContent!.Value;
        Assert.Equal([Concept], view.GetProperty("concepts").EnumerateArray().Select(c => c.GetProperty("id").GetString()), StringComparer.Ordinal);
        Assert.Equal("ok", view.GetProperty("state").GetString());
        var summary = Text(result);
        Assert.Contains(Concept, summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Self scheduling", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("People pick", summary, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_concepts_pages_by_cursor_filters_by_q_and_refuses_a_draft_argument()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, MapTabsRepo.Repo, ct);

        var paged = await mcp.Call("context_concepts", """{"limit":1}""", ct);
        var none = await mcp.Call("context_concepts", """{"q":"zzz"}""", ct);
        var bad = await mcp.Call("context_concepts", """{"cursor":"v9:3"}""", ct);
        var draft = await mcp.Call("context_concepts", """{"draft":true}""", ct);
        var limit = await mcp.Call("context_concepts", """{"limit":"2"}""", ct);

        Assert.False(paged.StructuredContent!.Value.TryGetProperty("next", out _));   // why: one concept is in this session's scope, so the first page is the last
        Assert.Empty(none.StructuredContent!.Value.GetProperty("concepts").EnumerateArray());
        Assert.Equal(("SND-CONTEXT-006", "SND-CONTEXT-022", "SND-CONTEXT-022"), (Code(bad), Code(draft), Code(limit)));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_concept_id_is_an_anchor_of_context_lenses_and_the_node_of_context_query()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, MapTabsRepo.Repo, ct);

        var lenses = await mcp.Call("context_lenses", $$"""{"anchor":"{{Concept}}"}""", ct);
        var query = await mcp.Call("context_query", $$"""{"node":"{{Concept}}"}""", ct);

        Assert.NotEqual(true, lenses.IsError);
        Assert.NotEqual(true, query.IsError);
        Assert.Contains(Concept, Text(lenses), StringComparison.Ordinal);
        Assert.Contains(Concept, Text(query), StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_concepts_without_a_repo_header_is_refused_and_sessions_off_is_020()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var off = await Host(sessions: false);
        await using var bare = await McpSession.Open(host, repo: null, ct);
        await using var offMcp = await McpSession.Open(off, MapTabsRepo.Repo, ct);

        var refused = await bare.Call("context_concepts", "{}", ct);

        Assert.Equal(true, refused.IsError);
        Assert.Equal("SND-CONTEXT-020", Code(await offMcp.Call("context_concepts", "{}", ct)));
    }
}
