using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Tests.Items;

/// <summary>The item state a root instance's ledger rows give an item (API spec section 3.6; API P11 U2).</summary>
[Trait("Lane", "unit")]
public sealed class EngineItemStatesTests
{
    private const string Address = "ITM-11111111111111111111111111111111/step/1";

    [Fact]
    public void Start_into_a_wait_state_reads_running()
    {
        var part = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("triaged", frame: new FrameSummary("triaged", 1, Run: null, Child: null)));

        Assert.Equal(ItemState.Running, EngineItemStates.StateOf(part!));
        Assert.Equal((Address, "triaged"), (part!.Address, part.State));
    }

    [Theory]
    [InlineData(StepRuntime.Desk, ItemState.NeedsAnswer)]
    [InlineData(StepRuntime.Agent, ItemState.Running)]
    public void An_open_run_reads_by_who_runs_it(StepRuntime runtime, ItemState expected)
    {
        var part = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, runtime)));

        Assert.Equal(expected, EngineItemStates.StateOf(part!));
    }

    [Fact]
    public void Open_desk_run_reads_needs_answer_and_a_row_from_before_the_runtime_field_reads_as_desk()
    {
        var part = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null)));

        Assert.Equal(ItemState.NeedsAnswer, EngineItemStates.StateOf(part!));
    }

    [Theory]
    [InlineData(SuspensionKind.Park)]
    [InlineData(SuspensionKind.Escalation)]
    public void Park_and_escalation_read_parked(SuspensionKind kind)
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        var parked = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", suspension: new SuspensionSummary(kind, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null)));

        Assert.Equal(ItemState.Parked, EngineItemStates.StateOf(parked!));
    }

    [Fact]
    public void An_unavailable_park_reads_running_so_it_stays_out_of_the_waiting_on_you_group_and_keeps_its_reason()
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Agent)));

        var parked = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", suspension: new SuspensionSummary(SuspensionKind.Park, ParkReason.Unavailable, ParkKind.Step, Escalation: null, Mirrors: null)));
        var later = EngineItemStates.Next(parked, EventTypes.WorkflowTransition, new WorkflowEventPayload(Address, "produce", "produce", "deadline", Outcome: null, Run: null, Detail: null));

        Assert.Equal((ItemState.Running, ParkReason.Unavailable), (EngineItemStates.StateOf(parked!), parked!.Reason));
        Assert.Equal(QueueGroup.Running, QueueGroups.GroupOf(EngineItemStates.StateOf(parked)));
        Assert.Equal(ParkReason.Unavailable, later!.Reason);   // a row without detail keeps what came before
    }

    [Fact]
    public void A_skill_trial_that_fails_at_launch_ends_its_instance_so_the_item_reads_closed()
    {
        // why: skill flows spec §7 review item 7: a refused launch sends step.failed into the trial's terminal 'failed', which names an outcome, so the item closes without the CLI
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("run", frame: new FrameSummary("run", 1, "1.1", Child: null, StepRuntime.Agent)));

        var failed = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("failed", trigger: "step.failed", outcome: "failed"));

        Assert.True(failed!.Ended);
        Assert.Equal(ItemState.Done, EngineItemStates.StateOf(failed));
    }

    [Fact]
    public void Live_unavailable_parks_are_counted_for_health_the_latest_root_and_an_earlier_one_each()
    {
        var unavailable = new SuspensionSummary(SuspensionKind.Park, ParkReason.Unavailable, ParkKind.Step, Escalation: null, Mirrors: null);
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Agent)));
        var parked = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", suspension: unavailable))!;
        var other = parked with { Address = "ITM-11111111111111111111111111111111/review/1" };

        Assert.Equal((0, 0, 1, 2), (EngineItemStates.Unavailable(part: null), EngineItemStates.Unavailable(started), EngineItemStates.Unavailable(parked), EngineItemStates.Unavailable(parked with { Earlier = [other] })));
        Assert.Equal(0, EngineItemStates.Unavailable(parked with { Ended = true }));
    }

    [Fact]
    public void Takeover_reads_taken_over_and_handback_returns_to_the_frame_state()
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        var taken = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", suspension: new SuspensionSummary(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, Mirrors: null)));
        var handed = EngineItemStates.Next(taken, EventTypes.WorkflowTransition, Row("produce"));

        Assert.Equal(ItemState.TakenOver, EngineItemStates.StateOf(taken!));
        Assert.Equal(ItemState.NeedsAnswer, EngineItemStates.StateOf(handed!));
    }

    [Theory]
    [InlineData(TriggerNames.Cancel, ItemState.Cancelled)]
    [InlineData("step.completed", ItemState.Done)]
    public void Cancel_ends_cancelled_and_any_other_terminal_ends_done(string trigger, ItemState expected)
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        var ended = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("done", trigger: trigger, frame: new FrameSummary("done", 2, Run: null, Child: null), outcome: "resolved"));

        Assert.Equal(expected, EngineItemStates.StateOf(ended!));
    }

    [Fact]
    public void Child_rows_leave_the_item_alone()
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("triaged", frame: new FrameSummary("triaged", 1, Run: null, Child: null)));

        var child = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", address: Address + "/build.1", suspension: new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null)));

        Assert.Null(child);
    }

    private const string Second = "ITM-11111111111111111111111111111111/other/1";

    [Fact]
    public void A_second_root_keeps_the_first_live_and_the_item_follows_the_latest()
    {
        var first = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        var both = EngineItemStates.Next(first, EventTypes.WorkflowStarted, Row("triaged", address: Second, frame: new FrameSummary("triaged", 1, Run: null, Child: null)));

        Assert.Equal((Second, ItemState.Running), (both!.Address, EngineItemStates.StateOf(both)));
        Assert.Equal([Address], both.Earlier!.Select(static p => p.Address), StringComparer.Ordinal);
    }

    [Fact]
    public void An_earlier_root_moves_and_leaves_the_set_when_it_ends_while_the_latest_stays()
    {
        var first = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));
        var both = EngineItemStates.Next(first, EventTypes.WorkflowStarted, Row("triaged", address: Second, frame: new FrameSummary("triaged", 1, Run: null, Child: null)));

        var parked = EngineItemStates.Next(both, EventTypes.WorkflowTransition, Row("produce", suspension: new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null)));
        var ended = EngineItemStates.Next(parked, EventTypes.WorkflowTransition, Row("done", trigger: TriggerNames.Cancel, frame: new FrameSummary("done", 2, Run: null, Child: null), outcome: "cancelled"));

        Assert.Equal((Second, ItemState.Running, SuspensionKind.Park), (parked!.Address, EngineItemStates.StateOf(parked), parked.Earlier!.Single().Suspension));
        Assert.Equal((Second, ItemState.Running), (ended!.Address, EngineItemStates.StateOf(ended)));
        Assert.Null(ended.Earlier);
    }

    [Fact]
    public void A_cancel_row_stored_without_detail_still_ends_the_instance_and_a_park_survives_a_row_without_detail()
    {
        var started = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));
        var parked = EngineItemStates.Next(started, EventTypes.WorkflowTransition, Row("produce", suspension: new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null)));

        var bare = EngineItemStates.Next(parked, EventTypes.WorkflowTransition, Bare("produce", "deadline"));
        var cancelled = EngineItemStates.Next(parked, EventTypes.WorkflowTransition, Bare(EngineStates.Cancelled, TriggerNames.Cancel));

        Assert.Equal(ItemState.Parked, EngineItemStates.StateOf(bare!));
        Assert.Equal(ItemState.Cancelled, EngineItemStates.StateOf(cancelled!));
    }

    private const string Attempt2 = "ITM-11111111111111111111111111111111/step/2";

    [Fact]
    public void A_straggler_row_of_an_earlier_attempt_and_a_row_after_the_end_leave_the_item_alone()
    {
        var first = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("produce", frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));
        var ended = EngineItemStates.Next(first, EventTypes.WorkflowTransition, Row("done", frame: new FrameSummary("done", 2, Run: null, Child: null), outcome: "resolved"));
        var second = EngineItemStates.Next(ended, EventTypes.WorkflowStarted, Row("triaged", address: Attempt2, frame: new FrameSummary("triaged", 1, Run: null, Child: null)));
        var park = new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);

        var late = EngineItemStates.Next(second, EventTypes.WorkflowTransition, Row("produce", suspension: park));   // attempt 1's row, after attempt 2 started
        var afterEnd = EngineItemStates.Next(ended, EventTypes.WorkflowTransition, Row("produce", suspension: park));   // a row after the instance ended

        Assert.Null(late);
        Assert.Null(afterEnd);
        Assert.Null(second!.Earlier);   // the ended first attempt isn't kept
    }

    [Fact]
    public void A_start_after_the_end_reopens_the_item_with_the_new_attempt()
    {
        var first = EngineItemStates.Next(prior: null, EventTypes.WorkflowStarted, Row("done", frame: new FrameSummary("done", 1, Run: null, Child: null), outcome: "resolved"));

        var reopened = EngineItemStates.Next(first, EventTypes.WorkflowStarted, Row("produce", address: Attempt2, frame: new FrameSummary("produce", 1, "1.1", Child: null, StepRuntime.Desk)));

        Assert.Equal((ItemState.Done, Attempt2, ItemState.NeedsAnswer), (EngineItemStates.StateOf(first!), reopened!.Address, EngineItemStates.StateOf(reopened)));
    }

    private static WorkflowEventPayload Bare(string to, string trigger) => new(Address, "from", to, trigger, Outcome: null, Run: null, Detail: null);

    private static WorkflowEventPayload Row(string to, string? address = null, string trigger = "step.completed", FrameSummary? frame = null, SuspensionSummary? suspension = null, string? outcome = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, outcome);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address ?? Address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
