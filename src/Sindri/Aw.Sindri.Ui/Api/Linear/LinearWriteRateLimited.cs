namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>429 SND-LINEAR-012 of a write route: nothing was sent.</summary>
/// <param name="RetryAfter">When to try again.</param>
internal sealed record LinearWriteRateLimited(DateTimeOffset RetryAfter) : LinearWriteOutcome;
