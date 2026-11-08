using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.RetireInstancesOnStart;

namespace Aw.Sindri.Tests.RetireInstancesOnStart;

public sealed class RetireInstancesOnStartTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Serve_cancels_instances_in_flight_once_with_a_retired_note()
    {
        var grant = await _h.HoldLease();
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new { instance = "T-1/item/1" }, "workflow:T-1/item/1:run-a:1", item: "T-1"), grant, Token);
        var service = new RetireInstancesOnStartService();

        var first = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;
        var restart = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;

        Assert.Equal(1, first.Retired);
        Assert.Equal(0, restart.Retired);
        _ = Assert.Single(_h.Store.Events, e => e.Envelope.Type == EventTypes.WorkflowRetired);
    }
}
