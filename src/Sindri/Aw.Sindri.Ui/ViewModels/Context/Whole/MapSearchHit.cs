namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One match of a search over the map, shaped like a hit of the context search endpoint so the swap is a new adapter.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Kind">Its kind as served.</param>
/// <param name="Label">Its label.</param>
/// <param name="Area">The feature (area) it sits in; null when not known.</param>
/// <param name="Repo">The repo it belongs to; null when not known.</param>
/// <param name="Score">Its rank score; null for an unranked stopgap.</param>
/// <param name="Matched">What matched: "title", "path" or "text"; null when not known.</param>
/// <param name="Snippet">The matching text; null when none.</param>
internal sealed record MapSearchHit(string Key, string Kind, string Label, string? Area, string? Repo = null, double? Score = null, string? Matched = null, string? Snippet = null);
