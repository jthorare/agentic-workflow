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

            var removed = await inputs.Repos.RemovedPaths(repo, read.Commit, cancellationToken).ConfigureAwait(false);
            if (removed.IsError)
            {
                return (ExtractedMap.Empty, [Unavailable(removed.Error)], []);
            }

            tracked.UnionWith(paths);
            files.Add(new MapRepoFiles(repo.Name, paths, blobs.Value) { Removed = removed.Value });
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
        var docs = DocsByPath(half);
        var nodes = half.Nodes.ToList();
        var edges = new Dictionary<(string From, string To, EdgeType Type), GraphEdge>();
        foreach (var edge in half.Edges)
        {
            var target = edge;
            if (edge.To.Kind == NodeKind.Code && !known.Contains(edge.To.Value) && tracked.Contains(edge.To.Name))
            {
                if (docs.TryGetValue(edge.To.Name, out var holders))
                {
                    // why: a path two repos both hold is the doc of the mentioning doc's own repo; with no such pick the edge is dropped, never linked to another repo's doc.
                    if (DocFor(holders, edge.From.Value) is not { } doc)
                    {
                        continue;
                    }

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

    // Each doc record's key by its path in its repo, in key order.
    private static Dictionary<string, List<string>> DocsByPath(ExtractedMap half)
    {
        var docs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var key in half.Nodes.Select(n => n.Key.Value).Where(k => k.StartsWith(DocPrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            if (key.IndexOf('/', DocPrefix.Length) is var slash and >= 0)
            {
                var path = key[(slash + 1)..];
                if (!docs.TryGetValue(path, out var same))
                {
                    docs[path] = same = [];
                }

                same.Add(key);
            }
        }

        return docs;
    }

    // The one doc a mention lands on: the only holder of the path, else the holder in the mentioning doc's own repo; null when neither picks exactly one.
    private static string? DocFor(List<string> holders, string from)
    {
        if (holders.Count == 1)
        {
            return holders[0];
        }

        var slash = from.StartsWith(DocPrefix, StringComparison.Ordinal) ? from.IndexOf('/', DocPrefix.Length) : -1;
        if (slash < 0)
        {
            return null;
        }

        var own = holders.Where(h => h.StartsWith(from[..(slash + 1)], StringComparison.Ordinal)).ToList();
        return own.Count == 1 ? own[0] : null;
    }

    private static string Unavailable(Error error) => $"records: unavailable ({error.Code})";
}
