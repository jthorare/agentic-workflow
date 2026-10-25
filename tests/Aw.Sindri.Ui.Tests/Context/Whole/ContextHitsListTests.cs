using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The hits list beside the map: a row per hit, best first, with where it matched and the snippet; a click focuses the node on the map.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextHitsListTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static ContextPageViewModel Page(FakeSindriApi? api = null)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api ?? new FakeSindriApi(), () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static async Task<ContextPageViewModel> Searched(IReadOnlyList<MapSearchHit> hits)
    {
        var page = Page();
        await page.Opened(Token);
        page.MapSearch = new Stub(hits);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        return page;
    }

    [Fact]
    public async Task A_search_lists_its_hits_best_first_with_where_each_matched_and_its_snippet()
    {
        var page = Page();
        await page.Opened(Token);
        var keys = page.Whole.Geometry!.Nodes.Take(2).Select(n => n.Key).ToList();
        page.MapSearch = new Stub([new MapSearchHit(keys[1], "feature", "Second", Area: null, Repo: "r", Score: 2, Matched: "path", Snippet: "src/second.ts"), new MapSearchHit(keys[0], "code", "First", Area: null, Matched: "title", Snippet: "First")]);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.True(page.Hits.IsVisible);
        Assert.Equal(["Second", "First"], page.Hits.Rows.Select(r => r.Name), StringComparer.Ordinal);
        Assert.Equal(("feature", "matched in: path", "src/second.ts"), (page.Hits.Rows[0].Kind, page.Hits.Rows[0].MatchedText, page.Hits.Rows[0].Snippet));
    }

    [Fact]
    public async Task A_hit_from_the_drawn_fallback_has_no_matched_or_snippet_line()
    {
        var page = Page();
        await page.Opened(Token);
        var key = page.Whole.Geometry!.Nodes[0].Key;
        page.MapSearch = new Stub([new MapSearchHit(key, "feature", "Plain", Area: null)]);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((false, false), (page.Hits.Rows[0].HasMatched, page.Hits.Rows[0].HasSnippet));
    }

    [Fact]
    public async Task The_total_shows_only_when_there_were_more_matches_than_rows()
    {
        var more = new MapSearchHit("a", "feature", "A", Area: null);
        var page = await Searched([more]);
        page.Hits.Show([more], total: 132);
        Assert.Equal(("1 of 132", true), (page.Hits.CountText, page.Hits.HasCount));

        page.Hits.Show([more], total: 1);
        Assert.Equal((string.Empty, false), (page.Hits.CountText, page.Hits.HasCount));
    }

    [Fact]
    public async Task The_servers_total_reaches_the_list()
    {
        var body = GoldenFiles.Node("context-search", "get-tab-manager");
        body["total"] = 132;
        var page = Page(new FakeSindriApi { ContextSearchAnswer = _ => new RouteAnswer(HttpStatusCode.OK, body.ToJsonString()) });
        await page.Opened(Token);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal("2 of 132", page.Hits.CountText);
    }

    [Fact]
    public async Task Choosing_a_row_pins_that_node_on_the_map_and_asks_the_camera_to_centre_on_it()
    {
        var page = Page();
        await page.Opened(Token);
        var key = page.Whole.Geometry!.Nodes[0].Key;
        page.MapSearch = new Stub([new MapSearchHit(key, "feature", "One", Area: null)]);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        var centred = 0;
        page.Whole.CentreRequested += (_, _) => centred++;

        page.Hits.Choose(page.Hits.Rows[0]);

        Assert.Equal((key, 1), (page.Whole.Pinned, centred));
    }

    [Fact]
    public async Task Ending_the_search_takes_the_list_away()
    {
        var page = Page();
        await page.Opened(Token);
        page.MapSearch = new Stub([new MapSearchHit(page.Whole.Geometry!.Nodes[0].Key, "feature", "A", Area: null)]);
        page.AnchorText = Anchor;
        await page.Submit(Token);

        Assert.True(page.Back());

        Assert.False(page.Hits.IsVisible);
    }

    [Fact]
    public async Task A_search_with_no_matches_shows_the_note_and_no_list()
    {
        var page = await Searched([]);

        Assert.Equal((false, true), (page.Hits.IsVisible, page.HasSearchNote));
    }

    private sealed class Stub(IReadOnlyList<MapSearchHit> hits) : IMapSearch
    {
        public Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken) => Task.FromResult(hits);
    }
}
