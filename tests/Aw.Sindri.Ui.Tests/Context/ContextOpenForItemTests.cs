using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>Opening the Context page for an item: the page reads the item, opens its repo's map and notes the item as not scoped yet; it asks for no starting points.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextOpenForItemTests
{
    private const string Item = "ITM-0000000000000000000000000000000f";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task An_unknown_item_leaves_the_page_as_it_was()
    {
        var api = new FakeSindriApi();
        _ = api.Items.Remove(Item);
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(string.Empty, page.AnchorText);
        Assert.False(page.IsItemFailed);
    }
}
