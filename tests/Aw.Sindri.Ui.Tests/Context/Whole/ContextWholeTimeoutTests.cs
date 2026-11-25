using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#4363: a whole-map read that passed the app's deadline says it took too long, and any other failure keeps the plain line.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeTimeoutTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ContextPageViewModel> Open(RouteAnswer answer)
    {
        var api = new FakeSindriApi { WholeMapAnswer = _ => answer };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        await page.Opened(Token);
        return page;
    }

    [Fact]
    public async Task A_read_past_the_deadline_shows_the_timeout_line()
    {
        var page = await Open(RouteAnswer.Problem(HttpStatusCode.GatewayTimeout, "SND-UI-002", "127.0.0.1:7410 didn't answer within 120 s"));

        Assert.Equal((true, Labels.ContextWholeTimedOut), (page.Whole.IsFailed, page.Whole.FailedText));
    }

    [Fact]
    public async Task Any_other_failed_read_keeps_the_plain_line()
    {
        var page = await Open(new RouteAnswer(HttpStatusCode.InternalServerError, "{}"));

        Assert.Equal((true, Labels.ContextWholeFailed), (page.Whole.IsFailed, page.Whole.FailedText));
    }
}
