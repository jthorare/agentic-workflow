using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Children both ways (spec §5.8, W17): a child started by its parent's transition, its changes propagated to the
/// parent's suspension, answers forwarded to it, and a parent's cancel cascading. The interpreter runs the child as a
/// child workflow whose id is its address (§5.1).
/// </summary>
internal static class ChildPropagation
{
    /// <summary>
    /// The child a 'waitFor: { child }' creates (spec §5.8): the child recipe as <paramref name="pinned"/> resolves it,
    /// under the parent's pinned hash; its own pins (its child waits' recipes, as <paramref name="pinned"/> resolves
    /// them), so a child that waits on a grandchild creates it the same way (RCP-CHILD-003 allows depth 4); its inputs
    /// from the wait's '{ fact }' and literals; its address '{parent}/{recipe name}.{parent entry}', which is its
    /// workflow id, and its id derived from that address (§5.1); and the parent trigger's actor. A resolved child whose hash isn't the
    /// one the parent pinned is refused (§4.1: a child is created from the hash its parent pinned).
    /// </summary>
    /// <param name="parent">The parent, in the frame that entered the wait.</param>
    /// <param name="wait">The child wait.</param>
    /// <param name="pinned">The pinned child recipes.</param>
    /// <param name="actor">The parent trigger's actor.</param>
    /// <returns>The child's start with the recipe it starts, or SND-RECIPE-002 when the child isn't resolved, isn't in the parent's pins, or resolves to another hash.</returns>
    public static Result<ChildStart> Create(WorkflowInstance parent, ChildWait wait, IChildRecipeResolver pinned, string actor)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(pinned);
        if (pinned.Resolve(wait.Recipe) is not { } child || !parent.Recipe.Children.TryGetValue(wait.Recipe, out var hash))
        {
            return WorkflowErrors.RecipeNotFound.With(
                $"child recipe {wait.Recipe.Value} isn't pinned for {parent.Recipe.Id.Value} (fix: re-approve {parent.Recipe.Id.Value})");
        }

        if (!child.Hash.Equals(hash))
        {
            return WorkflowErrors.RecipeNotFound.With(
                $"child recipe {wait.Recipe.Value} resolves to {child.Hash.Value}, not {hash.Value}, the hash {parent.Recipe.Id.Value} pinned (fix: re-approve {parent.Recipe.Id.Value})");
        }

