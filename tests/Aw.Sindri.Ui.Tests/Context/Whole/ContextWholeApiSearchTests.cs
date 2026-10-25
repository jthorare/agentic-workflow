using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The page's search box over the served search route: a refusal is the banner, not "none of those items are on the map".</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeApiSearchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    [Fact]
    public async Task A_refused_search_shows_the_APIs_words_on_the_page()
    {
        var api = new FakeSindriApi { ContextSearchAnswer = _ => new RouteAnswer(HttpStatusCode.BadRequest, GoldenFiles.Api("context-search", "empty-q-006")) };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
        await page.Opened(Token);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal("the search needs words to find (fix: pass q, for example q=tab manager)", page.Problem);
    }
}
