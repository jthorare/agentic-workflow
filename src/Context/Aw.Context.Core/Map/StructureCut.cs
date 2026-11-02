using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Map;

/// <summary>
/// What a build drops from the map it keeps because the build makes it again (spec 2026-10-27 §2.3 and §5): every deterministic home and group feature with its links, and every cluster-made feature (a model feature whose file links are all
/// the model's) with its links. A feature a person wrote, and a model feature with another origin (a scoping run's proposal), stay. A decision on a dropped file link moves to the new 'tags' link of the same file and name, or is undone;
/// a decision on a dropped feature the build does not make again is undone.
/// </summary>
internal static class StructureCut
{
    public static (GraphSnapshot Graph, IReadOnlyList<MapOverride> Rows, StructureCounts Counts) Apply(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ExtractedMap found, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(found);
        var (own, kept) = (Purged(graph), Accepted(graph, overrides));
        var purged = own.Except(kept.Select(e => e.To.Value), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var remade = found.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var dropped = graph.Edges.Where(e => !kept.Contains(e) && IsDropped(e, own, remade)).ToList();
        if (purged.Count == 0 && dropped.Count == 0)
        {
            return (graph, [], StructureCounts.None);
        }

        var dropSet = dropped.ToHashSet();
        var cut = graph with { Nodes = [.. graph.Nodes.Where(n => !purged.Contains(n.Key.Value))], Edges = [.. graph.Edges.Where(e => !dropSet.Contains(e))] };
        var (rows, undone, carried) = Decisions(overrides, dropped.Where(IsFileLink), purged, found, at);
        var replaced = dropped.Where(e => e is { Type: EdgeType.Implements, Provenance: Provenance.Model } && IsFileLink(e)).ToList();
        var features = replaced.Select(e => e.To.Value).Distinct(StringComparer.Ordinal).Count();
        return (cut, rows, new StructureCounts(features, replaced.Count, undone, carried));
    }

    // A 'tags' link a person or the engine accepted outlives the build that no longer proposes its cluster (owner, 2026-10-27): it stays, and so does the tag feature it points at.
    private static HashSet<GraphEdge> Accepted(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides)
    {
        var accepted = OverrideLog.Active(overrides).Select(o => o is { Action: OverrideAction.Accept, Link: { } link } ? link.Value : null).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return accepted.Count == 0
            ? []
            : [.. graph.Edges.Where(e => e is { Type: EdgeType.Tags, Provenance: Provenance.Model } && accepted.Contains(LinkIds.For(e).Value))];
    }

    private static bool IsFileLink(GraphEdge e) => e is { Type: EdgeType.Implements or EdgeType.Tags, From.Kind: NodeKind.Code, To.Kind: NodeKind.Feature };

    // An edge a pin declared in the profile (spec 2026-10-27 §10.3): the build makes it again from the profile's pins, so one whose entry was removed leaves nothing behind.
    private static bool IsDeclared(GraphEdge e) =>
        e is { Provenance: Provenance.Human, From.Kind: NodeKind.Concept } && e.EvidenceOrEmpty.Any(x => x.Key.Value.StartsWith("profile:index.concepts.", StringComparison.Ordinal));

    // A deterministic feature is a home or a group; a model feature is the build's own when every file link into it is the model's.
    private static HashSet<string> Purged(GraphSnapshot graph)
    {
        var links = graph.Edges.Where(IsFileLink).ToLookup(e => e.To.Value, StringComparer.Ordinal);
        return [.. graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature && Purges(n, [.. links[n.Key.Value]])).Select(n => n.Key.Value)];
    }

    private static bool Purges(GraphNode feature, List<GraphEdge> links) =>
        feature.Provenance == Provenance.Deterministic || (feature.Provenance == Provenance.Model && links.Count > 0 && links.TrueForAll(e => e.Provenance == Provenance.Model));

    // The edges of a feature the build makes again (a deterministic file link into a feature a person or an import wrote is theirs and stays). A person's edge to one of those features stays when the feature comes back.
    private static bool IsDropped(GraphEdge e, HashSet<string> purged, HashSet<string> remade)
    {
        // A roll-up edge has a count, only the build writes one, and it writes every roll-up edge again; a pin's declared edge is made again from the profile.
        if (e.Count is not null || IsDeclared(e))
        {
            return true;
        }

        var touched = purged.Contains(e.From.Value) ? e.From.Value : purged.Contains(e.To.Value) ? e.To.Value : null;
        return touched is not null && (e.Provenance != Provenance.Human || !remade.Contains(touched));
    }

    private static (List<MapOverride> Rows, int Undone, int Carried) Decisions(IReadOnlyList<MapOverride> overrides, IEnumerable<GraphEdge> droppedLinks, HashSet<string> purged, ExtractedMap found, DateTimeOffset at)
    {
        var old = droppedLinks.GroupBy(e => LinkIds.For(e).Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var again = found.Edges.Select(e => LinkIds.For(e).Value).ToHashSet(StringComparer.Ordinal);
        var remade = found.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var tags = found.Edges.Where(e => e.Type == EdgeType.Tags).ToLookup(e => e.From.Value, StringComparer.Ordinal);
        var rows = new List<MapOverride>();
        int undone = 0, carried = 0;
        foreach (var decision in OverrideLog.Active(overrides).Where(o => o.Action is not (OverrideAction.Propose or OverrideAction.Add)))
        {
            if (decision.Link is { } link)
            {
                if (!old.TryGetValue(link.Value, out var edge) || again.Contains(link.Value))
                {
                    continue;
                }

                rows.Add(MapOverride.UndoingStructure(decision, at));
                if (TagFor(edge, tags[edge.From.Value]) is { } tag && decision.Action is OverrideAction.Accept or OverrideAction.Reject)
                {
                    rows.Add(decision with { Link = LinkIds.For(tag) });
                    carried++;
                }
                else
                {
                    undone++;
                }
            }
            else if (Lost(decision.Target, purged, remade) || (decision.Other is { } other && Lost(other, purged, remade)))
            {
                rows.Add(MapOverride.UndoingStructure(decision, at));
                undone++;
            }
        }

        return (rows, undone, carried);
    }

    // The new 'tags' link of the same file whose key names what the old cluster feature was ('feature:src.commands' is now 'feature:tag:<repo>:src.commands').
    private static GraphEdge? TagFor(GraphEdge old, IEnumerable<GraphEdge> candidates)
    {
        if (old.Type != EdgeType.Implements)
        {
            return null;
        }

        return candidates.FirstOrDefault(t => t.To.Name.StartsWith("tag:", StringComparison.Ordinal) && t.To.Name[(t.To.Name.IndexOf(':', "tag:".Length) + 1)..] == old.To.Name);
    }

    private static bool Lost(NodeKey key, HashSet<string> purged, HashSet<string> remade) => purged.Contains(key.Value) && !remade.Contains(key.Value);
}
