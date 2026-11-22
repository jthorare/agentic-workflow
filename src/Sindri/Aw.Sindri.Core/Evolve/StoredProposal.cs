namespace Aw.Sindri.Core.Evolve;

/// <summary>A proposal as the store keeps it. The original and its sources stay local; only a gated public version may leave the box.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Original">The proposal as the model drafted it.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Tier">Its tier.</param>
/// <param name="Attempts">Gate runs so far.</param>
/// <param name="HeldBy">The layer that held it last, or null.</param>
/// <param name="Sessions">The opaque refs of the sessions it was built from.</param>
/// <param name="Origin">Where it came from.</param>
/// <param name="CreatedAt">When it was first saved.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record StoredProposal(
    ProposalId Id,
    Proposal Original,
    ProposalState State,
    ProposalTier Tier,
    int Attempts,
    LeakLayer? HeldBy,
    IReadOnlyList<string> Sessions,
    string Origin,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Gets a value indicating whether the owner may still reject it: proposed, staged or held.</summary>
    public bool Rejectable => State is ProposalState.Proposed or ProposalState.Staged or ProposalState.Held;
}
