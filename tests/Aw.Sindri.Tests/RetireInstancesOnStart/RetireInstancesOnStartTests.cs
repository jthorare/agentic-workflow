using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
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

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_that_retired_nothing_still_leaves_a_marker_so_the_next_start_reads_no_page()
    {
        var grant = await _h.HoldLease();
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowStarted, new WorkflowEventPayload("T-1/item/1", "", "running", "start", Outcome: null, Run: null), "workflow:T-1/item/1:run-a:1", item: "T-1"), grant, Token);
        _ = await _h.Store.Append(_h.Events.Entry(EventTypes.WorkflowTransition, new WorkflowEventPayload("T-1/item/1", "running", "done", "report", Outcome: "resolved", Run: null), "workflow:T-1/item/1:run-a:2", item: "T-1"), grant, Token);
        var counting = new PageCounting(_h.Store);
        var service = new RetireInstancesOnStartService(counting, _h.Held, _h.Events);

        var first = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;
        var pagesAfterFirst = counting.Pages;
        var second = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;

        Assert.Equal((0, 0), (first.Retired, second.Retired));
        Assert.True(pagesAfterFirst > 0);
        Assert.Equal(pagesAfterFirst, counting.Pages);   // the second start read no page
        _ = Assert.Single(_h.Store.Events, e => e.Envelope.Type == EventTypes.WorkflowRetireDone);
        Assert.DoesNotContain(_h.Store.Events, e => e.Envelope.Type == EventTypes.WorkflowRetired);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ledger_that_never_held_a_recipe_instance_reads_no_page_and_gets_no_marker()
    {
        _ = await _h.HoldLease();
        var counting = new PageCounting(_h.Store);
        var service = new RetireInstancesOnStartService(counting, _h.Held, _h.Events);

        var pass = (await service.Handle(new RetireInstancesOnStartRequest(), Token)).Value;

        Assert.Equal(0, pass.Retired);
        Assert.Equal(0, counting.Pages);
        Assert.DoesNotContain(_h.Store.Events, e => e.Envelope.Type == EventTypes.WorkflowRetireDone);
    }

    /// <summary>The ledger with its paged reads counted, so a test can tell a scan from a lookup.</summary>
    private sealed class PageCounting(ILedgerStore inner) : ILedgerStore
    {
        public int Pages { get; private set; }

        public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken) => inner.Append(entry, grant, cancellationToken);

        public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken)
        {
            Pages++;
            return inner.ReadAfter(sequence, max, cancellationToken);
        }

        public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => inner.Head(cancellationToken);

        public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) => inner.Schema(cancellationToken);
    }
}
