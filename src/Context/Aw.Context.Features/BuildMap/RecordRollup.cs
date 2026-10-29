using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.BuildMap;

/// <summary>The record-to-feature edges (spec 2026-10-27 §7.5, §11.2): one 'mentions' edge per home feature of a record's files, counting the files in it, and one from a directory doc to the home feature at its directory.</summary>
internal static class RecordRollup
{
    /// <summary>Rolls each record's files up to the features that are their homes, and links each directory doc to its directory's feature.</summary>
    /// <param name="records">The records that name files, over the cap or not.</param>
    /// <param name="directoryDocs">The directory docs with the feature at their directory.</param>
    /// <param name="map">The joined extraction, home 'implements' edges included.</param>
    /// <returns>The nodes and edges to add; a record none of whose files has a home adds nothing. A doc whose files roll up to its directory's feature gets that one edge, with the file count.</returns>
    internal static ExtractedMap Edges(IReadOnlyList<RolledRecord> records, IReadOnlyList<DirectoryDoc> directoryDocs, ExtractedMap map)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(directoryDocs);
        ArgumentNullException.ThrowIfNull(map);
        var homes = new Dictionary<string, NodeKey>(StringComparer.Ordinal);
        foreach (var edge in map.Edges.Where(e => e is { Type: EdgeType.Implements, Provenance: Provenance.Deterministic, From.Kind: NodeKind.Code, To.Kind: NodeKind.Feature }))
        {
            homes[edge.From.Value] = edge.To;
        }

        var nodes = new List<GraphNode>();
        var edges = new Dictionary<(string From, string To), GraphEdge>();
        foreach (var record in records)
        {
            var perFeature = record.Paths.Select(p => homes.GetValueOrDefault("code:" + p)).OfType<NodeKey>()
                .GroupBy(f => f.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
            if (perFeature.Count > 0)
            {
                nodes.Add(record.Node);
            }

            foreach (var g in perFeature)
            {
                edges[(record.Node.Key.Value, g.Key)] = new GraphEdge(record.Node.Key, g.First(), EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(record.Evidence)], Count: g.Count());
            }
        }

        foreach (var doc in directoryDocs)
        {
            _ = edges.TryAdd((doc.Doc.Value, doc.Feature.Value), new GraphEdge(doc.Doc, doc.Feature, EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(doc.Evidence)]));
        }

        return new ExtractedMap(nodes, [.. edges.Values]);
    }

    /// <summary>Finds the home feature at the directory of each directory doc: the feature homed at exactly that directory, else the deepest one whose directory contains it. A doc at the repo root only takes a feature homed at the root.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="docs">The repo's doc nodes (label = path).</param>
    /// <param name="directoryNames">'context.docs.directoryNames'.</param>
    /// <param name="directories">The feature homed at each directory of the repo.</param>
    /// <returns>The docs that have a feature, in path order.</returns>
    internal static IEnumerable<DirectoryDoc> DirectoryDocs(ContextRepoName repo, IReadOnlyList<GraphNode> docs, IReadOnlyList<string> directoryNames, IReadOnlyDictionary<string, NodeKey> directories)
    {
        foreach (var doc in docs.Where(d => directoryNames.Contains(d.Label[(d.Label.LastIndexOf('/') + 1)..], StringComparer.Ordinal)))
        {
            var dir = doc.Label.Contains('/', StringComparison.Ordinal) ? doc.Label[..doc.Label.LastIndexOf('/')] : "";
            while (!directories.ContainsKey(dir) && dir.Length > 0)
            {
                dir = dir.Contains('/', StringComparison.Ordinal) ? dir[..dir.LastIndexOf('/')] : "";
            }

            if (directories.TryGetValue(dir, out var feature) && RepoDocs.TryKey(repo, doc.Label, out var key))
            {
                yield return new DirectoryDoc(doc.Key, key, feature);
            }
        }
    }
}
