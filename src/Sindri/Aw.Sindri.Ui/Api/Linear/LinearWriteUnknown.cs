namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>504 SND-LINEAR-011 of a write route: the route re-read and couldn't decide. Retry sends the same body (and the same client id).</summary>
/// <param name="ClientId">The comment's or new issue's client id; null for an edit.</param>
internal sealed record LinearWriteUnknown(string? ClientId) : LinearWriteOutcome;
