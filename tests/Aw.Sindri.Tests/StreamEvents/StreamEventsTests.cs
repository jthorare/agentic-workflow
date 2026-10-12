using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.StreamEvents;

namespace Aw.Sindri.Tests.StreamEvents;

public sealed class StreamEventsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task StreamEvents_returns_the_events_after_the_cursor_with_their_envelope()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var record = new RecordHeartbeatService(h.Store, h.Events, h.Clock);
        for (var i = 0; i < 3; i++)
        {
            _ = await record.Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, h.Clock.GetUtcNow().AddSeconds(i))), TestContext.Current.CancellationToken);
        }

        var page = (await new StreamEventsService(h.Store).Handle(new StreamEventsRequest(After: 1), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([2L, 3L], page.Events.Select(e => e.Sequence));
        Assert.All(page.Events, e => Assert.Equal(("service.heartbeat", "local", grant.Epoch), (e.Type, e.Tenant, e.Epoch)));
    }
}
