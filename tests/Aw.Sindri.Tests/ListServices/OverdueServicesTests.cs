using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.ListServices;

/// <summary>GET /services names a slot the scheduler really missed (#1226): its own next slot, still in the past by more than a minute, for a service that is not paused.</summary>
public sealed class OverdueServicesTests
{
    private static readonly ServiceName _heartbeat = ServiceName.From("heartbeat");
    private readonly ViewsHarness _v = new();
    private readonly LiveSchedule _live = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task OverdueSince_is_the_live_slot_when_it_is_five_minutes_past()
    {
        await Approve();
        var slot = _v.Slice.Clock.GetUtcNow().AddMinutes(-5);
        Publish(slot);

        Assert.Equal(slot, (await Listed()).OverdueSince);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task OverdueSince_is_null_when_the_live_slot_is_thirty_seconds_past()
    {
        await Approve();
        Publish(_v.Slice.Clock.GetUtcNow().AddSeconds(-30));   // a busy tick, not a miss

        Assert.Null((await Listed()).OverdueSince);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task OverdueSince_is_null_when_the_live_slot_is_still_to_come()
    {
        await Approve();
        Publish(_v.Slice.Clock.GetUtcNow().AddSeconds(20));

        Assert.Null((await Listed()).OverdueSince);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task OverdueSince_is_null_for_a_paused_service()
    {
        await Approve();
        Publish(_v.Slice.Clock.GetUtcNow().AddMinutes(-5));
        await _v.Fold(EventTypes.ServicePaused, new ServicePaused(_v.Slice.Ids.New<CommandId>(), _heartbeat));

        var heartbeat = await Listed();

        Assert.True(heartbeat.Paused);
        Assert.Null(heartbeat.OverdueSince);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task OverdueSince_is_null_when_the_scheduler_has_published_no_schedule()
    {
        await Approve();

        Assert.Null((await Listed()).OverdueSince);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task OverdueSince_is_null_for_a_service_the_live_schedule_does_not_hold()
    {
        await Approve();
        _live.Publish([new ServiceSchedule(ServiceName.From("digest"), Cadence.Parse("30s").Value, _v.Slice.Clock.GetUtcNow().AddMinutes(-5))]);

        Assert.Null((await Listed()).OverdueSince);
    }

    private void Publish(DateTimeOffset slot) => _live.Publish([new ServiceSchedule(_heartbeat, Cadence.Parse("30s").Value, slot)]);

    private async Task<ServiceView> Listed() =>
        Assert.Single((await new ListServicesService(_v.Slice.Profiles, _v.Views, _v.Progress, new InMemoryJobViews(), [], _live, _v.Slice.Clock).Handle(new ListServicesRequest(), Token)).Value.Services);

    private async Task Approve() => _ = (await _v.Slice.Profiles.Save(SliceHarness.ProfileOf("thor", ("heartbeat", "30s")), Token)).Value;
}
