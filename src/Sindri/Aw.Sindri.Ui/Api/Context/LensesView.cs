using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// GET /sindri/context/lenses: the three bands around one anchor (schema 1, anchor) or a set of them (schema 2,
/// anchors, missingAnchors and coverage.byAnchor). <see cref="Anchors"/> is filled in both cases.
/// </summary>
/// <param name="SchemaVersion">SchemaVersion, as served.</param>
/// <param name="MapVersion">The map version, exactly v and its number.</param>
/// <param name="Draft">Whether the draft map answered.</param>
/// <param name="Depth">Depth, as served.</param>
/// <param name="AsOf">AsOf, as served.</param>
/// <param name="Bands">Bands, as served.</param>
/// <param name="Edges">Edges across the bands, as served.</param>
/// <param name="Conflicts">Conflicts, as served.</param>
/// <param name="Coverage">Coverage, as served.</param>
/// <param name="Searched">Searched, as served.</param>
/// <param name="Degraded">Degraded, as served.</param>
/// <param name="Cites">The citations by marker.</param>
/// <param name="Anchor">The one anchor of a schema 1 answer.</param>
/// <param name="AnchorSet">The anchors of a schema 2 answer (wire name anchors).</param>
/// <param name="MissingAnchors">The anchors a set asked for that the map does not hold, in request order.</param>
/// <param name="MapCommit">The map's commit (7 hex), absent when the map has none.</param>
internal sealed record LensesView(
    int SchemaVersion,
    string MapVersion,
    bool Draft,
    int Depth,
    DateTimeOffset AsOf,
    LensBands Bands,
    IReadOnlyList<LensEdge> Edges,
    IReadOnlyList<LensConflict> Conflicts,
    CoverageView Coverage,
    SearchedView Searched,
    IReadOnlyList<JsonElement> Degraded,
    IReadOnlyDictionary<string, LensCitation>? Cites,
    LensAnchor? Anchor = null,
    [property: JsonPropertyName("anchors")] IReadOnlyList<LensAnchor>? AnchorSet = null,
    IReadOnlyList<string>? MissingAnchors = null,
    string? MapCommit = null)
{
    /// <summary>The anchors the answer is for: the set when served, else the one anchor.</summary>
    [JsonIgnore]
    public IReadOnlyList<LensAnchor> Anchors => AnchorSet ?? (Anchor is null ? [] : [Anchor]);
}
