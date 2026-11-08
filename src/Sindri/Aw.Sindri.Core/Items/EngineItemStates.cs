using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// The item states a root instance's ledger rows give an item (API spec section 3.6, API P11): an open desk run needs an answer,
/// an agent run or a wait is running, a park or escalation is parked, a takeover is taken over, and an ended instance is done
/// (cancelled when a cancel ended it). Pure folds of the rows: a delta that leaves a part out keeps the part that came before.
/// Rows from before 'FrameSummary.Runtime' with an open run read as desk, the only runtime before the agent runtime (P8 must re-check).
/// </summary>
public static class EngineItemStates
{
    /// <summary>
    /// The part after an event, or null when the event leaves the item alone: a child instance's row, a row of an earlier attempt that already ended,
    /// or a row after the instance ended. The part is the latest root's, which the item's state follows; another root of the item that is still live
    /// (a second recipe started on it) rides in <see cref="EngineItemPart.Earlier"/>, moves with its own rows and leaves when it ends.
    /// </summary>
    /// <param name="prior">The part so far, or null before the first row.</param>
    /// <param name="eventType">The ledger event type.</param>
    /// <param name="payload">The event's payload.</param>
    /// <returns>The new part, or null.</returns>
    public static EngineItemPart? Next(EngineItemPart? prior, string eventType, WorkflowEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(payload);
        if (!IsRoot(payload.Instance))
        {
            return null;
        }

        if (string.Equals(eventType, EventTypes.WorkflowStarted, StringComparison.Ordinal))
        {
            return Started(prior, payload);
        }

        if (prior is null)
        {
            return null;
        }

        if (Continues(prior, payload.Instance))
        {
            return Build(payload, prior) with { Earlier = prior.Earlier };
        }

        return Moved(prior, payload);
    }

    /// <summary>A root started: the new latest. The root it replaces stays in the set while it is live.</summary>
    private static EngineItemPart Started(EngineItemPart? prior, WorkflowEventPayload payload)
    {
        var fresh = Build(payload, carried: null);
        if (prior is null)
        {
            return fresh;
        }

        var kept = prior.Earlier?.Where(p => !SameAddress(p.Address, payload.Instance)).ToList() ?? [];
        if (!prior.Ended && !SameAddress(prior.Address, payload.Instance))
        {
            kept.Insert(0, prior with { Earlier = null });
        }

        return kept.Count == 0 ? fresh : fresh with { Earlier = kept };
    }

    /// <summary>An earlier live root's row: that root moves, or leaves the set when it ended; null when the row is no live root's.</summary>
    private static EngineItemPart? Moved(EngineItemPart prior, WorkflowEventPayload payload)
    {
        var earlier = prior.Earlier;
        var at = earlier?.ToList().FindIndex(p => SameAddress(p.Address, payload.Instance)) ?? -1;
        if (earlier is null || at < 0)
        {
            return null;
        }

        var moved = Build(payload, earlier[at]);
        var rest = earlier.ToList();
        if (moved.Ended)
        {
            rest.RemoveAt(at);
        }
        else
        {
            rest[at] = moved;
        }

        return prior with { Earlier = rest.Count == 0 ? null : rest };
    }

    /// <summary>The root instance <paramref name="address"/> as the item's part holds it: the latest root's part or an earlier live root's; null when the item holds no such root.</summary>
    /// <param name="part">The item's engine part, or null.</param>
    /// <param name="address">The root instance's address.</param>
    /// <returns>That root's part, or null.</returns>
    internal static EngineItemPart? PartOf(EngineItemPart? part, string address) =>
        part is null ? null
        : SameAddress(part.Address, address) ? part
        : part.Earlier?.FirstOrDefault(p => SameAddress(p.Address, address));

    /// <summary>The addresses of the item's root instances that are not finished: the latest root's when it is live, then the earlier live ones.</summary>
    /// <param name="part">The item's engine part.</param>
    /// <returns>The addresses.</returns>
    internal static IEnumerable<string> Live(EngineItemPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return (part.Ended ? [] : new[] { part.Address }).Concat(part.Earlier?.Select(static p => p.Address) ?? []);
    }

