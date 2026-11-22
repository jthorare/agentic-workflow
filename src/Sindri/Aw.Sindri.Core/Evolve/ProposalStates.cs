using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Evolve;

/// <summary>The state rules around the gate: who may be gated, and what a verdict does.</summary>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "The proposal_stage block (issue 3787, piece 3c) calls it; until then only the tests do.")]
internal static class ProposalStates
{
    /// <summary>The most gate runs a proposal gets; after that it stays held until the owner rejects it.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Whether a proposal may go through the gate now.</summary>
    /// <param name="state">Its state.</param>
    /// <param name="attempts">Gate runs so far.</param>
    /// <returns>True for a proposed one, or a held one with attempts left.</returns>
    public static bool CanStage(ProposalState state, int attempts) => throw new NotSupportedException();

    /// <summary>The state after one gate run.</summary>
    /// <param name="verdict">The gate's verdict.</param>
    /// <param name="attempts">Gate runs before this one.</param>
    /// <returns>Staged when clean, held otherwise; the attempt count goes up by one either way.</returns>
    public static StageOutcome After(LeakVerdict verdict, int attempts) => throw new NotSupportedException();
}
