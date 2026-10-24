using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Kernel;

namespace Aw.Context.Core.History;

/// <summary>The pure half of the history lens (S-H): the anchors' own scopes, the stored intent and implementing files they reach, and the objects the answer repeats from the lenses (edges, coverage, searched).</summary>
public static class HistoryLens
{
    private const string CodePrefix = "code:";
    private const string RepoPrefix = "repo:";

    /// <summary>Resolves the anchors in the snapshot and reads each one's own scope (no cascade).</summary>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="anchors">Node keys, aliases or labels.</param>
    /// <returns>The scope; an anchor that names no node goes to 'MissingAnchors'. None resolving is SND-CONTEXT-003, an ambiguous name 037, no anchor at all SND-CONTEXT-006.</returns>
    public static Result<HistoryScope> Resolve(GraphSnapshot graph, IReadOnlyList<string> anchors)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(anchors);
        var distinct = anchors.Distinct(StringComparer.Ordinal).ToList();
        if (QueryRules.CheckAnchors(distinct) is { } refused)
        {
            return refused;
        }

        var resolved = AnchorSet.Resolve(graph, distinct, depth: 0);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var (scopes, missing) = resolved.Value;
        var (records, decisions) = LensSets.IntentUnion(scopes);
        return new HistoryScope(graph, scopes, missing, records, decisions, Files(scopes));
    }

    /// <summary>The edges across the found records and decisions and the own scope (mentions, decides, quotes, supersedes), as the lenses return them.</summary>
    /// <param name="scope">The resolved anchors.</param>
    /// <param name="found">The keys of the records and decisions that survived the cut.</param>
    /// <returns>The edges.</returns>
    public static IReadOnlyList<LensEdge> Edges(HistoryScope scope, IEnumerable<NodeKey> found)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(found);
        var items = new HashSet<string>(scope.Own, StringComparer.Ordinal);
        items.UnionWith(found.Select(k => k.Value));
        return LensEdges.Across(scope.Graph, items);
    }

    /// <summary>The coverage of the union of the anchors' own scopes, with each anchor's and the map's.</summary>
    /// <param name="scope">The resolved anchors.</param>
    /// <param name="basis">What the answer rests on.</param>
    /// <returns>The coverage.</returns>
    public static LensCoverage Coverage(HistoryScope scope, LensBasis basis)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(basis);
        var byAnchor = scope.Scopes.ToDictionary(s => s.Anchor.Key.Value, LensCoverageBuilder.Of, StringComparer.Ordinal);
        return LensCoverageBuilder.Of(scope.Scopes[0].Graph, scope.Scopes[0].Nodes, [.. scope.Own.Select(NodeKey.From)]) with { Map = MapCoverageBuilder.Of(scope.Graph, scope.Own, basis), ByAnchor = byAnchor };
    }

    /// <summary>What a history answer searched: the map, the intent sources, the history sources it read, and which bands came back empty.</summary>
    /// <param name="scope">The resolved anchors.</param>
    /// <param name="basis">What the answer rests on.</param>
    /// <param name="intentSources">The intent sources' rows.</param>
    /// <param name="history">The history sources' rows ('git-history', 'incidents').</param>
    /// <param name="found">How many records and decisions the answer lists, and how many files were searched.</param>
    /// <returns>The searched object.</returns>
    public static LensSearched Searched(HistoryScope scope, LensBasis basis, IReadOnlyList<SearchedSource> intentSources, IReadOnlyList<SearchedSource> history, (int Intent, int Files) found)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(basis);
        var bands = new SearchedBands(new BandSearched(found.Intent == 0), new BandSearched(scope.Own.Count == 0), new BandSearched(found.Files == 0));
        return LensSearchedBuilder.ForHistory(basis, scope.AnchorKeys, intentSources, history, bands);
    }

    private static List<HistoryFile> Files(List<LensScope> scopes)
    {
        var reached = new Dictionary<string, (GraphEdge Best, SortedSet<int> By, bool Confirmed)>(StringComparer.Ordinal);
        for (var i = 0; i < scopes.Count; i++)
        {
            foreach (var edge in scopes[i].IntoScope(EdgeType.Implements).Where(e => IsFile(e.From.Value) && scopes[i].Nodes.ContainsKey(e.From.Value)))
            {
                var confirmed = LensScope.Confirmed(edge.Provenance);
                if (!reached.TryGetValue(edge.From.Value, out var entry))
                {
                    entry = (edge, [], confirmed);
                }
                else if (Better(edge, confirmed, entry.Best, entry.Confirmed))
                {
                    entry = (edge, entry.By, confirmed);
                }

                _ = entry.By.Add(i);
                reached[edge.From.Value] = entry;
            }
        }

        return [.. reached.Values
            .OrderByDescending(e => e.Confirmed).ThenByDescending(e => e.Best.Confidence).ThenBy(e => e.Best.From.Value, StringComparer.Ordinal)
            .Select(e => new HistoryFile(RepoOf(e.Best), e.Best.From.Name, [.. e.By.Select(i => scopes[i].Anchor.Key.Value)]))];
    }

    private static bool Better(GraphEdge edge, bool confirmed, GraphEdge best, bool bestConfirmed) =>
        confirmed != bestConfirmed ? confirmed : edge.Confidence > best.Confidence;

    private static bool IsFile(string key) => key.StartsWith(CodePrefix, StringComparison.Ordinal) && !key.Contains('#', StringComparison.Ordinal);

    // The edge's file evidence names the repo: 'repo:<name>/<path>' with the file's own path.
    private static ContextRepoName? RepoOf(GraphEdge edge)
    {
        var suffix = "/" + edge.From.Name;
        foreach (var evidence in edge.EvidenceOrEmpty)
        {
            var key = evidence.Key.Value;
            if (key.StartsWith(RepoPrefix, StringComparison.Ordinal) && key.EndsWith(suffix, StringComparison.Ordinal)
                && ContextRepoName.TryFrom(key[RepoPrefix.Length..^suffix.Length], out var name))
            {
                return name;
            }
        }

        return null;
    }
}
