using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.GetQueue;

/// <summary>
/// The queue's tag carries the serve process's lease epoch (#4007): a views rebuild after an upgrade replays the same ledger, so the
/// items view's mark comes back the same even when the new build folds other row text.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GetQueueServeEpochTests
{
    private readonly ItemQueryHarness _q = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_tag_from_an_earlier_serve_epoch_gets_a_full_answer()
    {
        await _q.Item('1', "one");
        var first = await Query(new GetQueueRequest(), _q.Views.Slice.Held);
        var held = _q.Views.Slice.Held.Current!;
        var restarted = new HeldLease();
        restarted.Hold(held with { Epoch = held.Epoch + 1 });

        var second = await Query(new GetQueueRequest(IfNoneMatch: first.Tag), restarted);

        Assert.False(second.NotModified);
        Assert.NotEqual(first.Tag, second.Tag, StringComparer.Ordinal);
        Assert.NotEmpty(second.Groups);
    }

    [Fact]
    public async Task The_same_epoch_and_the_same_mark_still_answer_not_modified()
    {
        await _q.Item('1', "one");
        var first = await Query(new GetQueueRequest(), _q.Views.Slice.Held);

        var second = await Query(new GetQueueRequest(IfNoneMatch: first.Tag), _q.Views.Slice.Held);

        Assert.True(second.NotModified);
    }

    private async Task<QueueResponse> Query(GetQueueRequest request, HeldLease lease)
    {
        if (_q.Views.Slice.Held.Current is null)
        {
            _ = await _q.Views.Slice.HoldLease();
        }

        using var entered = _q.Human();
        var result = await new GetQueueService(_q.Views.Views, _q.Views.Progress, _q.Views.Slice.Callers, lease).Handle(request, Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return result.Value;
    }
}
