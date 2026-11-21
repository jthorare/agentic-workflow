using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>A search that finds nothing (#3677, decision 5): a plain sentence with one action and no refusal banner, and a Did you mean chip puts the node's title in the box, not its key.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextMissTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Tabs { get; } = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static RouteAnswer Unknown(Ui.Api.Context.LensesQuery query) =>
        query.Anchors.Contains(Tabs, StringComparer.Ordinal) ? ContextAnswers.Lenses(query)
            : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-003", "no node is named 'Tab' (fix: sindri context map status)");

    [Fact]
    public async Task A_miss_is_one_plain_sentence_and_not_the_refusal_banner()
    {
        var page = Page(new FakeSindriApi { LensesAnswer = Unknown });
        page.AnchorText = "Tab groups";

        await page.Submit(Token);

        Assert.Equal((true, false, "Nothing matches 'Tab groups'."), (page.IsMiss, page.ShowBanner, page.Problem));
    }

    [Fact]
    public async Task Another_refusal_still_shows_the_banner()
    {
        var page = Page(new FakeSindriApi { LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-CONTEXT-041", "the map can't be read") });
        page.AnchorText = "Tab groups";

        await page.Submit(Token);

        Assert.Equal((false, true), (page.IsMiss, page.ShowBanner));
    }

    [Fact]
    public async Task A_chip_reads_the_key_and_leaves_the_title_in_the_box()
    {
        var asked = new List<IReadOnlyList<string>>();
        var page = Page(new FakeSindriApi { LensesAnswer = q => { asked.Add(q.Anchors); return Unknown(q); } });

        await page.Pick(Tabs, "Tab groups", Token);

        Assert.Equal(([Tabs], "Tab groups"), (Assert.Single(asked), page.AnchorText));
    }
}
