using System.Globalization;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Concepts;

public sealed class ConceptGroupingSpreadTests
{
    private static readonly IReadOnlyList<ConceptPin> _noPins = [];

    private static GraphNode Feature(string name) => new(NodeKey.From("feature:" + name), name);

    private static GraphEdge Edge(string from, string to, EdgeType type) => new(NodeKey.From(from), NodeKey.From(to), type);

    // A root with two children: three features that the area rules group together.
    private static (List<GraphNode> Nodes, List<GraphEdge> Edges) Area(string root) =>
        ([Feature(root), Feature(root + ".one"), Feature(root + ".two")],
         [Edge($"feature:{root}.one", $"feature:{root}", EdgeType.PartOf), Edge($"feature:{root}.two", $"feature:{root}", EdgeType.PartOf)]);

    private static string AreaName(int i) => "area" + i.ToString("D2", CultureInfo.InvariantCulture);

    private static GraphSnapshot Graph(IEnumerable<GraphNode> nodes, IEnumerable<GraphEdge> edges) => new([.. nodes], [.. edges], [], []);

    private static GraphSnapshot UnconnectedAreas(int count)
    {
        var parts = Enumerable.Range(0, count).Select(i => Area(AreaName(i))).ToList();
        return Graph(parts.SelectMany(p => p.Nodes), parts.SelectMany(p => p.Edges));
    }

    private static List<int> ConceptSizes(ExtractedMap map) =>
        [.. map.Edges.Where(e => e.Type == EdgeType.BelongsTo && e.From.Kind == NodeKind.Feature && e.To.Value != ConceptKeys.Other)
            .GroupBy(e => e.To.Value, StringComparer.Ordinal).Select(g => g.Count())];

    [Fact, Trait("Lane", "unit")]
    public void Unconnected_areas_leave_no_concept_over_three_times_the_average()
    {
        var map = ConceptDeriver.Derive(UnconnectedAreas(60), _noPins, names: null);

        var sizes = ConceptSizes(map);
        var average = 180.0 / sizes.Count;
        Assert.True(sizes.Max() <= 3 * average, string.Create(CultureInfo.InvariantCulture, $"largest concept holds {sizes.Max()} features, over {sizes.Count} concepts, average {average:F1}: more than 3x the average"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Unconnected_areas_leave_no_concept_over_half_the_features()
    {
        var map = ConceptDeriver.Derive(UnconnectedAreas(60), _noPins, names: null);

        var sizes = ConceptSizes(map);
        Assert.True(sizes.Max() <= 90, string.Create(CultureInfo.InvariantCulture, $"largest concept holds {sizes.Max()} of 180 features, over {sizes.Count} concepts: more than half"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Strongly_connected_areas_still_merge_into_one_concept()
    {
        var parts = Enumerable.Range(0, 16).Select(i => Area(AreaName(i))).ToList();
        string[] a = ["area00", "area00.one", "area00.two"];
        string[] b = ["area01", "area01.one", "area01.two"];
        var links = a.SelectMany(x => b.Select(y => Edge($"feature:{x}", $"feature:{y}", EdgeType.DependsOn)));

        var map = ConceptDeriver.Derive(Graph(parts.SelectMany(p => p.Nodes), parts.SelectMany(p => p.Edges).Concat(links)), _noPins, names: null);

        string ConceptOf(string feature) => map.Edges.Single(e => e.Type == EdgeType.BelongsTo && e.From.Value == "feature:" + feature).To.Value;
        Assert.Equal(ConceptOf("area00"), ConceptOf("area01"));
        Assert.Equal(ConceptOf("area00.one"), ConceptOf("area01.two"));
    }
}
