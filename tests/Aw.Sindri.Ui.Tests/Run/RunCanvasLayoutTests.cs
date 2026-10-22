using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Layout;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>
/// The run canvas's deterministic layout (F3 spec 3 §2): the prototype's running sample (four stages, the redesign card and
/// the three ends) must come out at the rects its own layout() computes for the same heights, and any other graph follows the same rules.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunCanvasLayoutTests
{
    private static readonly string[] _ends = ["won", "lost", "lost2", "off", "off2"];

    private static RunNode Node(string id, RunNodeKind kind, int? order = null, string? branchOf = null, string? outcome = null) =>
        new(id, kind, id, order, branchOf, RunNodeState.NotStarted, outcome, WorkflowName: null, ChildId: null, ChildAddress: null, Steps: []);

    private static RunEdge Edge(string from, string to, RunEdgeKind kind = RunEdgeKind.Main) => new(from, to, kind, Outcome: null, Label: null, EdgeTone.Normal);

    // scoping, design, plan, phase; redesign (a loop card leaving plan); Merged, Stuck, Stopped.
    private static RunGraph Sample() => new(
        [
            Node("scoping", RunNodeKind.Stage, 1),
            Node("design", RunNodeKind.Stage, 2),
            Node("plan", RunNodeKind.Stage, 3),
            Node("phase", RunNodeKind.Stage, 4),
            Node("redesign", RunNodeKind.Side, branchOf: "plan"),
            Node("done", RunNodeKind.End, 5, outcome: "resolved"),
            Node("stuck", RunNodeKind.End, 6, outcome: "unresolved"),
            Node("dropped", RunNodeKind.End, 7, outcome: "cancelled"),
        ],
        [
            Edge("scoping", "design"),
            Edge("design", "plan"),
            Edge("plan", "phase"),
            Edge("phase", "done", RunEdgeKind.End),
            Edge("phase", "stuck", RunEdgeKind.End),
            Edge("scoping", "dropped", RunEdgeKind.End),
            Edge("redesign", "plan", RunEdgeKind.Loop),
        ],
        BranchesServed: false);

    private static Dictionary<string, double> Heights() => new(StringComparer.Ordinal)
    {
        ["scoping"] = 236,
        ["design"] = 228,
        ["plan"] = 196,
        ["phase"] = 260,
        ["redesign"] = 96,
        ["done"] = 92,
        ["stuck"] = 92,
        ["dropped"] = 92,
    };

    private static Dictionary<string, double> Only(params string[] ids) => ids.ToDictionary(id => id, _ => 100d, StringComparer.Ordinal);


    [Fact]
    public void Running_sample_matches_the_prototype_rects()
    {
        var layout = RunCanvasLayout.Of(Sample(), Heights());

        // The prototype: y runs down the main path with 104 between cards; the stages are 320 wide and centred on x = 0; redesign is
        // 280 wide at +470 and 60 above the bottom of design; the ends sit 104 under phase at 0, +470 and -470.
        Assert.Equal(new MapRect(-160, 0, 320, 236), layout.Rects["scoping"]);
        Assert.Equal(new MapRect(-160, 340, 320, 228), layout.Rects["design"]);
        Assert.Equal(new MapRect(-160, 672, 320, 196), layout.Rects["plan"]);
        Assert.Equal(new MapRect(-160, 972, 320, 260), layout.Rects["phase"]);
        Assert.Equal(new MapRect(330, 508, 280, 96), layout.Rects["redesign"]);
        Assert.Equal(new MapRect(-140, 1336, 280, 92), layout.Rects["done"]);
        Assert.Equal(new MapRect(330, 1336, 280, 92), layout.Rects["stuck"]);
        Assert.Equal(new MapRect(-610, 1336, 280, 92), layout.Rects["dropped"]);
        Assert.Equal(new MapRect(-730, -120, 1460, 1668), layout.World);
    }

    [Fact]
    public void A_taller_card_pushes_every_card_below_it()
    {
        var taller = Heights();
        taller["design"] = 300;

        var layout = RunCanvasLayout.Of(Sample(), taller);

        Assert.Equal(new MapRect(-160, 340, 320, 300), layout.Rects["design"]);
        Assert.Equal(744, layout.Rects["plan"].Y);
        Assert.Equal(1044, layout.Rects["phase"].Y);
        Assert.Equal(1408, layout.Rects["done"].Y);
        Assert.Equal(580, layout.Rects["redesign"].Y);
    }

    [Fact]
    public void The_end_cards_take_their_column_from_the_outcome()
    {
        var graph = new RunGraph(
            [
                Node("a", RunNodeKind.Stage, 1),
                Node("won", RunNodeKind.End, 2, outcome: "alreadySatisfied"),
                Node("lost", RunNodeKind.End, 3, outcome: "failed"),
                Node("lost2", RunNodeKind.End, 4, outcome: "unresolved"),
                Node("off", RunNodeKind.End, 5, outcome: "cancelled"),
                Node("off2", RunNodeKind.End, 6, outcome: null),
            ],
            [],
            BranchesServed: false);

        var layout = RunCanvasLayout.Of(graph, Only("a"));

        double CentreX(string id) => layout.Rects[id].X + (layout.Rects[id].Width / 2);
        Assert.Equal([0d, 470d, 940d, -470d, -940d], _ends.Select(CentreX));
        Assert.All(_ends, id => Assert.Equal(204, layout.Rects[id].Y));
    }

    [Fact]
    public void A_graph_with_no_end_draws_no_end_row()
    {
        var graph = new RunGraph([Node("a", RunNodeKind.Stage, 1), Node("b", RunNodeKind.Stage, 2)], [Edge("a", "b")], BranchesServed: false);

        var layout = RunCanvasLayout.Of(graph, Only("a", "b"));

        Assert.Equal(["a", "b"], layout.Rects.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(new MapRect(-280, -120, 560, 544), layout.World);
    }

    [Fact]
    public void A_card_without_a_measured_height_gets_a_default_and_an_empty_graph_an_empty_world()
    {
        var layout = RunCanvasLayout.Of(new RunGraph([Node("a", RunNodeKind.Stage, 1)], [], BranchesServed: false), Only());

        Assert.True(layout.Rects["a"].Height > 0);
        Assert.Equal(default, RunCanvasLayout.Of(new RunGraph([], [], BranchesServed: false), Only()).World);
    }

    [Fact]
    public void The_same_input_gives_the_same_layout()
    {
        var first = RunCanvasLayout.Of(Sample(), Heights());
        var second = RunCanvasLayout.Of(Sample(), Heights());

        Assert.Equal(first.World, second.World);
        Assert.Equal(first.Rects.OrderBy(r => r.Key, StringComparer.Ordinal), second.Rects.OrderBy(r => r.Key, StringComparer.Ordinal));
        Assert.Equal(first.Ports.OrderBy(r => r.Key, StringComparer.Ordinal), second.Ports.OrderBy(r => r.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void Each_edge_leaves_and_arrives_on_the_sides_the_prototype_draws()
    {
        var ports = RunCanvasLayout.Of(Sample(), Heights()).Ports;

        Assert.Equal(new EdgePorts(PortSide.Bottom, PortSide.Top), ports["scoping>design"]);
        Assert.Equal(new EdgePorts(PortSide.Bottom, PortSide.Top), ports["phase>done"]);
        Assert.Equal(new EdgePorts(PortSide.Right, PortSide.Top), ports["phase>stuck"]);
        Assert.Equal(new EdgePorts(PortSide.Left, PortSide.Top), ports["scoping>dropped"]);
        Assert.Equal(new EdgePorts(PortSide.Left, PortSide.Top), ports["redesign>plan"]);
    }

    [Fact]
    public void An_edge_out_to_the_loop_card_leaves_the_right_and_arrives_at_its_bottom()
    {
        var graph = Sample() with { Edges = [Edge("plan", "redesign", RunEdgeKind.Loop)] };

        Assert.Equal(new EdgePorts(PortSide.Right, PortSide.Bottom), RunCanvasLayout.Of(graph, Heights()).Ports["plan>redesign"]);
    }
}
