using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>See its context (#1046): the Context page says what it is doing for the item. It shows a loading line while the item is read, the item's repo map with a note when nothing is scoped yet, and the reason when a read fails.</summary>
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
    public async Task An_item_that_cannot_be_read_still_names_itself_and_opens_the_map()
    {
        var api = new FakeSindriApi();
        _ = api.Items.Remove(Item);   // the fake serves this id by default: here the item is not served, the scope is, as "no scope"
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(Labels.ContextItemUnscoped(Item, repo: null), page.ItemNote);
    }

    [Fact]
    public async Task The_page_says_it_is_loading_while_the_scope_is_read()
    {
        var api = ApiWithItem();
        var page = Page(api);
        string? during = null;
        api.ItemScopeAnswer = _ =>
        {
            during = page.ItemNote;
            return RouteAnswer.Api("items-id-scope", "no-scope");
        };

        await page.OpenForItem(Item, Token);

        Assert.Equal(Labels.ContextItemLoading(Item), during);
    }

    [Fact]
    public async Task A_failed_read_shows_its_reason_with_the_item()
    {
        var api = ApiWithItem();
        api.ItemScopeAnswer = _ => RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-CTX-500", "the scope store is down");
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.StartsWith(Labels.ContextItemFailed(Item, string.Empty).TrimEnd(), page.ItemNote, StringComparison.Ordinal);
        Assert.Contains("the scope store is down", page.ItemNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_scope_read_still_opens_the_items_repo_map_and_offers_retry()
    {
        var api = ApiWithItem();
        api.ItemScopeAnswer = _ => RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-CTX-500", "the scope store is down");
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.True(page.IsItemFailed);
        Assert.Equal(ItemRepo, api.WholeMapAsked[^1].Repo);
    }

    [Fact]
    public async Task Retry_reads_the_item_again()
    {
        var api = ApiWithItem();
        var failing = true;
        api.ItemScopeAnswer = _ => failing ? RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-CTX-500", "down") : RouteAnswer.Api("items-id-scope", "no-scope");
        var page = Page(api);
        await page.OpenForItem(Item, Token);
        failing = false;

        await page.RetryItem(Token);

        Assert.False(page.IsItemFailed);
        Assert.Equal(Labels.ContextItemUnscoped(Item, ItemRepo), page.ItemNote);
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
