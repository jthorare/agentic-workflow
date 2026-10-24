using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The page's search box over the whole map: a search beats a pin, no matches show a note, Esc and an empty box return to the full map.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeSearchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static ContextPageViewModel Page()
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(new FakeSindriApi(), () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task A_search_overrides_a_pin_on_the_map()
    {
        var page = Page();
        await page.Opened(Token);
        var node = page.Whole.Geometry!.Nodes[0];
        page.Whole.PointerClicked(node.Rect.CenterX, node.Rect.CenterY);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Null(page.Whole.Pinned);
        Assert.True(page.Whole.SearchActive);
        Assert.False(page.HasSearchNote);
    }

    [Fact]
    public async Task Esc_and_the_clear_button_clear_the_box_and_return_to_the_full_map()
    {
        var page = Page();
        await page.Opened(Token);
        page.AnchorText = Anchor;
        await page.Submit(Token);

        Assert.True(page.Back());

        Assert.Equal((false, string.Empty), (page.Whole.SearchActive, page.AnchorText));
    }

    [Fact]
    public async Task Emptying_the_box_ends_the_search()
    {
        var page = Page();
        await page.Opened(Token);
        page.AnchorText = Anchor;
        await page.Submit(Token);

        page.AnchorText = string.Empty;

        Assert.False(page.Whole.SearchActive);
        Assert.False(page.IsZoomedIn);
    }

    [Fact]
    public async Task A_search_with_nothing_on_the_map_shows_the_no_matches_note()
    {
        var page = Page();
        await page.Opened(Token);
        page.MapSearch = new NoHits();
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((true, Labels.ContextSearchNoMatches), (page.HasSearchNote, page.SearchNote));
    }

    private sealed class NoHits : IMapSearch
    {
        public Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MapSearchHit>>([]);
    }
}
