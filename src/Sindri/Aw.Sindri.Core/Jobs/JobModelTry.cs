namespace Aw.Sindri.Core.Jobs;

/// <summary>One harness a stored model call tried (<see cref="JobModelCall.Tries"/>); the jobs-side copy of the ledger event's try.</summary>
/// <param name="Harness">'claude-code', 'codex', 'cursor', or 'jev'.</param>
/// <param name="Model">The model asked for.</param>
/// <param name="Outcome">'answered', 'limit', 'failed', 'skipped' or 'not-asked'.</param>
/// <param name="Reason">A code, never CLI text; null when the try answered.</param>
/// <param name="Ms">How long the try took.</param>
/// <param name="Source">Where the model name came from.</param>
public sealed record JobModelTry(string Harness, string Model, string Outcome, string? Reason, int Ms, string Source);
