namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The run an escalation follows (model-pick spec 5): what failed, so the pick can leave that model and everything below it.</summary>
/// <param name="Model">The model that failed.</param>
/// <param name="Kind">'refused' or 'check-failed'.</param>
/// <param name="Check">The check's name, when a check failed; else null.</param>
/// <param name="Tokens">What the failed run spent.</param>
internal sealed record PreviousRun(string Model, string Kind, string? Check, int Tokens);
