using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3084: on a real map the page shows area cards and no pins, and a word in a card's title must filter to that card (the server finds no anchor in a plain word).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeAreaSearchTests
{
    private const string TabGroups = "feature:tabs.tab-groups";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The overview by feature (two cards, no concepts, no pins) where no anchor is found for any text: only the cards can answer.
    private static async Task<ContextPageViewModel> Overview()
    {
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q => q.Area is null && q.Locate.Count == 0 ? RouteAnswer.Api("context-whole", "areas-over-budget") : ContextAnswers.WholeMap(q),
            LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-003", "no node is named that"),
        };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
        await page.Opened(Token);
        Assert.True(page.Whole.ByArea);   // why: the arrange is the real map's shape
        Assert.Equal(2, page.Whole.Areas.Count);
        return page;
    }

    [Fact]
    public async Task A_word_from_a_cards_title_filters_to_that_card_and_does_not_say_no_matches()
    {
        var page = await Overview();
        page.AnchorText = "groups";

        await page.Submit(Token);

        Assert.Equal((true, false, false), (page.Whole.SearchActive, page.HasSearchNote, page.HasProblem));
        Assert.Contains(page.Whole.Shown!.Tiles, t => string.Equals(t.Key, TabGroups, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_sentence_with_a_word_from_a_cards_title_filters_to_that_card()
    {
        var page = await Overview();
        page.AnchorText = "change how the tab groups sort";

        await page.Submit(Token);

        Assert.Equal((true, false), (page.Whole.SearchActive, page.HasSearchNote));
        Assert.Contains(page.Whole.Shown!.Tiles, t => string.Equals(t.Key, TabGroups, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Esc_after_a_card_search_shows_every_card_again()
    {
        var page = await Overview();
        page.AnchorText = "groups";
        await page.Submit(Token);

        Assert.True(page.Back());

        Assert.Equal((false, 2), (page.Whole.SearchActive, page.Whole.Shown!.Tiles.Count));
    }
}
