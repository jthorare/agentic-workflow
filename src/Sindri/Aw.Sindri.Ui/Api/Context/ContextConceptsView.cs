namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// GET /sindri/context/concepts (the concept band): the product's core concepts, the first layer of the context map. Paged by cursor: a page with next has more.
/// </summary>
/// <param name="ApiVersion">The answer's version.</param>
/// <param name="Repo">The repo the concepts are of; null when the profile has several and none was asked for.</param>
/// <param name="State">ok, empty (no concepts yet), building or unavailable.</param>
/// <param name="MapVersion">The map version the concepts were read at.</param>
/// <param name="Concepts">The concepts of this page; absent unless the state is ok.</param>
/// <param name="Other">The concept of the unassigned nodes.</param>
/// <param name="Links">The weighted links between concepts.</param>
/// <param name="Next">The cursor of the next page; null on the last.</param>
/// <param name="Code">The error code when the state is unavailable.</param>
internal sealed record ContextConceptsView(
    int ApiVersion,
    string? Repo,
    string State,
    string? MapVersion = null,
    IReadOnlyList<ContextConcept>? Concepts = null,
    ConceptOther? Other = null,
    IReadOnlyList<ConceptLink>? Links = null,
    string? Next = null,
    string? Code = null);
