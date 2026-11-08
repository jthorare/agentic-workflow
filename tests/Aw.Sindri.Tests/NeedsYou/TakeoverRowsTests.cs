using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The needs-you row of a taken-over instance (API spec section 3.6; API P11 U3): desk task, takeover, idle reminder, handback, cancel, through the real projections.</summary>
[Lane(TestLane.Integration)]
public sealed class TakeoverRowsTests
{
    private static readonly string _address = "ITM-" + new string('1', 32) + "/step/1";
    private static readonly FrameSummary _produce = new("produce", 1, "1.1", Child: null, StepRuntime.Desk);

    [Fact]
    public async Task The_row_follows_desk_takeover_idle_reminder_handback_and_cancel()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");
        var steps = new List<string[]>();

        await Fold(q, EventTypes.WorkflowStarted, Row("produce", TriggerNames.StepCompleted, _produce, suspension: null));
        steps.Add(await Prefixes(q));   // an open desk task
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", TriggerNames.Takeover, frame: null, Takeover()));
        steps.Add(await Prefixes(q));   // taken over: the desk task is the human's own, and no reminder yet
        await Fold(q, EventTypes.WorkflowNotified, Row("produce", TriggerNames.TakeoverIdle, frame: null, Takeover()));
        steps.Add(await Prefixes(q));   // the idle reminder
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", TriggerNames.Handback, frame: null, suspension: null));
        steps.Add(await Prefixes(q));   // handed back: the reminder clears and the desk task is open again
        await Fold(q, EventTypes.WorkflowTransition, Row("done", TriggerNames.Cancel, new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "cancelled"));
        steps.Add(await Prefixes(q));   // cancelled: nothing needs you

        Assert.Equal(["desk:"], steps[0]);
        Assert.Empty(steps[1]);
        Assert.Equal(["takeover:"], steps[2]);
        Assert.Equal(["desk:"], steps[3]);
        Assert.Empty(steps[4]);
    }

    [Fact]
    public async Task The_idle_reminder_stays_across_a_later_row_while_the_instance_is_taken_over_and_clears_on_cancel()
    {
        var q = new ItemQueryHarness();
        await q.Item('1', "the item", repo: "agentic-workflow");
        await Fold(q, EventTypes.WorkflowStarted, Row("produce", TriggerNames.StepCompleted, _produce, suspension: null));
        await Fold(q, EventTypes.WorkflowTransition, Row("produce", TriggerNames.Takeover, frame: null, Takeover()));
        await Fold(q, EventTypes.WorkflowNotified, Row("produce", TriggerNames.TakeoverIdle, frame: null, Takeover()));

        await Fold(q, EventTypes.WorkflowTransition, Row("produce", "command.note", frame: null, Takeover()));
        var during = await Prefixes(q);
        await Fold(q, EventTypes.WorkflowTransition, Row("done", TriggerNames.Cancel, new FrameSummary("done", 2, Run: null, Child: null), suspension: null, outcome: "cancelled"));

        Assert.Equal(["takeover:"], during);
        Assert.Empty(await Prefixes(q));
    }

    private static async Task<string[]> Prefixes(ItemQueryHarness q)
    {
        var rows = await q.Views.Views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, ItemQueryHarness.Token);
        return [.. rows.Value.Select(static r => r.Row.Id.Value[..(r.Row.Id.Value.IndexOf(':', StringComparison.Ordinal) + 1)])];
    }

    private static Task Fold(ItemQueryHarness q, string type, WorkflowEventPayload payload) => q.Views.Fold(type, payload, ItemQueryHarness.Id('1'));

    private static SuspensionSummary Takeover() => new(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, Mirrors: null);

    private static WorkflowEventPayload Row(string to, string trigger, FrameSummary? frame, SuspensionSummary? suspension, string? outcome = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(_address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
