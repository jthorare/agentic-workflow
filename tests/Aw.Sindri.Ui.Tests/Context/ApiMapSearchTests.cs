using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The page's search over GET /sindri/context/search: the hits in the order served, what is not sent, and when the drawn search stands in.</summary>
[Lane(TestLane.Unit)]
public sealed class ApiMapSearchTests
{
    private static readonly Uri _api = new("http://127.0.0.1:1/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Golden(string scenario, HttpStatusCode status = HttpStatusCode.OK) => new(status, GoldenFiles.Api("context-search", scenario));

    private static (ApiMapSearch Search, List<string> Problems) Over(FakeSindriApi api, IMapSearch? fallback = null, int limit = 50)
    {
        var problems = new List<string>();
        return (new ApiMapSearch(api, () => _api, () => "aw-split-panes", fallback ?? new Drawn([]), problems.Add, limit), problems);
    }

    [Fact]
    public async Task The_hits_are_mapped_in_the_order_served_with_their_rank_and_match()
    {
        var (search, _) = Over(new FakeSindriApi { ContextSearchAnswer = _ => Golden("get-tab-manager") });

        var hits = await search.Search("tab manager", Token);

        Assert.Equal(
            [
                new MapSearchHit("feature:tabs.tab-manager", "feature", "Tab manager", "area:unassigned", "aw-split-panes", 3.3615, "title", "Tab manager"),
                new MapSearchHit("record:linear:issue/0e5a1b00-0000-4000-8000-000000000203", "record", "Tab manager spec", "area:unassigned", "aw-split-panes", 3.3247, "title", "Tab manager spec"),
            ],
            hits);
    }

    [Fact]
    public async Task An_empty_search_is_not_sent_and_matches_nothing()
    {
        var api = new FakeSindriApi { ContextSearchAnswer = _ => Golden("get-tab-manager") };
        var (search, _) = Over(api);

        var hits = await search.Search("   ", Token);

        Assert.Equal((0, 0), (hits.Count, api.ContextSearchAsked.Count));
    }

    [Fact]
    public async Task The_words_are_trimmed_and_the_repo_travels_with_them()
    {
        var api = new FakeSindriApi { ContextSearchAnswer = _ => Golden("get-tab-manager") };
        var (search, _) = Over(api);

        _ = await search.Search("  tab manager ", Token);

        Assert.Equal(("tab manager", "aw-split-panes"), (api.ContextSearchAsked[0].Q, api.ContextSearchAsked[0].Repo));
    }

    [Theory]
    [InlineData(500, 200)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(30, 30)]
    public async Task The_limit_is_kept_to_one_to_two_hundred(int asked, int sent)
    {
        var api = new FakeSindriApi { ContextSearchAnswer = _ => Golden("get-tab-manager") };
        var (search, _) = Over(api, limit: asked);

        _ = await search.Search("tab", Token);

        Assert.Equal(sent, api.ContextSearchAsked[0].Limit);
    }

    [Fact]
    public async Task Words_over_512_characters_are_not_sent_and_say_so()
    {
        var api = new FakeSindriApi { ContextSearchAnswer = _ => Golden("get-tab-manager") };
        var (search, problems) = Over(api);

        var hits = await search.Search(new string('a', 513), Token);

        Assert.Equal((0, 0, [Labels.ContextSearchTooLong]), (hits.Count, api.ContextSearchAsked.Count, problems));
    }

    [Fact]
    public async Task A_refusal_shows_the_APIs_words_and_matches_nothing()
    {
        var (search, problems) = Over(new FakeSindriApi { ContextSearchAnswer = _ => Golden("empty-q-006", HttpStatusCode.BadRequest) });

        var hits = await search.Search("tab", Token);

        Assert.Equal((0, ["the search needs words to find (fix: pass q, for example q=tab manager)"]), (hits.Count, problems));
    }

    [Fact]
    public async Task A_server_without_the_route_falls_back_to_the_drawn_search()
    {
        var drawn = new MapSearchHit("feature:a", "feature", "A", Area: null);
        var (search, problems) = Over(new FakeSindriApi(), new Drawn([drawn]));

        var hits = await search.Search("tab", Token);

        Assert.Equal(([drawn], 0), (hits, problems.Count));
    }

    private sealed class Drawn(IReadOnlyList<MapSearchHit> hits) : IMapSearch
    {
        public Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken) => Task.FromResult(hits);
    }
}
