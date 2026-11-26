using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #4470: an If step's true and false edges each get their own route, and each label sits beside its own line. Seen on "Both edges": Flagged? -> Tell me (true) and -> Skip it (false) wrapped to the next row
/// along one shared lane, so "false" floated with no line under it and "true" sat on the line.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GraphBranchEdgeRouteTests
{
    private const string Nodes = """[{"id":"start","type":"manual","config":{}},{"id":"measure","type":"code","title":"Measure repos","config":{}},{"id":"flagged","type":"condition","title":"Flagged?","config":{}},{"id":"tell","type":"notify","title":"Tell me","config":{}},{"id":"skip","type":"notify","title":"Skip it","config":{}}]""";

    private const string Edges = """[{"from":"start","to":"measure"},{"from":"measure","to":"flagged"},{"from":"flagged","to":"tell","output":"true"},{"from":"flagged","to":"skip","output":"false"}]""";

    private const string Positions = """[{"node":"start","x":0,"y":0},{"node":"measure","x":280,"y":0},{"node":"flagged","x":560,"y":0},{"node":"tell","x":840,"y":0},{"node":"skip","x":840,"y":100}]""";

    private static async Task<WorkflowGraphViewModel> Laid()
    {
        var graph = await WorkflowGraphViewModelTests.GraphOf(new WorkflowsFixture(), Nodes, Edges, Positions);
        _ = graph.Relayout(1230, GraphFitTests.CanvasHeight, 1.0);   // the 1280 px frame: the two arms wrap onto the second row
        return graph;
    }

    private static List<(GraphPoint A, GraphPoint B)> Segments(GraphEdgeViewModel edge)
    {
        List<GraphPoint> points = [edge.Start, .. edge.Route, edge.End];
        return [.. points.Zip(points.Skip(1))];
    }

    private static bool SharesHorizontalRun((GraphPoint A, GraphPoint B) p, (GraphPoint A, GraphPoint B) q) =>
        Math.Abs(p.A.Y - p.B.Y) < 0.5 && Math.Abs(q.A.Y - q.B.Y) < 0.5 && Math.Abs(p.A.Y - q.A.Y) < 0.5
        && Math.Min(Math.Max(p.A.X, p.B.X), Math.Max(q.A.X, q.B.X)) - Math.Max(Math.Min(p.A.X, p.B.X), Math.Min(q.A.X, q.B.X)) > 0.5;

    // a label's box (the pill: about 52 x 18 px at 100 % text) against one line segment, which is horizontal or vertical
    private static bool Touches(GraphPoint center, (GraphPoint A, GraphPoint B) s)
    {
        double left = center.X - 26, right = center.X + 26, top = center.Y - 9, bottom = center.Y + 9;
        double x0 = Math.Min(s.A.X, s.B.X), x1 = Math.Max(s.A.X, s.B.X), y0 = Math.Min(s.A.Y, s.B.Y), y1 = Math.Max(s.A.Y, s.B.Y);
        return x1 >= left && x0 <= right && y1 >= top && y0 <= bottom;
    }

    [Fact]
    public async Task The_true_and_false_edges_share_no_horizontal_run()
    {
        var graph = await Laid();
        var (yes, no) = (graph.Edges.Single(e => e.Label == "true"), graph.Edges.Single(e => e.Label == "false"));
        Assert.True(yes.Route.Count > 0 && no.Route.Count > 0, "the arms wrap in this frame");

        var shared = Segments(yes).SelectMany(p => Segments(no).Where(q => SharesHorizontalRun(p, q))).ToList();

        Assert.Empty(shared);
    }

    [Fact]
    public async Task Each_label_sits_off_every_edge_line_and_beside_its_own()
    {
        var graph = await Laid();

        foreach (var edge in graph.Edges.Where(e => e.Label is not null))
        {
            var at = edge.LabelAt ?? throw new InvalidOperationException($"the {edge.Label} edge has no label place");
            var all = graph.Edges.SelectMany(Segments).ToList();
            Assert.DoesNotContain(all, s => Touches(at, s));   // not on any line, its own or the other branch's
            var nearest = Segments(edge).Min(s => Math.Min(Math.Abs(at.Y - s.A.Y), Math.Abs(at.X - s.A.X)));
            Assert.True(nearest <= 24, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the {edge.Label} label is {nearest} px from its own line"));
        }
    }
}
