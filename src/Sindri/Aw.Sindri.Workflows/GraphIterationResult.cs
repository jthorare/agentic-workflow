namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How one batch ended.</summary>
/// <param name="Failure">Why the batch stopped (the first failed item), or null when every item ran.</param>
internal sealed record GraphIterationResult(string? Failure);
