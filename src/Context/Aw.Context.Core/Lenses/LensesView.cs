using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>'context lenses &lt;anchor&gt;' (spec §8.9): intent, product and code bands around one node, with the edges across them, their conflicts and coverage.</summary>
/// <param name="SchemaVersion">1.</param>
/// <param name="Anchor">The anchor node.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="Depth">How far the neighbors reach and the cascade ran, 0 to 4.</param>
/// <param name="AsOf">When the view was built.</param>
/// <param name="Bands">The three bands.</param>
/// <param name="Edges">Edges across the bands.</param>
/// <param name="Conflicts">Typed disagreements, one per decided node.</param>
/// <param name="Coverage">Counts over the anchor's own scope.</param>
/// <param name="Searched">What was searched: the anchors, the map version, each source's state, which bands are empty (scoping spec C6).</param>
/// <param name="Degraded">Sources that couldn't be read (none in S-4: the view reads stored edges only).</param>
/// <param name="Cites">The citations by marker (empty in S-4: record citations come with the source stages).</param>
public sealed record LensesView(
    int SchemaVersion,
    LensAnchor Anchor,
    string MapVersion,
    bool Draft,
    int Depth,
    DateTimeOffset AsOf,
    LensBands Bands,
    IReadOnlyList<LensEdge> Edges,
    IReadOnlyList<LensConflict> Conflicts,
    LensCoverage Coverage,
    LensSearched Searched,
    IReadOnlyList<string> Degraded,
    IReadOnlyDictionary<string, Citation> Cites)
{
    /// <summary>The commit the map was built at (first seven characters), or null for an imported map; 'mapVersion' itself stays 'v&lt;N&gt;'.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? MapCommit { get; init; }

    /// <summary>The page this answer is, when the caller sent a limit; left out for the whole answer.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LensWindow? Page { get; init; }

    /// <summary>The code units the component hop skipped for the fan-out cap; left out when none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SkippedComponent>? SkippedComponents { get; init; }

    /// <summary>How many demoted files the answer carries, per role ('index.demote'); left out when none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FilteredCounts? Filtered { get; init; }
}
