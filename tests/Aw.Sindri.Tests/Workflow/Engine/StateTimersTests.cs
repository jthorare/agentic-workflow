using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The timer plan the interpreter applies (engine spec §3.4, §5.5, W20): how long a state's timer runs, and what a
/// decided change does to it. A new entry or run arms the full timeout, a park, an escalation or an end stops it, an
/// answer that ends a park arms it in full again, a 'then: retry' arms the retry timer, and anything else keeps it.
/// </summary>
public sealed class StateTimersTests
{
    private const string Timers = "service:workflow.timers";
    private static readonly TimeSpan _floor = TimeSpan.FromDays(2);

    [Fact, Trait("Lane", "unit")]
    public void A_desk_run_waits_at_least_the_desk_timeout_and_every_other_state_its_own_timeout()
    {
        var produce = At(Step, "produce");   // 1d
        var agent = produce.Frame.Run! with { Runtime = StepRuntime.Agent };

        Assert.Equal(_floor, StateTimers.Timeout(Step.States["produce"], produce.Frame.Run, _floor));
        Assert.Equal(TimeSpan.FromDays(1), StateTimers.Timeout(Step.States["produce"], produce.Frame.Run, TimeSpan.FromMinutes(1)));
        Assert.Equal(TimeSpan.FromDays(1), StateTimers.Timeout(Step.States["produce"], agent, _floor));
        Assert.Equal(TimeSpan.FromDays(7), StateTimers.Timeout(Item.States["triaged"], run: null, _floor));
        Assert.Null(StateTimers.Timeout(Step.States["done"], run: null, _floor));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_entry_arms_the_entered_state_in_full_and_a_terminal_one_stops_it()
    {
        var produce = At(Step, "produce");
        var checks = Applied(produce, Completed(1, "done", ("fact:output", "x")));
        var verify = At(Step, "verify", entry: 3);
        var done = Applied(verify, Completed(3, "accepted"));

        Assert.Equal(new TimerAction(TimerActionKind.Arm, DeadlineKind.State, _floor), StateTimers.Between(produce, checks, Step, _floor));
        Assert.Equal(new TimerAction(TimerActionKind.Arm, DeadlineKind.State, TimeSpan.FromMinutes(30)), StateTimers.Between(produce, checks, Step, TimeSpan.FromMinutes(1)));
        Assert.Equal(TimerAction.Stop, StateTimers.Between(verify, done, Step, _floor));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_park_stops_the_timer_and_the_answer_that_ends_it_arms_it_in_full()
    {
        var triaged = At(Item, "triaged");
        var escalated = Applied(triaged, new DeadlineFired(Timers, 1, DeadlineKind.State));
        var resumed = Applied(escalated, new AnswerGiven("human:thor", new CommandStamp(CommandNonce.From(new string('a', 32)), new string('b', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null));

        Assert.Equal(TimerAction.Stop, StateTimers.Between(triaged, escalated, Item, _floor));
        Assert.Equal(new TimerAction(TimerActionKind.Arm, DeadlineKind.State, TimeSpan.FromDays(7)), StateTimers.Between(escalated, resumed, Item, _floor));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_retry_arms_the_retry_timer_and_a_change_in_the_same_run_keeps_the_timer()
    {
        var claimed = At(Item, "claimed", entry: 3);
        var retried = Applied(claimed, new DeadlineFired(Timers, 3, DeadlineKind.State));

        Assert.Equal(new TimerAction(TimerActionKind.Arm, DeadlineKind.Retry, TimeSpan.FromMinutes(10)), StateTimers.Between(claimed, retried, Item, _floor));
        Assert.Equal(TimerAction.Keep, StateTimers.Between(retried, retried with { Facts = new Dictionary<FactKey, FactValue> { [FactKey.From("fact:answer")] = Fact("a") } }, Item, _floor));
    }

    private static WorkflowInstance Applied(WorkflowInstance instance, Trigger trigger)
    {
        var recipe = instance.Recipe.Id == Item.Id ? Item : Step;
        var decision = TransitionDecision.Decide(new DecisionInput(instance, recipe, trigger, GateAnswers.None, EngineFixture.Context()));
        return Assert.IsType<Applied>(decision).Changes[0].After;
    }
}
