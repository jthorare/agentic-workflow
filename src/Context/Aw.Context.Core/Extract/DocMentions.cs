using Aw.Context.Core.Engine;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Extract;

/// <summary>
/// The 'mentions' edges from doc records to the files their text cites, and what didn't resolve (#1373, spec §11.2). A
/// citation that names a file of the repo exactly is Deterministic at confidence 1.0, like an import edge: it claims only
/// that the doc names the file. One that names nothing is counted, never an edge.
/// </summary>
public static class DocMentions
{
    /// <summary>Resolves what the docs cite against the map's files.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="records">The doc record nodes (<see cref="DocRecords.Find"/>); text for a path with no record is ignored.</param>
    /// <param name="texts">Each doc's scrubbed text by path; null when the scrubber withheld it (the record stays, with no edges).</param>
    /// <param name="files">The repo's file paths.</param>
    /// <returns>The edges, sorted by source then target, and the unresolved counts.</returns>
    public static DocMentionScan Find(ContextRepoName repo, IReadOnlyList<GraphNode> records, IReadOnlyDictionary<string, string?> texts, IReadOnlyCollection<string> files)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(files);
        var known = files.ToHashSet(StringComparer.Ordinal);
        var recorded = records.Select(r => r.Key.Value).ToHashSet(StringComparer.Ordinal);
        var edges = new List<GraphEdge>();
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var docsMissing = 0;
        foreach (var (path, text) in texts.OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            if (text is null || DocRecords.Key(repo, path) is not { } from || !recorded.Contains(from.Value))
            {
                continue;
            }

            var cited = CitedPaths.Find(text);
            var resolved = cited.Where(known.Contains).Select(c => (Path: c, Key: MapExtractor.CodeKey(c))).Where(c => c.Key is not null).OrderBy(c => c.Path, StringComparer.Ordinal).ToList();
            var evidence = RepoDocs.TryKey(repo, path, out var docKey) ? new[] { new EvidenceRef(docKey) } : null;
            edges.AddRange(resolved.Select(c => new GraphEdge(from, c.Key!.Value, EdgeType.Mentions, Provenance.Deterministic, 1.0, evidence)));   // why: the filter above keeps valid keys
            var unresolved = cited.Where(c => !known.Contains(c)).ToList();
            docsMissing += unresolved.Count > 0 ? 1 : 0;
            missing.UnionWith(unresolved);
        }

        return new DocMentionScan([.. edges.OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal)], docsMissing, missing.Count);
    }
}
