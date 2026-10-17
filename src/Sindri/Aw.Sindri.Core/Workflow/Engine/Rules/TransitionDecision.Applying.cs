using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Applying a matched transition (spec §3.5, §3.7, §5.1, §5.7, W5, W20): the loop's round, the produced facts, the
/// state entry, a child wait's child, the SND-WF-010 park, the effects and the Step's start.
/// </summary>
public static partial class TransitionDecision
{
    private static readonly FactKey _findings = FactKey.From("fact:findings");

    /// <summary>Gates and evidence first (§5.2): any refusal is a refusal of this transition, never a fall-through to the next.</summary>
    private static Decision Transition(Deciding d, WorkflowInstance instance, int index)
    {
        var records = Check(d, d.Recipe.Transitions[index]);
        return records.Refused ? Refuse(d, instance, index, records) : Apply(d, instance, index, records);
    }

    private static Decision Apply(Deciding d, WorkflowInstance instance, int index, Records records)
    {
        var transition = d.Recipe.Transitions[index];
        var (looped, park) = Reenter(d, instance, transition.To, index);
        if (park is not null)
        {
            return d.Changed(d.Rules.Suspend(instance, park), records);
        }

        var produced = looped with { Facts = Produce(looped.Facts, transition, d.Trigger) };
        var entering = Enter(d.Scope, produced, transition.To);
        if (entering.IsError)
        {
            return new Rejected(d.Key, entering.Error);
        }

        var entered = entering.Value;
        var effects = Effects(transition, instance.Id, entered.Entry);
        var after = entered.After with { Frame = entered.After.Frame with { EnteredBy = index } };   // a 'then: retry' re-emits these effects (§3.4)
        var own = d.Change(after, WorkflowEventType.Transition, records);
        own = own with { Event = own.Event with { Effects = [.. effects.Select(e => e.Identity)] } };
        return new Applied(d.Key, [own, .. entered.Children], [.. effects, .. entered.Effects], entered.FollowUps);
    }

    /// <summary>
    /// Entering a looped state again (§3.7) is the next round, or a park with rounds or no-progress; the park keeps the
    /// withheld transition, so its grant performs it. A first entry starts round 1 (<see cref="FirstRound"/>).
    /// </summary>
    private static (WorkflowInstance Instance, Suspension? Park) Reenter(Deciding d, WorkflowInstance instance, string state, int index)
    {
        if (d.Recipe.States[state].Loop is not { } name || !instance.Loops.ContainsKey(name))
        {
            return (instance, null);
        }

        var verdict = d.Rules.Reenter(instance, d.Recipe, state, Progress(d.Trigger));
        if (verdict.Park is { } park)
        {
            return (instance, Caused(park, d.Trigger, index));
        }

        return verdict.Next is { } next ? (instance with { Loops = With(instance.Loops, name, next) }, null) : (instance, null);
    }

    /// <summary>
    /// A real state entry (§5.1): the new frame from <see cref="StateEntry.Enter"/>; a child wait's child created in the
    /// same decision (W17); a Step with no runtime that isn't desk-capable parked with SND-WF-010 (W5); otherwise the
    /// run started once the entry is in history (on the desk runtime when the Step has no runtime, W20, §5.3).
    /// </summary>
    private static Result<Entered> Enter(Scope scope, WorkflowInstance instance, string state)
    {
        var target = scope.Recipe.States[state];
        var entered = scope.Rules.Enter(FirstRound(instance, scope.Recipe, state), scope.Recipe, state, scope.Context);
        var entry = entered.Frame.Entry;
        if (target.WaitFor?.Child is { } wait)
        {
            return WithChild(scope, entered, wait, entry);
        }

        if (target.Step is not { } step)
        {
            return new Entered(entered, entry, [], [], []);
        }

        if (!Runnable(scope.Context, step.Use))
        {
            return new Entered(scope.Rules.Suspend(entered, NoRuntime(step.Use, scope.Actor)), entry, [], [], []);
        }

        FollowUp[] start = entered.Frame.Run is { } run ? [new(FollowUpKind.StartRun, entered.Id, run.Token, step.Use, Answer: null)] : [];
        return new Entered(entered, entry, [], [], start);
    }

