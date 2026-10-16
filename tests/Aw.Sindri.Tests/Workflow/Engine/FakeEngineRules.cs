using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A small stand-in for the entry, suspension, loop, budget, child and delta rules, so the decision is tested on its
/// own: it does what each rule's summary says, in the simplest way.
/// </summary>
internal sealed class FakeEngineRules : IEngineRules
{
    /// <summary>When set, every spend exhausts the budget.</summary>
    public bool BudgetExhausted { get; init; }

    public WorkflowInstance Enter(WorkflowInstance instance, Recipe recipe, string state, DecisionContext context)
    {
        var target = recipe.States[state];
        var entry = instance.Frame.Entry + 1;
        var run = target.Step is { } step
            ? new OpenRun(RunToken.For(entry, 1), context.Runtimes.Contains(step.Use) ? StepRuntime.Agent : StepRuntime.Desk, Launch: null, Task: null)
            : null;
        return instance with
        {
            Frame = new Frame(state, entry, run, Child: null),
            Outcome = target.Kind == StateKind.Terminal ? target.Outcome : null,
        };
    }

    public WorkflowInstance Suspend(WorkflowInstance instance, Suspension suspension) => instance.Suspension is null
        ? instance with { Suspension = suspension }
        : instance with { Suspension = suspension, PendingPark = instance.Suspension };

    public Result<Ended> End(WorkflowInstance instance, WorkflowInstanceId? child) => instance.Suspension is { } suspension
        ? new Ended(instance with { Suspension = instance.PendingPark, PendingPark = null }, suspension, instance.PendingPark is not null)
        : WorkflowErrors.TriggerNotAllowed.With("nothing to end (fix: none)");

    public Result<WorkflowInstance> TakeOver(WorkflowInstance instance, string actor) => Suspend(instance, new Suspension(
        SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, "taken over", actor, Mirrors: null,
        new ResumeAction(ResumeKind.Return, Withheld: null, Gate: null, Effects: [])));

    public WorkflowInstance Cancel(WorkflowInstance instance) => instance with
    {
        Frame = new Frame(EngineStates.Cancelled, instance.Frame.Entry, Run: null, Child: null),
        Suspension = null,
        PendingPark = null,
        Outcome = OutcomeName.From("cancelled"),
    };

    public LoopVerdict Reenter(WorkflowInstance instance, Recipe recipe, string state, ProgressMark mark)
    {
        var name = recipe.States[state].Loop!; // why: the decision calls Reenter only for a looped state
        var loop = instance.Loops[name];
        if (NoProgress(recipe.Loops[name], loop.Last, mark))
        {
            return new LoopVerdict(Next: null, LoopPark(ParkReason.NoProgress));
        }

        return loop.Round + 1 > loop.Allowed
            ? new LoopVerdict(Next: null, LoopPark(ParkReason.Rounds))
            : new LoopVerdict(loop with { Round = loop.Round + 1, Last = mark }, Park: null);
    }

    public bool AtCap(WorkflowInstance instance, Recipe recipe, string state) =>
        recipe.States[state].Loop is { } name && instance.Loops.TryGetValue(name, out var loop) && loop.Round >= loop.Allowed;

    public bool NoProgress(LoopSpec loop, ProgressMark? previous, ProgressMark current) =>
        previous is not null
        && ((current.Findings is not null && current.Findings == previous.Findings) || (current.Failure is not null && current.Failure == previous.Failure));

    public BudgetVerdict Spend(WorkflowInstance instance, Recipe recipe, int tokens)
    {
        var spent = instance.Spent with { Total = instance.Spent.Total + tokens };
        return new BudgetVerdict(spent, BudgetExhausted
            ? new Suspension(
                SuspensionKind.Park, ParkReason.Budget, ParkKind.Step, Escalation: null, "budget spent", "service:test", Mirrors: null,
                new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []))
            : null);
    }

    public Result<ChildStart> CreateChild(WorkflowInstance parent, ChildWait wait, IChildRecipeResolver pinned, string actor)
    {
        if (pinned.Resolve(wait.Recipe) is not { } child)
        {
            return WorkflowErrors.RecipeNotFound.With($"no {wait.Recipe.Value} in the test's resolver");
        }

        var request = new StartRequest(
            TriggerKey.For(EngineFixture.ChildId, "start"),
            EngineFixture.ChildId,
            parent.Address.Child(wait.Recipe, parent.Frame.Entry),
            new PinnedRecipe(wait.Recipe, 1, parent.Recipe.Children[wait.Recipe], new Dictionary<RecipeId, RecipeHash>()),
            parent.Item,
            parent.Attempt,
            parent.Id,
            new Dictionary<FactKey, FactValue>(),
            actor,
            TakenOver: false,
            parent.Tenant,
            parent.Ring);
        return new ChildStart(request, child.Recipe);
    }

    public ParentEffect ToParent(WorkflowInstance parent, Recipe recipe, ChildChanged change) => change.Change switch
    {
        ChildChange.Parked => new ParentEffect(ParentEffectKind.MirrorPark, new Suspension(
            SuspensionKind.Park, change.Reason, ParkKind: null, Escalation: null, change.Question ?? string.Empty, change.Actor, change.Child,
            new ResumeAction(ResumeKind.Forward, Withheld: null, Gate: null, Effects: []))),
        ChildChange.Resumed => new ParentEffect(ParentEffectKind.Release, Suspend: null),
        ChildChange.Escalated or _ => new ParentEffect(ParentEffectKind.Ignore, Suspend: null),
    };

    public ParentReleased Release(WorkflowInstance parent, WorkflowInstanceId child) =>
        new(parent.Suspension?.Mirrors == child ? parent with { Suspension = parent.PendingPark, PendingPark = null } : parent);

    public InstanceDelta Delta(WorkflowInstance? before, WorkflowInstance after) =>
        new(after.Frame, after.Suspension, after.PendingPark, after.Loops, after.Spent, after.Facts, after.Outcome);

    private static Suspension LoopPark(ParkReason reason) => new(
        SuspensionKind.Park, reason, ParkKind.Loop, Escalation: null, "loop", "service:test", Mirrors: null,
        new ResumeAction(ResumeKind.Transition, Withheld: null, Gate: null, Effects: []));
}
