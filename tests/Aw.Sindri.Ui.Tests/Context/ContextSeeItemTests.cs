using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>
/// #1562: See its context enters the page twice at once (the page was shown, and the item is opened: ShellViewModel.Links.cs opens the page and then OpenForItem). The item's answer comes after the whole map was drawn;
/// when the page has settled the map is on the stage and no "Reading the map" row stays under it.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ContextSeeItemTests
{
    private const string Item = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a304";
    private static readonly Uri _url = new("http://127.0.0.1:1/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Opening_the_page_and_the_item_together_leaves_the_map_drawn_and_no_loading_row()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = RouteAnswer.SpecShaped("items-id", "new");
        var itemAnswer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.ItemGates[Item] = itemAnswer;   // the item comes after the map, as it did on the box
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        catalog.Show(_url, api, (await api.Commands(_url, Token)).Value);
        var follows = new List<Func<Uri, CancellationToken, Task>>();
        var page = new ContextPageViewModel(new ContextPorts(api, () => _url, clipboard, clock, catalog, follows.Add));

        var opened = page.Opened(Token);
        var forItem = page.OpenForItem(Item, Token);
        await opened;   // the whole map is drawn while the item is still being read
        itemAnswer.SetResult();
        await forItem;
        foreach (var follow in follows)
        {
            await follow(_url, Token);   // the polls that run after every event
        }

        Assert.Equal((true, false, false), (page.Whole.IsReady, page.Whole.IsLoading, page.ShowWholeStatus));
    }
}
