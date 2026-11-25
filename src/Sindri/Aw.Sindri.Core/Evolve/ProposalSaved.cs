namespace Aw.Sindri.Core.Evolve;

/// <summary>The proposal a save landed on, and how.</summary>
/// <param name="Id">The new row, the open row it merged into, or the rejected row it matched.</param>
/// <param name="Outcome">What the save did.</param>
public sealed record ProposalSaved(ProposalId Id, ProposalSaveOutcome Outcome);
