namespace Aw.Sindri.Core.Evolve;

/// <summary>Where a proposal stands. Only a staged proposal has a public version that passed the leak gate.</summary>
internal enum ProposalState
{
    /// <summary>Saved from a finding; local only.</summary>
    Proposed,

    /// <summary>Its public rewrite passed the judge and every deny layer.</summary>
    Staged,

    /// <summary>The gate held it; retried until <see cref="ProposalStates.MaxAttempts"/> attempts.</summary>
    Held,

    /// <summary>Its public version went out.</summary>
    Published,

    /// <summary>The change it asked for merged.</summary>
    Merged,

    /// <summary>The owner turned it down; the same proposal is not saved again.</summary>
    Rejected,
}
