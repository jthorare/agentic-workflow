using System.Net;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>GET and QUERY /sindri/context/search: ranked full-text search over the map, one handler for both transports, the session's scope, 006 for a question with no words.</summary>
public sealed class ContextSearchApiTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private async Task<GoldenHost> Host()
    {
        var host = await ContextHosts.Approved(_repo, sessions: true, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        return host;
    }

    private static (string Name, string Value)[] Terminal(GoldenHost host) => Human(host.Token.Value);

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Search_get_answers_hits_best_first_with_the_total()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var answer = await host.Http.Ask(Get("/sindri/context/search?q=tabs&limit=3", Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var view = Json(answer.Body);
        var hits = view.GetProperty("hits").EnumerateArray().ToList();
        Assert.True(hits[0].GetProperty("key").GetString() == "feature:tabs.tab-manager", answer.Body);   // the body names every hit, score and matched field
        Assert.Equal("title", hits[0].GetProperty("matched").GetString());
        Assert.True(hits.Count <= 3);
        Assert.True(view.GetProperty("total").GetInt32() >= hits.Count);
        Assert.Equal(hits.Select(h => h.GetProperty("score").GetDouble()).OrderDescending(), hits.Select(h => h.GetProperty("score").GetDouble()));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Search_get_and_query_for_the_same_question_answer_byte_identical_bodies()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var get = await host.Http.Ask(Get("/sindri/context/search?q=tab&limit=10", Terminal(host)));
        var query = await host.Http.Ask(Query("/sindri/context/search", """{"q":"tab","limit":10}""", Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, query.Status);
        Assert.Equal(get.Body, query.Body);
    }

    [Theory(Timeout = 120_000), Trait("Lane", "integration")]
    [InlineData("/sindri/context/search")]
    [InlineData("/sindri/context/search?q=")]
    [InlineData("/sindri/context/search?q=%21%21")]
    [InlineData("/sindri/context/search?q=tab&limit=0")]
    [InlineData("/sindri/context/search?q=tab&limit=201")]
    [InlineData("/sindri/context/search?q=tab&draft=true&mapVersion=v1")]
    public async Task Search_without_words_or_with_a_bad_limit_is_006(string route)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var refused = await host.Http.Ask(Get(route, Terminal(host)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (refused.Status, refused.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Search_query_with_no_words_in_the_body_is_006_and_a_foreign_origin_is_refused()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var none = await host.Http.Ask(Query("/sindri/context/search", "{}", Terminal(host)));
        var foreign = await host.Http.Ask(Query("/sindri/context/search", """{"q":"tab"}""", [.. Terminal(host), ("Origin", "http://localhost:3000")]));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (none.Status, none.Code));
        Assert.Equal((HttpStatusCode.Forbidden, "SND-API-008"), (foreign.Status, foreign.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Search_post_is_not_a_route()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var post = await host.Http.Ask(Post("/sindri/context/search", """{"q":"tab"}""", Terminal(host)));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.Status);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Search_a_session_of_the_mapped_repo_finds_hits_and_a_session_of_an_unmapped_repo_is_003()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();
        (string Name, string Value)[] other = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "other")];
        (string Name, string Value)[] own = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

        var mine = await host.Http.Ask(Get("/sindri/context/search?q=tab", own));
        var theirs = await host.Http.Ask(Get("/sindri/context/search?q=tab", other));

        Assert.True(mine.Status == HttpStatusCode.OK, "own repo: " + mine.Body);
        Assert.True(Json(mine.Body).GetProperty("total").GetInt32() > 0, "own repo: " + mine.Body);
        // Like every context read, a session of a repo the profile doesn't map is SND-CONTEXT-003 (404), not an empty answer.
        Assert.True((theirs.Status, theirs.Code) == (HttpStatusCode.NotFound, "SND-CONTEXT-003"), "other repo: " + theirs.Body);
    }
}
