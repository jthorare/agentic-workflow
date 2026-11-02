using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

public static partial class TransitionDecision
{
    // Steps whose entry changes something outside the instance (map proposals, the frozen packet, the owner's stop): never re-entered by a rewind.
    private static readonly HashSet<string> _noRewindSteps = new(StringComparer.Ordinal)
    {
        "step.scope.propose", "step.scope.freeze", "step.scope.withdraw", "step.scope.review",
    };

    /// <summary>'workflow rewind' (#1270): the instance re-enters an earlier state with every fact kept and its park, if any, ended. Not a loop round; each loop's no-progress marker is cleared, its cap is not touched.</summary>
    private static Decision Rewind(Deciding d, WorkflowInstance instance, RewindRequested rewind)
    {
        if (RewindRefusal(d, instance, rewind) is { } refusal)
        {
            return new Rejected(d.Key, refusal);
        }

        var cleared = instance with
        {
            Suspension = null,
            PendingPark = null,
            LaterParks = null,
            Loops = instance.Loops.ToDictionary(static l => l.Key, static l => l.Value with { Last = null }, StringComparer.Ordinal),
        };
        var entering = Enter(d.Scope, cleared, rewind.To);
        if (entering.IsError)
        {
            return new Rejected(d.Key, entering.Error);
        }

        var entered = entering.Value;
        var own = d.Change(entered.After, WorkflowEventType.Transition, Records.None);
        return new Applied(d.Key, [own, .. entered.Children], entered.Effects, entered.FollowUps);
    }

    private static Kernel.Error? RewindRefusal(Deciding d, WorkflowInstance instance, RewindRequested rewind)
    {
        var address = instance.Address.Value;
        if (instance.Ended)
        {
            return FitRefusals.Refuse($"{address} has ended; there is nothing to rewind", FitRefusals.Status(address));
        }

        if (instance.State == EngineStates.TakenOver)
        {
            return FitRefusals.Refuse($"{address} is taken over", $"sindri workflow handback {address}, then rewind");
        }

        if (instance.Frame.Run?.Launch is { Length: > 0 } launch && !string.Equals(rewind.EndedLaunch, launch, StringComparison.Ordinal))
        {
            return FitRefusals.Refuse($"{address} has a live agent ({launch}); a rewind never ends it", $"end or wait for {launch}, then rewind");
        }

        if (RewindBlockedByWait(instance) is { } waiting)
        {
            return waiting;
        }

        return RewindTargetRefusal(d, instance, rewind.To);
    }

    private static Kernel.Error? RewindBlockedByWait(WorkflowInstance instance)
    {
        var address = instance.Address.Value;
        if (instance.Frame.Child is not null || instance.Suspension?.Mirrors is not null)
        {
            return FitRefusals.Refuse($"{address} waits on a child", "answer or cancel the child, or cancel this instance");
        }

        if ((instance.Suspension is { } current ? [current, .. Suspensions.PendingParks(instance)] : Suspensions.PendingParks(instance)).Any(static p => p.Resume.Effects.Count > 0))
        {
            return FitRefusals.Refuse($"{address} is parked on refused effects that a rewind would drop", FitRefusals.Status(address));
        }

        return null;
    }

    private static Kernel.Error? RewindTargetRefusal(Deciding d, WorkflowInstance instance, string to)
    {
        var address = instance.Address.Value;
        var fix = $"rewind to one of: {string.Join(", ", RewindTargets(d, instance))}";
        if (!d.Recipe.States.TryGetValue(to, out var target))
        {
            return FitRefusals.Refuse($"'{to}' is no state of {d.Recipe.Id.Value}", fix);
        }

        if (!Rewindable(d.Recipe, to, target))
        {
            return FitRefusals.Refuse($"'{to}' can't be re-entered: it ends the instance or changes something outside it", fix);
        }

        if (instance.Visited?.Contains(to, StringComparer.Ordinal) != true)
        {
            return FitRefusals.Refuse($"{address} has not been in '{to}' yet", fix);
        }

        if (instance.Suspension is null && string.Equals(instance.Frame.State, to, StringComparison.Ordinal))
        {
            return FitRefusals.Refuse($"{address} is already in '{to}'", FitRefusals.Status(address));
        }

        var missing = MissingInputs(d.Context.Catalog, instance, target);
        return missing.Count == 0 ? null : FitRefusals.Refuse($"'{to}' reads {string.Join(", ", missing)}, which {address} does not hold", fix);
    }

    private static bool Rewindable(Recipe recipe, string name, RecipeState state) =>
        state is { Kind: StateKind.Active, Step: { } step } && !_noRewindSteps.Contains(step.Use.Value) && !EnteredWithEffects(recipe, name);

    private static IEnumerable<string> RewindTargets(Deciding d, WorkflowInstance instance) =>
        (instance.Visited ?? []).Where(s => d.Recipe.States.TryGetValue(s, out var state) && Rewindable(d.Recipe, s, state)
            && !string.Equals(s, instance.Frame.State, StringComparison.Ordinal));

    /// <summary>The states a rewind of <paramref name="instance"/> would accept now, the state of the park's withheld transition first (what 'sindri workflow rewind' offers a parked run); empty when the instance has ended, is taken over or waits on a child.</summary>
    /// <param name="recipe">The recipe it runs.</param>
    /// <param name="catalog">The build's block descriptors: the facts a target's Step reads.</param>
    /// <param name="instance">The instance.</param>
    /// <returns>The target states, each one <see cref="RewindTargetRefusal"/> would accept.</returns>
    internal static IReadOnlyList<string> RewindTargets(Recipe recipe, BlockCatalog catalog, WorkflowInstance instance)
    {
        if (RewindClosed(instance))
        {
            return [];
        }

        var withheld = instance.Suspension?.Resume.Withheld?.Transition is { } index && index >= 0 && index < recipe.Transitions.Count ? recipe.Transitions[index].From : [];
        return [.. (instance.Visited ?? []).Where(s => RewindOffered(recipe, catalog, instance, s)).OrderBy(s => withheld.Contains(s, StringComparer.Ordinal) ? 0 : 1)];
    }

    private static bool RewindClosed(WorkflowInstance instance) =>
        instance.Ended || instance.State == EngineStates.TakenOver || instance.Suspension is { Kind: SuspensionKind.Takeover } || RewindBlockedByWait(instance) is not null;

    private static bool RewindOffered(Recipe recipe, BlockCatalog catalog, WorkflowInstance instance, string name) =>
        recipe.States.TryGetValue(name, out var state) && Rewindable(recipe, name, state)
        && (instance.Suspension is not null || !string.Equals(name, instance.Frame.State, StringComparison.Ordinal))
        && MissingInputs(catalog, instance, state).Count == 0;

    private static List<string> MissingInputs(BlockCatalog catalog, WorkflowInstance instance, RecipeState target) =>
        catalog.Find(target.Step!.Use)?.Inputs.Where(i => !instance.Facts.ContainsKey(i)).Select(static i => i.Value).ToList() ?? [];   // why: Rewindable holds a Step

    private static bool EnteredWithEffects(Recipe recipe, string state) =>
        recipe.Transitions.Any(t => string.Equals(t.To, state, StringComparison.Ordinal) && t.Effects.Count > 0);
}
