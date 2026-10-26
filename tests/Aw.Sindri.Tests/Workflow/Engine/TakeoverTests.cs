using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The takeover's timers and triggers (engine spec §3.8, §5.2, §5.5, §5.8; UI spec E-6), over the real rules: a
/// takeover pauses the running timer and a handback resumes what it kept; a takeover over a park has no timer to pause and the
/// handback returns to the park; a park that arrives while taken over waits as the pending park; a state timer during a
/// takeover changes nothing; 'takeoverIdle' records one 'workflow.notified' and changes nothing; an 'open-request' is the
/// engine's takeover, a no-op when already taken over.
/// </summary>
public sealed class TakeoverTests
{
    private const string Engine = "service:engine";
    private const string Timers = "service:workflow.timers";
    private static readonly TimeSpan _floor = TimeSpan.FromDays(2);

    [Fact, Trait("Lane", "unit")]
    public void A_takeover_pauses_the_running_timer_and_the_handback_resumes_what_it_kept()
    {
        var produce = At(Step, "produce");
        var taken = Applied(produce, Takeover());
        var kept = taken with { Suspension = taken.Suspension! with { Remaining = new RemainingTimer(DeadlineKind.State, TimeSpan.FromSeconds(40)) } };
        var back = Applied(kept, Handback());

        Assert.Equal(TimerAction.Pause, StateTimers.Between(produce, taken, Step, _floor));
        Assert.Equal(new TimerAction(TimerActionKind.Resume, DeadlineKind.State, TimeSpan.FromSeconds(40)), StateTimers.Between(kept, back, Step, _floor));
        Assert.Equal(("produce", "1.1"), (back.State, back.Frame.Run?.Token.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_takeover_over_a_park_has_no_timer_to_pause_and_the_handback_returns_to_the_park_with_no_timer()
    {
        var retried = Applied(Agent(At(Step, "verify", entry: 3)), new DeadlineFired(Timers, 3, DeadlineKind.State));   // a park timeout retries once first (owner q93)
        var parked = Applied(retried, new DeadlineFired(Timers, 3, DeadlineKind.Retry));
        var taken = Applied(parked, Takeover());
        var back = Applied(taken, Handback());

        Assert.Equal(TimerAction.Pause, StateTimers.Between(parked, taken, Step, _floor));
        Assert.Equal(new TimerAction(TimerActionKind.Resume, Timer: null, After: null), StateTimers.Between(taken, back, Step, _floor));
        Assert.Equal((EngineStates.Parked, ParkReason.Timeout), (back.State, back.Suspension?.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancel_while_taken_over_stops_every_timer_and_a_change_that_keeps_the_takeover_keeps_them()
    {
        var taken = Applied(At(Step, "produce"), Takeover());
        var cancelled = Applied(taken, new CancelRequested("human:cli", CancelOrigin.Command, Stamp('c'), Sequence: null, Parent: null));

        Assert.Equal(TimerAction.Stop, StateTimers.Between(taken, cancelled, Step, _floor));
        Assert.Equal(TimerAction.Keep, StateTimers.Between(taken, taken with { PendingPark = DeskTimeouts.TimeoutPark("produce", _floor, Timers) }, Step, _floor));
    }

    [Fact, Trait("Lane", "unit")]
    public void Deadline_during_takeover_is_refused_and_a_park_while_taken_over_waits_as_pending()
    {
        var taken = Applied(At(Step, "produce"), Takeover());
        var park = DeskTimeouts.TimeoutPark("produce", _floor, Timers);

        _ = Assert.IsType<Absorbed>(Real(taken, Step, new DeadlineFired(Timers, 1, DeadlineKind.State)));
        var pending = Suspensions.Suspend(taken, park);
        Assert.Equal((EngineStates.TakenOver, park), (pending.State, pending.PendingPark));
        Assert.Equal((EngineStates.Parked, ParkReason.Timeout), (Applied(pending, Handback()).State, Applied(pending, Handback()).Suspension?.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public void Handback_when_not_taken_over_is_SND_WF_003()
    {
        var rejected = Assert.IsType<Rejected>(Real(At(Step, "produce"), Step, Handback()));

        Assert.Equal("SND-WF-003", rejected.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void TakeoverIdle_while_taken_over_records_one_notified_event_and_leaves_the_instance_as_it_is()
    {
        var taken = Applied(At(Step, "produce"), Takeover());
        var back = Applied(taken, Handback());

        var notified = Assert.IsType<Applied>(Real(taken, Step, Idle()));

        var change = Assert.Single(notified.Changes);
        Assert.Equal((WorkflowEventType.Notified, TriggerNames.TakeoverIdle), (change.Event.Type, change.Event.Trigger.Kind));
        Assert.Equal(taken, change.After);
        Assert.Empty(notified.Effects);
        Assert.Equal(TimerAction.Keep, StateTimers.Between(taken, change.After, Step, _floor));
        _ = Assert.IsType<Absorbed>(Real(back, Step, Idle()));
    }

    [Fact, Trait("Lane", "unit")]
    public void Open_request_takes_over_as_the_engine_and_is_a_no_op_when_already_taken_over()
    {
        var produce = At(Step, "produce");
        var open = new OpenRequested(Engine, "LCH-0123456789abcdef0123456789abcdef");

        var taken = Assert.IsType<Applied>(Real(produce, Step, open)).Changes[0];
        var again = Real(taken.After, Step, open);
        var byHand = Real(Applied(produce, Takeover()), Step, open);

        Assert.Equal((EngineStates.TakenOver, Engine), (taken.After.State, taken.After.Suspension?.Actor));
        Assert.Equal(("open-request", "wf:" + produce.Id.Value + ":open:LCH-0123456789abcdef0123456789abcdef"), (taken.Event.Trigger.Kind, taken.Event.Trigger.Key.Value));
        _ = Assert.IsType<Absorbed>(again);
        _ = Assert.IsType<Absorbed>(byHand);
    }

    [Fact, Trait("Lane", "unit")]
    public void Open_request_on_an_ended_instance_is_SND_WF_003_and_is_exempt_from_the_approval_check()
    {
        var produce = At(Step, "produce");
        var open = new OpenRequested(Engine, "LCH-0123456789abcdef0123456789abcdef");
        var ended = Applied(produce, new CancelRequested("human:cli", CancelOrigin.Command, Stamp('c'), Sequence: null, Parent: null));
        var revoked = EngineFixture.Context() with { Approval = ApprovalCheck.Revoked };

        Assert.Equal("SND-WF-003", Assert.IsType<Rejected>(Real(ended, Step, open)).Error.Code);
        _ = Assert.IsType<Applied>(Real(produce, Step, open, context: revoked));
    }

    [Fact, Trait("Lane", "unit")]
    public void Status_shows_what_the_takeover_left_of_the_timer()
    {
        var taken = Applied(At(Step, "produce"), Takeover());
        var kept = taken with { Suspension = taken.Suspension! with { Remaining = new RemainingTimer(DeadlineKind.State, TimeSpan.FromSeconds(40)) } };
        var recipe = new InterpretedRecipe(Version(), Step, EngineFixture.Catalog, new Dictionary<FactKey, InputFact>());

        Assert.Equal(TimeSpan.FromSeconds(40), InstanceViews.Of(kept, recipe, Now, timeoutAt: null, new InstanceViewContext()).Paused);
        Assert.Null(InstanceViews.Of(taken, recipe, Now, timeoutAt: null, new InstanceViewContext()).Paused);
    }

    /// <summary>Decides through the real rules, not the fixture's fake ones.</summary>
    private static Decision Real(WorkflowInstance instance, Recipe recipe, Trigger trigger, DecisionContext? context = null) =>
        Decide(instance, recipe, trigger, context: context, rules: EngineRules.Default);

    private static TakeoverRequested Takeover() => new("human:cli", Stamp('a'));

    private static HandbackRequested Handback() => new("human:cli", Stamp('b'));

    private static DeadlineFired Idle() => new(Timers, 1, DeadlineKind.TakeoverIdle);

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));

    private static WorkflowInstance Agent(WorkflowInstance instance) =>
        instance with { Frame = instance.Frame with { Run = instance.Frame.Run! with { Runtime = StepRuntime.Agent } } };

    private static RecipeVersion Version() => new(Step.Id, Step.Version, RecipeHash.From("sha256:" + new string('0', 64)));

    private static WorkflowInstance Applied(WorkflowInstance instance, Trigger trigger) =>
        Assert.IsType<Applied>(Real(instance, Step, trigger)).Changes[0].After;
}
