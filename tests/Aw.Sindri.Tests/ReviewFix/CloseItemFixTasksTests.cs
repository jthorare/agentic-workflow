using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>Closing an item takes its fix task off the head's file (PR review page spec §8.2), so the hook stops showing it.</summary>
[Lane(TestLane.Unit)]
public sealed class CloseItemFixTasksTests
{
    private readonly ViewsHarness _h = new();
    private readonly FakeFixTaskFiles _files = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Closing_the_item_removes_its_task()
    {
        _ = await _h.Slice.HoldLease();
        await _h.Fold(EventTypes.ItemCreated, new ItemCreated(_h.Slice.Ids.New<CommandId>(), A.FirstItemId, "Fix: guard the null", Repo: null, Link: null, Body: null, Owner: null));
        _ = await _files.Add("acme/api:feat/x", A.FirstItemId.Value, 2071, Token);
        using var entered = _h.Slice.Callers.Enter(A.Caller().Human().Build());
        var service = new CloseItemService(new ViewItemResolver(_h.Views), _h.Slice.Store, _h.Slice.Held, _h.Slice.Events, _h.Slice.Callers, _files);

        var result = await service.Handle(A.CloseItemRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Empty(_files.Heads);
    }
}
