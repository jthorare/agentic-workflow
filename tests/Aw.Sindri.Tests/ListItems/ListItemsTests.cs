using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.ListItems;

/// <summary>The item list (API spec §3.1): open items by default, closed ones only when the filter asks (G11), by repo, oldest first.</summary>
[Lane(TestLane.Unit)]
public sealed class ListItemsTests
{
    private readonly ItemQueryHarness _q = new();

    private ListItemsService Service() => new(_q.Views.Views, _q.Views.Progress, _q.Views.Slice.Callers);

    private async Task<IReadOnlyList<string>> Titles(ListItemsRequest request) =>
        [.. (await Service().Handle(request, ItemQueryHarness.Token)).Value.Items.Select(i => i.Title)];

    [Fact]
    public async Task ListItems_lists_items_filtered_by_state_and_repo()
    {
        await _q.Item('1', "web queued", repo: "web");
        await _q.Item('2', "api queued", repo: "api");
        await _q.Item('3', "web running", repo: "web", state: ItemState.Running);
        using var entered = _q.Human();

        var result = await Service().Handle(A.ListItemsRequest().Build(), ItemQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Sequence(["web queued", "api queued", "web running"], [.. result.Value.Items.Select(i => i.Title)]);
        Assert.Null(result.Value.Empty);
        Expect.Equal(_q.Views.Progress.Current.AsOf, result.Value.AsOf);
    }

    [Fact]
    public async Task Items_state_filter()
    {
        await _q.Item('1', "queued");
        await _q.Item('2', "running", state: ItemState.Running);
        using var entered = _q.Human();

        Expect.Sequence(["running"], [.. await Titles(new ListItemsRequest(State: ItemState.Running, Repo: null))]);
    }

    [Fact]
    public async Task Items_repo_filter()
    {
        await _q.Item('1', "web", repo: "web");
        await _q.Item('2', "api", repo: "api");
        using var entered = _q.Human();

        Expect.Sequence(["api"], [.. await Titles(new ListItemsRequest(State: null, Repo: RepoName.From("api")))]);
    }

    [Fact]
    public async Task Closed_items_appear_only_when_the_state_asks()
    {
        await _q.Item('1', "open");
        await _q.Item('2', "finished", state: ItemState.Done);
        using var entered = _q.Human();

        Expect.Sequence(["open"], [.. await Titles(new ListItemsRequest(State: null, Repo: null))]);
        Expect.Sequence(["finished"], [.. await Titles(new ListItemsRequest(State: ItemState.Done, Repo: null))]);
    }

    [Fact]
    public async Task A_session_lists_every_repo()
    {
        await _q.Item('1', "mine", repo: "alpha");
        await _q.Item('2', "theirs", repo: "beta");
        using var entered = _q.Session("alpha");

        Expect.Sequence(["mine", "theirs"], [.. await Titles(new ListItemsRequest(State: null, Repo: null))]);
    }

    [Fact]
    public async Task No_match_says_why()
    {
        using var entered = _q.Human();

        var result = (await Service().Handle(new ListItemsRequest(State: ItemState.Parked, Repo: RepoName.From("web")), ItemQueryHarness.Token)).Value;

        Assert.Empty(result.Items);
        Expect.Equal(EmptyText.Items(ItemState.Parked, RepoName.From("web")), result.Empty);
    }
}
