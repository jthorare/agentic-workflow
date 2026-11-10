using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.Tests.Context.Concepts;
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
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        page.AnchorText = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

        await page.Submit(Token);

        Assert.Equal("No map yet. Build one to see what your repo is made of.", page.EmptyTitle);
    }

    [Fact]
    public void The_search_box_label_is_Search() =>
        Assert.Equal("Search", Labels.ContextMapStartsAt);
}
