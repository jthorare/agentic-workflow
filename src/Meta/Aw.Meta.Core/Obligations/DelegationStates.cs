using Aw.Meta.Core.Delegations;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;

namespace Aw.Meta.Core.Obligations;

/// <summary>Derives where a delegation stands from the recorded messages; only the delegation itself is stored.</summary>
public static class DelegationStates
{
    /// <summary>The delegation's state now.</summary>
    /// <param name="delegation">The delegation.</param>
    /// <param name="inbound">Every inbound message.</param>
    /// <param name="outbound">Every message the meta sent.</param>
    /// <param name="lanes">The lane map, so a send addressed to the lane's session id counts as well as one to its name.</param>
    /// <returns>Answered when the lane sent a message naming the issue after the delegation; sent when aw-meta delivered it or the meta sent the lane a message naming the issue since; owed otherwise.</returns>
    public static DelegationState Of(Delegation delegation, IReadOnlyList<InboxMessage> inbound, IReadOnlyList<Outbound> outbound, LaneMapState lanes)
    {
        ArgumentNullException.ThrowIfNull(delegation);
        ArgumentNullException.ThrowIfNull(inbound);
        ArgumentNullException.ThrowIfNull(outbound);
        ArgumentNullException.ThrowIfNull(lanes);
        if (inbound.Any(m => m.At > delegation.At && Recipient.Matches(delegation.Session, m.FromName, m.From) && DelegationText.Names(m.Body, delegation.Issue)))
        {
            return DelegationState.Answered;
        }

        return IsSent(delegation, outbound, lanes) ? DelegationState.Sent : DelegationState.Owed;
    }

    /// <summary>Whether the ACTION message reached the lane: aw-meta delivered it, or a send to the lane's session since the delegation names the issue (aw-meta's own attempt to a lane that couldn't act on it does not count).</summary>
    /// <param name="delegation">The delegation.</param>
    /// <param name="outbound">Every message the meta sent.</param>
    /// <param name="lanes">The lane map: a send to the lane's session name or to its session id counts, as for a reply.</param>
    /// <returns>True when sent.</returns>
    public static bool IsSent(Delegation delegation, IReadOnlyList<Outbound> outbound, LaneMapState lanes)
    {
        ArgumentNullException.ThrowIfNull(delegation);
        ArgumentNullException.ThrowIfNull(outbound);
        ArgumentNullException.ThrowIfNull(lanes);
        var lane = Assignment(delegation, lanes);
        return delegation.Delivered
            || outbound.Any(o => o.At >= delegation.At && !IsAttempt(o, delegation) && Recipient.Matches(o.To, lane) && DelegationText.Names(o.Message, delegation.Issue));
    }

    // The lane map's entry for the delegation's session (it carries the session id); just the name when the map has none.
    private static LaneAssignment Assignment(Delegation delegation, LaneMapState lanes) =>
        lanes.Entries.FirstOrDefault(e => string.Equals(e.SessionName, delegation.Session, StringComparison.OrdinalIgnoreCase))
        ?? new LaneAssignment(delegation.Lane, delegation.Session, Session: null, LaneSource.Direct);

    private static bool IsAttempt(Outbound sent, Delegation delegation) =>
        delegation.Attempt is { } attempt && sent.Id is { } id && id.Equals(attempt);
}
