namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/pins/suggest: what the "Pin this" and "Add several" forms offer. Only the answer of the mode asked is set.</summary>
/// <param name="ApiVersion">The answer's version.</param>
/// <param name="Repo">The repo the answer is for; null when the profile has several and none was asked for.</param>
/// <param name="MapVersion">The map version read.</param>
/// <param name="Terms">A concept's terms with the records each would newly link, best first; null unless a concept was asked for.</param>
/// <param name="Features">The features a search found, or a concept's member features; null unless a concept or a search was asked for.</param>
/// <param name="Matches">One entry per line of a pasted list, in order; null unless lines were sent.</param>
internal sealed record PinSuggestView(
    int ApiVersion,
    string? Repo,
    string MapVersion,
    IReadOnlyList<PinSuggestTerm>? Terms,
    IReadOnlyList<PinSuggestFeature>? Features,
    IReadOnlyList<PinSuggestMatch>? Matches);
