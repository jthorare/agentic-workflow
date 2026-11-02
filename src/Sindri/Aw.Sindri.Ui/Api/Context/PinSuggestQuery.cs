namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/pins/suggest is asked: exactly one of a concept, a search and a pasted list.</summary>
/// <param name="Repo">The repo to read; empty for the profile's only repo.</param>
/// <param name="Concept">The concept whose terms to count ('concept:billing'), or null.</param>
/// <param name="Search">The text to find in the features' labels and keys; "" lists every feature; null when not searching.</param>
/// <param name="Limit">The most features a search answers; null for the server's 50.</param>
/// <param name="Lines">The pasted list to match to concepts, or null. The client sends it 25 lines at a time.</param>
internal sealed record PinSuggestQuery(string Repo, string? Concept = null, string? Search = null, int? Limit = null, IReadOnlyList<PinSuggestLine>? Lines = null);
