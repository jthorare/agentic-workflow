using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Ranking;

/// <summary>What the map says about one fused record, for the ranker's features (spec §8.2 step 5): how it reaches the answer's nodes.</summary>
/// <param name="CascadeDistance">The fewest cascade steps from an anchor to a node the record is linked to (0: the anchor's own scope), or null when the record is not linked to the answer.</param>
/// <param name="Edge">The type of the link (a record that decides a node, one that only mentions it), or null.</param>
/// <param name="Provenance">Who made the link, or null.</param>
/// <param name="Superseded">True when a decision of the record has been replaced by a later one.</param>
/// <param name="Linked">When the link was written, or null.</param>
public sealed record RecordSignals(int? CascadeDistance, EdgeType? Edge, Provenance? Provenance, bool Superseded, DateTimeOffset? Linked)
{
    /// <summary>A record the map does not link to the answer.</summary>
    public static RecordSignals None { get; } = new(CascadeDistance: null, Edge: null, Provenance: null, Superseded: false, Linked: null);
}