        var address = parent.Address.Child(wait.Recipe, parent.Frame.Entry);
        var id = ChildInputs.IdFor(address);
        var request = new StartRequest(
            TriggerKey.For(id, "start"),
            id,
            address,
            new PinnedRecipe(wait.Recipe, child.Recipe.Version, hash, ChildInputs.Pins(child.Recipe, pinned)),
            parent.Item,
            parent.Attempt,
            parent.Id,
            ChildInputs.From(parent, wait),
            actor,
            TakenOver: false,
            parent.Tenant,
            parent.Ring);
        return new ChildStart(request, child.Recipe);
    }

    /// <summary>
    /// What a child's park, resume or escalation does to the parent (spec §5.8): a park propagates as a mirror with
    /// 'onParked: propagate', a resume ends the mirror, an escalation always suspends it 'escalated (child)'; a parent
    /// suspended by something else holds the change (the interpreter keeps the child's signal until it can apply).
    /// </summary>
    /// <param name="parent">The parent.</param>
    /// <param name="recipe">The parent's recipe (the wait's onParked).</param>
    /// <param name="change">The child's change.</param>
    /// <returns>The parent's effect.</returns>
    public static ParentEffect ToParent(WorkflowInstance parent, Recipe recipe, ChildChanged change)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(change);
        var wait = WaitOn(parent, recipe, change.Child);
        if (wait is null)
        {
            return Effect(ParentEffectKind.Ignore);
        }

        if (change.Change == ChildChange.Resumed)
        {
            return Effect(Resumed(parent, change.Child));
        }

        if (change.Change == ChildChange.Parked && wait.OnParked == OnParked.Ignore)
        {
            return Effect(ParentEffectKind.Ignore);
        }

        // A mirror of this same child ends before the new one takes its place (Release); any other suspension is the parent's own.
        if (parent.Suspension is { } suspension && suspension.Mirrors != change.Child)
        {
            return Effect(ParentEffectKind.Hold);
        }

        return change.Change == ChildChange.Parked
            ? new ParentEffect(ParentEffectKind.MirrorPark, Mirror(change, SuspensionKind.Park, escalation: null))
            : new ParentEffect(ParentEffectKind.Escalate, Mirror(change, SuspensionKind.Escalation, EscalationKind.Child));
    }

    /// <summary>Any change from a child first ends the suspension mirroring that child (spec §5.8), so a child that ends releases its parent.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="child">The child the change is from.</param>
    /// <returns>The parent with nothing mirroring that child, and the effects its ended parks free to send.</returns>
    public static ParentReleased Release(WorkflowInstance parent, WorkflowInstanceId child) => ChildRelease.Release(parent, child);

    /// <summary>Where an answer to the parent goes (spec §5.8): to the child its suspension mirrors; cancel and takeover act on the parent.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="trigger">The trigger given to the parent.</param>
    /// <returns>The child to forward it to; null when the parent takes it.</returns>
    public static WorkflowInstanceId? ForwardTo(WorkflowInstance parent, Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return trigger is AnswerGiven ? parent.Suspension?.Mirrors : null;
    }

    /// <summary>
    /// The answer forwarded to the child (spec §5.8, W17): the child's own trigger under the nonce minted for the child,
    /// keeping the actor who answered the parent, with 'onBehalfOf' naming the parent and that actor.
    /// </summary>
    /// <param name="parent">The parent whose suspension mirrors the child.</param>
    /// <param name="answer">The answer given to the parent.</param>
    /// <param name="stamp">The nonce minted for the child and the argument digest.</param>
    /// <returns>The child's answer.</returns>
    public static AnswerGiven Forward(WorkflowInstance parent, AnswerGiven answer, CommandStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(answer);
        return answer with { Stamp = stamp, Forwarded = new OnBehalfOf(parent.Id, answer.Actor) };
    }

    /// <summary>The children a parent's cancel cancels (spec §5.8): every one, whatever its state.</summary>
    /// <param name="parent">The cancelled parent.</param>
    /// <param name="children">The parent's children.</param>
    /// <returns>The children still to cancel.</returns>
    public static IReadOnlyList<WorkflowInstanceId> Cascade(WorkflowInstance parent, IReadOnlyList<WorkflowInstance> children)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(children);
        return [.. children.Where(c => c.Parent == parent.Id && !c.Ended).Select(static c => c.Id)];
    }

    /// <summary>The cancel a cascaded child gets (spec §5.8): origin parent, keyed by the parent; exempt from the approval check, so a drifted child is cancelled too.</summary>
    /// <param name="parent">The cancelled parent.</param>
    /// <param name="actor">The service that carries the parent's cancel.</param>
    /// <returns>The child's cancel.</returns>
    public static CancelRequested CancelFor(WorkflowInstance parent, string actor)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return new CancelRequested(actor, CancelOrigin.Parent, Stamp: null, Sequence: null, parent.Id);
    }

    // The wait on 'child' of the parent's recipe frame; null when the parent ended or waits on another child.
    private static ChildWait? WaitOn(WorkflowInstance parent, Recipe recipe, WorkflowInstanceId child)
    {
        var frame = parent.Frame;
        if (parent.Ended || frame.Child != child)
        {
            return null;
        }

        return recipe.States.TryGetValue(frame.State, out var state) ? state.WaitFor?.Child : null;
    }

    // A child's resume ends the mirror it is under; a mirror waiting behind a takeover holds the resume until the handback.
    private static ParentEffectKind Resumed(WorkflowInstance parent, WorkflowInstanceId child)
    {
        if (parent.Suspension?.Mirrors == child)
        {
            return ParentEffectKind.Release;
        }

        return Suspensions.PendingParks(parent).Any(p => p.Mirrors == child) ? ParentEffectKind.Hold : ParentEffectKind.Ignore;
    }

    private static ParentEffect Effect(ParentEffectKind kind) => new(kind, Suspend: null);

    private static Suspension Mirror(ChildChanged change, SuspensionKind kind, EscalationKind? escalation) => new(
        kind, kind == SuspensionKind.Park ? change.Reason : null, ParkKind: null, escalation, change.Question ?? string.Empty, change.Actor, change.Child,
        new ResumeAction(ResumeKind.Forward, Withheld: null, Gate: null, Effects: []));
}
