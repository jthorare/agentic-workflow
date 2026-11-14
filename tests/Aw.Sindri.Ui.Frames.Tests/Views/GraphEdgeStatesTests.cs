using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Context.Layout;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3234 edges box: the shared edge layer draws what the Workflows canvas draws by hand today: an arrow head, a label pill, the lit state at
/// double width and the dimmed state at 0.42, all from the existing tokens (Brush.Wire #686870, WireHi).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class GraphEdgeStatesTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphEdgeStatesTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static GraphEdgesControl Show(out Window window, params MapEdge[] edges)
    {
        var control = new GraphEdgesControl { Width = 400, Height = 300, Edges = edges, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        window = new Window { Content = control, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return control;
    }

    private static MapEdge Line(string key, bool arrow = false, string? label = null) =>
        new(key, EdgeRoute.Straight(new MapPoint(20, 100), new MapPoint(220, 100)), Dashed: false, Arrow: arrow, Label: label);

    [Fact]
    public async Task An_edge_with_an_arrow_ends_in_a_9_px_head_that_points_along_the_line() => await OnUi(() =>
    {
        var edges = Show(out var window, Line("a", arrow: true), Line("plain"));
        var head = edges.ArrowHead("a");

        Assert.NotNull(head);
        Assert.Equal(3, head.Length);
        Assert.Equal(new Point(220, 100), head[0]);   // the tip sits on the edge's end
        Assert.All(head.Skip(1), wing => Assert.True(wing.X < 220 && Math.Abs(Math.Sqrt(Math.Pow(wing.X - 220, 2) + Math.Pow(wing.Y - 100, 2)) - 9) < 0.01, $"wing {wing}"));
        Assert.True(head[1].Y < 100 && head[2].Y > 100, "the wings sit either side of the line");
        Assert.Null(edges.ArrowHead("plain"));
        window.Close();
        return true;
    });

    // #3650: the 10 px port square is drawn over the arrow, so a tip on the route end hides the front 5 px of the 9 px head (by the head's geometry:
    // 12 of its 31.7 px2, 38 %). The tip belongs on the port square's outer edge, so the whole head shows.
    [Fact]
    public async Task The_arrow_head_ends_at_the_port_squares_outer_edge_and_no_corner_sits_under_the_port() => await OnUi(() =>
    {
        var edges = Show(out var window, Line("a", arrow: true));
        var head = edges.ArrowHead("a");
        var port = new Rect(215, 95, 10, 10);   // the route ends at (220, 100): the 10 px port square is centred there

        Assert.NotNull(head);
        Assert.Equal(new Point(215, 100), head[0]);   // the tip is on the square's outer edge, on the incoming side
        Assert.All(head, corner => Assert.False(corner.X > port.Left && corner.X < port.Right && corner.Y > port.Top && corner.Y < port.Bottom, $"{corner} is under the port"));
        Assert.All(head.Skip(1), wing => Assert.True(wing.X < 215 && Math.Abs(Math.Sqrt(Math.Pow(wing.X - 215, 2) + Math.Pow(wing.Y - 100, 2)) - 9) < 0.01, $"wing {wing}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_label_is_a_pill_centred_on_the_edge_midpoint_and_padded_around_its_text() => await OnUi(() =>
    {
        var edges = Show(out var window, Line("a", label: "if ok"), Line("plain"));
        var pill = edges.LabelPill("a");

        _ = Assert.NotNull(pill);
        Assert.Equal(120, pill.Value.Center.X, 1);
        Assert.Equal(100, pill.Value.Center.Y, 1);
        Assert.True(pill.Value.Width > 12 && pill.Value.Height > 4, $"{pill}");
        Assert.Null(edges.LabelPill("plain"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_lit_edge_draws_at_double_width_and_a_dimmed_one_at_042() => await OnUi(() =>
    {
        var edges = Show(out var window, Line("rest"), Line("lit"), Line("dim"));
        edges.Highlighted = new HashSet<string>(StringComparer.Ordinal) { "lit" };
        edges.Dimmed = new HashSet<string>(StringComparer.Ordinal) { "dim" };

        Assert.Equal(2, edges.StrokeWidth("rest"));
        Assert.Equal(4, edges.StrokeWidth("lit"));
        Assert.Equal(1, edges.StrokeOpacity("rest"));
        Assert.Equal(0.42, edges.StrokeOpacity("dim"));
        window.Close();
        return true;
    });
}
