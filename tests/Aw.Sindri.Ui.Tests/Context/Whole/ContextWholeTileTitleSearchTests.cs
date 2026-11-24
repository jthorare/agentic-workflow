using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3084: on the area tiles a search for a tile's own title finds that tile, even when no item of the map matches the words.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeTileTitleSearchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }))
        {
            MapSearch = new NoHits(),
        };
    }

    // The by-feature overview past the tile line (200 nodes): two area tiles, "Tab groups" and "Not linked to a feature yet".
    private static FakeSindriApi Tiles() => new()
    {
        WholeMapAnswer = q => q switch
        {
            { Locate.Count: > 0 } => RouteAnswer.Api("context-whole", "locate"),
            { Area: not null } => ContextAnswers.WholeMap(q),
            _ => Overview(),
        },
    };

    private static RouteAnswer Overview()
    {
        var golden = GoldenFiles.Node("context-whole", "areas-over-budget");
        golden["counts"]!["nodes"] = 200;
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public async Task A_tile_title_typed_in_any_case_lists_that_tile_and_is_no_miss()
    {
        var page = Page(Tiles());
        await page.Opened(Token);
        Assert.True(page.Whole.ShowAreaTiles);
        page.AnchorText = "tab groups";

        await page.Submit(Token);

        Assert.Equal([("Tab groups", true)], page.Whole.Items.Select(i => (i.Label, i.IsTile)));
        Assert.Equal((false, string.Empty), (page.Whole.NoMatches, page.SearchNote));
    }

    [Fact]
    public async Task A_word_of_a_tile_title_on_a_search_list_still_finds_the_tile()
    {
        var page = Page(Tiles());
        await page.Opened(Token);
        page.AnchorText = "tab";
        await page.Submit(Token);
        page.AnchorText = "feature yet";

        await page.Submit(Token);

        Assert.Equal([("Not linked to a feature yet", true)], page.Whole.Items.Select(i => (i.Label, i.IsTile)));
    }

    // No item of the map matches: only a tile's own title can.
    private sealed class NoHits : IMapSearch
    {
        public Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MapSearchHit>>([]);
    }
}
