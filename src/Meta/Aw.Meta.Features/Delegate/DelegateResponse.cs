using Aw.Meta.Core.Delegations;

namespace Aw.Meta.Features.Delegate;

/// <summary>The Delegate response.</summary>
/// <param name="Outcome">"sent" (aw-meta delivered the message), "owed" (the meta still sends it) or "no-lane" (nothing filed: pick a lane).</param>
/// <param name="Delegation">The recorded delegation; null for "no-lane".</param>
/// <param name="Lanes">The team's lanes with the keywords each one hit; filled only for "no-lane".</param>
public sealed record DelegateResponse(string Outcome, DelegationView? Delegation, IReadOnlyList<LaneHint> Lanes)
{
    /// <summary>Why aw-meta could not deliver the message to a non-Claude lane, so it is owed; null otherwise.</summary>
    public string? SendProblem { get; init; }

    /// <summary>The line appended to the running log; null for "no-lane".</summary>
    public string? LogLine { get; init; }
}
