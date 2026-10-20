using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The edges layer under the headless platform: which edge a point is on (the 12 px hit stroke), hover, and the geometry it builds.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ContextEdgesControlTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextEdgesControlTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static MapEdge Edge(string key, Point from, Point to, bool dashed = false) => new(key, EdgeRoute.Straight(from, to), dashed);

    private static (Window Window, ContextEdgesControl Edges) Show(params MapEdge[] edges)
    {
        var control = new ContextEdgesControl { Width = 400, Height = 300, Edges = edges, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = new Window { Content = control, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, control);
    }

    [Fact]
    public async Task A_point_within_6_px_of_the_line_is_on_the_edge_and_one_further_is_not() => await OnUi(() =>
    {
        var (window, edges) = Show(Edge("a", new Point(20, 50), new Point(220, 50)));

        Assert.Equal("a", edges.EdgeAt(new Point(100, 50)));
        Assert.Equal("a", edges.EdgeAt(new Point(100, 55.5)));
        Assert.Null(edges.EdgeAt(new Point(100, 57)));
        Assert.Null(edges.EdgeAt(new Point(300, 50)));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Where_two_edges_overlap_the_one_drawn_last_is_on_top() => await OnUi(() =>
    {
        var (window, edges) = Show(Edge("under", new Point(0, 100), new Point(200, 100)), Edge("over", new Point(100, 100), new Point(300, 100)));

        Assert.Equal("over", edges.EdgeAt(new Point(150, 100)));
        Assert.Equal("under", edges.EdgeAt(new Point(50, 100)));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_hit_test_is_the_edge_test() => await OnUi(() =>
    {
        var (window, edges) = Show(Edge("a", new Point(20, 50), new Point(220, 50)));

        Assert.True(edges.HitTest(new Point(100, 50)));
        Assert.False(edges.HitTest(new Point(100, 200)));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Hovering_an_edge_names_it_and_leaving_clears_it() => await OnUi(() =>
    {
        var (window, edges) = Show(Edge("a", new Point(20, 50), new Point(220, 50)));

        window.MouseMove(new Point(100, 52));
        Assert.Equal("a", edges.Hovered);

        window.MouseMove(new Point(100, 200));
        Assert.Null(edges.Hovered);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_curve_and_an_elbow_are_hit_along_their_whole_path() => await OnUi(() =>
    {
        var (window, edges) = Show(
            new MapEdge("curve", EdgeRoute.Curve(new Point(50, 20), new Point(250, 220)), Dashed: false),
            new MapEdge("elbow", EdgeRoute.Bus(new Point(350, 20), new Point(300, 220), 120), Dashed: true));

        Assert.Equal("curve", edges.EdgeAt(new Point(150, 120)));
        Assert.Equal("elbow", edges.EdgeAt(new Point(350, 60)));
        Assert.Equal("elbow", edges.EdgeAt(new Point(325, 120)));
        Assert.Equal("elbow", edges.EdgeAt(new Point(300, 180)));
        Assert.Null(edges.EdgeAt(new Point(100, 250)));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Changing_the_edges_changes_what_can_be_hit() => await OnUi(() =>
    {
        var (window, edges) = Show(Edge("a", new Point(20, 50), new Point(220, 50)));

        edges.Edges = [Edge("b", new Point(20, 150), new Point(220, 150))];

        Assert.Null(edges.EdgeAt(new Point(100, 50)));
        Assert.Equal("b", edges.EdgeAt(new Point(100, 150)));
        window.Close();
        return true;
    });

    [Fact]
    public async Task No_edges_draw_nothing_and_hit_nothing() => await OnUi(() =>
    {
        var (window, edges) = Show();

        Assert.Null(edges.EdgeAt(new Point(10, 10)));
        window.Close();
        return true;
    });
}
