using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>The file-level links of a graph ('code:&lt;path&gt;' nodes with 'implements' and 'tests' edges), by file path, with the intent on features.</summary>
internal sealed class FileLinks
{
    private const string CodePrefix = "code:";
    private readonly Dictionary<string, List<GraphEdge>> _byFile;
    private readonly Dictionary<string, HashSet<string>> _records = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _decisions = new(StringComparer.Ordinal);

    // One index per applied graph: a graph is immutable and the process keeps the same instance per map version, so a directory listing never re-walks every edge.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GraphSnapshot, FileLinks> _all = [];

    /// <param name="graph">The graph.</param>
    /// <param name="repo">The repo whose links are read, or null for every link: an edge whose file evidence ('repo:&lt;repo&gt;/&lt;path&gt;') names only other repos is left out; one with no repo evidence counts for any repo.</param>
    internal FileLinks(GraphSnapshot graph, Engine.ContextRepoName? repo = null)
    {
        _byFile = graph.Edges.Where(e => e.Type is EdgeType.Implements or EdgeType.Tests or EdgeType.Tags && BelongsTo(e, repo) && e.From.Value.StartsWith(CodePrefix, StringComparison.Ordinal) && !e.From.Value.Contains('#', StringComparison.Ordinal))
            .GroupBy(e => e.From.Value[CodePrefix.Length..], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e.Type is EdgeType.Mentions or EdgeType.Decides))
        {
            var index = edge.Type == EdgeType.Mentions ? _records : _decisions;
            if (!index.TryGetValue(edge.To.Value, out var from))
            {
                index[edge.To.Value] = from = new HashSet<string>(StringComparer.Ordinal);
            }

            _ = from.Add(edge.From.Value);
        }
    }

    /// <summary>The index of every link in a graph, built once per graph instance.</summary>
    /// <param name="graph">The graph.</param>
    internal static FileLinks For(GraphSnapshot graph) => _all.GetValue(graph, static g => new FileLinks(g));

    private static bool BelongsTo(GraphEdge edge, Engine.ContextRepoName? repo)
    {
        if (repo is not { } wanted)
        {
            return true;
        }

        var files = edge.EvidenceOrEmpty.Select(r => r.Key.Value).Where(k => k.StartsWith("repo:", StringComparison.Ordinal)).ToList();
        return files.Count == 0 || files.Exists(k => k.StartsWith($"repo:{wanted.Value}/", StringComparison.Ordinal));
    }

    /// <summary>Mapped (a confirmed 'implements' edge), suggested (only unconfirmed ones) or unmapped.</summary>
    internal FileMapStatus StatusOf(string file)
    {
        if (!_byFile.TryGetValue(file, out var edges))
        {
            return FileMapStatus.Unmapped;
        }

        var status = FileMapStatus.Unmapped;
        foreach (var edge in edges)
        {
            if (edge.Type is not (EdgeType.Implements or EdgeType.Tags))
            {
                continue;
            }

            if (edge.Type == EdgeType.Implements && Confirmation.Of(edge.Provenance))
            {
                return FileMapStatus.Mapped;
            }

            status = FileMapStatus.Suggested;
        }

        return status;
    }

    /// <summary>A file's links as features, its home first and its tags after it by key: a suggestion keeps its link id.</summary>
    internal IEnumerable<FileFeature> FeaturesOf(string file, bool confirmedOnly) =>
        (_byFile.GetValueOrDefault(file) ?? []).Where(e => !confirmedOnly || Confirmation.Of(e.Provenance))
            .OrderBy(e => e.Type == EdgeType.Tags ? 1 : 0).ThenBy(e => e.Type == EdgeType.Tags ? e.To.Value : string.Empty, StringComparer.Ordinal)
            .Select(e => new FileFeature(e.To, e.Provenance, e.Provenance == Provenance.Model ? e.Confidence : null, Confirmation.Of(e.Provenance),
                Confirmation.Of(e.Provenance) ? null : LinkIds.For(e), e.Type));

    /// <summary>The unconfirmed 'implements' and 'tags' links of a file, to accept or reject.</summary>
    internal IEnumerable<FileMapSuggestion> Suggestions(string file) =>
        (_byFile.GetValueOrDefault(file) ?? []).Where(e => e.Type is EdgeType.Implements or EdgeType.Tags && !Confirmation.Of(e.Provenance))
            .OrderBy(e => e.To.Value, StringComparer.Ordinal).Select(e => new FileMapSuggestion(LinkIds.For(e), file, e.To, e.Confidence, [.. e.EvidenceOrEmpty.Select(r => r.Key.Value)]));

    /// <summary>The distinct records and decisions with an edge onto any of the features.</summary>
    internal FileIntent IntentOn(IEnumerable<NodeKey> features)
    {
        var keys = features.Select(k => k.Value).ToList();
        return new FileIntent(Count(_records, keys), Count(_decisions, keys));
    }

    private static int Count(Dictionary<string, HashSet<string>> index, List<string> features) =>
        features.SelectMany(f => index.GetValueOrDefault(f) ?? []).Distinct(StringComparer.Ordinal).Count();
}
