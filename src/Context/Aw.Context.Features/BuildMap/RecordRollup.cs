using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.BuildMap;

/// <summary>The record-to-feature edges (spec 2026-10-27 §7.5, §11.2): one 'mentions' edge per home feature of a record's files, counting the files in it, one from a directory doc to the home feature at its directory, and one from a tracker project to each feature its in-project issues reach.</summary>
internal static class RecordRollup
{
    private const int EvidenceCap = 3;

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
            // why: a path with no home feature (a script no recognizer reads) has no node to roll up from.
            var perFeature = Features(record, homes);
            if (perFeature.Count > 0)
            {
                nodes.Add(record.Node);
            }

            foreach (var g in perFeature)
            {
                edges[(record.Node.Key.Value, g.Key)] = new GraphEdge(record.Node.Key, g.First(), EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(record.Evidence)], Count: g.Count());
            }
        }

        foreach (var edge in ProjectEdges(records, homes))
        {
            edges[(edge.From.Value, edge.To.Value)] = edge;
        }

        foreach (var doc in directoryDocs)
        {
            _ = edges.TryAdd((doc.Doc.Value, doc.Feature.Value), new GraphEdge(doc.Doc, doc.Feature, EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(doc.Evidence)]));
        }

        return new ExtractedMap(nodes, [.. edges.Values]);
    }

    // The features a record's files are homed in, one group per feature in ordinal order.
    private static List<IGrouping<string, NodeKey>> Features(RolledRecord record, Dictionary<string, NodeKey> homes) =>
        [.. record.Paths.Where(p => homes.ContainsKey("code:" + p)).Select(p => homes["code:" + p]).GroupBy(f => f.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)];

    // A project mentions the features of its in-project issues: the count is how many issues reach the feature, the evidence the first few of them. The project node is already in the map, put there by the issue's link to it.
    private static IEnumerable<GraphEdge> ProjectEdges(IReadOnlyList<RolledRecord> records, Dictionary<string, NodeKey> homes) =>
        records.Where(r => r.Project.HasValue)
            .SelectMany(r => Features(r, homes).Select(g => (Project: r.Project.GetValueOrDefault(), Feature: g.First(), r.Evidence)))
            .GroupBy(x => (Project: x.Project.Value, Feature: x.Feature.Value), x => x)
            .OrderBy(g => g.Key.Project, StringComparer.Ordinal).ThenBy(g => g.Key.Feature, StringComparer.Ordinal)
            .Select(g => new GraphEdge(g.First().Project, g.First().Feature, EdgeType.Mentions, Provenance.Deterministic, 1.0,
                [.. g.Select(x => x.Evidence).Distinct().OrderBy(k => k.Value, StringComparer.Ordinal).Take(EvidenceCap).Select(k => new EvidenceRef(k))], Count: g.Select(x => x.Evidence).Distinct().Count()));

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
