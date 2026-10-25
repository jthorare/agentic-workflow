using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The engine triggers Phase 5 adds (engine spec §3.4, §3.8, §5.2, §5.5; UI spec E-6): a state or retry timer that
/// fired, the takeover's idle reminder, and an 'open-request'. Engine P5: unit
/// (a) built the state timers and unit (b) 'takeoverIdle' and 'open-request'.
/// </summary>
public static partial class TransitionDecision
{
    /// <summary>
    /// A state or retry timer fired in the entry it was armed in (spec §3.4, §5.5): 'then: park' retries once like 'then: retry' and then parks with 'timeout' (owner q93; the 'timeout-retries-before-park-v1' patch),
    /// 'then: escalate' escalates (kind timeout), 'then: retry' re-arms once in the same entry and re-emits the entering
    /// transition's effects (new command ids, the same effect identity), and a retry timer that fires parks with
    /// 'timeout'. In desk mode every 'then' parks (W20). A timer of an entry that has ended, or of a suspended instance,
    /// is absorbed. Unit (a).
    /// </summary>
    private static Decision Expired(Deciding d, WorkflowInstance instance, DeadlineFired fired)
    {
        if (instance.Ended || instance.Suspension is not null || fired.Entry != instance.Frame.Entry
            || d.Recipe.States.GetValueOrDefault(instance.Frame.State) is not { Timeout: { } timeout } state)
        {
            return new Absorbed(d.Key, string.Create(CultureInfo.InvariantCulture,
                $"the {fired.Kind} timer of entry {fired.Entry} has nothing to time: {instance.Address.Value} is {instance.State} in entry {instance.Frame.Entry}"));
        }

        var after = StateTimers.Timeout(state, instance.Frame.Run, d.Context.DeskTimeout) ?? timeout.After.Span;
        var desk = instance.Frame.Run is { Runtime: StepRuntime.Desk };
        var retried = fired.Kind == DeadlineKind.Retry || instance.Frame.Retried;
        var then = Action(d, state, timeout, desk, retried);
        return then switch
        {
            TimeoutAction.Escalate => d.Changed(d.Rules.Suspend(instance, new Suspension(
                SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Timeout, DeskTimeouts.RanPast(instance.Frame.State, after),
                fired.Actor, Mirrors: null, new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effects: [])))),
            TimeoutAction.Retry => Retry(d, instance),
            TimeoutAction.Park or _ => d.Changed(d.Rules.Suspend(instance, DeskTimeouts.TimeoutPark(instance.Frame.State, after, fired.Actor))),
        };
    }

    /// <summary>
    /// What an expiry does: a desk run always parks (W20); a second expiry of a 'then: retry' state parks; and (owner q93, 2026-10-23) a 'then: park' state that has not
    /// retried yet retries once first, unless it waits for a child (a child can't be retried, RCP-TIME-002) or the history predates the patch.
    /// </summary>
    private static TimeoutAction Action(Deciding d, RecipeState state, TimeoutSpec timeout, bool desk, bool retried)
    {
        if (desk || (retried && timeout.Then == TimeoutAction.Retry))
        {
            return TimeoutAction.Park;
        }

        return timeout.Then == TimeoutAction.Park && !retried && !d.Context.LegacyTimeoutPark && state.WaitFor?.Child is null
            ? TimeoutAction.Retry
            : timeout.Then;
    }

    /// <summary>
    /// 'then: retry' (spec §3.4, §5.7): the same entry, run and inbox, marked retried so the interpreter arms the retry
    /// timer; the transition that entered the state has its effects re-emitted under the same identities, which their
    /// existence checks make harmless. The initial state was entered by no transition, so it re-emits nothing.
    /// </summary>
    private static Applied Retry(Deciding d, WorkflowInstance instance)
    {
        var retried = instance with { Frame = instance.Frame with { Retried = true } };
        var effects = instance.Frame.EnteredBy is { } index && index < d.Recipe.Transitions.Count
            ? Effects(d.Recipe.Transitions[index], instance.Id, instance.Frame.Entry, d.Context.Catalog, actor: null)
            : [];
        var own = d.Change(retried, WorkflowEventType.Transition, Records.None);
        own = own with { Event = own.Event with { Effects = [.. effects.Select(static e => e.Identity)] } };
        return new Applied(d.Key, [own], effects, []);
    }

    /// <summary>
    /// The takeover's idle reminder fired (spec §5.5, §6.3): while still taken over, one 'workflow.notified' and one
    /// engine-origin 'notify.desk' ("hand back {item}?"), and the instance stays as it is; it never hands back. After a
    /// handback it is absorbed. Until P6 delivers 'notify.desk', the decision records the 'workflow.notified' row only.
    /// </summary>
    private static Decision Idle(Deciding d, WorkflowInstance instance, DeadlineFired fired) =>
        instance is { Ended: false, Suspension.Kind: SuspensionKind.Takeover }
            ? new Applied(d.Key, [d.Change(instance, WorkflowEventType.Notified, Records.None)], [], [])
            : new Absorbed(d.Key, string.Create(CultureInfo.InvariantCulture,
                $"the takeoverIdle timer of entry {fired.Entry} has no takeover to remind of: {instance.Address.Value} is {instance.State}"));

    /// <summary>
    /// An 'open-request' (UI spec E-6; spec §5.2): a takeover with actor 'service:engine' (any other actor is SND-WF-003) wherever a takeover is allowed,
    /// exempt from the approval check; absorbed (a no-op) when the instance is already taken over; SND-WF-003 when it
    /// has ended, which the launcher reports as the launch's failure.
    /// </summary>
    private static Decision OpenRequest(Deciding d, WorkflowInstance instance, OpenRequested open) => open.Actor switch
    {
        not EngineActor.Name => new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
            $"an open-request comes from {EngineActor.Name}, not {open.Actor} (fix: the launcher sends it; run the launch from the desk)")),
        _ when !LaunchId.TryFrom(open.Launch, out _) => new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
            $"an open-request names launch '{open.Launch}', not a launch id: 'LCH-' and 32 lower-case hex digits (fix: the launcher sends the id it minted)")),
        _ when instance is { Ended: false, Suspension.Kind: SuspensionKind.Takeover } =>
            new Absorbed(d.Key, $"{instance.Address.Value} is already taken over; launch {open.Launch} opens beside it"),
        _ => d.Rules.TakeOver(instance, EngineActor.Name).Match<Decision>(d.Changed, e => new Rejected(d.Key, e)),
    };
}
