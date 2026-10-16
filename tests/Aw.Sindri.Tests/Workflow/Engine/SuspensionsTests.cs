using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.FrameFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Entering states, and an instance's suspension and pending park (spec §3.8, §5.1, §5.8, rev 8).</summary>
public sealed class SuspensionsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Enter_opens_a_new_entry_with_its_run()
    {
        var entered = StateEntry.Enter(Producing(), Recipe, "produce", FrameFixture.Context());

        Assert.Equal("produce", entered.State);
        Assert.Equal(new Frame("produce", 4, new OpenRun(RunToken.For(4, 1), StepRuntime.Agent, Launch: null, Task: null), Child: null), entered.Frame);
        Assert.Null(entered.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Enter_runs_a_desk_capable_step_on_the_desk()
    {
        var entered = StateEntry.Enter(Producing(), Recipe, "checks", FrameFixture.Context());

        Assert.Equal(new OpenRun(RunToken.For(4, 1), StepRuntime.Desk, Launch: null, "Check."), entered.Frame.Run);
    }

    [Fact, Trait("Lane", "unit")]
    public void Enter_a_terminal_state_sets_the_outcome_and_opens_no_run()
    {
        var entered = StateEntry.Enter(Producing(), Recipe, "done", FrameFixture.Context());

        Assert.Equal(OutcomeName.From("resolved"), entered.Outcome);
        Assert.Null(entered.Frame.Run);
    }

    [Fact, Trait("Lane", "unit")]
    public void Park_shows_parked_and_keeps_the_frame()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));

        Assert.Equal(EngineStates.Parked, parked.State);
        Assert.Equal(ParkReason.Timeout, parked.Park);
        Assert.Equal(Producing().Frame, parked.Frame);
        Assert.Null(parked.PendingPark);
    }

    [Fact, Trait("Lane", "unit")]
    public void Takeover_of_a_parked_instance_returns_to_the_park()
    {
        var park = Park(ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun);
        var parked = Suspended(park);

        var taken = Suspensions.TakeOver(parked, "human:cli").Value;
        var back = Suspensions.End(taken, child: null).Value;

        Assert.Equal(EngineStates.TakenOver, taken.State);
        Assert.Equal(park, taken.PendingPark);
        Assert.Equal(ResumeKind.Return, back.Suspension.Resume.Kind);
        Assert.True(back.PendingTookEffect);
        Assert.Equal(parked, back.Instance);
    }

    [Fact, Trait("Lane", "unit")]
    public void Takeover_of_a_taken_over_or_ended_instance_is_refused()
    {
        var taken = Suspensions.TakeOver(Producing(), "human:cli").Value;

        Assert.Equal("SND-WF-003", Suspensions.TakeOver(taken, "human:cli").Error.Code);
        Assert.Equal("SND-WF-003", Suspensions.TakeOver(Suspensions.Cancel(Producing()), "human:cli").Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ending_with_nothing_suspended_is_refused()
    {
        var refused = Suspensions.End(Producing(), child: null);

        Assert.Equal("SND-WF-003", refused.Error.Code);
        Assert.Contains("(fix: ", refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void End_keeps_entry_run_and_child()
    {
        var waiting = Instance(new Frame("await", 7, Run: null, ChildId));
        var parked = Suspended(Park(ParkReason.NeedsAnswer, ParkKind.Wait, ResumeKind.Reevaluate, CiFinished()), waiting);

        var ended = Suspensions.End(parked, child: null).Value;

        Assert.Equal(waiting, ended.Instance);
        Assert.Equal(ResumeKind.Reevaluate, ended.Suspension.Resume.Kind);
        Assert.False(ended.PendingTookEffect);
    }

    [Fact, Trait("Lane", "unit")]
    public void End_keeps_the_open_run_token()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));

        var ended = Suspensions.End(parked, child: null).Value;

        Assert.Equal(Producing().Frame.Run, ended.Instance.Frame.Run);
    }

    [Fact, Trait("Lane", "unit")]
    public void Park_on_a_parked_instance_waits_as_pending()
    {
        var parked = Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm));
        var drift = Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm);

        var pending = Suspensions.Suspend(parked, drift);
        var ended = Suspensions.End(pending, child: null).Value;

        Assert.Equal(ParkReason.Timeout, pending.Park);
        Assert.Equal(drift, pending.PendingPark);
        Assert.True(ended.PendingTookEffect);
        Assert.Equal(drift, ended.Instance.Suspension);
        Assert.Null(ended.Instance.PendingPark);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_park_while_a_pending_park_waits_queues_behind_it_and_the_first_stays_first()
    {
        var drift = Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm);
        var pending = Suspensions.Suspend(Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm)), drift);

        var budget = Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);
        var again = Suspensions.Suspend(pending, budget);

        // §3.8, §5.8: the pending park takes effect first; a later park queues behind it and is not dropped.
        Assert.Equal([drift, budget], Suspensions.PendingParks(again));
        Assert.Same(again, Suspensions.Suspend(again, budget));
    }

    [Fact, Trait("Lane", "unit")]
    public void Park_on_a_taken_over_instance_takes_effect_on_handback()
    {
        var taken = Suspensions.TakeOver(Producing(), "human:cli").Value;
        var budget = Park(ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);

        var pending = Suspensions.Suspend(taken, budget);
        var back = Suspensions.End(pending, child: null).Value;

        Assert.Equal(EngineStates.TakenOver, pending.State);
        Assert.Equal(budget, back.Instance.Suspension);
    }

    [Fact, Trait("Lane", "unit")]
    public void Takeover_over_a_park_with_a_pending_drift_park_keeps_the_park()
    {
        var timeout = Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm);
        var drifting = Suspensions.Suspend(Suspended(timeout), Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm));

        var taken = Suspensions.TakeOver(drifting, "human:cli").Value;

        // Two fields hold two of the three: the park the human answers stays; the drift park goes, and the next
        // decision parks or refuses again while the drift lasts (§4.1).
        Assert.Equal(timeout, taken.PendingPark);
        Assert.Equal(timeout, Suspensions.End(taken, child: null).Value.Instance.Suspension);
    }

    [Fact, Trait("Lane", "unit")]
    public void Revoke_park_replaces_the_suspension_and_ends_a_takeover()
    {
        var taken = Suspensions.TakeOver(Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm)), "human:cli").Value;
        var revoke = Park(ParkReason.SpecConflict, ParkKind.Revoked, ResumeKind.None);

        var revoked = Suspensions.Suspend(taken, revoke);

        Assert.Equal(EngineStates.Parked, revoked.State);
        Assert.Equal(revoke, revoked.Suspension);
        Assert.Null(revoked.PendingPark);
        Assert.Equal(Producing().Frame, revoked.Frame);
    }

    [Fact, Trait("Lane", "unit")]
    public void Drift_park_on_a_drift_park_is_absorbed()
    {
        var drifted = Suspended(Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm));

        var again = Suspensions.Suspend(drifted, Park(ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm));

        Assert.Same(drifted, again);
    }

    [Fact, Trait("Lane", "unit")]
    public void Mirrored_suspension_ends_by_its_child_only()
    {
        var waiting = Instance(new Frame("await", 7, Run: null, ChildId));
        var mirrored = Suspended(MirrorChild(ParkReason.NoProgress), waiting);

        Assert.Equal("SND-WF-003", Suspensions.End(mirrored, child: null).Error.Code);
        Assert.Equal(waiting, Suspensions.End(mirrored, ChildId).Value.Instance);
    }

    [Fact, Trait("Lane", "unit")]
    public void Cancel_drops_the_suspension_and_ends_cancelled()
    {
        var taken = Suspensions.TakeOver(Suspended(Park(ParkReason.Timeout, kind: null, ResumeKind.Rearm)), "human:cli").Value;

        var cancelled = Suspensions.Cancel(taken);

        Assert.Equal(EngineStates.Cancelled, cancelled.State);
        Assert.Equal(OutcomeName.From("cancelled"), cancelled.Outcome);
        Assert.Null(cancelled.Suspension);
        Assert.Null(cancelled.PendingPark);
        Assert.Null(cancelled.Frame.Run);
        Assert.True(cancelled.Ended);
    }
}
