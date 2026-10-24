using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.GetItem;

/// <summary>One item (API spec §3.1): its version for a close, the actions its state offers, and the body only here (U2-API-7).</summary>
[Lane(TestLane.Unit)]
public sealed class GetItemTests
{
    private readonly ItemQueryHarness _q = new();

    private GetItemService Service() => new(new ViewItemResolver(_q.Views.Views), _q.Views.Progress, _q.Views.Slice.Callers, new EmptyLaunchSource());

    [Fact]
    public async Task GetItem_shows_the_item_with_its_version_and_actions()
    {
        await _q.Item('1', "Ship it", repo: "web");
        using var entered = _q.Human();

        var result = await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var item = result.Value;
        Expect.Equal(("Ship it", 1L, ItemState.New), (item.Title, item.Version, item.State));
        Expect.Equal(RepoName.From("web"), item.Repo);
        Expect.Sequence(ItemActions.For(ItemState.New), [.. item.Actions]);
        Expect.Equal(_q.Views.Progress.Current.AsOf, item.AsOf);
    }

    [Fact]
    public async Task Item_actions_follow_its_state()
    {
        await _q.Item('1', "finished", state: ItemState.Done);
        using var entered = _q.Human();

        var item = (await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token)).Value;

        Assert.Empty(item.Actions);
        Assert.Null(item.PrimaryAction);
    }

    [Fact]
    public async Task Body_and_link_only_in_GetItem()
    {
        await _q.Item('1', "Ship it");
        using var entered = _q.Human();

        var item = (await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token)).Value;

        Expect.Equal("the body", item.Body);
        var fields = typeof(ItemSummary).GetProperties().Select(static p => p.Name).ToList();
        Assert.DoesNotContain("Body", fields, StringComparer.Ordinal);
        Assert.DoesNotContain("Link", fields, StringComparer.Ordinal);
        Assert.DoesNotContain("Version", fields, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Session_outside_the_repo_sees_the_item_like_a_human()
    {
        await _q.Item('1', "secret", repo: "alpha");
        using var entered = _q.Session("beta");

        var result = await Service().Handle(A.GetItemRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token);

        Expect.Equal("secret", result.Value.Title);
    }

    /// <summary>The launch block is decided elsewhere (LaunchOptionsTests); here an item only has to carry it.</summary>
    private sealed class EmptyLaunchSource : IItemLaunchSource
    {
        public ValueTask<Result<ItemLaunch>> For(ItemRow item, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ItemLaunch>>(new ItemLaunch(Modes: [], AgentId.From("claude-code"), ChosenBy: "builtIn", Attach: null, Resumable: null, InFlight: null, LastStarted: null, Confirm: null, RepoPath: null, Blockers: []));
    }
}
