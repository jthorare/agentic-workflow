namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>The accepted command of a resume.</summary>
/// <param name="CommandId">The attempt's id.</param>
/// <param name="Sequence">The domain event's ledger sequence.</param>
internal sealed record JobResumeAccepted(string CommandId, long Sequence);
