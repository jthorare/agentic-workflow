namespace Aw.Sindri.Core.Evolve;

/// <summary>A proposal's state and attempt count after one gate run.</summary>
/// <param name="State">Staged or held.</param>
/// <param name="Attempts">Gate runs so far, this one included.</param>
internal sealed record StageOutcome(ProposalState State, int Attempts);
