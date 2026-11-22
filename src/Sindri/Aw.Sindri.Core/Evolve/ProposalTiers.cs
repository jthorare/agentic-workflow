using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Evolve;

/// <summary>The tier rules: computed from the files a proposal touches, failing closed to <see cref="ProposalTier.Approval"/>.</summary>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "The proposal store and the proposal_stage block (issue 3787, pieces 3b and 3c) call it; until then only the tests do.")]
internal static class ProposalTiers
{
    /// <summary>The tier of one proposal.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <returns>The tier and why.</returns>
    public static TierDecision For(Proposal proposal) => throw new NotSupportedException();

    /// <summary>The tier a public version ships at: the stricter of the original's and the rewrite's, so a rewrite never lowers it.</summary>
    /// <param name="original">The local original.</param>
    /// <param name="rewrite">The public rewrite.</param>
    /// <returns>The stricter decision.</returns>
    public static TierDecision Public(Proposal original, Proposal rewrite) => throw new NotSupportedException();
}
