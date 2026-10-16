using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The build's <see cref="IEngineRules"/>: each member calls its static rule.</summary>
internal sealed class EngineRules : IEngineRules
{
    private EngineRules()
    {
    }

    /// <summary>The one instance.</summary>
    public static EngineRules Default { get; } = new();

    /// <inheritdoc/>
    public WorkflowInstance Enter(WorkflowInstance instance, Recipe recipe, string state, DecisionContext context) =>
        StateEntry.Enter(instance, recipe, state, context);

    /// <inheritdoc/>
    public WorkflowInstance Suspend(WorkflowInstance instance, Suspension suspension) => Suspensions.Suspend(instance, suspension);

    /// <inheritdoc/>
    public Result<Ended> End(WorkflowInstance instance, WorkflowInstanceId? child) => Suspensions.End(instance, child);

    /// <inheritdoc/>
    public Result<WorkflowInstance> TakeOver(WorkflowInstance instance, string actor) => Suspensions.TakeOver(instance, actor);

    /// <inheritdoc/>
    public WorkflowInstance Cancel(WorkflowInstance instance) => Suspensions.Cancel(instance);

    /// <inheritdoc/>
    public LoopVerdict Reenter(WorkflowInstance instance, Recipe recipe, string state, ProgressMark mark) =>
        LoopRules.Reenter(instance, recipe, state, mark);

    /// <inheritdoc/>
    public bool AtCap(WorkflowInstance instance, Recipe recipe, string state) => LoopRules.AtCap(instance, recipe, state);

    /// <inheritdoc/>
    public bool NoProgress(LoopSpec loop, ProgressMark? previous, ProgressMark current) => LoopRules.NoProgress(loop, previous, current);

    /// <inheritdoc/>
    public BudgetVerdict Spend(WorkflowInstance instance, Recipe recipe, int tokens) => BudgetRules.Spend(instance, recipe, tokens);

    /// <inheritdoc/>
    public Result<ChildStart> CreateChild(WorkflowInstance parent, ChildWait wait, IChildRecipeResolver pinned, string actor) =>
        ChildPropagation.Create(parent, wait, pinned, actor);

    /// <inheritdoc/>
    public ParentEffect ToParent(WorkflowInstance parent, Recipe recipe, ChildChanged change) => ChildPropagation.ToParent(parent, recipe, change);

    /// <inheritdoc/>
    public ParentReleased Release(WorkflowInstance parent, WorkflowInstanceId child) => ChildPropagation.Release(parent, child);

    /// <inheritdoc/>
    public InstanceDelta Delta(WorkflowInstance? before, WorkflowInstance after) => InstanceDeltas.Between(before, after);
}
