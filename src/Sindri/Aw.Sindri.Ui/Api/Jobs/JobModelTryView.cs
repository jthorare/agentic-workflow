namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>One harness a model call tried.</summary>
/// <param name="Harness">'claude-code', 'codex', 'cursor', or 'jev'.</param>
/// <param name="Model">The model asked for.</param>
/// <param name="Outcome">'answered', 'limit', 'failed', 'skipped' or 'not-asked'; a word this build doesn't know is shown as served.</param>
/// <param name="Reason">A code ('exit-1', 'limited', 'no-key', ...), or null.</param>
/// <param name="Ms">How long the try took.</param>
internal sealed record JobModelTryView(string Harness, string Model, string Outcome, string? Reason, int Ms);
