using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// A real state entry (spec §5.1, §5.3, W5, W20): a new frame with entry + 1, and a new run '{entry}.1' in an active
/// state. The interpreter arms the state's timer when the entry is in history (§5.5).
/// </summary>
internal static class StateEntry
{
    /// <summary>
    /// Enters <paramref name="state"/>: a new frame with entry + 1, a new run '{entry}.1' for an active state (on the desk
    /// runtime when the Step has no runtime and is deskCapable). A Step with neither runtime opens no run (the decision
    /// parks with SND-WF-010). The frame's child is left to the caller, which creates it with
    /// <see cref="ChildPropagation.Create"/>. A terminal state sets the outcome.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="state">The state entered.</param>
    /// <param name="context">The runtimes and the catalog.</param>
    /// <returns>The instance in the new frame.</returns>
    public static WorkflowInstance Enter(WorkflowInstance instance, Recipe recipe, string state, DecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(context);
        var target = recipe.States[state];
        var entry = instance.Frame.Entry + 1;
        return instance with
        {
            Frame = new Frame(state, entry, OpenRun(target, entry, context), Child: null),
            Outcome = target.Kind == StateKind.Terminal ? target.Outcome : instance.Outcome,
        };
    }

    /// <summary>
    /// An SND-WF-010 restart (spec §5.8 'restart', W5, W20): the frame's Step opens run '{entry}.1' as a real entry would
    /// (the agent runtime when the build has it, else the desk when the Step is deskCapable). Null when the build still
    /// has neither, so the caller parks again.
    /// </summary>
    /// <param name="instance">The instance, its SND-WF-010 park ended.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="context">The runtimes and the catalog.</param>
    /// <returns>The instance with the run open, or null.</returns>
    public static WorkflowInstance? Restart(WorkflowInstance instance, Recipe recipe, DecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(context);
        return OpenRun(recipe.States[instance.Frame.State], instance.Frame.Entry, context) is { } run
            ? instance with { Frame = instance.Frame with { Run = run } }
            : null;
    }

    // An active state's run '{entry}.1': the block runtime when this build registered the Step's (DT spec §8.2), the agent runtime when the build has the Step's, else the desk when it is
    // deskCapable (W20); with neither there is no run, and the decision parks with SND-WF-010 (W5).
    private static OpenRun? OpenRun(RecipeState target, int entry, DecisionContext context)
    {
        if (target.Kind != StateKind.Active || target.Step is not { } step)
        {
            return null;
        }

        if (context.RunsAsBlock(step.Use))
        {
            return new OpenRun(RunToken.For(entry, 1), StepRuntime.Block, Launch: null, Task: null);   // DT spec §8.2: the engine runs it, no desk task
        }

        if (context.Runtimes.Contains(step.Use))
        {
            return new OpenRun(RunToken.For(entry, 1), StepRuntime.Agent, Launch: null, Task: null);
        }

        return context.Catalog.Find(step.Use) is { DeskCapable: true }
            ? new OpenRun(RunToken.For(entry, 1), StepRuntime.Desk, Launch: null, target.Next)
            : null;
    }
}
