using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>An item the Context page can't read says so in plain words: an unknown item is named as unknown, any other failure offers Retry, and neither shows the route or the status.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextOpenForItemReadTests
{
    private const string Item = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a305";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task An_item_the_host_does_not_know_is_named_as_unknown_with_no_failure()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-ITEM-404", "No such item.");
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(Labels.ContextItemUnknown(Item), page.ItemNote);
        Assert.False(page.IsItemFailed);
        Assert.DoesNotContain("/sindri/", page.ItemNote, StringComparison.Ordinal);
        Assert.DoesNotContain("answered", page.ItemNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bare_404_on_the_item_read_is_named_as_unknown_too()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = new RouteAnswer(HttpStatusCode.NotFound, string.Empty);
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(Labels.ContextItemUnknown(Item), page.ItemNote);
        Assert.False(page.IsItemFailed);
    }

    [Fact]
    public async Task A_failed_item_read_offers_retry_and_shows_no_route_or_status()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = new RouteAnswer(HttpStatusCode.InternalServerError, string.Empty);
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.True(page.IsItemFailed);
        Assert.DoesNotContain("/sindri/", page.ItemNote, StringComparison.Ordinal);
        Assert.DoesNotContain("answered", page.ItemNote, StringComparison.Ordinal);
    }
}
