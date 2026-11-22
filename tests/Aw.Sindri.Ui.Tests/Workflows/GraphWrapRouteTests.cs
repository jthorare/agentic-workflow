using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A wrapped edge runs along its row's return lane (#3684): out through the right pad, back along the row gap, down the left pad, and never across a card.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphWrapRouteTests
{
    // Every point of a path's segments, one pixel apart, leaving out the endpoints (they touch the source and target cards' edges).
    private static IEnumerable<GraphPoint> Samples(GraphEdgeViewModel edge)
    {
        List<GraphPoint> path = [edge.Start, .. edge.Route, edge.End];
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            var steps = (int)Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)));
            for (var k = 1; k < steps; k++)
            {
                yield return new GraphPoint(a.X + ((b.X - a.X) * k / steps), a.Y + ((b.Y - a.Y) * k / steps));
            }
        }
    }

    private static bool Inside(GraphPoint p, GraphNodeViewModel n) => p.X > n.X + 0.5 && p.X < n.X + n.Width - 0.5 && p.Y > n.Y + 0.5 && p.Y < n.Y + n.Height - 0.5;

    [Theory]
    [InlineData(1440)]
    [InlineData(1000)]
    public async Task A_wrapped_edges_path_stays_out_of_every_card(double width)
    {
        var graph = await ChainGraph.Of(16);

        _ = graph.Relayout(width, GraphFitTests.CanvasHeight, 1.0);

        var wrapped = graph.Edges.Where(e => e.Route.Count > 0).ToList();
        Assert.NotEmpty(wrapped);   // 16 steps wrap at both widths
        foreach (var edge in wrapped)
        {
            var hit = Samples(edge).SelectMany(p => graph.Nodes.Where(n => Inside(p, n)).Select(n => n.Id)).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(hit.Count == 0, $"{edge.From} -> {edge.To} crosses {string.Join(", ", hit)}");
        }
    }

    [Fact]
    public async Task An_edge_inside_one_row_has_no_route_and_a_wrapped_one_ends_level_with_its_target()
    {
        var graph = await ChainGraph.Of(16);

        _ = graph.Relayout(GraphFitTests.CanvasWidth, GraphFitTests.CanvasHeight, 1.0);   // 4 rows of 4: s3 -> s4 wraps, s0 -> s1 does not

        var inRow = graph.Edges.Single(e => string.Equals(e.From, "s0", StringComparison.Ordinal));
        var wrap = graph.Edges.Single(e => string.Equals(e.From, "s3", StringComparison.Ordinal));
        Assert.Empty(inRow.Route);
        Assert.Equal(4, wrap.Route.Count);
        Assert.Equal(wrap.End.Y, wrap.Route[^1].Y, 3);   // the last stretch runs straight into the target's left side
        Assert.True(wrap.Route[0].X > wrap.Start.X, "the route does not leave to the right of the row's last card");
    }
}
