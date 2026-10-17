using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>Matching (spec §3.5, §3.6, §5.2): the run token, the trigger allowed in the state, and the first transition whose closed 'when' holds.</summary>
public static partial class TransitionDecision
{
    private static readonly Dictionary<FactKey, FactValue> _noOutputs = [];

    private static TransitionMatch MatchFrom(WorkflowInstance instance, Recipe recipe, Trigger trigger)
    {
        var miss = Miss(instance, recipe, trigger);
        if (miss != MatchMiss.None)
        {
            return new TransitionMatch(Transition: null, miss);
        }

        var matching = new Matching(instance, recipe, trigger);
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var transition = recipe.Transitions[i];
            if (transition.On == trigger.Name && transition.From.Contains(instance.State, StringComparer.Ordinal) && Holds(transition.When, matching))
            {
                return new TransitionMatch(i, MatchMiss.None);
            }
        }

        return new TransitionMatch(Transition: null, MatchMiss.NoTransition);
    }

    /// <summary>Why the trigger can't match here: an engine trigger, an ended instance, another run's report, or a trigger the state doesn't take.</summary>
    private static MatchMiss Miss(WorkflowInstance instance, Recipe recipe, Trigger trigger)
    {
        if (trigger is not (StepCompleted or StepFailed or WaitSatisfied or CommandFired))
        {
            return MatchMiss.EngineTrigger;
        }

        if (instance.Ended)
        {
            return MatchMiss.NotAllowed;
        }

        if (Run(trigger) is { } token && instance.Frame.Run?.Token != token)
        {
            return MatchMiss.StaleRun;
        }

        if (trigger is StepCompleted { Via: not StepRuntime.Block } && instance.Frame.Run is { Runtime: StepRuntime.Block })
        {
            return MatchMiss.EngineOwnedRun;
        }

        return recipe.States.TryGetValue(instance.State, out var state) && Allowed(instance, recipe, state, trigger) ? MatchMiss.None : MatchMiss.NotAllowed;
    }

    /// <summary>
    /// The instance as a child's end sees it (§5.8): a suspension that mirrors the child ends before the change applies,
    /// so the match reads the recipe frame. A takeover over the mirror stays (the change is held until the handback).
    /// </summary>
    private static WorkflowInstance Unmirrored(WorkflowInstance instance, Trigger trigger) =>
        trigger is WaitSatisfied { Source: ChildWaitSource child } && instance.Suspension?.Mirrors == child.Child
            ? instance with { Suspension = null, PendingPark = null, LaterParks = null }
            : instance;

    /// <summary>Step reports from an active state, a satisfied wait of the kind the wait names, a command a transition from here takes.</summary>
    private static bool Allowed(WorkflowInstance instance, Recipe recipe, RecipeState state, Trigger trigger) => trigger switch
    {
        StepCompleted or StepFailed => state.Kind == StateKind.Active,
        WaitSatisfied satisfied => state.Kind == StateKind.Wait && state.WaitFor is { } wait && Fits(wait, instance.Frame, satisfied.Source),
        _ => state.Kind != StateKind.Terminal
            && recipe.Transitions.Any(t => t.On == trigger.Name && t.From.Contains(instance.State, StringComparer.Ordinal)),
    };

    private static bool Fits(WaitFor wait, Frame frame, WaitSource source) => source switch
    {
        EventWaitSource e => wait.Event == e.Kind,
        ResourceWaitSource r => wait.Resource == r.Resource,
        ChildWaitSource c => wait.Child is not null && frame.Child == c.Child,
        AnswerWaitSource a => wait.Answer is { } answer && answer.Id.Equals(a.Answer),
        _ => false,
    };

    /// <summary>The closed 'when' (§3.6): every form set on a clause must hold.</summary>
    private static bool Holds(WhenClause? when, Matching matching) =>
        when is null || (Tests(when, matching) && Combines(when, matching));

    private static bool Tests(WhenClause when, Matching m) =>
        (when.Outcome is not { } outcome || Outcome(m.Trigger) == outcome)
        && (when.Fact is not { } fact || Present(fact, m) == (when.Is != FactPresence.Absent))
        && (when.Loop is not { } loop || Below(loop, m));

    private static bool Combines(WhenClause when, Matching m) =>
        (when.All is not { } all || all.All(c => Holds(c, m)))
        && (when.Any is not { } any || any.Any(c => Holds(c, m)))
        && (when.Not is not { } not || !Holds(not, m));

    /// <summary>A fact is present when the instance holds it or the trigger reports it.</summary>
    private static bool Present(FactKey fact, Matching m) => m.Instance.Facts.ContainsKey(fact) || Outputs(m.Trigger).ContainsKey(fact);

    /// <summary>'{ loop, below: cap }': the loop's round is below the rounds allowed (its cap, raised by grants).</summary>
    private static bool Below(string loop, Matching m)
    {
        var state = m.Instance.Loops.GetValueOrDefault(loop);
        var allowed = state?.Allowed ?? m.Recipe.Loops.GetValueOrDefault(loop)?.Cap ?? 0;
        return (state?.Round ?? 0) < allowed;
    }

    private static RunToken? Run(Trigger trigger) => trigger switch
    {
        StepCompleted completed => completed.Run,
        StepFailed failed => failed.Run,
        _ => null,
    };

    /// <summary>The outcome the trigger's source reported.</summary>
    private static OutcomeName? Outcome(Trigger trigger) => trigger switch
    {
        StepCompleted completed => completed.Outcome,
        WaitSatisfied satisfied => satisfied.Outcome,
        _ => null,
    };

    /// <summary>The facts the trigger's source reported.</summary>
    private static IReadOnlyDictionary<FactKey, FactValue> Outputs(Trigger trigger) => trigger switch
    {
        StepCompleted completed => completed.Outputs,
        WaitSatisfied satisfied => satisfied.Outputs,
        _ => _noOutputs,
    };

    private static string Describe(Trigger trigger) => Outcome(trigger) is { } outcome ? $"outcome '{outcome.Value}'" : "trigger";

    /// <summary>What a 'when' reads.</summary>
    /// <param name="Instance">The instance.</param>
    /// <param name="Recipe">Its recipe.</param>
    /// <param name="Trigger">The trigger.</param>
    private sealed record Matching(WorkflowInstance Instance, Recipe Recipe, Trigger Trigger);
}
