using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The registry's wakes (spec §4.1, §5.8, W11; engine P6 integration): 'recipe.revoked' parks the instance at once
/// ('cancel' only), 'recipe.drifted' parks it for the re-approval, and 'recipe.approved' (a drift approval) resumes a
/// drift park and re-sends the effect the park carries, the one refused for the drift (§3.8). It never pops any other
/// park.
/// </summary>
public static partial class TransitionDecision
{
    private static Decision Woken(Deciding d, WorkflowInstance instance, RecipeWake wake)
    {
        if (instance.Ended)
        {
            return new Absorbed(d.Key, $"{instance.Address.Value} has ended; the {wake.Name} wake has nothing to park or resume");
        }

        if (wake.Kind == RecipeWakeKind.Revoked && instance.Suspension is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked })
        {
            return new Absorbed(d.Key, "already parked for the revoke");   // Suspend rebuilds a revoke park, so the repeat is caught here
        }

        return wake.Kind switch
        {
            RecipeWakeKind.Revoked => d.ChangedOrAbsorbed(
                d.Rules.Suspend(instance, RevokedPark(d.Recipe.Id.Value, wake.Actor)), instance, "already parked for the revoke"),
            RecipeWakeKind.Drifted => d.ChangedOrAbsorbed(
                d.Rules.Suspend(instance, DriftPark(d.Recipe.Id.Value, effect: null, wake.Actor)), instance, "already parked for drift"),
            RecipeWakeKind.Approved or _ => Reapproved(d, instance),
        };
    }

    /// <summary>
    /// A drift approval (§3.8): the drift park ends wherever it sits and its resume re-sends the effects it carries;
    /// nothing else is resumed. Behind a takeover or another park it is marked approved and re-sends once nothing else
    /// holds the instance.
    /// </summary>
    private static Decision Reapproved(Deciding d, WorkflowInstance instance)
    {
        var queue = Suspensions.PendingParks(instance);
        if (instance.Suspension is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Drift, Approved: false } park)
        {
            var ending = d.Rules.End(Suspensions.WithPending(instance, Approve(queue)), child: null);
            if (ending.IsError)
            {
                return new Rejected(d.Key, ending.Error);
            }

            // Another park takes effect: the approved drift park stays queued as a holder (EndCurrent) and its effects go out when nothing holds the instance.
            return ending.Value.PendingTookEffect ? d.Changed(ending.Value.Instance) : WithReleased(Resume(d, ending.Value.Instance, park.Resume), ending.Value.Released);
        }

        return queue.Any(static p => p is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Drift, Approved: false })
            ? d.Changed(Suspensions.WithPending(instance, Approve(queue)))
            : new Absorbed(d.Key, "the approval resumes only a drift park, and this instance has none");
    }

    private static List<Suspension> Approve(IReadOnlyList<Suspension> queue) =>
        [.. queue.Select(static p => p is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Drift, Approved: false } ? p with { Approved = true } : p)];

    /// <summary>Effects released because the last park holding them ended: they run again as new commands, listed on the event. Every caller of an ended suspension passes what it freed; none may drop it.</summary>
    private static Decision WithReleased(Decision decision, IReadOnlyList<RefusedEffect>? released)
    {
        if (released is not { Count: > 0 })
        {
            return decision;
        }

        return decision switch
        {
            Applied applied => applied with { Changes = Listed(applied.Changes, applied.Effects, released), Effects = Sent(applied.Effects, released) },
            Refused refused => refused with { Changes = Listed(refused.Changes, refused.Effects, released), Effects = Sent(refused.Effects, released) },
            _ => decision,
        };
    }

    private static IReadOnlyList<InstanceChange> Listed(IReadOnlyList<InstanceChange> changes, IReadOnlyList<EffectCommand> sent, IReadOnlyList<RefusedEffect> released)
    {
        var more = New(sent, released);
        return more.Count == 0 || changes.Count == 0
            ? changes
            : [changes[0] with { Event = changes[0].Event with { Effects = [.. changes[0].Event.Effects ?? [], .. more.Select(static e => e.Identity)] } }, .. changes.Skip(1)];
    }

    private static List<EffectCommand> Sent(IReadOnlyList<EffectCommand> sent, IReadOnlyList<RefusedEffect> released) =>
        [.. sent, .. New(sent, released).Select(static e => new EffectCommand(e.Kind, e.Identity, e.Origin, e.With))];

    private static List<RefusedEffect> New(IReadOnlyList<EffectCommand> sent, IReadOnlyList<RefusedEffect> released)
    {
        var known = sent.Select(static e => e.Identity).ToHashSet();
        return [.. released.Where(e => !known.Contains(e.Identity))];
    }
}
