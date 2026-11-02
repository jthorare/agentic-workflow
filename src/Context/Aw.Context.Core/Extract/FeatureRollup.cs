using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Extract;

/// <summary>
/// Rolls the file-level edges up to the home features (spec 2026-10-27 §3): F depends-on G when a file homed in F depends on a file homed in G, F reads or writes a thing when a file homed in F does. Each edge is deterministic, confidence 1.0,
/// carries a <see cref="GraphEdge.Count"/> (the distinct file pairs for 'depends-on', the distinct files for 'reads' and 'writes') and cites the first <see cref="EvidencePairs"/> of them by path. A 'depends-on' pair is one
/// <see cref="EvidenceRef"/>: the importing file as its key and the imported file's record key as its span. Only features that hold files get edges, so a group feature gets none.
/// </summary>
public static class FeatureRollup
{
    /// <summary>How many pairs (or files) a roll-up edge cites as evidence.</summary>
    public const int EvidencePairs = 5;

    /// <summary>Adds the feature-level 'depends-on', 'reads' and 'writes' edges the file-level ones imply.</summary>
    /// <param name="map">The joined extraction: home 'implements' edges, code-to-code 'depends-on' and code 'reads'/'writes' edges.</param>
    /// <returns>The map plus the roll-up edges (the same edges again when it already holds them).</returns>
    public static ExtractedMap Apply(ExtractedMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var homes = Homes(map);
        var groups = new Dictionary<(string From, string To, EdgeType Type), SortedDictionary<(string Source, string Target), (RecordKey? SourceRecord, RecordKey? TargetRecord)>>();
        foreach (var edge in map.Edges.Where(e => e.Provenance == Provenance.Deterministic && e.From.Kind == NodeKind.Code && homes.ContainsKey(e.From.Value)))
        {
            var (fromFeature, fromRecord) = homes[edge.From.Value];
            var (to, targetRecord) = edge switch
            {
                { Type: EdgeType.DependsOn, To.Kind: NodeKind.Code } when homes.TryGetValue(edge.To.Value, out var other) && other.Feature != fromFeature => (other.Feature, other.Record),
                { Type: EdgeType.Reads or EdgeType.Writes, To.Kind: NodeKind.Thing } => (edge.To.Value, null),
                _ => (null, null),
            };
            if (to is null)
            {
                continue;
            }

            var key = (fromFeature, to, edge.Type);
            if (!groups.TryGetValue(key, out var pairs))
            {
                groups[key] = pairs = new SortedDictionary<(string, string), (RecordKey?, RecordKey?)>(PairOrder.Instance);
            }

            pairs[(edge.From.Value, edge.Type == EdgeType.DependsOn ? edge.To.Value : string.Empty)] = (fromRecord, targetRecord);
        }

        var rolled = groups.Select(g => Edge(g.Key, g.Value)).ToList();
        return map.Plus(new ExtractedMap([], rolled));
    }

    private static GraphEdge Edge((string From, string To, EdgeType Type) key, SortedDictionary<(string Source, string Target), (RecordKey? SourceRecord, RecordKey? TargetRecord)> pairs)
    {
        var evidence = pairs.Values.Take(EvidencePairs)
            .Select(p => p.SourceRecord is { } source ? new EvidenceRef(source, p.TargetRecord?.Value) : null).OfType<EvidenceRef>().ToList();
        return new GraphEdge(NodeKey.From(key.From), NodeKey.From(key.To), key.Type, Provenance.Deterministic, 1.0, evidence, Count: pairs.Count);
    }

    // The home feature of each file and the record key of the file, from the deterministic 'implements' edges to a feature (a surface's claim is not a home).
    private static Dictionary<string, (string Feature, RecordKey? Record)> Homes(ExtractedMap map)
    {
        var homes = new Dictionary<string, (string, RecordKey?)>(StringComparer.Ordinal);
        foreach (var edge in map.Edges.Where(e => e is { Type: EdgeType.Implements, Provenance: Provenance.Deterministic, From.Kind: NodeKind.Code, To.Kind: NodeKind.Feature }))
        {
            homes[edge.From.Value] = (edge.To.Value, edge.EvidenceOrEmpty.Count > 0 ? edge.EvidenceOrEmpty[0].Key : null);
        }

        return homes;
    }

    private sealed class PairOrder : IComparer<(string Source, string Target)>
    {
        public static PairOrder Instance { get; } = new();

        public int Compare((string Source, string Target) x, (string Source, string Target) y) =>
            StringComparer.Ordinal.Compare(x.Source, y.Source) is var c and not 0 ? c : StringComparer.Ordinal.Compare(x.Target, y.Target);
    }
}
