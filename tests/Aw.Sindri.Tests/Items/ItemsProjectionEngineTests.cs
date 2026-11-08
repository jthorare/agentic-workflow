using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Items;

/// <summary>The items projection folds a root instance's 'workflow.*' rows (API P11 U2), through the real projections.</summary>
[Lane(TestLane.Integration)]
public sealed class ItemsProjectionEngineTests
{
    private static readonly string _address = "ITM-" + new string('1', 32) + "/step/1";

    [Fact]
    public async Task Queue_groups_a_running_item_under_running()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "waiting item");
        await Fold(q.Views, EventTypes.WorkflowStarted, Wait());
        using var entered = q.Human();

        var result = await new GetQueueService(q.Views.Views, q.Views.Progress, q.Views.Slice.Callers).Handle(new GetQueueRequest(), ItemQueryHarness.Token);

        var running = result.Value.Groups.Single(g => g.Group == QueueGroup.Running);
        Assert.Equal("waiting item", Assert.Single(running.Items).Title);
    }

    [Fact]
    public async Task Rebuilding_views_from_the_ledger_gives_the_same_item_rows()
    {
        var first = await Replay();
        var again = await Replay();

        Assert.Equal(first, again);
        Assert.Contains("\"engine\"", first, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_row_without_an_item_row_is_skipped()
    {
        var views = new ViewsHarness();

        await Fold(views, EventTypes.WorkflowStarted, Wait());   // Fold fails the test when the projection stalls

        var row = await views.Views.Get<ItemRow>(ViewNames.Items, ItemQueryHarness.Id('1').Value, ItemQueryHarness.Token);
        Assert.Null(row.Value);
    }

    private static async Task<string> Replay()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "replayed");
        await Fold(q.Views, EventTypes.WorkflowStarted, Wait());
        await Fold(q.Views, EventTypes.WorkflowTransition, Parked());
        var row = await q.Views.Views.Get<ItemRow>(ViewNames.Items, ItemQueryHarness.Id('1').Value, ItemQueryHarness.Token);
        return JsonSerializer.Serialize(row.Value, SliceHarness.Json);
    }

    private static Task Fold(ViewsHarness views, string type, WorkflowEventPayload payload) =>
        views.Fold(type, payload, ItemQueryHarness.Id('1'));

    private static WorkflowEventPayload Wait() => Row("triaged", new FrameSummary("triaged", 1, Run: null, Child: null), suspension: null);

    private static WorkflowEventPayload Parked() =>
        Row("triaged", frame: null, new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null));

    private static WorkflowEventPayload Row(string to, FrameSummary? frame, SuspensionSummary? suspension)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(_address, "from", to, "step.completed", Outcome: null, Run: null, detail);
    }
}
