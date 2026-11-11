using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>See its context (#1046): the Context page says what it is doing for the item. It shows the item's repo map with a note that nothing is scoped yet.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextOpenForItemNoteTests
{
    private const string Item = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a304";
    private const string ItemRepo = "agentic-workflow";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static FakeSindriApi ApiWithItem()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = RouteAnswer.SpecShaped("items-id", "new");
        return api;
    }

    [Fact]
    public async Task An_item_with_no_frozen_scope_names_itself_and_opens_its_repos_map()
    {
        var api = ApiWithItem();
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(Labels.ContextItemUnscoped(Item, ItemRepo), page.ItemNote);
        Assert.True(page.HasItemNote);
        Assert.Equal(ItemRepo, api.WholeMapAsked[^1].Repo);
        Assert.Equal(ItemRepo, page.SelectedRepo);
    }

    [Fact]
    public async Task Opening_the_page_again_drops_the_note()
    {
        var page = Page(ApiWithItem());
        await page.OpenForItem(Item, Token);

        await page.Opened(Token);

        Assert.False(page.HasItemNote);
    }
}
