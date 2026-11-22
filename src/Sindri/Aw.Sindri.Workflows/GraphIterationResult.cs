namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How one batch ended.</summary>
/// <param name="Failure">Why the batch stopped (the first failed item), or null when every item ran.</param>
/// <param name="Skipped">How many of the batch's items a body node with 'onFailure: skip-item' dropped (#3806); 0 in a history written before it.</param>
/// <param name="Tokens">The tokens the batch's nodes used (#3805), which the parent adds to the run's total; 0 in a history written before it.</param>
internal sealed record GraphIterationResult(string? Failure, int Skipped = 0, long Tokens = 0);
