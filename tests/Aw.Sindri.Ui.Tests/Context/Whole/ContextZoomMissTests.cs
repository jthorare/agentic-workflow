using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A zoom that finds none of the answer on the whole map is a miss like 003 and 037 (#3060): the plain sentence with its Clear button, never the refusal banner.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextZoomMissTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task A_zoom_that_finds_none_of_the_answer_is_the_miss_sentence_and_Clear_ends_it()
    {
        var page = Page(new FakeSindriApi { WholeMapAnswer = _ => RouteAnswer.Api("context-whole", "draft") });
        await page.Opened(Token);
        page.Whole.Show(WholeMapLayoutTests.Draft("nodes-small") with { Nodes = [], Edges = [] });
        page.AnchorText = Anchor;

        await page.Submit(Token);

        // why: the zoom set its sentence with no code, so it read as a refusal: the banner with no button (#3060)
        Assert.Equal((true, false, Labels.ContextWholeNoMatch), (page.IsMiss, page.ShowBanner, page.Problem));

        page.ClearSearch();

        Assert.Equal((false, false, string.Empty), (page.IsMiss, page.ShowBanner, page.Problem));
    }
}
