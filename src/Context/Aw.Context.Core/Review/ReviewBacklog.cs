using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Review;

/// <summary>Builds what is left to decide in a draft (spec §8.7, S-4): deterministic proposals, model proposals and suggested file links by impact, then orphaned overrides.</summary>
public static class ReviewBacklog
{
    private static readonly NodeKind[] _proposable = [NodeKind.Feature, NodeKind.Surface, NodeKind.Action, NodeKind.Thing, NodeKind.Flow];

    /// <summary>Builds the queue.</summary>
    /// <param name="label">The draft read ('v14@7c3e1a2').</param>
    /// <param name="draft">The draft as built (before overrides).</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="mapRepo">The repo the draft was built for, or null; the repo of a proposed node that names none (<see cref="NodeRepos"/>).</param>
    /// <returns>The undecided entries and how many proposals already have a decision.</returns>
    public static ReviewQueueView Build(string label, GraphSnapshot draft, IReadOnlyList<MapOverride> overrides, string? mapRepo = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(overrides);
        var applied = OverrideApplier.Apply(draft, overrides);
        var graph = applied.Graph;
        var ends = EdgeEnds.Of(graph);
        var nodes = graph.Nodes.Where(IsProposal).Select(n => NodeEntry(ends, n));
        var links = graph.Edges.Where(IsSuggestion).Select(e => LinkEntry(ends, e));
        var undecided = nodes.Concat(links).ToList();
        var repoOf = NodeRepos.Index(graph, mapRepo);
        var ordered = undecided.Where(e => e.Group == ReviewGroup.Deterministic).OrderBy(e => e.Node.Value, StringComparer.Ordinal)
            .Concat(undecided.Where(e => e.Group == ReviewGroup.Model).OrderByDescending(e => e.Impact).ThenBy(e => e.Node.Value, StringComparer.Ordinal).ThenBy(e => e.Link?.Value, StringComparer.Ordinal))
            .Concat(applied.Orphaned.Where(o => o.Action != OverrideAction.Propose).Select(Orphan))
            .Select(e => e.Group == ReviewGroup.Orphaned ? e : e with { Repo = repoOf.GetValueOrDefault(e.Node.Value) })
            .ToList();
        var counts = QueueCounts.Of(ordered);
        var decided = DecidedProposals(draft, overrides, ordered);
        return new ReviewQueueView(label, ReviewVersion.None, ordered, decided, decided + counts.Deterministic + counts.Model, counts, ByKind(ordered), ReviewTruncated.None);
    }

    // A home or group feature is deterministic structure the build derives from the directories, not something to review (spec 2026-10-27 §2.2).
    private static bool IsProposal(GraphNode n) => n.Provenance != Provenance.Human && _proposable.Contains(n.Key.Kind) && n is not { Provenance: Provenance.Deterministic, Key.Kind: NodeKind.Feature };

    private static bool IsSuggestion(GraphEdge e) => e.Provenance == Provenance.Model && e.Type is EdgeType.Implements or EdgeType.Tests or EdgeType.Tags;

    // The proposals of this draft with a standing decision: the draft's own proposals and its scoping run's, not every target someone ever decided about.
    // One that is still in the queue is undecided whatever a row says: a reject whose cited span changed no longer applies, and the node is back.
    private static int DecidedProposals(GraphSnapshot draft, IReadOnlyList<MapOverride> overrides, List<ReviewEntry> queued)
    {
        var active = OverrideLog.Active(overrides);
        var proposals = draft.Nodes.Where(IsProposal).Select(n => n.Key.Value)
            .Concat(draft.Edges.Where(IsSuggestion).Select(e => LinkIds.For(e).Value))
            .Concat(active.Where(o => o.Action == OverrideAction.Propose).Select(o => o.Target.Value)).ToHashSet(StringComparer.Ordinal);
        var deciding = active.Where(OverrideLog.Decides).Select(o => o.Link?.Value ?? o.Target.Value).ToHashSet(StringComparer.Ordinal);
        var waiting = queued.Where(e => e.Group != ReviewGroup.Orphaned).Select(e => e.Link?.Value ?? e.Node.Value).ToHashSet(StringComparer.Ordinal);
        return proposals.Count(p => deciding.Contains(p) && !waiting.Contains(p));
    }

