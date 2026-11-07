namespace Aw.Sindri.Features.SuggestLinks;

/// <summary>The suggestions, newest pull request first.</summary>
/// <param name="Suggestions">One per (issue, pull request).</param>
internal sealed record SuggestLinksResponse(IReadOnlyList<LinkSuggestion> Suggestions);
