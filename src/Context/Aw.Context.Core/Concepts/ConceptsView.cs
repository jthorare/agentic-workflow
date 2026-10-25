using System.Text.Json.Serialization;

namespace Aw.Context.Core.Concepts;

/// <summary>'GET /sindri/context/concepts': the product's concepts, the top band of the map, one page at a time.</summary>
/// <param name="ApiVersion">1.</param>
/// <param name="Repo">The repo this answer is for, or null when the profile has several and the question named none.</param>
/// <param name="MapVersion">The map read ('v13'), or empty when none was.</param>
/// <param name="State">One of <see cref="ConceptStates"/>.</param>
/// <param name="Code">The error code that makes the state 'unavailable', or null; left out when null.</param>
/// <param name="Concepts">This page's concepts, by name.</param>
/// <param name="Other">The reserved concept for what no concept takes.</param>
/// <param name="Links">Links with an end among this page's concepts (a link between two pages arrives with both), by source, target.</param>
/// <param name="Next">The cursor of the next page, or null on the last; left out when null.</param>
public sealed record ConceptsView(
    int ApiVersion,
    string? Repo,
    string MapVersion,
    string State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code,
    IReadOnlyList<ConceptRow> Concepts,
    ConceptOther Other,
    IReadOnlyList<ConceptLink> Links,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Next);
