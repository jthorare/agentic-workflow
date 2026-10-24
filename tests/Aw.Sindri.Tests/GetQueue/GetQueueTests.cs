using Aw.Kernel.Testing;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.GetQueue;

/// <summary>The queue (API spec §3.6, UI spec E-18): every group in order, even empty; open items only; scoped to a session's repo.</summary>
[Lane(TestLane.Unit)]
public sealed class GetQueueTests
{
    private readonly ItemQueryHarness _q = new();

    private GetQueueService Service() => new(_q.Views.Views, _q.Views.Progress, _q.Views.Slice.Callers);

    [Fact]
    public async Task GetQueue_groups_open_items_in_the_fixed_order_with_their_empty_text()
    {
        await _q.Item('1', "later", state: ItemState.Running);
        await _q.Item('2', "needs you", state: ItemState.NeedsDecision);
        await _q.Item('3', "queued");
        using var entered = _q.Human();

        var result = await Service().Handle(A.GetQueueRequest().Build(), ItemQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Sequence(QueueGroups.Order, [.. result.Value.Groups.Select(g => g.Group)]);
        Expect.Equal("needs you", Assert.Single(result.Value.Groups[0].Items).Title);
        Expect.Equal(EmptyText.Group(QueueGroup.WithYou), result.Value.Groups[1].Empty);
        Assert.Null(result.Value.Groups[0].Empty);
        Assert.Null(result.Value.Empty);
        Expect.Equal(_q.Views.Progress.Current.AsOf, result.Value.AsOf);
    }

    [Fact]
    public async Task Queue_returns_every_group_even_empty()
    {
        using var entered = _q.Human();

        var result = (await Service().Handle(new GetQueueRequest(), ItemQueryHarness.Token)).Value;

        Expect.Equal(QueueGroups.Order.Count, result.Groups.Count);
        Assert.All(result.Groups, g => Assert.NotNull(g.Empty));
        Expect.Equal(EmptyText.Queue(), result.Empty);
    }

    [Fact]
    public async Task Every_group_has_a_sentence()
    {
        using var entered = _q.Human();

        var result = (await Service().Handle(new GetQueueRequest(), ItemQueryHarness.Token)).Value;

        Assert.All(result.Groups, g => Expect.Equal(EmptyText.Group(g.Group), g.Empty));
    }

    [Fact]
    public async Task Within_a_group_oldest_first()
    {
        await _q.Item('2', "second");
        await _q.Item('1', "third");
        using var entered = _q.Human();

        var queued = (await Service().Handle(new GetQueueRequest(), ItemQueryHarness.Token)).Value.Groups.Single(g => g.Group == QueueGroup.Queued);

        Expect.Sequence(["second", "third"], [.. queued.Items.Select(i => i.Title)]);
    }

    [Fact]
    public async Task Closed_items_leave_the_queue()
    {
        await _q.Item('1', "done", state: ItemState.Done);
        await _q.Item('2', "cancelled", state: ItemState.Cancelled);
        using var entered = _q.Human();

        var result = (await Service().Handle(new GetQueueRequest(), ItemQueryHarness.Token)).Value;

        Assert.All(result.Groups, g => Assert.Empty(g.Items));
        Assert.NotNull(result.Empty);
    }

    [Fact]
    public async Task Session_queue_lists_every_repo()
    {
        await _q.Item('1', "mine", repo: "alpha");
        await _q.Item('2', "theirs", repo: "beta");
        await _q.Item('3', "no repo");
        using var entered = _q.Session("alpha");

        var queued = (await Service().Handle(new GetQueueRequest(), ItemQueryHarness.Token)).Value.Groups.Single(g => g.Group == QueueGroup.Queued);

        Expect.Sequence(["mine", "theirs", "no repo"], [.. queued.Items.Select(static i => i.Title)]);
    }

    [Fact]
    public async Task Stalled_views_carry_the_stalled_sentence()
    {
        await _q.Item('1', "queued");
        _q.Views.Publish(stalledAt: 5);
        using var entered = _q.Human();

        var result = (await Service().Handle(new GetQueueRequest(), ItemQueryHarness.Token)).Value;

        Expect.Equal(EmptyText.Stalled(5), result.Empty);
        Assert.True(result.AsOf.Stalled);
    }

    [Fact]
    public async Task Stalled_views_replace_every_groups_sentence_too()
    {
        _q.Views.Publish(stalledAt: 5);
        using var entered = _q.Human();

        var result = (await Service().Handle(new GetQueueRequest(), ItemQueryHarness.Token)).Value;

        Assert.All(result.Groups, g => Expect.Equal(EmptyText.Stalled(5), g.Empty));
    }
}
