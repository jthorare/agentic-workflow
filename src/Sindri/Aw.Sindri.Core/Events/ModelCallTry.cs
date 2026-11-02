namespace Aw.Sindri.Core.Events;

/// <summary>One harness a model call tried (<see cref="ContextModelCalled.Tries"/>).</summary>
/// <param name="Harness">'claude-code', 'codex', 'cursor', or 'jev' in this event only.</param>
/// <param name="Model">The model asked for.</param>
/// <param name="Outcome">What came of it; see <see cref="ModelCallOutcome"/>.</param>
/// <param name="Reason">A code ('exit-1', 'cli-reported-error', 'timeout', 'limit', 'no-key', 'off'), never CLI text; null when the try answered.</param>
/// <param name="Ms">How long the try took.</param>
/// <param name="Source">Where the model name came from: 'request', 'kind.models', 'default.models', 'names' or 'builtin'.</param>
public sealed record ModelCallTry(string Harness, string Model, string Outcome, string? Reason, int Ms, string Source);
