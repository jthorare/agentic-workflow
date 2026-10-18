using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The engine triggers this unit routes to the suspension and child rules (spec §3.8, §5.8): cancel, takeover,
/// handback, a child's park, resume or escalation, and answers; P5's timers and open-requests go to
/// TransitionDecision.Timers.cs. Recipe wakes and effect refusals come with P2 and P6.
/// </summary>
public static partial class TransitionDecision
{
    private static Decision DecideEngineTrigger(Deciding d)
    {
        var instance = d.Input.Instance;
        if (Unapproved(d, instance, transition: null) is { } unapproved)
        {
            return unapproved;
        }

        return d.Trigger switch
        {
            CancelRequested when !instance.Ended => d.Changed(d.Rules.Cancel(instance)),
            TakeoverRequested => d.Rules.TakeOver(instance, d.Trigger.Actor).Match<Decision>(d.Changed, e => new Rejected(d.Key, e)),
            HandbackRequested => Handback(d, instance),
            ChildChanged change => Child(d, instance, change),
            AnswerGiven answer when WaitingForAnswer(instance, d.Recipe) is { } wait => AnswerTheWait(d, instance, answer, wait),
            AnswerGiven answer => Answer(d, instance, answer),
            CancelRequested => new Rejected(d.Key, NotAllowed(d.Trigger, instance)),
            DeadlineFired { Kind: DeadlineKind.TakeoverIdle } idle => Idle(d, instance, idle),
            DeadlineFired fired => Expired(d, instance, fired),
            OpenRequested open => OpenRequest(d, instance, open),
            OutboxRefused refused => OutboxRefusal(d, instance, refused),
            RecipeWake wake => Woken(d, instance, wake),
            RuntimeUnavailable unavailable => Unavailable(d, instance, unavailable),
            LaunchRefused refused => LaunchParked(d, instance, refused),
            _ => throw new NotSupportedException($"{d.Trigger.Name} is decided by the engine-owned rules its phase adds."),
        };
    }

    /// <summary>'handback' (§3.8): ends the takeover; the park it covered, or one that arrived meanwhile, takes effect again.</summary>
    private static Decision Handback(Deciding d, WorkflowInstance instance)
    {
        if (instance.State != EngineStates.TakenOver)
        {
            return new Rejected(d.Key, NotAllowed(d.Trigger, instance));
        }

        var ending = d.Rules.End(instance, child: null);
        return ending.IsError ? new Rejected(d.Key, ending.Error) : WithReleased(d.Changed(ending.Value.Instance), ending.Value.Released);
    }

    /// <summary>The change's release of the parent's mirror, then <paramref name="then"/>; the effects the release frees are sent with it.</summary>
    private static Decision ReleasedChild(Deciding d, WorkflowInstance instance, ChildChanged change, Func<WorkflowInstance, Decision> then)
    {
        var freed = d.Rules.Release(instance, change.Child);
        return WithReleased(then(freed.Instance), freed.Released);
    }

    /// <summary>
    /// A child's park, resume or escalation (§5.8): mirrored, released, ignored, or held while the parent is busy. Any
    /// change from the child first ends the suspension that mirrors it, so a new mirror replaces an old one.
    /// </summary>
    private static Decision Child(Deciding d, WorkflowInstance instance, ChildChanged change)
    {
        var effect = d.Rules.ToParent(instance, d.Recipe, change);
        return effect.Kind switch
        {
            ParentEffectKind.MirrorPark or ParentEffectKind.Escalate when effect.Suspend is { } suspend =>
                ReleasedChild(d, instance, change, freed => d.Changed(d.Rules.Suspend(freed, suspend))),
            ParentEffectKind.Release => ReleasedChild(d, instance, change, freed => d.ChangedOrAbsorbed(freed, instance, "nothing mirrors that child")),
            ParentEffectKind.Hold => new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
                $"{instance.Address.Value} is busy, so the child's {change.Name} stays pending (fix: none needed; it applies when the parent can take it)")),
            ParentEffectKind.Ignore or ParentEffectKind.MirrorPark or ParentEffectKind.Escalate or _ =>
                new Absorbed(d.Key, $"the child's {change.Name} leaves the parent as it is: nothing waits on or mirrors that child, or its wait says onParked: ignore"),
        };
    }
}
