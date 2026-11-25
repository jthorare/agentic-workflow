using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>Opening the Context page for an item that exists, against a host with no per-item scope route (#2794): the item shows as not scoped yet, with no failure line.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextOpenForItemUnscopedTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task Opening_an_existing_item_shows_it_not_scoped_yet_and_no_failure()
    {
        var item = A.NewItem;
        var repo = (string)System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("items-id", "new"))!["repo"]!;
        var page = Page(new FakeSindriApi());

        await page.OpenForItem(item, Token);

        Assert.False(page.IsItemFailed);
        Assert.Equal(Labels.ContextItemUnscoped(item, repo), page.ItemNote);
    }
}
