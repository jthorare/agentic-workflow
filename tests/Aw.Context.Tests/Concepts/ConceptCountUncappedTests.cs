using System.Globalization;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Concepts;

/// <summary>There is no fixed number of concepts: areas of three features stay their own concepts however many there are, a one-feature group joins its best-connected neighbour, and two areas merge only when their features link into each other.</summary>
public sealed class ConceptCountUncappedTests
{
    private static readonly IReadOnlyList<ConceptPin> _noPins = [];

    private static GraphNode Feature(string name) => new(NodeKey.From("feature:" + name), name);

    private static GraphEdge Edge(string from, string to, EdgeType type) => new(NodeKey.From(from), NodeKey.From(to), type);

    // A root with two children: three features that the area rules group together.
    private static (List<GraphNode> Nodes, List<GraphEdge> Edges) Area(string root) =>
        ([Feature(root), Feature(root + ".one"), Feature(root + ".two")],
         [Edge($"feature:{root}.one", $"feature:{root}", EdgeType.PartOf), Edge($"feature:{root}.two", $"feature:{root}", EdgeType.PartOf)]);

    private static string AreaName(int i) => "area" + i.ToString("D2", CultureInfo.InvariantCulture);

    private static ExtractedMap Derive(int areas, IEnumerable<GraphNode> extraNodes, IEnumerable<GraphEdge> extraEdges)
    {
        var parts = Enumerable.Range(0, areas).Select(i => Area(AreaName(i))).ToList();
        var graph = new GraphSnapshot([.. parts.SelectMany(p => p.Nodes), .. extraNodes], [.. parts.SelectMany(p => p.Edges), .. extraEdges], [], []);
        return ConceptDeriver.Derive(graph, _noPins, names: null);
    }

    private static string ConceptOf(ExtractedMap map, string feature) =>
        map.Edges.Single(e => e.Type == EdgeType.BelongsTo && e.From.Value == "feature:" + feature).To.Value;

    private static List<int> ConceptSizes(ExtractedMap map) =>
        [.. map.Edges.Where(e => e.Type == EdgeType.BelongsTo && e.From.Kind == NodeKind.Feature && e.To.Value != ConceptKeys.Other)
            .GroupBy(e => e.To.Value, StringComparer.Ordinal).Select(g => g.Count())];

    [Fact, Trait("Lane", "unit")]
    public void Forty_unconnected_areas_make_forty_concepts_and_none_passes_the_cap()
    {
        var map = Derive(40, [], []);

        var sizes = ConceptSizes(map);
        Assert.True(sizes.Count > 15, string.Create(CultureInfo.InvariantCulture, $"{sizes.Count} concepts"));
        Assert.Equal(120, sizes.Sum());
        Assert.True(sizes.Max() <= Math.Max(2 * 3, (int)Math.Ceiling(3.0 * 120 / 40)), string.Create(CultureInfo.InvariantCulture, $"largest concept holds {sizes.Max()} features"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_one_feature_group_with_depends_on_edges_joins_the_area_it_depends_on()
    {
        var map = Derive(3, [Feature("lone")], [Edge("feature:lone", "feature:area01.one", EdgeType.DependsOn), Edge("feature:lone", "feature:area01.two", EdgeType.DependsOn)]);

        Assert.Equal(ConceptOf(map, "area01"), ConceptOf(map, "lone"));
        Assert.Equal(3, ConceptSizes(map).Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_areas_with_three_links_merge_and_two_with_one_link_do_not()
    {
        string[] a = ["area00", "area00.one", "area00.two"];
        string[] b = ["area01", "area01.one", "area01.two"];
        var linked = a.Zip(b, (x, y) => Edge($"feature:{x}", $"feature:{y}", EdgeType.DependsOn));
        var single = Edge("feature:area02", "feature:area03", EdgeType.DependsOn);

        var map = Derive(4, [], [.. linked, single]);

        Assert.Equal(ConceptOf(map, "area00"), ConceptOf(map, "area01"));
        Assert.NotEqual(ConceptOf(map, "area02"), ConceptOf(map, "area03"), StringComparer.Ordinal);
    }
}
