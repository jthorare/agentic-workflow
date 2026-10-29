using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The record half of the build: the store read once, its nodes and edges joined to the extraction.</summary>
internal sealed partial class BuildMapService
{
    private const string DocPrefix = "record:repo:";

    // The record store read once for the whole build (spec 2026-10-27 §7.1). A store or a checkout that can't be read is the one degraded line 'records: unavailable (<code>)' and no record nodes: the build is about the code, and the next build reads them again.
    private async ValueTask<(ExtractedMap Map, IReadOnlyList<string> Degraded, IReadOnlyList<string> Notes)> Records(ContextSettings settings, List<(ContextRepo Repo, RepoExtraction Read)> extractions, ExtractedMap found, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow();
        var files = new List<MapRepoFiles>(extractions.Count);
        var tracked = new HashSet<string>(StringComparer.Ordinal);
        var directoryDocs = new List<DirectoryDoc>();
        foreach (var (repo, read) in extractions)
        {
            var paths = read.Paths.Where(p => !repo.Denies(p)).ToList();
            var nodes = DocRecords.Find(repo.Name, paths, settings.DocGlobs, settings.DocDirectoryNames, at);
            var docs = nodes.Select(n => n.Label).ToList();
            directoryDocs.AddRange(RecordRollup.DirectoryDocs(repo.Name, nodes, settings.DocDirectoryNames, read.Directories));
            var blobs = await inputs.Repos.BlobIds(repo, read.Commit, docs, cancellationToken).ConfigureAwait(false);
            if (blobs.IsError)
            {
                return (ExtractedMap.Empty, [Unavailable(blobs.Error)], []);
            }

            tracked.UnionWith(paths);
            files.Add(new MapRepoFiles(repo.Name, paths, blobs.Value));
        }

        var reading = await inputs.Records.Read(new MapRecordInput(files, settings.DocGlobs, settings.DocDirectoryNames, at), cancellationToken).ConfigureAwait(false);
        if (reading.IsError)
        {
            return (ExtractedMap.Empty, [Unavailable(reading.Error)], []);
        }

        var half = reading.Value.Map.Plus(RecordRollup.Edges([.. reading.Value.Overflow, .. reading.Value.UnderCap], directoryDocs, found));
        var (withFiles, edges) = WithFileNodes(half, found, tracked);
        // why: a record can mention a path that is neither a node nor in the tree; an edge to a node that isn't in the graph fails the save's foreign key.
        var nodeKeys = found.Nodes.Concat(withFiles).Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var kept = new ExtractedMap(withFiles, [.. edges.Where(e => nodeKeys.Contains(e.From.Value) && nodeKeys.Contains(e.To.Value))]);
        return (kept, reading.Value.Degraded, [.. reading.Value.Notes.Except(reading.Value.Degraded, StringComparer.Ordinal)]);
    }

    // A record's mention of a tracked file the extractor didn't read (setup.sh, a script) gets a code: node marked read: false that implements nothing; a mention of a path that is itself a doc record points at that record instead.
    private static (List<GraphNode> Nodes, List<GraphEdge> Edges) WithFileNodes(ExtractedMap half, ExtractedMap found, HashSet<string> tracked)
    {
        var known = found.Nodes.Concat(half.Nodes).Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var docs = half.Nodes.Where(n => n.Key.Value.StartsWith(DocPrefix, StringComparison.Ordinal)).Select(n => n.Key.Value).Order(StringComparer.Ordinal).ToList();
        var nodes = half.Nodes.ToList();
        var edges = new Dictionary<(string From, string To, EdgeType Type), GraphEdge>();
        foreach (var edge in half.Edges)
        {
            var target = edge;
            if (edge.To.Kind == NodeKind.Code && !known.Contains(edge.To.Value) && tracked.Contains(edge.To.Name))
            {
                var doc = docs.Find(d => d.AsSpan(DocPrefix.Length).IndexOf('/') is var slash and >= 0 && string.Equals(d[(DocPrefix.Length + slash + 1)..], edge.To.Name, StringComparison.Ordinal));
                if (doc is not null)
                {
                    target = edge with { To = NodeKey.From(doc) };
                }
                else
                {
                    nodes.Add(new GraphNode(edge.To, edge.To.Name[(edge.To.Name.LastIndexOf('/') + 1)..], Provenance.Deterministic, Attributes: UnreadFileNodes.Attributes()));
                    _ = known.Add(edge.To.Value);
                }
            }

            var id = (target.From.Value, target.To.Value, target.Type);
            edges[id] = edges.TryGetValue(id, out var first) ? first with { Evidence = [.. first.EvidenceOrEmpty.Concat(target.EvidenceOrEmpty).Distinct()] } : target;
        }

        return (nodes, [.. edges.Values]);
    }

    private static string Unavailable(Error error) => $"records: unavailable ({error.Code})";
}
