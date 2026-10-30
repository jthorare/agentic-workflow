using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Surfaces;

/// <summary>Finds the action candidates of a build and groups the lookalikes (spec §6.9, apply step).</summary>
public static class SurfaceCandidates
{
    private const string ActionPass = "action";

    /// <summary>
    /// Groups the build's deterministic actions that look alike. Lookalikes share an evidence key or have equal names (trimmed, case-folded), joined transitively; each set is cut into groups of at most
    /// <see cref="SurfaceGroup.MaxCandidates"/>, and a loner is a group of one. An action a person already decided (an override on its key whose 'who' does not start with 'service:') is left out.
    /// </summary>
    /// <param name="found">The map this build found.</param>
    /// <param name="overrides">Every override of the map.</param>
    /// <returns>The groups, ordered by their first candidate's key.</returns>
    public static IReadOnlyList<SurfaceGroup> Groups(ExtractedMap found, IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(found);
        ArgumentNullException.ThrowIfNull(overrides);
        var decided = overrides.Where(static o => !o.Who.StartsWith("service:", StringComparison.Ordinal)).Select(static o => o.Target.Value).ToHashSet(StringComparer.Ordinal);
        var nodes = found.Nodes
            .Where(n => n.Key.Kind == NodeKind.Action && n.Provenance == Provenance.Deterministic && !decided.Contains(n.Key.Value))
            .DistinctBy(static n => n.Key.Value, StringComparer.Ordinal)
            .OrderBy(static n => n.Key.Value, StringComparer.Ordinal)
            .ToList();
        var edges = found.Edges.ToLookup(static e => e.From.Value, StringComparer.Ordinal);
        var sets = LookalikeSets(nodes);
        return [.. sets.SelectMany(set => SurfaceGroups.Cut(set.Select(i => Candidate(nodes[i], edges[nodes[i].Key.Value])))).OrderBy(static g => g.Candidates[0].NodeKey, StringComparer.Ordinal)];
    }

    private static SurfaceCandidate Candidate(GraphNode node, IEnumerable<GraphEdge> outgoing) => new(
        node.Key.Value,
        "action",
        node.Label,
        ActionPass,
        [.. node.EvidenceOrEmpty.Select(static e => e.Key.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
        [.. outgoing.Select(static e => $"{EdgeRules.Name(e.Type)} {e.To.Value}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);

    // Union-find over node indexes: two actions join when they share an evidence key or an equal folded name.
    private static List<List<int>> LookalikeSets(List<GraphNode> nodes)
    {
        var parent = Enumerable.Range(0, nodes.Count).ToArray();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < nodes.Count; i++)
        {
            var tokens = nodes[i].EvidenceOrEmpty.Select(static e => "e:" + e.Key.Value).Append("n:" + nodes[i].Label.Trim().ToLowerInvariant());
            foreach (var token in tokens)
            {
                if (seen.TryGetValue(token, out var other))
                {
                    Union(parent, i, other);
                }
                else
                {
                    seen[token] = i;
                }
            }
        }

        return [.. Enumerable.Range(0, nodes.Count).GroupBy(i => Find(parent, i)).Select(static g => g.ToList())];
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }

        return i;
    }

    private static void Union(int[] parent, int a, int b) => parent[Find(parent, a)] = Find(parent, b);
}
