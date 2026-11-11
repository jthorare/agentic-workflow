using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Concepts;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page says things in plain words (#2552): no error code in the text, the no-item empty state, the search label.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextPlainWordsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unavailable_concepts_say_it_in_plain_words_and_keep_the_code_for_the_tooltip()
    {
        var api = ConceptPage.Serving(AConcepts.Without("unavailable", "SND-CONTEXT-030"));

        var page = await ConceptPage.Shown(api);

        Assert.Equal("No concepts yet, showing the whole map.", page.Concepts.Note);
        Assert.Equal("SND-CONTEXT-030", page.Concepts.NoteTip);
    }

    [Fact]
    public async Task With_no_item_picked_the_empty_state_asks_for_a_map_not_an_item_map()
    {
        var api = new FakeSindriApi { LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "no map yet") };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }))
        {
            AnchorText = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!,
        };

        await page.Submit(Token);

        Assert.Equal(("No map yet", "Build one to see what your repo is made of and how its parts connect."), (page.EmptyTitle, page.EmptyBody));
    }

    [Fact]
    public async Task With_an_item_opened_the_empty_state_keeps_the_item_words()
    {
        const string item = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a304";
        var api = new FakeSindriApi();
        api.Items[item] = RouteAnswer.SpecShaped("items-id", "new");
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));

        await page.OpenForItem(item, Token);

        Assert.Equal(("No map for this item yet", "Build it to see why this work exists and what it touches."), (page.EmptyTitle, page.EmptyBody));
    }

    [Fact]
    public async Task The_note_tip_is_null_when_the_note_has_no_code()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Without("building", code: null)));

        Assert.Null(page.Concepts.NoteTip);
    }

    [Fact]
    public void The_search_box_label_is_Search() =>
        Assert.Equal("Search", Labels.ContextMapStartsAt);
}
