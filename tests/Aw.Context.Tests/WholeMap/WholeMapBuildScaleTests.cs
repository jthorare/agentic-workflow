using System.Diagnostics;
using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Concepts;

namespace Aw.Context.Tests.WholeMap;

/// <summary>
/// The whole-map build stays near-linear at the size #4363 reported (52,000 nodes, about 342,000 edges, no concept): any new per-node full pass over the nodes or edges
/// shows up here as a red, not as the desktop's 120 s timeout. Heavy lane: it builds a big graph.
/// </summary>
public sealed class WholeMapBuildScaleTests
{
    private const int Features = 12_000;
    private const int Records = 4_000;
    private const int Routes = 2_000;
    private const int Screens = 1_000;
    private const int CodeFiles = 33_000;
    private const int ComponentRoutes = 1_000;
    private const int DependsOnEdges = 300_000;
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(20);
    private static readonly WholeMapBasis _basis = new("v1", MapCommit: null, Draft: false, DateTimeOffset.UnixEpoch, Repo: null);

    private static string Id(string prefix, int i) => prefix + i.ToString(CultureInfo.InvariantCulture);

    private static GraphNode Node(string key) => new(NodeKey.From(key), key);

    private static GraphEdge Edge(string from, string to, EdgeType type) => new(NodeKey.From(from), NodeKey.From(to), type);

    // Deterministic: a fixed seed, the same graph every run.
    private static GraphSnapshot Generate()
    {
        var random = new Random(4363);
        var nodes = new List<GraphNode>(Features + Records + Routes + Screens + CodeFiles);
        var edges = new List<GraphEdge>(DependsOnEdges + (2 * Features) + Routes + Screens);
        var code = new string[CodeFiles];
        for (var i = 0; i < CodeFiles; i++)
        {
            // The first ComponentRoutes files are components a route is implemented from; the rest alternate between .cs and .ts.
            code[i] = i < ComponentRoutes ? $"code:web/pages/{Id("P", i)}.tsx" : $"code:src/{Id("f", i)}{(i % 2 == 0 ? ".cs" : ".ts")}";
            nodes.Add(Node(code[i]));
        }

        for (var i = 0; i < Features; i++)
        {
            var feature = "feature:" + Id("f", i);
            nodes.Add(Node(feature));
            edges.Add(Edge(code[random.Next(CodeFiles)], feature, EdgeType.Implements));
            edges.Add(Edge(code[random.Next(CodeFiles)], feature, EdgeType.Implements));
        }

        AddProductNodes(nodes, edges, code, random);

        for (var i = 0; i < DependsOnEdges; i++)
        {
            edges.Add(Edge(code[random.Next(CodeFiles)], code[random.Next(CodeFiles)], EdgeType.DependsOn));
        }

        return new GraphSnapshot(nodes, edges, [], []);
    }

    private static void AddProductNodes(List<GraphNode> nodes, List<GraphEdge> edges, string[] code, Random random)
    {
        for (var i = 0; i < Records; i++)
        {
            nodes.Add(Node("record:" + Id("r", i)));
        }

        for (var i = 0; i < Routes; i++)
        {
            var route = $"surface:route/GET:/{Id("r", i)}";
            nodes.Add(Node(route));
            edges.Add(Edge(code[i < ComponentRoutes ? i : random.Next(ComponentRoutes, CodeFiles)], route, EdgeType.Implements));
        }

        for (var i = 0; i < Screens; i++)
        {
            var screen = $"surface:screen/{Id("s", i)}";
            nodes.Add(Node(screen));
            edges.Add(Edge(code[random.Next(CodeFiles)], screen, EdgeType.Implements));
        }
    }

    [Fact, Trait("Lane", "heavy")]
    public void The_whole_map_builds_at_the_reported_size_in_under_twenty_seconds()
    {
        var output = TestContext.Current.TestOutputHelper;
        var graph = Generate();
        output?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"graph: {graph.Nodes.Count} nodes, {graph.Edges.Count} edges"));
        var total = Stopwatch.StartNew();
        var rules = WholeMapRules.Default;

        var stage = Stopwatch.StartNew();
        var areas = MapAreas.Assign(graph, rules.AreaCap);
        Report(output, "MapAreas.Assign", stage);

        // The desktop's open reads the overview first, then each opened part: the pages in no concept, the 'other' concept and the first area's nodes.
        var overview = Build(new WholeMapAsk(_basis, WholeMapDetail.Auto, []), graph, areas, output, "Build auto (overview)");
        _ = Build(new WholeMapAsk(_basis, WholeMapDetail.Auto, []) { PagesInNoConcept = true }, graph, areas, output, "Build pages in no concept");
        _ = Build(new WholeMapAsk(_basis, WholeMapDetail.Auto, []) { Concept = ConceptKeys.Other }, graph, areas, output, "Build concept:other");
        if (overview.Areas.Count > 0)
        {
            _ = Build(new WholeMapAsk(_basis, WholeMapDetail.Nodes, [], overview.Areas[0].Key), graph, areas, output, "Build first area (nodes)");
        }

        Report(output, "total", total);
        Assert.True(total.Elapsed < _budget, string.Create(CultureInfo.InvariantCulture, $"the whole-map build took {total.Elapsed.TotalSeconds:0.0} s for {graph.Nodes.Count} nodes (budget {_budget.TotalSeconds:0} s, #4363)"));
    }

    private static WholeMapView Build(WholeMapAsk ask, GraphSnapshot graph, AreaIndex areas, ITestOutputHelper? output, string name)
    {
        var stage = Stopwatch.StartNew();
        var built = WholeMapBuilder.Build(ask, graph, areas, WholeMapRules.Default, ConceptGraph.Text);
        Report(output, name, stage);
        Assert.True(built.IsSuccess, name + " failed");
        return built.Value;
    }

    private static void Report(ITestOutputHelper? output, string name, Stopwatch stage) =>
        output?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{name}: {stage.Elapsed.TotalSeconds:0.00} s"));
}
