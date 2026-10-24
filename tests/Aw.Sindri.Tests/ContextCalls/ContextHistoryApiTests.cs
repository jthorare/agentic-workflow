using System.Net;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The history lens route on the split-panes fixture repo (five merged pull requests) and its map (scoping spec C5, C10).</summary>
public sealed class ContextHistoryApiTests : IDisposable
{
    private const string Anchor = "feature%3Alayout.split-pane";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private async Task<GoldenHost> Host(bool sessions = true)
    {
        var host = await ContextHosts.Approved(_repo, sessions, TestContext.Current.CancellationToken,
            s => { _ = s.RemoveAll<IClusterSource>(); _ = s.AddSingleton<IClusterSource>(new FakeClusterSource()); }, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        return host;
    }

    private static (string Name, string Value)[] Terminal(GoldenHost host) => Human(host.Token.Value);

    private static (string Name, string Value)[] Session(string repo = FixtureMap.Repo) => [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, repo)];

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    private static string History(params string[] pairs) => "/sindri/context/history?" + string.Join('&', pairs);

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task History_lists_the_landed_pull_requests_newest_first_with_their_numbers_and_the_never_synced_rows()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var answer = await host.Http.Ask(Get(History("anchor=" + Anchor), Terminal(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var view = Json(answer.Body);
        var changes = view.GetProperty("changes").EnumerateArray().ToList();
        Assert.NotEmpty(changes);
        Assert.Equal(changes.Select(c => c.GetProperty("at").GetDateTimeOffset()).OrderDescending(), changes.Select(c => c.GetProperty("at").GetDateTimeOffset()));
        Assert.Contains(changes, c => c.TryGetProperty("pr", out var pr) && pr.ValueKind == JsonValueKind.Number);
        Assert.All(changes, c => Assert.Equal("untrusted", c.GetProperty("trust").GetString()));
        Assert.Equal("not-configured", view.GetProperty("incidents").GetProperty("state").GetString());
        var sources = view.GetProperty("searched").GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToList();
        Assert.Equal(["map", "onyx", "git-history", "incidents"], sources, StringComparer.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Before_drops_the_pull_requests_merged_after_it()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var all = Json((await host.Http.Ask(Get(History("anchor=" + Anchor), Terminal(host)))).Body);
        var cut = Json((await host.Http.Ask(Get(History("anchor=" + Anchor, "before=2026-07-12T00:00:00Z"), Terminal(host)))).Body);

        Assert.True(cut.GetProperty("changes").GetArrayLength() < all.GetProperty("changes").GetArrayLength());
        Assert.All(cut.GetProperty("changes").EnumerateArray(), c => Assert.True(c.GetProperty("at").GetDateTimeOffset() <= DateTimeOffset.Parse("2026-07-12T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Since_keeps_only_the_later_changes()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var view = Json((await host.Http.Ask(Get(History("anchor=" + Anchor, "since=2026-07-20T00:00:00Z"), Terminal(host)))).Body);

        Assert.All(view.GetProperty("changes").EnumerateArray(), c => Assert.True(c.GetProperty("at").GetDateTimeOffset() >= DateTimeOffset.Parse("2026-07-20T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task History_refusals_are_problems_unknown_anchor_003_relative_time_006_not_retained_120_draft_and_version_006()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var unknown = await host.Http.Ask(Get(History("anchor=feature%3Anope.x"), Terminal(host)));
        var relative = await host.Http.Ask(Get(History("anchor=" + Anchor, "since=90d"), Terminal(host)));
        var swapped = await host.Http.Ask(Get(History("anchor=" + Anchor, "since=2026-07-20", "before=2026-07-01"), Terminal(host)));
        var many = await host.Http.Ask(Get(History([.. Enumerable.Range(0, 50).Select(i => "anchor=feature%3Anope." + i.ToString(System.Globalization.CultureInfo.InvariantCulture))]), Terminal(host)));
        var gone = await host.Http.Ask(Get(History("anchor=" + Anchor, "mapVersion=v99"), Terminal(host)));
        var both = await host.Http.Ask(Get(History("anchor=" + Anchor, "draft=true", "mapVersion=v1"), Terminal(host)));
        var none = await host.Http.Ask(Get(History(), Terminal(host)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (unknown.Status, unknown.Code));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (many.Status, many.Code));   // fifty anchors are asked, none is known: never a count refusal
        Assert.All([relative, swapped, both, none], r => Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (r.Status, r.Code)));
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-120"), (gone.Status, gone.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_session_reads_its_repos_scope_and_another_repos_anchor_is_missing()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var inside = await host.Http.Ask(Get(History("anchor=" + Anchor, "anchor=feature%3Aother.thing"), Session()));
        var outside = await host.Http.Ask(Get(History("anchor=" + Anchor), Session("other")));

        Assert.Equal(HttpStatusCode.OK, inside.Status);
        Assert.Equal(["feature:other.thing"], Json(inside.Body).GetProperty("missingAnchors").EnumerateArray().Select(e => e.GetString()), StringComparer.Ordinal);
        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-003"), (outside.Status, outside.Code));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Job_events_invalidate_the_context_lenses_and_history_views()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var catalog = Json((await host.Http.Ask(Get("/sindri/events/catalog", Terminal(host)))).Body);

        var finished = catalog.GetProperty("types").EnumerateArray().Single(t => t.GetProperty("type").GetString() == EventTypes.JobFinished);
        Assert.Contains(finished.GetProperty("invalidates").EnumerateArray().Select(e => e.GetString()), v => v == ApiViews.ContextHistory);
        Assert.Contains(finished.GetProperty("invalidates").EnumerateArray().Select(e => e.GetString()), v => v == ApiViews.ContextLenses);
    }
}
