using Aw.Sindri.Core;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RecordHeartbeat;

namespace Aw.Sindri.Tests.RecordHeartbeat;

public sealed class RecordHeartbeatTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task RecordHeartbeat_records_a_slot_once_even_when_replayed()
    {
        var grant = await _h.HoldLease();
        var service = new RecordHeartbeatService(_h.Store, _h.Events, _h.Clock);
        var run = new ScheduledRun(grant, _h.Clock.GetUtcNow());

        var first = (await service.Handle(new RecordHeartbeatRequest(run), Token)).Value;
        var replay = (await service.Handle(new RecordHeartbeatRequest(run), Token)).Value;

        Assert.False(first.AlreadyRecorded);
        Assert.Equal(new RecordHeartbeatResponse(first.Sequence, AlreadyRecorded: true), replay);
        _ = Assert.Single(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RecordHeartbeat_under_a_lost_lease_is_fenced_out()
    {
        var grant = await _h.HoldLease();
        _ = await _h.Store.Release(grant, Token);

        var refused = await new RecordHeartbeatService(_h.Store, _h.Events, _h.Clock).Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, _h.Clock.GetUtcNow())), Token);

        Assert.Equal(SindriErrors.LeaseStale.Code, refused.Error.Code);
    }
}
