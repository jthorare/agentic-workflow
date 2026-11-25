namespace Aw.Sindri.Core.Evolve;

/// <summary>One proposal as the reads show it (GET /sindri/proposals, 'sindri proposals'). Title and target are model output: a client fences them as untrusted.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Kind">What it changes.</param>
/// <param name="Target">What it changes, by name.</param>
/// <param name="Title">Its title.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Tier">Its tier.</param>
/// <param name="Attempts">Gate runs so far.</param>
/// <param name="HeldReason">Why the gate held it last, naming the layer only; null when it isn't held.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record ProposalSummary(ProposalId Id, ProposalKind Kind, string Target, string Title, ProposalState State, ProposalTier Tier, int Attempts, string? HeldReason, DateTimeOffset UpdatedAt)
{
    /// <summary>The summary of a stored proposal.</summary>
    /// <param name="stored">The proposal.</param>
    /// <returns>Its summary.</returns>
    public static ProposalSummary From(StoredProposal stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var held = stored.State == ProposalState.Held && stored.HeldBy is { } layer ? LeakReasons.For(layer) : null;
        return new ProposalSummary(stored.Id, stored.Original.Kind, stored.Original.Target, stored.Original.Title, stored.State, stored.Tier, stored.Attempts, held, stored.UpdatedAt);
    }
}
