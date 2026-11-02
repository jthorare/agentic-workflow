using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Subgraph;

/// <summary>
/// The nodes a caller names, from one map version, with the nodes around them to a depth (scoping spec C14). Every band item carries its 'depth' (0 = given).
/// 'edges' holds the edges across the bands (mentions, decides, quotes, supersedes, implements, tests) among the returned nodes; 'bands.product.edges' holds the structure
/// edges among the product items, so no edge is in both and a full drawing uses both lists.
/// </summary>
/// <param name="SchemaVersion">1.</param>
/// <param name="Nodes">The given keys the version holds and the caller may see, in request order.</param>
/// <param name="Missing">The other given keys (absent, out of scope or not visible: one answer for every reason), in request order.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Depth">How far the answer widened, 0 to 4.</param>
/// <param name="AsOf">When the view was built; the map version's own time is 'coverage.map.created'.</param>
/// <param name="Bands">The three bands over the returned nodes.</param>
/// <param name="Edges">Edges across the bands, both ends in the returned set.</param>
/// <param name="Conflicts">Typed disagreements on returned nodes.</param>
/// <param name="Coverage">Counts over the given product nodes.</param>
/// <param name="Searched">What was searched.</param>
/// <param name="Degraded">Sources that couldn't be read.</param>
/// <param name="Cites">The citations by marker.</param>
public sealed record SubgraphView(
    int SchemaVersion,
    IReadOnlyList<NodeKey> Nodes,
    IReadOnlyList<string> Missing,
    string MapVersion,
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MapCommit { get; init; }

    /// <summary>How many demoted files' code nodes depth expansion left out (<see cref="DemotedCode"/>); null when none was, so the field is absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Demoted { get; init; }
}
