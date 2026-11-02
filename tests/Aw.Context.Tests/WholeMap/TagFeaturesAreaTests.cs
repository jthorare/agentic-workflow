using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.WholeMap;

namespace Aw.Context.Tests.WholeMap;

/// <summary>Spec 2026-10-27 §2.3 and §2.4: tag features are never an area and never a concept, so adding them changes neither count.</summary>
public sealed class TagFeaturesAreaTests
{
    private static (List<GraphNode> Nodes, List<GraphEdge> Edges) Home()
    {
        List<GraphNode> nodes = [];
        List<GraphEdge> edges = [];
        foreach (var root in new[] { "sched", "billing", "tabs" })
        {
            nodes.AddRange(new[] { root, root + ".one", root + ".two" }.Select(n => new GraphNode(NodeKey.From("feature:" + n), n, Provenance.Deterministic)));
            nodes.Add(new GraphNode(NodeKey.From($"code:src/{root}/a.ts"), "a.ts", Provenance.Deterministic));
            edges.Add(new GraphEdge(NodeKey.From($"feature:{root}.one"), NodeKey.From("feature:" + root), EdgeType.PartOf, Provenance.Deterministic));
            edges.Add(new GraphEdge(NodeKey.From($"feature:{root}.two"), NodeKey.From("feature:" + root), EdgeType.PartOf, Provenance.Deterministic));
            edges.Add(new GraphEdge(NodeKey.From($"code:src/{root}/a.ts"), NodeKey.From($"feature:{root}.one"), EdgeType.Implements, Provenance.Deterministic));
        }

        return (nodes, edges);
    }

    private static GraphSnapshot Plain()
    {
        var (nodes, edges) = Home();
        return new GraphSnapshot(nodes, edges, [], []);
    }

    private static GraphSnapshot WithTags()
    {
        var (nodes, edges) = Home();
        for (var i = 0; i < 40; i++)
        {
            var key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"feature:tag:work:t{i}");
            nodes.Add(new GraphNode(NodeKey.From(key), "tag", Provenance.Model, 0.7));
            edges.Add(new GraphEdge(NodeKey.From("code:src/sched/a.ts"), NodeKey.From(key), EdgeType.Tags, Provenance.Model, 0.7));
        }

        return new GraphSnapshot(nodes, edges, [], []);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tag_features_are_never_the_root_of_an_area_so_the_areas_are_the_same_with_or_without_them()
    {
        var without = MapAreas.Assign(Plain());
        var with = MapAreas.Assign(WithTags());

        Assert.Equal(without.ByNode.Values.Select(a => a.Area).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), with.ByNode.Values.Select(a => a.Area).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(with.ByNode.Where(p => p.Key.StartsWith("feature:tag:", StringComparison.Ordinal)), p => Assert.Equal("feature:sched", p.Value.Area));
    }

    [Fact, Trait("Lane", "unit")]
    public void Tag_features_are_never_a_concept_member_so_the_concepts_are_the_same_with_or_without_them()
    {
        var without = ConceptDeriver.Derive(Plain(), [], names: null);
        var with = ConceptDeriver.Derive(WithTags(), [], names: null);

        Assert.Equal(without.Nodes.Select(n => n.Key.Value), with.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.Equal(without.Edges.Select(e => (e.From.Value, e.To.Value)), with.Edges.Select(e => (e.From.Value, e.To.Value)));
        Assert.DoesNotContain(with.Edges, e => e.From.Value.StartsWith("feature:tag:", StringComparison.Ordinal));
    }
}