    private static bool SameAddress(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);

    private static EngineItemPart Build(WorkflowEventPayload payload, EngineItemPart? carried)
    {
        var frame = payload.Detail?.Delta.Frame;
        var (runOpen, runtime) = Run(frame, carried);
        var ended = Ends(payload);
        return new EngineItemPart(
            payload.Instance,
            frame?.State ?? carried?.State ?? payload.To,
            runOpen,
            runtime,
            Suspension(payload.Detail, carried),
            ended,
            ended && Cancels(payload.Trigger))
        {
            Reason = Reason(payload.Detail, carried),
        };
    }

    // The reason of the park the row shows; a row without detail keeps the one before, and a row that leaves the park (or shows another suspension) has none.
    private static ParkReason? Reason(WorkflowEventDetail? detail, EngineItemPart? carried) =>
        detail is null ? carried?.Reason : detail.Delta.Suspension is { Kind: SuspensionKind.Park } park ? park.Reason : null;

    /// <summary>
    /// Whether the row ended the instance: its delta names an outcome, or (a row stored without its detail, as the scrub's fallback does) its
    /// trigger is the cancel, which always ends it. Another terminal move whose detail was dropped can't be told from the row, which names only
    /// the state entered.
    /// </summary>
    private static bool Ends(WorkflowEventPayload payload) =>
        payload.Detail is { } detail ? detail.Delta.Outcome is not null : Cancels(payload.Trigger);

    private static bool Cancels(string trigger) => string.Equals(trigger, TriggerNames.Cancel, StringComparison.Ordinal);

    private static SuspensionKind? Suspension(WorkflowEventDetail? detail, EngineItemPart? carried) =>
        detail is null ? carried?.Suspension : detail.Delta.Suspension?.Kind;

    private static bool Continues(EngineItemPart prior, string address) =>
        !prior.Ended && SameAddress(prior.Address, address);

    private static (bool Open, StepRuntime? Runtime) Run(FrameSummary? frame, EngineItemPart? carried) => frame switch
    {
        { Run: null } => (false, null),
        { } open => (true, open.Runtime ?? StepRuntime.Desk),
        null => (carried?.RunOpen ?? false, carried?.Runtime),
    };

    /// <summary>How many of an item's live root instances are parked as unavailable (a dependency of their Step is down): the latest root's, and any earlier live one's.</summary>
    /// <param name="part">The item's engine part, or null before the engine touched the item.</param>
    /// <returns>The count.</returns>
    public static int Unavailable(EngineItemPart? part) =>
        part is null ? 0 : (IsUnavailable(part) ? 1 : 0) + (part.Earlier?.Count(IsUnavailable) ?? 0);

    private static bool IsUnavailable(EngineItemPart part) =>
        part is { Ended: false, Suspension: SuspensionKind.Park, Reason: ParkReason.Unavailable };

    /// <summary>The item state a part reads as.</summary>
    /// <param name="part">The part.</param>
    /// <returns>The state.</returns>
    public static ItemState StateOf(EngineItemPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return part switch
        {
            { Ended: true, Cancelled: true } => ItemState.Cancelled,
            { Ended: true } => ItemState.Done,
            { Suspension: SuspensionKind.Takeover } => ItemState.TakenOver,
            { Suspension: SuspensionKind.Park, Reason: ParkReason.Unavailable } => ItemState.Running,   // a dependency is down: nobody is asked, so the item is not waiting on you (health shows it)
            { Suspension: SuspensionKind.Park or SuspensionKind.Escalation } => ItemState.Parked,
            { RunOpen: true, Runtime: StepRuntime.Desk } => ItemState.NeedsAnswer,
            _ => ItemState.Running,
        };
    }

    /// <summary>Whether <paramref name="address"/> is a root instance's: '{item}/{recipe}/{attempt}', with no child generation after it.</summary>
    /// <param name="address">The address.</param>
    /// <returns>True for a root.</returns>
    internal static bool IsRoot(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.AsSpan().Count('/') == 2;
    }
}
