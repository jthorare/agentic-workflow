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
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new WorkflowEventPayload("T-1/item/1", "", "produce", "start", Outcome: null, Run: null), "workflow:T-1/item/1:run-a:1", item: "T-1"), grant, Token);
        var service = new RetireInstancesOnStartService(_h.Store, _h.Held, _h.Events);

        var first = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;
        var restart = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;

        Assert.Equal(1, first.Retired);
        Assert.Equal(0, restart.Retired);
        _ = Assert.Single(_h.Store.Events, e => e.Envelope.Type == EventTypes.WorkflowRetired);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_finished_instance_gets_no_note_and_an_open_one_exactly_one()
    {
        var grant = await _h.HoldLease();
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new WorkflowEventPayload("T-1/item/1", "", "running", "start", Outcome: null, Run: null), "workflow:T-1/item/1:run-a:1", item: "T-1"), grant, Token);
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowTransition, new WorkflowEventPayload("T-1/item/1", "running", "done", "report", Outcome: "resolved", Run: null), "workflow:T-1/item/1:run-a:2", item: "T-1"), grant, Token);
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new WorkflowEventPayload("T-2/item/1", "", "running", "start", Outcome: null, Run: null), "workflow:T-2/item/1:run-a:1", item: "T-2"), grant, Token);
        var service = new RetireInstancesOnStartService(_h.Store, _h.Held, _h.Events);

        var first = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;
        var restart = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;

        Assert.Equal(1, first.Retired);
        Assert.Equal(0, restart.Retired);
        var note = Assert.Single(_h.Store.Events, e => e.Envelope.Type == EventTypes.WorkflowRetired);
        Assert.Equal("T-2", note.Item);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_pass_runs_once_so_an_instance_started_after_it_is_left_alone()
    {
        var grant = await _h.HoldLease();
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new WorkflowEventPayload("T-1/item/1", "", "running", "start", Outcome: null, Run: null), "workflow:T-1/item/1:run-a:1", item: "T-1"), grant, Token);
        var service = new RetireInstancesOnStartService(_h.Store, _h.Held, _h.Events);
        _ = await service.Handle(new RetireInstancesOnStartRequest(), Token);
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new WorkflowEventPayload("T-9/item/1", "", "running", "start", Outcome: null, Run: null), "workflow:T-9/item/1:run-a:1", item: "T-9"), grant, Token);

        var restart = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;

        Assert.Equal(0, restart.Retired);
        Assert.DoesNotContain(_h.Store.Events, e => e.Envelope.Type == EventTypes.WorkflowRetired && e.Item == "T-9");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_graph_runs_rows_are_never_retired_and_a_recipe_era_instance_beside_it_is()
    {
        var grant = await _h.HoldLease();
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new WorkflowEventPayload("nightly-build@3", "", "running", "start", Outcome: null, Run: null), "workflow:nightly-build@3:start", item: "T-5"), grant, Token);
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new WorkflowEventPayload("T-2/item/1", "", "running", "start", Outcome: null, Run: null), "workflow:T-2/item/1:run-a:1", item: "T-2"), grant, Token);
        var service = new RetireInstancesOnStartService(_h.Store, _h.Held, _h.Events);

        var pass = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;

        Assert.Equal(1, pass.Retired);
        var note = Assert.Single(_h.Store.Events, e => e.Envelope.Type == EventTypes.WorkflowRetired);
        Assert.Equal("T-2", note.Item);
    }
}
