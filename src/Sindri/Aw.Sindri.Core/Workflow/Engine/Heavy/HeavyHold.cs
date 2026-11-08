using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Heavy;

/// <summary>
/// The heavy resource's rule (line spec LN-WF3, §8.4): a slot is taken at a 'waitFor: { resource: heavy }' wait, held while the
/// instance runs Steps off the desk, and given up while it is parked, taken over, at a desk Step or ended. A pure function of
/// the instance, so the interpreter and its tests ask the same question.
/// </summary>
public static class HeavyHold
{
    /// <summary>What to do about the slot for <paramref name="instance"/> now.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="inPhase">Whether its heavy wait was granted (a durable fact of its history): a Step that follows must hold a slot again after a release or a restart.</param>
    /// <param name="leased">Whether a lease may be held for it (a Take was issued and not released).</param>
    /// <returns>The move.</returns>
    public static HeavyMove Of(WorkflowInstance instance, Recipe recipe, bool inPhase, bool leased)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        if (instance.Ended || instance.Suspension is not null || instance.Frame.Run is { Runtime: StepRuntime.Desk })
        {
            return leased ? HeavyMove.Release : HeavyMove.None;
        }

        if (instance.Frame.Run is not null)
        {
            return inPhase ? HeavyMove.Ensure : HeavyMove.None;
        }

        return recipe.States.TryGetValue(instance.Frame.State, out var state) && state.WaitFor?.Resource == ResourceKind.Heavy ? HeavyMove.Acquire : HeavyMove.None;
    }
}
