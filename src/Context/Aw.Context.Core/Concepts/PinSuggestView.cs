namespace Aw.Context.Core.Concepts;

/// <summary>'GET /sindri/context/pins/suggest': what the "Pin this" and "Add several" forms offer. Only the answer of the mode the query asked for is set.</summary>
/// <param name="ApiVersion">1.</param>
/// <param name="Repo">The repo this answer is for, or null.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Terms">A concept's terms with the records each would newly link, best term first; null unless a concept was asked for.</param>
/// <param name="Features">The features a search found; null unless a search was asked for.</param>
/// <param name="Matches">One entry per line of a pasted list, in order; null unless lines were sent.</param>
public sealed record PinSuggestView(
    int ApiVersion,
    string? Repo,
    string MapVersion,
    IReadOnlyList<PinSuggestTerm>? Terms,
    IReadOnlyList<PinSuggestFeature>? Features,
    IReadOnlyList<PinSuggestMatch>? Matches);
