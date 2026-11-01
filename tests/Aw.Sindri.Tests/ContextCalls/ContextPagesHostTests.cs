using System.Net;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel;
using Aw.Sindri.Tests.ApiGoldens;
using ModelContextProtocol.Protocol;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>GET /sindri/context/pages and MCP's context_pages, on a host whose engine answers the pages read from a script: the route's JSON, the arguments, the refusals and the scope the call carries.</summary>
public sealed class ContextPagesHostTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();
    private readonly List<(PagesQuery Query, ContextScope Scope)> _asked = [];
    private Result<PagesView> _answer = PagesEngineProxy.OneOwnerPage();

    public void Dispose() => _repo.Dispose();

    private async Task<GoldenHost> Host() => await ContextHosts.Approved(_repo, sessions: true, TestContext.Current.CancellationToken,
        services => RecordingEngine.Install(services, [], pages: (query, scope) =>
        {
            _asked.Add((query, scope));
            return _answer;
        }));

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));

    private static string Code(CallToolResult result) => result.StructuredContent!.Value.GetProperty("code").GetString()!;

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_route_answers_the_pages_view_and_hands_the_filters_to_the_engine()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var answer = await host.Http.Ask(Get("/sindri/context/pages?concept=scheduling&page=schedule-week&marks=suggested&limit=5&offset=10&mapVersion=v3", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var body = JsonDocument.Parse(answer.Body);
        var page = body.RootElement.GetProperty("pages")[0];
        Assert.Equal(("schedule-week", "surface:route/GET:/schedule/week", "owner", true), (page.GetProperty("id").GetString(), page.GetProperty("key").GetString(), page.GetProperty("mark").GetString(), page.GetProperty("code").GetBoolean()));
        var action = page.GetProperty("actions")[0];
        Assert.Equal(("submit-schedule", "scheduling"), (action.GetProperty("id").GetString(), action.GetProperty("concepts")[0].GetString()));
        Assert.Equal((1, false, "v13"), (body.RootElement.GetProperty("total").GetInt32(), body.RootElement.GetProperty("truncated").GetBoolean(), body.RootElement.GetProperty("mapVersion").GetString()));
        var (query, _) = Assert.Single(_asked);
        Assert.Equal(("scheduling", "schedule-week", PageMarks.Suggested, 5, 10, false, "v3"), (query.Concept?.Value, query.Page, query.Marks, query.Limit, query.Offset, query.Draft, query.MapVersion));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_route_reads_both_marks_by_default_and_suggested_true_as_suggested()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        _ = await host.Http.Ask(Get("/sindri/context/pages", Human(host.Token.Value)));
        _ = await host.Http.Ask(Get("/sindri/context/pages?suggested=true", Human(host.Token.Value)));

        Assert.Equal([PageMarks.All, PageMarks.Suggested], _asked.Select(a => a.Query.Marks));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_route_hands_q_to_the_engine()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        _ = await host.Http.Ask(Get("/sindri/context/pages?q=schedul&limit=50", Human(host.Token.Value)));

        var (query, _) = Assert.Single(_asked);
        Assert.Equal(("schedul", 50), (query.Q, query.Limit));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task An_unknown_page_is_a_404_with_257()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        _answer = ContextErrors.PageNotFound.With("no page is named 'nope' (fix: try schedule-week)");
        await using var host = await Host();

        var answer = await host.Http.Ask(Get("/sindri/context/pages?page=nope", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Contains("SND-CONTEXT-257", answer.Body, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_pages_binds_its_arguments_reads_as_session_mcp_and_the_summary_has_ids_and_counts_only()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, "split-panes", ct);

        var result = await mcp.Call("context_pages", """{"concept":"concept:scheduling","page":"schedule-week","repo":"split-panes","limit":3,"offset":1,"mapVersion":"v3"}""", ct);

        Assert.NotEqual(true, result.IsError);
        var (query, scope) = Assert.Single(_asked);
        Assert.Equal(("scheduling", "schedule-week", "split-panes", PageMarks.All, 3, 1, false, "v3"), (query.Concept?.Value, query.Page, query.Repo?.Value, query.Marks, query.Limit, query.Offset, query.Draft, query.MapVersion));
        Assert.Equal(new ContextCaller(ContextCallerKind.Session, ContextRepoName.From("split-panes"), ContextClient.Mcp), scope.Caller);
        var summary = Text(result);
        Assert.Contains("schedule-week: owner, 1 actions, code found", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Schedule week", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Submit schedule", summary, StringComparison.Ordinal);
        Assert.Equal("Schedule week", result.StructuredContent!.Value.GetProperty("pages")[0].GetProperty("name").GetString());
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_pages_refuses_an_unknown_argument_and_a_wrong_type_with_022_and_never_asks_the_engine()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var mcp = await McpSession.Open(host, "split-panes", ct);

        var draft = await mcp.Call("context_pages", """{"draft":true}""", ct);
        var suggested = await mcp.Call("context_pages", """{"suggested":true}""", ct);
        var limit = await mcp.Call("context_pages", """{"limit":"2"}""", ct);
        var offset = await mcp.Call("context_pages", """{"offset":1.5}""", ct);
        var concept = await mcp.Call("context_pages", """{"concept":7}""", ct);

        Assert.All([draft, suggested, limit, offset, concept], r => Assert.Equal("SND-CONTEXT-022", Code(r)));
        Assert.Empty(_asked);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Context_pages_without_a_repo_header_is_refused_before_the_engine_is_asked()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Host();
        await using var bare = await McpSession.Open(host, repo: null, ct);

        var refused = await bare.Call("context_pages", "{}", ct);

        Assert.Equal(true, refused.IsError);
        Assert.Empty(_asked);   // why: the session gate (020) lives in the real engine, which this script replaces
    }
}
