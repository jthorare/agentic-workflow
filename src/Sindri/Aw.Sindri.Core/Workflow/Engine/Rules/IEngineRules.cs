using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The rules <see cref="TransitionDecision"/> and <see cref="RefusalRules"/> hand work to: state entry, suspensions,
/// loops, budgets, children and the event delta. <see cref="EngineRules.Default"/> calls the static rule classes; a
/// test passes a fake, so the decision is tested apart from each rule.
/// </summary>
internal interface IEngineRules
{
    /// <summary><see cref="StateEntry.Enter"/>.</summary>
    public WorkflowInstance Enter(WorkflowInstance instance, Recipe recipe, string state, DecisionContext context);

    /// <summary><see cref="Suspensions.Suspend"/>.</summary>
    public WorkflowInstance Suspend(WorkflowInstance instance, Suspension suspension);

    /// <summary><see cref="Suspensions.End"/>.</summary>
    public Result<Ended> End(WorkflowInstance instance, WorkflowInstanceId? child);

    /// <summary><see cref="Suspensions.TakeOver"/>.</summary>
    public Result<WorkflowInstance> TakeOver(WorkflowInstance instance, string actor);

    /// <summary><see cref="Suspensions.Cancel"/>.</summary>
    public WorkflowInstance Cancel(WorkflowInstance instance);

    /// <summary><see cref="LoopRules.Reenter"/>.</summary>
    public LoopVerdict Reenter(WorkflowInstance instance, Recipe recipe, string state, ProgressMark mark);

    /// <summary><see cref="LoopRules.AtCap"/>.</summary>
    public bool AtCap(WorkflowInstance instance, Recipe recipe, string state);

    /// <summary><see cref="LoopRules.NoProgress"/>.</summary>
    public bool NoProgress(LoopSpec loop, ProgressMark? previous, ProgressMark current);

    /// <summary><see cref="BudgetRules.Spend"/>.</summary>
    public BudgetVerdict Spend(WorkflowInstance instance, Recipe recipe, int tokens);

    /// <summary><see cref="ChildPropagation.Create"/>.</summary>
    public Result<ChildStart> CreateChild(WorkflowInstance parent, ChildWait wait, IChildRecipeResolver pinned, string actor);

    /// <summary><see cref="ChildPropagation.ToParent"/>.</summary>
    public ParentEffect ToParent(WorkflowInstance parent, Recipe recipe, ChildChanged change);

    /// <summary><see cref="ChildPropagation.Release"/>.</summary>
    public ParentReleased Release(WorkflowInstance parent, WorkflowInstanceId child);

    /// <summary><see cref="InstanceDeltas.Between"/>.</summary>
    public InstanceDelta Delta(WorkflowInstance? before, WorkflowInstance after);
}
