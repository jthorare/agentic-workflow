namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/search is asked: words and how many hits.</summary>
/// <param name="Q">The words, as typed (trimmed by the caller; 1 to 512 characters).</param>
/// <param name="Limit">How many hits at most (1 to 200); null leaves it to the server.</param>
/// <param name="Area">Narrows the hits to one feature; null for all.</param>
/// <param name="Repo">Narrows the hits to one repo; null for the session's or the profile's.</param>
internal sealed record MapSearchQuery(string Q, int? Limit = null, string? Area = null, string? Repo = null);
