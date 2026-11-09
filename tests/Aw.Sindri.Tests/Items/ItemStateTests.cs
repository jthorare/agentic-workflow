using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.Items;

/// <summary>An item's state once the recipe engine is gone (Sindri v2 PR 4b): new rows write only New, Done and Cancelled.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemStateTests
{
    private static readonly string _address = "ITM-" + new string('1', 32) + "/step/1";

    [Fact]
    public async Task Items_with_no_engine_rows_read_as_open_or_closed()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "still open");
        await q.Item('2', "finished", state: ItemState.Done);
        await q.Item('3', "dropped", state: ItemState.Cancelled);

        var states = await States(q);

        Assert.Equal([ItemState.New, ItemState.Done, ItemState.Cancelled], states);
    }

    [Fact]
    public async Task Old_workflow_rows_that_end_nothing_leave_an_item_open_and_a_closed_item_closed()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "still open");
        await q.Item('2', "finished", state: ItemState.Done);

        await q.Views.Fold(EventTypes.WorkflowStarted, Old(), ItemQueryHarness.Id('1'));
        await q.Views.Fold(EventTypes.WorkflowTransition, Old(), ItemQueryHarness.Id('1'));
        await q.Views.Fold(EventTypes.WorkflowTransition, Old(), ItemQueryHarness.Id('2'));

        var states = await States(q);

        Assert.Equal([ItemState.New, ItemState.Done], states);
    }

    [Fact]
    public async Task An_old_instance_started_on_a_closed_item_reopens_it_as_main_did()
    {
        var q = new ItemQueryHarness();
        await q.Item('2', "finished", state: ItemState.Done);

        await q.Views.Fold(EventTypes.WorkflowStarted, Old(), ItemQueryHarness.Id('2'));

        var states = await States(q);

        Assert.Equal([ItemState.New], states);
    }

    private static async Task<IReadOnlyList<ItemState>> States(ItemQueryHarness q)
    {
        var rows = (await q.Views.Views.List<ItemRow>(ViewNames.Items, ItemQueryHarness.Token)).Value;
        return [.. rows.OrderBy(static r => r.Id.Value, StringComparer.Ordinal).Select(static r => r.State)];
    }

    // a row an old ledger holds: the instance entered a wait state with an open agent run
    private static WorkflowEventPayload Old()
    {
        var delta = new WorkflowEventDelta(new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Agent), Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(_address, "from", "produce", "step.completed", Outcome: null, Run: null, detail);
    }
}
