using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A state or retry timer that fired (engine spec §3.4, §3.8, §5.5, W20), over the real rules: 'then: park' parks with
/// 'timeout', 'then: escalate' escalates (kind timeout), 'then: retry' stays in the same entry and re-emits the entering
/// transition's effects under the same identities, and a second expiry parks with 'timeout'. A desk run always parks. A
/// timer of an entry that has ended, or of a suspended instance, changes nothing.
/// </summary>
public sealed class TimerExpiryTests
{
    private const string Timers = "service:workflow.timers";

    [Fact, Trait("Lane", "unit")]
    public void State_timeout_then_park_retries_the_state_once_before_it_parks()
    {
        var verify = Agent(At(Step, "verify", entry: 3));   // timeout: { after: 1d, then: park }

        var retried = Assert.IsType<Applied>(Expire(verify, DeadlineKind.State));
        var again = Assert.IsType<Applied>(Expire(retried.Changes[0].After, DeadlineKind.Retry));

        var first = retried.Changes[0].After;
        Assert.Equal(("verify", 3, true, null), (first.Frame.State, first.Frame.Entry, first.Frame.Retried, first.Suspension));
        Assert.Equal("3.1", first.Frame.Run?.Token.Value);
        var after = again.Changes[0].After;
        var park = Assert.IsType<Suspension>(after.Suspension);
        Assert.Equal((SuspensionKind.Park, ParkReason.Timeout, ResumeKind.Rearm), (park.Kind, park.Reason, park.Resume.Kind));
        Assert.Equal(("verify ran past 1d", Timers), (park.Question, park.Actor));
        Assert.Equal(("parked", "verify", "3.1"), (after.State, after.Frame.State, after.Frame.Run?.Token.Value));
        Assert.Equal(("deadline", Timers), (again.Changes[0].Event.Trigger.Kind, again.Changes[0].Event.Trigger.Actor));
        Assert.Empty(again.Effects);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_history_from_before_the_retry_patch_parks_at_the_first_expiry()
    {
        var verify = Agent(At(Step, "verify", entry: 3));

        var applied = Assert.IsType<Applied>(Expire(verify, DeadlineKind.State, EngineFixture.Context() with { LegacyTimeoutPark = true }));

        var park = Assert.IsType<Suspension>(applied.Changes[0].After.Suspension);
        Assert.Equal((ParkReason.Timeout, "verify ran past 1d"), (park.Reason, park.Question));
    }

    [Fact, Trait("Lane", "unit")]
    public void State_timeout_then_escalate_escalates_with_kind_timeout()
    {
        var triaged = At(Item, "triaged");   // timeout: { after: 7d, then: escalate }

        var applied = Assert.IsType<Applied>(Expire(triaged, DeadlineKind.State));

        var after = applied.Changes[0].After;
        var escalation = Assert.IsType<Suspension>(after.Suspension);
        Assert.Equal((SuspensionKind.Escalation, EscalationKind.Timeout, ResumeKind.Rearm), (escalation.Kind, escalation.Escalation, escalation.Resume.Kind));
        Assert.Equal(("triaged ran past 7d", "escalated"), (escalation.Question, after.State));
    }

    [Fact, Trait("Lane", "unit")]
    public void Retry_stays_in_the_entry_and_reemits_the_entering_effects_then_a_second_timeout_parks()
    {
        var startable = At(Item, "startable", entry: 2);
        var entering = Assert.IsType<Applied>(Decide(startable, Item, Slot(), Gates(("gate.budget.available", GateVerdict.Pass, null)), rules: EngineRules.Default));
        var claimed = entering.Changes[0].After;   // timeout: { after: 10m, then: retry }; entered with effect.claim.ref

        var retried = Assert.IsType<Applied>(Expire(claimed, DeadlineKind.State));
        var again = Assert.IsType<Applied>(Expire(retried.Changes[0].After, DeadlineKind.Retry));

        var after = retried.Changes[0].After;
        Assert.Equal(("claimed", 3, null), (after.State, after.Frame.Entry, after.Suspension));
        var reemitted = Assert.Single(retried.Effects);
        var original = Assert.Single(entering.Effects);
        Assert.Equal((BlockId.From("effect.claim.ref"), original.Identity, original.Origin), (reemitted.Effect, reemitted.Identity, reemitted.Origin));
        Assert.Equal([original.Identity], retried.Changes[0].Event.Effects);
        var park = Assert.IsType<Suspension>(again.Changes[0].After.Suspension);
        Assert.Equal((ParkReason.Timeout, "claimed ran past 10m", ResumeKind.Rearm), (park.Reason, park.Question, park.Resume.Kind));
        Assert.Empty(again.Effects);
    }

    [Fact, Trait("Lane", "unit")]
    public void Retry_once_per_entry_so_a_state_timer_after_the_retry_parks()
    {
        var claimed = At(Item, "claimed", entry: 3);
        var retried = Assert.IsType<Applied>(Expire(claimed, DeadlineKind.State)).Changes[0].After;

        var rearmed = Assert.IsType<Applied>(Expire(retried, DeadlineKind.State));   // a resume re-armed the state timer in the same entry

        Assert.Equal(ParkReason.Timeout, rearmed.Changes[0].After.Park);
    }

    [Fact, Trait("Lane", "unit")]
    public void Desk_timeout_never_retries()
    {
        var checks = At(Step, "checks", entry: 2);   // a desk run; timeout: { after: 30m, then: retry }

        var floor = Assert.IsType<Applied>(Expire(checks, DeadlineKind.State));
        var dev = Assert.IsType<Applied>(Expire(checks, DeadlineKind.State, EngineFixture.Context() with { DeskTimeout = TimeSpan.FromMinutes(1) }));

        var park = Assert.IsType<Suspension>(floor.Changes[0].After.Suspension);
        Assert.Equal((SuspensionKind.Park, ParkReason.Timeout, ResumeKind.Rearm, "checks ran past 2d"), (park.Kind, park.Reason, park.Resume.Kind, park.Question));
        Assert.Equal("2.1", floor.Changes[0].After.Frame.Run?.Token.Value);
        Assert.Empty(floor.Effects);
        Assert.Equal("checks ran past 30m", dev.Changes[0].After.Suspension?.Question);
    }

    [Fact, Trait("Lane", "unit")]
    public void Desk_timeout_parks_where_the_state_says_escalate()
    {
        var triaged = At(Item, "triaged");   // timeout: { after: 7d, then: escalate }
        var desk = triaged with { Frame = triaged.Frame with { Run = new OpenRun(RunToken.For(1, 1), StepRuntime.Desk, Launch: null, Task: "desk-1") } };

        var applied = Assert.IsType<Applied>(Expire(desk, DeadlineKind.State));

        Assert.Equal((SuspensionKind.Park, ParkReason.Timeout, "triaged ran past 7d"), (applied.Changes[0].After.Suspension?.Kind, applied.Changes[0].After.Park, applied.Changes[0].After.Suspension?.Question));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_timer_of_an_ended_entry_or_of_a_suspended_instance_changes_nothing()
    {
        var verify = Agent(At(Step, "verify", entry: 3));
        var parked = Assert.IsType<Applied>(Expire(verify, DeadlineKind.State, EngineFixture.Context() with { LegacyTimeoutPark = true })).Changes[0].After;
        var cancelled = Suspensions.Cancel(verify);

        _ = Assert.IsType<Absorbed>(TransitionDecision.Decide(new DecisionInput(verify, Step, new DeadlineFired(Timers, 2, DeadlineKind.State), GateAnswers.None, EngineFixture.Context())));
        _ = Assert.IsType<Absorbed>(Expire(parked, DeadlineKind.State));
        _ = Assert.IsType<Absorbed>(Expire(cancelled, DeadlineKind.Retry));
    }

    private static Decision Expire(WorkflowInstance instance, DeadlineKind kind, DecisionContext? context = null)
    {
        var recipe = instance.Recipe.Id == Item.Id ? Item : Step;
        return TransitionDecision.Decide(new DecisionInput(instance, recipe, new DeadlineFired(Timers, instance.Frame.Entry, kind), GateAnswers.None, context ?? EngineFixture.Context()));
    }

    private static WorkflowInstance Agent(WorkflowInstance instance) =>
        instance with { Frame = instance.Frame with { Run = instance.Frame.Run! with { Runtime = StepRuntime.Agent, Task = null } } };

    private static WaitSatisfied Slot() =>
        new("service:workflow.inbox", new ResourceWaitSource(ResourceKind.Slot, 1), Outcome: null, new Dictionary<FactKey, FactValue>());
}
