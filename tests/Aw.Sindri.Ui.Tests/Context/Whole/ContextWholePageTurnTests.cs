using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>Page turns on an open feature (#3674): the page asked last is the one that stays, whatever order the answers land in.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholePageTurnTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    // The open feature's golden, saying which of five pages it is.
    private static RouteAnswer AreaPage(WholeMapQuery query)
    {
        if (query.Area is null)
        {
            return ContextAnswers.WholeMap(query);
        }

        var golden = GoldenFiles.Node("context-whole", "area-open");
        golden["area"]!["page"] = query.Page;
        golden["area"]!["pages"] = 5;
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public async Task Page_two_answering_after_page_three_leaves_page_three_on_screen()
    {
        var api = new FakeSindriApi { WholeMapAnswer = AreaPage };
        var page = Page(api);
        await page.Opened(Token);
        await page.OpenWholeArea(ContextAnswers.OpenArea, Token);
        var slow = new TaskCompletionSource();
        api.WholeMapGate = slow;
        var two = page.OpenWholeAreaPage(ContextAnswers.OpenArea, 2, Token);
        api.WholeMapGate = null;

        await page.OpenWholeAreaPage(ContextAnswers.OpenArea, 3, Token);
        slow.SetResult();
        await two;

        Assert.Equal((3, 5), (page.Whole.AreaPage, page.Whole.AreaPages));
    }

    [Fact]
    public async Task Leaving_the_feature_before_its_page_answers_keeps_the_feature_closed()
    {
        var api = new FakeSindriApi { WholeMapAnswer = AreaPage };
        var page = Page(api);
        await page.Opened(Token);
        await page.OpenWholeArea(ContextAnswers.OpenArea, Token);
        var slow = new TaskCompletionSource();
        api.WholeMapGate = slow;
        var two = page.OpenWholeAreaPage(ContextAnswers.OpenArea, 2, Token);
        api.WholeMapGate = null;

        page.MapHome();
        slow.SetResult();
        await two;

        Assert.False(page.Whole.IsAreaOpen);
    }
}