    private static Dictionary<string, int> ByKind(List<ReviewEntry> entries) =>
        entries.Where(e => e is { Group: ReviewGroup.Deterministic, Link: null }).GroupBy(e => e.Node.Value[..e.Node.Value.IndexOf(':', StringComparison.Ordinal)], StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    private static ReviewEntry NodeEntry(EdgeEnds ends, GraphNode node)
    {
        var key = node.Key.Value;
        var files = (ends.Implementers.GetValueOrDefault(key) ?? []).OrderBy(k => k.Value, StringComparer.Ordinal).ToList();
        var parent = ends.Parents.TryGetValue(key, out var up) ? up : (NodeKey?)null;
        var impact = ends.Touching.GetValueOrDefault(key);
        var group = node.Provenance == Provenance.Deterministic ? ReviewGroup.Deterministic : ReviewGroup.Model;
        return new ReviewEntry(node.Key, Link: null, group, node.Label, node.Provenance, node.Confidence, impact, files, parent, Trust: TrustPolicy.For(node.Provenance, accepted: false));
    }

    // Each node's edges, read in one pass (per node, a scan of every edge made one queue build quadratic): the files that implement it,
    // its first part-of parent, how many edges touch it (a self-loop once), and each node's label by key.
    private sealed record EdgeEnds(Dictionary<string, List<NodeKey>> Implementers, Dictionary<string, NodeKey> Parents, Dictionary<string, int> Touching, Dictionary<string, string> Labels, Dictionary<string, string> Aliases)
    {
        // The label of the node a key resolves to, as GraphSnapshot.Resolve finds it (an alias names its target; the first node with a key wins).
        internal string? Label(NodeKey key) => Labels.GetValueOrDefault(Aliases.GetValueOrDefault(key.Value) ?? key.Value);

        internal static EdgeEnds Of(GraphSnapshot graph)
        {
            var ends = new EdgeEnds(new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal),
                graph.Nodes.DistinctBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(n => n.Key.Value, n => n.Label, StringComparer.Ordinal),
                graph.Aliases.DistinctBy(a => a.Alias.Value, StringComparer.Ordinal).ToDictionary(a => a.Alias.Value, a => a.Target.Value, StringComparer.Ordinal));
            foreach (var e in graph.Edges)
            {
                if (e.Type is EdgeType.Implements or EdgeType.Tags)
                {
                    _ = ends.Implementers.TryAdd(e.To.Value, []);
                    ends.Implementers[e.To.Value].Add(e.From);
                }

                if (e.Type == EdgeType.PartOf)
                {
                    _ = ends.Parents.TryAdd(e.From.Value, e.To);
                }

                ends.Touching[e.From.Value] = ends.Touching.GetValueOrDefault(e.From.Value) + 1;
                if (e.To.Value != e.From.Value)
                {
                    ends.Touching[e.To.Value] = ends.Touching.GetValueOrDefault(e.To.Value) + 1;
                }
            }

            return ends;
        }
    }

    private static ReviewEntry LinkEntry(EdgeEnds ends, GraphEdge edge)
    {
        var to = ends.Label(edge.To) ?? edge.To.Name;
        return new ReviewEntry(edge.From, LinkIds.For(edge), ReviewGroup.Model, $"{edge.From.Name} {EdgeRules.Name(edge.Type)} {to}", edge.Provenance, edge.Confidence, 1, [edge.From], edge.To, Trust: TrustPolicy.For(edge.Provenance, accepted: false));
    }

    private static ReviewEntry Orphan(MapOverride o) =>
        new(o.Target, o.Link, ReviewGroup.Orphaned, $"{o.Action.ToString().ToLowerInvariant()} {o.Label ?? o.Target.Name}", Provenance.Human, 1, 0, [], Parent: null, o.ToId());
}