    /// <summary>The child the wait creates (ChildPropagation.Create resolves and checks it against the pin), started by this decision.</summary>
    private static Result<Entered> WithChild(Scope scope, WorkflowInstance entered, ChildWait wait, int entry)
    {
        var creating = scope.Rules.CreateChild(entered, wait, scope.Context.Children, scope.Actor);
        if (creating.IsError)
        {
            return creating.Error;
        }

        var (request, recipe) = creating.Value;
        return Start(request, recipe, scope.Context, scope.Rules) switch
        {
            Applied started => new Entered(
                entered with { Frame = entered.Frame with { Child = request.Id } }, entry, started.Changes, started.Effects, started.FollowUps),
            Rejected rejected => rejected.Error,
            var other => throw new InvalidOperationException($"Starting a child decided {other.GetType().Name}; a start is applied or rejected."),
        };
    }

    /// <summary>The first entry of a looped state is round 1 of the loop's cap (§3.7).</summary>
    private static WorkflowInstance FirstRound(WorkflowInstance instance, Recipe recipe, string state) =>
        recipe.States[state].Loop is { } name && !instance.Loops.ContainsKey(name) && recipe.Loops.TryGetValue(name, out var loop)
            ? instance with { Loops = With(instance.Loops, name, new LoopState(1, loop.Cap, Last: null)) }
            : instance;

    /// <summary>A Step runs when this build has its runtime, or on the desk runtime when its descriptor says deskCapable (W5, W20).</summary>
    private static bool Runnable(DecisionContext context, BlockId step) =>
        context.Runtimes.Contains(step) || context.RunsAsBlock(step) || context.Catalog.Find(step) is { DeskCapable: true };

    private static Suspension NoRuntime(BlockId step, string actor) => new(
        SuspensionKind.Park, ParkReason.NoRuntime, ParkKind: null, Escalation: null,
        $"No runtime for {step.Value} in this build. Cancel, or resume in a build that has it.", actor, Mirrors: null,
        new ResumeAction(ResumeKind.Restart, Withheld: null, Gate: null, Effects: []));

    /// <summary>'produces' (§3.5): the facts the transition records from what its trigger's source reported; a renamed entry (DT spec §8.4) records its source's value under its own key.</summary>
    private static IReadOnlyDictionary<FactKey, FactValue> Produce(IReadOnlyDictionary<FactKey, FactValue> facts, RecipeTransition transition, Trigger trigger)
    {
        var outputs = Outputs(trigger);
        var produced = transition.Produces.Where(p => outputs.ContainsKey(p.SourceKey())).ToList();
        if (produced.Count == 0)
        {
            return facts;
        }

        var merged = new Dictionary<FactKey, FactValue>(facts);
        foreach (var entry in produced)
        {
            merged[entry.Fact] = outputs[entry.SourceKey()];
        }

        return merged;
    }

    /// <summary>What the round that ends produced (§3.7): the findings it reported, or the failure it ended with. Digests, never content.</summary>
    private static ProgressMark Progress(Trigger trigger) => trigger switch
    {
        StepFailed failed => new ProgressMark(Findings: null, RefusalRules.Digest(failed.Reason), EmptyDiff: false),
        _ => new ProgressMark(Outputs(trigger).GetValueOrDefault(_findings)?.Sha256, Failure: null, EmptyDiff: false),
    };

    private static Dictionary<string, LoopState> With(IReadOnlyDictionary<string, LoopState> loops, string name, LoopState state) =>
        new(loops, StringComparer.Ordinal) { [name] = state };

    /// <summary>A state entry's result.</summary>
    /// <param name="After">The instance in the entered frame (or parked in it).</param>
    /// <param name="Entry">The entered frame's entry, which effect identities use (§5.7).</param>
    /// <param name="Children">A created child's change.</param>
    /// <param name="Effects">A created child's effects.</param>
    /// <param name="FollowUps">The runs to start.</param>
    private sealed record Entered(
        WorkflowInstance After,
        int Entry,
        IReadOnlyList<InstanceChange> Children,
        IReadOnlyList<EffectCommand> Effects,
        IReadOnlyList<FollowUp> FollowUps);
}
